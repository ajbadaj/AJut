namespace AJut.Application.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;

#if WINDOWS_UWP
    using Windows.UI;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;
#else
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Controls;
#endif

    using AJut.Application.Converters;

    using DPUtils = DPUtils<ToggleStrip>;
    using System.Windows.Data;
    using AJut.Storage;

    public class ToggleStrip : Control
    {
        public static readonly DependencyProperty ItemsSourceProperty = DPUtils.Register(_ => _.ItemsSource, (d, e) => d.OnItemsSourceChanged(e));

        public static readonly DependencyProperty AllowMultiSelectProperty = DPUtils.Register(_ => _.AllowMultiSelect, (d,e)=>d.OnAllowMultiSelectChanged(e.NewValue));

        public static readonly DependencyProperty SelectedItemProperty = DPUtils.Register(_ => _.SelectedItem, (d, e) => d.OnSelectedItemChanged(e));
        public static readonly DependencyProperty SelectedItemsProperty = DPUtils.Register(_ => _.SelectedItems, (d, e) => d.OnSelectedItemsChanged(e));

        public static readonly DependencyProperty DisplayPropertyPathProperty = DPUtils.Register(_ => _.DisplayPropertyPath, "", (d, e) => d.OnDisplayPropertyPathChanged(e.NewValue));
        public static readonly DependencyProperty ItemTemplateProperty = DPUtils.Register(_ => _.ItemTemplate);
        public static readonly DependencyProperty SeparatorBrushProperty = DPUtils.Register(_ => _.SeparatorBrush);
        public static readonly DependencyProperty SeparatorThicknessProperty = DPUtils.Register(_ => _.SeparatorThickness, new Thickness(0,0,1,0));
        public static readonly DependencyProperty ItemPaddingProperty = DPUtils.Register(_ => _.ItemPadding, new Thickness(6));
        public static readonly DependencyProperty AllowNoSelectionProperty = DPUtils.Register(_ => _.AllowNoSelection, false);


#if WINDOWS_UWP
        public static readonly DependencyProperty ItemsProperty = DPUtils.Register(_ => _.Items);
#else
        private static readonly DependencyPropertyKey ItemsPropertyKey = DPUtils.RegisterReadOnly(_ => _.Items);
        public static readonly DependencyProperty ItemsProperty = ItemsPropertyKey.DependencyProperty;
#endif

        public event EventHandler<ToggleStripSelectionChangedEventArgs> SelectionChanged;

        static ToggleStrip ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleStrip), new FrameworkPropertyMetadata(typeof(ToggleStrip)));
            BorderThicknessProperty.OverrideMetadata(typeof(ToggleStrip), new FrameworkPropertyMetadata(new Thickness(1)));
            BorderBrushProperty.OverrideMetadata(typeof(ToggleStrip), new FrameworkPropertyMetadata(CoerceUtils.CoerceBrushFrom("#202020")));
        }
        public ToggleStrip ()
        {
            // By default these are linked, feel free to set it to something else
            this.SetBinding(SeparatorBrushProperty, this.CreateBinding(BorderBrushProperty, BindingMode.OneWay));
            this.Items = new ToggleItemsCollection(this);
        }


        public IEnumerable ItemsSource
        {
            get => (IEnumerable)this.GetValue(ItemsSourceProperty);
            set => this.SetValue(ItemsSourceProperty, value);
        }
        public bool AllowMultiSelect
        {
            get => (bool)this.GetValue(AllowMultiSelectProperty);
            set => this.SetValue(AllowMultiSelectProperty, value);
        }

        public object SelectedItem
        {
            get { return (object)this.GetValue(SelectedItemProperty); }
            set { this.SetValue(SelectedItemProperty, value); }
        }

        public IList SelectedItems
        {
            get => (IList)this.GetValue(SelectedItemsProperty);
            set => this.SetValue(SelectedItemsProperty, value);
        }

        public string DisplayPropertyPath
        {
            get => (string)this.GetValue(DisplayPropertyPathProperty);
            set => this.SetValue(DisplayPropertyPathProperty, value);
        }
        public DataTemplate ItemTemplate
        {
            get => (DataTemplate)this.GetValue(ItemTemplateProperty);
            set => this.SetValue(ItemTemplateProperty, value);
        }
        public Brush SeparatorBrush
        {
            get => (Brush)this.GetValue(SeparatorBrushProperty);
            set => this.SetValue(SeparatorBrushProperty, value);
        }
        public Thickness SeparatorThickness
        {
            get => (Thickness)this.GetValue(SeparatorThicknessProperty);
            set => this.SetValue(SeparatorThicknessProperty, value);
        }

        public Thickness ItemPadding
        {
            get => (Thickness)this.GetValue(ItemPaddingProperty);
            set => this.SetValue(ItemPaddingProperty, value);
        }

        public bool AllowNoSelection
        {
            get => (bool)this.GetValue(AllowNoSelectionProperty);
            set => this.SetValue(AllowNoSelectionProperty, value);
        }

        public ToggleItemsCollection Items
        {
            get => (ToggleItemsCollection)this.GetValue(ItemsProperty);
            private set => this.SetValue(
#if WINDOWS_UWP
                ItemsProperty
#else
                ItemsPropertyKey
#endif
                , value);
        }

        private void OnAllowMultiSelectChanged (bool newValue)
        {

        }

        private void OnDisplayPropertyPathChanged (string newPath)
        {
            this.Items.ForEach(_ => _.ResetName(newPath));
        }

        private void OnItemsSourceChanged (DependencyPropertyChangedEventArgs<IEnumerable> e)
        {
            this.SelectedItem = null;
            if (this.SelectedItems is INotifyCollectionChanged)
            {
                this.SelectedItems.Clear(); ;
            }
            else
            {
                this.SelectedItems = new List<object>();
            }

            if (e.OldValue is INotifyCollectionChanged oldValue)
            {
                oldValue.CollectionChanged -= this.ItemsSource_CollectionChanged;
            }
            if (e.NewValue is INotifyCollectionChanged newValue)
            {
                newValue.CollectionChanged += this.ItemsSource_CollectionChanged;
            }

            if (e.NewValue == null)
            {
                this.Items.RemoveAllNotifyOnce();
            }
            else
            {
                int index = 0;
                this.Items.ReplaceAllWith(
                    e.NewValue.OfType<object>().Select(i => new ToggleItem(this.Items, i, index++, this.DisplayPropertyPath))
                );

                foreach (ToggleItem item in this.Items)
                {
                    if (this.SelectedItems?.Count == 0 && !this.AllowNoSelection)
                    {
                        item.IsSelected = true;
                    }
                }
            }
        }

        private void ItemsSource_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset && !e.NewItems.IsNullOrEmpty())
            {
                this.Items.RemoveAllNotifyOnce();
                return;
            }

            IEnumerable<ToggleItem> addedItems;
            if (e.NewItems != null)
            {
                addedItems = e.NewItems.OfType<object>()
                    .Select(i => new ToggleItem(this.Items, i, -1, this.DisplayPropertyPath));
            }
            else
            {
                addedItems = Enumerable.Empty<ToggleItem>();
            }

            IEnumerable<ToggleItem> removedItems;
            if (e.OldItems != null)
            {
                removedItems = this.Items.Where(_ => e.OldItems.Contains(_));
            }
            else
            {
                removedItems = Enumerable.Empty<ToggleItem>();
            }

            this.Items.AddAndRemove(addedItems, removedItems);
        }

        private bool m_isChangingSelectedItem;
        private bool m_isChangingSelectedItemsList;
        private void PerformModificationWithBlocker (ref bool blocker, Action action)
        {
            blocker = true;
            try
            {
                action();
            }
            finally
            {
                blocker = false;
            }
        }
        private void OnSelectedItemChanged (DependencyPropertyChangedEventArgs<object> e)
        {
            if (m_isChangingSelectedItem)
            {
                return;
            }

            if (e.NewValue == null)
            {
                this.PerformModificationWithBlocker(ref m_isChangingSelectedItemsList,
                    () =>
                    {
                        if (this.SelectedItems is INotifyCollectionChanged)
                        {
                            this.SelectedItems.Clear();
                        }
                        else
                        {
                            this.SelectedItems = null;
                        }
                    }
                );

                return;
            }

            ToggleItem newlySelected = this.Items.FirstOrDefault(_ => _.Data == e.NewValue);
            if (newlySelected == null)
            {
                return;
            }

            newlySelected.IsSelected = true;
        }
        private void OnSelectedItemsChanged (DependencyPropertyChangedEventArgs<IList> e)
        {
            if (m_isChangingSelectedItemsList)
            {
                return;
            }
        }

        private void HandleSelectionChanged (ToggleItem latest)
        {
            IList previousSelectedItems = this.SelectedItems;
            IList newlySelectedItems;

            if (this.AllowMultiSelect)
            {
                newlySelectedItems = this.Items.Where(_ => _.IsSelected).Select(_ => _.Data).ToList();
            }
            else
            {
                newlySelectedItems = new List<object> { latest.Data };
            }

            this.PerformModificationWithBlocker(
                ref m_isChangingSelectedItem,
                () => this.SelectedItem = latest.Data
            );

            this.PerformModificationWithBlocker(ref m_isChangingSelectedItemsList,
                () =>
                {
                    if (this.SelectedItems is INotifyCollectionChanged)
                    {
                        this.SelectedItems.Clear();
                        foreach (object obj in newlySelectedItems)
                        {
                            this.SelectedItems.Add(obj);
                        }
                    }
                    else
                    {
                        this.SelectedItems = newlySelectedItems;
                    }
                }
            );

            this.SelectionChanged?.Invoke(this, new ToggleStripSelectionChangedEventArgs(previousSelectedItems, newlySelectedItems));
        }

        public class ToggleItem : NotifyPropertyChanged
        {
            private string m_name;
            private bool m_isSelected;
            private int m_itemIndex;
            private bool m_hasDefaultName;

            private bool m_isFirstItem;
            private bool m_isLastItem;

            private ToggleItemsCollection m_owner;

            internal ToggleItem (ToggleItemsCollection owner, object item, int index, string displayPropertyPath)
            {
                this.Data = item;
                m_itemIndex = index;
                this.ResetName(displayPropertyPath);
                m_owner = owner;
            }

            public int ItemIndex
            {
                get => m_itemIndex;
                internal set
                {
                    if (this.SetAndRaiseIfChanged(ref m_itemIndex, value))
                    {
                        this.IsFirstItem = m_itemIndex == 0;
                        this.IsLastItem = m_owner.LastOrDefault() == this;
                        if (m_hasDefaultName)
                        {
                            this.ResetNameWithDefault();
                        }
                    }
                }
            }

            public string Name
            {
                get => m_name;
                set => this.SetAndRaiseIfChanged(ref m_name, value);
            }

            public bool IsSelected
            {
                get => m_isSelected;
                set // this should be treated as called by the user, a request
                {
                    if (!value && !m_owner.CanBeDeselected())
                    {
                        return;
                    }

                    if (this.SetAndRaiseIfChanged(ref m_isSelected, value))
                    {
                        if (value)
                        {
                            m_owner.HandleWasSelected(this);
                        }
                        else
                        {
                            m_owner.HandleWasDeselected(this);
                        }
                    }
                }
            }

            public object Data { get; }

            public bool IsFirstItem
            {
                get => m_isFirstItem;
                private set => this.SetAndRaiseIfChanged(ref m_isFirstItem, value);
            }

            public bool IsLastItem
            {
                get => m_isLastItem;
                private set => this.SetAndRaiseIfChanged(ref m_isLastItem, value);
            }

            internal void ResetName (string displayPropertyPath = null)
            {
                if (displayPropertyPath != null)
                {
                    string newName = this.Data.GetComplexPropertyValue(displayPropertyPath)?.ToString();
                    if (newName != null)
                    {
                        this.Name = newName;
                        m_hasDefaultName = false;
                        return;
                    }
                }

                this.ResetNameWithDefault();
            }

            internal void SetSelectionWithoutChecking (bool isSelected)
            {
                m_isSelected = isSelected;
                this.RaisePropertyChanged(nameof(IsSelected));
                m_owner.HandleWasDeselected(this);
            }

            private void ResetNameWithDefault ()
            {
                this.Name = $"Item {this.ItemIndex}";
                m_hasDefaultName = true;
            }
        }

        public class ToggleItemsCollection : ObservableCollectionX<ToggleItem>
        {
            ToggleStrip m_owner;

            internal ToggleItemsCollection (ToggleStrip owner)
            {
                m_owner = owner;
                this.CollectionChanged += this.Self_CollectionChanged;
            }

            internal bool CanBeDeselected ()
            {
                return m_owner.AllowNoSelection || m_owner.SelectedItems.Count > 1;
            }

            internal void HandleWasSelected (ToggleItem newlySelected)
            {
                if (!m_owner.AllowMultiSelect)
                {
                    foreach (ToggleItem item in this)
                    {
                        if (item != newlySelected)
                        {
                            item.SetSelectionWithoutChecking(false);
                        }
                    }
                }

                m_owner.HandleSelectionChanged(newlySelected);
            }

            internal void HandleWasDeselected (ToggleItem toggleItem)
            {
                if (m_owner.SelectedItems is INotifyCollectionChanged)
                {
                    m_owner.SelectedItems.Remove(toggleItem.Data);
                    if (m_owner.SelectedItem == toggleItem.Data)
                    {
                        m_owner.SelectedItem = m_owner.SelectedItems.OfType<object>().LastOrDefault();
                    }
                }
                else
                {
                    m_owner.SelectedItems = m_owner.SelectedItems.OfType<object>().Where(i => i != toggleItem.Data).ToList();
                }
            }

            private void Self_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
            {
                int index = 0;
                this.ForEach(_ => _.ItemIndex = index++);
            }
        }

        public class ToggleStripSelectionChangedEventArgs : EventArgs
        {
            public ToggleStripSelectionChangedEventArgs (IList previousSelection, IList currentSelection)
            {
                this.PreviousSelection = previousSelection;
                this.CurrentSelection = currentSelection;
            }

            public IList PreviousSelection { get; }
            public IList CurrentSelection { get; }
        }
    }

    public enum eToggleStripPlacement { First, Center, Last };
    public class ToggleStripCornerRadiusConverter : SimpleValueConverter<CornerRadius, CornerRadius>
    {
        public eToggleStripPlacement Placement { get; set; }
        public double Reduction { get; set; } = 0.0;

        protected override CornerRadius Convert (CornerRadius value)
        {
            switch (this.Placement)
            {
                case eToggleStripPlacement.First:
                    return new CornerRadius(value.TopLeft - this.Reduction, 0, 0, value.BottomLeft - this.Reduction);

                case eToggleStripPlacement.Last:
                    return new CornerRadius(0, value.TopRight - this.Reduction, value.BottomRight - this.Reduction, 0);

                default:
                    return new CornerRadius(0);
            }
        }
    }

    public class ToggleStripBorderThicknessConverter : SimpleValueConverter<Thickness, Thickness>
    {
        public eToggleStripPlacement Placement { get; set; }

        protected override Thickness Convert (Thickness value)
        {
            switch (this.Placement)
            {
                case eToggleStripPlacement.First:
                    return new Thickness(0, value.Top, value.Right, value.Bottom);

                case eToggleStripPlacement.Last:
                    return new Thickness(value.Left, value.Top, 0, value.Bottom);

                default:
                    return value;
            }
        }
    }
}
