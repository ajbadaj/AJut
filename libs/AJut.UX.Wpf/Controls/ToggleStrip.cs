namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Media;
    using AJut.UX.AttachedProperties;
    using AJut.UX.Converters;
    using DPUtils = DPUtils<ToggleStrip>;

    public class ToggleStrip : Control
    {
        // ================== [ Fields ]================================

        private bool m_isChangingSelectedItem;
        private bool m_isChangingSelectedItemsList;

        // ================== [ Dependency Properties ]================================

        public static readonly DependencyProperty ItemsSourceProperty = DPUtils.Register(_ => _.ItemsSource, (d, e) => d.OnItemsSourceChanged(e));

        public static readonly DependencyProperty AllowMultiSelectProperty = DPUtils.Register(_ => _.AllowMultiSelect);

        public static readonly DependencyProperty SelectedItemProperty = DPUtils.Register(_ => _.SelectedItem, (d, e) => d.OnSelectedItemChanged(e));
        public static readonly DependencyProperty SelectedItemsProperty = DPUtils.Register(_ => _.SelectedItems, (d, e) => d.OnSelectedItemsChanged(e));

        public static readonly DependencyProperty DisplayPropertyPathProperty = DPUtils.Register(_ => _.DisplayPropertyPath, "", (d, e) => d.OnDisplayPropertyPathChanged(e.NewValue));
        public static readonly DependencyProperty ItemTemplateProperty = DPUtils.Register(_ => _.ItemTemplate);
        public static readonly DependencyProperty SeparatorBrushProperty = DPUtils.Register(_ => _.SeparatorBrush);
        public static readonly DependencyProperty SeparatorThicknessProperty = DPUtils.Register(_ => _.SeparatorThickness, 1.0);

        public static readonly DependencyProperty BackgroundPressedColorBaseProperty = DPUtils.Register(_ => _.BackgroundPressedColorBase);
        public static readonly DependencyProperty ForegroundPressedProperty = DPUtils.Register(_ => _.ForegroundPressed);
        public static readonly DependencyProperty BackgroundHoverProperty = DPUtils.Register(_ => _.BackgroundHover);
        public static readonly DependencyProperty BackgroundHoverOverPressedProperty = DPUtils.Register(_ => _.BackgroundHoverOverPressed);
        public static readonly DependencyProperty ForegroundHoverProperty = DPUtils.Register(_ => _.ForegroundHover);

        public static readonly DependencyProperty ItemPaddingProperty = DPUtils.Register(_ => _.ItemPadding, new Thickness(6));
        public static readonly DependencyProperty AllowNoSelectionProperty = DPUtils.Register(_ => _.AllowNoSelection, false);
        private static readonly DependencyPropertyKey HasItemsPropertyKey = DPUtils.RegisterReadOnly(_ => _.HasItems);
        public static readonly DependencyProperty HasItemsProperty = HasItemsPropertyKey.DependencyProperty;
        private static readonly DependencyPropertyKey ItemsPropertyKey = DPUtils.RegisterReadOnly(_ => _.Items);
        public static readonly DependencyProperty ItemsProperty = ItemsPropertyKey.DependencyProperty;

        // ================== [ Construction ]================================

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
            this.Items.CollectionChanged += _OnItemsCollectionChanged;

            void _OnItemsCollectionChanged (object _s, EventArgs _e)
            {
                this.HasItems = this.Items.Count > 0;
                this.Items.ForEach(i => i.EvaluateOrder());
            }
        }

        // ================== [ Events ]================================

        public event EventHandler<ToggleStripSelectionChangedEventArgs> SelectionChanged;

        // ================== [ Properties ]================================

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
        public double SeparatorThickness
        {
            get => (double)this.GetValue(SeparatorThicknessProperty);
            set => this.SetValue(SeparatorThicknessProperty, value);
        }

        public Brush ForegroundPressed
        {
            get => (Brush)this.GetValue(ForegroundPressedProperty);
            set => this.SetValue(ForegroundPressedProperty, value);
        }

        public Brush ForegroundHover
        {
            get => (Brush)this.GetValue(ForegroundHoverProperty);
            set => this.SetValue(ForegroundHoverProperty, value);
        }

        public Color BackgroundPressedColorBase
        {
            get => (Color)this.GetValue(BackgroundPressedColorBaseProperty);
            set => this.SetValue(BackgroundPressedColorBaseProperty, value);
        }

        public Brush BackgroundHover
        {
            get => (Brush)this.GetValue(BackgroundHoverProperty);
            set => this.SetValue(BackgroundHoverProperty, value);
        }

        public Brush BackgroundHoverOverPressed
        {
            get => (Brush)this.GetValue(BackgroundHoverOverPressedProperty);
            set => this.SetValue(BackgroundHoverOverPressedProperty, value);
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

        public bool HasItems
        {
            get => (bool)this.GetValue(HasItemsProperty);
            protected set => this.SetValue(HasItemsPropertyKey, value);
        }

        public ToggleItemsCollection Items
        {
            get => (ToggleItemsCollection)this.GetValue(ItemsProperty);
            private set => this.SetValue(ItemsPropertyKey, value);
        }

        // ================== [ Private Utility Functions ]================================
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
                this.Items.Clear();
            }
            else
            {
                int index = 0;
                this.Items.ReplaceAllWith(
                    e.NewValue.OfType<object>().Select(i => new ToggleItem(this, i, index++, this.DisplayPropertyPath))
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
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                this.Items.Clear();
                return;
            }

            IEnumerable<ToggleItem> addedItems;
            if (e.NewItems != null)
            {
                addedItems = e.NewItems.OfType<object>()
                    .Select(i => new ToggleItem(this, i, -1, this.DisplayPropertyPath));
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

        // ================== [ Utility Classes ]================================

        public class ToggleItem : NotifyPropertyChanged
        {
            private string m_name;
            private bool m_isSelected;
            private int m_itemIndex;
            private bool m_hasDefaultName;

            private bool m_isFirstItem;
            private bool m_isLastItem;

            internal ToggleItem (ToggleStrip owner, object item, int index, string displayPropertyPath)
            {
                this.Data = item;
                m_itemIndex = index;
                this.ResetName(displayPropertyPath);
                this.Owner = owner;
            }

            public int ItemIndex
            {
                get => m_itemIndex;
                internal set
                {
                    if (this.SetAndRaiseIfChanged(ref m_itemIndex, value))
                    {
                        this.IsFirstItem = m_itemIndex == 0;
                        this.IsLastItem = Owner.Items.LastOrDefault() == this;
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
                    if (!value && !Owner.Items.CanBeDeselected())
                    {
                        return;
                    }

                    if (this.SetAndRaiseIfChanged(ref m_isSelected, value))
                    {
                        if (value)
                        {
                            Owner.Items.HandleWasSelected(this);
                        }
                        else
                        {
                            Owner.Items.HandleWasDeselected(this);
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

            public ToggleStrip Owner { get; }

            public bool IsLastItem
            {
                get => m_isLastItem;
                private set => this.SetAndRaiseIfChanged(ref m_isLastItem, value);
            }

            internal void EvaluateOrder ()
            {
                m_itemIndex = -1;
                this.ItemIndex = Owner.Items.IndexOf(this);
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
                Owner.Items.HandleWasDeselected(this);
            }

            private void ResetNameWithDefault ()
            {
                this.Name = $"Item {this.ItemIndex}";
                m_hasDefaultName = true;
            }
        }

        public class ToggleItemsCollection : ObservableCollection<ToggleItem>
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

            internal void ReplaceAllWith (IEnumerable<ToggleItem> enumerable)
            {
                this.Clear();
                this.AddEach(enumerable);
            }

            internal void AddAndRemove (IEnumerable<ToggleItem> addedItems, IEnumerable<ToggleItem> removedItems)
            {
                this.AddEach(addedItems);
                this.RemoveEach(removedItems);
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

    public class ToggleStripCornerRadiusConverter : SimpleValueConverter<ToggleStrip.ToggleItem, CornerRadius>
    {
        public double ReductionPercent { get; set; }
        protected override CornerRadius Convert (ToggleStrip.ToggleItem value)
        {
            CornerRadius corners = BorderXTA.GetCornerRadius(value.Owner);
            if (value.IsFirstItem)
            {
                if (value.IsLastItem)
                {
                    return corners;
                }

                return new CornerRadius(_Reduce(corners.TopLeft), 0, 0, _Reduce(corners.BottomLeft));
            }
            else if (value.IsLastItem)
            {
                return new CornerRadius(0, _Reduce(corners.TopRight), _Reduce(corners.BottomRight), 0);
            }

            return new CornerRadius(0);

            double _Reduce (double _v)
            {
                return Math.Max(0.0, _v * (1.0 - this.ReductionPercent));
            }
        }
    }

    public class ToggleStripBorderThicknessConverter : SimpleValueConverter<ToggleStrip.ToggleItem, Thickness>
    {
        public bool Inside { get; set; }
        protected override Thickness Convert (ToggleStrip.ToggleItem value)
        {
            if (value.IsFirstItem)
            {
                if (value.IsLastItem)
                {
                    // First AND last
                    if (this.Inside)
                    {
                        return new Thickness(0);
                    }
                    else
                    {
                        return value.Owner.BorderThickness;
                    }
                }

                // First
                return new Thickness(
                    this.Inside ? 0 : value.Owner.BorderThickness.Left,
                    this.Inside ? 0 : value.Owner.BorderThickness.Top,
                    this.Inside ? value.Owner.SeparatorThickness : 0,
                    this.Inside ? 0 : value.Owner.BorderThickness.Bottom
                );
            }
            else if (value.IsLastItem)
            {
                if (this.Inside)
                {
                    return new Thickness(0);
                }
                else
                {
                    return new Thickness(
                        0,
                        value.Owner.BorderThickness.Top,
                        value.Owner.BorderThickness.Right,
                        value.Owner.BorderThickness.Bottom
                    );
                }
            }

            // Center
            return new Thickness(
                0,
                this.Inside ? 0 : value.Owner.BorderThickness.Top,
                this.Inside ? value.Owner.SeparatorThickness : 0,
                this.Inside ? 0 : value.Owner.BorderThickness.Bottom
            );
        }
    }

    public class ToggleStripItemPressedBackgroundColorAlphatizer : SimpleValueConverter<Color, Color>
    {
        protected override Color Convert (Color value)
        {
            if (value.A == 255)
            {
                value.A = 55;
            }

            return value;
        }
    }

    public class ToggleStripItemPressedHoverBackgroundColorAlphatizer : SimpleValueConverter<Color, Color>
    {
        protected override Color Convert (Color value)
        {
            if (value.A == 255)
            {
                value.A = 55;
            }

            return value;
        }
    }
}
