namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Data;
    using Microsoft.UI.Xaml.Media;
    using Windows.Foundation;
    using DPUtils = AJut.UX.DPUtils<ToggleStrip>;

    // ===========[ ToggleStrip ]================================================
    // A horizontal (or vertical) strip of mutually exclusive toggle buttons.
    // Exactly one item is selected at a time (unless AllowNoSelection = true).
    // Items are provided via ItemsSource or built manually via the Items collection.
    //
    // Template parts:
    //   PART_ItemsPanel  - StackPanel that holds the generated toggle buttons
    //
    // Note: CornerRadius distribution and BorderThickness are intentionally set in
    // code (CreateItemButton) because they are index-aware - the first and last buttons
    // receive rounded corners matching the strip's CornerRadius DP, while interior
    // buttons receive squared corners. These are structural, not stylistic, assignments.
    // Button Padding comes from the ItemPadding DP (default 8,4); callers can override
    // via that DP or via ToggleButtonItemStyle.

    [TemplatePart(Name = nameof(PART_ScrollHost), Type = typeof(BumpStack))]
    [TemplatePart(Name = nameof(PART_OverflowButton), Type = typeof(Button))]
    public class ToggleStrip : Control
    {
        // ===========[ Const-like ]===============================================
        private const double kFallbackOverflowButtonWidth = 28.0;
        private static readonly PropertyPath kIsSelectedPath = new PropertyPath("IsSelected");
        private static readonly PropertyPath kNamePath = new PropertyPath("Name");
        private static readonly PropertyPath kBorderBrushPath = new PropertyPath("BorderBrush");

        // ===========[ Instance fields ]==========================================
        // PART_ScrollHost is a BumpStack that hosts PART_ItemsPanel (its single child) and provides the
        // Scroll-mode bump buttons. PART_ItemsPanel still holds the per-item buttons across all modes.
        private BumpStack PART_ScrollHost;
        private StackPanel PART_ItemsPanel;
        private Button PART_OverflowButton;
        private StackPanel PART_OverflowPanel;

        private readonly List<ToggleStripButton> m_buttons = new List<ToggleStripButton>();
        private readonly List<double> m_naturalWidths = new List<double>();
        private double m_uniformItemWidth;
        private bool m_isUpdatingOverflow;

        private bool m_isChangingSelectedItem;
        private bool m_isChangingSelectedItemsList;

        // ===========[ Construction ]=============================================
        public ToggleStrip ()
        {
            this.DefaultStyleKey = typeof(ToggleStrip);
            this.Items = new ToggleItemsCollection(this);
            this.Items.CollectionChanged += this.Items_OnCollectionChanged;
            this.SizeChanged += this.OnSizeChanged;
        }

        // ===========[ Events ]===================================================
        public event EventHandler<ToggleStripSelectionChangedEventArgs> SelectionChanged;

        // ===========[ Dependency Properties ]====================================

        public static readonly DependencyProperty ItemsSourceProperty = DPUtils.Register(_ => _.ItemsSource, (d, e) => d.OnItemsSourceChanged(e));
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)this.GetValue(ItemsSourceProperty);
            set => this.SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty AllowMultiSelectProperty = DPUtils.Register(_ => _.AllowMultiSelect, false);
        public bool AllowMultiSelect
        {
            get => (bool)this.GetValue(AllowMultiSelectProperty);
            set => this.SetValue(AllowMultiSelectProperty, value);
        }

        public static readonly DependencyProperty AllowNoSelectionProperty = DPUtils.Register(_ => _.AllowNoSelection, false);
        public bool AllowNoSelection
        {
            get => (bool)this.GetValue(AllowNoSelectionProperty);
            set => this.SetValue(AllowNoSelectionProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty = DPUtils.Register(_ => _.SelectedItem, (d, e) => d.OnSelectedItemChanged(e));
        public object SelectedItem
        {
            get => this.GetValue(SelectedItemProperty);
            set => this.SetValue(SelectedItemProperty, value);
        }

        public static readonly DependencyProperty SelectedItemsProperty = DPUtils.Register(_ => _.SelectedItems, (d, e) => d.OnSelectedItemsChanged(e));
        public IList SelectedItems
        {
            get => (IList)this.GetValue(SelectedItemsProperty);
            set => this.SetValue(SelectedItemsProperty, value);
        }

        public static readonly DependencyProperty DisplayPropertyPathProperty = DPUtils.Register(_ => _.DisplayPropertyPath, "", (d, e) => d.OnDisplayPropertyPathChanged(e.NewValue));
        public string DisplayPropertyPath
        {
            get => (string)this.GetValue(DisplayPropertyPathProperty);
            set => this.SetValue(DisplayPropertyPathProperty, value);
        }

        public static readonly DependencyProperty ItemTemplateProperty = DPUtils.Register(_ => _.ItemTemplate, (d, e) => d.RebuildItems());
        public DataTemplate ItemTemplate
        {
            get => (DataTemplate)this.GetValue(ItemTemplateProperty);
            set => this.SetValue(ItemTemplateProperty, value);
        }

        public static readonly DependencyProperty SeparatorThicknessProperty = DPUtils.Register(_ => _.SeparatorThickness, 1.0, (d, e) => d.RebuildItems());
        public double SeparatorThickness
        {
            get => (double)this.GetValue(SeparatorThicknessProperty);
            set => this.SetValue(SeparatorThicknessProperty, value);
        }

        public static readonly DependencyProperty HasItemsProperty = DPUtils.Register(_ => _.HasItems, false);
        public bool HasItems
        {
            get => (bool)this.GetValue(HasItemsProperty);
            private set => this.SetValue(HasItemsProperty, value);
        }

        public static readonly DependencyProperty ItemsProperty = DPUtils.Register(_ => _.Items);
        public ToggleItemsCollection Items
        {
            get => (ToggleItemsCollection)this.GetValue(ItemsProperty);
            private set => this.SetValue(ItemsProperty, value);
        }

        // ToggleButtonItemStyle: optional Style applied to each generated ToggleStripButton.
        // Use this to customize button appearance (font, colors, etc.) without subclassing.
        // Note: style TargetType must be ToggleStripButton or a compatible base type.
        // Code still sets CornerRadius, BorderThickness, and Padding (via ItemPadding) after
        // applying this style because those values are index-aware or DP-driven.
        public static readonly DependencyProperty ToggleButtonItemStyleProperty = DPUtils.Register(_ => _.ToggleButtonItemStyle, (d, e) => d.RebuildItems());
        public Style ToggleButtonItemStyle
        {
            get => (Style)this.GetValue(ToggleButtonItemStyleProperty);
            set => this.SetValue(ToggleButtonItemStyleProperty, value);
        }

        // ItemPadding: applied to each generated ToggleButton as a local value.
        // Default set in XAML style; override via this DP to adjust without replacing ToggleButtonItemStyle.
        public static readonly DependencyProperty ItemPaddingProperty = DPUtils.Register(_ => _.ItemPadding, new Thickness(8, 4, 8, 4), (d, e) => d.RebuildItems());
        public Thickness ItemPadding
        {
            get => (Thickness)this.GetValue(ItemPaddingProperty);
            set => this.SetValue(ItemPaddingProperty, value);
        }

        // EnsureUniformSize: when true every item is widened to the widest item's natural size, so
        // the buttons read as an even grid rather than each hugging its own content.
        public static readonly DependencyProperty EnsureUniformSizeProperty = DPUtils.Register(_ => _.EnsureUniformSize, false, (d, e) => d.RebuildItems());
        public bool EnsureUniformSize
        {
            get => (bool)this.GetValue(EnsureUniformSizeProperty);
            set => this.SetValue(EnsureUniformSizeProperty, value);
        }

        // OverflowBehavior: how the strip reacts when its items do not all fit (default Clip).
        // Scroll and OverflowPopup only do anything when the strip is width constrained
        // (e.g. HorizontalAlignment=Stretch or an explicit Width) - a content sized strip never overflows.
        public static readonly DependencyProperty OverflowBehaviorProperty = DPUtils.Register(_ => _.OverflowBehavior, eToggleStripOverflow.Clip, (d, e) => d.UpdateOverflowLayout());
        public eToggleStripOverflow OverflowBehavior
        {
            get => (eToggleStripOverflow)this.GetValue(OverflowBehaviorProperty);
            set => this.SetValue(OverflowBehaviorProperty, value);
        }

        // ===========[ Template application ]=====================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            this.PART_ScrollHost = this.GetTemplateChild(nameof(PART_ScrollHost)) as BumpStack;
            this.PART_OverflowButton = this.GetTemplateChild(nameof(PART_OverflowButton)) as Button;

            // PART_ItemsPanel is the BumpStack's single child (its content). It is reparented into the
            // BumpStack's own template, so grab it from the Children collection rather than via
            // GetTemplateChild. Likewise the overflow popup's panel lives inside the button's Flyout,
            // which GetTemplateChild can't reach - grab it from the Flyout content.
            this.PART_ItemsPanel = this.PART_ScrollHost?.Children.Count > 0 ? this.PART_ScrollHost.Children[0] as StackPanel : null;
            this.PART_OverflowPanel = (this.PART_OverflowButton?.Flyout as Flyout)?.Content as StackPanel;

            this.RebuildItems();
        }

        // ===========[ Events ]===================================================
        private void OnSizeChanged (object sender, SizeChangedEventArgs e)
        {
            // Re-run in every mode: OverflowPopup repartitions, and Clip/Scroll need the pass too -
            // uniform sizing depends on real measured widths.
            this.UpdateOverflowLayout();
        }

        // ===========[ Private handlers ]=========================================
        private void Items_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            this.HasItems = this.Items.Count > 0;
            this.Items.ForEach(i => i.EvaluateOrder());
            this.RebuildItems();
        }

        private void OnDisplayPropertyPathChanged (string newPath)
        {
            this.Items.ForEach(i => i.ResetName(newPath));
        }

        private void OnItemsSourceChanged (DependencyPropertyChangedEventArgs<IEnumerable> e)
        {
            // 1. Remember the current selection so we can restore it if the new
            //    items contain the same value (uses Equals, not reference equality,
            //    so boxed value types survive the round-trip).
            object previousSelectedItem = this.SelectedItem;

            this.SelectedItem = null;
            if (this.SelectedItems is INotifyCollectionChanged)
            {
                this.SelectedItems.Clear();
            }
            else
            {
                this.SelectedItems = new List<object>();
            }

            // 2. Swap collection-changed subscriptions
            if (e.OldValue is INotifyCollectionChanged oldSource)
            {
                oldSource.CollectionChanged -= this.ItemsSource_OnCollectionChanged;
            }

            if (e.NewValue is INotifyCollectionChanged newSource)
            {
                newSource.CollectionChanged += this.ItemsSource_OnCollectionChanged;
            }

            // 3. Rebuild the ToggleItem collection
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

                // 4. Try to restore the previous selection (value-based match)
                if (previousSelectedItem != null)
                {
                    ToggleItem match = this.Items.FirstOrDefault(ti => Equals(ti.Data, previousSelectedItem));
                    if (match != null)
                    {
                        match.IsSelected = true;
                        return;
                    }
                }

                // 5. Fall back: select the first item if not allowing empty selection
                if (this.Items.Count > 0
                    && (this.SelectedItems == null || this.SelectedItems.Count == 0)
                    && !this.AllowNoSelection)
                {
                    this.Items[0].IsSelected = true;
                }
            }
        }

        private void ItemsSource_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                this.Items.Clear();
                return;
            }

            IEnumerable<ToggleItem> addedItems = e.NewItems != null
                ? e.NewItems.OfType<object>().Select(i => new ToggleItem(this, i, -1, this.DisplayPropertyPath))
                : Enumerable.Empty<ToggleItem>();

            IEnumerable<ToggleItem> removedItems = e.OldItems != null
                ? this.Items.Where(ti => e.OldItems.Contains(ti.Data))
                : Enumerable.Empty<ToggleItem>();

            this.Items.AddAndRemove(addedItems, removedItems);
        }

        private void OnSelectedItemChanged (DependencyPropertyChangedEventArgs<object> e)
        {
            if (m_isChangingSelectedItem)
            {
                return;
            }

            if (e.NewValue == null)
            {
                this.PerformModificationWithBlocker(ref m_isChangingSelectedItemsList, () =>
                {
                    if (this.SelectedItems is INotifyCollectionChanged)
                    {
                        this.SelectedItems.Clear();
                    }
                    else
                    {
                        this.SelectedItems = null;
                    }
                });
                return;
            }

            ToggleItem newlySelected = this.Items.FirstOrDefault(ti => Equals(ti.Data, e.NewValue));
            if (newlySelected != null)
            {
                newlySelected.IsSelected = true;
            }
        }

        private void OnSelectedItemsChanged (DependencyPropertyChangedEventArgs<IList> e)
        {
            // External changes to SelectedItems are not re-processed to avoid cycles.
            // Selection is driven through SelectedItem or by clicking items directly.
        }

        internal void HandleSelectionChanged (ToggleItem latest)
        {
            IList previousSelectedItems = this.SelectedItems;
            IList newlySelectedItems;

            if (this.AllowMultiSelect)
            {
                newlySelectedItems = this.Items.Where(ti => ti.IsSelected).Select(ti => ti.Data).ToList();
            }
            else
            {
                newlySelectedItems = new List<object> { latest.Data };
            }

            this.PerformModificationWithBlocker(ref m_isChangingSelectedItem,
                () => this.SelectedItem = latest.Data
            );

            this.PerformModificationWithBlocker(ref m_isChangingSelectedItemsList, () =>
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
            });

            this.RaiseSelectionChanged(previousSelectedItems, newlySelectedItems);

            this.RefreshForSelection();
        }

        // Owner-side raiser for the public SelectionChanged event. Both the select path
        // (HandleSelectionChanged) and the multi-select deselect path (ToggleItemsCollection.
        // HandleWasDeselected) funnel through here so the event is raised consistently.
        internal void RaiseSelectionChanged (IList previousSelection, IList currentSelection)
        {
            this.SelectionChanged?.Invoke(
                this,
                new ToggleStripSelectionChangedEventArgs(previousSelection, currentSelection)
            );
        }

        // ===========[ Item building ]============================================
        private void RebuildItems ()
        {
            if (this.PART_ItemsPanel == null)
            {
                return;
            }

            // 1. Detach any existing buttons from both hosts and start fresh.
            this.PART_ItemsPanel.Children.Clear();
            this.PART_OverflowPanel?.Children.Clear();
            m_buttons.Clear();
            m_naturalWidths.Clear();

            // 2. Build a button per item and capture a best-effort natural width up front. Widths are
            //    needed for uniform sizing and overflow math; they are refined from the real layout in
            //    UpdateOverflowLayout because an off-tree Measure can come back empty before first arrange.
            foreach (ToggleItem item in this.Items)
            {
                ToggleStripButton btn = this.CreateItemButton(item);
                btn.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                m_buttons.Add(btn);
                m_naturalWidths.Add(btn.DesiredSize.Width);
            }

            this.UpdateOverflowLayout();

            // The off-tree Measure above can come back empty before the buttons are first arranged. If
            // so, schedule one more pass after this layout cycle so uniform sizing and overflow can use
            // the real widths even when nothing triggers a SizeChanged (e.g. toggling a DP at runtime).
            if (m_buttons.Count > 0 && m_naturalWidths.All(w => w <= 0.0))
            {
                this.DispatcherQueue?.TryEnqueue(() => this.UpdateOverflowLayout());
            }
        }

        private ToggleStripButton CreateItemButton (ToggleItem item)
        {
            var btn = new ToggleStripButton();

            // Apply caller-provided style first so structural overrides set later (CornerRadius,
            // BorderThickness, Padding) are local values and take precedence.
            if (this.ToggleButtonItemStyle != null)
            {
                btn.Style = this.ToggleButtonItemStyle;
            }

            btn.Padding = this.ItemPadding;

            btn.SetBinding(Control.BorderBrushProperty, new Binding
            {
                Source = this,
                Path = kBorderBrushPath,
                Mode = BindingMode.OneWay,
            });

            // IsSelected <-> IsSelected (TwoWay so clicking drives selection logic)
            btn.SetBinding(ToggleStripButton.IsSelectedProperty, new Binding
            {
                Source = item,
                Path = kIsSelectedPath,
                Mode = BindingMode.TwoWay,
            });

            if (this.ItemTemplate != null)
            {
                // Custom template: show item.Data through the user-supplied template
                btn.Content = item.Data;
                btn.ContentTemplate = this.ItemTemplate;
            }
            else
            {
                // Default: show the display name (handles DisplayPropertyPath)
                btn.SetBinding(ContentControl.ContentProperty, new Binding
                {
                    Source = item,
                    Path = kNamePath,
                });
            }

            return btn;
        }

        // ===========[ Overflow layout ]==========================================
        private void UpdateOverflowLayout ()
        {
            if (this.PART_ItemsPanel == null || m_isUpdatingOverflow)
            {
                return;
            }

            m_isUpdatingOverflow = true;
            try
            {
                // Refine natural widths from the real layout if the up-front measure came back empty,
                // then widen everyone to the widest when uniform sizing is on.
                this.RefreshNaturalWidthsIfNeeded();
                m_uniformItemWidth = m_naturalWidths.Count > 0 ? m_naturalWidths.Max() : 0.0;
                foreach (ToggleStripButton btn in m_buttons)
                {
                    btn.MinWidth = this.EnsureUniformSize ? m_uniformItemWidth : 0.0;
                }

                // A UIElement can only live in one panel, so detach everything before redistributing.
                this.PART_ItemsPanel.Children.Clear();
                this.PART_OverflowPanel?.Children.Clear();

                this.ApplyScrollMode();

                // Clip / Scroll: every button stays in the strip; the host either clips or scrolls it.
                bool canPopup = this.OverflowBehavior == eToggleStripOverflow.OverflowPopup
                    && this.PART_OverflowPanel != null
                    && this.PART_OverflowButton != null;
                if (!canPopup)
                {
                    this.PlaceAllInStrip();
                    return;
                }

                // OverflowPopup: nothing to lay out against until we have a real viewport width.
                double available = this.PART_ScrollHost?.ActualWidth ?? this.ActualWidth;
                if (available <= 0.0)
                {
                    this.PlaceAllInStrip();
                    return;
                }

                // Partition by available width, keeping selected items visible where possible.
                double[] widths = new double[m_buttons.Count];
                bool[] selected = new bool[m_buttons.Count];
                for (int i = 0; i < m_buttons.Count; ++i)
                {
                    widths[i] = this.EnsureUniformSize ? m_uniformItemWidth : m_naturalWidths[i];
                    selected[i] = i < this.Items.Count && this.Items[i].IsSelected;
                }

                double reserved = this.MeasureOverflowButtonWidth();
                ToggleStripOverflowResult result = ToggleStripOverflowCalculator.Compute(widths, selected, available, reserved);

                var visibleButtons = new List<ToggleStripButton>(result.VisibleIndices.Count);
                foreach (int index in result.VisibleIndices)
                {
                    visibleButtons.Add(m_buttons[index]);
                    this.PART_ItemsPanel.Children.Add(m_buttons[index]);
                }

                var overflowButtons = new List<ToggleStripButton>(result.OverflowIndices.Count);
                foreach (int index in result.OverflowIndices)
                {
                    overflowButtons.Add(m_buttons[index]);
                    this.PART_OverflowPanel.Children.Add(m_buttons[index]);
                }

                // A trailing separator sits between the last visible item and the chevron so the
                // chevron reads as part of the strip rather than floating.
                this.ApplyEdgeStyling(visibleButtons, trailingSeparator: result.HasOverflow);
                this.ApplyFlyoutStyling(overflowButtons);
                this.PART_OverflowButton.Visibility = result.HasOverflow ? Visibility.Visible : Visibility.Collapsed;
            }
            finally
            {
                m_isUpdatingOverflow = false;
            }
        }

        private void PlaceAllInStrip ()
        {
            foreach (ToggleStripButton btn in m_buttons)
            {
                btn.HorizontalAlignment = HorizontalAlignment.Left;
                this.PART_ItemsPanel.Children.Add(btn);
            }

            this.ApplyEdgeStyling(m_buttons);
            if (this.PART_OverflowButton != null)
            {
                this.PART_OverflowButton.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyScrollMode ()
        {
            if (this.PART_ScrollHost != null)
            {
                // The BumpStack scrolls (with its bump buttons) only in Scroll mode; otherwise it clips.
                this.PART_ScrollHost.ScrollingEnabled = this.OverflowBehavior == eToggleStripOverflow.Scroll;
            }
        }

        // Distribute the strip's outer CornerRadius and separator borders across the given ordered,
        // currently-visible buttons so the group reads as one cohesive control. When trailingSeparator
        // is set the last item also gets a right divider - used to separate it from the overflow chevron.
        private void ApplyEdgeStyling (IReadOnlyList<ToggleStripButton> orderedButtons, bool trailingSeparator = false)
        {
            CornerRadius cr = this.CornerRadius;
            int last = orderedButtons.Count - 1;
            for (int i = 0; i < orderedButtons.Count; ++i)
            {
                ToggleStripButton btn = orderedButtons[i];
                bool isFirst = i == 0;
                bool isLast = i == last;

                double left = isFirst ? 0.0 : this.SeparatorThickness;
                double right = (isLast && trailingSeparator) ? this.SeparatorThickness : 0.0;

                btn.HorizontalAlignment = HorizontalAlignment.Left;
                btn.BorderThickness = new Thickness(left, 0, right, 0);

                // The strip continues into the chevron when there is a trailing separator, so the last
                // item stays square on the right - the strip's outer border supplies the rounded edge.
                bool roundLeft = isFirst;
                bool roundRight = isLast && !trailingSeparator;
                btn.CornerRadius = new CornerRadius(
                    roundLeft ? cr.TopLeft : 0,
                    roundRight ? cr.TopRight : 0,
                    roundRight ? cr.BottomRight : 0,
                    roundLeft ? cr.BottomLeft : 0
                );
            }
        }

        // Buttons in the overflow popup are a vertical list - square them off, stack their separators
        // on top, and stretch them to a shared width.
        private void ApplyFlyoutStyling (IReadOnlyList<ToggleStripButton> orderedButtons)
        {
            for (int i = 0; i < orderedButtons.Count; ++i)
            {
                ToggleStripButton btn = orderedButtons[i];
                btn.HorizontalAlignment = HorizontalAlignment.Stretch;
                btn.CornerRadius = new CornerRadius(0);
                btn.BorderThickness = i == 0
                    ? new Thickness(0)
                    : new Thickness(0, this.SeparatorThickness, 0, 0);
            }
        }

        // The up-front off-tree Measure in RebuildItems can return 0 before the buttons are first
        // arranged. Once they have a real ActualWidth, capture it as the natural width. We only do
        // this while widths are still empty so MinWidth-inflated values never overwrite the natural ones.
        private void RefreshNaturalWidthsIfNeeded ()
        {
            for (int i = 0; i < m_naturalWidths.Count; ++i)
            {
                if (m_naturalWidths[i] > 0.0)
                {
                    return;
                }
            }

            for (int i = 0; i < m_buttons.Count; ++i)
            {
                double actual = m_buttons[i].ActualWidth;
                if (actual > 0.0)
                {
                    m_naturalWidths[i] = actual;
                }
            }
        }

        private double MeasureOverflowButtonWidth ()
        {
            if (this.PART_OverflowButton == null)
            {
                return kFallbackOverflowButtonWidth;
            }

            this.PART_OverflowButton.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double measured = this.PART_OverflowButton.DesiredSize.Width;
            return measured > 0.0 ? measured : kFallbackOverflowButtonWidth;
        }

        private void RefreshForSelection ()
        {
            if (this.OverflowBehavior == eToggleStripOverflow.OverflowPopup)
            {
                // A selected item is kept visible where possible, so re-partition (and close the popup).
                this.PART_OverflowButton?.Flyout?.Hide();
                this.UpdateOverflowLayout();
            }
            else if (this.OverflowBehavior == eToggleStripOverflow.Scroll)
            {
                // Bring the (first) selected item fully into view past the bump buttons.
                ToggleStripButton target = this.FirstSelectedButton();
                if (target != null)
                {
                    this.PART_ScrollHost?.ScrollFirstElementIntoView(element => ReferenceEquals(element, target));
                }
            }
        }

        private ToggleStripButton FirstSelectedButton ()
        {
            for (int i = 0; i < m_buttons.Count && i < this.Items.Count; ++i)
            {
                if (this.Items[i].IsSelected)
                {
                    return m_buttons[i];
                }
            }

            return null;
        }

        // ===========[ Helpers ]==================================================
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

        // ===========[ Subclasses ]===============================================

        public class ToggleItem : INotifyPropertyChanged
        {
            // ===========[ Fields ]===============================================
            private string m_name;
            private bool m_isSelected;
            private int m_itemIndex;
            private bool m_hasDefaultName;
            private bool m_isFirstItem;
            private bool m_isLastItem;

            // ===========[ Construction ]=========================================
            internal ToggleItem (ToggleStrip owner, object data, int index, string displayPropertyPath)
            {
                this.Data = data;
                m_itemIndex = index;
                this.Owner = owner;
                this.ResetName(displayPropertyPath);
            }

            // ===========[ Events ]===============================================
            public event PropertyChangedEventHandler PropertyChanged;

            // ===========[ Properties ]==========================================
            public object Data { get; }
            public ToggleStrip Owner { get; }

            public int ItemIndex
            {
                get => m_itemIndex;
                internal set
                {
                    if (this.SetAndRaiseIfChanged(ref m_itemIndex, value))
                    {
                        this.IsFirstItem = (m_itemIndex == 0);
                        this.IsLastItem = (this.Owner.Items.LastOrDefault() == this);
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
                set
                {
                    if (!value && !this.Owner.Items.CanBeDeselected())
                    {
                        // Deselect rejected - raise PropertyChanged to revert the ToggleButton's IsChecked
                        this.RaisePropertyChanged(nameof(IsSelected));
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

            // ===========[ Internal methods ]=====================================
            internal void EvaluateOrder ()
            {
                m_itemIndex = -1;
                this.ItemIndex = this.Owner.Items.IndexOf(this);
            }

            internal void ResetName (string displayPropertyPath = null)
            {
                if (displayPropertyPath?.Length > 0)
                {
                    try
                    {
                        var prop = this.Data?.GetType().GetProperty(displayPropertyPath);
                        string resolved = prop?.GetValue(this.Data)?.ToString();
                        if (resolved != null)
                        {
                            this.Name = resolved;
                            m_hasDefaultName = false;
                            return;
                        }
                    }
                    catch { }
                }

                this.ResetNameWithDefault();
            }

            internal void SetSelectionWithoutChecking (bool isSelected)
            {
                m_isSelected = isSelected;
                this.RaisePropertyChanged(nameof(IsSelected));
                if (!isSelected)
                {
                    // This runs only as the deselect half of a single-select swap (the follow-on select
                    // raises SelectionChanged for the whole change), so flag it to suppress a duplicate raise.
                    this.Owner.Items.HandleWasDeselected(this, isPartOfSelectionSwap: true);
                }
            }

            // ===========[ Helpers ]=============================================
            private bool SetAndRaiseIfChanged<T> (ref T field, T value, [CallerMemberName] string propertyName = null)
            {
                if (EqualityComparer<T>.Default.Equals(field, value))
                {
                    return false;
                }

                field = value;
                this.RaisePropertyChanged(propertyName);
                return true;
            }

            private void RaisePropertyChanged (string propertyName)
                => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            private void ResetNameWithDefault ()
            {
                // Use Data.ToString() when it is meaningfully overridden (i.e. not
                // just the default type-name). Works naturally for strings, enums,
                // and value types. Falls back to positional label otherwise.
                string fromData = this.Data?.ToString();
                bool usable = fromData != null && fromData != this.Data.GetType().FullName;
                this.Name = usable ? fromData : $"Item {m_itemIndex}";
                m_hasDefaultName = !usable;
            }
        }

        public class ToggleItemsCollection : ObservableCollection<ToggleItem>
        {
            // ===========[ Fields ]===============================================
            private ToggleStrip m_owner;

            // ===========[ Construction ]=========================================
            internal ToggleItemsCollection (ToggleStrip owner)
            {
                m_owner = owner;
                this.CollectionChanged += this.Self_OnCollectionChanged;
            }

            // ===========[ Internal methods ]=====================================
            internal bool CanBeDeselected ()
                => m_owner.AllowNoSelection || (m_owner.SelectedItems?.Count ?? 0) > 1;

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
                    if (Equals(m_owner.SelectedItem, toggleItem.Data))
                    {
                        m_owner.SelectedItem = m_owner.SelectedItems.OfType<object>().LastOrDefault();
                    }
                }
                else if (m_owner.SelectedItems != null)
                {
                    m_owner.SelectedItems = m_owner.SelectedItems.OfType<object>()
                        .Where(i => !Equals(i, toggleItem.Data))
                        .ToList<object>();
                }

                // A standalone deselect changes the selection in its own right, so raise it here. The one
                // deselect that must NOT raise is the internal first-half of a single-select swap (via
                // SetSelectionWithoutChecking): that swap's follow-on select already raises once for the
                // whole change, so raising here too would double-fire. This is the only suppression case -
                // multi-select deselects and single-select deselect-to-empty (AllowNoSelection) both raise.
                if (!isPartOfSelectionSwap)
                {
                    IList currentSelection = this.Where(ti => ti.IsSelected).Select(ti => ti.Data).ToList();
                    m_owner.RaiseSelectionChanged(previousSelection, currentSelection);
                }
            }

            internal void ReplaceAllWith (IEnumerable<ToggleItem> items)
            {
                this.Clear();
                this.AddEach(items);
            }

            internal void AddAndRemove (IEnumerable<ToggleItem> addedItems, IEnumerable<ToggleItem> removedItems)
            {
                this.AddEach(addedItems);
                this.RemoveEach(removedItems);
            }

            // ===========[ Private handlers ]=====================================
            private void Self_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
            {
                int index = 0;
                this.ForEach(ti => ti.ItemIndex = index++);
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
}
