namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using Microsoft.UI.Input;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Data;
    using Microsoft.UI.Xaml.Input;
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
    // code (ApplyEdgeStyling) because they are index-aware - the first and last buttons
    // receive rounded corners matching the strip's CornerRadius DP, while interior
    // buttons receive squared corners. These are structural, not stylistic, assignments.
    // Button Padding comes from the ItemPadding DP (default 8,4); callers can override
    // via that DP or via ToggleButtonItemStyle.
    //
    // Orientation: the template's OrientationStates group (Horizontal / Vertical) turns the
    // BumpStack and the overflow button, and code turns the items panel. Everything measured along the strip
    // (natural sizes, uniform sizing, the overflow budget) is widths for a horizontal strip
    // and heights for a vertical one.
    //
    // Disabled: the template's CommonStates group (Normal / Disabled) puts DisabledBackground,
    // DisabledBorderBrush and DisabledForeground on the strip while IsEnabled is false.
    //
    // The bump and overflow buttons' colors hang on both, so a third group, ButtonColorStates (InStrip / Plain /
    // DisabledButtons), owns them and is picked from the orientation and IsEnabled together.

    [TemplatePart(Name = nameof(PART_ScrollHost), Type = typeof(BumpStack))]
    [TemplatePart(Name = nameof(PART_OverflowButton), Type = typeof(Button))]
    [TemplateVisualState(GroupName = kOrientationStatesGroup, Name = kHorizontalState)]
    [TemplateVisualState(GroupName = kOrientationStatesGroup, Name = kVerticalState)]
    [TemplateVisualState(GroupName = kCommonStatesGroup, Name = kNormalState)]
    [TemplateVisualState(GroupName = kCommonStatesGroup, Name = kDisabledState)]
    [TemplateVisualState(GroupName = kButtonColorStatesGroup, Name = kInStripButtonsState)]
    [TemplateVisualState(GroupName = kButtonColorStatesGroup, Name = kPlainButtonsState)]
    [TemplateVisualState(GroupName = kButtonColorStatesGroup, Name = kDisabledButtonsState)]
    public class ToggleStrip : Control, IToggleStripHost
    {
        // ===========[ Const-like ]===============================================
        // Used when the overflow button can't be measured yet (it measures empty while collapsed), along either axis.
        private const double kFallbackOverflowButtonLength = 28.0;
        private const string kOrientationStatesGroup = "OrientationStates";
        private const string kHorizontalState = "Horizontal";
        private const string kVerticalState = "Vertical";
        private const string kCommonStatesGroup = "CommonStates";
        private const string kNormalState = "Normal";
        private const string kDisabledState = "Disabled";
        private const string kButtonColorStatesGroup = "ButtonColorStates";
        private const string kInStripButtonsState = "InStrip";
        private const string kPlainButtonsState = "Plain";
        private const string kDisabledButtonsState = "DisabledButtons";
        private static readonly PropertyPath kIsSelectedPath = new PropertyPath("IsSelected");
        private static readonly PropertyPath kNamePath = new PropertyPath("Name");
        private static readonly PropertyPath kBorderBrushPath = new PropertyPath("BorderBrush");
        private static readonly PropertyPath kDisabledBorderBrushPath = new PropertyPath("DisabledBorderBrush");

        // ===========[ Instance fields ]==========================================
        // PART_ScrollHost is a BumpStack that hosts PART_ItemsPanel (its single child) and provides the
        // Scroll-mode bump buttons. PART_ItemsPanel still holds the per-item buttons across all modes.
        private BumpStack PART_ScrollHost;
        private StackPanel PART_ItemsPanel;
        private Button PART_OverflowButton;
        private StackPanel PART_OverflowPanel;

        // Natural lengths are measured along the strip: widths when horizontal, heights when vertical.
        private readonly List<ToggleStripButton> m_buttons = new List<ToggleStripButton>();
        private readonly List<double> m_naturalLengths = new List<double>();
        private double m_uniformItemLength;
        private bool m_isUpdatingOverflow;

        private bool m_isChangingSelectedItem;
        private bool m_isChangingSelectedItemsList;

        // Click-and-drag scrolling in Scroll mode; the handlers are kept so they can be removed from the template part
        private readonly ToggleStripDragScroll m_dragScroll = new ToggleStripDragScroll();
        private readonly PointerEventHandler m_dragPressedHandler;
        private readonly PointerEventHandler m_dragMovedHandler;
        private readonly PointerEventHandler m_dragReleasedHandler;
        private bool m_hasDragCapture;

        // ===========[ Construction ]=============================================
        public ToggleStrip ()
        {
            m_dragPressedHandler = this.OnScrollHostPointerPressed;
            m_dragMovedHandler = this.OnScrollHostPointerMoved;
            m_dragReleasedHandler = this.OnScrollHostPointerReleased;
            this.DefaultStyleKey = typeof(ToggleStrip);
            this.Items = new ToggleItemsCollection(this);
            this.Items.CollectionChanged += this.Items_OnCollectionChanged;
            this.SizeChanged += this.OnSizeChanged;
            this.IsEnabledChanged += this.OnIsEnabledChanged;
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

        /// <summary>
        /// When true every item is grown to the largest item's natural length along the strip (the widest when
        /// horizontal, the tallest when vertical), so the buttons read as an even grid rather than each hugging
        /// its own content.
        /// </summary>
        public static readonly DependencyProperty EnsureUniformSizeProperty = DPUtils.Register(_ => _.EnsureUniformSize, false, (d, e) => d.RebuildItems());
        public bool EnsureUniformSize
        {
            get => (bool)this.GetValue(EnsureUniformSizeProperty);
            set => this.SetValue(EnsureUniformSizeProperty, value);
        }

        /// <summary>
        /// How the strip reacts when its items do not all fit (default Clip). Scroll and OverflowPopup only do
        /// anything when the strip is constrained along its own axis: a horizontal strip by width (for example
        /// HorizontalAlignment=Stretch or an explicit Width), a vertical one by height (VerticalAlignment=Stretch in
        /// a sized parent, or an explicit Height). A content sized strip never overflows.
        /// </summary>
        public static readonly DependencyProperty OverflowBehaviorProperty = DPUtils.Register(_ => _.OverflowBehavior, eToggleStripOverflow.Clip, (d, e) => d.UpdateOverflowLayout());
        public eToggleStripOverflow OverflowBehavior
        {
            get => (eToggleStripOverflow)this.GetValue(OverflowBehaviorProperty);
            set => this.SetValue(OverflowBehaviorProperty, value);
        }

        /// <summary>
        /// Lays the buttons out left to right (Horizontal, the default) or top to bottom (Vertical). A vertical
        /// strip stretches each button across its width, so it is as wide as its widest button unless sized
        /// otherwise; overflow, scrolling and uniform sizing all work along its height instead of its width.
        /// </summary>
        public static readonly DependencyProperty OrientationProperty = DPUtils.Register(_ => _.Orientation, Orientation.Horizontal, (d, e) => d.OnOrientationChanged());
        public Orientation Orientation
        {
            get => (Orientation)this.GetValue(OrientationProperty);
            set => this.SetValue(OrientationProperty, value);
        }

        private void OnOrientationChanged ()
        {
            // The buttons are rebuilt so their natural lengths are re-measured along the new axis and the
            // uniform size of the old axis does not linger as a MinWidth or MinHeight.
            this.ApplyOrientationState();
            this.RebuildItems();
        }

        /// <summary>
        /// The strip's surface while it is disabled, in place of its Background. Also fills the bump buttons and the
        /// overflow button, which sit on the strip. The AJut theme sets the disabled ComboBox's flat fill.
        /// </summary>
        public static readonly DependencyProperty DisabledBackgroundProperty = DPUtils.Register(_ => _.DisabledBackground);
        public Brush DisabledBackground
        {
            get => (Brush)this.GetValue(DisabledBackgroundProperty);
            set => this.SetValue(DisabledBackgroundProperty, value);
        }

        /// <summary>The strip's outline while it is disabled, in place of its BorderBrush.</summary>
        public static readonly DependencyProperty DisabledBorderBrushProperty = DPUtils.Register(_ => _.DisabledBorderBrush);
        public Brush DisabledBorderBrush
        {
            get => (Brush)this.GetValue(DisabledBorderBrushProperty);
            set => this.SetValue(DisabledBorderBrushProperty, value);
        }

        /// <summary>
        /// Every item's text while the strip is disabled, the selected item's too: a disabled strip keeps its selection
        /// shaded so it still shows, and nothing in it reads as live.
        /// </summary>
        public static readonly DependencyProperty DisabledForegroundProperty = DPUtils.Register(_ => _.DisabledForeground);
        public Brush DisabledForeground
        {
            get => (Brush)this.GetValue(DisabledForegroundProperty);
            set => this.SetValue(DisabledForegroundProperty, value);
        }

        // ===========[ Properties ]===============================================
        private bool IsVertical => this.Orientation == Orientation.Vertical;

        // ===========[ Template application ]=====================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_ScrollHost != null)
            {
                this.PART_ScrollHost.RemoveHandler(PointerPressedEvent, m_dragPressedHandler);
                this.PART_ScrollHost.RemoveHandler(PointerMovedEvent, m_dragMovedHandler);
                this.PART_ScrollHost.RemoveHandler(PointerReleasedEvent, m_dragReleasedHandler);
                this.PART_ScrollHost.PointerCaptureLost -= this.OnScrollHostPointerCaptureLost;
            }

            this.PART_ScrollHost = this.GetTemplateChild(nameof(PART_ScrollHost)) as BumpStack;
            if (this.PART_ScrollHost != null)
            {
                // handledEventsToo: the item buttons mark the press and release handled, and a drag starts on one
                this.PART_ScrollHost.AddHandler(PointerPressedEvent, m_dragPressedHandler, true);
                this.PART_ScrollHost.AddHandler(PointerMovedEvent, m_dragMovedHandler, true);
                this.PART_ScrollHost.AddHandler(PointerReleasedEvent, m_dragReleasedHandler, true);
                this.PART_ScrollHost.PointerCaptureLost += this.OnScrollHostPointerCaptureLost;
            }

            this.PART_OverflowButton = this.GetTemplateChild(nameof(PART_OverflowButton)) as Button;

            // PART_ItemsPanel is the BumpStack's single child (its content). It is reparented into the
            // BumpStack's own template, so grab it from the Children collection rather than via
            // GetTemplateChild. Likewise the overflow popup's panel lives inside the button's Flyout,
            // which GetTemplateChild can't reach - grab it from the Flyout content.
            this.PART_ItemsPanel = this.PART_ScrollHost?.Children.Count > 0 ? this.PART_ScrollHost.Children[0] as StackPanel : null;
            this.PART_OverflowPanel = (this.PART_OverflowButton?.Flyout as Flyout)?.Content as StackPanel;

            this.ApplyOrientationState();
            this.ApplyCommonState(false);
            this.RebuildItems();
        }

        private void ApplyCommonState (bool useTransitions)
        {
            VisualStateManager.GoToState(this, this.IsEnabled ? kNormalState : kDisabledState, useTransitions);
            this.ApplyButtonColorState(useTransitions);
            foreach (ToggleStripButton button in m_buttons)
            {
                this.BindItemOutline(button);
            }
        }

        private void ApplyButtonColorState (bool useTransitions)
        {
            string state = !this.IsEnabled ? kDisabledButtonsState : (this.IsVertical ? kPlainButtonsState : kInStripButtonsState);
            VisualStateManager.GoToState(this, state, useTransitions);
        }

        // An item's outline draws the lines between items, so it takes the strip's border: DisabledBorderBrush while the
        // strip is disabled, as the Disabled state gives the frame, and BorderBrush otherwise. It is bound from here rather
        // than set by the state, since the items are made in code and the state can only name parts of the template.
        private void BindItemOutline (ToggleStripButton button)
        {
            button.SetBinding(Control.BorderBrushProperty, new Binding
            {
                Source = this,
                Path = this.IsEnabled ? kBorderBrushPath : kDisabledBorderBrushPath,
                Mode = BindingMode.OneWay,
            });
        }

        // The template turns its BumpStack and overflow button for the orientation; this picks the state. The
        // items panel is turned here rather than by the state: the BumpStack holds it in a plain collection and
        // moves it into the BumpStack's own template (which is also why OnApplyTemplate finds it through Children),
        // so a state setter naming it from this template is not guaranteed to resolve.
        private void ApplyOrientationState ()
        {
            if (this.PART_ItemsPanel != null)
            {
                this.PART_ItemsPanel.Orientation = this.Orientation;
            }

            VisualStateManager.GoToState(this, this.IsVertical ? kVerticalState : kHorizontalState, false);
            this.ApplyButtonColorState(false);
        }

        // ===========[ Events ]===================================================
        private void OnIsEnabledChanged (object sender, DependencyPropertyChangedEventArgs e) => this.ApplyCommonState(true);

        private void OnSizeChanged (object sender, SizeChangedEventArgs e)
        {
            // Re-run in every mode: OverflowPopup repartitions, and Clip/Scroll need the pass too -
            // uniform sizing depends on real measured widths.
            this.UpdateOverflowLayout();
        }

        // Click-and-drag scrolling in Scroll mode, by AJut.UX's shared ToggleStripDragScroll, for the mouse (touch already
        // pans the scroller). Past the threshold the scroll host captures the pointer, which takes it from the item button
        // under it, so that button never clicks; the strip then follows the pointer until the release.
        private void OnScrollHostPointerPressed (object sender, PointerRoutedEventArgs e)
        {
            if ((this.OverflowBehavior == eToggleStripOverflow.Scroll)
                && (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse))
            {
                m_dragScroll.Begin(this.PositionAlongStrip(e.GetCurrentPoint(this.PART_ScrollHost).Position));
            }
        }

        private void OnScrollHostPointerMoved (object sender, PointerRoutedEventArgs e)
        {
            PointerPoint point = e.GetCurrentPoint(this.PART_ScrollHost);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            double delta = m_dragScroll.Move(this.PositionAlongStrip(point.Position));
            if (!m_dragScroll.IsDragging)
            {
                return;
            }

            if (!m_hasDragCapture)
            {
                m_hasDragCapture = this.PART_ScrollHost.CapturePointer(e.Pointer);
            }

            this.PART_ScrollHost.ScrollBy(-delta);
            e.Handled = true;
        }

        private void OnScrollHostPointerReleased (object sender, PointerRoutedEventArgs e)
        {
            if (m_dragScroll.End())
            {
                this.PART_ScrollHost.ReleasePointerCapture(e.Pointer);
                m_hasDragCapture = false;
                e.Handled = true;
            }
        }

        private void OnScrollHostPointerCaptureLost (object sender, PointerRoutedEventArgs e)
        {
            m_dragScroll.End();
            m_hasDragCapture = false;
        }

        private double PositionAlongStrip (Point point) => this.IsVertical ? point.Y : point.X;

        // ===========[ Private handlers ]=========================================
        private void Items_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            this.HasItems = this.Items.Count > 0;
            this.RebuildItems();
        }

        private void OnDisplayPropertyPathChanged (string newPath)
        {
            this.Items.ResetNames();
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

            // 3. Rebuild the items, restoring the previous selection by value (so boxed value types survive the
            //    round-trip), else selecting the first item unless an empty selection is allowed
            this.Items.ResetFromSource(e.NewValue, previousSelectedItem);
        }

        private void ItemsSource_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            this.Items.ApplySourceChange(e);
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

            this.Items.SelectData(e.NewValue);
        }

        private void OnSelectedItemsChanged (DependencyPropertyChangedEventArgs<IList> e)
        {
            // External changes to SelectedItems are not re-processed to avoid cycles.
            // Selection is driven through SelectedItem or by clicking items directly.
        }

        // The items collection worked out the change; this keeps SelectedItem and SelectedItems in step with it and raises
        // SelectionChanged. A select comes with the item that was selected, a standalone deselect without one.
        void IToggleStripHost.OnSelectionChanged (ToggleStripSelectionChange change)
        {
            this.PerformModificationWithBlocker(ref m_isChangingSelectedItem,
                () => this.SelectedItem = change.SelectedItem
            );

            // A SelectedItems list that reports its own changes (a consumer's ObservableCollection, say) is kept and
            // updated in place, so bindings to it hold; any other list is replaced.
            this.PerformModificationWithBlocker(ref m_isChangingSelectedItemsList, () =>
            {
                if (this.SelectedItems is INotifyCollectionChanged)
                {
                    this.SelectedItems.Clear();
                    foreach (object obj in change.CurrentSelection)
                    {
                        this.SelectedItems.Add(obj);
                    }
                }
                else
                {
                    this.SelectedItems = change.CurrentSelection;
                }
            });

            this.RaiseSelectionChanged(change.PreviousSelection, change.CurrentSelection);

            // A newly selected item is kept in view (and closes the overflow popup), so only a select re-lays the strip out.
            if (change.Selected != null)
            {
                this.RefreshForSelection();
            }
        }

        // The one raiser for the public SelectionChanged event, for the select path and the standalone deselect path alike.
        private void RaiseSelectionChanged (IList previousSelection, IList currentSelection)
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
            m_naturalLengths.Clear();

            // 2. Build a button per item and capture a best-effort natural length along the strip up front.
            //    Lengths are needed for uniform sizing and overflow math; they are refined from the real layout
            //    in UpdateOverflowLayout because an off-tree Measure can come back empty before first arrange.
            bool isVertical = this.IsVertical;
            foreach (ToggleItem item in this.Items)
            {
                ToggleStripButton btn = this.CreateItemButton(item);
                btn.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                m_buttons.Add(btn);
                m_naturalLengths.Add(isVertical ? btn.DesiredSize.Height : btn.DesiredSize.Width);
            }

            this.UpdateOverflowLayout();

            // The off-tree Measure above can come back empty before the buttons are first arranged. If
            // so, schedule one more pass after this layout cycle so uniform sizing and overflow can use
            // the real lengths even when nothing triggers a SizeChanged (e.g. toggling a DP at runtime).
            if (m_buttons.Count > 0 && m_naturalLengths.All(w => w <= 0.0))
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
            this.BindItemOutline(btn);

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
                // Measure every natural length again, in the tree, then grow everyone to the longest along the
                // strip when uniform sizing is on.
                bool isVertical = this.IsVertical;
                this.MeasureNaturalLengths(isVertical);
                m_uniformItemLength = m_naturalLengths.Count > 0 ? m_naturalLengths.Max() : 0.0;
                double minLength = this.EnsureUniformSize ? m_uniformItemLength : 0.0;
                foreach (ToggleStripButton btn in m_buttons)
                {
                    if (isVertical)
                    {
                        btn.MinHeight = minLength;
                    }
                    else
                    {
                        btn.MinWidth = minLength;
                    }
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

                // OverflowPopup: nothing to lay out against until we have a real viewport length along the strip.
                double available = isVertical
                    ? (this.PART_ScrollHost?.ActualHeight ?? this.ActualHeight)
                    : (this.PART_ScrollHost?.ActualWidth ?? this.ActualWidth);
                if (available <= 0.0)
                {
                    this.PlaceAllInStrip();
                    return;
                }

                // Partition by available length, keeping selected items visible where possible, and the item just
                // picked (SelectedItem) ahead of the rest, so a pick from the popup comes into the strip.
                double[] lengths = new double[m_buttons.Count];
                bool[] selected = new bool[m_buttons.Count];
                int pickedIndex = -1;
                for (int i = 0; i < m_buttons.Count; ++i)
                {
                    lengths[i] = this.EnsureUniformSize ? m_uniformItemLength : m_naturalLengths[i];
                    selected[i] = i < this.Items.Count && this.Items[i].IsSelected;
                    if (selected[i] && Equals(this.Items[i].Data, this.SelectedItem))
                    {
                        pickedIndex = i;
                    }
                }

                double reserved = this.MeasureOverflowButtonLength();
                ToggleStripOverflowResult result = ToggleStripOverflowCalculator.Compute(lengths, selected, available, reserved, pickedIndex);

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
        // is set the last item also gets a trailing divider (right, or bottom when vertical) to separate
        // it from the overflow chevron, and stays square on that end since the strip's outer border
        // supplies the rounded edge there. The rules are AJut.UX's ToggleStripItemEdges, shared by every stack.
        //
        // A horizontal strip's buttons hug their content left to right; a vertical strip's buttons stretch
        // across the strip's width so the column reads as one control rather than a ragged edge.
        private void ApplyEdgeStyling (IReadOnlyList<ToggleStripButton> orderedButtons, bool trailingSeparator = false)
        {
            bool isVertical = this.IsVertical;
            HorizontalAlignment alignment = isVertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
            for (int i = 0; i < orderedButtons.Count; ++i)
            {
                ToggleStripButton btn = orderedButtons[i];
                ToggleStripItemEdges edges = ToggleStripItemEdges.For(i, orderedButtons.Count, isVertical, trailingSeparator);

                btn.HorizontalAlignment = alignment;
                btn.BorderThickness = EdgeStyling.BorderThicknessFor(edges, this.SeparatorThickness);
                btn.CornerRadius = EdgeStyling.CornerRadiusFor(edges, this.CornerRadius);
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

        // Every pass measures each button's natural length again, inside the strip's items panel. The up-front
        // Measure in RebuildItems runs before the buttons are in the tree, and a button that started in the
        // overflow popup has never been laid out at all, so either can come back short of its real size (seen on a
        // vertical strip: an item picked from the popup came into the strip with only its top third showing). A
        // short length lets the overflow split keep more than the strip can show, and the last one kept is cut
        // off. Measured in the tree, with the uniform minimum along the strip cleared first, the length is the
        // button's own. A measure that still comes back empty (before the first layout) keeps the last length.
        private void MeasureNaturalLengths (bool isVertical)
        {
            this.PART_OverflowPanel?.Children.Clear();
            this.PART_ItemsPanel.Children.Clear();

            var unconstrained = new Size(double.PositiveInfinity, double.PositiveInfinity);
            for (int i = 0; i < m_buttons.Count; ++i)
            {
                ToggleStripButton btn = m_buttons[i];
                if (isVertical)
                {
                    btn.MinHeight = 0.0;
                }
                else
                {
                    btn.MinWidth = 0.0;
                }

                this.PART_ItemsPanel.Children.Add(btn);
                btn.Measure(unconstrained);
                double length = isVertical ? btn.DesiredSize.Height : btn.DesiredSize.Width;
                if (length > 0.0)
                {
                    m_naturalLengths[i] = length;
                }
            }
        }

        private double MeasureOverflowButtonLength ()
        {
            if (this.PART_OverflowButton == null)
            {
                return kFallbackOverflowButtonLength;
            }

            this.PART_OverflowButton.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size desired = this.PART_OverflowButton.DesiredSize;
            double measured = this.IsVertical ? desired.Height : desired.Width;
            return measured > 0.0 ? measured : kFallbackOverflowButtonLength;
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

        // ===========[ IToggleStripHost Implementations ]=========================
        // Items are named through DisplayPropertyPath here; the reflection-free selector is for toolkits that trim.
        Func<object, string> IToggleStripHost.ItemNameSelector => null;

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

        /// <summary>
        /// Turns AJut.UX's <see cref="ToggleStripItemEdges"/> into the WinUI3 values a button takes: a
        /// <see cref="Thickness"/> with the separator on each edge that draws one, and a <see cref="CornerRadius"/>
        /// with the strip's own radius on each corner that rounds.
        /// </summary>
        internal static class EdgeStyling
        {
            /// <summary>The button's BorderThickness: <paramref name="separatorThickness"/> on each separator edge, 0 elsewhere.</summary>
            public static Thickness BorderThicknessFor (ToggleStripItemEdges edges, double separatorThickness)
            {
                return new Thickness(
                    edges.SeparatorLeft ? separatorThickness : 0.0,
                    edges.SeparatorTop ? separatorThickness : 0.0,
                    edges.SeparatorRight ? separatorThickness : 0.0,
                    edges.SeparatorBottom ? separatorThickness : 0.0
                );
            }

            /// <summary>The button's CornerRadius: the matching corner of <paramref name="stripRadius"/> where it rounds, 0 elsewhere.</summary>
            public static CornerRadius CornerRadiusFor (ToggleStripItemEdges edges, CornerRadius stripRadius)
            {
                return new CornerRadius(
                    edges.RoundTopLeft ? stripRadius.TopLeft : 0.0,
                    edges.RoundTopRight ? stripRadius.TopRight : 0.0,
                    edges.RoundBottomRight ? stripRadius.BottomRight : 0.0,
                    edges.RoundBottomLeft ? stripRadius.BottomLeft : 0.0
                );
            }
        }

        /// <summary>One option in the strip. The selection rules and naming live in AJut.UX's <see cref="ToggleStripItem"/>, shared with every toolkit's strip.</summary>
        public class ToggleItem : ToggleStripItem
        {
            internal ToggleItem (ToggleStrip owner, object data, int index)
                : base(owner, data, index)
            {
                this.Owner = owner;
            }

            /// <summary>The strip this item belongs to.</summary>
            public ToggleStrip Owner { get; }
        }

        /// <summary>The strip's items. The selection bookkeeping lives in AJut.UX's <see cref="ToggleStripItemCollection{TItem}"/>, shared with every toolkit's strip.</summary>
        public class ToggleItemsCollection : ToggleStripItemCollection<ToggleItem>
        {
            internal ToggleItemsCollection (ToggleStrip owner)
                : base(owner, (data, index) => new ToggleItem(owner, data, index))
            {
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
