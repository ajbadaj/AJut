namespace AJut.Text.AJson.Legacy
{
    using System;
    using AJut.Storage;
    using AJut.Tree;

    [Obsolete("AJson V1 is moved to AJut.Text.AJson.Legacy and will be removed in a future release. Migrate to AJut.Text.AJson (V2). See AJut README for migration notes.")]
    public delegate string Formatter (string input);

    /// <summary>
    /// Stores parsed json data and tracking information
    /// </summary>
    [Obsolete("AJson V1 is moved to AJut.Text.AJson.Legacy and will be removed in a future release. Migrate to AJut.Text.AJson (V2). See AJut README for migration notes.")]
    public class Json : Result
    {
        private JsonValue m_data;

        internal Json(TrackedStringManager tracker)
        {
            this.TextTracking = tracker;
        }

        /// <summary>
        /// The parsed json data
        /// </summary>
        public JsonValue Data
        {
            get => m_data;
            internal set
            {
                m_data = value;
                if (m_data != null)
                {
                    Traverser = new TreeTraverser<JsonValue>(m_data);
                }
            }
        }

        /// <summary>
        /// Build a generic json failure. Uers of Json api expect Json to always be non-null, if you have a scenario
        /// where you may make json or not and are returning json, you might want a way to create a generic failure
        /// which is what this is for.
        /// </summary>
        public static Json Failure (string error = null)
        {
            var json = new Json(null);
            json.AddError(error ?? "Error creating json");
            return json;
        }

        /// <summary>
        /// The text tracking source
        /// </summary>
        public TrackedStringManager TextTracking { get; private set; }

        /// <summary>
        /// A pre-made tree traverser for easier tree searches of this Json
        /// </summary>
        public TreeTraverser<JsonValue> Traverser { get; private set; }

        /// <summary>
        /// The json data as a string
        /// </summary>
        public override string ToString()
        {
            if (this.Data != null)
            {
                return this.Data.StringValue;
            }
            else
            {
                return "<Invalid Source Text>";
            }
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
