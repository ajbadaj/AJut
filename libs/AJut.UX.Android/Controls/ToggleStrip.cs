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
    using Android.Graphics;
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
    /// switches, ajutCornerRadius, ajutSeparatorThickness, ajutItemPadding, ajutItemPaddingHorizontal and
    /// ajutItemPaddingVertical the look, android:enabled disables the strip, and ajutItems takes a string array resource
    /// as the items.
    /// </remarks>
    [Register("ajut.ux.controls.ToggleStrip")]
    public class ToggleStrip : FrameLayout, IToggleStripHost
    {
        // The look is MobileLook.ToggleStrip's, which UIKit's ToggleStrip draws too: the keys, sizes and which key each
        // state takes come from there, and only the colors each key falls back to with no theme loaded are Android's own.
        // Unthemed, the strip is WinUI3's stock control fill and stroke, and the state fills WinUI3's unthemed washes.
        private static readonly ThemedGradient kStripBackground = new ThemedGradient(
            MobileLook.ToggleStrip.StripSurface,
            new[] { ThemedGradient.Stop(0.0, 0xFFFDFDFD) },
            new[] { ThemedGradient.Stop(0.0, 0xFF2B2B2B) }
        );
        private static readonly ThemedColor kStripBorder = ThemedColor.FromArgb(MobileLook.ToggleStrip.StripBorder, 0xFFD1D1D1, 0xFF454545);

        // Disabled, the strip takes a disabled ComboBox's flat fill and border. Unthemed, WinUI3's stock disabled control
        // fill, which barely changes the surface, and the same stroke as at rest, so there the dimmed text says it.
        private static readonly ThemedGradient kStripDisabledBackground = new ThemedGradient(
            MobileLook.ToggleStrip.StripDisabledSurface,
            new[] { ThemedGradient.Stop(0.0, 0x4DF9F9F9) },
            new[] { ThemedGradient.Stop(0.0, 0x0BFFFFFF) }
        );
        private static readonly ThemedColor kStripDisabledBorder = ThemedColor.FromArgb(MobileLook.ToggleStrip.StripDisabledBorder, 0xFFD1D1D1, 0xFF454545);

        // Every text an item can take, by the key MobileLook.ToggleStrip.ItemTextFor picks for its state. Unthemed, the
        // disabled text is WinUI3's stock disabled text, the same literal a read-only NumericEditor's number dims to.
        private static readonly Dictionary<string, ThemedColor> kItemTexts = new Dictionary<string, ThemedColor>
        {
            [MobileLook.ToggleStrip.ItemText] = ThemedColor.FromArgb(MobileLook.ToggleStrip.ItemText, 0xE4000000, 0xFFFFFFFF),
            [MobileLook.ToggleStrip.ItemHighlightText] = ThemedColor.FromArgb(MobileLook.ToggleStrip.ItemHighlightText, 0xE4000000, 0xFFFFFFFF),
            [MobileLook.ToggleStrip.ItemDisabledText] = ThemedColor.FromArgb(MobileLook.ToggleStrip.ItemDisabledText, 0x5C000000, 0x5DFFFFFF),
        };

        // Every fill an item can take, by the key MobileLook.ToggleStrip.ItemFill picks for its state
        private static readonly Dictionary<string, ThemedGradient> kItemFills = new Dictionary<string, ThemedGradient>
        {
            [MobileLook.ToggleStrip.ItemPressedFill] = new ThemedGradient(
                MobileLook.ToggleStrip.ItemPressedFill,
                new[] { ThemedGradient.Stop(0.0, 0x26000000) },
                new[] { ThemedGradient.Stop(0.0, 0x40000000) }
            ),
            [MobileLook.ToggleStrip.ItemPointerOverFill] = new ThemedGradient(
                MobileLook.ToggleStrip.ItemPointerOverFill,
                new[] { ThemedGradient.Stop(0.0, 0x14000000) },
                new[] { ThemedGradient.Stop(0.0, 0x20FFFFFF) }
            ),
            [MobileLook.ToggleStrip.ItemSelectedFill] = new ThemedGradient(
                MobileLook.ToggleStrip.ItemSelectedFill,
                new[] { ThemedGradient.Stop(0.0, 0x59000000), ThemedGradient.Stop(0.2, 0x38000000), ThemedGradient.Stop(1.0, 0x1A000000) },
                new[] { ThemedGradient.Stop(0.0, 0xCC000000), ThemedGradient.Stop(0.2, 0x80000000), ThemedGradient.Stop(1.0, 0x33000000) }
            ),
            [MobileLook.ToggleStrip.ItemSelectedPointerOverFill] = new ThemedGradient(
                MobileLook.ToggleStrip.ItemSelectedPointerOverFill,
                new[] { ThemedGradient.Stop(0.0, 0x59000000), ThemedGradient.Stop(0.2, 0x43000000), ThemedGradient.Stop(1.0, 0x26000000) },
                new[] { ThemedGradient.Stop(0.0, 0xCC000000), ThemedGradient.Stop(0.2, 0x99000000), ThemedGradient.Stop(1.0, 0x55000000) }
            ),
            [MobileLook.ToggleStrip.ItemSelectedPressedFill] = new ThemedGradient(
                MobileLook.ToggleStrip.ItemSelectedPressedFill,
                new[] { ThemedGradient.Stop(0.0, 0x73000000), ThemedGradient.Stop(0.2, 0x4D000000), ThemedGradient.Stop(1.0, 0x2E000000) },
                new[] { ThemedGradient.Stop(0.0, 0xEE000000), ThemedGradient.Stop(0.2, 0xAA000000), ThemedGradient.Stop(1.0, 0x66000000) }
            ),
        };

        // The Scroll strip's end dots: the AJut symbol color themed, and Android's secondary text color unthemed.
        private static readonly ThemedColor kScrollIndicator = ThemedColor.FromArgb(MobileLook.ToggleStrip.ScrollIndicator, 0x8A000000, 0xB3FFFFFF);

        private readonly ToggleStripItemCollection m_items;
        private readonly List<ItemButton> m_buttons = new List<ItemButton>();
        private readonly GradientDrawable m_backDot = new GradientDrawable();
        private readonly GradientDrawable m_forwardDot = new GradientDrawable();
        private readonly Rect m_itemPadding = new Rect();
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
        private float m_cornerRadius;
        private int m_separatorThickness;

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

        /// <summary>
        /// Whether the strip takes taps. Disabled, it looks like a disabled ComboBox: the flat disabled fill and its border.
        /// Its item buttons and its overflow button are disabled with it, and their text dims while their fills stay, so the
        /// selection still shows. android:enabled sets it from a layout.
        /// </summary>
        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;

                // A ViewGroup's Enabled does not reach its children, so the buttons are set here, and RebuildButtons
                // makes new ones to match. The overflow button and the row are made in Initialize, so they are not there
                // yet when the base constructor runs, and Initialize paints the surface itself.
                foreach (ItemButton button in m_buttons)
                {
                    button.Enabled = value;
                }

                if (m_overflowButton != null)
                {
                    m_overflowButton.Enabled = value;
                }

                if ((m_row != null) && (m_overflowButton != null))
                {
                    this.PaintSurface();
                }
            }
        }

        /// <summary>
        /// How round the strip's outer corners are, in pixels: the end items round with it and the ones between stay
        /// square. Defaults to 4dp; the ajutCornerRadius attribute sets it from a layout.
        /// </summary>
        public float CornerRadius
        {
            get => m_cornerRadius;
            set
            {
                m_cornerRadius = value;
                this.PaintSurface();
                foreach (ItemButton button in m_buttons)
                {
                    button.Repaint();
                }
            }
        }

        /// <summary>The thickness of the line between items, in pixels. Defaults to 1dp; the ajutSeparatorThickness attribute sets it from a layout.</summary>
        public int SeparatorThickness
        {
            get => m_separatorThickness;
            set
            {
                m_separatorThickness = value;
                this.PaintSurface();
            }
        }

        /// <summary>
        /// The padding inside each item, in pixels, as a Rect's left, top, right and bottom (the way Drawable.GetPadding
        /// hands one back); the overflow button takes the same along the strip. Reading it gives a copy, so set a whole
        /// Rect to change it. Defaults to 12dp across and 8dp up and down; the ajutItemPadding, ajutItemPaddingHorizontal
        /// and ajutItemPaddingVertical attributes set it from a layout.
        /// </summary>
        public Rect ItemPadding
        {
            get => new Rect(m_itemPadding);
            set
            {
                m_itemPadding.Set(value);
                foreach (ItemButton button in m_buttons)
                {
                    button.SetPadding(m_itemPadding.Left, m_itemPadding.Top, m_itemPadding.Right, m_itemPadding.Bottom);
                }

                this.PlaceOverflowButton();
                this.ApplyUniformSize();
                this.Post(this.ApplyOverflowLayout);
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

            // 1. MobileLook's sizes, until the properties or the layout attributes below change them
            m_cornerRadius = MobileLook.ToggleStrip.CornerRadius * m_density;
            m_separatorThickness = (int)Math.Max(1f, MobileLook.ToggleStrip.SeparatorThickness * m_density);
            int horizontalPadding = (int)(MobileLook.ToggleStrip.ItemPaddingHorizontal * m_density);
            int verticalPadding = (int)(MobileLook.ToggleStrip.ItemPaddingVertical * m_density);
            m_itemPadding.Set(horizontalPadding, verticalPadding, horizontalPadding, verticalPadding);

            // 2. The buttons clip to the surface's rounded corners (PaintSurface makes the surface); and the Scroll mode end dots
            this.ClipToOutline = true;
            m_backDot.SetShape(ShapeType.Oval);
            m_forwardDot.SetShape(ShapeType.Oval);

            // 3. A scroller (enabled only in Scroll mode) around the row of buttons, and the overflow button on top
            m_row = new LinearLayout(context) { Orientation = m_orientation };
            m_row.ShowDividers = ShowDividers.Middle;
            this.BuildScrollHost();

            m_overflowButton = new TextView(context) { Text = "...", Gravity = GravityFlags.Center, Visibility = ViewStates.Gone };
            this.AddView(m_overflowButton);
            this.PlaceOverflowButton();
            this.PaintSurface();

            // 4. Layout attributes
            string[]? layoutItems = null;
            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutToggleStrip);
                m_allowMultiSelect = values.GetBoolean(Resource.Styleable.AJutToggleStrip_ajutAllowMultiSelect, false);
                m_allowNoSelection = values.GetBoolean(Resource.Styleable.AJutToggleStrip_ajutAllowNoSelection, false);
                m_ensureUniformSize = values.GetBoolean(Resource.Styleable.AJutToggleStrip_ajutEnsureUniformSize, false);
                m_overflowBehavior = (eToggleStripOverflow)values.GetInt(Resource.Styleable.AJutToggleStrip_ajutOverflowBehavior, (int)eToggleStripOverflow.Clip);
                this.Orientation = (Orientation)values.GetInt(Resource.Styleable.AJutToggleStrip_ajutOrientation, (int)Orientation.Horizontal);

                // Android reads android:enabled in TextView and the widgets built on it, and a ViewGroup ignores it, so the
                // strip reads it itself, the way a widget would
                this.Enabled = values.GetBoolean(Resource.Styleable.AJutToggleStrip_android_enabled, true);
                this.CornerRadius = values.GetDimension(Resource.Styleable.AJutToggleStrip_ajutCornerRadius, m_cornerRadius);
                this.SeparatorThickness = values.GetDimensionPixelSize(Resource.Styleable.AJutToggleStrip_ajutSeparatorThickness, m_separatorThickness);
                this.ItemPadding = ReadItemPadding(
                    values,
                    Resource.Styleable.AJutToggleStrip_ajutItemPadding,
                    Resource.Styleable.AJutToggleStrip_ajutItemPaddingHorizontal,
                    Resource.Styleable.AJutToggleStrip_ajutItemPaddingVertical,
                    m_itemPadding
                );
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

            // The surface is made anew each time, since themed it is a gradient and unthemed a solid color: rounded,
            // and bordered with the outline. Disabled, both are a disabled ComboBox's, and so is the divider.
            ThemedGradient background = this.Enabled ? kStripBackground : kStripDisabledBackground;
            ThemedColor border = this.Enabled ? kStripBorder : kStripDisabledBorder;
            GradientDrawable surface = ThemeColors.ToDrawable(ThemeColors.Resolve(context, background));
            surface.SetCornerRadius(m_cornerRadius);
            surface.SetStroke((int)Math.Max(1f, MobileLook.ToggleStrip.BorderThickness * m_density), ThemeColors.Resolve(context, border).ToAndroidColor());
            this.Background = surface;

            // The divider is a line across the strip: as thick as the border along the strip, and stretched by the row
            // across it.
            var divider = new GradientDrawable();
            divider.SetColor(ThemeColors.Resolve(context, border).ToAndroidColor());
            if (this.IsVertical)
            {
                divider.SetSize(1, m_separatorThickness);
            }
            else
            {
                divider.SetSize(m_separatorThickness, 1);
            }

            m_row.SetDividerDrawable(divider);

            // The overflow button dims with the strip, as the items do
            m_overflowButton.SetTextColor(new ColorStateList(
                new[] { new[] { -Android.Resource.Attribute.StateEnabled }, Array.Empty<int>() },
                new[]
                {
                    ThemeColors.Resolve(context, kItemTexts[MobileLook.ToggleStrip.ItemDisabledText]).ToAndroidColor().ToArgb(),
                    ThemeColors.Resolve(context, kItemTexts[MobileLook.ToggleStrip.ItemText]).ToAndroidColor().ToArgb(),
                }
            ));

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
        // bottom, full width, when vertical. It is padded along the strip the way the items are. Its popup menu opens
        // below it either way.
        private void PlaceOverflowButton ()
        {
            if (this.IsVertical)
            {
                m_overflowButton.SetPadding(0, m_itemPadding.Top, 0, m_itemPadding.Bottom);
                m_overflowButton.LayoutParameters = new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Bottom);
            }
            else
            {
                m_overflowButton.SetPadding(m_itemPadding.Left, 0, m_itemPadding.Right, 0);
                m_overflowButton.LayoutParameters = new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.MatchParent, GravityFlags.End);
            }
        }

        /// <summary>
        /// The item padding a layout gives, over <paramref name="fallback"/>: ajutItemPadding sets all four sides, then
        /// ajutItemPaddingHorizontal and ajutItemPaddingVertical one pair each, the way android:padding and
        /// paddingHorizontal do. The indices are the styleable's being read, so EnumToggleStrip reads the same three.
        /// </summary>
        internal static Rect ReadItemPadding (TypedArray values, int allIndex, int horizontalIndex, int verticalIndex, Rect fallback)
        {
            var padding = new Rect(fallback);
            if (values.HasValue(allIndex))
            {
                int all = values.GetDimensionPixelSize(allIndex, 0);
                padding.Set(all, all, all, all);
            }

            if (values.HasValue(horizontalIndex))
            {
                int horizontal = values.GetDimensionPixelSize(horizontalIndex, 0);
                padding.Left = horizontal;
                padding.Right = horizontal;
            }

            if (values.HasValue(verticalIndex))
            {
                int vertical = values.GetDimensionPixelSize(verticalIndex, 0);
                padding.Top = vertical;
                padding.Bottom = vertical;
            }

            return padding;
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
                var button = new ItemButton(this, item) { Enabled = this.Enabled };
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
            int baseMinHeight = (int)(MobileLook.ToggleStrip.ItemMinHeight * m_density);
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
                Rect padding = owner.m_itemPadding;
                this.Gravity = GravityFlags.Center;
                this.SetMinHeight((int)(MobileLook.ToggleStrip.ItemMinHeight * owner.m_density));
                this.SetPadding(padding.Left, padding.Top, padding.Right, padding.Bottom);
                this.Clickable = true;
                this.Focusable = true;
                this.Text = item.Name;
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

            /// <summary>Paints the item again, for a change to the strip's look (its corner radius).</summary>
            public void Repaint () => this.PaintState();

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
                float radius = m_owner.m_cornerRadius;
                // GradientDrawable takes an x and y radius per corner, clockwise from the top left
                float topLeft = m_edges.RoundTopLeft ? radius : 0f;
                float topRight = m_edges.RoundTopRight ? radius : 0f;
                float bottomRight = m_edges.RoundBottomRight ? radius : 0f;
                float bottomLeft = m_edges.RoundBottomLeft ? radius : 0f;
                float[] radii = new[] { topLeft, topLeft, topRight, topRight, bottomRight, bottomRight, bottomLeft, bottomLeft };

                // Which fill and text the item's state takes is MobileLook's call, shared with UIKit
                string? fillKey = MobileLook.ToggleStrip.ItemFill(m_item.IsSelected, this.Pressed, this.Hovered);
                IReadOnlyList<AJutGradientStop> stops = (fillKey != null)
                    ? ThemeColors.Resolve(context, kItemFills[fillKey])
                    : new[] { new AJutGradientStop(0.0, new ColorArgb(0, 0, 0, 0)) };

                this.Background = ThemeColors.ToDrawable(stops, radii);
                string textKey = MobileLook.ToggleStrip.ItemTextFor(this.Enabled, this.Pressed, this.Hovered);
                this.SetTextColor(ThemeColors.Resolve(context, kItemTexts[textKey]).ToAndroidColor());
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
