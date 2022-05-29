namespace AJut.Storage
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A simple utility for simplifying usage of Dictionary where the value is a list of values.
    /// </summary>
    public class MultiMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, List<TValue>>>
    {
        private readonly Dictionary<TKey, List<TValue>> m_store = new Dictionary<TKey, List<TValue>>();

        /// <summary>
        /// Get value list (or create it if it doesn't exist) for the given key
        /// </summary>
        public List<TValue> this[TKey key] => this.GetValuesFor(key, true);

        /// <summary>
        /// Add the key (and the list of values for that list if the key doesn't exist) and value to the key's list.
        /// </summary>
        public virtual void Add (TKey key, TValue value)
        {
            this.GetValuesFor(key, true).Add(value);
        }

        /// <summary>
        /// Removes and drops a key and all associated values
        /// </summary>
        public virtual bool RemoveAllFor (TKey key)
        {
            return m_store.Remove(key);
        }

        /// <summary>
        /// Remove value for a given key (and optionally remove the key if it's the last value)
        /// </summary>
        public virtual bool Remove (TKey key, TValue value, bool clearIfLast = true)
        {
            var values = this.GetValuesFor(key, false);
            if (values?.Remove(value) ?? false)
            {
                // If it's the last value - nix the list
                if (clearIfLast && values.Count == 0)
                {
                    m_store.Remove(key);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Remove all instances of the given value from all lists across all keys
        /// </summary>
        public void RemoveAllValues (TValue value)
        {
            foreach (var key in m_store.Keys)
            {
                this.Remove(key, value);
            }
        }

        /// <summary>
        /// Determine the total element count
        /// </summary>
        public int GetTotalElementCount () => m_store.Values.Sum(v => v.Count);

        /// <summary>
        /// Get the count for a given key's list (zero if key has not been stored)
        /// </summary>
        public virtual int GetCountFor (TKey key)
        {
            return this.GetValuesFor(key, false)?.Count ?? 0;
        }

        protected List<TValue> GetValuesFor (TKey key, bool createIfNonExistant = false)
        {
            if (!m_store.TryGetValue(key, out List<TValue> values))
            {
                if (!createIfNonExistant)
                {
                    return null;
                }

                values = new List<TValue>();
                m_store.Add(key, values);
            }

            return values;
        }

        /// <summary>
        /// Enumerator for iterating
        /// </summary>
        IEnumerator<KeyValuePair<TKey, List<TValue>>> IEnumerable<KeyValuePair<TKey, List<TValue>>>.GetEnumerator () => m_store.GetEnumerator();

        /// <summary>
        /// Enumerator for iterating
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator () => m_store.GetEnumerator();

    }
}
