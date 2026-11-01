namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Data;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut.UX.AttachedProperties;
    using AJut.UX.Converters;
    using DPUtils = DPUtils<ToggleStrip>;

    public class ToggleStrip : Control
    {
        // ================== [ Fields ]================================

        // Matches the Scroll-mode bump button Width (its Height when vertical) in ToggleStrip.xaml; used as content
        // clearance and as the scroll-into-view inset so a selected item lands clear of the bump buttons.
        private const double kScrollButtonClearance = 24.0;
        private const double kScrollStep = 48.0;

        private readonly ToggleStripDragScroll m_dragScroll = new ToggleStripDragScroll();
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
        public static readonly DependencyProperty DisabledBackgroundProperty = DPUtils.Register(_ => _.DisabledBackground);
        public static readonly DependencyProperty DisabledBorderBrushProperty = DPUtils.Register(_ => _.DisabledBorderBrush);
        public static readonly DependencyProperty DisabledForegroundProperty = DPUtils.Register(_ => _.DisabledForeground);
        public static readonly DependencyProperty DisabledSelectedBackgroundProperty = DPUtils.Register(_ => _.DisabledSelectedBackground);
        public static readonly DependencyProperty VerticalButtonBackgroundProperty = DPUtils.Register(_ => _.VerticalButtonBackground);
        public static readonly DependencyProperty VerticalButtonForegroundProperty = DPUtils.Register(_ => _.VerticalButtonForeground);
        public static readonly DependencyProperty VerticalButtonBorderBrushProperty = DPUtils.Register(_ => _.VerticalButtonBorderBrush);

        public static readonly DependencyProperty ItemPaddingProperty = DPUtils.Register(_ => _.ItemPadding, new Thickness(6));
        public static readonly DependencyProperty CornerRadiusProperty = DPUtils.Register(_ => _.CornerRadius, (d, e) => BorderXTA.SetCornerRadius(d, e.NewValue));
        public static readonly DependencyProperty AllowNoSelectionProperty = DPUtils.Register(_ => _.AllowNoSelection, false);
        private static readonly DependencyPropertyKey HasItemsPropertyKey = DPUtils.RegisterReadOnly(_ => _.HasItems);
        public static readonly DependencyProperty HasItemsProperty = HasItemsPropertyKey.DependencyProperty;
        private static readonly DependencyPropertyKey ItemsPropertyKey = DPUtils.RegisterReadOnly(_ => _.Items);
        public static readonly DependencyProperty ItemsProperty = ItemsPropertyKey.DependencyProperty;

        public static readonly DependencyProperty EnsureUniformSizeProperty = DPUtils.Register(_ => _.EnsureUniformSize, false, (d, e) => d.OnLayoutModeChanged());
        public static readonly DependencyProperty OverflowBehaviorProperty = DPUtils.Register(_ => _.OverflowBehavior, eToggleStripOverflow.Clip, (d, e) => d.OnLayoutModeChanged());
        public static readonly DependencyProperty OrientationProperty = DPUtils.Register(_ => _.Orientation, Orientation.Horizontal, (d, e) => d.OnOrientationChanged());
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
                m_scrollHost.PreviewMouseLeftButtonDown -= this.OnScrollHostPreviewMouseDown;
                m_scrollHost.PreviewMouseMove -= this.OnScrollHostPreviewMouseMove;
                m_scrollHost.PreviewMouseLeftButtonUp -= this.OnScrollHostPreviewMouseUp;
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
                m_scrollHost.PreviewMouseLeftButtonDown += this.OnScrollHostPreviewMouseDown;
                m_scrollHost.PreviewMouseMove += this.OnScrollHostPreviewMouseMove;
                m_scrollHost.PreviewMouseLeftButtonUp += this.OnScrollHostPreviewMouseUp;
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

        // Click-and-drag scrolling in Scroll mode, by AJut.UX's shared ToggleStripDragScroll. The preview events see the
        // press before the item button under it does. Past the threshold the scroll host takes the mouse capture, which
        // takes it from the button, so the button never clicks; the strip then follows the pointer until the release.
        private void OnScrollHostPreviewMouseDown (object sender, MouseButtonEventArgs e)
        {
            if (this.OverflowBehavior == eToggleStripOverflow.Scroll)
            {
                m_dragScroll.Begin(this.PositionAlongStrip(e.GetPosition(m_scrollHost)));
            }
        }

        private void OnScrollHostPreviewMouseMove (object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            double delta = m_dragScroll.Move(this.PositionAlongStrip(e.GetPosition(m_scrollHost)));
            if (!m_dragScroll.IsDragging)
            {
                return;
            }

            if (Mouse.Captured != m_scrollHost)
            {
                m_scrollHost.CaptureMouse();
            }

            this.NudgeScroll(-delta);
            e.Handled = true;
        }

        private void OnScrollHostPreviewMouseUp (object sender, MouseButtonEventArgs e)
        {
            if (m_dragScroll.End())
            {
                m_scrollHost.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private double PositionAlongStrip (Point point) => this.IsVertical ? point.Y : point.X;

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

            // Scroll mode hides the scrollbar along the strip (Hidden keeps the content scrollable); other modes
            // clip. Across the strip it is always Disabled, so the items get the strip's real cross size.
            bool scroll = this.OverflowBehavior == eToggleStripOverflow.Scroll;
            ScrollBarVisibility alongStrip = scroll ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
            bool isVertical = this.IsVertical;
            m_scrollHost.HorizontalScrollBarVisibility = isVertical ? ScrollBarVisibility.Disabled : alongStrip;
            m_scrollHost.VerticalScrollBarVisibility = isVertical ? alongStrip : ScrollBarVisibility.Disabled;
            this.UpdateScrollButtons();
        }

        private void UpdateScrollButtons ()
        {
            if (this.PART_ScrollLeftButton == null || this.PART_ScrollRightButton == null)
            {
                return;
            }

            // Everything here runs along the strip: left/right when horizontal, up/down when vertical. The
            // template's Orientation trigger moves the two buttons to the top and bottom edges.
            bool isVertical = this.IsVertical;
            double scrollable = isVertical
                ? (m_scrollHost?.ScrollableHeight ?? 0.0)
                : (m_scrollHost?.ScrollableWidth ?? 0.0);
            bool show = this.OverflowBehavior == eToggleStripOverflow.Scroll && scrollable > 0.5;

            this.PART_ScrollLeftButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            this.PART_ScrollRightButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            // Inset the items so scrolling to an end parks a button over empty padding, not an item.
            if (this.PART_ItemsHost != null)
            {
                double clearance = show ? kScrollButtonClearance : 0.0;
                this.PART_ItemsHost.Margin = isVertical
                    ? new Thickness(0, clearance, 0, clearance)
                    : new Thickness(clearance, 0, clearance, 0);
            }

            if (show)
            {
                double offset = isVertical ? m_scrollHost.VerticalOffset : m_scrollHost.HorizontalOffset;
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

            if (this.IsVertical)
            {
                double target = Math.Max(0.0, Math.Min(m_scrollHost.ScrollableHeight, m_scrollHost.VerticalOffset + delta));
                m_scrollHost.ScrollToVerticalOffset(target);
            }
            else
            {
                double target = Math.Max(0.0, Math.Min(m_scrollHost.ScrollableWidth, m_scrollHost.HorizontalOffset + delta));
                m_scrollHost.ScrollToHorizontalOffset(target);
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

        /// <summary>
        /// The strip's surface while it is disabled, in place of its Background. The AJut theme sets the disabled
        /// ComboBox's flat fill.
        /// </summary>
        public Brush DisabledBackground
        {
            get => (Brush)this.GetValue(DisabledBackgroundProperty);
            set => this.SetValue(DisabledBackgroundProperty, value);
        }

        /// <summary>The strip's outline while it is disabled, in place of its BorderBrush.</summary>
        public Brush DisabledBorderBrush
        {
            get => (Brush)this.GetValue(DisabledBorderBrushProperty);
            set => this.SetValue(DisabledBorderBrushProperty, value);
        }

        /// <summary>
        /// Every item's text while the strip is disabled, the selected item's too: a disabled strip keeps its selection
        /// shaded so it still shows, and nothing in it reads as live.
        /// </summary>
        public Brush DisabledForeground
        {
            get => (Brush)this.GetValue(DisabledForegroundProperty);
            set => this.SetValue(DisabledForegroundProperty, value);
        }

        /// <summary>
        /// The selected item's fill while the strip is disabled, in place of its pressed shading: muted, so the selection
        /// still shows without reading as live. The AJut theme sets a faded, desaturated primary.
        /// </summary>
        public Brush DisabledSelectedBackground
        {
            get => (Brush)this.GetValue(DisabledSelectedBackgroundProperty);
            set => this.SetValue(DisabledSelectedBackgroundProperty, value);
        }

        /// <summary>
        /// The scroll and overflow buttons' fill while the strip is vertical. The strip's surface shades top to bottom, so
        /// across a vertical strip's ends those buttons would show a slice of it that reads as part of neither; they take
        /// plain button colors instead. A horizontal strip's buttons sit inside its shading.
        /// </summary>
        public Brush VerticalButtonBackground
        {
            get => (Brush)this.GetValue(VerticalButtonBackgroundProperty);
            set => this.SetValue(VerticalButtonBackgroundProperty, value);
        }

        /// <summary>The scroll and overflow buttons' glyph color while the strip is vertical (see <see cref="VerticalButtonBackground"/>).</summary>
        public Brush VerticalButtonForeground
        {
            get => (Brush)this.GetValue(VerticalButtonForegroundProperty);
            set => this.SetValue(VerticalButtonForegroundProperty, value);
        }

        /// <summary>The scroll and overflow buttons' outline while the strip is vertical (see <see cref="VerticalButtonBackground"/>).</summary>
        public Brush VerticalButtonBorderBrush
        {
            get => (Brush)this.GetValue(VerticalButtonBorderBrushProperty);
            set => this.SetValue(VerticalButtonBorderBrushProperty, value);
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

        /// <summary>
        /// When true every item is grown to the largest item's natural length along the strip (the widest when
        /// horizontal, the tallest when vertical), so the buttons read as an even row or column rather than each
        /// hugging its own content.
        /// </summary>
        public bool EnsureUniformSize
        {
            get => (bool)this.GetValue(EnsureUniformSizeProperty);
            set => this.SetValue(EnsureUniformSizeProperty, value);
        }

        /// <summary>
        /// How the strip reacts when its items do not all fit (default Clip). Scroll and OverflowPopup only do
        /// anything when the strip is constrained along its own axis: a horizontal strip by width
        /// (HorizontalContentAlignment=Stretch or an explicit Width), a vertical one by height
        /// (VerticalContentAlignment=Stretch or an explicit Height). A content sized strip never overflows.
        /// </summary>
        public eToggleStripOverflow OverflowBehavior
        {
            get => (eToggleStripOverflow)this.GetValue(OverflowBehaviorProperty);
            set => this.SetValue(OverflowBehaviorProperty, value);
        }

        /// <summary>
        /// Lays the buttons out left to right (Horizontal, the default) or top to bottom (Vertical). A vertical
        /// strip stretches each button across its width, so it is as wide as its widest button unless sized
        /// otherwise; overflow, scrolling, uniform sizing and stretch fill (VerticalContentAlignment=Stretch) all
        /// work along its height instead of its width.
        /// </summary>
        public Orientation Orientation
        {
            get => (Orientation)this.GetValue(OrientationProperty);
            set => this.SetValue(OrientationProperty, value);
        }

        internal bool IsVertical => this.Orientation == Orientation.Vertical;

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

        // The items panel lays the items out again for the new axis, and gives each item its edges for it
        private void OnOrientationChanged () => this.OnLayoutModeChanged();
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

            this.RaiseSelectionChanged(previousSelectedItems, newlySelectedItems);

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

        // Owner-side raiser for the public SelectionChanged event. Both the select path
        // (HandleSelectionChanged) and the multi-select deselect path (ToggleItemsCollection.
        // HandleWasDeselected) funnel through here so the event is raised consistently.
        internal void RaiseSelectionChanged (IList previousSelection, IList currentSelection)
        {
            this.SelectionChanged?.Invoke(this, new ToggleStripSelectionChangedEventArgs(previousSelection, currentSelection));
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
            private ToggleStripItemEdges m_edges;

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

            /// <summary>
            /// Which of this item's button edges draw a separator and which corners take the strip's radius, by AJut.UX's
            /// shared <see cref="ToggleStripItemEdges"/>. Set by the strip's items panel each time it lays the items out.
            /// </summary>
            public ToggleStripItemEdges Edges
            {
                get => m_edges;
                internal set => this.SetAndRaiseIfChanged(ref m_edges, value);
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

                // This runs only as the deselect half of a single-select swap (the follow-on select
                // raises SelectionChanged for the whole change), so flag it to suppress a duplicate raise.
                this.Owner.Items.HandleWasDeselected(this, isPartOfSelectionSwap: true);
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

            internal void HandleWasDeselected (ToggleItem toggleItem, bool isPartOfSelectionSwap = false)
            {
                // Snapshot the selection as it stood before this deselect so a raised SelectionChanged
                // reports an accurate previous set (the bookkeeping below mutates SelectedItems in place).
                IList previousSelection = m_owner.SelectedItems?.OfType<object>().ToList() ?? new List<object>();

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

                // A standalone deselect changes the selection in its own right, so raise it here. The one
                // deselect that must NOT raise is the internal first-half of a single-select swap (via
                // SetSelectionWithoutChecking): that swap's follow-on select already raises once for the
                // whole change, so raising here too would double-fire. This is the only suppression case -
                // multi-select deselects and single-select deselect-to-empty (AllowNoSelection) both raise.
                if (!isPartOfSelectionSwap)
                {
                    IList currentSelection = this.Where(_ => _.IsSelected).Select(_ => _.Data).ToList();
                    m_owner.RaiseSelectionChanged(previousSelection, currentSelection);
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
    // The items panel for a ToggleStrip's main row (or column). It owns all the length-aware layout:
    // uniform sizing, stretch-fill (the old UniformGrid behavior), and overflow partitioning. Overflowed
    // items stay as children (so their natural length stays measurable) but are arranged off to a
    // zero rect; the strip's overflow popup mirrors them via ToggleItem.IsInOverflow.
    //
    // Lengths run along the strip: widths when it is horizontal, heights when it is vertical. The cross
    // size is the largest child's, and every visible child is arranged at that full cross size.
    public class ToggleStripPanel : Panel
    {
        // ===========[ Const-like ]===============================================
        // Kept in sync with the overflow button's Width (its Height when vertical) in ToggleStrip.xaml.
        private const double kReservedOverflowLength = 28.0;

        // ===========[ Instance fields ]==========================================
        private ToggleStrip m_owner;
        private bool[] m_overflow;
        private double[] m_arrangeLengths;
        private bool m_stretchFill;
        private bool m_isVertical;

        // ===========[ Layout ]===================================================
        protected override Size MeasureOverride (Size availableSize)
        {
            ToggleStrip owner = this.ResolveOwner();
            int count = this.InternalChildren.Count;
            bool isVertical = owner?.IsVertical == true;
            m_isVertical = isVertical;

            // 1. Measure every child at natural size. The overflow partition works from each item's length without its
            //    own separators, the way WinUI3 measures its buttons before giving them edges: the partition decides the
            //    edges, so lengths that include them would feed back into it, and a strip sized right at the boundary
            //    could flip between two layouts.
            var lengths = new double[count];
            var partitionLengths = new double[count];
            double separator = owner?.SeparatorThickness ?? 0.0;
            double maxCross = 0.0;
            for (int i = 0; i < count; ++i)
            {
                UIElement child = this.InternalChildren[i];
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                lengths[i] = isVertical ? child.DesiredSize.Height : child.DesiredSize.Width;
                partitionLengths[i] = lengths[i] - SeparatorLengthAlongStrip(this.GetItem(i), separator, isVertical);
                maxCross = Math.Max(maxCross, isVertical ? child.DesiredSize.Width : child.DesiredSize.Height);
            }

            bool uniform = owner?.EnsureUniformSize == true;
            bool popup = owner?.OverflowBehavior == eToggleStripOverflow.OverflowPopup;
            bool stretch = isVertical
                ? owner?.VerticalContentAlignment == VerticalAlignment.Stretch
                : owner?.HorizontalContentAlignment == HorizontalAlignment.Stretch;

            double availableLength = isVertical ? availableSize.Height : availableSize.Width;
            bool constrained = !double.IsPositiveInfinity(availableLength);

            // 2. Uniform sizing grows every item to the longest natural length.
            if (uniform)
            {
                double uniformLength = 0.0;
                double uniformPartitionLength = 0.0;
                for (int i = 0; i < count; ++i)
                {
                    uniformLength = Math.Max(uniformLength, lengths[i]);
                    uniformPartitionLength = Math.Max(uniformPartitionLength, partitionLengths[i]);
                }

                for (int i = 0; i < count; ++i)
                {
                    lengths[i] = uniformLength;
                    partitionLengths[i] = uniformPartitionLength;
                }
            }

            // 3. Overflow partition - only when in popup mode with a real length to fit into.
            m_overflow = new bool[count];
            bool hasOverflow = false;
            if (popup && constrained && count > 0)
            {
                // The item just picked (the strip's SelectedItem) claims the strip first, so a pick from the popup comes in
                var selected = new bool[count];
                int pickedIndex = -1;
                for (int i = 0; i < count; ++i)
                {
                    ToggleStrip.ToggleItem item = this.GetItem(i);
                    selected[i] = item?.IsSelected == true;
                    if (selected[i] && Equals(item.Data, owner.SelectedItem))
                    {
                        pickedIndex = i;
                    }
                }

                ToggleStripOverflowResult result = ToggleStripOverflowCalculator.Compute(partitionLengths, selected, availableLength, kReservedOverflowLength, pickedIndex);
                foreach (int idx in result.OverflowIndices)
                {
                    m_overflow[idx] = true;
                }

                hasOverflow = result.HasOverflow;
            }

            owner?.SetHasOverflow(hasOverflow);

            // Mirror the partition onto the items so the overflow popup can show the hidden ones, and give each item its
            // edges by the shared rules: an item in the strip by its place among the ones showing (the last one showing
            // is separated from the overflow button when there is one), an item in the popup as one of a square list
            // separated top to bottom.
            int overflowCount = 0;
            foreach (bool isInOverflow in m_overflow)
            {
                if (isInOverflow)
                {
                    ++overflowCount;
                }
            }

            int shownCount = count - overflowCount;
            int shownIndex = 0;
            int overflowIndex = 0;
            for (int i = 0; i < count; ++i)
            {
                ToggleStrip.ToggleItem item = this.GetItem(i);
                if (item == null)
                {
                    continue;
                }

                item.IsInOverflow = m_overflow[i];
                item.Edges = m_overflow[i]
                    ? new ToggleStripItemEdges(false, overflowIndex++ > 0, false, false, false, false, false, false)
                    : ToggleStripItemEdges.For(shownIndex++, shownCount, isVertical, hasOverflow);
            }

            // 4. Stretch fill (no overflow) splits the row evenly so the items are uniform and fill the
            //    length (the old UniformGrid behavior). The split is done in ArrangeOverride against the
            //    real final length - the panel is often measured at an infinite length (e.g. inside a
            //    stack running the same way), where dividing would be meaningless.
            m_arrangeLengths = lengths;
            m_stretchFill = stretch && !uniform && !popup && count > 0;

            // 5. Desired length is the sum of the visible item lengths.
            double total = 0.0;
            for (int i = 0; i < count; ++i)
            {
                if (!m_overflow[i])
                {
                    total += m_arrangeLengths[i];
                }
            }

            // When overflowing, include the chevron reserve in the desired length. Otherwise a content
            // sized strip (HorizontalContentAlignment=Left, or VerticalContentAlignment=Top when vertical)
            // shrinks the Border down to just the visible items, pulling the trailing-edge overflow chevron
            // on top of the last item.
            double desiredLength = hasOverflow ? total + kReservedOverflowLength : total;
            desiredLength = constrained ? Math.Min(desiredLength, availableLength) : desiredLength;
            return isVertical ? new Size(maxCross, desiredLength) : new Size(desiredLength, maxCross);
        }

        protected override Size ArrangeOverride (Size finalSize)
        {
            int count = this.InternalChildren.Count;
            double finalLength = m_isVertical ? finalSize.Height : finalSize.Width;

            // Stretch fill: every visible item gets an equal share of the real final length (uniform + fills).
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
                    stretchEach = finalLength / visible;
                }
            }

            double position = 0.0;
            for (int i = 0; i < count; ++i)
            {
                UIElement child = this.InternalChildren[i];
                if (m_overflow != null && i < m_overflow.Length && m_overflow[i])
                {
                    // Overflowed - keep it measured but out of the visible row.
                    child.Arrange(new Rect(0, 0, 0, 0));
                    continue;
                }

                double naturalLength = m_isVertical ? child.DesiredSize.Height : child.DesiredSize.Width;
                double length = m_stretchFill
                    ? stretchEach
                    : ((m_arrangeLengths != null && i < m_arrangeLengths.Length) ? m_arrangeLengths[i] : naturalLength);
                child.Arrange(m_isVertical
                    ? new Rect(0, position, finalSize.Width, length)
                    : new Rect(position, 0, length, finalSize.Height)
                );
                position += length;
            }

            return finalSize;
        }

        // ===========[ Helpers ]==================================================
        // How much of an item's length along the strip is its own separators, as its edges stand
        private static double SeparatorLengthAlongStrip (ToggleStrip.ToggleItem item, double separator, bool isVertical)
        {
            if (item == null)
            {
                return 0.0;
            }

            ToggleStripItemEdges edges = item.Edges;
            int separators = isVertical
                ? (edges.SeparatorTop ? 1 : 0) + (edges.SeparatorBottom ? 1 : 0)
                : (edges.SeparatorLeft ? 1 : 0) + (edges.SeparatorRight ? 1 : 0);
            return separators * separator;
        }

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

    // An item button's corners, from the item's Edges: each corner it rounds takes the strip's radius, reduced by
    // ReductionPercent so the button sits inside the strip's own rounded border. An item rounding all four is the only
    // one showing, and takes the strip's corners as they are.
    //
    // A binding to the item itself (no path) runs once, when the button is made, so the template binds this as a
    // MultiBinding: the first value is the item, and the second (its Edges) is only there so the binding runs again
    // when the item's edges change.
    public class ToggleStripCornerRadiusConverter : SimpleValueConverter<ToggleStrip.ToggleItem, CornerRadius>, IMultiValueConverter
    {
        public double ReductionPercent { get; set; }

        object IMultiValueConverter.Convert (object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return (values.Length > 0) && (values[0] is ToggleStrip.ToggleItem item) ? this.Convert(item) : DependencyProperty.UnsetValue;
        }

        object[] IMultiValueConverter.ConvertBack (object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        protected override CornerRadius Convert (ToggleStrip.ToggleItem value)
        {
            CornerRadius corners = BorderXTA.GetCornerRadius(value.Owner);
            ToggleStripItemEdges edges = value.Edges;
            if (edges.RoundTopLeft && edges.RoundTopRight && edges.RoundBottomRight && edges.RoundBottomLeft)
            {
                return corners;
            }

            return new CornerRadius(
                edges.RoundTopLeft ? _Reduce(corners.TopLeft) : 0,
                edges.RoundTopRight ? _Reduce(corners.TopRight) : 0,
                edges.RoundBottomRight ? _Reduce(corners.BottomRight) : 0,
                edges.RoundBottomLeft ? _Reduce(corners.BottomLeft) : 0
            );

            double _Reduce (double _v)
            {
                return Math.Max(0.0, _v * (1.0 - this.ReductionPercent));
            }
        }
    }

    // An item button's separators, from the item's Edges: the strip's SeparatorThickness on each edge that draws one,
    // which leads every button but the first. The strip's own border is the outline, so the buttons draw nothing of
    // it. Bound as a MultiBinding for the same reason as ToggleStripCornerRadiusConverter.
    public class ToggleStripBorderThicknessConverter : SimpleValueConverter<ToggleStrip.ToggleItem, Thickness>, IMultiValueConverter
    {
        object IMultiValueConverter.Convert (object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return (values.Length > 0) && (values[0] is ToggleStrip.ToggleItem item) ? this.Convert(item) : DependencyProperty.UnsetValue;
        }

        object[] IMultiValueConverter.ConvertBack (object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        protected override Thickness Convert (ToggleStrip.ToggleItem value)
        {
            ToggleStripItemEdges edges = value.Edges;
            double separator = value.Owner.SeparatorThickness;
            return new Thickness(
                edges.SeparatorLeft ? separator : 0,
                edges.SeparatorTop ? separator : 0,
                edges.SeparatorRight ? separator : 0,
                edges.SeparatorBottom ? separator : 0
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
