namespace AJut.Text.AJson
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// An array of json values, values can be simple strings, JsonDocument or even child arrays.
    /// </summary>
    public class JsonArray : JsonValue, IEnumerable<JsonValue>
    {
        private List<JsonValue> m_values = new List<JsonValue>();
        
        // ===================[ Construction & Parsing ]===========================
        /// <summary>
        /// Constructor for parsing
        /// </summary>
        internal JsonArray (JsonTextIndexer indexer, TrackedStringManager source, int startIndex, out int endIndex) : base(source)
        {
            endIndex = -1;

            int nextEval = indexer.NextAny(startIndex + 1);
            int lastStart = startIndex + 1;
            int quoteStart = -1;
            bool lastWasUseful;
            while (nextEval != -1)
            {
                lastWasUseful = true;
                switch (source.Text[nextEval])
                {
                    case ']':
                        if (quoteStart != -1)
                        {
                            break;
                        }

                        // There can be an unquoted array item inbetween the last comma'd item and the end 
                        //  bracket. To make sure we handle that case, but don't add empty elements, we're 
                        //  going to check it's an empty string before we add it.
                        //
                        // While empty keys and values will be supported (keys as long as they are unique)
                        //  they must be quoted so we can tell when the item is supposed to end.
                        if (lastStart != nextEval)
                        {
                            this.Add(new JsonValue(source, lastStart, nextEval - 1, false));
                        }

                        endIndex = nextEval;
                        break;

                    case '[':
                        {
                            int endOfArr;
                            this.Add(new JsonArray(indexer, source, nextEval, out endOfArr));
                            if (endOfArr == -1)
                            {
                                throw new FormatException($"Invalid json format provided, issue starts with array that begins at text index {nextEval}");
                            }

                            int peekAheadInd = indexer.NextAny(endOfArr + 1);
                            if (peekAheadInd != -1 && source.Text[peekAheadInd] == ',')
                            {
                                nextEval = peekAheadInd;
                            }
                            else
                            {
                                nextEval = endOfArr;
                            }
                        }
                        break;

                    case '{':
                        {
                            if (quoteStart != -1)
                            {
                                break;
                            }

                            int endOfDoc;
                            this.Add(new JsonDocument(indexer, source, nextEval, out endOfDoc));
                            if (endOfDoc == -1)
                            {
                                throw new FormatException($"Invalid json format provided, issue starts with document that begins at text index {nextEval}");
                            }

                            int peekAheadInd = indexer.NextAny(endOfDoc + 1);
                            if (peekAheadInd != -1 && source.Text[peekAheadInd] == ',')
                            {
                                nextEval = peekAheadInd;
                            }
                            else
                            {
                                nextEval = endOfDoc;
                            }
                        }
                        break;

                    case ',':
                        if (quoteStart != -1)
                        {
                            break;
                        }

                        // We're safe to assume we can add this here because commas after docs are covered above
                        this.Add(new JsonValue(source, lastStart, nextEval - 1, false));
                        break;

                    case '\"':
                        if (quoteStart != -1)
                        {
                            this.Add(new JsonValue(source, quoteStart, nextEval - 1, true));
                            quoteStart = -1;

                            int peekAheadInd = indexer.NextAny(nextEval + 1);
                            if (peekAheadInd != -1 && source.Text[peekAheadInd] == ',')
                            {
                                nextEval = peekAheadInd;
                            }
                        }
                        else
                        {
                            quoteStart = nextEval + 1;
                        }
                        break;
                    default:
                        lastWasUseful = false;
                        break;
                }

                if (endIndex != -1)
                {
                    break;
                }

                if (lastWasUseful)
                {
                    lastStart = nextEval + 1;
                }
                nextEval = indexer.NextAny(nextEval + 1);
            }

            this.ResetStringValue(startIndex, endIndex);
            this.Source.Track(this);
        }

        /// <summary>
        /// Constructor for building
        /// </summary>
        internal JsonArray (TrackedStringManager source, int startIndex) : base(source)
        {
            this.OffsetInSource = startIndex;
        }

        // ===================[ Properties ]===========================
        public override bool IsArray => true;
        public override bool IsValue => false;

        public int Count => m_values.Count;
        public JsonValue this[int index]
        {
            get
            {
                return m_values[index];
            }
        }

        // ===================[ Interface Methods ]===========================

        public JsonValue AppendNew(JsonBuilder valueBuilder)
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

            this.Source.BeginPlaceholderMode(trackerBuilder);
            JsonValue value = valueBuilder.BuildJsonValue(this.Source, trackerBuilder, currTabbing);
            this.Add(value);
            this.Source.PlaceholderSetupComplete();
            return value;
        }

        public JsonValue AppendNew(object value, JsonBuilder.Settings settings = null)
        {
            return AppendNew(JsonHelper.MakeValueBuilder(value, settings));
        }

        public IEnumerator<JsonValue> GetEnumerator()
        {
            return m_values.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_values.GetEnumerator();
        }

        // ===================[ Utility Methods ]===========================

        /// <summary>
        /// Internal utility - for building json
        /// </summary>
        internal void Add (JsonValue value)
        {
            // Don't add zero length value items
            if (value.IsValue && value.StringValue.Length == 0)
            {
                return;
            }

            value.Parent = this;
            m_values.Add(value);
        }

    }
}
