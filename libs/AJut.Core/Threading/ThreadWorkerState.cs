namespace AJut.Threading
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class ThreadWorkerState<T>
    {
        private readonly object m_lock = new object();
        private List<T> m_data = new List<T>();

        // ==================[ Methods ]============================
        public void DisposeAndClearDisposables ()
        {
            lock (m_lock)
            {
                if (typeof(IDisposable).IsAssignableFrom(typeof(T)))
                {
                    foreach (IDisposable item in m_data)
                    {
                        item.Dispose();
                    }
                }

                m_data.Clear();
            }
        }
        public void Add (T data)
        {
            lock (m_lock)
            {
                m_data.Add(data);
                this.OnItemsAdded();
            }

            this.OnItemsAdded();
        }

        public void AddRange (IEnumerable<T> data)
        {
            lock (m_lock)
            {
                m_data.AddRange(data);
                this.OnItemsAdded();
            }

            this.OnItemsAdded();
        }

        public void Remove (T data)
        {
            lock (m_lock)
            {
                m_data.Remove(data);
            }

            this.OnItemsRemoved();
        }

        public T[] ToArray ()
        {
            lock (m_lock)
            {
                return m_data.ToArray();
            }
        }

        public bool Any ()
        {
            lock (m_lock)
            {
                return m_data.Any();
            }
        }

        public T[] RemoveAll (Predicate<T> match)
        {
            var result = new List<T>();
            lock (m_lock)
            {
                foreach (T item in m_data.ToList())
                {
                    if (match(item))
                    {
                        result.Add(item);
                    }

                    m_data.Remove(item);
                }
            }

            this.OnItemsRemoved();
            return result.ToArray();
        }

        public void Clear ()
        {
            lock (m_lock)
            {
                m_data.Clear();
            }

            this.OnItemsRemoved();
        }

        public T Take (int index)
        {
            T item;
            lock (m_lock)
            {
                if (m_data.Count <= index)
                {
                    return default;
                }

                item = m_data[index];
                m_data.RemoveAt(index);
            }

            this.OnItemsRemoved();
            return item;
        }

        public T[] TakeAll ()
        {
            T[] result;
            lock (m_lock)
            {
                result = m_data.ToArray();
                m_data.Clear();
            }

            this.OnItemsRemoved();
            return result;
        }

        // ==================[ Helpers ]============================
        protected virtual void OnItemsAdded () { }
        protected virtual void OnItemsRemoved () { }
    }
}
