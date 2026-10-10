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

        /// <summary>
        /// The AJson version of the text this json was read from: the root document's version
        /// marker, or 0 when it had none. Json that was built rather than read has the current
        /// version, <see cref="JsonHelper.kCurrentAJsonVersion"/>.
        /// </summary>
        public int AJsonVersion { get; internal set; }

        /// <summary>
        /// Whether <see cref="ToString()"/> writes the AJson version marker. Null follows the
        /// settings the text is written with (those this json was built with, unless others are
        /// passed), then <see cref="JsonHelper.WriteAJsonVersion"/>. Only a root document can carry
        /// the marker, and the text of a JsonDocument or JsonArray written on its own never does.
        /// </summary>
        public bool? WriteAJsonVersion { get; set; }

        /// <summary>
        /// The settings this json was built with, which its text is written with. Null for json that
        /// was read rather than built.
        /// </summary>
        internal JsonBuilderSettings BuiltWith { get; set; }

        // ===============================[ Public Interface Methods ]===========================
        public static Json Failure (string error = null)
        {
            Json json = new Json();
            json.AddError(error ?? "Error creating json");
            return json;
        }

        /// <summary>
        /// Returns the serialized form of the json data, or a placeholder string if no data was parsed. Built json is
        /// written with the formatting of the settings it was built with, and json that was read with
        /// <see cref="JsonBuilderSettings.Default"/>.
        /// </summary>
        public override string ToString () => this.ToString(null);

        /// <summary>
        /// Returns the serialized form of the json data, formatted by <paramref name="settings"/>: tabbing, newlines, spacing,
        /// quoting and quote characters (<see cref="JsonBuilderSettings.BuildMinifiedSettings"/> for minified text). Null
        /// writes it the way <see cref="ToString()"/> does.
        /// </summary>
        public string ToString (JsonBuilderSettings settings)
        {
            if (this.Data == null)
            {
                return "<Invalid Source Text>";
            }

            JsonBuilderSettings writeWith = settings ?? this.BuiltWith ?? JsonBuilderSettings.Default;
            bool writeVersion = this.WriteAJsonVersion ?? writeWith.WriteAJsonVersion ?? JsonHelper.WriteAJsonVersion;
            int versionToWrite = writeVersion && this.Data.IsDocument ? JsonHelper.kCurrentAJsonVersion : 0;
            return JsonWriter.Write(this.Data, writeWith, versionToWrite);
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
