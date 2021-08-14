namespace AJut.Text.AJson
{
    using AJut.Text;
    using AJut;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using AJut.Tree;

    public class JsonDocument : JsonValue, IEnumerable<KeyValuePair<TrackedString, JsonValue>>
    {
        internal const string kTypeIndicator = "__type";
        internal const string kKVPKeyTypeIndicator = "__key-type";
        internal const string kKVPValueTypeIndicator = "__value-type";
        internal const string kRuntimeTypeEvalValue = "__value";

        List<KeyValuePair<TrackedString, JsonValue>> m_memberStorage = new List<KeyValuePair<TrackedString, JsonValue>>();

        // =========================[ Constructor & Parsing ]===============================
        /// <summary>
        /// Constructor for parsing
        /// </summary>
        internal JsonDocument (JsonTextIndexer textIndexer, TrackedStringManager source, int startIndex, out int endIndex) : base(source)
        {
            endIndex = -1;

            int nextEval = textIndexer.NextAny(startIndex + 1);
            JsonHelper.IndexTrackingHelper keyIndexTracker = new JsonHelper.IndexTrackingHelper();
            int lastStart = startIndex + 1;
            int insideQuoteStart = -1;
            bool lastWasUseful;
            while (nextEval != -1)
            {
                lastWasUseful = true;
                switch (source.Text[nextEval])
                {
                    case '}':
                        if (insideQuoteStart != -1)
                        {
                            break;
                        }

                        if (keyIndexTracker)
                        {
                            this.Add(keyIndexTracker.CreateTS(source), new JsonValue(source, lastStart, nextEval - 1, false));
                        }
                        endIndex = nextEval;
                        break;

                    case '{':
                        {
                            if (insideQuoteStart != -1)
                            {
                                break;
                            }

                            int endOfDoc;

                            if (!keyIndexTracker)
                            {
                                throw new FormatException("Can't read json format - error found around  offset: " + nextEval.ToString());
                            }

                            this.Add(keyIndexTracker.CreateTS(source),
                                    new JsonDocument(textIndexer, source, nextEval, out endOfDoc));
                            if (endOfDoc == -1)
                            {
                                throw new FormatException($"Invalid json format provided, issue starts with document that begins at text index {nextEval}");
                            }

                            nextEval = endOfDoc;
                            keyIndexTracker.Reset();
                        }
                        break;

                    case '[':
                        {
                            if (insideQuoteStart != -1)
                            {
                                break;
                            }

                            if (!keyIndexTracker)
                            {
                                throw new FormatException("Can't read json format - attempted to add empty string key");
                            }

                            this.Add(keyIndexTracker.CreateTS(source),
                                    new JsonArray(textIndexer, source, nextEval, out int endOfArr));
                            if (endOfArr == -1)
                            {
                                throw new FormatException($"Invalid json format provided, issue starts with array that begins at text index {nextEval}");
                            }

                            nextEval = endOfArr;
                            keyIndexTracker.Reset();
                        }
                        break;

                    case ':':
                        if (insideQuoteStart != -1)
                        {
                            break;
                        }

                        keyIndexTracker.StartIndex = lastStart;
                        keyIndexTracker.EndIndex = nextEval - 1;
                        break;

                    case '\"':
                        // Start quote
                        if (insideQuoteStart == -1)
                        {
                            insideQuoteStart = nextEval + 1;
                        }
                        // End quote
                        else
                        {
                            // Peek ahead so we can decide if it's a key or a value
                            // If the next thing is a colon, then it's a key, otherwise it's a value
                            int peekAheadInd = textIndexer.NextAny(nextEval + 1);
                            if (peekAheadInd != -1 && source.Text[peekAheadInd] == ':')
                            {
                                keyIndexTracker.StartIndex = insideQuoteStart;
                                keyIndexTracker.EndIndex = nextEval - 1; // One before the quote that was just found
                                keyIndexTracker.IsInsideQuotes = true;
                                nextEval = peekAheadInd;
                            }
                            else
                            {
                                if (!keyIndexTracker)
                                {
                                    string quote = source.Text.Substring(insideQuoteStart, nextEval - 1);
                                    throw new FormatException("Json parsing failed due to incorrect string formatting around string: " + quote);
                                }

                                this.Add(keyIndexTracker.CreateTS(source), new JsonValue(source, insideQuoteStart, nextEval - 1, true));
                                keyIndexTracker.Reset();
                            }

                            insideQuoteStart = -1;
                        }

                        break;

                    case ',':
                        if (insideQuoteStart != -1)
                        {
                            break;
                        }

                        if (keyIndexTracker)
                        {
                            var endValue = new JsonValue(source, lastStart, nextEval - 1, false);
                            if (endValue.StringValue.Length != 0)
                            {
                                this.Add(keyIndexTracker.CreateTS(source), endValue);
                                keyIndexTracker.Reset();
                            }
                        }
                        break;
                    default:
                        lastWasUseful = false;
                        break;
                }

                if (endIndex != -1)
                    break;

                if (lastWasUseful)
                {
                    lastStart = nextEval + 1;
                }
                nextEval = textIndexer.NextAny(nextEval + 1);
            }

            if (nextEval == -1)
            {
                throw new FormatException("Json was improperly formatted");
            }

            this.ResetStringValue(startIndex, endIndex);
            this.Source.Track(this);
        }

        /// <summary>
        /// Constructor for building
        /// </summary>
        internal JsonDocument (TrackedStringManager source, int startIndex) : base(source)
        {
            this.OffsetInSource = startIndex;
        }

        // =========================[ Properties ]===============================
        public override bool IsDocument { get { return true; } }
        public override bool IsValue { get { return false; } }
        
        public int Count => m_memberStorage.Count;

        // =========================[ Interface Methods ]===============================

        public TrackedString KeyAt (int index)
        {
            return m_memberStorage[index].Key;
        }

        public JsonValue ValueAt (int index)
        {
            return m_memberStorage[index].Value;
        }

        public void Add (TrackedString key, JsonValue value)
        {
            value.Parent = this;
            m_memberStorage.Add(new KeyValuePair<TrackedString, JsonValue>(key, value));
        }

        public JsonValue AppendNew(string key, JsonBuilder valueBuilder)
        {
            int currTabbing = JsonHelper.EvaluteBegginningTabOffset(this, valueBuilder.BuilderSettings);

            int startIndex = this.OffsetInSource + this.StringValue.Length - 1;
            JsonHelper.FindInsertStart(this.Source.Text, ref startIndex);
            
            var trackerBuilder = new IndexTrackingStringBuilder(startIndex);

            if (this.Count > 0)
            {
                trackerBuilder.Write(",");
            }

            JsonBuilder.MakeNewline(trackerBuilder, currTabbing, valueBuilder.BuilderSettings);

            // Add the key part. This will actually be invalid until the PlaceholderSetupComplete happens
            JsonBuilder.WritePropertyHeading(trackerBuilder, key, out int keyStart, valueBuilder.BuilderSettings);

            if (!valueBuilder.IsValue)
            {
                JsonBuilder.MakeNewline(trackerBuilder, currTabbing, valueBuilder.BuilderSettings);
            }


            this.Source.BeginPlaceholderMode(trackerBuilder);
            TrackedString trackedKey = new TrackedString(this.Source, keyStart, key);

            JsonValue value = valueBuilder.BuildJsonValue(this.Source, trackerBuilder, currTabbing);

            this.Add(trackedKey, value);
            this.Source.PlaceholderSetupComplete();

            return value;
        }

        public JsonValue AppendNew(string key, object value, JsonBuilder.Settings settings = null)
        {
            return AppendNew(key, JsonHelper.MakeValueBuilder(value, settings));
        }

        public KeyValuePair<TrackedString, JsonValue> KeyAndValueAt(int index)
        {
            return m_memberStorage[index];
        }

        public bool ContainsKey(string key)
        {
            return m_memberStorage.Any(kvp => kvp.Key == key);
        }

        public JsonValue[] AllValuesForKey(string key)
        {
            return m_memberStorage.Where(kvp => kvp.Key == key).Select(kvp => kvp.Value).ToArray();
        }

        public IEnumerable<JsonValue> AllValues()
        {
            return m_memberStorage.Select(_ => _.Value);
        }

        /// <summary>
        /// Gets the value for whatever child (non-recursively) is assigned to the passed in key
        /// </summary>
        public JsonValue ValueFor(string key)
        {
            return m_memberStorage.Where(kvp => kvp.Key.StringValue == key).Select(kvp => kvp.Value).FirstOrDefault();
        }

        public JsonValue ValueFor(TrackedString key)
        {
            return m_memberStorage.Where(kvp => kvp.Key == key).Select(kvp => kvp.Value).FirstOrDefault();
        }

        public TrackedString KeyFor(JsonValue value)
        {
            return m_memberStorage.Where(kvp => kvp.Value == value).Select(kvp => kvp.Key).FirstOrDefault();
        }

        public TrackedString KeyFor(string value)
        {
            return m_memberStorage.Where(kvp => kvp.Value.StringValue == value).Select(kvp => kvp.Key).FirstOrDefault();
        }

        public bool TryGetValue<T> (string key, out T foundValue)
        {
            JsonValue value = this.ValueFor(key);
            if (value == null)
            {
                foundValue = default;
                return true;
            }

            foundValue = JsonHelper.BuildObjectForJson<T>(value);
            return true;
        }

        /// <summary>
        /// Breadth First search of the document tree
        /// </summary>
        public JsonValue FindValueByKey(string key, eTraversalStrategy strategy = eTraversalStrategy.BreadthFirst, eTraversalFlowDirection direction = eTraversalFlowDirection.ThroughChildren)
        {
            JsonValue found = TreeTraversal<JsonValue>.GetFirstChildWhichPasses(this, _TestIsDocumentWithKey, direction, strategy);
            if (found != null)
            {
                return ((JsonDocument)found).ValueFor(key);
            }

            return null;
            
            bool _TestIsDocumentWithKey (JsonValue value)
            {
                return value.IsDocument && ((JsonDocument)value).ContainsKey(key);
            }
        }

        public IEnumerator<KeyValuePair<TrackedString, JsonValue>> GetEnumerator()
        {
            return m_memberStorage.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_memberStorage.GetEnumerator();
        }

    }
}
