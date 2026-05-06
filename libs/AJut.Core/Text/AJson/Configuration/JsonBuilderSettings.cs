namespace AJut.Text.AJson
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using AJut;

    public enum ePropertyValueQuoting
    {
        /// <summary>
        /// Quote only usually quoted items (ie strings &amp; chars)
        /// </summary>
        QuoteAnyUsuallyQuotedItem,

        /// <summary>
        /// If you plan to add quotes yourself
        /// </summary>
        NeverQuoteValues,

        /// <summary>
        /// Quote everything regardless of type
        /// </summary>
        QuoteAll,
    }

    [Flags]
    public enum eTypeIdInfo
    {
        /// <summary>
        /// Do not write out any type id
        /// </summary>
        None = 0b0000,

        /// <summary>
        /// Whatever was registered via [TypeId("...")] or RegisterTypeId
        /// </summary>
        TypeIdAttributed = 0b0001,

        /// <summary>
        /// The type name (*not* fully qualified)
        /// </summary>
        SystemTypeName = 0b0010,

        /// <summary>
        /// The type name (fully qualified)
        /// </summary>
        FullyQualifiedSystemType = 0b1000,

        /// <summary>
        /// If a registered TypeId exists, use that, otherwise use the fully qualified system type.
        /// </summary>
        Any = TypeIdAttributed | FullyQualifiedSystemType,
    }

    public delegate string JsonStringMaker (object instance);

    /// <summary>
    /// Settings used when *building* json - the V2 top-level replacement for V1's nested JsonBuilder.Settings.
    /// </summary>
    public class JsonBuilderSettings
    {
        private readonly Dictionary<Type, JsonStringMaker> m_customJsonConstructor = new Dictionary<Type, JsonStringMaker>();
        private readonly Dictionary<Type, object> m_defaultEquivalents = new Dictionary<Type, object>();

        public JsonBuilderSettings ()
        {
            this.PropertyNameQuoteChars = '\"';
            this.PropertyValueQuoteChars = '\"';
            this.Tabbing = "\t";
            this.QuotePropertyNames = true;
            this.Newline = "\n";
            this.PropertyValueQuoting = ePropertyValueQuoting.QuoteAnyUsuallyQuotedItem;
            this.MakeDateTimesUTC = true;
            this.TypeIdToWrite = eTypeIdInfo.TypeIdAttributed;
            this.KeyValuePairKeyTypeIdToWrite = eTypeIdInfo.None;
            this.KeyValuePairValueTypeIdToWrite = eTypeIdInfo.None;
            this.UseReadonlyObjectProperties = true;
            this.SpacingAroundPropertyIndicators = " ";

            m_customJsonConstructor.Add(typeof(bool), _BoolToJsonString);
            m_customJsonConstructor.Add(typeof(DateTime), _DateTimeToJsonString);
            m_customJsonConstructor.Add(typeof(TimeSpan), _TimeSpanToJsonString);
            m_customJsonConstructor.Add(typeof(Guid), _GuidToJsonString);
            m_customJsonConstructor.Add(typeof(TimeZoneInfo), _TimeZoneToAJsonString);
            m_customJsonConstructor.Add(typeof(Vector2), _Vector2ToAJsonString);

            string _BoolToJsonString (object instance) => ((bool)instance) ? "true" : "false";
            string _DateTimeToJsonString (object instance)
            {
                DateTime date = (DateTime)instance;
                if (this.MakeDateTimesUTC && date.Kind != DateTimeKind.Utc)
                {
                    date = date.ToUniversalTime();
                }

                return date.ToString();
            }
            string _TimeSpanToJsonString (object instance) => ((TimeSpan)instance).ToString();
            string _GuidToJsonString (object instance) => ((Guid)instance).ToString();
            string _TimeZoneToAJsonString (object instance) => ((TimeZoneInfo)instance).Id;
            string _Vector2ToAJsonString (object instance)
            {
                Vector2 vec2 = (Vector2)instance;
                return $"<{vec2.X},{vec2.Y}>";
            }
        }

        public string Tabbing { get; set; }
        public string Newline { get; set; }
        public string SpacingAroundPropertyIndicators { get; set; }

        public bool QuotePropertyNames { get; set; }
        public char PropertyNameQuoteChars { get; set; }
        public char PropertyValueQuoteChars { get; set; }

        public bool MakeDateTimesUTC { get; set; }

        public ePropertyValueQuoting PropertyValueQuoting { get; set; }

        /// <summary>
        /// Should a "__type" property be written for each document - and if so what kind of typing info should it carry.
        /// </summary>
        public eTypeIdInfo TypeIdToWrite { get; set; }

        /// <summary>
        /// KeyValuePair Key type id write rules (relevant when serializing dictionaries with non-trivial key types).
        /// </summary>
        public eTypeIdInfo KeyValuePairKeyTypeIdToWrite { get; set; }

        /// <summary>
        /// KeyValuePair Value type id write rules.
        /// </summary>
        public eTypeIdInfo KeyValuePairValueTypeIdToWrite { get; set; }

        public bool HasAnyKVPTypeIdWriteInstructions
            => this.KeyValuePairKeyTypeIdToWrite != eTypeIdInfo.None
            || this.KeyValuePairValueTypeIdToWrite != eTypeIdInfo.None;

        /// <summary>
        /// When pulling properties off an object, should read-only properties be included (default true).
        /// </summary>
        public bool UseReadonlyObjectProperties { get; set; }

        public void SetCustomJsonManager (Type forType, JsonStringMaker creator)
        {
            m_customJsonConstructor[forType] = creator;
        }

        /// <summary>
        /// Register an explicit "this counts as default" instance for a specific type. The
        /// JsonOmitIfDefault writer check consults this map after the per-attribute explicit
        /// default and before the Activator.CreateInstance fallback. Use this when the
        /// "default" for a type cannot be expressed as an attribute argument (e.g. Vector2,
        /// Guid - non-const-expressible types).
        /// </summary>
        public void RegisterDefaultEquivalent<T> (T value)
        {
            m_defaultEquivalents[typeof(T)] = value;
        }

        public bool TryGetDefaultEquivalent (Type type, out object value)
        {
            return m_defaultEquivalents.TryGetValue(type, out value);
        }

        public JsonStringMaker TryGetJsonValueStringMakerFor (Type instanceType)
        {
            foreach (KeyValuePair<Type, JsonStringMaker> kvp in m_customJsonConstructor)
            {
                if (instanceType.TargetsSameTypeAs(kvp.Key))
                {
                    return kvp.Value;
                }
            }

            return instanceType.IsSimpleType() ? (JsonStringMaker)_SimpleTypeStringMaker : null;

            string _SimpleTypeStringMaker (object _instance) => _instance?.ToString();
        }

        public static JsonBuilderSettings BuildMinifiedSettings ()
        {
            return new JsonBuilderSettings
            {
                Tabbing = String.Empty,
                Newline = String.Empty,
                SpacingAroundPropertyIndicators = String.Empty,
            };
        }
    }
}
