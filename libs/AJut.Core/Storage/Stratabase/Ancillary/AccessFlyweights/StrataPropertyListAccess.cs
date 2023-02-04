namespace AJut.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Reflection.Emit;
    using System.Xml.Linq;

    /// <summary>
    /// Represents a property access for a list. You could just store a list manually, but every override layer would essentially be a 100% override of the entire list.
    /// By using this access instead, you can store insertion overrides in any layer, and access the cached list here.
    /// </summary>
    /// <typeparam name="TElement">The type of element stored and inserted through out the <see cref="Stratabase"/></typeparam>
    public class StrataPropertyListAccess<TElement> : StrataPropertyAccessBase<ObservableCollection<TElement>>
    {
        private readonly ObservableCollection<TElement> m_currentListCache = new ObservableCollection<TElement>();

        // =========================[ Construction ]===================================
        internal StrataPropertyListAccess (Stratabase.ObjectDataAccessManager owner, string propertyName)
            : base(owner, propertyName)
        {
            this.ODAM.LayerListElementsChanged += this.OnLayerListElementsChanged;
            this.ResetElementsFromActive();
            this.Elements = new ReadOnlyObservableCollection<TElement>(m_currentListCache);
        }

        private void ResetElementsFromActive ()
        {
            m_currentListCache.Clear();
            if (this.IsSet)
            {
                if (this.IsBaselineSet)
                {
                    if (this.ODAM.TryGetBaselineValue(this.PropertyName, out List<TElement> listTracker))
                    {
                        m_currentListCache.AddEach(listTracker);
                    }
                }
                else if (this.ODAM.TryGetOverrideValue(this.ActiveLayerIndex, this.PropertyName, out List<TElement> listTracker))
                {
                    m_currentListCache.AddEach(listTracker);
                }
            }
        }

        private void OnLayerListElementsChanged (object sender, StratabaseListElementsChangedEventArgs e)
        {
            if (this.ActiveLayerIndex == kUnsetLayerIndex)
            {
                this.ActiveLayerIndex = e.WasBaseline ? kBaselineLayerIndex : e.Layer;
            }

            if (e.Layer > this.ActiveLayerIndex)
            {
                this.ActiveLayerIndex = e.Layer;
            }
            else if (e.Layer == this.ActiveLayerIndex)
            {
                if (e.WasElementAdded)
                {
                    m_currentListCache.Insert(e.ElementIndex, (TElement)e.Element);
                }
                else if (e.WasElementRemoved)
                {
                    m_currentListCache.RemoveAt(e.ElementIndex);
                }
            }

            this.TriggerValueChanged();
        }

        protected override void OnActiveLayerChanged (int formerActiveLayer)
        {
            base.OnActiveLayerChanged(formerActiveLayer);
            if (this.ActiveLayerIndex != kUnsetLayerIndex)
            {
                if (this.IsActiveLayerBaseline)
                {
                    if (this.ODAM.TryGetBaselineValue(this.PropertyName, out List<TElement> listTracker))
                    {
                        // TODO: It would be nice to take a more surgical approach
                        m_currentListCache.Clear();
                        m_currentListCache.AddEach(listTracker);
                    }
                }
                else if (this.ODAM.TryGetOverrideValue(this.ActiveLayerIndex, this.PropertyName, out List<TElement> listTracker))
                {
                    // TODO: It would be nice to take a more surgical approach
                    m_currentListCache.Clear();
                    m_currentListCache.AddEach(listTracker);
                }
            }
        }

        protected override void HandleAdditionalDispose ()
        {
            this.ODAM.LayerListElementsChanged -= this.OnLayerListElementsChanged;
        }

        /// <summary>
        /// This is kind of weird, but I wanted it to live here. Essentially Strabase may know the template type as a type during construction
        /// and need to create list insertion data, but since the classes are typed that is tricky. Using extension methods to call this indirectly
        /// allows us to more cleanly and in a more unified location, generate the list insertions.
        /// </summary>
        internal static object GenerateStorageForBaselineListAccess (object[] elements)
        {
            var data = new List<TElement>();
            for (int index = 0; index < elements.Length; ++index)
            {
                var target = (TElement)elements[index];
                data.Add(target);
            }

            return data;
        }

        // =========================[ Public Interface ]===================================

        public int FindElementIndex (Predicate<TElement> predicate, int startIndex = 0)
        {
            return this.Elements.IndexOf(predicate, startIndex);
        }

        public void ObliterateLayer (int layer)
        {
            this.ODAM.ObliteratePropertyStorageInLayer(layer, this.PropertyName);
        }

        public bool InsertElementIntoActiveLayer (int index, TElement newElement)
        {
            if (this.IsActiveLayerBaseline)
            {
                return this.ODAM.InsertElementIntoBaselineList(this.PropertyName, index, newElement);
            }

            return this.InsertElementIntoOverrideLayer(this.ActiveLayerIndex, index, newElement);
        }

        public bool InsertElementIntoBaseline (int index, TElement newElement)
        {
            return this.ODAM.InsertElementIntoBaselineList(this.PropertyName, index, newElement);
        }

        public bool InsertElementIntoOverrideLayer (int layer, int index, TElement newElement)
        {
            return this.ODAM.InsertElementIntoOverrideLayerList(layer, this.PropertyName, index, newElement);
        }

        public bool AddElementIntoBaseline (TElement newElement)
        {
            return this.ODAM.AddElementIntoBaselineList(this.PropertyName, newElement);
        }

        public bool AddElementIntoOverrideLayer (int layer, TElement newElement)
        {
            return this.ODAM.AddElementIntoOverrideList(layer, this.PropertyName, newElement);
        }

        public void RemoveAllElementsFrom (int layer)
        {
            this.ODAM.RemoveAllElementsInOverrideLayerList(layer, this.PropertyName);
        }

        public void RemoveAllElementsFromBaseline ()
        {
            this.ODAM.RemoveAllElementsInBaselineList(this.PropertyName);
        }

        public bool RemoveElementFromBaselineList (TElement element)
        {
            int index = m_currentListCache.IndexOf(element);
            if (index != -1)
            {
                return this.ODAM.RemoveElementFromBaselineList(this.PropertyName, index);
            }

            return false;
        }

        public bool RemoveElementFromOverrideLayerList (int layer, TElement element)
        {
            int index = m_currentListCache.IndexOf(element);
            if (index != -1)
            {
                return this.ODAM.RemoveElementFromOverrideLayerList(layer, this.PropertyName, index);
            }

            return false;
        }

        public void OverrideLayerWithListIn (int sourceLayer, int layerToOverride, int startIndex = 0, int endIndex = -1)
        {
            throw new NotImplementedException();
        }

        public bool ResetLayerByCopyingElementsToActive (int overrideLayerToCopyFrom)
        {
            return this.ResetLayerByCopyingElements(overrideLayerToCopyFrom, this.ActiveLayerIndex);
        }
        public bool ResetLayerByCopyingElements (int overrideLayerToCopyFrom, int overrideLayerToCopyTo)
        {
            if (!this.ODAM.TryGetOverrideValue(overrideLayerToCopyFrom, this.PropertyName, out List<TElement> copyFrom))
            {
                return false;
            }

            return this.ODAM.SetOverrideValue(overrideLayerToCopyTo, this.PropertyName, copyFrom);
        }


        public bool ResetLayerByCopyingElementsFromBaselineToActive ()
        {
            return this.ResetLayerByCopyingElementsFromBaseline(this.ActiveLayerIndex);
        }

        public bool ResetLayerByCopyingElementsFromBaseline (int overrideLayerToCopyTo)
        {
            if (!this.ODAM.TryGetBaselineValue(this.PropertyName, out List<TElement> copyFrom))
            {
                return false;
            }

            return this.ODAM.SetOverrideValue(overrideLayerToCopyTo, this.PropertyName, copyFrom);
        }

        public bool ResetBaselineByCopyingElementsFrom (int overrideLayerToCopyFrom)
        {
            if (!this.ODAM.TryGetOverrideValue(overrideLayerToCopyFrom, this.PropertyName, out List<TElement> copyFrom))
            {
                return false;
            }

            return this.ODAM.SetBaselineValue(this.PropertyName, copyFrom);
        }


        public int GetCount () => this.Elements.Count;
        public TElement GetElementAt (int elementIndex) => m_currentListCache[elementIndex];

        public ReadOnlyObservableCollection<TElement> Elements { get; }

        // =========================[ Base Override Impls ]===================================
        protected override void OnClearAllTriggered ()
        {
            base.OnClearAllTriggered();
            m_currentListCache.Clear();
        }
    }
}