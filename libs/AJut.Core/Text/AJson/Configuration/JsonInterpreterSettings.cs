namespace AJut.Text.AJson
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Numerics;
    using AJut;
    using AJut.Text;
    using AJut.TypeManagement;

    /// <summary>
    /// A delegate used to construct an instance from a json value (given interpreter settings).
    /// V2 adds the owning Json so error reporting goes through Json.Errors instead of throwing.
    /// </summary>
    public delegate object JsonToObjectConstructor (Type fullTarget, JsonValue value, JsonInterpreterSettings settings, Json owner);

    /// <summary>
    /// Settings used to determine how to interpret json and create object instances. V2 typo-fix
    /// of V1's JsonInterpretterSettings (double-t) - V2 uses JsonInterpreterSettings.
    /// </summary>
    public class JsonInterpreterSettings
    {
        private readonly Dictionary<Type, JsonToObjectConstructor> m_customConstructors = new Dictionary<Type, JsonToObjectConstructor>();

        // ===========================[ Construction ]===============================
        public JsonInterpreterSettings (StringParser stringParser = null)
        {
            this.StringParser = stringParser ?? new StringParser();
            m_customConstructors.Add(typeof(Guid), _CreateGuidFor);
            m_customConstructors.Add(typeof(DateTime), _CreateDateTimeFor);
            m_customConstructors.Add(typeof(TimeSpan), _CreateTimeSpanFor);
            m_customConstructors.Add(typeof(KeyValuePair<,>), _CreateKeyValuePairFor);
            m_customConstructors.Add(typeof(TimeZoneInfo), _CreateTimezoneInfo);
            m_customConstructors.Add(typeof(Vector2), _CreateVector2);

            object _CreateGuidFor (Type fullTarget, JsonValue json, JsonInterpreterSettings settings, Json owner)
            {
                return Guid.TryParse(json.StringValue, out Guid found) ? found : Guid.Empty;
            }

            object _CreateDateTimeFor (Type fullTarget, JsonValue json, JsonInterpreterSettings settings, Json owner)
            {
                return DateTime.TryParse(json.StringValue, CultureInfo.CurrentCulture.DateTimeFormat, this.DefaultDateTimeParseStyle, out DateTime found) ? found : default;
            }

            object _CreateTimeSpanFor (Type fullTarget, JsonValue json, JsonInterpreterSettings settings, Json owner)
            {
                return TimeSpan.TryParse(json.StringValue, out TimeSpan found) ? found : TimeSpan.Zero;
            }

            // KVP construction goes through Type.GetConstructor on a generic KeyValuePair<,> the
            // trimmer can't statically verify; KeyValuePair<,> is a closed system shape and the
            // ctor is always present, so the suppression is safe in practice.
            [UnconditionalSuppressMessage("Trimming", "IL2075",
                Justification = "KeyValuePair<,> ctor is intrinsic and always preserved; the FindBaseTypeOrInterface return is a constructed KeyValuePair<,> by definition of the call site.")]
            [UnconditionalSuppressMessage("Trimming", "IL2067",
                Justification = "KVP element types come from generic arguments of the KeyValuePair<,> the consumer requested - keeping members of those is the consumer's responsibility per AJson reflection-path contract.")]
            object _CreateKeyValuePairFor (Type fullTarget, JsonValue json, JsonInterpreterSettings settings, Json owner)
            {
                Type kvpType = fullTarget.FindBaseTypeOrInterface(typeof(KeyValuePair<,>));
                Type[] genericTypes = kvpType.GetGenericArguments();

                if (!json.IsDocument)
                {
                    owner?.AddError($"KeyValuePair source must be a document - got {(json.IsArray ? "array" : "value")}");
                    return kvpType.GetConstructor(genericTypes).Invoke(new object[] { _DefaultFor(genericTypes[0]), _DefaultFor(genericTypes[1]) });
                }

                JsonDocument doc = (JsonDocument)json;
                Type keyType = null;
                if (doc.TryGetValue(JsonDocument.kKVPKeyTypeIndicator, out string keyTypeId))
                {
                    JsonHelper.TryGetTypeForTypeId(keyTypeId, out keyType);
                }

                Type valueType = null;
                if (doc.TryGetValue(JsonDocument.kKVPValueTypeIndicator, out string valueTypeId))
                {
                    JsonHelper.TryGetTypeForTypeId(valueTypeId, out valueType);
                }

                keyType = keyType ?? genericTypes[0];
                valueType = valueType ?? genericTypes[1];

                JsonValue keyJson = doc.ValueFor("Key");
                JsonValue valueJson = doc.ValueFor("Value");

                // V2 Phase C fold-in: missing-value contract is now errors-or-default rather than throw.
                if (keyJson == null)
                {
                    owner?.AddError("KeyValuePair source missing 'Key' field");
                }
                if (valueJson == null)
                {
                    owner?.AddError("KeyValuePair source missing 'Value' field");
                }

                object keyObj = keyJson != null
                    ? JsonHelper.BuildObjectForJson(keyType, keyJson, settings)
                    : _DefaultFor(keyType);

                object valueObj = valueJson != null
                    ? JsonHelper.BuildObjectForJson(valueType, valueJson, settings)
                    : _DefaultFor(valueType);

                return kvpType.GetConstructor(genericTypes).Invoke(new[] { keyObj, valueObj });
            }

            object _CreateTimezoneInfo (Type fullTarget, JsonValue json, JsonInterpreterSettings settings, Json owner)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(json.StringValue);
                }
                catch (Exception exc)
                {
                    owner?.AddError($"Failed to resolve TimeZoneInfo '{json.StringValue}': {exc.Message}");
                    return TimeZoneInfo.Utc;
                }
            }

            object _CreateVector2 (Type fullTarget, JsonValue json, JsonInterpreterSettings settings, Json owner)
            {
                string[] xystrs = json.StringValue.Trim('<', '>').Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (xystrs.Length == 2
                    && float.TryParse(xystrs[0], out float x)
                    && float.TryParse(xystrs[1], out float y))
                {
                    return new Vector2(x, y);
                }

                return Vector2.Zero;
            }

            static object _DefaultFor ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;
        }

        // ===========================[ Properties ]===============================
        public static JsonInterpreterSettings Default { get; set; } = new JsonInterpreterSettings();

        public StringParser StringParser { get; }

        public DateTimeStyles DefaultDateTimeParseStyle { get; set; } = DateTimeStyles.AssumeUniversal;

        // ===========================[ Public Interface Methods ]===============================

        public void Add (Type t, JsonToObjectConstructor constructor)
        {
            m_customConstructors.Add(t, constructor);
        }

        /// <summary>
        /// Register a strongly-typed factory for a target type that does not have a parameterless
        /// constructor (or otherwise needs custom translation from a json value to an instance).
        /// The factory is consulted before the parameterless-constructor + property-assignment
        /// path runs.
        /// </summary>
        public void RegisterCustomConstructor<T> (Func<JsonValue, T> ctor)
        {
            m_customConstructors[typeof(T)] = (fullTarget, json, settings, owner) => ctor(json);
        }

        /// <summary>
        /// Construct an instance of the given type from the passed in json. Owner Json (if any)
        /// receives error reports from delegate constructors instead of throwing.
        /// </summary>
        public object ConstructInstanceFor ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type, JsonValue jsonValue, Json owner = null)
        {
            foreach (KeyValuePair<Type, JsonToObjectConstructor> kvp in m_customConstructors)
            {
                if (type.TargetsSameTypeAs(kvp.Key))
                {
                    return kvp.Value(type, jsonValue, this, owner);
                }
            }

            if (type == typeof(string))
            {
                return String.Empty;
            }

            return AJutActivator.CreateInstanceOf(type);
        }
    }
}
