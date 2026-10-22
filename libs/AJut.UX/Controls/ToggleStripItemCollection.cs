namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;

    /// <summary>
    /// The items of a toggle strip and the rules for selecting them, independent of any UI toolkit. A single-select
    /// strip deselects the other items when one is selected; the last selected item cannot be deselected unless the strip
    /// allows no selection; and every change reaches the strip as one <see cref="ToggleStripSelectionChange"/> through
    /// <see cref="IToggleStripHost.OnSelectionChanged"/>, so a single-select swap is reported once, not twice. The strip
    /// keeps the items in step with its items source through <see cref="ResetFromSource"/> and
    /// <see cref="ApplySourceChange"/>.
    /// </summary>
    /// <remarks>
    /// ResetFromSource, ApplySourceChange, SelectData and ResetNames are for the strip that owns the collection, and are
    /// hidden from a consumer's code completion: called from outside, they would put the items out of step with the
    /// strip's ItemsSource.
    /// </remarks>
    public class ToggleStripItemCollection<TItem> : ObservableCollection<TItem>, IToggleStripItemList
        where TItem : ToggleStripItem
    {
        private readonly Func<object, int, TItem> m_createItem;
        private IEnumerable? m_source;
        private object? m_selectedItem;

        // Set while a source change re-selects an item, so the change reports the selection from before the source
        // change rather than what was left after it.
        private IList? m_previousOverride;

        // ===========[ Setup/Construction/Teardown ]===================================
        /// <summary>Makes the collection for <paramref name="host"/>; <paramref name="createItem"/> makes an item from its data and index.</summary>
        public ToggleStripItemCollection (IToggleStripHost host, Func<object, int, TItem> createItem)
        {
            this.Host = host;
            m_createItem = createItem;
        }

        // ===========[ Properties ]===================================
        /// <summary>The strip these items belong to.</summary>
        public IToggleStripHost Host { get; }

        /// <summary>The data of the most recently selected item that is still selected, or null for none.</summary>
        public object? SelectedItem => m_selectedItem;

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// Rebuilds the items from <paramref name="source"/>, then selects the item whose data equals
        /// <paramref name="previousSelection"/> (by Equals, so boxed values match), or else the first item unless the
        /// strip allows no selection. Null clears the items.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void ResetFromSource (IEnumerable? source, object? previousSelection)
        {
            m_source = source;
            m_selectedItem = null;
            this.Clear();
            if (source == null)
            {
                return;
            }

            int index = 0;
            foreach (object data in source)
            {
                this.Add(m_createItem(data, index++));
            }

            if (previousSelection != null)
            {
                TItem? match = this.FirstOrDefault(item => Equals(item.Data, previousSelection));
                if (match != null)
                {
                    match.IsSelected = true;
                    return;
                }
            }

            if ((this.Count > 0) && !this.Any(item => item.IsSelected) && !this.Host.AllowNoSelection)
            {
                this[0].IsSelected = true;
            }
        }

        /// <summary>
        /// Applies an incremental change the items source raised, at the index the source reports, so the items stay in
        /// the source's order. A remove or replace with no index falls back to matching by data, an add with no index
        /// goes at the end, and a move with no index is ignored. A reset rebuilds the items from the source last given
        /// to <see cref="ResetFromSource"/>, restoring the selection by value.
        /// </summary>
        /// <remarks>
        /// When the change takes away a selected item, the strip hears of it as a selection change. A strip that needs a
        /// selection (single-select, no empty selection) selects its first item again if nothing selected is left.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void ApplySourceChange (NotifyCollectionChangedEventArgs e)
        {
            IList previous = this.CaptureSelection();
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    this.RebuildFromSource(previous);
                    break;

                case NotifyCollectionChangedAction.Add:
                    this.InsertFromSource(e.NewStartingIndex, e.NewItems);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    this.RemoveFromSource(e.OldStartingIndex, e.OldItems);
                    this.ReportSelectionTakenAway(previous);
                    break;

                case NotifyCollectionChangedAction.Replace:
                    this.RemoveFromSource(e.OldStartingIndex, e.OldItems);
                    this.InsertFromSource(e.NewStartingIndex, e.NewItems);
                    this.ReportSelectionTakenAway(previous);
                    break;

                case NotifyCollectionChangedAction.Move:
                    // Moving the existing items keeps their selection, which removing and re-adding would lose.
                    int count = e.OldItems?.Count ?? 0;
                    if ((e.OldStartingIndex < 0) || (e.NewStartingIndex < 0) || (count == 0))
                    {
                        break;
                    }

                    for (int offset = 0; offset < count; ++offset)
                    {
                        // A multi-item move keeps the block's order: each next item goes after the one moved before it.
                        int from = e.NewStartingIndex > e.OldStartingIndex ? e.OldStartingIndex : e.OldStartingIndex + offset;
                        int to = e.NewStartingIndex > e.OldStartingIndex ? e.NewStartingIndex + count - 1 : e.NewStartingIndex + offset;
                        this.Move(from, to);
                    }

                    break;
            }
        }

        /// <summary>
        /// Selects the item whose data equals <paramref name="data"/>, the way a click would, for when the strip's own
        /// selected item is set from outside. Returns false when no item has that data.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool SelectData (object? data)
        {
            TItem? match = this.FirstOrDefault(item => Equals(item.Data, data));
            if (match == null)
            {
                return false;
            }

            match.IsSelected = true;
            return true;
        }

        /// <summary>Names every item again, after the strip's DisplayPropertyPath or ItemNameSelector changed.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void ResetNames ()
        {
            foreach (TItem item in this)
            {
                item.ResetName();
            }
        }

        // ===========[ ObservableCollection Overrides ]===================================
        protected override void InsertItem (int index, TItem item)
        {
            item.AttachTo(this);
            base.InsertItem(index, item);
        }

        protected override void SetItem (int index, TItem item)
        {
            item.AttachTo(this);
            base.SetItem(index, item);
        }

        // Indexes and first/last flags are brought up to date before anyone listening hears of the change.
        protected override void OnCollectionChanged (NotifyCollectionChangedEventArgs e)
        {
            foreach (TItem item in this)
            {
                item.EvaluateOrder();
            }

            base.OnCollectionChanged(e);
        }

        // ===========[ IToggleStripItemList Implementations ]===================================
        // Counts the items actually selected, so an item the source removed no longer counts.
        bool IToggleStripItemList.CanBeDeselected ()
        {
            return this.Host.AllowNoSelection
                || (this.Count(item => item.IsSelected) > 1);
        }

        void IToggleStripItemList.HandleWasSelected (ToggleStripItem newlySelected)
        {
            // The previous selection is what was selected before this item, captured before a single-select swap
            // deselects anything (or, when a source change took the selection away, what was selected before that).
            IList previous = m_previousOverride ?? this.Where(item => item.IsSelected && (item != newlySelected)).Select(item => item.Data).ToList();
            if (!this.Host.AllowMultiSelect)
            {
                foreach (TItem item in this)
                {
                    if (item != newlySelected)
                    {
                        item.SetSelectionWithoutChecking(false);
                    }
                }
            }

            IList current = this.CaptureSelection();
            m_selectedItem = newlySelected.Data;
            this.Host.OnSelectionChanged(new ToggleStripSelectionChange(m_selectedItem, current, previous, newlySelected));
        }

        void IToggleStripItemList.HandleWasDeselected (ToggleStripItem deselected, bool isPartOfSelectionSwap)
        {
            // The deselect half of a single-select swap is reported with the select half, as one change.
            if (isPartOfSelectionSwap)
            {
                return;
            }

            IList previous = this.Where(item => item.IsSelected || (item == deselected)).Select(item => item.Data).ToList();
            IList current = this.CaptureSelection();

            // The selected item moves to the last one still selected when it is the one deselected.
            if (Equals(m_selectedItem, deselected.Data))
            {
                m_selectedItem = current.Count > 0 ? current[current.Count - 1] : null;
            }

            this.Host.OnSelectionChanged(new ToggleStripSelectionChange(m_selectedItem, current, previous, null));
        }

        int IToggleStripItemList.IndexOf (ToggleStripItem item) => item is TItem typed ? this.IndexOf(typed) : -1;

        bool IToggleStripItemList.IsLast (ToggleStripItem item) => (this.Count > 0) && ReferenceEquals(this[this.Count - 1], item);

        // ===========[ Helper Methods ]===================================
        private IList CaptureSelection () => this.Where(item => item.IsSelected).Select(item => item.Data).ToList();

        // A reset says the source changed in bulk, so the items are rebuilt from it. The change the strip hears reports
        // the selection from before the reset.
        private void RebuildFromSource (IList previous)
        {
            m_previousOverride = previous;
            try
            {
                this.ResetFromSource(m_source, m_selectedItem);
            }
            finally
            {
                m_previousOverride = null;
            }

            if ((previous.Count > 0) && !this.Any(item => item.IsSelected))
            {
                this.Host.OnSelectionChanged(new ToggleStripSelectionChange(null, new List<object>(), previous, null));
            }
        }

        // A source remove or replace that took away a selected item is a selection change. A strip that needs a selection
        // selects its first item again when none is left; otherwise the change reports what is left.
        private void ReportSelectionTakenAway (IList previous)
        {
            IList current = this.CaptureSelection();
            if (current.Count == previous.Count)
            {
                return;
            }

            if ((current.Count == 0) && (this.Count > 0) && !this.Host.AllowNoSelection)
            {
                m_previousOverride = previous;
                try
                {
                    this[0].IsSelected = true;
                }
                finally
                {
                    m_previousOverride = null;
                }

                return;
            }

            if ((m_selectedItem != null) && !current.Contains(m_selectedItem))
            {
                m_selectedItem = current.Count > 0 ? current[current.Count - 1] : null;
            }

            this.Host.OnSelectionChanged(new ToggleStripSelectionChange(m_selectedItem, current, previous, null));
        }

        private void InsertFromSource (int index, IList? newItems)
        {
            if (newItems == null)
            {
                return;
            }

            int insertAt = ((index >= 0) && (index <= this.Count)) ? index : this.Count;
            foreach (object data in newItems)
            {
                this.Insert(insertAt, m_createItem(data, insertAt));
                ++insertAt;
            }
        }

        private void RemoveFromSource (int index, IList? oldItems)
        {
            if (oldItems == null)
            {
                return;
            }

            // At the reported index when it lines up with the data there, which keeps duplicates straight; by data otherwise.
            if ((index >= 0) && (index + oldItems.Count <= this.Count)
                && Enumerable.Range(0, oldItems.Count).All(offset => Equals(this[index + offset].Data, oldItems[offset])))
            {
                for (int offset = 0; offset < oldItems.Count; ++offset)
                {
                    this.RemoveAt(index);
                }

                return;
            }

            foreach (object data in oldItems)
            {
                TItem? match = this.FirstOrDefault(item => Equals(item.Data, data));
                if (match != null)
                {
                    this.Remove(match);
                }
            }
        }
    }

    /// <summary>A <see cref="ToggleStripItemCollection{TItem}"/> of plain <see cref="ToggleStripItem"/>s, for a strip with no item type of its own.</summary>
    public class ToggleStripItemCollection : ToggleStripItemCollection<ToggleStripItem>
    {
        public ToggleStripItemCollection (IToggleStripHost host)
            : base(host, (data, index) => new ToggleStripItem(host, data, index))
        {
        }
    }
}
