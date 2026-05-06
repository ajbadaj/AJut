namespace AJut.Text.AJson
{
    using AJut.Tree;

    /// <summary>
    /// A simple string value - or the basis of the other two json storage types
    /// (<see cref="JsonDocument"/> and <see cref="JsonArray"/>).
    /// </summary>
    public class JsonValue
    {
        // ===============================[ Construction ]===========================
        static JsonValue ()
        {
            JsonValue[] emptyEnumerable = new JsonValue[0];
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

        public JsonValue ()
        {
            this.IsQuoted = true;
        }

        public JsonValue (string stringValue, bool isQuoted = true)
        {
            this.StringValue = stringValue;
            this.IsQuoted = isQuoted;
        }

        // ===============================[ Properties ]===========================
        /// <summary>
        /// Raw string contents for value-kind json. For JsonDocument and JsonArray this
        /// stays null until something explicitly populates it - serialization goes through
        /// <see cref="ToString"/> on the document/array overrides instead.
        /// </summary>
        public virtual string StringValue { get; set; }

        /// <summary>
        /// Whether this value was originally quoted (or, for object-built values, whether the
        /// type would normally serialize quoted - strings, chars, enums, dates, guids, etc).
        /// Documents and arrays ignore this. Numeric / bool / null values use false.
        /// </summary>
        public bool IsQuoted { get; set; }

        public virtual bool IsArray => false;
        public virtual bool IsDocument => false;
        public virtual bool IsValue => true;

        public JsonValue Parent { get; internal set; }

        // ===============================[ Public Interface Methods ]===========================
        public override string ToString ()
        {
            return this.StringValue;
        }
    }
}
