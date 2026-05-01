namespace AJut.Text.AJson
{
    using AJut.Storage;
    using AJut.Tree;

    public delegate string Formatter (string input);

    /// <summary>
    /// Top-level wrapper returned by parse and build APIs. Carries the parsed root value plus
    /// any errors encountered. Always non-null - consumers check <see cref="Result.HasErrors"/>
    /// before trusting <see cref="Data"/>.
    /// </summary>
    public class Json : Result
    {
        private JsonValue m_data;

        // ===============================[ Construction ]===========================
        public Json ()
        {
        }

        // ===============================[ Properties ]===========================
        public JsonValue Data
        {
            get => m_data;
            internal set
            {
                m_data = value;
                if (m_data != null)
                {
                    this.Traverser = new TreeTraverser<JsonValue>(m_data);
                }
            }
        }

        public TreeTraverser<JsonValue> Traverser { get; private set; }

        // ===============================[ Public Interface Methods ]===========================
        public static Json Failure (string error = null)
        {
            Json json = new Json();
            json.AddError(error ?? "Error creating json");
            return json;
        }

        /// <summary>
        /// Returns the serialized form of the json data, or a placeholder string if no data was parsed.
        /// </summary>
        public override string ToString ()
        {
            return this.Data != null ? JsonWriter.Write(this.Data) : "<Invalid Source Text>";
        }

        public void FormatAllKeys (Formatter keyStringFormatter)
        {
            if (this.Data == null)
            {
                return;
            }

            foreach (JsonValue value in TreeTraversal<JsonValue>.All(this.Data))
            {
                if (value.IsDocument)
                {
                    ((JsonDocument)value).FormatAllKeys(keyStringFormatter);
                }
            }
        }

        public void FormatAllValues (Formatter valueStringFormatter)
        {
            if (this.Data == null)
            {
                return;
            }

            foreach (JsonValue value in TreeTraversal<JsonValue>.All(this.Data))
            {
                if (value.IsValue)
                {
                    value.StringValue = valueStringFormatter(value.StringValue);
                }
            }
        }

        public void FormatAll (Formatter stringFormatter)
        {
            if (this.Data == null)
            {
                return;
            }

            foreach (JsonValue value in TreeTraversal<JsonValue>.All(this.Data))
            {
                if (value.IsDocument)
                {
                    ((JsonDocument)value).FormatAllKeys(stringFormatter);
                }
                else if (value.IsValue)
                {
                    value.StringValue = stringFormatter(value.StringValue);
                }
            }
        }
    }
}
