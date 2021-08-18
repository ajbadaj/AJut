namespace AJut.Storage
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using AJut.Extensions;
    using AJut.Storage.ListOperations;

    /// <summary>
    /// A list whose actions are notified out. While notification events are consolodated - UI elements require the individualistic INotifyCollectionChanged
    /// style of operation, which this class supports as well - simply subscribe to the notification style you like and you will get the results you want
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class ObservableListBase<T> : NotifyPropertyChanged,
        IObservableList,
        INotifyCollectionChanged,
        ICollection<T>, IEnumerable<T>, IEnumerable, IList<T>, IReadOnlyCollection<T>, IReadOnlyList<T>, ICollection, IList
    {
        protected static readonly PropertyChangedEventArgs kCountPropChange = new PropertyChangedEventArgs(nameof(Count));
        protected static readonly PropertyChangedEventArgs kIndexerPropChange = new PropertyChangedEventArgs("Item[]");

        private readonly List<T> m_backing;
        private event EventHandler<NotifyListCompositionChangedEventArgs> m_untypedListCompositionChangedEvent;
        private event NotifyCollectionChangedEventHandler m_collectionChangedEvent;

        // ===============================[ Construction ]==================================
        public ObservableListBase () => m_backing = new List<T>();
        public ObservableListBase (IEnumerable<T> elements) => m_backing = new List<T>(elements);
        public ObservableListBase (int capacity) => m_backing = new List<T>(capacity);
        protected ObservableListBase (ObservableListBase<T> referenceSource) => m_backing = referenceSource.m_backing;

        // ===============================[ Events ]==================================
        
        /// <summary>
        /// Sign up for typed list composition change events
        /// </summary>
        public event EventHandler<NotifyListCompositionChangedEventArgs<T>> ListCompositionChanged;

        /// <summary>
        /// Sign up for untyped list composition change events
        /// </summary>
        event EventHandler<NotifyListCompositionChangedEventArgs> IObservableList.ListCompositionChanged
        {
            add => this.m_untypedListCompositionChangedEvent += value;
            remove => this.m_untypedListCompositionChangedEvent -= value;
        }

        /// <summary>
        /// Sign up for single change limited <see cref="INotifyCollectionChanged.CollectionChanged"/> events
        /// </summary>
        event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged
        {
            add => m_collectionChangedEvent += value;
            remove => m_collectionChangedEvent -= value;
        }

        // ===============================[ Properties ]==================================
        protected List<T> BackingList => m_backing;
        public bool IsEmpty => this.Count == 0;
        public bool HasElements => this.Count != 0;
        public int Count => m_backing.Count;

        /// <summary>
        /// Gets or sets the total number of elements the internal data structure can hold without resizing.
        /// </summary>
        public int Capacity
        {
            get => m_backing.Capacity;
            set
            {
                if (m_backing.Capacity != value)
                {
                    m_backing.Capacity = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        public T this[int index]
        {
            get => m_backing[index];
            set => this.DoReplace(index, value);
        }

        public abstract bool IsReadOnly { get; }

        // ===============================[ General Interface ]==================================
        public void Add (T item) => this.DoInsert(this.Count, item);

        public void Clear () => this.DoClear();

        public bool Contains (T item) => m_backing.Contains(item);

        public void AddEach (IEnumerable<T> elements) => this.DoAdd(elements);
        public void AddEach (params T[] elements) => this.DoAdd(elements);

        public void RemoveEach (IEnumerable<T> elements) => this.DoRemoveEach(elements);
        public void RemoveEach (params T[] elements) => this.DoRemoveEach(elements);
        public void RemoveAll (Predicate<T> predicate) => this.DoRemoveEach(m_backing.Where(e => predicate(e)));

        public int IndexOf (T element) => m_backing.IndexOf(element);
        public void Insert (int index, T element) => this.DoInsert(index, element);

        public bool Remove (T item) => this.DoRemove(item);
        public void RemoveAt (int index) => this.DoRemoveAt(index);

        public void Swap (int leftIndex, int rightIndex) => this.DoSwap(leftIndex, rightIndex);

        #region ICollection<T> + IEnumerable<T> + IEnumerable + IReadOnlyCollection<T>

        /// <summary>
        /// Gets a value indicating whether access to the <see cref="ICollection"/> is synchronized (thread safe).
        /// </summary>
        bool ICollection.IsSynchronized => ((ICollection)m_backing).IsSynchronized;

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see cref="ICollection"/>.
        /// </summary>
        object ICollection.SyncRoot => ((ICollection)m_backing).SyncRoot;

        bool IList.IsFixedSize => false;
        
        IEnumerator<T> IEnumerable<T>.GetEnumerator () => m_backing.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator () => m_backing.GetEnumerator();

        void ICollection.CopyTo (Array array, int index) => ((ICollection)m_backing).CopyTo(array, index);

        object IList.this[int index]
        {
            get => m_backing[index];
            set => this.DoReplace(index, (T)value);
        }

        int IList.Add (object element) => this.DoInsert(-1, (T)element);

        bool IList.Contains (object value) => ((IList)m_backing).Contains(value);
        int IList.IndexOf (object value) => ((IList)m_backing).IndexOf(value);
        void IList.Insert (int index, object element) => this.DoInsert(index, (T)element);
        void IList.Remove (object element) => this.DoRemove((T)element);

        #endregion

        #region List<T> forwarding
        #nullable enable
        /// <summary>
        /// Searches a range of elements in the sorted list for an element
        /// using the specified comparer and returns the zero-based index
        /// of the element.
        /// </summary>
        public int BinarySearch (int index, int count, T item, IComparer<T>? comparer) => m_backing.BinarySearch(index, count, item, comparer);

        /// <summary>
        /// Searches the entire sorted list for an element using
        /// the default comparer and returns the zero-based index of the element.
        /// </summary>
        public int BinarySearch (T item) => m_backing.BinarySearch(item);

        /// <summary>
        /// Searches the entire sorted System.Collections.Generic.List`1 for an element using
        /// the specified comparer and returns the zero-based index of the element.
        /// </summary>
        public int BinarySearch (T item, IComparer<T>? comparer) => m_backing.BinarySearch(item, comparer);

        /// <summary>
        /// Copies the entire list to a compatible one-dimensional
        /// array, starting at the specified index of the target array.
        /// </summary>
        public void CopyTo (T[] array, int arrayIndex) => m_backing.CopyTo(array, arrayIndex);

        /// <summary>
        /// Copies the entire list to a compatible one-dimensional
        /// array, starting at the beginning of the target array.
        /// </summary>
        public void CopyTo (T[] array) => m_backing.CopyTo(array);

        /// <summary>
        /// Copies a range of elements from the list to a compatible one-dimensional array, 
        /// starting at the specified index of the target array.
        /// </summary>
        public void CopyTo (int index, T[] array, int arrayIndex, int count) => m_backing.CopyTo(index, array, arrayIndex, count);

        /// <summary>
        /// Determines whether the System.Collections.Generic.List`1 contains elements that
        /// match the conditions defined by the specified predicate.
        /// </summary>
        public bool Exists (Predicate<T> match) => m_backing.Exists(match);

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified
        /// predicate, and returns the first occurrence within the entire list.
        /// </summary>
        public T? Find (Predicate<T> match) => m_backing.Find(match);

        /// <summary>
        /// Retrieves all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        public List<T> FindAll (Predicate<T> match) => m_backing.FindAll(match);

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified
        /// predicate, and returns the zero-based index of the first occurrence within the
        /// range of elements in the list that starts at the specified index and contains 
        /// the specified number of elements.
        /// </summary>
        public int FindIndex (int startIndex, int count, Predicate<T> match) => m_backing.FindIndex(startIndex, count, match);

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified
        /// predicate, and returns the zero-based index of the first occurrence within the
        /// range of elements in the list that extends from the specified index to the last element.
        /// </summary>
        public int FindIndex (int startIndex, Predicate<T> match) => m_backing.FindIndex(startIndex, match);

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified
        /// predicate, and returns the zero-based index of the first occurrence within the
        /// entire list.
        /// </summary>
        public int FindIndex (Predicate<T> match) => m_backing.FindIndex(match);

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified
        /// predicate, and returns the last occurrence within the entire list.
        /// </summary>
        public T? FindLast (Predicate<T> match) => m_backing.FindLast(match);

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified
        /// predicate, and returns the zero-based index of the last occurrence within the
        /// range of elements in the list that contains the specified number of elements
        /// and ends at the specified index.
        /// </summary>
        public int FindLastIndex (int startIndex, int count, Predicate<T> match) => m_backing.FindLastIndex(startIndex, count, match);

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified
        /// predicate, and returns the zero-based index of the last occurrence within the
        /// range of elements in the list that extends from
        /// the first element to the specified index.
        public int FindLastIndex (int startIndex, Predicate<T> match) => m_backing.FindLastIndex(startIndex, match);

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified
        /// predicate, and returns the zero-based index of the last occurrence within the
        /// entire list.
        /// </summary>
        public int FindLastIndex (Predicate<T> match) => m_backing.FindLastIndex(match);

        /// <summary>
        /// Performs the specified action on each element of the list.
        /// </summary>
        public void ForEach (Action<T> action) => m_backing.ForEach(action);

        /// <summary>
        /// Returns an enumerator that iterates through the list.
        /// </summary>
        public List<T>.Enumerator GetEnumerator () => m_backing.GetEnumerator();

        /// <summary>
        /// Creates a shallow copy of a range of elements in the source list.
        /// </summary>
        public List<T> GetRange (int index, int count) => m_backing.GetRange(index, count);

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the first
        /// occurrence within the range of elements in the list
        /// that starts at the specified index and contains the specified number of elements.
        /// </summary>
        public int IndexOf (T item, int index, int count) => m_backing.IndexOf(item, index, count);

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the first
        /// occurrence within the range of elements in the list
        /// that extends from the specified index to the last element.
        /// </summary>
        public int IndexOf (T item, int index) => m_backing.IndexOf(item, index);

        /// <summary>
        /// Inserts the elements of a collection into the list
        /// at the specified index.
        /// </summary>
        public void InsertRange (int index, IEnumerable<T> collection) => this.DoInsert(index, collection);

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the last
        /// occurrence within the entire list.
        /// </summary>
        public int LastIndexOf (T item) => m_backing.LastIndexOf(item);

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the last
        /// occurrence within the range of elements in the list
        /// that extends from the first element to the specified index.
        /// </summary>
        public int LastIndexOf (T item, int index) => m_backing.LastIndexOf(item, index);

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the last
        /// occurrence within the range of elements in the list
        /// that contains the specified number of elements and ends at the specified index.
        /// </summary>
        public int LastIndexOf (T item, int index, int count) => m_backing.LastIndexOf(item, index, count);

        /// <summary>
        /// Removes a range of elements from the list.
        /// </summary>
        public void RemoveRange (int index, int count) => this.RemoveEach(m_backing.GetRange(index, count));

        /// <summary>
        /// Reverses the order of the elements in the specified range.
        /// </summary>
        public void Reverse (int index, int count) => this.DoReverse(index, count);

        /// <summary>
        /// Reverses the order of the elements in the entire list.
        /// </summary>
        public void Reverse () => this.DoReverse(0, this.Count);

        /// <summary>
        /// Sorts the elements in the entire list using the
        /// specified System.Comparison`1.
        /// </summary>
        public void Sort (Comparison<T> comparison) => Algorithms.Sort.QuickSortInplace(this, comparison, this.DoSwap);

        /// <summary>
        /// Sorts the elements in a range of elements in list
        /// using the specified comparer.
        /// </summary>
        public void Sort (int index, int count, IComparer<T>? comparer) => Algorithms.Sort.QuickSortInplace(this, comparer.ToComparison(), this.DoSwap, index, index + count);

        /// <summary>
        /// Sorts the elements in the entire list using the
        /// default comparer.
        /// </summary>
        public void Sort () => Algorithms.Sort.QuickSortInplace(this, this.DoSwap);

        /// <summary>
        /// Sorts the elements in the entire list using the
        /// specified comparer.
        /// </summary>
        public void Sort (IComparer<T>? comparer) => Algorithms.Sort.QuickSortInplace(this, comparer.ToComparison(), this.DoSwap);

        /// <summary>
        /// Copies the elements of the list to a new array.
        /// </summary>
        public T[] ToArray () => m_backing.ToArray();

        /// <summary>
        /// Sets the capacity to the actual number of elements in the list,
        /// if that number is less than a threshold value.
        /// </summary>
        public void TrimExcess () => m_backing.TrimExcess();

        /// <summary>
        /// Determines whether every element in the list matches
        /// the conditions defined by the specified predicate.
        /// </summary>
        public bool TrueForAll (Predicate<T> match) => m_backing.TrueForAll(match);
        #nullable disable
        #endregion

        // ======================== [ Utility Interface ] ==================================

        protected void DoAdd (IEnumerable<T> elements) => this.DoInsert(-1, elements);
        protected void DoAdd (params T[] elements) => this.DoInsert(-1, elements);

        protected int DoInsert (int index, params T[] elements) => this.DoInsert(index, (IEnumerable<T>)elements);

        protected void DoRemoveAt (int index)
        {
            this.DoRemove(m_backing[index]);
        }

        protected bool DoRemove (T element)
        {
            return this.DoRemoveEach(new[] { element }) == 1;
        }

        protected void RaiseAllStandardEvents (NotifyListCompositionChangedEventArgs<T> listChangedArgs)
        {
            this.RaiseListCompositionChangedEvents(listChangedArgs);
            this.RaiseCollectionChangedEventsFor(listChangedArgs);
        }

        protected void RaiseListCompositionChangedEvents (NotifyListCompositionChangedEventArgs<T> listChangedArgs)
        {
            this.ListCompositionChanged?.Invoke(this, listChangedArgs);
            m_untypedListCompositionChangedEvent?.Invoke(this, listChangedArgs);
        }

        protected void RaiseCollectionChangedEventsFor (NotifyListCompositionChangedEventArgs<T> eventArgs)
        {
            if (m_collectionChangedEvent != null)
            {
                foreach (var collectionChangedArgs in eventArgs.Operations.SelectMany(o => o.ToCollectionChanged()))
                {
                    m_collectionChangedEvent.Invoke(this, collectionChangedArgs);
                }
            }
        }

        protected void RaiseCollectionChangedEventArgsFor (NotifyCollectionChangedEventArgs eventArgs)
        {
            m_collectionChangedEvent.Invoke(this, eventArgs);
        }

        // ======================== [ Abstract Methods ] ==================================

        /// <summary>
        /// Insert an element at a given index, or -1 to simply add it
        /// </summary>
        /// <returns>The index inserted or -1 if not</returns>
        protected abstract int DoInsert (int index, IEnumerable<T> elements);
        protected abstract int DoRemoveEach (IEnumerable<T> elements);
        protected abstract void DoClear ();
        protected abstract void DoReplace (int index, T value);
        protected abstract void DoReverse (int startIndex, int count);
        protected abstract void DoSwap (int leftIndex, int rightIndex);
    }
}
