namespace AJut.Text.AJson
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// The reflection path's constructor route: how a type that has no parameterless constructor, or that names one with
    /// <see cref="AJsonConstructorAttribute"/>, gets built from json. The source generator follows the same rules at compile
    /// time, and <see cref="AJsonConstructorAttribute"/> documents them.
    /// </summary>
    /// <remarks>
    /// Only construction happens here. Whatever the constructor did not take is filled in afterwards the way it is for every
    /// other type, property by property.
    /// </remarks>
    internal static class AJsonConstructorRoute
    {
        /// <summary>
        /// The compiler gives every record class this method, and nothing else can declare a member with that name
        /// </summary>
        private const string kRecordCloneMethodName = "<Clone>$";

        private const BindingFlags kInstanceConstructors = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string kTrimJustification = "Reflection path; the trim-safe path is [OptimizeAJson], whose generated reader calls the constructor directly.";

        /// <summary>
        /// FindRoute as a delegate, made once rather than on every GetOrAdd
        /// </summary>
        private static readonly Func<Type, Route> kFindRoute = FindRoute;

        private static readonly IReadOnlySet<string> kNoJsonKeys = new HashSet<string>();

        // Both dictionaries are filled on first use from any thread, so both are concurrent
        private static readonly ConcurrentDictionary<Type, Route> g_routes = new ConcurrentDictionary<Type, Route>();
        private static readonly ConcurrentDictionary<Type, byte> g_customConstructorChecked = new ConcurrentDictionary<Type, byte>();

        // ===========================[ Public Interface Methods ]===========================

        /// <summary>
        /// Builds <paramref name="type"/> through its constructor route, with each constructor argument read from the json key of
        /// the property it matches
        /// </summary>
        /// <returns>True if the type has a constructor route and <paramref name="instance"/> was built through it, false if the
        /// type should be built the way any other type is</returns>
        /// <param name="consumedJsonKeys">The json keys the constructor took as arguments, for the property fill to leave alone; null when false is returned</param>
        public static bool TryConstruct (Type type, JsonValue jsonValue, JsonInterpreterSettings settings, Json owner, out object instance, out IReadOnlySet<string> consumedJsonKeys)
        {
            instance = null;
            consumedJsonKeys = null;
            Route route = g_routes.GetOrAdd(type, kFindRoute);
            if (route.IsAmbiguous)
            {
                owner?.AddError($"Type '{type.FullName}' has more than one constructor marked [AJsonConstructor], so there is no way to tell which to use. Mark exactly one.");
                return false;
            }

            if (route.Constructor == null)
            {
                return false;
            }

            instance = route.Constructor.Invoke(BuildArguments(route, jsonValue, settings, owner));
            consumedJsonKeys = route.ConsumedJsonKeys;
            return true;
        }

        /// <summary>
        /// The json keys <paramref name="type"/>'s constructor route takes as arguments, empty when it has no route. The writer
        /// keeps writing a get-only member whose key is here, since the constructor is how the reader gets its value back.
        /// </summary>
        public static IReadOnlySet<string> GetConsumedJsonKeys (Type type)
        {
            Route route = g_routes.GetOrAdd(type, kFindRoute);
            return route.Constructor != null ? route.ConsumedJsonKeys : kNoJsonKeys;
        }

        /// <summary>
        /// Call when a constructor registered with <see cref="JsonInterpreterSettings"/> built an instance of <paramref name="type"/>.
        /// The registered constructor wins over one marked [AJsonConstructor], so the first time this happens for a type that
        /// marks one, it is logged as a warning.
        /// </summary>
        public static void NoteCustomConstructorWon (Type type)
        {
            // This runs for every value the built-in constructors read (each Guid, DateTime and so on), and TryAdd takes a lock
            //  even when the key is already there, so the lock-free check comes first
            if (g_customConstructorChecked.ContainsKey(type))
            {
                return;
            }

            if (g_customConstructorChecked.TryAdd(type, 0)
                && g_routes.GetOrAdd(type, kFindRoute).MarkedConstructorCount > 0)
            {
                Logger.LogInfo($"[WARNING] AJson: '{type.FullName}' has a constructor marked [AJsonConstructor], but a custom constructor is also registered for it with JsonInterpreterSettings. The registered one wins, so the marked constructor is not used.");
            }
        }

        // ===========================[ Helper Methods ]===========================

        [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = kTrimJustification)]
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = kTrimJustification)]
        private static Route FindRoute (Type type)
        {
            if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
            {
                return Route.None(0);
            }

            ConstructorInfo[] constructors = type.GetConstructors(kInstanceConstructors);
            ConstructorInfo[] marked = constructors.Where(c => c.IsDefined(typeof(AJsonConstructorAttribute), inherit: false)).ToArray();

            // 1. A parameterless constructor is used the way it always has been, and a value type can always be built without
            //  one, unless it marks a constructor to use instead
            bool hasParameterless = constructors.Any(c => !c.IsPrivate && c.GetParameters().Length == 0);
            if (hasParameterless || (type.IsValueType && marked.Length == 0))
            {
                return Route.None(marked.Length);
            }

            // 2. Exactly one marked constructor
            if (marked.Length > 1)
            {
                return new Route(null, Array.Empty<RouteParameter>(), marked.Length, isAmbiguous: true);
            }

            ConstructorInfo chosen = marked.Length == 1 ? marked[0] : null;

            // The same members the reflection path reads and writes
            JsonHelper.DataMember[] members = JsonHelper.GetDataMembers(type);

            // 3. A record's positional constructor, which needs no attribute
            if (chosen == null && type.GetMethod(kRecordCloneMethodName, BindingFlags.Public | BindingFlags.Instance) != null)
            {
                chosen = FindPositionalRecordConstructor(type, constructors, members);
            }

            // 4. Nothing to use, so the type is built like any other (and fails like any other with no parameterless constructor)
            if (chosen == null)
            {
                return Route.None(marked.Length);
            }

            ParameterInfo[] parameters = chosen.GetParameters();
            RouteParameter[] routeParameters = new RouteParameter[parameters.Length];
            for (int index = 0; index < parameters.Length; ++index)
            {
                routeParameters[index] = BuildRouteParameter(parameters[index], FindMatchingMember(members, parameters[index].Name));
            }

            return new Route(chosen, routeParameters, marked.Length, isAmbiguous: false);
        }

        /// <summary>
        /// The public constructor, other than the copy constructor, whose every parameter matches a member. A record compiled
        /// from source has exactly one (its primary constructor), but reflection has no way to tell which constructor that was,
        /// so more than one candidate means no route.
        /// </summary>
        private static ConstructorInfo FindPositionalRecordConstructor (Type type, ConstructorInfo[] constructors, JsonHelper.DataMember[] members)
        {
            ConstructorInfo found = null;
            foreach (ConstructorInfo constructor in constructors)
            {
                if (!constructor.IsPublic)
                {
                    continue;
                }

                ParameterInfo[] parameters = constructor.GetParameters();
                if (parameters.Length == 0
                    || (parameters.Length == 1 && parameters[0].ParameterType == type))
                {
                    continue;
                }

                if (!parameters.All(p => FindMatchingMember(members, p.Name) != null))
                {
                    continue;
                }

                if (found != null)
                {
                    return null;
                }

                found = constructor;
            }

            return found;
        }

        private static RouteParameter BuildRouteParameter (ParameterInfo parameter, JsonHelper.DataMember matchedMember)
        {
            // A key the json does not have is never an error, since nulls are never written. The value it takes is the one the
            //  writer leaves out, when the matched member has [JsonOmitIfDefault]: its explicit value, or the type's default for
            //  the bare attribute. Otherwise the parameter's own default, then the type's default. The type's default is null here,
            //  which Invoke passes to a value type as its default. A default registered with
            //  JsonBuilderSettings.RegisterDefaultEquivalent cannot be used, since the reader never sees the writer's settings.
            object missingValue = null;
            JsonOmitIfDefaultAttribute omit = matchedMember?.Info.GetCustomAttribute<JsonOmitIfDefaultAttribute>(inherit: true);
            if (omit != null)
            {
                missingValue = omit.HasExplicitDefault ? CoerceTo(omit.ExplicitDefault, parameter.ParameterType) : null;
            }
            else if (parameter.HasDefaultValue)
            {
                missingValue = CoerceTo(parameter.DefaultValue, parameter.ParameterType);
            }

            return new RouteParameter(
                parameter.ParameterType,
                matchedMember?.JsonKey,
                matchedMember?.Info.GetCustomAttribute<JsonRuntimeTypeEvalAttribute>(inherit: false) != null,
                missingValue
            );
        }

        private static object[] BuildArguments (Route route, JsonValue jsonValue, JsonInterpreterSettings settings, Json owner)
        {
            RouteParameter[] parameters = route.Parameters;
            object[] arguments = new object[parameters.Length];
            for (int index = 0; index < parameters.Length; ++index)
            {
                arguments[index] = parameters[index].MissingValue;
            }

            if (jsonValue is JsonDocument document)
            {
                // Every key is visited, so a key the json repeats ends with its last value, the same as a property set from it
                foreach (KeyValuePair<string, JsonValue> kvp in document)
                {
                    for (int index = 0; index < parameters.Length; ++index)
                    {
                        if (parameters[index].JsonKey == kvp.Key)
                        {
                            arguments[index] = ReadArgument(parameters[index], kvp.Value, settings, owner);
                        }
                    }
                }
            }

            return arguments;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = kTrimJustification)]
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = kTrimJustification)]
        private static object ReadArgument (RouteParameter parameter, JsonValue value, JsonInterpreterSettings settings, Json owner)
        {
            // [JsonRuntimeTypeEval] wraps the value with its runtime type id, the same as when it fills a property
            if (parameter.IsRuntimeTypeEval
                && value is JsonDocument wrapper
                && wrapper.TryGetValue(JsonDocument.kTypeIndicator, out string runtimeTypeId)
                && wrapper.ValueFor(JsonDocument.kRuntimeTypeEvalValue) is JsonValue wrappedValue
                && JsonHelper.TryGetTypeForTypeId(runtimeTypeId, out Type runtimeType))
            {
                object built = JsonHelper.BuildObjectForJson(runtimeType, wrappedValue, settings, owner);
                if (built != null)
                {
                    return built;
                }
            }

            return JsonHelper.BuildObjectForJson(parameter.Type, value, settings, owner);
        }

        /// <summary>
        /// Puts an attribute or default value into the parameter's type. An enum value is stored as its underlying number, so it
        /// goes through Enum.ToObject the way the omit check does it, and a number of another width is converted, the way the
        /// compiler converts it in generated code.
        /// </summary>
        private static object CoerceTo (object value, Type parameterType)
        {
            Type targetType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
            if (value == null || targetType.IsInstanceOfType(value))
            {
                return value;
            }

            if (targetType.IsEnum && IsIntegral(value.GetType()))
            {
                return Enum.ToObject(targetType, value);
            }

            if (IsNumeric(targetType) && IsNumeric(value.GetType()))
            {
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }

            return value;
        }

        private static bool IsIntegral (Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return !type.IsEnum;
            }

            return false;
        }

        private static bool IsNumeric (Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
            }

            return IsIntegral(type);
        }

        /// <summary>
        /// The property or field a constructor parameter fills: the same name, preferring an exact match over one that differs only
        /// by case
        /// </summary>
        private static JsonHelper.DataMember FindMatchingMember (JsonHelper.DataMember[] members, string parameterName)
        {
            JsonHelper.DataMember caseInsensitiveMatch = null;
            foreach (JsonHelper.DataMember member in members)
            {
                if (member.Info.Name == parameterName)
                {
                    return member;
                }

                if (caseInsensitiveMatch == null && string.Equals(member.Info.Name, parameterName, StringComparison.OrdinalIgnoreCase))
                {
                    caseInsensitiveMatch = member;
                }
            }

            return caseInsensitiveMatch;
        }

        // ===========================[ Subclasses/structs ]===========================

        /// <summary>
        /// What was found for one type. <see cref="Constructor"/> is null when the type has no constructor route.
        /// </summary>
        private sealed class Route
        {
            public Route (ConstructorInfo constructor, RouteParameter[] parameters, int markedConstructorCount, bool isAmbiguous)
            {
                this.Constructor = constructor;
                this.Parameters = parameters;
                this.MarkedConstructorCount = markedConstructorCount;
                this.IsAmbiguous = isAmbiguous;

                this.ConsumedJsonKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (RouteParameter parameter in parameters)
                {
                    if (parameter.JsonKey != null)
                    {
                        this.ConsumedJsonKeys.Add(parameter.JsonKey);
                    }
                }
            }

            public static Route None (int markedConstructorCount) => new Route(null, Array.Empty<RouteParameter>(), markedConstructorCount, isAmbiguous: false);

            public ConstructorInfo Constructor { get; }
            public RouteParameter[] Parameters { get; }

            /// <summary>
            /// The json keys the constructor takes as arguments. The property fill that runs after construction leaves these alone,
            /// so what the constructor did with a value stands.
            /// </summary>
            public HashSet<string> ConsumedJsonKeys { get; }

            /// <summary>
            /// How many constructors carry [AJsonConstructor], counted even when the type is built some other way
            /// </summary>
            public int MarkedConstructorCount { get; }

            /// <summary>
            /// True when the type needs its marked constructor, but marks more than one
            /// </summary>
            public bool IsAmbiguous { get; }
        }

        private sealed class RouteParameter
        {
            public RouteParameter (Type type, string jsonKey, bool isRuntimeTypeEval, object missingValue)
            {
                this.Type = type;
                this.JsonKey = jsonKey;
                this.IsRuntimeTypeEval = isRuntimeTypeEval;
                this.MissingValue = missingValue;
            }

            public Type Type { get; }

            /// <summary>
            /// The json key of the matched property, or null when no property matches and the argument is always its missing value
            /// </summary>
            public string JsonKey { get; }

            public bool IsRuntimeTypeEval { get; }

            /// <summary>
            /// What the argument is when the json has no key for it
            /// </summary>
            public object MissingValue { get; }
        }
    }
}
