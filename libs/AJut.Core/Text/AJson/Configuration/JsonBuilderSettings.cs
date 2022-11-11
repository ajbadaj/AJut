namespace AJut.Text.AJson
{
    using System;
    using System.Collections.Generic;

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
        /// Whatever is given to [<see cref="JsonTypeIdAttribute"/>("...")]
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
        /// If the <see cref="TypeIdAttributed"/> is registered, use that, otherwise use <see cref="FullyQualifiedSystemType"/>
        /// </summary>
        Any = TypeIdAttributed | FullyQualifiedSystemType,
    }
    
    public delegate string JsonStringMaker (object instance);

    public sealed partial class JsonBuilder
    {
        /// <summary>
        /// The settings used when *building* json
        /// </summary>
        public class Settings
        {
            private readonly Dictionary<Type, JsonStringMaker> m_customJsonConstructor = new Dictionary<Type, JsonStringMaker>();

            public Settings ()
            {
                this.PropertyNameQuoteChars = '\"';
                this.PropertyValueQuoteChars = '\"';
                this.Tabbing = "\t";
                this.QuotePropertyNames = true;
                this.Newline = "\n";
                this.PropertyValueQuoting = ePropertyValueQuoting.QuoteAll;
                this.MakeDateTimesUTC = true;
                this.TypeIdToWrite = eTypeIdInfo.TypeIdAttributed;
                this.KeyValuePairKeyTypeIdToWrite = eTypeIdInfo.None;
                this.KeyValuePairValueTypeIdToWrite = eTypeIdInfo.None;
                this.UseReadonlyObjectProperties = true;

                m_customJsonConstructor.Add(typeof(bool), _BoolToJsonString);
                m_customJsonConstructor.Add(typeof(DateTime), _DateTimeToJsonString);
                m_customJsonConstructor.Add(typeof(TimeSpan), _TimeSpanToJsonString);
                m_customJsonConstructor.Add(typeof(Guid), _GuidToJsonString);
                m_customJsonConstructor.Add(typeof(TimeZoneInfo), _TimeZoneToAJsonString);

                string _BoolToJsonString (object instance) => ((bool)instance) ? "True" : "False";
                string _DateTimeToJsonString (object instance)
                {
                    var date = (DateTime)instance;
                    if (this.MakeDateTimesUTC && date.Kind != DateTimeKind.Utc)
                    {
                        date = date.ToUniversalTime();
                    }

                    return date.ToString();
                }
                string _TimeSpanToJsonString (object instance) => ((TimeSpan)instance).ToString();
                string _GuidToJsonString (object instance) => ((Guid)instance).ToString();
                string _TimeZoneToAJsonString (object instance) => ((TimeZoneInfo)instance).Id;
            }

            public string Tabbing { get; set; }

            public string Newline { get; set; }

            public bool QuotePropertyNames { get; set; }

            public char PropertyNameQuoteChars { get; set; }

            public char PropertyValueQuoteChars { get; set; }

            public bool MakeDateTimesUTC { get; set; }

            public ePropertyValueQuoting PropertyValueQuoting { get; set; }

            /// <summary>
            /// Should a "__type": "...typing info..." be written out as a property of each document? This will allow the proper reading back of derived types.
            /// You can specify what kind of typing info to use that will allow the System.Type or a string specified by the [<see cref="JsonTypeIdAttribute"/>("...")] attribute
            /// </summary>
            public eTypeIdInfo TypeIdToWrite { get; set; }

            /// <summary>
            /// [Key] KeyValuePair is a bit different, it is an object that is typed specially (needed for dictionaries), you may want to control
            /// how you write key value pair type ids for better deserialization
            /// </summary>
            public eTypeIdInfo KeyValuePairKeyTypeIdToWrite { get; set; }

            /// <summary>
            /// [Value] KeyValuePair is a bit different, it is an object that is typed specially (needed for dictionaries), you may want to control
            /// how you write key value pair type ids for better deserialization
            /// </summary>
            public eTypeIdInfo KeyValuePairValueTypeIdToWrite { get; set; }

            public bool HasAnyKVPTypeIdWriteInstructions => this.KeyValuePairKeyTypeIdToWrite != eTypeIdInfo.None || this.KeyValuePairValueTypeIdToWrite != eTypeIdInfo.None;

            /// <summary>
            /// When reading an object to decide what properties to use to build json, should read only properties be applied (default = true)
            /// </summary>
            public bool UseReadonlyObjectProperties { get; set; }

            public void SetCustomJsonManager (Type forType, JsonStringMaker creator)
            {
                if (m_customJsonConstructor.ContainsKey(forType))
                {
                    m_customJsonConstructor[forType] = creator;
                }
                else
                {
                    m_customJsonConstructor.Add(forType, creator);
                }
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

                // Does this need to be unboxed?
                string _SimpleTypeStringMaker (object _instance) => _instance?.ToString();
            }
        }
    }
}
