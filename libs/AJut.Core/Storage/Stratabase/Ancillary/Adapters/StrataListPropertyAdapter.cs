namespace AJut.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;

    /// <summary>
    /// Use this as a way to adapt lists of elements stored in the stratabase to lists of a converted element. You might store
    /// lists of strings in the stratabase and lists of colors in the adapter. Most commonly I expect this as a reference adapter
    /// where lists of <see cref="Guid"/>s are stored in the stratabase for "Sub objects" and the adapter would interpret this into
    /// some kind of strata property access based model.
    /// </summary>
    /// <typeparam name="TStrataElementValue">The type of value the elements are which are stored in the <see cref="Stratabase"/></typeparam>
    /// <typeparam name="TAdaptedElementValue">The type of value the elements are adapted to</typeparam>
    public class StrataListPropertyAdapter<TStrataElementValue, TAdaptedElementValue> : IDisposable
    {
        public delegate TAdaptedElementValue ConvertAccessToOutput (Stratabase sb, TStrataElementValue accessValue);

        private readonly Stratabase m_sb;
        private readonly ConvertAccessToOutput m_factory;
        private readonly ReadOnlyObservableCollection<TStrataElementValue> m_referenceElements;
        private readonly List<Tracker> m_trackers = new List<Tracker>();
        private readonly ObservableCollection<TAdaptedElementValue> m_elements = new ObservableCollection<TAdaptedElementValue>();

        public StrataListPropertyAdapter (Stratabase sb, Guid itemId, string propertyName, ConvertAccessToOutput factory)
            : this(sb.GenerateListPropertyAccess<TStrataElementValue>(itemId, propertyName), factory)
        {
        }

        public StrataListPropertyAdapter(StrataPropertyListAccess<TStrataElementValue> referenceAccess, ConvertAccessToOutput factory)
        {
            this.Access = referenceAccess;
            m_referenceElements = referenceAccess.Elements;
            ((INotifyCollectionChanged)m_referenceElements).CollectionChanged += this.ListAccessElementsCache_OnCollectionChanged;

            m_sb = referenceAccess.ODAM.SB;
            m_factory = factory;
            this.Elements = new ReadOnlyObservableCollection<TAdaptedElementValue>(m_elements);
            this.Reset();
        }
        
        public StrataPropertyListAccess<TStrataElementValue> Access { get; }
        public ReadOnlyObservableCollection<TAdaptedElementValue> Elements { get; }

        public void Dispose ()
        {
            ((INotifyCollectionChanged)m_referenceElements).CollectionChanged -= this.ListAccessElementsCache_OnCollectionChanged;
            foreach (var element in this.Elements.OfType<IDisposable>())
            {
                element.Dispose();
            }

            m_elements.Clear();

            this.Access.Dispose();
        }

        private void ListAccessElementsCache_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            // Just easier this way
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                m_trackers.Clear();
                m_elements.Clear();
            }

            this.Reset();
        }

        private void Reset()
        {
            // =======================================================================
            // Step 1: Fill up the cache
            // =======================================================================
            Dictionary<TStrataElementValue, int> expectedIndicies = new Dictionary<TStrataElementValue, int>(m_referenceElements.Count);
            Dictionary<TStrataElementValue, int> toCreate = new Dictionary<TStrataElementValue, int>(m_referenceElements.Count);
            for (int index = 0; index < m_referenceElements.Count; ++index)
            {
                expectedIndicies[m_referenceElements[index]] = index;
                toCreate[m_referenceElements[index]] = index;
            }

            // =======================================================================
            // Step 2: Remove items not found in new, find new items not yet tracked
            // =======================================================================

            for (int index = m_trackers.Count - 1; index >= 0; --index)
            {
                var tracker = m_trackers[index];
                if (!toCreate.Remove(tracker.Key))
                {
                    m_elements.RemoveAt(index);
                    m_trackers.RemoveAt(index);
                }
            }

            // =======================================================================
            // Step 3: We now know what elements (if any) need to be created, and we
            //    have removed all elements that were removed from our source. Next 
            //    step is to get existing elements in the right relative order.
            // =======================================================================
            List<Tracker> sortedDuplicates = new List<Tracker>(m_trackers);
            sortedDuplicates.Sort((t1, t2) => expectedIndicies[t1.Key].CompareTo(expectedIndicies[t2.Key]));
            for (int index = sortedDuplicates.Count - 1; index >= 0; --index)
            {
                var currTracker = m_trackers[index];
                if (m_trackers[index] == sortedDuplicates[index])
                {
                    continue;
                }

                int moveToIndex = sortedDuplicates.IndexOf(currTracker);
                m_trackers.Swap(index, moveToIndex);
                m_elements.Swap(index, moveToIndex);
            }

            // =======================================================================
            // Step 4: Create missing items & insert them in the proper order
            // =======================================================================
            foreach (KeyValuePair<TStrataElementValue, int> item in toCreate)
            {
                Tracker t = new Tracker(item.Key, m_factory(m_sb, item.Key));
                m_trackers.Insert(item.Value, t);
                m_elements.Insert(item.Value, t.Element);
            }
        }

        private class Tracker
        {
            public Tracker(TStrataElementValue id, TAdaptedElementValue element)
            {
                this.Key = id;
                this.Element = element;
            }

            public TStrataElementValue Key { get; }
            public TAdaptedElementValue Element { get; }
        }
    }
}
