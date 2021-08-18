namespace AJut.Storage
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using AJut.Algorithms;
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

        // #error Should almost definitely implement ALL of the missing List<T> pieces here
        private readonly List<T> m_backing;
        private event EventHandler<NotifyListCompositionChangedEventArgs> m_untypedListCompositionChangedEvent;
        private event NotifyCollectionChangedEventHandler m_collectionChangedEvent;

        // ===============================[ Construction ]==================================
        public ObservableListBase () => m_backing = new List<T>();
        public ObservableListBase (IEnumerable<T> elements) => m_backing = new List<T>(elements);
        public ObservableListBase (int capacity) => m_backing = new List<T>(capacity);
        protected ObservableListBase (ObservableListBase<T> referenceSource) => m_backing = referenceSource.m_backing;

        // ===============================[ Events ]==================================
        public event EventHandler<NotifyListCompositionChangedEventArgs<T>> ListCompositionChanged;
        event EventHandler<NotifyListCompositionChangedEventArgs> IObservableList.ListCompositionChanged
        {
            add => this.m_untypedListCompositionChangedEvent += value;
            remove => this.m_untypedListCompositionChangedEvent -= value;
        }

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

        public T this[int index]
        {
            get => m_backing[index];
            set => this.DoReplace(index, value);
        }

        public abstract bool IsReadOnly { get; }

        bool ICollection.IsSynchronized => ((ICollection)m_backing).IsSynchronized;
        object ICollection.SyncRoot => ((ICollection)m_backing).SyncRoot;

        bool IList.IsFixedSize => false;
        object IList.this[int index]
        {
            get => m_backing[index];
            set => this.DoReplace(index, (T)value);
        }

        // ===============================[ General Interface ]==================================
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
        void ICollection<T>.Add (T item) => this.DoInsert(this.Count, item);

        IEnumerator<T> IEnumerable<T>.GetEnumerator () => m_backing.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator () => m_backing.GetEnumerator();

        void ICollection.CopyTo (Array array, int index) => ((ICollection)m_backing).CopyTo(array, index);

        int IList.Add (object element) => this.DoInsert(-1, (T)element);

        bool IList.Contains (object value) => ((IList)m_backing).Contains(value);
        int IList.IndexOf (object value) => ((IList)m_backing).IndexOf(value);
        void IList.Insert (int index, object element) => this.DoInsert(index, (T)element);
        void IList.Remove (object element) => this.DoRemove((T)element);

        #endregion

        #region List<T> forwarding
        
        //
        // Summary:
        //     Searches a range of elements in the sorted System.Collections.Generic.List`1
        //     for an element using the specified comparer and returns the zero-based index
        //     of the element.
        //
        // Parameters:
        //   index:
        //     The zero-based starting index of the range to search.
        //
        //   count:
        //     The length of the range to search.
        //
        //   item:
        //     The object to locate. The value can be null for reference types.
        //
        //   comparer:
        //     The System.Collections.Generic.IComparer`1 implementation to use when comparing
        //     elements, or null to use the default comparer System.Collections.Generic.Comparer`1.Default.
        //
        // Returns:
        //     The zero-based index of item in the sorted System.Collections.Generic.List`1,
        //     if item is found; otherwise, a negative number that is the bitwise complement
        //     of the index of the next element that is larger than item or, if there is no
        //     larger element, the bitwise complement of System.Collections.Generic.List`1.Count.
        //
        // Exceptions:
        //   T:System.ArgumentOutOfRangeException:
        //     index is less than 0. -or- count is less than 0.
        //
        //   T:System.ArgumentException:
        //     index and count do not denote a valid range in the System.Collections.Generic.List`1.
        //
        //   T:System.InvalidOperationException:
        //     comparer is null, and the default comparer System.Collections.Generic.Comparer`1.Default
        //     cannot find an implementation of the System.IComparable`1 generic interface or
        //     the System.IComparable interface for type T.
        public int BinarySearch (int index, int count, T item, IComparer<T>? comparer) => m_backing.BinarySearch(index, count, item, comparer);
        //
        // Summary:
        //     Searches the entire sorted System.Collections.Generic.List`1 for an element using
        //     the default comparer and returns the zero-based index of the element.
        //
        // Parameters:
        //   item:
        //     The object to locate. The value can be null for reference types.
        //
        // Returns:
        //     The zero-based index of item in the sorted System.Collections.Generic.List`1,
        //     if item is found; otherwise, a negative number that is the bitwise complement
        //     of the index of the next element that is larger than item or, if there is no
        //     larger element, the bitwise complement of System.Collections.Generic.List`1.Count.
        //
        // Exceptions:
        //   T:System.InvalidOperationException:
        //     The default comparer System.Collections.Generic.Comparer`1.Default cannot find
        //     an implementation of the System.IComparable`1 generic interface or the System.IComparable
        //     interface for type T.
        public int BinarySearch (T item) => m_backing.BinarySearch(item);
        //
        // Summary:
        //     Searches the entire sorted System.Collections.Generic.List`1 for an element using
        //     the specified comparer and returns the zero-based index of the element.
        //
        // Parameters:
        //   item:
        //     The object to locate. The value can be null for reference types.
        //
        //   comparer:
        //     The System.Collections.Generic.IComparer`1 implementation to use when comparing
        //     elements. -or- null to use the default comparer System.Collections.Generic.Comparer`1.Default.
        //
        // Returns:
        //     The zero-based index of item in the sorted System.Collections.Generic.List`1,
        //     if item is found; otherwise, a negative number that is the bitwise complement
        //     of the index of the next element that is larger than item or, if there is no
        //     larger element, the bitwise complement of System.Collections.Generic.List`1.Count.
        //
        // Exceptions:
        //   T:System.InvalidOperationException:
        //     comparer is null, and the default comparer System.Collections.Generic.Comparer`1.Default
        //     cannot find an implementation of the System.IComparable`1 generic interface or
        //     the System.IComparable interface for type T.
        public int BinarySearch (T item, IComparer<T>? comparer) => m_backing.BinarySearch(item, comparer);
        //
        // Summary:
        //     Copies the entire System.Collections.Generic.List`1 to a compatible one-dimensional
        //     array, starting at the specified index of the target array.
        //
        // Parameters:
        //   array:
        //     The one-dimensional System.Array that is the destination of the elements copied
        //     from System.Collections.Generic.List`1. The System.Array must have zero-based
        //     indexing.
        //
        //   arrayIndex:
        //     The zero-based index in array at which copying begins.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     array is null.
        //
        //   T:System.ArgumentOutOfRangeException:
        //     arrayIndex is less than 0.
        //
        //   T:System.ArgumentException:
        //     The number of elements in the source System.Collections.Generic.List`1 is greater
        //     than the available space from arrayIndex to the end of the destination array.
        public void CopyTo (T[] array, int arrayIndex) => m_backing.CopyTo(array, arrayIndex);
        //
        // Summary:
        //     Copies the entire System.Collections.Generic.List`1 to a compatible one-dimensional
        //     array, starting at the beginning of the target array.
        //
        // Parameters:
        //   array:
        //     The one-dimensional System.Array that is the destination of the elements copied
        //     from System.Collections.Generic.List`1. The System.Array must have zero-based
        //     indexing.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     array is null.
        //
        //   T:System.ArgumentException:
        //     The number of elements in the source System.Collections.Generic.List`1 is greater
        //     than the number of elements that the destination array can contain.
        public void CopyTo (T[] array) => m_backing.CopyTo(array);
        //
        // Summary:
        //     Copies a range of elements from the System.Collections.Generic.List`1 to a compatible
        //     one-dimensional array, starting at the specified index of the target array.
        //
        // Parameters:
        //   index:
        //     The zero-based index in the source System.Collections.Generic.List`1 at which
        //     copying begins.
        //
        //   array:
        //     The one-dimensional System.Array that is the destination of the elements copied
        //     from System.Collections.Generic.List`1. The System.Array must have zero-based
        //     indexing.
        //
        //   arrayIndex:
        //     The zero-based index in array at which copying begins.
        //
        //   count:
        //     The number of elements to copy.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     array is null.
        //
        //   T:System.ArgumentOutOfRangeException:
        //     index is less than 0. -or- arrayIndex is less than 0. -or- count is less than
        //     0.
        //
        //   T:System.ArgumentException:
        //     index is equal to or greater than the System.Collections.Generic.List`1.Count
        //     of the source System.Collections.Generic.List`1. -or- The number of elements
        //     from index to the end of the source System.Collections.Generic.List`1 is greater
        //     than the available space from arrayIndex to the end of the destination array.
        public void CopyTo (int index, T[] array, int arrayIndex, int count) => m_backing.CopyTo(index, array, arrayIndex, count);
        //
        // Summary:
        //     Determines whether the System.Collections.Generic.List`1 contains elements that
        //     match the conditions defined by the specified predicate.
        //
        // Parameters:
        //   match:
        //     The System.Predicate`1 delegate that defines the conditions of the elements to
        //     search for.
        //
        // Returns:
        //     true if the System.Collections.Generic.List`1 contains one or more elements that
        //     match the conditions defined by the specified predicate; otherwise, false.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     match is null.
        public bool Exists (Predicate<T> match) => m_backing.Exists(match);
        //
        // Summary:
        //     Searches for an element that matches the conditions defined by the specified
        //     predicate, and returns the first occurrence within the entire System.Collections.Generic.List`1.
        //
        // Parameters:
        //   match:
        //     The System.Predicate`1 delegate that defines the conditions of the element to
        //     search for.
        //
        // Returns:
        //     The first element that matches the conditions defined by the specified predicate,
        //     if found; otherwise, the default value for type T.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     match is null.
        public T? Find (Predicate<T> match) => m_backing.Find(match);
        //
        // Summary:
        //     Retrieves all the elements that match the conditions defined by the specified
        //     predicate.
        //
        // Parameters:
        //   match:
        //     The System.Predicate`1 delegate that defines the conditions of the elements to
        //     search for.
        //
        // Returns:
        //     A System.Collections.Generic.List`1 containing all the elements that match the
        //     conditions defined by the specified predicate, if found; otherwise, an empty
        //     System.Collections.Generic.List`1.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     match is null.
        public List<T> FindAll (Predicate<T> match) => m_backing.FindAll(match);
        //
        // Summary:
        //     Searches for an element that matches the conditions defined by the specified
        //     predicate, and returns the zero-based index of the first occurrence within the
        //     range of elements in the System.Collections.Generic.List`1 that starts at the
        //     specified index and contains the specified number of elements.
        //
        // Parameters:
        //   startIndex:
        //     The zero-based starting index of the search.
        //
        //   count:
        //     The number of elements in the section to search.
        //
        //   match:
        //     The System.Predicate`1 delegate that defines the conditions of the element to
        //     search for.
        //
        // Returns:
        //     The zero-based index of the first occurrence of an element that matches the conditions
        //     defined by match, if found; otherwise, -1.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     match is null.
        //
        //   T:System.ArgumentOutOfRangeException:
        //     startIndex is outside the range of valid indexes for the System.Collections.Generic.List`1.
        //     -or- count is less than 0. -or- startIndex and count do not specify a valid section
        //     in the System.Collections.Generic.List`1.
        public int FindIndex (int startIndex, int count, Predicate<T> match) => m_backing.FindIndex(startIndex, count, match);
        //
        // Summary:
        //     Searches for an element that matches the conditions defined by the specified
        //     predicate, and returns the zero-based index of the first occurrence within the
        //     range of elements in the System.Collections.Generic.List`1 that extends from
        //     the specified index to the last element.
        //
        // Parameters:
        //   startIndex:
        //     The zero-based starting index of the search.
        //
        //   match:
        //     The System.Predicate`1 delegate that defines the conditions of the element to
        //     search for.
        //
        // Returns:
        //     The zero-based index of the first occurrence of an element that matches the conditions
        //     defined by match, if found; otherwise, -1.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     match is null.
        //
        //   T:System.ArgumentOutOfRangeException:
        //     startIndex is outside the range of valid indexes for the System.Collections.Generic.List`1.
        public int FindIndex (int startIndex, Predicate<T> match) => m_backing.FindIndex(startIndex, match);
        //
        // Summary:
        //     Searches for an element that matches the conditions defined by the specified
        //     predicate, and returns the zero-based index of the first occurrence within the
        //     entire System.Collections.Generic.List`1.
        //
        // Parameters:
        //   match:
        //     The System.Predicate`1 delegate that defines the conditions of the element to
        //     search for.
        //
        // Returns:
        //     The zero-based index of the first occurrence of an element that matches the conditions
        //     defined by match, if found; otherwise, -1.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     match is null.
        public int FindIndex (Predicate<T> match) => m_backing.FindIndex(match);
        //
        // Summary:
        //     Searches for an element that matches the conditions defined by the specified
        //     predicate, and returns the last occurrence within the entire System.Collections.Generic.List`1.
        //
        // Parameters:
        //   match:
        //     The System.Predicate`1 delegate that defines the conditions of the element to
        //     search for.
        //
        // Returns:
        //     The last element that matches the conditions defined by the specified predicate,
        //     if found; otherwise, the default value for type T.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     match is null.
        public T? FindLast (Predicate<T> match) => m_backing.FindLast(match);
        //
        // Summary:
        //     Searches for an element that matches the conditions defined by the specified
        //     predicate, and returns the zero-based index of the last occurrence within the
        //     range of elements in the System.Collections.Generic.List`1 that contains the
        //     specified number of elements and ends at the specified index.
        //
        // Parameters:
        //   startIndex:
        //     The zero-based starting index of the backward search.
        //
        //   count:
        //     The number of elements in the section to search.
        //
        //   match:
        //     The System.Predicate`1 delegate that defines the conditions of the element to
        //     search for.
        //
        // Returns:
        //     The zero-based index of the last occurrence of an element that matches the conditions
        //     defined by match, if found; otherwise, -1.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     match is null.
        //
        //   T:System.ArgumentOutOfRangeException:
        //     startIndex is outside the range of valid indexes for the System.Collections.Generic.List`1.
        //     -or- count is less than 0. -or- startIndex and count do not specify a valid section
        //     in the System.Collections.Generic.List`1.
        public int FindLastIndex (int startIndex, int count, Predicate<T> match) => m_backing.FindLastIndex(startIndex, count, match);
        //
        // Summary:
        //     Searches for an element that matches the conditions defined by the specified
        //     predicate, and returns the zero-based index of the last occurrence within the
        //     range of elements in the System.Collections.Generic.List`1 that extends from
        //     the first element to the specified index.
        //
        // Parameters:
        //   startIndex:
        //     The zero-based starting index of the backward search.
        //
        //   match:
        //     The System.Predicate`1 delegate that defines the conditions of the element to
        //     search for.
        //
        // Returns:
        //     The zero-based index of the last occurrence of an element that matches the conditions
        //     defined by match, if found; otherwise, -1.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     match is null.
        //
        //   T:System.ArgumentOutOfRangeException:
        //     startIndex is outside the range of valid indexes for the System.Collections.Generic.List`1.
        public int FindLastIndex (int startIndex, Predicate<T> match) => m_backing.FindLastIndex(startIndex, match);
        //
        // Summary:
        //     Searches for an element that matches the conditions defined by the specified
        //     predicate, and returns the zero-based index of the last occurrence within the
        //     entire System.Collections.Generic.List`1.
        //
        // Parameters:
        //   match:
        //     The System.Predicate`1 delegate that defines the conditions of the element to
        //     search for.
        //
        // Returns:
        //     The zero-based index of the last occurrence of an element that matches the conditions
        //     defined by match, if found; otherwise, -1.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     match is null.
        public int FindLastIndex (Predicate<T> match) => m_backing.FindLastIndex(match);
        //
        // Summary:
        //     Performs the specified action on each element of the System.Collections.Generic.List`1.
        //
        // Parameters:
        //   action:
        //     The System.Action`1 delegate to perform on each element of the System.Collections.Generic.List`1.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     action is null.
        //
        //   T:System.InvalidOperationException:
        //     An element in the collection has been modified.
        public void ForEach (Action<T> action) => m_backing.ForEach(action);
        //
        // Summary:
        //     Returns an enumerator that iterates through the System.Collections.Generic.List`1.
        //
        // Returns:
        //     A System.Collections.Generic.List`1.Enumerator for the System.Collections.Generic.List`1.
        public List<T>.Enumerator GetEnumerator () => m_backing.GetEnumerator();
        //
        // Summary:
        //     Creates a shallow copy of a range of elements in the source System.Collections.Generic.List`1.
        //
        // Parameters:
        //   index:
        //     The zero-based System.Collections.Generic.List`1 index at which the range starts.
        //
        //   count:
        //     The number of elements in the range.
        //
        // Returns:
        //     A shallow copy of a range of elements in the source System.Collections.Generic.List`1.
        //
        // Exceptions:
        //   T:System.ArgumentOutOfRangeException:
        //     index is less than 0. -or- count is less than 0.
        //
        //   T:System.ArgumentException:
        //     index and count do not denote a valid range of elements in the System.Collections.Generic.List`1.
        public List<T> GetRange (int index, int count) => m_backing.GetRange(index, count);
        //
        // Summary:
        //     Searches for the specified object and returns the zero-based index of the first
        //     occurrence within the range of elements in the System.Collections.Generic.List`1
        //     that starts at the specified index and contains the specified number of elements.
        //
        // Parameters:
        //   item:
        //     The object to locate in the System.Collections.Generic.List`1. The value can
        //     be null for reference types.
        //
        //   index:
        //     The zero-based starting index of the search. 0 (zero) is valid in an empty list.
        //
        //   count:
        //     The number of elements in the section to search.
        //
        // Returns:
        //     The zero-based index of the first occurrence of item within the range of elements
        //     in the System.Collections.Generic.List`1 that starts at index and contains count
        //     number of elements, if found; otherwise, -1.
        //
        // Exceptions:
        //   T:System.ArgumentOutOfRangeException:
        //     index is outside the range of valid indexes for the System.Collections.Generic.List`1.
        //     -or- count is less than 0. -or- index and count do not specify a valid section
        //     in the System.Collections.Generic.List`1.
        public int IndexOf (T item, int index, int count) => m_backing.IndexOf(item, index, count);
        //
        // Summary:
        //     Searches for the specified object and returns the zero-based index of the first
        //     occurrence within the range of elements in the System.Collections.Generic.List`1
        //     that extends from the specified index to the last element.
        //
        // Parameters:
        //   item:
        //     The object to locate in the System.Collections.Generic.List`1. The value can
        //     be null for reference types.
        //
        //   index:
        //     The zero-based starting index of the search. 0 (zero) is valid in an empty list.
        //
        // Returns:
        //     The zero-based index of the first occurrence of item within the range of elements
        //     in the System.Collections.Generic.List`1 that extends from index to the last
        //     element, if found; otherwise, -1.
        //
        // Exceptions:
        //   T:System.ArgumentOutOfRangeException:
        //     index is outside the range of valid indexes for the System.Collections.Generic.List`1.
        public int IndexOf (T item, int index) => m_backing.IndexOf(item, index);
        //
        // Summary:
        //     Inserts the elements of a collection into the System.Collections.Generic.List`1
        //     at the specified index.
        //
        // Parameters:
        //   index:
        //     The zero-based index at which the new elements should be inserted.
        //
        //   collection:
        //     The collection whose elements should be inserted into the System.Collections.Generic.List`1.
        //     The collection itself cannot be null, but it can contain elements that are null,
        //     if type T is a reference type.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     collection is null.
        //
        //   T:System.ArgumentOutOfRangeException:
        //     index is less than 0. -or- index is greater than System.Collections.Generic.List`1.Count.
        public void InsertRange (int index, IEnumerable<T> collection) => this.DoInsert(index, collection);
        //
        // Summary:
        //     Searches for the specified object and returns the zero-based index of the last
        //     occurrence within the entire System.Collections.Generic.List`1.
        //
        // Parameters:
        //   item:
        //     The object to locate in the System.Collections.Generic.List`1. The value can
        //     be null for reference types.
        //
        // Returns:
        //     The zero-based index of the last occurrence of item within the entire the System.Collections.Generic.List`1,
        //     if found; otherwise, -1.
        public int LastIndexOf (T item) => m_backing.LastIndexOf(item);
        //
        // Summary:
        //     Searches for the specified object and returns the zero-based index of the last
        //     occurrence within the range of elements in the System.Collections.Generic.List`1
        //     that extends from the first element to the specified index.
        //
        // Parameters:
        //   item:
        //     The object to locate in the System.Collections.Generic.List`1. The value can
        //     be null for reference types.
        //
        //   index:
        //     The zero-based starting index of the backward search.
        //
        // Returns:
        //     The zero-based index of the last occurrence of item within the range of elements
        //     in the System.Collections.Generic.List`1 that extends from the first element
        //     to index, if found; otherwise, -1.
        //
        // Exceptions:
        //   T:System.ArgumentOutOfRangeException:
        //     index is outside the range of valid indexes for the System.Collections.Generic.List`1.
        public int LastIndexOf (T item, int index) => m_backing.LastIndexOf(item, index);
        //
        // Summary:
        //     Searches for the specified object and returns the zero-based index of the last
        //     occurrence within the range of elements in the System.Collections.Generic.List`1
        //     that contains the specified number of elements and ends at the specified index.
        //
        // Parameters:
        //   item:
        //     The object to locate in the System.Collections.Generic.List`1. The value can
        //     be null for reference types.
        //
        //   index:
        //     The zero-based starting index of the backward search.
        //
        //   count:
        //     The number of elements in the section to search.
        //
        // Returns:
        //     The zero-based index of the last occurrence of item within the range of elements
        //     in the System.Collections.Generic.List`1 that contains count number of elements
        //     and ends at index, if found; otherwise, -1.
        //
        // Exceptions:
        //   T:System.ArgumentOutOfRangeException:
        //     index is outside the range of valid indexes for the System.Collections.Generic.List`1.
        //     -or- count is less than 0. -or- index and count do not specify a valid section
        //     in the System.Collections.Generic.List`1.
        public int LastIndexOf (T item, int index, int count) => m_backing.LastIndexOf(item, index, count);
        //
        // Summary:
        //     Removes a range of elements from the System.Collections.Generic.List`1.
        //
        // Parameters:
        //   index:
        //     The zero-based starting index of the range of elements to remove.
        //
        //   count:
        //     The number of elements to remove.
        //
        // Exceptions:
        //   T:System.ArgumentOutOfRangeException:
        //     index is less than 0. -or- count is less than 0.
        //
        //   T:System.ArgumentException:
        //     index and count do not denote a valid range of elements in the System.Collections.Generic.List`1.
        public void RemoveRange (int index, int count) => this.RemoveEach(m_backing.GetRange(index, count));
        //
        // Summary:
        //     Reverses the order of the elements in the specified range.
        //
        // Parameters:
        //   index:
        //     The zero-based starting index of the range to reverse.
        //
        //   count:
        //     The number of elements in the range to reverse.
        //
        // Exceptions:
        //   T:System.ArgumentOutOfRangeException:
        //     index is less than 0. -or- count is less than 0.
        //
        //   T:System.ArgumentException:
        //     index and count do not denote a valid range of elements in the System.Collections.Generic.List`1.
        public void Reverse (int index, int count) => this.DoRangeReorder(()=>m_backing.Reverse(index, count));
        //
        // Summary:
        //     Reverses the order of the elements in the entire System.Collections.Generic.List`1.
        public void Reverse () => this.DoRangeReorder(() => m_backing.Reverse());
        //
        // Summary:
        //     Sorts the elements in the entire System.Collections.Generic.List`1 using the
        //     specified System.Comparison`1.
        //
        // Parameters:
        //   comparison:
        //     The System.Comparison`1 to use when comparing elements.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     comparison is null.
        //
        //   T:System.ArgumentException:
        //     The implementation of comparison caused an error during the sort. For example,
        //     comparison might not return 0 when comparing an item with itself.
        public void Sort (Comparison<T> comparison) => Algorithms.Sort.QuickSortInplace(this, comparison, this.DoSwap);
        //
        // Summary:
        //     Sorts the elements in a range of elements in System.Collections.Generic.List`1
        //     using the specified comparer.
        //
        // Parameters:
        //   index:
        //     The zero-based starting index of the range to sort.
        //
        //   count:
        //     The length of the range to sort.
        //
        //   comparer:
        //     The System.Collections.Generic.IComparer`1 implementation to use when comparing
        //     elements, or null to use the default comparer System.Collections.Generic.Comparer`1.Default.
        //
        // Exceptions:
        //   T:System.ArgumentOutOfRangeException:
        //     index is less than 0. -or- count is less than 0.
        //
        //   T:System.ArgumentException:
        //     index and count do not specify a valid range in the System.Collections.Generic.List`1.
        //     -or- The implementation of comparer caused an error during the sort. For example,
        //     comparer might not return 0 when comparing an item with itself.
        //
        //   T:System.InvalidOperationException:
        //     comparer is null, and the default comparer System.Collections.Generic.Comparer`1.Default
        //     cannot find implementation of the System.IComparable`1 generic interface or the
        //     System.IComparable interface for type T.
        public void Sort (int index, int count, IComparer<T>? comparer) => Algorithms.Sort.QuickSortInplace(this, comparer.ToComparison(), this.DoSwap, index, index + count);
        //
        // Summary:
        //     Sorts the elements in the entire System.Collections.Generic.List`1 using the
        //     default comparer.
        //
        // Exceptions:
        //   T:System.InvalidOperationException:
        //     The default comparer System.Collections.Generic.Comparer`1.Default cannot find
        //     an implementation of the System.IComparable`1 generic interface or the System.IComparable
        //     interface for type T.
        public void Sort () => Algorithms.Sort.QuickSortInplace(this, this.DoSwap);
        //
        // Summary:
        //     Sorts the elements in the entire System.Collections.Generic.List`1 using the
        //     specified comparer.
        //
        // Parameters:
        //   comparer:
        //     The System.Collections.Generic.IComparer`1 implementation to use when comparing
        //     elements, or null to use the default comparer System.Collections.Generic.Comparer`1.Default.
        //
        // Exceptions:
        //   T:System.InvalidOperationException:
        //     comparer is null, and the default comparer System.Collections.Generic.Comparer`1.Default
        //     cannot find implementation of the System.IComparable`1 generic interface or the
        //     System.IComparable interface for type T.
        //
        //   T:System.ArgumentException:
        //     The implementation of comparer caused an error during the sort. For example,
        //     comparer might not return 0 when comparing an item with itself.
        public void Sort (IComparer<T>? comparer) => Algorithms.Sort.QuickSortInplace(this, comparer.ToComparison(), this.DoSwap);
        //
        // Summary:
        //     Copies the elements of the System.Collections.Generic.List`1 to a new array.
        //
        // Returns:
        //     An array containing copies of the elements of the System.Collections.Generic.List`1.
        public T[] ToArray () => m_backing.ToArray();
        //
        // Summary:
        //     Sets the capacity to the actual number of elements in the System.Collections.Generic.List`1,
        //     if that number is less than a threshold value.
        public void TrimExcess () => m_backing.TrimExcess();
        //
        // Summary:
        //     Determines whether every element in the System.Collections.Generic.List`1 matches
        //     the conditions defined by the specified predicate.
        //
        // Parameters:
        //   match:
        //     The System.Predicate`1 delegate that defines the conditions to check against
        //     the elements.
        //
        // Returns:
        //     true if every element in the System.Collections.Generic.List`1 matches the conditions
        //     defined by the specified predicate; otherwise, false. If the list has no elements,
        //     the return value is true.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     match is null.
        public bool TrueForAll (Predicate<T> match) => m_backing.TrueForAll(match);
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

        // ======================== [ Overridable Methods ] ==================================

        /// <summary>
        /// Insert an element at a given index, or -1 to simply add it
        /// </summary>
        /// <returns>The index inserted or -1 if not</returns>
        protected abstract int DoInsert (int index, IEnumerable<T> elements);
        protected abstract int DoRemoveEach (IEnumerable<T> elements);
        protected abstract void DoClear ();
        protected abstract void DoReplace (int index, T value);
        protected abstract void DoRangeReorder (Action actionThatWillOperateOnBacking, Comparison<T> specialtyComparer = null);
        protected abstract void DoSwap (int leftIndex, int rightIndex);
    }
}
