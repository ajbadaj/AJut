namespace AJut.Text.AJson
{
    using AJut.Storage;
    using AJut.Tree;

    /// <summary>
    /// Stores parsed json data and tracking information
    /// </summary>
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
    }
}
