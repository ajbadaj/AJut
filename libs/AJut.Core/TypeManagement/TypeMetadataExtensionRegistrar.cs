namespace AJut.TypeManagement
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;
    using AJut.Text.AJson;

    /// <summary>
    /// Central registry for type metadata extensions. Provides deterministic member ordering
    /// (derived-first by default), member visibility (globally hiding members from all consuming
    /// systems), and attribute overlays for types you do not control.
    ///
    /// All consuming systems (PropertyGrid, AJson, etc.) query this registrar instead of going
    /// directly to reflection, so registrations here affect all of them uniformly.
    ///
    /// Use <see cref="For{T}()"/> to get a fluent builder for a type. The ordering cache is
    /// invalidated automatically whenever a registration is modified.
    ///
    /// Queries are safe from any number of threads, including the first query for a type. Make
    /// registrations up front: changing a type's registration, calling <see cref="ClearAll"/> or
    /// changing <see cref="DefaultMemberOrdering"/> while another thread is querying is not
    /// supported, since a query already in flight can put back what the change just cleared.
    /// </summary>
    public static class TypeMetadataExtensionRegistrar
    {
        // ===========[ Const-like ]==========================================
        private const BindingFlags kDefaultPropertyFlags
            = BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty;
        private const BindingFlags kPublicInstanceMembers = BindingFlags.Public | BindingFlags.Instance;

        // ===========[ Static fields ]==========================================
        // These maps are concurrent because the first use of a type writes the order caches from whatever
        //  thread got there, and AJson calls GetOrderedPropertiesAndFields from inside its own
        //  ConcurrentDictionary.GetOrAdd factory, which runs outside any lock. Two threads building
        //  json for new types at once (the same type or two different ones) write this cache at the
        //  same time, and concurrent writes can corrupt a plain Dictionary for the rest of the
        //  process: later lookups throw or hang. Two racing computations of one entry both produce
        //  the same order, so last-write-wins is fine.
        private static readonly ConcurrentDictionary<Type, TypeMetadataExtension> g_extensions = new();
        private static readonly ConcurrentDictionary<(Type, BindingFlags), PropertyInfo[]> g_orderCache = new();
        private static readonly ConcurrentDictionary<Type, MemberInfo[]> g_propertyAndFieldOrderCache = new();
        private static eMemberInheritanceOrdering g_defaultMemberOrdering = eMemberInheritanceOrdering.BaseFirst;

        // ===========[ Global ordering default ]==========================================

        /// <summary>
        /// Controls the default tier ordering when no explicit tier order is registered or attributed.
        /// Defaults to <see cref="eMemberInheritanceOrdering.BaseFirst"/> (base-class members first).
        /// Changing this clears the entire ordering cache so all subsequent <see cref="GetOrderedProperties"/>
        /// calls reflect the new default, including types whose ordering was already cached.
        /// Explicit <see cref="TypeMetadataExtension.SetTierOrder"/> registrations and
        /// <see cref="TypeTierOrderAttribute"/> annotations are never affected by this setting.
        /// </summary>
        public static eMemberInheritanceOrdering DefaultMemberOrdering
        {
            get => g_defaultMemberOrdering;
            set
            {
                if (g_defaultMemberOrdering != value)
                {
                    g_defaultMemberOrdering = value;
                    g_orderCache.Clear();
                    g_propertyAndFieldOrderCache.Clear();
                    JsonHelper.ClearMemberCaches();
                }
            }
        }

        // ===========[ Registration ]==========================================

        /// <summary>Returns the <see cref="TypeMetadataExtension"/> for <typeparamref name="T"/>,
        /// creating it if necessary. Repeat calls for the same type accumulate registrations.</summary>
        public static TypeMetadataExtension For<T> () => For(typeof(T));

        /// <summary>Returns the <see cref="TypeMetadataExtension"/> for <paramref name="type"/>,
        /// creating it if necessary. Repeat calls for the same type accumulate registrations.</summary>
        public static TypeMetadataExtension For (Type type)
        {
            return g_extensions.GetOrAdd(type, static t => new TypeMetadataExtension(t));
        }

        // ===========[ Clearing ]==========================================

        /// <summary>Removes all registered metadata extensions for <paramref name="type"/>.</summary>
        public static void ClearFor (Type type)
        {
            g_extensions.TryRemove(type, out _);
            InvalidateOrderCacheFor(type);
        }

        /// <summary>
        /// Removes all registered metadata extensions and clears all ordering caches.
        /// Does not reset <see cref="DefaultMemberOrdering"/> -- that is a program-level preference
        /// independent of per-type registrations. Reset it explicitly if needed.
        /// </summary>
        public static void ClearAll ()
        {
            g_extensions.Clear();
            g_orderCache.Clear();
            g_propertyAndFieldOrderCache.Clear();
            JsonHelper.ClearMemberCaches();
        }

        // ===========[ Ordering ]==========================================

        /// <summary>
        /// Returns the public instance properties of <paramref name="type"/> in deterministic order:
        /// most-derived class members first, then each base class in turn. Within each tier, members
        /// are sorted by registered order, <see cref="MemberOrderAttribute"/>, or MetadataToken
        /// (source declaration order) in descending priority.
        ///
        /// Results are cached per (type, flags) pair. The cache is invalidated when registrations
        /// change for the type.
        /// </summary>
        public static IEnumerable<PropertyInfo> GetOrderedProperties (
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] Type type,
            BindingFlags flags = kDefaultPropertyFlags)
        {
            if (g_orderCache.TryGetValue((type, flags), out PropertyInfo[] cached))
            {
                return cached;
            }

            PropertyInfo[] resultArray = _OrderByTier(type, type.GetProperties(flags));
            g_orderCache[(type, flags)] = resultArray;
            return resultArray;
        }

        /// <summary>
        /// Returns the public instance properties and fields of <paramref name="type"/>, ordered the same way
        /// <see cref="GetOrderedProperties"/> orders properties. Within a tier, a field with no explicit order comes before a
        /// property with none, since its MetadataToken is lower: a token's top byte names its metadata table, and the field
        /// table comes before the property table.
        ///
        /// Results are cached per type, and invalidated when registrations change for the type.
        /// </summary>
        public static IEnumerable<MemberInfo> GetOrderedPropertiesAndFields (
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] Type type)
        {
            if (g_propertyAndFieldOrderCache.TryGetValue(type, out MemberInfo[] cached))
            {
                return cached;
            }

            MemberInfo[] allMembers = type.GetProperties(kPublicInstanceMembers)
                .Concat<MemberInfo>(type.GetFields(kPublicInstanceMembers))
                .ToArray();

            MemberInfo[] resultArray = _OrderByTier(type, allMembers);
            g_propertyAndFieldOrderCache[type] = resultArray;
            return resultArray;
        }

        // ===========[ Visibility ]==========================================

        /// <summary>
        /// Returns true if <paramref name="member"/> was explicitly hidden via
        /// <see cref="TypeMetadataExtension.Hide"/>. Does not check system-specific
        /// attributes like [PGHidden] or [JsonIgnore] - those are checked by their
        /// respective systems independently.
        /// </summary>
        public static bool IsHidden (MemberInfo member)
        {
            if (g_extensions.TryGetValue(member.DeclaringType, out TypeMetadataExtension ext))
            {
                return ext.IsMemberHidden(member.Name);
            }

            return false;
        }

        // ===========[ Attribute overlay ]==========================================

        /// <summary>
        /// Returns the first registered attribute of type <typeparamref name="TAttr"/> for
        /// <paramref name="member"/>, falling back to reflected attributes if none is registered.
        /// Registry attributes take priority over reflected ones.
        /// </summary>
        public static TAttr GetAttribute<TAttr> (MemberInfo member) where TAttr : Attribute
            => GetAttributes<TAttr>(member).FirstOrDefault();

        /// <summary>
        /// Returns all attributes of type <typeparamref name="TAttr"/> for <paramref name="member"/>:
        /// registered attributes first, then reflected attributes.
        /// </summary>
        public static IEnumerable<TAttr> GetAttributes<TAttr> (MemberInfo member) where TAttr : Attribute
        {
            IEnumerable<TAttr> registered = _GetRegisteredAttributes<TAttr>(member);
            IEnumerable<TAttr> reflected = member.GetCustomAttributes<TAttr>(inherit: true);
            return registered.Concat(reflected);
        }

        /// <summary>
        /// Returns true if any attribute of type <typeparamref name="TAttr"/> exists for
        /// <paramref name="member"/> (registered or reflected).
        /// </summary>
        public static bool HasAttribute<TAttr> (MemberInfo member) where TAttr : Attribute
            => GetAttribute<TAttr>(member) != null;

        // ===========[ Internal cache invalidation ]==========================================

        internal static void InvalidateOrderCacheFor (Type type)
        {
            // Remove the entries for this type and every type derived from it, under any BindingFlags
            //  combination. A derived type's order is built from its bases' tier and member orders,
            //  and its property list carries the bases' members, so a registration on a base changes
            //  the derived types' results too.
            List<(Type, BindingFlags)> toRemove = g_orderCache.Keys
                .Where(k => type.IsAssignableFrom(k.Item1))
                .ToList();

            foreach (var key in toRemove)
            {
                g_orderCache.TryRemove(key, out _);
            }

            foreach (Type cachedType in g_propertyAndFieldOrderCache.Keys)
            {
                if (type.IsAssignableFrom(cachedType))
                {
                    g_propertyAndFieldOrderCache.TryRemove(cachedType, out _);
                }
            }

            // AJson keeps its own per-type member lists, filtered by IsHidden and taken from
            //  GetOrderedPropertiesAndFields, so they go stale the same way.
            JsonHelper.InvalidateMemberCachesFor(type);
        }

        // ===========[ Private helpers ]==========================================

        private static TMember[] _OrderByTier<TMember> (Type type, TMember[] allMembers) where TMember : MemberInfo
        {
            // 1. Build inheritance chain from most-derived to object (exclusive)
            var chain = new List<Type>();
            Type current = type;
            while (current != null && current != typeof(object))
            {
                chain.Add(current);
                current = current.BaseType;
            }

            // 2. Sort the tiers by their priority
            int chainLength = chain.Count;
            var tiersWithOrder = chain
                .Select((tier, depth) => (tier, order: _GetTierOrder(tier, depth, chainLength)))
                .OrderBy(x => x.order);

            // 3. Within each tier, collect and sort its own members.
            //    Two-key sort: explicitly ordered members always precede unattributed ones,
            //    regardless of the magnitude of the explicit order value. This prevents MetadataToken
            //    values (which are in the hundreds-of-millions range) from interleaving with large
            //    explicit orders. Within each group, sort by the order value or MetadataToken.
            var result = new List<TMember>();
            foreach (var (tier, _) in tiersWithOrder)
            {
                IEnumerable<TMember> tierMembers = allMembers
                    .Where(m => m.DeclaringType == tier)
                    .OrderBy(m => _HasExplicitMemberOrder(tier, m) ? 0 : 1)
                    .ThenBy(m => _GetMemberOrder(tier, m));
                result.AddRange(tierMembers);
            }

            return result.ToArray();
        }

        private static int _GetTierOrder (Type tier, int depthIndex, int chainLength)
        {
            // 1. Registry registration (highest priority - unaffected by DefaultMemberOrdering)
            if (g_extensions.TryGetValue(tier, out TypeMetadataExtension ext)
                && ext.TryGetTierOrder(out int registeredOrder))
            {
                return registeredOrder;
            }

            // 2. [TypeTierOrder] attribute (unaffected by DefaultMemberOrdering)
            var attr = tier.GetCustomAttribute<TypeTierOrderAttribute>(inherit: false);
            if (attr != null)
            {
                return attr.Order;
            }

            // 3. Default: governed by DefaultMemberOrdering.
            //    ConcreteToBase: depthIndex directly (0 = most derived = appears first).
            //    BaseToConcrete: flip so the deepest base (chainLength-1) maps to 0 (appears first).
            return g_defaultMemberOrdering == eMemberInheritanceOrdering.DerivedFirst
                ? depthIndex
                : (chainLength - 1 - depthIndex);
        }

        private static bool _HasExplicitMemberOrder (Type declaringTier, MemberInfo member)
        {
            if (g_extensions.TryGetValue(declaringTier, out TypeMetadataExtension ext)
                && ext.TryGetMemberOrder(member.Name, out _))
            {
                return true;
            }

            return member.GetCustomAttribute<MemberOrderAttribute>(inherit: false) != null;
        }

        private static int _GetMemberOrder (Type declaringTier, MemberInfo member)
        {
            // 1. Registry registration (highest priority)
            if (g_extensions.TryGetValue(declaringTier, out TypeMetadataExtension ext)
                && ext.TryGetMemberOrder(member.Name, out int registeredOrder))
            {
                return registeredOrder;
            }

            // 2. [MemberOrder] attribute
            var attr = member.GetCustomAttribute<MemberOrderAttribute>(inherit: false);
            if (attr != null)
            {
                return attr.Order;
            }

            // 3. Default: source declaration order via MetadataToken
            return member.MetadataToken;
        }

        private static IEnumerable<TAttr> _GetRegisteredAttributes<TAttr> (MemberInfo member)
            where TAttr : Attribute
        {
            if (!g_extensions.TryGetValue(member.DeclaringType, out TypeMetadataExtension ext))
            {
                return Enumerable.Empty<TAttr>();
            }

            if (member is TypeInfo)
            {
                return ext.GetTypeAttributes<TAttr>();
            }

            return ext.GetMemberAttributes<TAttr>(member.Name);
        }
    }
}
