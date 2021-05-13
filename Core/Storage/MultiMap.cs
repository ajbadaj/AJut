namespace AJut.Storage
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    // ================================================================================================================
    // ===[ AJut note: Expect Major Rework ]===
    //
    // Just messing with this file to say... oof. This needs optimization. None of the hashing beneifts of Dictionary
    //  just to make an easier multi element interface? Huge mistake, will have to rework :/

    public class MultiMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, List<TValue>>>
    {
        private readonly Dictionary<TKey, List<TValue>> m_store = new Dictionary<TKey, List<TValue>>();

        public List<TValue> this [TKey key] => this.GetValuesFor(key, true);

        public virtual void AddFrom<T> (T valueSource, Func<T, TKey> keySelector, Func<T, IEnumerable<TValue>> valuesSelector)
        {
            this.GetValuesFor(keySelector(valueSource), true).AddRange(valuesSelector(valueSource));
        }

        public virtual void Add (TKey key, TValue value)
        {
            this.GetValuesFor(key, true).Add(value);
        }

        public virtual bool Remove (TKey key, TValue value)
        {
            return this.GetValuesFor(key, false)?.Remove(value) ?? false;
        }

        public int GetTotalElementCount() => m_store.Values.Sum(v => v.Count);

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

        public IEnumerator<KeyValuePair<TKey, List<TValue>>> GetEnumerator () => m_store.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator () => m_store.GetEnumerator();
    }
}
