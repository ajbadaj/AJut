namespace AJut.Text.AJson
{
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// An array of json values - any element can be a value, a JsonDocument, or another JsonArray.
    /// </summary>
    public class JsonArray : JsonValue, IEnumerable<JsonValue>
    {
        private List<JsonValue> m_values;

        // ===============================[ Construction ]===========================
        public JsonArray ()
        {
            m_values = new List<JsonValue>();
        }

        internal JsonArray (int initialCapacity)
        {
            m_values = new List<JsonValue>(initialCapacity);
        }

        // ===============================[ Properties ]===========================
        public override bool IsArray => true;
        public override bool IsValue => false;

        public int Count => m_values.Count;

        public JsonValue this[int index] => m_values[index];

        public override string StringValue
        {
            get => JsonWriter.Write(this);
            set { /* arrays serialize on demand */ }
        }

        // ===============================[ Public Interface Methods ]===========================
        public void Add (JsonValue value)
        {
            value.Parent = this;
            m_values.Add(value);
        }

        public bool Remove (JsonValue value) => m_values.Remove(value);

        public void RemoveAt (int index) => m_values.RemoveAt(index);

        /// <summary>
        /// Appends <paramref name="value"/>, built the way <see cref="JsonHelper.BuildJsonForObject(object, JsonBuilderSettings)"/>
        /// builds one. A null value is json's null literal.
        /// </summary>
        public JsonValue AppendNew (object value, JsonBuilderSettings settings = null)
        {
            JsonValue built = JsonHelper.MakeValueBuilder(value, settings).BuildJsonValue();
            this.Add(built);
            return built;
        }

        /// <summary>
        /// Appends what <paramref name="valueBuilder"/> builds. A null builder appends json's null literal, the same as a null
        /// value, which matters because a bare null passed to AppendNew picks this overload.
        /// </summary>
        public JsonValue AppendNew (JsonBuilder valueBuilder)
        {
            JsonValue built = valueBuilder?.BuildJsonValue() ?? JsonValue.CreateNull();
            this.Add(built);
            return built;
        }

        public IEnumerator<JsonValue> GetEnumerator () => m_values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator () => m_values.GetEnumerator();
    }
}
