namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics.Drawables;
    using Android.Runtime;
    using Android.Util;
    using Android.Views;
    using Android.Widget;
    using Orientation = Android.Widget.Orientation;

    /// <summary>
    /// A strip of toggle buttons, one per item of <see cref="ItemsSource"/>, left to right or (with <see cref="Orientation"/>)
    /// top to bottom. One item is selected at a time
    /// unless <see cref="AllowMultiSelect"/>, and the last selected item stays selected unless
    /// <see cref="AllowNoSelection"/>. The selection rules are AJut.UX's <see cref="ToggleStripItemCollection{TItem}"/>,
    /// the same ones every AJut toolkit's ToggleStrip runs on; this view draws the items and keeps
    /// <see cref="SelectedItem"/> and <see cref="SelectedItems"/> in step.
    /// </summary>
    /// <remarks>
    /// The strip follows an ItemsSource that raises collection changes while it is attached to a window, and catches up
    /// when it is attached again, so a long-lived source never holds on to a strip that has gone. In a layout,
    /// ajutAllowMultiSelect, ajutAllowNoSelection, ajutEnsureUniformSize, ajutOverflowBehavior and ajutOrientation set the
    /// switches, and ajutItems takes a string array resource as the items.
    /// </remarks>
    [Register("ajut.ux.controls.ToggleStrip")]
    public class ToggleStrip : FrameLayout, IToggleStripHost
    {
        private static readonly ThemedColor kStripBackground = ThemedColor.FromArgb("ControlFillColorDefaultBrush", 0xFFFDFDFD, 0xFF2B2B2B);
        private static readonly ThemedColor kStripBorder = ThemedColor.FromArgb("ControlStrokeColorDefaultBrush", 0xFFD1D1D1, 0xFF454545);
        private static readonly ThemedColor kItemForeground = ThemedColor.FromArgb("TextFillColorPrimaryBrush", 0xE4000000, 0xFFFFFFFF);
        private static readonly ThemedColor kPressedBackground = ThemedColor.FromArgb(AJutThemeKeys.ToggleStripButton_PressedBackground, 0x26000000, 0x40000000);
        private static readonly ThemedColor kPointerOverBackground = ThemedColor.FromArgb(AJutThemeKeys.ToggleStripButton_PointerOverBackground, 0x14000000, 0x20FFFFFF);
        private static readonly ThemedGradient kCheckedBackground = new ThemedGradient(
            AJutThemeKeys.ToggleStripButton_CheckedBackground,
            new[] { ThemedGradient.Stop(0.0, 0x59000000), ThemedGradient.Stop(0.2, 0x38000000), ThemedGradient.Stop(1.0, 0x1A000000) },
            new[] { ThemedGradient.Stop(0.0, 0xCC000000), ThemedGradient.Stop(0.2, 0x80000000), ThemedGradient.Stop(1.0, 0x33000000) }
        );
        private static readonly ThemedGradient kCheckedPressedBackground = new ThemedGradient(
            AJutThemeKeys.ToggleStripButton_CheckedPressedBackground,
            new[] { ThemedGradient.Stop(0.0, 0x73000000), ThemedGradient.Stop(0.2, 0x4D000000), ThemedGradient.Stop(1.0, 0x2E000000) },
            new[] { ThemedGradient.Stop(0.0, 0xEE000000), ThemedGradient.Stop(0.2, 0xAA000000), ThemedGradient.Stop(1.0, 0x66000000) }
        );

        // The Scroll strip's end dots: the AJut symbol color themed, the way the UIKit and AppKit strips alias their
        // scroll indicator key to it, and Android's secondary text color unthemed.
        private static readonly ThemedColor kScrollIndicator = ThemedColor.FromArgb("AJut_Brush_SymbolForeground", 0x8A000000, 0xB3FFFFFF);

        private const float kCornerRadiusDp = 4f;
        private const float kBorderThicknessDp = 1f;
        private const float kItemPaddingHorizontalDp = 12f;
        private const float kItemPaddingVerticalDp = 8f;
        private const float kItemMinHeightDp = 40f;

        private readonly ToggleStripItemCollection m_items;
        private readonly List<ItemButton> m_buttons = new List<ItemButton>();
        private readonly GradientDrawable m_surface = new GradientDrawable();
        private readonly GradientDrawable m_backDot = new GradientDrawable();
        private readonly GradientDrawable m_forwardDot = new GradientDrawable();
        private ViewOverlay? m_dotOverlay;
        private DotPlacement m_backDotPlacement;
        private DotPlacement m_forwardDotPlacement;
        private FrameLayout m_scroll = null!;
        private LinearLayout m_row = null!;
        private TextView m_overflowButton = null!;
        private IEnumerable? m_itemsSource;
        private INotifyCollectionChanged? m_observedSource;
        private object? m_selectedItem;
        private IList m_selectedItems = new List<object>();
        private bool m_allowMultiSelect;
        private bool m_allowNoSelection;
        private bool m_ensureUniformSize;
        private eToggleStripOverflow m_overflowBehavior = eToggleStripOverflow.Clip;
        private Orientation m_orientation = Orientation.Horizontal;
        private string? m_displayPropertyPath;
        private Func<object, string?>? m_itemNameSelector;
        private bool m_isApplyingSelection;
        private bool m_isLayingOutOverflow;
        private float m_density = 1f;

        // ===========[ Setup/Construction/Teardown ]===================================
        public ToggleStrip (Context context) : base(context)
        {
            m_items = new ToggleStripItemCollection(this);
            this.Initialize(context, null);
        }

        public ToggleStrip (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            m_items = new ToggleStripItemCollection(this);
            this.Initialize(context, attrs);
        }

        public ToggleStrip (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            m_items = new ToggleStripItemCollection(this);
            this.Initialize(context, attrs);
        }

        protected ToggleStrip (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
            m_items = new ToggleStripItemCollection(this);
        }

        protected override void OnAttachedToWindow ()
        {
            base.OnAttachedToWindow();
            m_items.CollectionChanged -= this.OnItemsChanged;
            m_items.CollectionChanged += this.OnItemsChanged;
            m_overflowButton.Click -= this.OnOverflowButtonClicked;
            m_overflowButton.Click += this.OnOverflowButtonClicked;

            // Changes the source made while the strip was detached were not heard, so catch up before listening again.
            if (m_observedSource == null && m_itemsSource is INotifyCollectionChanged observable)
            {
                this.RebuildFromSource();
                m_observedSource = observable;
                m_observedSource.CollectionChanged += this.OnItemsSourceCollectionChanged;
            }

            // The items may have changed while the strip was detached and its buttons were not listening.
            this.RebuildButtons();

            // A Scroll strip's end dots sit just past its ends, outside its bounds, where the parent would clip anything
            // the strip drew itself. So they go in the parent's overlay, which draws over the parent's children in the
            // parent's own space, takes no layout space and no input, and is left alone otherwise.
            m_dotOverlay = (this.Parent as ViewGroup)?.Overlay;
            if (m_dotOverlay != null)
            {
                m_dotOverlay.Add(m_backDot);
                m_dotOverlay.Add(m_forwardDot);
                this.UpdateScrollIndicators();
            }
        }

        // A source that outlives the strip must not keep it alive through the strip's subscription, and the buttons let
        // go of their items.
        protected override void OnDetachedFromWindow ()
        {
            if (m_dotOverlay != null)
            {
                m_dotOverlay.Remove(m_backDot);
                m_dotOverlay.Remove(m_forwardDot);
                m_dotOverlay = null;
            }

            this.StopObservingSource();
            m_items.CollectionChanged -= this.OnItemsChanged;
            m_overflowButton.Click -= this.OnOverflowButtonClicked;
            foreach (ItemButton button in m_buttons)
            {
                button.Unbind();
            }

            base.OnDetachedFromWindow();
        }

        // ===========[ Events ]===================================
        /// <summary>Raised once for each change to the selection, after <see cref="SelectedItem"/> and <see cref="SelectedItems"/> have been updated.</summary>
        public event EventHandler<ToggleStripSelectionChange>? SelectionChanged;

        // ===========[ Properties ]===================================
        /// <summary>The items, one toggle button each. An ObservableCollection is followed as it changes.</summary>
        public IEnumerable? ItemsSource
        {
            get => m_itemsSource;
            set
            {
                if (ReferenceEquals(m_itemsSource, value))
                {
                    return;
                }

                this.StopObservingSource();
                m_itemsSource = value;
                this.RebuildFromSource();
                if (this.IsAttachedToWindow && (m_itemsSource is INotifyCollectionChanged observable))
                {
                    m_observedSource = observable;
                    m_observedSource.CollectionChanged += this.OnItemsSourceCollectionChanged;
                }
            }
        }

        /// <summary>The strip's items, with each one's selection. Read-only from outside; set ItemsSource to change them.</summary>
        public IReadOnlyList<ToggleStripItem> Items => m_items;

        /// <summary>The most recently selected item's data. Setting it selects the item with that data, the way a tap would.</summary>
        public object? SelectedItem
        {
            get => m_selectedItem;
            set
            {
                if (m_isApplyingSelection || Equals(m_selectedItem, value))
                {
                    return;
                }

                if (value == null)
                {
                    m_selectedItem = null;
                    m_selectedItems = new List<object>();
                    return;
                }

                m_items.SelectData(value);
            }
        }

        /// <summary>The data of every selected item, in strip order.</summary>
        public IList SelectedItems => m_selectedItems;

        /// <summary>Whether more than one item can be selected at once.</summary>
        public bool AllowMultiSelect
        {
            get => m_allowMultiSelect;
            set => m_allowMultiSelect = value;
        }

        /// <summary>Whether the last selected item can be deselected, leaving nothing selected.</summary>
        public bool AllowNoSelection
        {
            get => m_allowNoSelection;
            set => m_allowNoSelection = value;
        }

        /// <summary>The property of each item to show as its name. Empty shows each item's ToString.</summary>
        public string? DisplayPropertyPath
        {
            get => m_displayPropertyPath;
            set
            {
                m_displayPropertyPath = value;
                m_items.ResetNames();
            }
        }

        /// <summary>Names each item from its data, winning over <see cref="DisplayPropertyPath"/>; needs no reflection, so it suits a trimmed build.</summary>
        public Func<object, string?>? ItemNameSelector
        {
            get => m_itemNameSelector;
            set
            {
                m_itemNameSelector = value;
                m_items.ResetNames();
            }
        }

        /// <summary>
        /// When true every button is as long along the strip as the longest one (as wide as the widest when horizontal,
        /// as tall as the tallest when vertical), so the strip reads as an even row or column.
        /// </summary>
        public bool EnsureUniformSize
        {
            get => m_ensureUniformSize;
            set
            {
                m_ensureUniformSize = value;
                this.RebuildButtons();
            }
        }

        /// <summary>
        /// What the strip does when its buttons do not fit its length (its width, or its height when vertical): clip them
        /// (the default), scroll them, or move the ones that do not fit into a popup behind a trailing button, keeping
        /// selected items in the strip where it can. A strip sized to its content never overflows.
        /// </summary>
        /// <remarks>
        /// Scrolling, the strip shows a small dot just past each end it can still scroll toward, by AJut.UX's
        /// <see cref="ToggleStripScrollIndicators"/>, since it has no scroll bar. The dots draw in the parent's overlay,
        /// about 10dp past the strip's ends, so a parent that ends right at the strip clips them.
        /// </remarks>
        public eToggleStripOverflow OverflowBehavior
        {
            get => m_overflowBehavior;
            set
            {
                m_overflowBehavior = value;
                this.RequestLayout();
                this.UpdateScrollIndicators();
            }
        }

        /// <summary>
        /// Lays the buttons out left to right (Horizontal, the default) or top to bottom (Vertical). A vertical strip
        /// stretches each button across its width, scrolls up and down, and puts the overflow button along its bottom
        /// edge; uniform sizing and overflow work along its height.
        /// </summary>
        public Orientation Orientation
        {
            get => m_orientation;
            set
            {
                if (m_orientation != value)
                {
                    m_orientation = value;
                    this.ApplyOrientation();
                }
            }
        }

        private bool IsVertical => m_orientation == Orientation.Vertical;

        // ===========[ Public Interface Methods ]===================================
        protected override void OnSizeChanged (int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged(w, h, oldw, oldh);
            this.Post(this.ApplyOverflowLayout);
        }

        // A move or a new content length moves or changes the dots
        protected override void OnLayout (bool changed, int left, int top, int right, int bottom)
        {
            base.OnLayout(changed, left, top, right, bottom);
            this.UpdateScrollIndicators();
        }

        protected override void OnVisibilityChanged (View changedView, ViewStates visibility)
        {
            base.OnVisibilityChanged(changedView, visibility);
            this.UpdateScrollIndicators();
        }

        // ===========[ IToggleStripHost Implementations ]===================================
        void IToggleStripHost.OnSelectionChanged (ToggleStripSelectionChange change)
        {
            m_isApplyingSelection = true;
            try
            {
                m_selectedItem = change.SelectedItem;
                m_selectedItems = change.CurrentSelection;
            }
            finally
            {
                m_isApplyingSelection = false;
            }

            this.SelectionChanged?.Invoke(this, change);
            if ((change.Selected != null) && (m_overflowBehavior != eToggleStripOverflow.Clip))
            {
                this.Post(this.ApplyOverflowLayout);
            }
        }

        // ===========[ Helper Methods ]===================================
        private void Initialize (Context context, IAttributeSet? attrs)
        {
            m_density = context.Resources?.DisplayMetrics?.Density ?? 1f;

            // 1. The surface: rounded, bordered, and clipping the buttons to its corners; and the Scroll mode end dots
            m_surface.SetCornerRadius(kCornerRadiusDp * m_density);
            this.Background = m_surface;
            this.ClipToOutline = true;
            m_backDot.SetShape(ShapeType.Oval);
            m_forwardDot.SetShape(ShapeType.Oval);

            // 2. A scroller (enabled only in Scroll mode) around the row of buttons, and the overflow button on top
            m_row = new LinearLayout(context) { Orientation = m_orientation };
            m_row.ShowDividers = ShowDividers.Middle;
            this.BuildScrollHost();

            m_overflowButton = new TextView(context) { Text = "...", Gravity = GravityFlags.Center, Visibility = ViewStates.Gone };
            this.AddView(m_overflowButton);
            this.PlaceOverflowButton();
            this.PaintSurface();

            // 3. Layout attributes
            string[]? layoutItems = null;
            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutToggleStrip);
                m_allowMultiSelect = values.GetBoolean(Resource.Styleable.AJutToggleStrip_ajutAllowMultiSelect, false);
                m_allowNoSelection = values.GetBoolean(Resource.Styleable.AJutToggleStrip_ajutAllowNoSelection, false);
                m_ensureUniformSize = values.GetBoolean(Resource.Styleable.AJutToggleStrip_ajutEnsureUniformSize, false);
                m_overflowBehavior = (eToggleStripOverflow)values.GetInt(Resource.Styleable.AJutToggleStrip_ajutOverflowBehavior, (int)eToggleStripOverflow.Clip);
                this.Orientation = (Orientation)values.GetInt(Resource.Styleable.AJutToggleStrip_ajutOrientation, (int)Orientation.Horizontal);
                if (values.HasValue(Resource.Styleable.AJutToggleStrip_ajutItems))
                {
                    layoutItems = values.GetTextArray(Resource.Styleable.AJutToggleStrip_ajutItems) is { } texts
                        ? Array.ConvertAll(texts, text => text?.ToString() ?? string.Empty)
                        : null;
                }

                values.Recycle();
            }

            if (layoutItems != null)
            {
                this.ItemsSource = layoutItems;
            }
        }

        private void PaintSurface ()
        {
            Context context = this.Context!;
            m_surface.SetColor(ThemeColors.Resolve(context, kStripBackground).ToAndroidColor());
            m_surface.SetStroke((int)Math.Max(1f, kBorderThicknessDp * m_density), ThemeColors.Resolve(context, kStripBorder).ToAndroidColor());

            // The divider is a line across the strip: as thick as the border along the strip, and stretched by the row
            // across it.
            var divider = new GradientDrawable();
            divider.SetColor(ThemeColors.Resolve(context, kStripBorder).ToAndroidColor());
            int thickness = (int)Math.Max(1f, kBorderThicknessDp * m_density);
            if (this.IsVertical)
            {
                divider.SetSize(1, thickness);
            }
            else
            {
                divider.SetSize(thickness, 1);
            }

            m_row.SetDividerDrawable(divider);
            m_overflowButton.SetTextColor(ThemeColors.Resolve(context, kItemForeground).ToAndroidColor());

            // The shared dot opacity applies over the indicator color's own
            ColorArgb indicator = ThemeColors.Resolve(context, kScrollIndicator);
            var dotColor = new ColorArgb(
                (byte)Math.Round(indicator.A * ToggleStripScrollIndicators.kDotOpacity),
                indicator.R, indicator.G, indicator.B
            );
            m_backDot.SetColor(dotColor.ToAndroidColor());
            m_forwardDot.SetColor(dotColor.ToAndroidColor());
        }

        // A new orientation turns the row, swaps in the scroller for that axis, moves the overflow button and resizes the
        // divider, then makes the buttons again so none keeps a size or layout from the old axis.
        private void ApplyOrientation ()
        {
            m_row.Orientation = m_orientation;
            this.BuildScrollHost();
            this.PlaceOverflowButton();
            this.PaintSurface();
            this.RebuildButtons();
        }

        // Android scrolls sideways and up and down with two different views (HorizontalScrollView and ScrollView), so the
        // host around the row is the one for the strip's axis, made again when the orientation changes. The row runs the
        // host's length and fills it across, so a vertical strip's buttons can stretch to its width.
        private void BuildScrollHost ()
        {
            if (m_scroll != null)
            {
                m_scroll.RemoveView(m_row);
                this.RemoveView(m_scroll);
            }

            if (this.IsVertical)
            {
                m_scroll = new VerticalScrollToggleHost(this) { VerticalScrollBarEnabled = false, FillViewport = false };
                m_scroll.AddView(m_row, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
                this.AddView(m_scroll, 0, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
            }
            else
            {
                m_scroll = new ScrollToggleHost(this) { HorizontalScrollBarEnabled = false, FillViewport = false };
                m_scroll.AddView(m_row, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.MatchParent));
                this.AddView(m_scroll, 0, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            }
        }

        // The overflow button sits on the strip's trailing edge: the right end, full height, when horizontal, and along the
        // bottom, full width, when vertical. Its popup menu opens below it either way.
        private void PlaceOverflowButton ()
        {
            int horizontalPadding = (int)(kItemPaddingHorizontalDp * m_density);
            int verticalPadding = (int)(kItemPaddingVerticalDp * m_density);
            if (this.IsVertical)
            {
                m_overflowButton.SetPadding(0, verticalPadding, 0, verticalPadding);
                m_overflowButton.LayoutParameters = new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Bottom);
            }
            else
            {
                m_overflowButton.SetPadding(horizontalPadding, 0, horizontalPadding, 0);
                m_overflowButton.LayoutParameters = new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.MatchParent, GravityFlags.End);
            }
        }

        private void RebuildFromSource ()
        {
            object? previous = m_selectedItem;
            m_selectedItem = null;
            m_selectedItems = new List<object>();
            m_items.ResetFromSource(m_itemsSource, previous);
        }

        private void StopObservingSource ()
        {
            if (m_observedSource != null)
            {
                m_observedSource.CollectionChanged -= this.OnItemsSourceCollectionChanged;
                m_observedSource = null;
            }
        }

        // One button per item, rebuilt whenever the items change. Each button listens to its item, so the old ones are
        // unhooked first.
        private void RebuildButtons ()
        {
            foreach (ItemButton button in m_buttons)
            {
                button.Unbind();
            }

            m_buttons.Clear();
            m_row.RemoveAllViews();
            bool isVertical = this.IsVertical;
            foreach (ToggleStripItem item in m_items)
            {
                var button = new ItemButton(this, item);
                m_buttons.Add(button);
                m_row.AddView(button, isVertical
                    ? new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
                    : new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.MatchParent)
                );
            }

            this.ApplyUniformSize();
            this.Post(this.ApplyOverflowLayout);
        }

        // Uniform sizing works along the strip: every button as wide as the widest when horizontal, as tall as the tallest
        // when vertical. Each starts from its own natural size (no minimum width, the standard minimum height).
        private void ApplyUniformSize ()
        {
            bool isVertical = this.IsVertical;
            int baseMinHeight = (int)(kItemMinHeightDp * m_density);
            int longest = 0;
            foreach (ItemButton button in m_buttons)
            {
                button.SetMinWidth(0);
                button.SetMinHeight(baseMinHeight);
                button.Measure(MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified), MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
                longest = Math.Max(longest, isVertical ? button.MeasuredHeight : button.MeasuredWidth);
            }

            if (m_ensureUniformSize)
            {
                foreach (ItemButton button in m_buttons)
                {
                    if (isVertical)
                    {
                        button.SetMinHeight(longest);
                    }
                    else
                    {
                        button.SetMinWidth(longest);
                    }
                }
            }
        }

        // Clip and Scroll show every button (the scroller only scrolls in Scroll mode); OverflowPopup moves the buttons
        // that do not fit behind the trailing "..." button, through AJut.UX's shared overflow calculator.
        private void ApplyOverflowLayout ()
        {
            if (m_isLayingOutOverflow || (m_row == null))
            {
                return;
            }

            m_isLayingOutOverflow = true;
            try
            {
                ((IScrollToggleHost)m_scroll).IsScrollingEnabled = m_overflowBehavior == eToggleStripOverflow.Scroll;
                foreach (ItemButton button in m_buttons)
                {
                    button.Visibility = ViewStates.Visible;
                }

                // Lengths run along the strip: widths when horizontal, heights when vertical.
                bool isVertical = this.IsVertical;
                int stripLength = isVertical ? this.Height : this.Width;
                m_overflowButton.Visibility = ViewStates.Gone;
                if ((m_overflowBehavior != eToggleStripOverflow.OverflowPopup) || (stripLength <= 0) || (m_buttons.Count == 0))
                {
                    this.ApplyCorners();
                    return;
                }

                // The item just picked (SelectedItem) claims the strip first, so a pick from the menu comes in
                var lengths = new double[m_buttons.Count];
                var selected = new bool[m_buttons.Count];
                int pickedIndex = -1;
                for (int index = 0; index < m_buttons.Count; ++index)
                {
                    m_buttons[index].Measure(MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified), MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
                    lengths[index] = isVertical ? m_buttons[index].MeasuredHeight : m_buttons[index].MeasuredWidth;
                    selected[index] = m_items[index].IsSelected;
                    if (selected[index] && Equals(m_items[index].Data, m_selectedItem))
                    {
                        pickedIndex = index;
                    }
                }

                m_overflowButton.Measure(MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified), MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
                double available = isVertical
                    ? stripLength - this.PaddingTop - this.PaddingBottom
                    : stripLength - this.PaddingLeft - this.PaddingRight;
                double reserved = isVertical ? m_overflowButton.MeasuredHeight : m_overflowButton.MeasuredWidth;
                ToggleStripOverflowResult result = ToggleStripOverflowCalculator.Compute(lengths, selected, available, reserved, pickedIndex);
                foreach (int index in result.OverflowIndices)
                {
                    m_buttons[index].Visibility = ViewStates.Gone;
                }

                m_overflowButton.Visibility = result.HasOverflow ? ViewStates.Visible : ViewStates.Gone;
                this.ApplyCorners();
            }
            finally
            {
                m_isLayingOutOverflow = false;
            }
        }

        // Shows a dot past each end a Scroll strip can still scroll toward, by AJut.UX's shared rule, centered across the
        // strip just outside that end, in the parent's space (the overlay is the parent's). Any other strip shows none.
        private void UpdateScrollIndicators ()
        {
            if ((m_dotOverlay == null) || (m_scroll == null) || (m_row == null))
            {
                return;
            }

            ToggleStripScrollIndicators indicators = default;
            bool isVertical = this.IsVertical;
            if ((m_overflowBehavior == eToggleStripOverflow.Scroll) && (this.Visibility == ViewStates.Visible))
            {
                indicators = isVertical
                    ? ToggleStripScrollIndicators.For(m_scroll.ScrollY, m_row.Height, m_scroll.Height)
                    : ToggleStripScrollIndicators.For(m_scroll.ScrollX, m_row.Width, m_scroll.Width);
            }

            int radius = (int)Math.Round(ToggleStripScrollIndicators.kDotDiameter * m_density / 2.0);
            int offset = (int)Math.Round(ToggleStripScrollIndicators.kDotCenterOffset * m_density);
            int centerX = this.Left + (this.Width / 2);
            int centerY = this.Top + (this.Height / 2);
            DotPlacement back = indicators.ShowBack
                ? new DotPlacement(isVertical ? centerX : this.Left - offset, isVertical ? this.Top - offset : centerY, radius)
                : default;
            DotPlacement forward = indicators.ShowForward
                ? new DotPlacement(isVertical ? centerX : this.Right + offset, isVertical ? this.Bottom + offset : centerY, radius)
                : default;
            _PlaceDot(m_backDot, ref m_backDotPlacement, back);
            _PlaceDot(m_forwardDot, ref m_forwardDotPlacement, forward);
        }

        // This runs on every scroll step, so a dot is only moved when its placement changes. A hidden dot (the default
        // placement) gets empty bounds, which draws nothing. A drawable only invalidates its old bounds when they change,
        // so the new ones are invalidated here too.
        private static void _PlaceDot (GradientDrawable dot, ref DotPlacement current, DotPlacement wanted)
        {
            if (current == wanted)
            {
                return;
            }

            current = wanted;
            dot.SetBounds(
                wanted.CenterX - wanted.Radius, wanted.CenterY - wanted.Radius,
                wanted.CenterX + wanted.Radius, wanted.CenterY + wanted.Radius
            );
            dot.InvalidateSelf();
        }

        // The visible buttons round their outer corners to match the strip, by AJut.UX's shared ToggleStripItemEdges: the
        // first rounds the strip's leading corners, the last its trailing ones unless the overflow button follows it. The
        // separators are the row's dividers, so only the corners are taken from it here.
        private void ApplyCorners ()
        {
            int visibleCount = 0;
            foreach (ItemButton button in m_buttons)
            {
                if (button.Visibility == ViewStates.Visible)
                {
                    ++visibleCount;
                }
            }

            bool isVertical = this.IsVertical;
            bool overflowShown = m_overflowButton.Visibility == ViewStates.Visible;
            int visibleIndex = 0;
            foreach (ItemButton button in m_buttons)
            {
                if (button.Visibility == ViewStates.Visible)
                {
                    button.SetEdges(ToggleStripItemEdges.For(visibleIndex++, visibleCount, isVertical, overflowShown));
                }
                else
                {
                    button.SetEdges(default);
                }
            }
        }

        private void OnItemsChanged (object? sender, NotifyCollectionChangedEventArgs e) => this.RebuildButtons();

        private void OnItemsSourceCollectionChanged (object? sender, NotifyCollectionChangedEventArgs e) => m_items.ApplySourceChange(e);

        private void OnOverflowButtonClicked (object? sender, EventArgs e)
        {
            var menu = new PopupMenu(this.Context!, m_overflowButton);
            for (int index = 0; index < m_buttons.Count; ++index)
            {
                if (m_buttons[index].Visibility != ViewStates.Visible)
                {
                    IMenuItem entry = menu.Menu!.Add(0, index, index, m_items[index].Name ?? string.Empty)!;
                    entry.SetCheckable(true);
                    entry.SetChecked(m_items[index].IsSelected);
                }
            }

            menu.MenuItemClick += this.OnOverflowMenuItemClicked;
            menu.DismissEvent += this.OnOverflowMenuDismissed;
            menu.Show();
        }

        private void OnOverflowMenuItemClicked (object? sender, PopupMenu.MenuItemClickEventArgs e)
        {
            int index = e.Item!.ItemId;
            if ((index >= 0) && (index < m_items.Count))
            {
                m_items[index].IsSelected = !m_items[index].IsSelected;
            }
        }

        private void OnOverflowMenuDismissed (object? sender, PopupMenu.DismissEventArgs e)
        {
            if (sender is PopupMenu menu)
            {
                menu.MenuItemClick -= this.OnOverflowMenuItemClicked;
                menu.DismissEvent -= this.OnOverflowMenuDismissed;
            }
        }

        // ===========[ Subclasses/structs ]===================================
        /// <summary>One item's toggle: shows the item's name, flips its selection on a tap, and paints its state.</summary>
        private sealed class ItemButton : TextView
        {
            private readonly ToggleStrip m_owner;
            private readonly ToggleStripItem m_item;
            private ToggleStripItemEdges m_edges;

            public ItemButton (ToggleStrip owner, ToggleStripItem item) : base(owner.Context)
            {
                m_owner = owner;
                m_item = item;
                float density = owner.m_density;
                this.Gravity = GravityFlags.Center;
                this.SetMinHeight((int)(kItemMinHeightDp * density));
                this.SetPadding((int)(kItemPaddingHorizontalDp * density), (int)(kItemPaddingVerticalDp * density), (int)(kItemPaddingHorizontalDp * density), (int)(kItemPaddingVerticalDp * density));
                this.Clickable = true;
                this.Focusable = true;
                this.Text = item.Name;
                this.SetTextColor(ThemeColors.Resolve(owner.Context!, kItemForeground).ToAndroidColor());
                m_item.PropertyChanged += this.OnItemPropertyChanged;
                this.Click += this.OnClicked;
                this.PaintState();
            }

            // Safe to call more than once: a detach unbinds, and the next rebuild unbinds again before replacing it.
            public void Unbind ()
            {
                m_item.PropertyChanged -= this.OnItemPropertyChanged;
                this.Click -= this.OnClicked;
            }

            public void SetEdges (ToggleStripItemEdges edges)
            {
                if (m_edges != edges)
                {
                    m_edges = edges;
                    this.PaintState();
                }
            }

            protected override void DrawableStateChanged ()
            {
                base.DrawableStateChanged();
                if (m_item != null)
                {
                    this.PaintState();
                }
            }

            private void PaintState ()
            {
                Context context = m_owner.Context!;
                float radius = kCornerRadiusDp * m_owner.m_density;
                // GradientDrawable takes an x and y radius per corner, clockwise from the top left
                float topLeft = m_edges.RoundTopLeft ? radius : 0f;
                float topRight = m_edges.RoundTopRight ? radius : 0f;
                float bottomRight = m_edges.RoundBottomRight ? radius : 0f;
                float bottomLeft = m_edges.RoundBottomLeft ? radius : 0f;
                float[] radii = new[] { topLeft, topLeft, topRight, topRight, bottomRight, bottomRight, bottomLeft, bottomLeft };

                IReadOnlyList<AJutGradientStop> stops;
                if (m_item.IsSelected)
                {
                    stops = ThemeColors.Resolve(context, this.Pressed ? kCheckedPressedBackground : kCheckedBackground);
                }
                else if (this.Pressed)
                {
                    stops = new[] { new AJutGradientStop(0.0, ThemeColors.Resolve(context, kPressedBackground)) };
                }
                else if (this.Hovered)
                {
                    stops = new[] { new AJutGradientStop(0.0, ThemeColors.Resolve(context, kPointerOverBackground)) };
                }
                else
                {
                    stops = new[] { new AJutGradientStop(0.0, new ColorArgb(0, 0, 0, 0)) };
                }

                this.Background = ThemeColors.ToDrawable(stops, radii);
            }

            private void OnItemPropertyChanged (object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(ToggleStripItem.Name))
                {
                    this.Text = m_item.Name;
                }
                else if (e.PropertyName == nameof(ToggleStripItem.IsSelected))
                {
                    this.PaintState();
                }
            }

            // A tap flips the item, the way a toggle button does; the model refuses a deselect it does not allow.
            private void OnClicked (object? sender, EventArgs e) => m_item.IsSelected = !m_item.IsSelected;
        }

        /// <summary>The scroller around the row, one for each axis: it only scrolls when the strip is in Scroll mode, and otherwise just clips.</summary>
        private interface IScrollToggleHost
        {
            bool IsScrollingEnabled { get; set; }
        }

        /// <summary>The horizontal strip's scroller. It tells the strip when it scrolls, for the end dots.</summary>
        private sealed class ScrollToggleHost : HorizontalScrollView, IScrollToggleHost
        {
            private readonly ToggleStrip m_owner;

            public ScrollToggleHost (ToggleStrip owner) : base(owner.Context)
            {
                m_owner = owner;
            }

            public bool IsScrollingEnabled { get; set; }

            public override bool OnInterceptTouchEvent (MotionEvent? ev) => this.IsScrollingEnabled && base.OnInterceptTouchEvent(ev);

            public override bool OnTouchEvent (MotionEvent? e) => this.IsScrollingEnabled && base.OnTouchEvent(e);

            protected override void OnScrollChanged (int l, int t, int oldl, int oldt)
            {
                base.OnScrollChanged(l, t, oldl, oldt);
                m_owner.UpdateScrollIndicators();
            }
        }

        /// <summary>The vertical strip's scroller. It tells the strip when it scrolls, for the end dots.</summary>
        private sealed class VerticalScrollToggleHost : ScrollView, IScrollToggleHost
        {
            private readonly ToggleStrip m_owner;

            public VerticalScrollToggleHost (ToggleStrip owner) : base(owner.Context)
            {
                m_owner = owner;
            }

            public bool IsScrollingEnabled { get; set; }

            public override bool OnInterceptTouchEvent (MotionEvent? ev) => this.IsScrollingEnabled && base.OnInterceptTouchEvent(ev);

            public override bool OnTouchEvent (MotionEvent? e) => this.IsScrollingEnabled && base.OnTouchEvent(e);

            protected override void OnScrollChanged (int l, int t, int oldl, int oldt)
            {
                base.OnScrollChanged(l, t, oldl, oldt);
                m_owner.UpdateScrollIndicators();
            }
        }

        /// <summary>Where one end dot sits, in the parent's space; the default is hidden.</summary>
        private readonly record struct DotPlacement (int CenterX, int CenterY, int Radius);
    }
}
