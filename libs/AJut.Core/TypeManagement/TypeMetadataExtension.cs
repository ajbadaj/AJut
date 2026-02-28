namespace AJut.TypeManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Per-type metadata extension - fluent builder returned by
    /// <see cref="TypeMetadataExtensionRegistrar.For{T}()"/>. Accumulates hidden member names,
    /// tier/member ordering overrides, and attribute overlays for the target type.
    /// All mutations automatically invalidate the registrar's ordering cache for this type.
    /// </summary>
    public sealed class TypeMetadataExtension
    {
        // ===========[ Instance fields ]==========================================
        private readonly Type m_type;
        private readonly HashSet<string> m_hiddenMembers = new();
        private bool m_hasTierOrder;
        private int m_tierOrder;
        private readonly Dictionary<string, int> m_memberOrders = new();
        private readonly Dictionary<string, List<Attribute>> m_memberAttributes = new();
        private readonly List<Attribute> m_typeAttributes = new();

        // ===========[ Construction ]=============================================
        internal TypeMetadataExtension (Type type)
        {
            m_type = type;
        }

        // ===========[ Properties ]===============================================
        internal Type TargetType => m_type;

        // ===========[ Public Interface Methods ]==========================================

        /// <summary>
        /// Marks one or more members as globally hidden (excluded from PropertyGrid, AJson, and
        /// any other system that queries <see cref="TypeMetadataExtensionRegistrar.IsHidden"/>).
        /// </summary>
        public TypeMetadataExtension Hide (params string[] memberNames)
        {
            foreach (string name in memberNames)
            {
                m_hiddenMembers.Add(name);
            }

            TypeMetadataExtensionRegistrar.InvalidateOrderCacheFor(m_type);
            return this;
        }

        /// <summary>Removes a previously registered hide for the named member.</summary>
        public TypeMetadataExtension Unhide (params string[] memberNames)
        {
            foreach (string name in memberNames)
            {
                m_hiddenMembers.Remove(name);
            }

            TypeMetadataExtensionRegistrar.InvalidateOrderCacheFor(m_type);
            return this;
        }

        /// <summary>
        /// Sets the tier order priority for all members declared directly on this type.
        /// Lower values appear first. Overrides <see cref="TypeTierOrderAttribute"/> when set.
        /// </summary>
        public TypeMetadataExtension SetTierOrder (int order)
        {
            m_hasTierOrder = true;
            m_tierOrder = order;
            TypeMetadataExtensionRegistrar.InvalidateOrderCacheFor(m_type);
            return this;
        }

        /// <summary>
        /// Sets the ordering priority for a specific member within its tier.
        /// Lower values appear first. Overrides <see cref="MemberOrderAttribute"/> when set.
        /// </summary>
        public TypeMetadataExtension SetMemberOrder (string memberName, int order)
        {
            m_memberOrders[memberName] = order;
            TypeMetadataExtensionRegistrar.InvalidateOrderCacheFor(m_type);
            return this;
        }

        /// <summary>
        /// Registers an attribute overlay for a specific member. Registry attributes take priority
        /// over reflected attributes when queried via <see cref="TypeMetadataExtensionRegistrar.GetAttribute{TAttr}"/>.
        /// </summary>
        public TypeMetadataExtension AddAttribute (string memberName, Attribute attribute)
        {
            if (!m_memberAttributes.TryGetValue(memberName, out List<Attribute> list))
            {
                list = new List<Attribute>();
                m_memberAttributes[memberName] = list;
            }

            list.Add(attribute);
            return this;
        }

        /// <summary>
        /// Registers a type-level attribute overlay. Registry attributes take priority over
        /// reflected attributes when queried via <see cref="TypeMetadataExtensionRegistrar.GetAttribute{TAttr}"/>.
        /// </summary>
        public TypeMetadataExtension AddAttribute (Attribute attribute)
        {
            m_typeAttributes.Add(attribute);
            return this;
        }

        /// <summary>Clears all registered attributes for a specific member.</summary>
        public TypeMetadataExtension ClearMemberAttributes (string memberName)
        {
            m_memberAttributes.Remove(memberName);
            return this;
        }

        // ===========[ Internal query interface used by TypeMetadataExtensionRegistrar ]=====

        internal bool IsMemberHidden (string memberName) => m_hiddenMembers.Contains(memberName);

        internal bool TryGetTierOrder (out int order)
        {
            order = m_tierOrder;
            return m_hasTierOrder;
        }

        internal bool TryGetMemberOrder (string memberName, out int order)
            => m_memberOrders.TryGetValue(memberName, out order);

        internal IEnumerable<TAttr> GetMemberAttributes<TAttr> (string memberName) where TAttr : Attribute
        {
            if (m_memberAttributes.TryGetValue(memberName, out List<Attribute> list))
            {
                return list.OfType<TAttr>();
            }

            return Enumerable.Empty<TAttr>();
        }

        internal IEnumerable<TAttr> GetTypeAttributes<TAttr> () where TAttr : Attribute
            => m_typeAttributes.OfType<TAttr>();
    }
}
