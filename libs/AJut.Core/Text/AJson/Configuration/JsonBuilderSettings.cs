namespace AJut.Text.AJson
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
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
            this.TypeIdToWrite = eTypeIdInfo.TypeIdAttributed;
            this.KeyValuePairKeyTypeIdToWrite = eTypeIdInfo.None;
            this.KeyValuePairValueTypeIdToWrite = eTypeIdInfo.None;
            this.UseReadonlyObjectProperties = false;
            this.SpacingAroundPropertyIndicators = " ";
        }

        /// <summary>
        /// The settings json is built with when none are given. A string maker registered here with
        /// <see cref="SetCustomJsonManager"/> applies to every such build.
        /// </summary>
        public static JsonBuilderSettings Default { get; set; } = new JsonBuilderSettings();

        public string Tabbing { get; set; }
        public string Newline { get; set; }
        public string SpacingAroundPropertyIndicators { get; set; }

        public bool QuotePropertyNames { get; set; }
        public char PropertyNameQuoteChars { get; set; }
        public char PropertyValueQuoteChars { get; set; }

        /// <summary>
        /// No longer does anything, and using it is a compile error. It converted DateTimes to UTC
        /// before writing, back when the text carried no offset or Kind and UTC was the only way
        /// to pin down the instant. Each DateTime is now written with its own Kind and reads back
        /// the same, so anything that wants UTC stored converts in its own model.
        /// </summary>
        /// <remarks>
        /// An error rather than a plain removal, so a project that set it is told why. Delete it
        /// in a later release.
        /// </remarks>
        [Obsolete(
            "AJson now writes each DateTime as round-trip ISO 8601 with its own Kind, and it reads back the "
            + "same, so this setting no longer does anything. To store UTC, convert in your own model with "
            + "ToUniversalTime().",
            error: true
        )]
        public bool MakeDateTimesUTC
        {
            get => false;
            set { }
        }

        /// <summary>
        /// Whether text written from json built with these settings carries the AJson version
        /// marker. Null, the default, follows <see cref="JsonHelper.WriteAJsonVersion"/>.
        /// </summary>
        public bool? WriteAJsonVersion { get; set; }

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
        /// Whether every get-only property and readonly field is written (default false). Off, a get-only member is written
        /// only when the reader can get it back: when the constructor the reader builds the type with takes it, when it is a
        /// collection the reader fills where it is, or when the type has nothing the reader can set or fill, so only a
        /// constructor can rebuild it. Turn it on to also write values worked out from other members (a Rect's Right, a
        /// Quaternion's IsIdentity) for something other than AJson to read, or for a type with settable members whose
        /// registered custom constructor reads a get-only member's key.
        /// </summary>
        public bool UseReadonlyObjectProperties { get; set; }

        /// <summary>
        /// Writes <paramref name="forType"/> as one value, the text <paramref name="creator"/> makes, for json built with these
        /// settings. It wins over a <see cref="JsonValueConverter"/> registered for the type. An open generic type covers every
        /// type made from it.
        /// </summary>
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

        /// <summary>
        /// What writes <paramref name="instanceType"/> as one value, if anything does: a string maker registered with
        /// <see cref="SetCustomJsonManager"/>, then a <see cref="JsonScalarConverter"/>, then the simple-type maker for a
        /// primitive, string or enum
        /// </summary>
        /// <returns>The maker, or null when the type is written as a document or an array</returns>
        public JsonStringMaker TryGetJsonValueStringMakerFor (Type instanceType)
        {
            JsonStringMaker registered = this.TryGetRegisteredStringMakerFor(instanceType);
            if (registered != null)
            {
                return registered;
            }

            if (JsonHelper.TryGetConverterFor(instanceType, out JsonValueConverter converter))
            {
                return (converter as JsonScalarConverter)?.StringMaker;
            }

            return instanceType.IsSimpleType() ? (JsonStringMaker)_SimpleTypeStringMaker : null;

            // JSON numbers are culture-invariant. The current culture's ToString writes 0.5 as 0,5
            //  under a comma-decimal culture, which the reader splits at the comma, and which no
            //  machine with a different culture could read anyway.
            string _SimpleTypeStringMaker (object _instance) => _instance is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : _instance?.ToString();
        }

        /// <summary>
        /// The string maker registered with <see cref="SetCustomJsonManager"/> for <paramref name="instanceType"/> exactly, or
        /// for the generic type it is made from
        /// </summary>
        internal JsonStringMaker TryGetRegisteredStringMakerFor (Type instanceType)
        {
            if (m_customJsonConstructor.Count == 0)
            {
                return null;
            }

            if (m_customJsonConstructor.TryGetValue(instanceType, out JsonStringMaker exact))
            {
                return exact;
            }

            if (instanceType.IsGenericType
                && m_customJsonConstructor.TryGetValue(instanceType.GetGenericTypeDefinition(), out JsonStringMaker byDefinition))
            {
                return byDefinition;
            }

            return null;
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
