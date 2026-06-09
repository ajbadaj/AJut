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
    using System.Windows.Controls.Primitives;
    using System.Windows.Data;
    using System.Windows.Media;
    using AJut.UX.AttachedProperties;
    using AJut.UX.Converters;
    using DPUtils = DPUtils<ToggleStrip>;

    public class ToggleStrip : Control
    {
        // ================== [ Fields ]================================

        // Matches the Scroll-mode bump button Width in ToggleStrip.xaml; used as content clearance and as
        // the scroll-into-view inset so a selected item lands clear of the bump buttons.
        private const double kScrollButtonClearance = 24.0;
        private const double kScrollStep = 48.0;

        private bool m_isChangingSelectedItem;
        private bool m_isChangingSelectedItemsList;
        private ScrollViewer m_scrollHost;
        private RepeatButton PART_ScrollLeftButton;
        private RepeatButton PART_ScrollRightButton;
        private ItemsControl PART_ItemsHost;

        // ================== [ Dependency Properties ]================================

        public static readonly DependencyProperty ItemsSourceProperty = DPUtils.Register(_ => _.ItemsSource, (d, e) => d.OnItemsSourceChanged(e));

        public static readonly DependencyProperty AllowMultiSelectProperty = DPUtils.Register(_ => _.AllowMultiSelect);

        public static readonly DependencyProperty SelectedItemProperty = DPUtils.Register(_ => _.SelectedItem, (d, e) => d.OnSelectedItemChanged(e));
        public static readonly DependencyProperty SelectedItemsProperty = DPUtils.Register(_ => _.SelectedItems, (d, e) => d.OnSelectedItemsChanged(e));

        public static readonly DependencyProperty DisplayPropertyPathProperty = DPUtils.Register(_ => _.DisplayPropertyPath, "", (d, e) => d.OnDisplayPropertyPathChanged(e.NewValue));
        public static readonly DependencyProperty ItemTemplateProperty = DPUtils.Register(_ => _.ItemTemplate);
        public static readonly DependencyProperty SeparatorBrushProperty = DPUtils.Register(_ => _.SeparatorBrush);
        public static readonly DependencyProperty SeparatorThicknessProperty = DPUtils.Register(_ => _.SeparatorThickness, 1.0);

        public static readonly DependencyProperty OverflowPopupBackgroundProperty = DPUtils.Register(_ => _.OverflowPopupBackground);
        public static readonly DependencyProperty OverflowPopupBorderBrushProperty = DPUtils.Register(_ => _.OverflowPopupBorderBrush);

        public static readonly DependencyProperty BackgroundPressedColorBaseProperty = DPUtils.Register(_ => _.BackgroundPressedColorBase);
        public static readonly DependencyProperty ForegroundPressedProperty = DPUtils.Register(_ => _.ForegroundPressed);
        public static readonly DependencyProperty BackgroundHoverProperty = DPUtils.Register(_ => _.BackgroundHover);
        public static readonly DependencyProperty BackgroundHoverOverPressedProperty = DPUtils.Register(_ => _.BackgroundHoverOverPressed);
        public static readonly DependencyProperty ForegroundHoverProperty = DPUtils.Register(_ => _.ForegroundHover);

        public static readonly DependencyProperty ItemPaddingProperty = DPUtils.Register(_ => _.ItemPadding, new Thickness(6));
        public static readonly DependencyProperty CornerRadiusProperty = DPUtils.Register(_ => _.CornerRadius, (d, e) => BorderXTA.SetCornerRadius(d, e.NewValue));
        public static readonly DependencyProperty AllowNoSelectionProperty = DPUtils.Register(_ => _.AllowNoSelection, false);
        private static readonly DependencyPropertyKey HasItemsPropertyKey = DPUtils.RegisterReadOnly(_ => _.HasItems);
        public static readonly DependencyProperty HasItemsProperty = HasItemsPropertyKey.DependencyProperty;
        private static readonly DependencyPropertyKey ItemsPropertyKey = DPUtils.RegisterReadOnly(_ => _.Items);
        public static readonly DependencyProperty ItemsProperty = ItemsPropertyKey.DependencyProperty;

        public static readonly DependencyProperty EnsureUniformSizeProperty = DPUtils.Register(_ => _.EnsureUniformSize, false, (d, e) => d.OnLayoutModeChanged());
        public static readonly DependencyProperty OverflowBehaviorProperty = DPUtils.Register(_ => _.OverflowBehavior, eToggleStripOverflow.Clip, (d, e) => d.OnLayoutModeChanged());
        private static readonly DependencyPropertyKey HasOverflowPropertyKey = DPUtils.RegisterReadOnly(_ => _.HasOverflow);
        public static readonly DependencyProperty HasOverflowProperty = HasOverflowPropertyKey.DependencyProperty;

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
            this.SetBinding(SeparatorBrushProperty, this.CreateBinding(nameof(BorderBrush), BindingMode.OneWay));
            this.Items = new ToggleItemsCollection(this);
            this.Items.CollectionChanged += _OnItemsCollectionChanged;

            void _OnItemsCollectionChanged (object _s, EventArgs _e)
            {
                this.HasItems = this.Items.Count > 0;
                this.Items.ForEach(i => i.EvaluateOrder());
            }
        }

        // ================== [ Template ]================================

        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            // Unwire old parts (OnApplyTemplate can run more than once).
            if (m_scrollHost != null)
            {
                m_scrollHost.SizeChanged -= this.OnScrollHostSizeChanged;
                m_scrollHost.ScrollChanged -= this.OnScrollHostScrollChanged;
            }

            if (this.PART_ScrollLeftButton != null)
            {
                this.PART_ScrollLeftButton.Click -= this.OnScrollLeftClick;
            }

            if (this.PART_ScrollRightButton != null)
            {
                this.PART_ScrollRightButton.Click -= this.OnScrollRightClick;
            }

            m_scrollHost = this.GetTemplateChild("PART_ScrollHost") as ScrollViewer;
            this.PART_ScrollLeftButton = this.GetTemplateChild("PART_ScrollLeftButton") as RepeatButton;
            this.PART_ScrollRightButton = this.GetTemplateChild("PART_ScrollRightButton") as RepeatButton;
            this.PART_ItemsHost = this.GetTemplateChild("PART_ItemsHost") as ItemsControl;

            if (m_scrollHost != null)
            {
                m_scrollHost.SizeChanged += this.OnScrollHostSizeChanged;
                m_scrollHost.ScrollChanged += this.OnScrollHostScrollChanged;
            }

            if (this.PART_ScrollLeftButton != null)
            {
                this.PART_ScrollLeftButton.Click += this.OnScrollLeftClick;
            }

            if (this.PART_ScrollRightButton != null)
            {
                this.PART_ScrollRightButton.Click += this.OnScrollRightClick;
            }

            this.ApplyScrollMode();
        }

        private void OnScrollHostSizeChanged (object sender, SizeChangedEventArgs e)
        {
            this.ItemsPanelInstance?.InvalidateMeasure();
            this.UpdateScrollButtons();
        }

        private void OnScrollHostScrollChanged (object sender, ScrollChangedEventArgs e) => this.UpdateScrollButtons();
        private void OnScrollLeftClick (object sender, RoutedEventArgs e) => this.NudgeScroll(-kScrollStep);
        private void OnScrollRightClick (object sender, RoutedEventArgs e) => this.NudgeScroll(kScrollStep);

        // ================== [ Scroll mode (bump buttons) ]================================
        // Bump buttons + scroll are driven from code rather than template triggers: FindAncestor bindings
        // in ControlTemplate.Triggers proved unreliable in this template.
        private void ApplyScrollMode ()
        {
            if (m_scrollHost == null)
            {
                return;
            }

            // Scroll mode hides the scrollbar (Hidden keeps the content scrollable); other modes clip.
            bool scroll = this.OverflowBehavior == eToggleStripOverflow.Scroll;
            m_scrollHost.HorizontalScrollBarVisibility = scroll ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
            this.UpdateScrollButtons();
        }

        private void UpdateScrollButtons ()
        {
            if (this.PART_ScrollLeftButton == null || this.PART_ScrollRightButton == null)
            {
                return;
            }

            double scrollable = m_scrollHost?.ScrollableWidth ?? 0.0;
            bool show = this.OverflowBehavior == eToggleStripOverflow.Scroll && scrollable > 0.5;

            this.PART_ScrollLeftButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            this.PART_ScrollRightButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            // Inset the items so scrolling to an end parks a button over empty padding, not an item.
            if (this.PART_ItemsHost != null)
            {
                this.PART_ItemsHost.Margin = show
                    ? new Thickness(kScrollButtonClearance, 0, kScrollButtonClearance, 0)
                    : new Thickness(0);
            }

            if (show)
            {
                double offset = m_scrollHost.HorizontalOffset;
                this.PART_ScrollLeftButton.IsEnabled = offset > 0.5;
                this.PART_ScrollRightButton.IsEnabled = offset < scrollable - 0.5;
            }
        }

        private void NudgeScroll (double delta)
        {
            if (m_scrollHost == null)
            {
                return;
            }

            double target = Math.Max(0.0, Math.Min(m_scrollHost.ScrollableWidth, m_scrollHost.HorizontalOffset + delta));
            m_scrollHost.ScrollToHorizontalOffset(target);
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

        public Brush OverflowPopupBackground
        {
            get => (Brush)this.GetValue(OverflowPopupBackgroundProperty);
            set => this.SetValue(OverflowPopupBackgroundProperty, value);
        }

        public Brush OverflowPopupBorderBrush
        {
            get => (Brush)this.GetValue(OverflowPopupBorderBrushProperty);
            set => this.SetValue(OverflowPopupBorderBrushProperty, value);
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

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)this.GetValue(CornerRadiusProperty);
            set => this.SetValue(CornerRadiusProperty, value);
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

        // EnsureUniformSize: when true every item is widened to the widest item's natural size, so the
        // buttons read as an even row rather than each hugging its own content.
        public bool EnsureUniformSize
        {
            get => (bool)this.GetValue(EnsureUniformSizeProperty);
            set => this.SetValue(EnsureUniformSizeProperty, value);
        }

        // OverflowBehavior: how the strip reacts when its items do not all fit (default Clip). Scroll
        // and OverflowPopup only do anything when the strip is width constrained
        // (HorizontalContentAlignment=Stretch or an explicit Width) - a content sized strip never overflows.
        public eToggleStripOverflow OverflowBehavior
        {
            get => (eToggleStripOverflow)this.GetValue(OverflowBehaviorProperty);
            set => this.SetValue(OverflowBehaviorProperty, value);
        }

        // HasOverflow: true while one or more items are tucked into the overflow popup.
        public bool HasOverflow
        {
            get => (bool)this.GetValue(HasOverflowProperty);
            private set => this.SetValue(HasOverflowPropertyKey, value);
        }

        // Set by ToggleStripPanel when it attaches so layout-affecting changes can re-run the partition.
        internal ToggleStripPanel ItemsPanelInstance { get; set; }

        // ================== [ Private Utility Functions ]================================
        internal void SetHasOverflow (bool value)
        {
            // Guard so writing this during the panel's MeasureOverride does not kick off a redundant
            // layout invalidation when nothing actually changed.
            if (this.HasOverflow != value)
            {
                this.HasOverflow = value;
            }
        }

        private void OnLayoutModeChanged ()
        {
            this.ApplyScrollMode();
            this.ItemsPanelInstance?.InvalidateMeasure();
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
                this.SelectedItems.Clear();
                ;
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

            // Selection changing can pull a selected item back into view (it is kept visible where
            // possible), so re-run the overflow partition.
            this.ItemsPanelInstance?.InvalidateMeasure();

            // In Scroll mode bring the (first) selected item fully into view. Deferred to after the
            // layout pass (we just invalidated measure) so the container positions are current when
            // the scroll offset is computed.
            if (this.OverflowBehavior == eToggleStripOverflow.Scroll && m_scrollHost != null)
            {
                ToggleItem firstSelected = this.Items.FirstOrDefault(i => i.IsSelected);
                if (firstSelected != null)
                {
                    this.Dispatcher.BeginInvoke(
                        new Action(() => m_scrollHost.ScrollFirstElementIntoView(fe => fe.DataContext is ToggleItem ti && ReferenceEquals(ti, firstSelected), kScrollButtonClearance, kScrollButtonClearance)),
                        System.Windows.Threading.DispatcherPriority.Loaded
                    );
                }
            }
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
            private bool m_isInOverflow;

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
                        this.IsLastItem = this.Owner.Items.LastOrDefault() == this;
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
                    if (!value && !this.Owner.Items.CanBeDeselected())
                    {
                        return;
                    }

                    if (this.SetAndRaiseIfChanged(ref m_isSelected, value))
                    {
                        if (value)
                        {
                            this.Owner.Items.HandleWasSelected(this);
                        }
                        else
                        {
                            this.Owner.Items.HandleWasDeselected(this);
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

            public bool IsInOverflow
            {
                get => m_isInOverflow;
                internal set => this.SetAndRaiseIfChanged(ref m_isInOverflow, value);
            }

            internal void EvaluateOrder ()
            {
                m_itemIndex = -1;
                this.ItemIndex = this.Owner.Items.IndexOf(this);
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
                this.Owner.Items.HandleWasDeselected(this);
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

    // ===========[ ToggleStripPanel ]===========================================
    // The items panel for a ToggleStrip's main row. It owns all the width-aware layout: uniform
    // sizing, stretch-fill (the old UniformGrid behavior), and overflow partitioning. Overflowed
    // items stay as children (so their natural width stays measurable) but are arranged off to a
    // zero rect; the strip's overflow popup mirrors them via ToggleItem.IsInOverflow.
    public class ToggleStripPanel : Panel
    {
        // ===========[ Const-like ]===============================================
        // Kept in sync with the overflow button's Width in ToggleStrip.xaml.
        private const double kReservedOverflowWidth = 28.0;

        // ===========[ Instance fields ]==========================================
        private ToggleStrip m_owner;
        private bool[] m_overflow;
        private double[] m_arrangeWidths;
        private bool m_stretchFill;

        // ===========[ Layout ]===================================================
        protected override Size MeasureOverride (Size availableSize)
        {
            ToggleStrip owner = this.ResolveOwner();
            int count = this.InternalChildren.Count;

            // 1. Measure every child at natural size.
            var widths = new double[count];
            double maxHeight = 0.0;
            for (int i = 0; i < count; ++i)
            {
                UIElement child = this.InternalChildren[i];
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                widths[i] = child.DesiredSize.Width;
                maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
            }

            bool uniform = owner?.EnsureUniformSize == true;
            bool popup = owner?.OverflowBehavior == eToggleStripOverflow.OverflowPopup;
            bool stretch = owner?.HorizontalContentAlignment == HorizontalAlignment.Stretch;

            bool constrained = !double.IsPositiveInfinity(availableSize.Width);

            // 2. Uniform sizing widens every item to the widest natural width.
            if (uniform)
            {
                double uniformWidth = 0.0;
                foreach (double w in widths)
                {
                    uniformWidth = Math.Max(uniformWidth, w);
                }

                for (int i = 0; i < count; ++i)
                {
                    widths[i] = uniformWidth;
                }
            }

            // 3. Overflow partition - only when in popup mode with a real width to fit into.
            m_overflow = new bool[count];
            bool hasOverflow = false;
            if (popup && constrained && count > 0)
            {
                var selected = new bool[count];
                for (int i = 0; i < count; ++i)
                {
                    selected[i] = this.GetItem(i)?.IsSelected == true;
                }

                ToggleStripOverflowResult result = ToggleStripOverflowCalculator.Compute(widths, selected, availableSize.Width, kReservedOverflowWidth);
                foreach (int idx in result.OverflowIndices)
                {
                    m_overflow[idx] = true;
                }

                hasOverflow = result.HasOverflow;
            }

            owner?.SetHasOverflow(hasOverflow);

            // Mirror the partition onto the items so the overflow popup can show the hidden ones.
            for (int i = 0; i < count; ++i)
            {
                ToggleStrip.ToggleItem item = this.GetItem(i);
                if (item != null)
                {
                    item.IsInOverflow = m_overflow[i];
                }
            }

            // 4. Stretch fill (no overflow) splits the row evenly so the items are uniform and fill the
            //    width (the old UniformGrid behavior). The split is done in ArrangeOverride against the
            //    real final width - the panel is often measured at infinite width (e.g. inside a
            //    horizontal stack), where dividing would be meaningless.
            m_arrangeWidths = widths;
            m_stretchFill = stretch && !uniform && !popup && count > 0;

            // 5. Desired size is the sum of the visible item widths.
            double total = 0.0;
            for (int i = 0; i < count; ++i)
            {
                if (!m_overflow[i])
                {
                    total += m_arrangeWidths[i];
                }
            }

            // When overflowing, include the chevron reserve in the desired width. Otherwise a content
            // sized strip (HorizontalContentAlignment=Left) shrinks the Border down to just the visible
            // items, pulling the right-anchored overflow chevron on top of the last item.
            double desiredWidth = hasOverflow ? total + kReservedOverflowWidth : total;
            desiredWidth = constrained ? Math.Min(desiredWidth, availableSize.Width) : desiredWidth;
            return new Size(desiredWidth, maxHeight);
        }

        protected override Size ArrangeOverride (Size finalSize)
        {
            int count = this.InternalChildren.Count;

            // Stretch fill: every visible item gets an equal share of the real final width (uniform + fills).
            double stretchEach = 0.0;
            if (m_stretchFill)
            {
                int visible = 0;
                for (int i = 0; i < count; ++i)
                {
                    if (m_overflow == null || i >= m_overflow.Length || !m_overflow[i])
                    {
                        ++visible;
                    }
                }

                if (visible > 0)
                {
                    stretchEach = finalSize.Width / visible;
                }
            }

            double x = 0.0;
            for (int i = 0; i < count; ++i)
            {
                UIElement child = this.InternalChildren[i];
                if (m_overflow != null && i < m_overflow.Length && m_overflow[i])
                {
                    // Overflowed - keep it measured but out of the visible row.
                    child.Arrange(new Rect(0, 0, 0, 0));
                    continue;
                }

                double w = m_stretchFill
                    ? stretchEach
                    : ((m_arrangeWidths != null && i < m_arrangeWidths.Length) ? m_arrangeWidths[i] : child.DesiredSize.Width);
                child.Arrange(new Rect(x, 0, w, finalSize.Height));
                x += w;
            }

            return finalSize;
        }

        // ===========[ Helpers ]==================================================
        private ToggleStrip.ToggleItem GetItem (int index)
            => (this.InternalChildren[index] as FrameworkElement)?.DataContext as ToggleStrip.ToggleItem;

        private ToggleStrip ResolveOwner ()
        {
            if (m_owner == null)
            {
                DependencyObject d = this;
                while (d != null && !(d is ToggleStrip))
                {
                    d = VisualTreeHelper.GetParent(d);
                }

                m_owner = d as ToggleStrip;
                if (m_owner != null)
                {
                    m_owner.ItemsPanelInstance = this;
                }
            }

            return m_owner;
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
