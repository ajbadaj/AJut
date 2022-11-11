namespace AJut.Text.AJson
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// A delegate used to construct an instance from a json value (given interpretter settings)
    /// </summary>
    public delegate object JsonToObjectConstructor (Type fullTarget, JsonValue value, JsonInterpretterSettings settings);

    /// <summary>
    /// Settings used to determine how to interpret json and create object instances
    /// </summary>
    public class JsonInterpretterSettings
    {
        private readonly Dictionary<Type, JsonToObjectConstructor> m_customConstructors = new Dictionary<Type, JsonToObjectConstructor>();

        // ===========================[ Construction ]===============================
        public JsonInterpretterSettings (StringParser stringParser = null)
        {
            this.StringParser = stringParser ?? new StringParser();
            m_customConstructors.Add(typeof(Guid), _CreateGuidFor);
            m_customConstructors.Add(typeof(DateTime), _CreateDateTimeFor);
            m_customConstructors.Add(typeof(TimeSpan), _CreateTimeSpanFor);
            m_customConstructors.Add(typeof(KeyValuePair<,>), _CreateKeyValuePairFor);
            m_customConstructors.Add(typeof(TimeZoneInfo), _CreateTimezoneInfo);

            object _CreateGuidFor (Type fullTarget, JsonValue json, JsonInterpretterSettings settings)
            {
                return Guid.TryParse(json.StringValue, out Guid found) ? found : Guid.Empty;
            }

            object _CreateDateTimeFor (Type fullTarget, JsonValue json, JsonInterpretterSettings settings)
            {
                return DateTime.TryParse(json.StringValue, CultureInfo.CurrentCulture.DateTimeFormat, this.DefaultDateTimeParseStyle, out DateTime found) ? found : default;
            }

            object _CreateTimeSpanFor (Type fullTarget, JsonValue json, JsonInterpretterSettings settings)
            {
                return TimeSpan.TryParse(json.StringValue, out TimeSpan found) ? found : TimeSpan.Zero;
            }

            object _CreateKeyValuePairFor (Type fullTarget, JsonValue json, JsonInterpretterSettings settings)
            {
                if (!json.IsDocument)
                {
                    throw new Exception($"Attempting to parse a Key value pair out of a non-document json value:\n{json}");
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

                Type kvpType = fullTarget.FindBaseTypeOrInterface(typeof(KeyValuePair<,>));
                Type[] genericTypes = kvpType.GetGenericArguments();
                if (keyType == null)
                {
                    keyType = genericTypes[0];
                }

                if (valueType == null)
                {
                    valueType = genericTypes[1];
                }

                JsonValue keyJson = doc.ValueFor("Key");
                if (keyJson == null)
                {
                    throw new FormatException($"Attempting to parse key value pair - 'Key' is missing:\n{json}");
                }

                JsonValue valueJson = doc.ValueFor("Value");
                if (valueJson == null)
                {
                    throw new FormatException($"Attempting to parse key value pair - 'Value' is missing:\n{json}");
                }

                object keyObj = JsonHelper.BuildObjectForJson(keyType, keyJson, settings);
                object valueObj = JsonHelper.BuildObjectForJson(valueType, valueJson, settings);

                return kvpType.GetConstructor(genericTypes).Invoke(new[] { keyObj, valueObj });
            }

            object _CreateTimezoneInfo (Type fullTarget, JsonValue json, JsonInterpretterSettings settings)
            {
                return TimeZoneInfo.FindSystemTimeZoneById(json.StringValue);
            }
        }

        // ===========================[ Properties ]===============================
        public static JsonInterpretterSettings Default { get; set; } = new JsonInterpretterSettings();

        public StringParser StringParser { get; }

        public DateTimeStyles DefaultDateTimeParseStyle { get; set; } = DateTimeStyles.AssumeUniversal;

        // ===========================[ Interface Methods ]===============================

        /// <summary>
        /// Add a constructor for a particular type to these settings
        /// </summary>
        public void Add(Type t, JsonToObjectConstructor constructor)
        {
            m_customConstructors.Add(t, constructor);
        }

        /// <summary>
        /// Construct an instance of the given type from the passed in json
        /// </summary>
        public object ConstructInstanceFor(Type type, JsonValue jsonValue)
        {
            // Check custom type constructors first
            foreach (KeyValuePair<Type, JsonToObjectConstructor> kvp in m_customConstructors)
            {
                if (type.TargetsSameTypeAs(kvp.Key))
                {
                    return kvp.Value(type, jsonValue, this);
                }
            }

            // String has a special constructor, using String.Empty :)
            if (type == typeof(string))
            {
                return String.Empty;
            }

            // Otherwise the normal Activator.CreateInstance
            return Activator.CreateInstance(type);
        }
    }
}
