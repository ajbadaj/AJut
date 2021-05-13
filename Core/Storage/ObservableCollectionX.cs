namespace AJut.Storage
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;

    // ================================================================================================================
    // ===[ AJut note: Expect Major Rework ]===
    // ================================================================================================================
    //  This is now used somewhere! In Stratabase for lists. The idea is sound, but it's too close to convincing UI
    //  people they should use it for UI situations. So instead here is what I will do, I will scrap this and build
    //  an ObservableList<T> which will have better "CollectionChangedEventArgs" args typed and everything.
    //
    //  In addition, consider also supporting implicit INotifyCollectionChanged, as well as indexer & count changes.
    //  That will allow UI poeple to use it as a swap out - but if they will have to cast it to use that event instead
    //  of the useful group action one I will provide
    // ================================================================================================================

    /// <summary>
    /// A more concise notification builder for ObservableCollection - unforutnately some WPF elements break with that (literally throw exceptions if they receieve more 
    /// than one action per event, even though it looks like it was setup for that).
    /// </summary>
    public class ObservableCollectionX<T> : ObservableCollection<T>
    {
        protected static readonly PropertyChangedEventArgs CountChanged = new PropertyChangedEventArgs(nameof(Count));
        protected static readonly PropertyChangedEventArgs IndexerChanged = new PropertyChangedEventArgs("Item[]");

        public void AddRange(IEnumerable<T> range)
        {
            List<T> list = range.ToList();
            this.Items.AddEach(range);

            this.OnPropertyChanged(CountChanged);
            this.OnPropertyChanged(IndexerChanged);

            if (list.Count == 1)
            {
                this.OnCollectionChanged(
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list[0], this.Items.Count - 1)
                );
            }
            else
            {
                this.OnCollectionChanged(
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list, this.Items.Count - 1)
                );
            }
        }

        public void RemoveRange(IEnumerable<T> range)
        {
            List<T> list = range.ToList();
            this.Items.RemoveEach(list);

            this.OnPropertyChanged(CountChanged);
            this.OnPropertyChanged(IndexerChanged);

            this.OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, list)
            );
        }

        /// <summary>
        /// Add some and remove some. This is useful because it will only have one NotifyCollectionChanged call
        /// </summary>
        public void AddAndRemove(IEnumerable<T> toAdd, IEnumerable<T> toRemove)
        {
            List<T> addList = toAdd.ToList();
            List<T> removeList = toRemove.ToList();
            this.Items.AddEach(addList);
            this.Items.RemoveEach(removeList);

            this.OnPropertyChanged(CountChanged);
            this.OnPropertyChanged(IndexerChanged);

            this.OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, addList, removeList)
            );
        }

        public void RemoveAllNotifyOnce()
        {
            this.Items.Clear();

            this.OnPropertyChanged(CountChanged);
            this.OnPropertyChanged(IndexerChanged);

            this.OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)
            );
        }

        public void ReplaceAllWith(IEnumerable<T> range)
        {
            List<T> newList = range.ToList();
            List<T> oldList = this.Items.ToList();
            this.Items.Clear();
            this.Items.AddEach(range);

            this.OnPropertyChanged(CountChanged);
            this.OnPropertyChanged(IndexerChanged);

            this.OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newList, oldList)
            );
        }
    }
}
