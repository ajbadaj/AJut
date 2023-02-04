namespace AJut.Storage
{
    using System;

    /// <summary>
    /// The base class of utilities for an object that caches and simplifies access to an object's property
    /// </summary>
    public abstract class StrataPropertyAccessBase<T> : IStrataPropertyAccess
    {
        protected const int kUnsetLayerIndex = -2;
        protected const int kBaselineLayerIndex = -1;

        private bool m_isBaselineSet;
        private bool m_isSet;
        private int m_activeLayerIndex = kUnsetLayerIndex;

        // Invalid base constructor
        internal protected StrataPropertyAccessBase () { }

        internal  StrataPropertyAccessBase (Stratabase.ObjectDataAccessManager odam, string propertyName)
        {
            this.ODAM = odam;
            this.PropertyName = propertyName;

            this.ODAM.LayerDataSet += this.OnLayerDataSet;
            this.ODAM.LayerDataRemoved += this.OnLayerDataRemoved;

            if (this.IsSet = this.ODAM.TryFindActiveLayer(this.PropertyName, out int activeLayer))
            {
                this.ActiveLayerIndex = activeLayer;
                this.IsBaselineSet = this.ODAM.TryGetBaselineValue<object>(this.PropertyName, out _);
            }
        }

        public void Dispose ()
        {
            if (this.ODAM == null)
            {
                return;
            }

            this.HandleAdditionalDispose();
            this.ODAM.LayerDataSet -= this.OnLayerDataSet;
            this.ODAM.LayerDataRemoved -= this.OnLayerDataRemoved;
            this.ODAM.HandleAccessWithdrawn();
            this.ODAM = null;
        }

        protected virtual void HandleAdditionalDispose() { }

        // ===============================[ Events ]========================================
        public event EventHandler<EventArgs> IsBaselineSetChanged;
        public event EventHandler<EventArgs> IsSetChanged;
        public event EventHandler<EventArgs> ValueChanged;

        // ===============================[ Properties ]====================================
        public string PropertyName { get; }

        public bool IsBaselineSet
        {
            get => m_isBaselineSet;
            internal set
            {
                if (m_isBaselineSet != value)
                {
                    m_isBaselineSet = value;
                    this.IsBaselineSetChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool IsSet
        {
            get => m_isSet;
            internal set
            {
                if (m_isSet != value)
                {
                    m_isSet = value;
                    this.IsSetChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public int ActiveLayerIndex
        {
            get => m_activeLayerIndex;
            protected set
            {
                int former = m_activeLayerIndex;
                if (m_activeLayerIndex != value)
                {
                    m_activeLayerIndex = value;
                    this.OnActiveLayerChanged(former);
                }
            }
        }

        protected virtual void OnActiveLayerChanged (int formerActiveLayer) { }

        public bool IsActiveLayerBaseline => this.ActiveLayerIndex == kBaselineLayerIndex;

        internal Stratabase.ObjectDataAccessManager ODAM { get; private set; }
        
        // ===============================[ Special Access Methods ]======================================
        public bool TryGetBaselineValue (out T value) => this.ODAM.TryGetBaselineValue(this.PropertyName, out value);
        public bool TryGetOverrideValue (int layerIndex, out T value) => this.ODAM.TryGetOverrideValue(layerIndex, this.PropertyName, out value);
        public bool SearchForFirstSetValue (int layerStartIndex, out T value) => this.ODAM.SearchForFirstSetValue(layerStartIndex, this.PropertyName, out value);

        // ===============================[ Overridable Interface ]=======================================
        protected virtual void OnBaselineLayerChanged (T oldValue, T newValue) { }
        protected virtual void OnOverrideLayerChanged (int layerIndex, T oldValue, T newValue) { }
        protected virtual void OnClearAllTriggered () { }

        // ===============================[ Utility Methods ]=======================================

        protected void TriggerValueChanged ()
        {
            this.ValueChanged?.Invoke(this, EventArgs.Empty);
        }


        private void OnLayerDataSet (object sender, StratabasePropertyChangeEventArgs e)
        {
            if (this.PropertyName != e.PropertyName)
            {
                return;
            }

            this.IsSet = true;
            if (e.IsBaseline)
            {
                this.IsBaselineSet = true;

                // If it's already the baseline layer, then we just need to trigger the value has changed
                if (this.ActiveLayerIndex == kBaselineLayerIndex)
                {
                    this.TriggerValueChanged();
                }
                // If it's unset, then we need to move to the baseline layer and trigger value changed
                else if (this.ActiveLayerIndex == kUnsetLayerIndex)
                {
                    this.ActiveLayerIndex = kBaselineLayerIndex;
                    this.TriggerValueChanged();
                }

                this.OnBaselineLayerChanged(
                        e.OldValue is T oldValue ? oldValue : default,
                        e.NewValue is T newValue ? newValue : default
                );
            }
            else
            {
                if (e.LayerIndex >= this.ActiveLayerIndex)
                {
                    this.ActiveLayerIndex = e.LayerIndex;
                    this.TriggerValueChanged();
                }

                this.OnOverrideLayerChanged(
                    e.LayerIndex,
                    e.OldValue is T oldValue ? oldValue : default,
                    e.NewValue is T newValue ? newValue : default
                );
            }
        }

        private void OnLayerDataRemoved (object sender, StratabasePropertyChangeEventArgs e)
        {
            if (e.PropertyName == String.Empty)
            {
                this.IsBaselineSet = false;
                this.IsSet = false;
                this.TriggerValueChanged();
                this.OnClearAllTriggered();
                return;
            }

            if (this.PropertyName != e.PropertyName)
            {
                return;
            }

            if (e.IsBaseline)
            {
                this.IsBaselineSet = false;
                _UpdateIsSetAndTopMost();

                this.OnBaselineLayerChanged(
                    e.OldValue is T oldValue ? oldValue : default,
                    e.NewValue is T newValue ? newValue : default
                );
            }
            else
            {
                if (e.LayerIndex == this.ActiveLayerIndex)
                {
                    _UpdateIsSetAndTopMost();
                }

                this.OnOverrideLayerChanged(
                    e.LayerIndex,
                    e.OldValue is T oldValue ? oldValue : default,
                    e.NewValue is T newValue ? newValue : default
                );
            }

            void _UpdateIsSetAndTopMost ()
            {
                bool isSet = this.IsSet;
                if (this.IsSet = this.ODAM.TryFindActiveLayer(this.PropertyName, out int activeLayer))
                {
                    this.ActiveLayerIndex = activeLayer;
                }

                if (this.IsSet != isSet)
                {
                    this.TriggerValueChanged();
                }
            }
        }
    }
}
