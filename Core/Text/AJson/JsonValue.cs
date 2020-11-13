namespace AJut.Text.AJson
{
    using AJut.Text;
    using AJut.Tree;

    /// <summary>
    /// A simple string value - or the basis of the other two json storage types (<see cref="JsonDocument"/> and <see cref="JsonArray"/>)
    /// </summary>
    public class JsonValue : TrackedString
    {
        // ===============================[ Construction ]===========================
        static JsonValue ()
        {
            JsonValue[] emptyEnumerable = new JsonValue[0];
            // Setup tree traversal
            TreeTraversal<JsonValue>.SetupDefaults(
                jv =>
                {
                    if (jv.IsDocument)
                    {
                        return ((JsonDocument)jv).AllValues();
                    }
                    else if (jv.IsArray)
                    {
                        return (JsonArray)jv;
                    }

                    return emptyEnumerable;
                }, jv => jv.Parent);
        }

        /// <summary>
        /// Base class constructor
        /// </summary>
        internal JsonValue (TrackedStringManager source) : base(source)
        {
            // Track value types manually with our source. This is done because values aren't parsed, 
            //  they're created in documents or arrays and so don't have a chance to know if they need
            //  to be tracked or not (done at the end of parse in document and array).
            //
            // If we are in placeholder mode, then we are *not* adding it yet as we don't know the whole 
            //  range of the json value until we're done building our placeholders, but we need to allocate 
            //  it in order to process it.
            if (this.IsValue && !this.Source.IsInPlaceholderMode)
            {
                this.Source.Track(this);
            }
        }

        /// <summary>
        /// Constructor (for parsing)
        /// </summary>
        internal JsonValue (TrackedStringManager source, int start, int end, bool isInsideQuote) : this(source)
        {
            this.ResetStringValue(start, end, isInsideQuote);
        }

        /// <summary>
        /// Constructor (for building)
        /// </summary>
        internal JsonValue (TrackedStringManager source, int start, string value) : this(source)
        {
            this.SetValueInternal(value);
            this.OffsetInSource = start;
        }

        // ===============================[ Properties ]===========================
        public virtual bool IsArray => false;
        public virtual bool IsDocument => false;
        public virtual bool IsValue => true;

        public JsonValue Parent { get; internal protected set; }

        // ===============================[ Methods ]===========================
        public override string ToString()
        {
            return this.StringValue;
        }

        protected internal void ResetStringValue (int startIndex, int endIndex, bool isInsideQuote = false)
        {
            string str;
            if (endIndex == -1)
            {
                str = this.Source.Text.Substring(startIndex);
            }
            else
            {
                str = this.Source.Text.SubstringWithIndices(startIndex, endIndex);
            }

            // If we're not inside quotes, then we are going to assume whitespace needs to be removed, to understand why
            //  imagine a json document like this: { thing:  4 }. We know to start tracking the value after ":" and to end
            //  tracking it at "}". The result is "  4 ", when clearly we want "4". However, if there were qutoes that would
            //  be much different: {thing: "  4 "} - in that case "  4 " is clearly what we want the value to be.
            if (this.IsValue && !isInsideQuote)
            {
                str = JsonHelper.TrimUnquotedValue(str, out int offsetFromStart);
                this.OffsetInSource = startIndex + offsetFromStart;
            }
            else
            {
                this.OffsetInSource = startIndex;
            }

            this.SetValueInternal(str);
        }
    }
}
