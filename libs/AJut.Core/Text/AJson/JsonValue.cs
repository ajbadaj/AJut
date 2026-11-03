namespace AJut.Text.AJson
{
    using AJut.Tree;

    /// <summary>
    /// A simple string value - or the basis of the other two json storage types
    /// (<see cref="JsonDocument"/> and <see cref="JsonArray"/>).
    /// </summary>
    public class JsonValue
    {
        /// <summary>The text of json's null literal</summary>
        internal const string kNullText = "null";

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

        /// <summary>
        /// Json's null literal, an unquoted null. It reads as null, or as a value type's default, and is always written as an
        /// unquoted null whatever the quoting settings say (a quoted "null" is the string). Its <see cref="StringValue"/> is
        /// the literal's text.
        /// </summary>
        public static JsonValue CreateNull ()
        {
            return new JsonValue(kNullText, isQuoted: false) { IsNull = true };
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

        /// <summary>
        /// Whether this is json's null literal (see <see cref="CreateNull"/>): what a parse makes of an unquoted null, and what
        /// a dictionary pair holding a null value writes
        /// </summary>
        public bool IsNull { get; private set; }

        public virtual bool IsArray => false;
        public virtual bool IsDocument => false;
        public virtual bool IsValue => true;

        public JsonValue Parent { get; internal set; }

        // ===============================[ Public Interface Methods ]===========================
        public override string ToString ()
        {
            return this.StringValue;
        }

        /// <summary>
        /// Writes this as json text, formatted by <paramref name="settings"/> (<see cref="JsonBuilderSettings.Default"/> when
        /// null): a document or an array with that tabbing, newlines and quoting, and a single value as it appears in json,
        /// quoted and escaped if it is a string. That last is unlike <see cref="ToString()"/>, which gives a single value's
        /// raw text.
        /// </summary>
        public string ToString (JsonBuilderSettings settings)
        {
            return JsonWriter.Write(this, settings);
        }
    }
}
