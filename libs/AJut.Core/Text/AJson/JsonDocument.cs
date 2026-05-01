namespace AJut.Text.AJson
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.Tree;

    public class JsonDocument : JsonValue, IEnumerable<KeyValuePair<string, JsonValue>>
    {
        public const string kTypeIndicator = "__type";
        public const string kKVPKeyTypeIndicator = "__key-type";
        public const string kKVPValueTypeIndicator = "__value-type";
        public const string kRuntimeTypeEvalValue = "__value";

        private List<KeyValuePair<string, JsonValue>> m_memberStorage;

        // ===============================[ Construction ]===========================
        public JsonDocument ()
        {
            m_memberStorage = new List<KeyValuePair<string, JsonValue>>();
        }

        internal JsonDocument (int initialCapacity)
        {
            m_memberStorage = new List<KeyValuePair<string, JsonValue>>(initialCapacity);
        }

        // ===============================[ Properties ]===========================
        public override bool IsDocument => true;
        public override bool IsValue => false;

        public int Count => m_memberStorage.Count;

        public override string StringValue
        {
            get => JsonWriter.Write(this);
            set { /* documents serialize on demand; no-op set keeps the base contract */ }
        }

        // ===============================[ Public Interface Methods ]===========================
        public string KeyAt (int index) => m_memberStorage[index].Key;
        public JsonValue ValueAt (int index) => m_memberStorage[index].Value;
        public KeyValuePair<string, JsonValue> KeyAndValueAt (int index) => m_memberStorage[index];

        public void Add (string key, JsonValue value)
        {
            value.Parent = this;
            m_memberStorage.Add(new KeyValuePair<string, JsonValue>(key, value));
        }

        /// <summary>
        /// Append a new property by building its value via the JsonBuilder pipeline. V2 in-memory
        /// mutation - the consumer re-serializes via ToString to get the updated text.
        /// </summary>
        public JsonValue AppendNew (string key, object value, JsonBuilderSettings settings = null)
        {
            JsonValue built = JsonHelper.MakeValueBuilder(value, settings).BuildJsonValue();
            this.Add(key, built);
            return built;
        }

        public JsonValue AppendNew (string key, JsonBuilder valueBuilder)
        {
            JsonValue built = valueBuilder.BuildJsonValue();
            this.Add(key, built);
            return built;
        }

        /// <summary>
        /// Upsert - replaces the value if the key already exists, appends otherwise. New in V2.
        /// </summary>
        public JsonValue Set (string key, object value, JsonBuilderSettings settings = null)
        {
            JsonValue built = JsonHelper.MakeValueBuilder(value, settings).BuildJsonValue();
            built.Parent = this;
            for (int i = 0; i < m_memberStorage.Count; ++i)
            {
                if (m_memberStorage[i].Key == key)
                {
                    m_memberStorage[i] = new KeyValuePair<string, JsonValue>(key, built);
                    return built;
                }
            }

            m_memberStorage.Add(new KeyValuePair<string, JsonValue>(key, built));
            return built;
        }

        public bool Remove (string key)
        {
            for (int i = 0; i < m_memberStorage.Count; ++i)
            {
                if (m_memberStorage[i].Key == key)
                {
                    m_memberStorage.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public bool ContainsKey (string key) => m_memberStorage.Any(kvp => kvp.Key == key);

        public JsonValue[] AllValuesForKey (string key)
            => m_memberStorage.Where(kvp => kvp.Key == key).Select(kvp => kvp.Value).ToArray();

        public IEnumerable<JsonValue> AllValues ()
            => m_memberStorage.Select(kvp => kvp.Value);

        public IEnumerable<string> AllKeys ()
            => m_memberStorage.Select(kvp => kvp.Key);

        public JsonValue ValueFor (string key)
            => m_memberStorage.Where(kvp => kvp.Key == key).Select(kvp => kvp.Value).FirstOrDefault();

        public string KeyFor (JsonValue value)
            => m_memberStorage.Where(kvp => kvp.Value == value).Select(kvp => kvp.Key).FirstOrDefault();

        public bool TryGetValue<T> (string key, out T foundValue)
        {
            JsonValue value = this.ValueFor(key);
            if (value == null)
            {
                foundValue = default;
                return false;
            }

            foundValue = JsonHelper.BuildObjectForJson<T>(value);
            return true;
        }

        /// <summary>
        /// Tree search for a child document holding the given key, returning that key's value.
        /// </summary>
        public JsonValue FindValueByKey (string key, eTraversalStrategy strategy = eTraversalStrategy.BreadthFirst, eTraversalFlowDirection direction = eTraversalFlowDirection.ThroughChildren)
        {
            JsonValue found = TreeTraversal<JsonValue>.GetFirstChildWhichPasses(this, _TestIsDocumentWithKey, direction, strategy);
            return found is JsonDocument doc ? doc.ValueFor(key) : null;

            bool _TestIsDocumentWithKey (JsonValue value)
            {
                return value.IsDocument && ((JsonDocument)value).ContainsKey(key);
            }
        }

        public IEnumerator<KeyValuePair<string, JsonValue>> GetEnumerator () => m_memberStorage.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator () => m_memberStorage.GetEnumerator();

        // ---------- Format helpers (plain in-memory mutation, no edit tracker in V2) ----------

        public void FormatAllKeys (Formatter keyStringFormatter)
        {
            for (int i = 0; i < m_memberStorage.Count; ++i)
            {
                KeyValuePair<string, JsonValue> kvp = m_memberStorage[i];
                m_memberStorage[i] = new KeyValuePair<string, JsonValue>(keyStringFormatter(kvp.Key), kvp.Value);
            }
        }

        public void FormatAllValues (Formatter valueStringFormatter)
        {
            foreach (JsonValue value in TreeTraversal<JsonValue>.All(this, includeSelf: false))
            {
                if (value.IsValue)
                {
                    value.StringValue = valueStringFormatter(value.StringValue);
                }
            }
        }

        public void FormatAll (Formatter stringFormatter)
        {
            foreach (JsonValue value in TreeTraversal<JsonValue>.All(this))
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
