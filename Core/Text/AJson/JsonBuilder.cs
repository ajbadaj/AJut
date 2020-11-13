namespace AJut.Text.AJson
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public enum ePropertyValueQuoting
    {
        /// <summary>
        /// Quote only usually quoted items (ie strings & chars)
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
        None                        = 0b0000,

        /// <summary>
        /// Whatever is given to [<see cref="JsonTypeIdAttribute"/>("...")]
        /// </summary>
        TypeIdAttributed            = 0b0001,

        /// <summary>
        /// The type name (*not* fully qualified)
        /// </summary>
        SystemTypeName              = 0b0010,

        /// <summary>
        /// The type name (fully qualified)
        /// </summary>
        FullyQualifiedSystemType    = 0b1000,

        /// <summary>
        /// If the <see cref="TypeIdAttributed"/> is registered, use that, otherwise use <see cref="FullyQualifiedSystemType"/>
        /// </summary>
        Any                         = TypeIdAttributed | FullyQualifiedSystemType,
    }

    public delegate string JsonStringMaker (object instance);

    /// <summary>
    /// Utility class for building json programatically (without parsing text)
    /// </summary>
    public class JsonBuilder
    {
        private string m_value;

        // ==========================[ Construction ]===================================
        internal JsonBuilder (Settings settings)
        {
            this.Key = String.Empty;
            this.BuilderSettings = settings ?? new Settings();
            this.Children = new List<JsonBuilder>();
            this.ArrayIndex = -1;
        }

        internal JsonBuilder (JsonBuilder parent) : this(parent.BuilderSettings)
        {
            this.Parent = parent;
        }

        /// <summary>
        /// Root value builder constructor
        /// </summary>
        internal JsonBuilder (Settings settings, object value) : this(settings)
        {
            this.IsValue = true;
            JsonHelper.FillOutJsonBuilderForObject(value, this);
        }

        // ==========================[ Properties ]===================================
        public Settings BuilderSettings
        {
            get; set;
        }

        public JsonBuilder Parent
        {
            get; private set;
        }

        public List<JsonBuilder> Children
        {
            get; private set;
        }

        public int ArrayIndex
        {
            get; private set;
        }

        /// <summary>
        /// This will *only* be set if this builder represents a document kvp
        /// </summary>
        public string Key
        {
            get; set;
        }

        /// <summary>
        /// This will *only* be set if this builder represents a document kvp
        /// </summary>
        public JsonBuilder DocumentKVPValue
        {
            get; internal set;
        }

        public bool IsUnset
        {
            get { return !IsDocument && !IsArray && !IsValue; }
        }
        public bool IsDocument
        {
            get; private set;
        }
        public bool IsArray
        {
            get; private set;
        }

        public bool IsValue
        {
            get; private set;
        }

        public bool IsDocumentKVP => this.Key != String.Empty;

        public bool IsArrayItem => this.ArrayIndex != -1;

        public string Value
        {
            get => m_value;
            set
            {
                if (m_value != value)
                {
                    m_value = value;
                    this.IsValue = true;
                }
            }
        }

        public bool IsValueUsualQuoteTarget
        {
            get; set;
        }

        // ==========================[ Interface Methods ]===================================

        public JsonBuilder StartProperty(string propertyName)
        {
            if(this.IsUnset)
            {
                this.IsDocument = true;
            }

            if(this.IsDocument)
            {
                JsonBuilder child = new JsonBuilder(this);
                child.Key = propertyName;
                this.Children.Add(child);
                return child;
            }

            throw new InvalidOperationException("You can only call StartProperty on a document!");
        }

        public JsonBuilder StartDocument()
        {
            // If we're in an array, then this means start a document child item
            if(this.IsArray)
            {
                JsonBuilder arrayItem = new JsonBuilder(this);
                arrayItem.ArrayIndex = this.Children.Count;
                arrayItem.IsDocument = true;
                this.Children.Add(arrayItem);
                return arrayItem;
            }
            if(this.IsDocument)
            {
                throw new InvalidOperationException("Tried to start a document inside a document, did you mean to use AddProperty?");
            }
            if (this.IsValue)
            {
                throw new InvalidOperationException("Tried to start a document inside a value.");
            }

            if(this.IsDocumentKVP)
            {
                this.DocumentKVPValue = new JsonBuilder(this);
                this.DocumentKVPValue.IsDocument = true;
                return this.DocumentKVPValue;
            }

            // otherwise it's unset, and we can do whatever we want with it.
            this.IsDocument = true;
            return this;
        }

        public JsonBuilder StartArray()
        {
            // If we're in an array, then this means start a array child item
            if (this.IsArray)
            {
                JsonBuilder arrayItem = new JsonBuilder(this);
                arrayItem.ArrayIndex = this.Children.Count;
                arrayItem.IsArray = true;
                this.Children.Add(arrayItem);
                return arrayItem;
            }
            if (this.IsDocument)
            {
                throw new InvalidOperationException("Tried to start an array inside a document, did you mean to use AddProperty?");
            }
            if (this.IsValue)
            {
                throw new InvalidOperationException("Tried to start an array inside a value.");
            }

            if (this.IsDocumentKVP)
            {
                this.DocumentKVPValue = new JsonBuilder(this);
                this.DocumentKVPValue.IsArray = true;
                return this.DocumentKVPValue;
            }

            // otherwise it's unset, and we can do whatever we want with it.
            this.IsArray = true;
            return this;
        }

        public JsonBuilder AddProperty(string propertyName, object propertyValue)
        {
            if (this.IsDocument)
            {
                if (propertyValue != null)
                {
                    JsonBuilder child = new JsonBuilder(this);
                    child.Key = propertyName;
                    child.DocumentKVPValue = new JsonBuilder(child);
                    child.DocumentKVPValue.IsValue = true;

                    JsonHelper.FillOutJsonBuilderForObject(propertyValue, child.DocumentKVPValue);

                    this.Children.Add(child);
                }
                return this;
            }

            throw new InvalidOperationException("You can only call AddProperty on documents.");
        }

        public JsonBuilder AddArrayItem(object arrayValue)
        {
            if (this.IsArray)
            {
                JsonBuilder child = new JsonBuilder(this);
                child.IsValue = true;
                JsonHelper.FillOutJsonBuilderForObject(arrayValue, child);

                this.Children.Add(child);
                return this;
            }

            throw new InvalidOperationException("You can only call AddArrayItem on an array.");
        }

        public JsonBuilder GetParent()
        {
            JsonBuilder target = this.Parent;
            if (target == null)
            {
                return null;
            }
            if (target.IsDocumentKVP)
            {
                target = target.Parent;
            }

            return target;
        }


        public JsonBuilder End()
        {
            return GetParent() ?? this;
        }

        public JsonBuilder FindRoot()
        {
            JsonBuilder target = this.GetParent() ?? this;
            while (true)
            {
                var next = target.GetParent();
                if (next == null)
                {
                    break;
                }

                target = next;
            }

            return target;
        }

        public Json Finalize()
        {
            JsonBuilder root = this.FindRoot();

            IndexTrackingStringBuilder textTracker = new IndexTrackingStringBuilder();
            TrackedStringManager placeholderTracker = new TrackedStringManager(textTracker);
            JsonValue data = root.BuildOutputValue(textTracker, placeholderTracker, 0, true);

            placeholderTracker.PlaceholderSetupComplete();
            var output = new Json(placeholderTracker);
            output.Data = data;
            placeholderTracker.HasChanges = false;

            return output;
        }

        // ==========================[ Utility Methods ]===================================

        internal JsonValue BuildJsonValue(TrackedStringManager manager, IndexTrackingStringBuilder builder, int startTabbing)
        {
            return BuildOutputValue(builder, manager, startTabbing, true);
        }


        private JsonValue BuildOutputValue(IndexTrackingStringBuilder jsonTextAssembler, TrackedStringManager placeholderSource, int currentTabbing, bool isFirstiteration = false)
        {
            if (!isFirstiteration && !this.IsValue)
            {
                MakeNewline(jsonTextAssembler, currentTabbing);
            }

            if (this.IsDocument)
            {
                int docStart = jsonTextAssembler.NextWriteIndex;
                jsonTextAssembler.Write("{");

                ++currentTabbing;
                MakeNewline(jsonTextAssembler, currentTabbing);

                JsonDocument doc = new JsonDocument(placeholderSource, docStart);

                JsonBuilder last = this.Children.LastOrDefault();
                foreach(JsonBuilder child in this.Children)
                {
                    if(child.DocumentKVPValue == null)
                    {
                        continue;
                    }
                    int headingStart;
                    WritePropertyHeading(jsonTextAssembler, child.Key, out headingStart);
                    TrackedString key = placeholderSource.Track(headingStart, child.Key.Length);

                    JsonValue value = child.DocumentKVPValue.BuildOutputValue(jsonTextAssembler, placeholderSource, currentTabbing);
                    doc.Add(key, value);

                    if (child != last)
                    {
                        jsonTextAssembler.Write(",");
                        MakeNewline(jsonTextAssembler, currentTabbing);
                    }
                }

                --currentTabbing;
                MakeNewline(jsonTextAssembler, currentTabbing);

                int docEndIndex = jsonTextAssembler.NextWriteIndex;
                jsonTextAssembler.Write("}");

                placeholderSource.TrackForPlaceholder(doc, docEndIndex);
                return doc;
            }
            if(this.IsArray)
            {
                int startIndex = jsonTextAssembler.NextWriteIndex;
                jsonTextAssembler.Write("[");

                ++currentTabbing;

                JsonArray arr = new JsonArray(placeholderSource, startIndex);

                JsonBuilder last = this.Children.LastOrDefault();
                foreach (JsonBuilder child in this.Children)
                {
                    if (child.IsValue)
                    {
                        MakeNewline(jsonTextAssembler, currentTabbing);
                    }

                    JsonValue value = child.BuildOutputValue(jsonTextAssembler, placeholderSource, currentTabbing);
                    arr.Add(value);

                    if(child != last)
                    {
                        jsonTextAssembler.Write(",");
                    }
                }

                --currentTabbing;
                MakeNewline(jsonTextAssembler, currentTabbing);

                int endIndex = jsonTextAssembler.NextWriteIndex;
                jsonTextAssembler.Write("]");

                placeholderSource.TrackForPlaceholder(arr, endIndex);
                return arr;
            }
            if (this.IsValue)
            {
                WritePropertyValue(jsonTextAssembler, this.Value, out int propStart, out int propEnd);

                var value = new JsonValue(placeholderSource, propStart, this.Value);
                placeholderSource.TrackForPlaceholder(value, propEnd);
                return value;
            }

            throw new Exception("Can't build output values, value came in that is neither Document, array, nor value. Possibly still unset?");
        }

        private void MakeNewline(IndexTrackingStringBuilder textTracker, int currentTabbing)
        {
            MakeNewline(textTracker, currentTabbing, this.BuilderSettings);
        }

        private void WritePropertyHeading(IndexTrackingStringBuilder textTracker, string propName, out int headingTextStart)
        {
            WritePropertyHeading(textTracker, propName, out headingTextStart, this.BuilderSettings);
        }

        private void WritePropertyValue(IndexTrackingStringBuilder jsonTextAssembler, string propValue, out int propStart, out int propEnd)
        {
            WritePropertyValue(jsonTextAssembler, propValue, this.IsValueUsualQuoteTarget, out propStart, out propEnd, this.BuilderSettings);
        }

        internal static void MakeNewline(IndexTrackingStringBuilder textTracker, int currentTabbing, Settings settings)
        {
            textTracker.Write(settings.Newline);

            while (currentTabbing-- > 0)
            {
                textTracker.Write(settings.Tabbing);
            }
        }

        internal static void WritePropertyHeading(IndexTrackingStringBuilder textTracker, string propName, out int headingTextStart, Settings settings)
        {
            if (settings.QuotePropertyNames)
            {
                headingTextStart = textTracker.NextWriteIndex + 1;
                textTracker.Write("{1}{0}{1} : ", propName, settings.PropertyNameQuoteChars);
            }
            else
            {
                headingTextStart = textTracker.NextWriteIndex;
                textTracker.Write("{0} : ", propName);
            }
        }

        internal static void WritePropertyValue(IndexTrackingStringBuilder jsonTextAssembler, string propValue, bool isUsuallyQuoted, out int propStart, out int propEnd, Settings settings)
        {
            if(propValue == null)
            {
                propStart = propEnd = jsonTextAssembler.NextWriteIndex;
                return;
            }

            if (settings.PropertyValueQuoting == ePropertyValueQuoting.NeverQuoteValues ||
                (settings.PropertyValueQuoting == ePropertyValueQuoting.QuoteAnyUsuallyQuotedItem && !isUsuallyQuoted))
            {
                propStart = jsonTextAssembler.NextWriteIndex;
                jsonTextAssembler.Write(propValue);
                // The next write index would be one after the end, so nextWritIndex - 1
                propEnd = jsonTextAssembler.NextWriteIndex - 1;
            }
            else
            {
                propStart = jsonTextAssembler.NextWriteIndex + 1;
                jsonTextAssembler.Write("{1}{0}{1}", propValue, settings.PropertyValueQuoteChars);
                // The next write index would be one after the end usually, but we want to track where the value
                //  inside the qutoes are, so instead it's -2
                propEnd = jsonTextAssembler.NextWriteIndex - 2;
            }
        }

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
                this.TypeIdToWrite = eTypeIdInfo.None;
                this.KeyValuePairKeyTypeIdToWrite = eTypeIdInfo.None;
                this.KeyValuePairValueTypeIdToWrite = eTypeIdInfo.None;

                m_customJsonConstructor.Add(typeof(bool), _BoolToJsonString);
                m_customJsonConstructor.Add(typeof(DateTime), _DateTimeToJsonString);
                m_customJsonConstructor.Add(typeof(TimeSpan), _TimeSpanToJsonString);
                m_customJsonConstructor.Add(typeof(Guid), _GuidToJsonString);

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

#if WINDOWS_UWP
            public bool UWP_RequireOptInViaDataMemberAttribute { get; set; } = false;
#endif

            public void SetCustomJsonManager(Type forType, JsonStringMaker creator)
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
