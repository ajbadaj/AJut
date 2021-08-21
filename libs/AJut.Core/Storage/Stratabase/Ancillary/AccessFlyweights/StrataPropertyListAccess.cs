namespace AJut.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;

    /// <summary>
    /// Represents a property access for a list. You could just store a list manually, but every override layer would essentially be a 100% override of the entire list.
    /// By using this access instead, you can store insertion overrides in any layer, and access the cached list here.
    /// </summary>
    /// <typeparam name="TElement">The type of element stored and inserted through out the <see cref="Stratabase"/></typeparam>
    public class StrataPropertyListAccess<TElement> : StrataPropertyAccessBase<ObservableCollection<StratabaseListInsertion<TElement>>>
    {
        private readonly List<StratabaseListInsertion<TElement>> m_insertionsCache = new List<StratabaseListInsertion<TElement>>();
        private readonly ObservableCollection<TElement> m_currentListCache = new ObservableCollection<TElement>();

        // =========================[ Construction ]===================================
        internal StrataPropertyListAccess (Stratabase.ObjectDataAccessManager owner, string propertyName)
            : base(owner, propertyName)
        {
            this.Elements = new ReadOnlyObservableCollection<TElement>(m_currentListCache);
            this.Reset();
        }

        protected override void HandleAdditionalDispose ()
        {
            if (this.GetBaselineChanges() is ObservableCollection<StratabaseListInsertion<TElement>> baselineCollection)
            {
                baselineCollection.CollectionChanged -= this.OnStorageCollectionChanged;
            }

            for (int layerIndex = 0; layerIndex < this.ODAM.SB.OverrideLayerCount; ++layerIndex)
            {
                if (this.GetOverrideChanges(layerIndex) is ObservableCollection<StratabaseListInsertion<TElement>> overrideCollection)
                {
                    overrideCollection.CollectionChanged -= this.OnStorageCollectionChanged;
                }
            }

            this.ClearListCache();
        }

        private void ClearListCache ()
        {
            foreach (var element in m_currentListCache.OfType<IDisposable>())
            {
                element.Dispose();
            }
            m_currentListCache.Clear();
        }

        /// <summary>
        /// This is kind of weird, but I wanted it to live here. Essentially Strabase may know the template type as a type during construction
        /// and need to create list insertion data, but since the classes are typed that is tricky. Using extension methods to call this indirectly
        /// allows us to more cleanly and in a more unified location, generate the list insertions.
        /// </summary>
        internal static object GenerateStorageForBaselineListAccess(object[] elements)
        {
            ObservableCollection<StratabaseListInsertion<TElement>> data = new ObservableCollection<StratabaseListInsertion<TElement>>();
            for (int index = 0; index < elements.Length; ++index)
            {
                var target = (TElement)elements[index];
                data.Add(new StratabaseListInsertion<TElement>(StratabaseListInsertion<TElement>.kBaseline, index, target));
            }

            return data;
        }

        // =========================[ Public Interface ]===================================

        public StratabaseListInsertion<TElement>.InsertionBuilder CreateInsert (int index, params TElement[] values)
        {
            return new StratabaseListInsertion<TElement>.InsertionBuilder(this, index, values);
        }

        public StratabaseListInsertion<TElement>.InsertionBuilder CreateAdd (params TElement[] values)
        {
            return new StratabaseListInsertion<TElement>.InsertionBuilder(this, m_currentListCache.Count, values);
        }

        public bool TryFindLayerIndexForElement (TElement element, out int layerIndex)
        {
            return this.TryFindLayerIndexForElementAt(this.Elements.IndexOf(element), out layerIndex);
        }

        public bool TryFindLayerIndexForElementAt (int elementIndex, out int layerIndex)
        {
            if (elementIndex >= 0 && elementIndex < m_insertionsCache.Count)
            {
                layerIndex = m_insertionsCache[elementIndex].SourceLayer;
                return layerIndex > -2;
            }

            layerIndex = -2;
            return false;
        }

        public bool Remove (TElement element)
        {
            int index = this.Elements.IndexOf(element);
            if (index != -1)
            {
                return this.RemoveAt(index);
            }

            return false;
        }

        public bool RemoveAt (int index)
        {
            var insertion = m_insertionsCache[index];
            if (insertion.IsBaseline)
            {
                return this.GetBaselineChanges(false)?.Remove(insertion) ?? false;
            }

            return this.GetOverrideChanges(insertion.SourceLayer, false)?.Remove(insertion) ?? false;
        }

        public int GetCount () => this.Elements.Count;
        public TElement GetElementAt (int elementIndex) => m_currentListCache[elementIndex];

        public ReadOnlyObservableCollection<TElement> Elements { get; }

        // =========================[ Base Override Impls ]===================================

        protected override void OnBaselineLayerChanged (ObservableCollection<StratabaseListInsertion<TElement>> oldValue, ObservableCollection<StratabaseListInsertion<TElement>> newValue)
        {
            if (oldValue != null)
            {
                oldValue.CollectionChanged -= this.OnStorageCollectionChanged;
            }

            this.TrackChanges(newValue);
        }

        protected override void OnOverrideLayerChanged (int layerIndex, ObservableCollection<StratabaseListInsertion<TElement>> oldValue, ObservableCollection<StratabaseListInsertion<TElement>> newValue)
        {
            if (oldValue != null)
            {
                oldValue.CollectionChanged -= this.OnStorageCollectionChanged;
            }

            this.TrackChanges(newValue);
        }

        protected override void OnClearAllTriggered ()
        {
            m_insertionsCache.Clear();
            this.ClearListCache();
        }

        // =========================[ Utility Methods ]===================================

        private void Reset ()
        {
            m_insertionsCache.Clear();
            this.ClearListCache();

            this.TrackChanges(this.GetBaselineChanges());
            for (int layerIndex = 0; layerIndex < this.ODAM.SB.OverrideLayerCount; ++layerIndex)
            {
                this.TrackChanges(this.GetOverrideChanges(layerIndex));
            }
        }

        private void TrackChanges (ObservableCollection<StratabaseListInsertion<TElement>> changes)
        {
            if (changes == null)
            {
                return;
            }

            // Sign up for collection changes
            changes.CollectionChanged -= this.OnStorageCollectionChanged;
            changes.CollectionChanged += this.OnStorageCollectionChanged;
            foreach (StratabaseListInsertion<TElement> item in changes)
            {
                this.Insert(item);
            }
        }

        private void Insert (StratabaseListInsertion<TElement> item)
        {
            int index = m_insertionsCache.InsertSorted(item, (l, r) => l.Index - r.Index);
            m_currentListCache.Insert(index, item.Value);
        }

        private void Remove (StratabaseListInsertion<TElement> item)
        {
            int index = m_insertionsCache.IndexOf(item);
            m_insertionsCache.RemoveAt(index);
            m_currentListCache.RemoveAt(index);
        }

        private void OnStorageCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (StratabaseListInsertion<TElement> element in e.NewItems)
                {
                    this.Insert(element);
                }
            }
            if (e.OldItems != null)
            {
                foreach (StratabaseListInsertion<TElement> element in e.OldItems)
                {
                    this.Remove(element);
                }
            }
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (StratabaseListInsertion<TElement> element in m_insertionsCache)
                {
                    this.Remove(element);

                }
            }
        }

        internal ObservableCollection<StratabaseListInsertion<TElement>> GetBaselineChanges (bool generateIfNonExistant = false)
        {
            if (!this.ODAM.TryGetBaselineValue(this.PropertyName, out ObservableCollection<StratabaseListInsertion<TElement>> changes) && generateIfNonExistant)
            {
                changes = new ObservableCollection<StratabaseListInsertion<TElement>>();
                this.ODAM.SetBaselineValue(this.PropertyName, changes);
            }

            return changes;
        }

        internal ObservableCollection<StratabaseListInsertion<TElement>> GetOverrideChanges (int layerIndex, bool generateIfNonExistant = false)
        {
            if (!this.ODAM.TryGetOverrideValue(layerIndex, this.PropertyName, out ObservableCollection<StratabaseListInsertion<TElement>> changes) && generateIfNonExistant)
            {
                changes = new ObservableCollection<StratabaseListInsertion<TElement>>();
                this.ODAM.SetOverrideValue(layerIndex, this.PropertyName, changes);
            }

            return changes;
        }
    }
}