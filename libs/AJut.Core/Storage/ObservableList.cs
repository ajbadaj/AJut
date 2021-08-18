namespace AJut.Storage
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using AJut.Storage.ListOperations;

    /// <summary>
    /// A list whose actions are notified out. While notification events are consolodated - UI elements require the individualistic INotifyCollectionChanged
    /// style of operation, which this class supports as well - simply subscribe to the notification style you like and you will get the results you want
    /// </summary>
    public class ObservableList<T> : ObservableListBase<T>
    {
        // #error Should almost definitely implement ALL of the missing List<T> pieces here
        // #error need to add AddMany/RemoveMany etc for collapsing

        // ===============================[ Construction ]==================================
        public ObservableList () : base() { }
        public ObservableList (IEnumerable<T> elements) : base (elements) { }
        public ObservableList (int capacity) : base (capacity) { }

        // ===============================[ Properties ]==================================

        /// <summary>
        /// Indicates if the list is readonly
        /// </summary>
        public override bool IsReadOnly => false;

        // ===============================[ General Interface ]==================================
        public ReadOnlyObservableList<T> ToReadOnly () => new ReadOnlyObservableList<T>(this);
        // Summary:
        //     Converts the elements in the current System.Collections.Generic.List`1 to another
        //     type, and returns a list containing the converted elements.
        //
        // Parameters:
        //   converter:
        //     A System.Converter`2 delegate that converts each element from one type to another
        //     type.
        //
        // Type parameters:
        //   TOutput:
        //     The type of the elements of the target array.
        //
        // Returns:
        //     A System.Collections.Generic.List`1 of the target type containing the converted
        //     elements from the current System.Collections.Generic.List`1.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     converter is null.
        public ObservableList<TOutput> ConvertAll<TOutput> (Converter<T, TOutput> converter) => new ObservableList<TOutput>(this.BackingList.ConvertAll(converter));

        // ======================== [ Overridable Methods ] ==================================

        protected override int DoInsert (int index, IEnumerable<T> elements)
        {
            // If this is just some random enumeration, put that in an array or something
            //  because we're about to go over it a few times.
            if (!(elements is IList))
            {
                elements = elements.ToArray();
            }

            if (index == -1 || index >= this.BackingList.Count)
            {
                index = this.BackingList.Count;
                this.BackingList.AddRange(elements);
            }
            else
            {
                this.BackingList.InsertRange(index, elements);
            }

            this.RaiseAllStandardEvents(new NotifyListCompositionChangedEventArgs<T>(new ListElementInsertOperation<T>(index, elements)));
            this.RaisePropertyChanged(kIndexerPropChange);
            this.RaisePropertyChanged(kCountPropChange);

            if (this.BackingList.Count == 1)
            {
                this.RaisePropertiesChanged(nameof(IsEmpty), nameof(HasElements));
            }

            return index;
        }

        protected override int DoRemoveEach (IEnumerable<T> elements)
        {
            List<T> actuallyRemoved = new List<T>();
            foreach (T element in elements.ToList())
            {
                if (this.BackingList.Remove(element))
                {
                    actuallyRemoved.Add(element);
                }
            }

            if (actuallyRemoved.IsEmpty())
            {
                return 0;
            }

            this.RaiseAllStandardEvents(new NotifyListCompositionChangedEventArgs<T>(new ListElementRemoveOperation<T>(actuallyRemoved.ToArray())));
            this.RaisePropertyChanged(kIndexerPropChange);
            this.RaisePropertyChanged(kCountPropChange);

            if (this.BackingList.Count == 0)
            {
                this.RaisePropertiesChanged(nameof(IsEmpty), nameof(HasElements));
            }

            return actuallyRemoved.Count;
        }

        protected override void DoClear ()
        {
            T[] actuallyRemoved = this.BackingList.ToArray();
            if (actuallyRemoved.IsEmpty())
            {
                return;
            }

            this.BackingList.Clear();

            var listChangedArgs = new NotifyListCompositionChangedEventArgs<T>(new ListElementRemoveOperation<T>(actuallyRemoved.ToArray()));
            this.RaiseListCompositionChangedEvents(listChangedArgs);
            this.RaiseCollectionChangedEventArgsFor(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            this.RaisePropertyChanged(kIndexerPropChange);
            this.RaisePropertyChanged(kCountPropChange);
            this.RaisePropertiesChanged(nameof(IsEmpty), nameof(HasElements));
        }

        protected override void DoReplace (int index, T value)
        {
            T oldValue = this.BackingList[index];
            this.BackingList[index] = value;

            var eventArgs = new NotifyListCompositionChangedEventArgs<T>(
                new ListElementRemoveOperation<T>(oldValue),
                new ListElementInsertOperation<T>(index, value)
            );

            this.RaiseAllStandardEvents(eventArgs);
        }
        
        protected override void DoRangeReorder (Action actionThatWillOperateOnBacking, Comparison<T> specialtyComparer = null)
        {
            Comparison<T> comparer = specialtyComparer ?? ((left, right) => Comparer<T>.Default.Compare(left, right));
            T[] originalSet = this.BackingList.ToArray();
            actionThatWillOperateOnBacking();
            if (originalSet.Length != this.BackingList.Count)
            {
                throw new InvalidOperationException("Error in range reorder for Observable List (base)");
            }

            List<ListElementMoveOperation<T>> moves = new List<ListElementMoveOperation<T>>();
            HashSet<int> alreadyUsed = new HashSet<int>();
            for (int originalSetIndex = 0; originalSetIndex < originalSet.Length; ++originalSetIndex)
            {
                if (comparer(originalSet[originalSetIndex], this.BackingList[originalSetIndex]) != 0)
                {
                    int newIndex = this.BackingList.IndexOf(originalSet[originalSetIndex]);
                    while (newIndex != -1 && !alreadyUsed.Add(newIndex))
                    {
                        newIndex = this.BackingList.IndexOf(originalSet[originalSetIndex], newIndex + 1);
                    }

                    if (newIndex != -1)
                    {
                        moves.InsertSorted(new ListElementMoveOperation<T>(this.BackingList[newIndex], originalSetIndex, newIndex), _MoveInsertionSorter);
                    }

                    //int fromIndex = originalSet.IndexOf(this.BackingList[originalSetIndex]);
                    //while (fromIndex != -1 && !alreadyUsed.Add(fromIndex))
                    //{
                    //    fromIndex = originalSet.IndexOf(this.BackingList[originalSetIndex], fromIndex + 1);
                    //}

                    //if (fromIndex != -1)
                    //{
                    //    moves.InsertSorted(new ListElementMoveOperation<T>(this.BackingList[originalSetIndex], fromIndex, originalSetIndex), _MoveInsertionSorter);
                    //}
                }
                else
                {
                    alreadyUsed.Add(originalSetIndex);
                }
            }

            this.RaiseAllStandardEvents(new NotifyListCompositionChangedEventArgs<T>(moves));

            int _MoveInsertionSorter (ListElementMoveOperation<T> left, ListElementMoveOperation<T> right)
            {
                return left.OriginalLocationIndex.CompareTo(right.OriginalLocationIndex);
            }
        }

        protected override void DoSwap (int leftIndex, int rightIndex)
        {
            // Store
            T left = this.BackingList[leftIndex];
            T right = this.BackingList[rightIndex];

            // Swap
            this.BackingList[leftIndex] = right;
            this.BackingList[rightIndex] = left;

            // Notify, but indicate it as two move operations.
            var listChangedArgs = new NotifyListCompositionChangedEventArgs<T>(
                new ListElementMoveOperation<T>(right, rightIndex, leftIndex),
                new ListElementMoveOperation<T>(left, leftIndex + 1, rightIndex)
            );

            this.RaiseAllStandardEvents(listChangedArgs);
            this.RaisePropertyChanged(kIndexerPropChange);
        }
    }
}
