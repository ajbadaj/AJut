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
        // "o" is exactly what the writer produces. The second form also takes ISO 8601 text with
        //  fewer (or no) fractional digits, as other tools write it.
        private static readonly string[] kIso8601DateTimeFormats = { "o", "yyyy-MM-ddTHH:mm:ss.FFFFFFFK" };

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
                string text = json.StringValue;

                // 1. Round-trip ISO 8601, which is what AJson writes. RoundtripKind hands back the
                //    Kind the text carries: Z is Utc, an offset is Local (as this machine's local
                //    time), and no suffix is Unspecified.
                bool isIso8601 = DateTime.TryParseExact(
                    text,
                    kIso8601DateTimeFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime found
                );

                if (isIso8601)
                {
                    return found;
                }

                // 2. Text written before that: the writing machine's current culture format, with no
                //    offset or Kind, which was always UTC under the old defaults. It reads the way it
                //    always did, with the invariant culture as a second try for a file written under
                //    another culture.
                DateTimeStyles oldTextStyle = this.DefaultDateTimeParseStyle;
                if (DateTime.TryParse(text, CultureInfo.CurrentCulture, oldTextStyle, out found)
                    || DateTime.TryParse(text, CultureInfo.InvariantCulture, oldTextStyle, out found))
                {
                    return found;
                }

                owner?.AddError($"Could not read '{text}' as a DateTime, the value is left at default");
                return default(DateTime);
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
                    ? JsonHelper.BuildObjectForJson(keyType, keyJson, settings, owner)
                    : _DefaultFor(keyType);

                object valueObj = valueJson != null
                    ? JsonHelper.BuildObjectForJson(valueType, valueJson, settings, owner)
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
                // The writer puts each component out invariant. Text written before that used the
                //  writing machine's culture, which reads back with the current culture as before.
                //  A comma-decimal culture's old text (<0,5,1,25>) splits into four parts and was
                //  never readable, so it is reported along with anything else that does not parse.
                string[] xystrs = (json.StringValue ?? String.Empty)
                    .Trim('<', '>')
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                // The old read used float.TryParse's own defaults, which allow thousands grouping
                const NumberStyles kOldTextStyles = NumberStyles.Float | NumberStyles.AllowThousands;
                if (_TryReadVector2(xystrs, NumberStyles.Float, CultureInfo.InvariantCulture, out Vector2 found)
                    || _TryReadVector2(xystrs, kOldTextStyles, CultureInfo.CurrentCulture, out found))
                {
                    return found;
                }

                owner?.AddError($"Could not read '{json.StringValue}' as a Vector2, the value is left at zero");
                return Vector2.Zero;
            }

            static bool _TryReadVector2 (string[] parts, NumberStyles styles, CultureInfo culture, out Vector2 vector)
            {
                if (parts.Length == 2
                    && float.TryParse(parts[0], styles, culture, out float x)
                    && float.TryParse(parts[1], styles, culture, out float y))
                {
                    vector = new Vector2(x, y);
                    return true;
                }

                vector = Vector2.Zero;
                return false;
            }

            static object _DefaultFor ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;
        }

        // ===========================[ Properties ]===============================
        public static JsonInterpreterSettings Default { get; set; } = new JsonInterpreterSettings();

        public StringParser StringParser { get; }

        /// <summary>
        /// How DateTime text that is not round-trip ISO 8601 is read: text written before AJson
        /// wrote ISO 8601, or by something else. The default, AssumeUniversal, matches how the old
        /// writer wrote it (UTC, with no offset).
        /// </summary>
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
