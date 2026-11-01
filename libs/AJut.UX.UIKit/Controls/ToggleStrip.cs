namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using CoreAnimation;
    using CoreGraphics;
    using ObjCRuntime;
    using UIKit;

    // ===========[ ToggleStrip ]================================================
    // UIKit toggle strip: a row of joined toggle buttons for a handful of mutually exclusive (or, with
    // AllowMultiSelect, combinable) options. The items and every selection rule (the single-select swap, the
    // "keep one selected" veto, one change report per user action) live in AJut.UX's ToggleStripItemCollection,
    // the same as on WinUI3, WPF, AppKit and Android; this control draws the items and reports the changes.
    //
    // It draws buttons rather than wrapping UISegmentedControl, because a segmented control can only ever have one
    // segment selected, and AllowMultiSelect needs more.
    //
    // Items come from ItemsSource (an INotifyCollectionChanged source is followed as it changes) or are added to
    // Items by hand. SelectionChanged reports every change, including one made by setting SelectedItem or ItemsSource,
    // as on WinUI3. ValueChanged is sent only for a change the user made, the UIKit convention the BindingSet adapters
    // rely on.
    //
    // Orientation lays the strip out horizontally (the default) or vertically, top to bottom. A vertical strip stretches
    // its buttons across its width. Everything measured along the strip (the overflow split, EnsureUniformSize) follows
    // the orientation, and each button's separators and rounded corners come from AJut.UX's ToggleStripItemEdges, the
    // same rules every stack uses.
    //
    // OverflowBehavior picks what happens when the items do not fit: Clip, Scroll (the strip scrolls along its length,
    // with a dot just outside each end it can still scroll toward), or OverflowPopup (the items that do not fit move into
    // a menu on a "more" button that takes the rest of the strip, its chevron at the trailing end, the right or the foot,
    // laid out by AJut.UX's ToggleStripOverflowCalculator, which keeps selected items in the strip where it can).
    //
    // Enabled reaches every button, which a UIControl's own Enabled does not: disabled, the strip takes a disabled
    // ComboBox's flat fill and border, the buttons take no touches and their text dims, while their fills stay so the
    // selection still shows.

    public class ToggleStrip : UIControl, IToggleStripHost
    {
        /// <summary>The least the overflow button takes along the strip, and what the overflow split saves for it.</summary>
        private const float kOverflowLength = 36f;

        // The look is MobileLook.ToggleStrip's, which Android's ToggleStrip draws too: the keys, sizes and which key each
        // state takes come from there, and only the unthemed defaults are UIKit's own. With the AJut theme loaded the
        // strip is on the pop-out surface and its buttons are clear over it; unthemed the surface stays off and each
        // button keeps the system toggle background, so the strip sits among stock UIKit controls.
        private static readonly ThemedGradient kSurface = new ThemedGradient(
            MobileLook.ToggleStrip.StripSurface,
            new[] { ThemedGradient.Stop(0.0, 0xFFFDFDFD), ThemedGradient.Stop(1.0, 0xFFFDFDFD) },
            new[] { ThemedGradient.Stop(0.0, 0xFF2B2B2B), ThemedGradient.Stop(1.0, 0xFF2B2B2B) }
        );

        // Disabled, the flat fill a disabled ComboBox takes. Unthemed the surface is off, so these only stand in.
        private static readonly ThemedGradient kDisabledSurface = new ThemedGradient(
            MobileLook.ToggleStrip.StripDisabledSurface,
            new[] { ThemedGradient.Stop(0.0, 0xFFFDFDFD), ThemedGradient.Stop(1.0, 0xFFFDFDFD) },
            new[] { ThemedGradient.Stop(0.0, 0xFF2B2B2B), ThemedGradient.Stop(1.0, 0xFF2B2B2B) }
        );

        // ===========[ Instance Fields ]===================================
        private readonly CAGradientLayer m_surface = new CAGradientLayer
        {
            StartPoint = new CGPoint(0.5, 0.0),
            EndPoint = new CGPoint(0.5, 1.0),
            ZPosition = -1,
            MasksToBounds = true,
        };
        private readonly UIScrollView m_scroller = new UIScrollView
        {
            ShowsHorizontalScrollIndicator = false,
            ShowsVerticalScrollIndicator = false,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };
        private readonly UIStackView m_row = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Alignment = UIStackViewAlignment.Fill,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };
        private readonly UIStackView m_outer = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Alignment = UIStackViewAlignment.Fill,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };
        private readonly OverflowButton m_overflowButton = new OverflowButton();
        private readonly UIView m_scrollBackIndicator = new UIView { UserInteractionEnabled = false, Hidden = true };
        private readonly UIView m_scrollForwardIndicator = new UIView { UserInteractionEnabled = false, Hidden = true };
        private NSLayoutConstraint[] m_horizontalConstraints;
        private NSLayoutConstraint[] m_verticalConstraints;
        private UILayoutConstraintAxis m_orientation = UILayoutConstraintAxis.Horizontal;
        private readonly List<ToggleStripButton> m_buttons = new List<ToggleStripButton>();
        private IEnumerable m_itemsSource;
        private INotifyCollectionChanged m_trackedSource;
        private object m_selectedItem;
        private IList m_selectedItems = Array.Empty<object>();
        private bool m_allowMultiSelect;
        private bool m_allowNoSelection;
        private string m_displayPropertyPath;
        private Func<object, string> m_itemNameSelector;
        private nfloat m_separatorThickness = MobileLook.ToggleStrip.SeparatorThickness;
        private NSDirectionalEdgeInsets m_itemPadding = new NSDirectionalEdgeInsets(
            MobileLook.ToggleStrip.ItemPaddingVertical,
            MobileLook.ToggleStrip.ItemPaddingHorizontal,
            MobileLook.ToggleStrip.ItemPaddingVertical,
            MobileLook.ToggleStrip.ItemPaddingHorizontal
        );
        private bool m_ensureUniformSize;
        private eToggleStripOverflow m_overflowBehavior = eToggleStripOverflow.Clip;
        private nfloat m_cornerRadius = MobileLook.ToggleStrip.CornerRadius;
        private bool m_isApplyingFromCode;
        private nfloat m_lastOverflowLength = -1f;

        // ===========[ Setup/Construction/Teardown ]===================================
        public ToggleStrip () : base(CGRect.Empty)
        {
            this.Items = new ToggleStripItemCollection(this);
            this.Setup();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native control.</summary>
        protected ToggleStrip (NativeHandle handle) : base(handle)
        {
            this.Items = new ToggleStripItemCollection(this);
            this.Setup();
        }

        private void Setup ()
        {
            // The strip doesn't clip, so a Scroll strip's dots can sit just outside its ends. The stack holding the
            // buttons clips them to the rounded border instead, and the border is drawn over them. The surface clips
            // itself to the same rounding, and sits under everything (a negative zPosition).
            this.ClipsToBounds = false;
            this.Layer.BorderWidth = MobileLook.ToggleStrip.BorderThickness;
            this.Layer.CornerRadius = m_cornerRadius;
            m_surface.CornerRadius = m_cornerRadius;
            m_surface.Hidden = !ThemedColors.IsThemeLoaded;
            this.Layer.InsertSublayer(m_surface, 0);
            m_outer.ClipsToBounds = true;
            m_outer.Layer.CornerRadius = m_cornerRadius;

            m_scroller.AddSubview(m_row);
            m_outer.AddArrangedSubview(m_scroller);
            m_outer.AddArrangedSubview(m_overflowButton);
            this.AddSubview(m_outer);

            // The dots are in front of everything and take no touches, and take no layout space either: they are placed
            // by hand, past the strip's bounds, so the strip doesn't move as they come and go
            UIColor indicatorColor = ThemedColors.Get(MobileLook.ToggleStrip.ScrollIndicator);
            foreach (UIView indicator in new[] { m_scrollBackIndicator, m_scrollForwardIndicator })
            {
                indicator.BackgroundColor = indicatorColor;
                indicator.Alpha = (nfloat)ToggleStripScrollIndicators.kDotOpacity;
                indicator.Frame = new CGRect(0, 0, ToggleStripScrollIndicators.kDotDiameter, ToggleStripScrollIndicators.kDotDiameter);
                indicator.Layer.CornerRadius = (nfloat)(ToggleStripScrollIndicators.kDotDiameter / 2.0);
                this.AddSubview(indicator);
            }

            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_outer.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_outer.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                m_outer.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_outer.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_row.LeadingAnchor.ConstraintEqualTo(m_scroller.ContentLayoutGuide.LeadingAnchor),
                m_row.TrailingAnchor.ConstraintEqualTo(m_scroller.ContentLayoutGuide.TrailingAnchor),
                m_row.TopAnchor.ConstraintEqualTo(m_scroller.ContentLayoutGuide.TopAnchor),
                m_row.BottomAnchor.ConstraintEqualTo(m_scroller.ContentLayoutGuide.BottomAnchor),
            });

            // Across the strip the row fills the scroller, which is what stretches a vertical strip's buttons across its
            // width. Along it, the scroller takes the row's natural length when there is room and scrolls (or clips) when
            // there is not. The overflow button takes at least kOverflowLength at the trailing end, and everything past
            // the items showing: it hugs its content less than anything else in the stack, so the room the scroller's
            // natural length leaves over goes to it, and the strip reads as one piece out to its edge.
            m_horizontalConstraints = new[]
            {
                m_row.HeightAnchor.ConstraintEqualTo(m_scroller.FrameLayoutGuide.HeightAnchor),
                _Natural(m_scroller.WidthAnchor.ConstraintEqualTo(m_row.WidthAnchor)),
                _NearlyRequired(m_overflowButton.WidthAnchor.ConstraintGreaterThanOrEqualTo(kOverflowLength)),
            };
            m_verticalConstraints = new[]
            {
                m_row.WidthAnchor.ConstraintEqualTo(m_scroller.FrameLayoutGuide.WidthAnchor),
                _Natural(m_scroller.HeightAnchor.ConstraintEqualTo(m_row.HeightAnchor)),
                _NearlyRequired(m_overflowButton.HeightAnchor.ConstraintGreaterThanOrEqualTo(kOverflowLength)),
            };

            this.Items.CollectionChanged += this.Items_OnCollectionChanged;
            this.ApplyOrientation();
            this.ApplyOverflowBehavior();

            static NSLayoutConstraint _Natural (NSLayoutConstraint constraint)
            {
                constraint.Priority = (float)UILayoutPriority.DefaultLow;
                return constraint;
            }

            // Just under required, so it gives way to the stack's own zero length for the button while it is hidden
            static NSLayoutConstraint _NearlyRequired (NSLayoutConstraint constraint)
            {
                constraint.Priority = (float)UILayoutPriority.Required - 1f;
                return constraint;
            }
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                this.Items.CollectionChanged -= this.Items_OnCollectionChanged;
                m_scroller.Scrolled -= this.Scroller_OnScrolled;
                this.UntrackSource();
                foreach (ToggleStripButton button in m_buttons)
                {
                    button.Detach();
                }
            }

            base.Dispose(disposing);
        }

        public override void MovedToWindow ()
        {
            base.MovedToWindow();

            // A long lived items source keeps every strip subscribed to it alive, so the strip only follows the source
            // while it is on screen, and catches up on anything it missed when it comes back.
            //
            // The scroller is a subview, so the strip keeps it alive natively, and the strip's handler held by the
            // scroller would keep the strip alive from the managed side: a cycle across native objects the GC can't
            // see. So the scroller is only followed while the strip is on screen too.
            if (this.Window == null)
            {
                this.UntrackSource();
                m_scroller.Scrolled -= this.Scroller_OnScrolled;
                return;
            }

            m_scroller.Scrolled -= this.Scroller_OnScrolled;
            m_scroller.Scrolled += this.Scroller_OnScrolled;
            if (m_trackedSource == null && m_itemsSource is INotifyCollectionChanged)
            {
                this.TrackSource();
                this.ResetItemsFromSource();
            }
        }

        // ===========[ Events ]===================================
        /// <summary>Raised once for every change to the selection, from the user or from code.</summary>
        public event EventHandler<ToggleStripSelectionChangedEventArgs> SelectionChanged;

        private void Items_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            this.RebuildButtons();
        }

        private void ItemsSource_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            this.Items.ApplySourceChange(e);
        }

        private void Scroller_OnScrolled (object sender, EventArgs e)
        {
            this.UpdateScrollIndicators();
        }

        // ===========[ Properties ]===================================
        /// <summary>The strip's items and its selection rules. Add to it by hand, or let <see cref="ItemsSource"/> fill it.</summary>
        public ToggleStripItemCollection Items { get; }

        /// <summary>The options to show, one button each. An INotifyCollectionChanged source is followed as it changes.</summary>
        public IEnumerable ItemsSource
        {
            get => m_itemsSource;
            set
            {
                this.UntrackSource();
                m_itemsSource = value;
                if (this.Window != null)
                {
                    this.TrackSource();
                }

                this.ResetItemsFromSource();
            }
        }

        /// <summary>The data of the selected item (the most recently selected one, under multi-select). Setting it selects the matching item.</summary>
        public object SelectedItem
        {
            get => m_selectedItem;
            set
            {
                m_selectedItem = value;
                if (value != null)
                {
                    m_isApplyingFromCode = true;
                    try
                    {
                        this.Items.SelectData(value);
                    }
                    finally
                    {
                        m_isApplyingFromCode = false;
                    }
                }
            }
        }

        /// <summary>The data of every selected item, in strip order.</summary>
        public IList SelectedItems => m_selectedItems;

        public bool AllowMultiSelect
        {
            get => m_allowMultiSelect;
            set => m_allowMultiSelect = value;
        }

        public bool AllowNoSelection
        {
            get => m_allowNoSelection;
            set => m_allowNoSelection = value;
        }

        /// <summary>The property of each item's data to show as its name; empty names items by their data's ToString.</summary>
        public string DisplayPropertyPath
        {
            get => m_displayPropertyPath;
            set
            {
                m_displayPropertyPath = value;
                this.Items.ResetNames();
            }
        }

        /// <summary>
        /// Names an item from its data, and wins over <see cref="DisplayPropertyPath"/>. It needs no reflection, so it
        /// keeps working under the trimmer that iOS release builds always run.
        /// </summary>
        public Func<object, string> ItemNameSelector
        {
            get => m_itemNameSelector;
            set
            {
                m_itemNameSelector = value;
                this.Items.ResetNames();
            }
        }

        /// <summary>
        /// Whether the strip runs left to right (Horizontal, the default) or top to bottom (Vertical). A vertical strip
        /// stretches its buttons across its width, and its overflow button sits at its foot.
        /// </summary>
        public UILayoutConstraintAxis Orientation
        {
            get => m_orientation;
            set
            {
                m_orientation = value;
                this.ApplyOrientation();
            }
        }

        /// <summary>The thickness of the line between buttons (default 1).</summary>
        public nfloat SeparatorThickness
        {
            get => m_separatorThickness;
            set
            {
                m_separatorThickness = value;
                this.ApplyItemEdges();
            }
        }

        /// <summary>The padding inside each button.</summary>
        public NSDirectionalEdgeInsets ItemPadding
        {
            get => m_itemPadding;
            set
            {
                m_itemPadding = value;
                foreach (ToggleStripButton button in m_buttons)
                {
                    button.SetPadding(value);
                }
            }
        }

        /// <summary>Makes every button as long along the strip as the longest one: as wide when horizontal, as tall when vertical.</summary>
        public bool EnsureUniformSize
        {
            get => m_ensureUniformSize;
            set
            {
                m_ensureUniformSize = value;
                m_row.Distribution = value ? UIStackViewDistribution.FillEqually : UIStackViewDistribution.Fill;

                // The overflow split measures by it, so it is worked out again
                m_lastOverflowLength = -1f;
                this.SetNeedsLayout();
            }
        }

        /// <summary>What happens when the items do not fit: Clip (the default), Scroll, or OverflowPopup.</summary>
        public eToggleStripOverflow OverflowBehavior
        {
            get => m_overflowBehavior;
            set
            {
                m_overflowBehavior = value;
                this.ApplyOverflowBehavior();
            }
        }

        /// <summary>The rounding of the strip's outer corners; inner buttons stay square.</summary>
        public nfloat CornerRadius
        {
            get => m_cornerRadius;
            set
            {
                m_cornerRadius = value;
                this.Layer.CornerRadius = value;
                m_surface.CornerRadius = value;
                m_outer.Layer.CornerRadius = value;
                this.ApplyItemEdges();
            }
        }

        public bool HasItems => this.Items.Count > 0;

        /// <summary>
        /// Whether the strip takes touches. Disabled, it looks like a disabled ComboBox: the flat disabled fill and its
        /// border. Its buttons and its overflow button are disabled with it, and their text dims while their fills stay,
        /// so the selection still shows.
        /// </summary>
        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                // A UIControl's Enabled does not reach its subviews, so the buttons are set here, and RebuildButtons makes
                // new ones to match. The surface, the outline and the separators are worked out in layout, so each is
                // laid out again.
                base.Enabled = value;
                foreach (ToggleStripButton button in m_buttons)
                {
                    button.Enabled = value;
                    button.SetNeedsLayout();
                }

                m_overflowButton.Enabled = value;
                this.SetNeedsLayout();
            }
        }

        private bool IsVertical => m_orientation == UILayoutConstraintAxis.Vertical;

        // ===========[ Public Interface Methods ]===================================
        public override void LayoutSubviews ()
        {
            base.LayoutSubviews();

            // A layer border is a CGColor, which freezes a dynamic color, so it is resolved here: UIKit calls
            // LayoutSubviews again whenever the appearance (light/dark) changes.
            this.Layer.BorderColor = ThemedColors.Get(MobileLook.ToggleStrip.StripBorderFor(this.Enabled)).GetResolvedColor(this.TraitCollection).CGColor;

            // The surface's colors are CGColors as well, so they are worked out here for the same reason
            IReadOnlyList<AJutGradientStop> stops = (this.Enabled ? kSurface : kDisabledSurface).Resolve(this.TraitCollection.UserInterfaceStyle);
            var colors = new CGColor[stops.Count];
            var locations = new Foundation.NSNumber[stops.Count];
            for (int index = 0; index < stops.Count; ++index)
            {
                colors[index] = stops[index].Color.ToUIColor().CGColor;
                locations[index] = new Foundation.NSNumber(stops[index].Offset);
            }

            CATransaction.Begin();
            CATransaction.DisableActions = true;
            m_surface.Frame = this.Bounds;
            m_surface.Colors = colors;
            m_surface.Locations = locations;
            CATransaction.Commit();

            nfloat length = this.IsVertical ? this.Bounds.Height : this.Bounds.Width;
            if ((m_overflowBehavior == eToggleStripOverflow.OverflowPopup) && (length != m_lastOverflowLength))
            {
                m_lastOverflowLength = length;
                this.UpdateOverflowLayout();
            }

            this.UpdateScrollIndicators();
        }

        // ===========[ IToggleStripHost Implementations ]===================================
        void IToggleStripHost.OnSelectionChanged (ToggleStripSelectionChange change)
        {
            m_selectedItem = change.SelectedItem;
            m_selectedItems = change.CurrentSelection;

            this.SelectionChanged?.Invoke(this, new ToggleStripSelectionChangedEventArgs(change.PreviousSelection, change.CurrentSelection));
            if (!m_isApplyingFromCode)
            {
                this.SendActionForControlEvents(UIControlEvent.ValueChanged);
            }

            // A newly selected item is kept in the strip, so a select re-lays out the overflow
            if (change.Selected != null && m_overflowBehavior == eToggleStripOverflow.OverflowPopup)
            {
                this.UpdateOverflowLayout();
            }
        }

        // ===========[ Helper Methods ]===================================
        // Rebuilds the items from ItemsSource, keeping the current selection where it still exists. It is a change from
        // code, so the selection change it causes is reported through SelectionChanged but not as ValueChanged.
        private void ResetItemsFromSource ()
        {
            m_isApplyingFromCode = true;
            try
            {
                this.Items.ResetFromSource(m_itemsSource, m_selectedItem);
            }
            finally
            {
                m_isApplyingFromCode = false;
            }
        }

        private void TrackSource ()
        {
            if (m_itemsSource is INotifyCollectionChanged source)
            {
                m_trackedSource = source;
                m_trackedSource.CollectionChanged -= this.ItemsSource_OnCollectionChanged;
                m_trackedSource.CollectionChanged += this.ItemsSource_OnCollectionChanged;
            }
        }

        private void UntrackSource ()
        {
            if (m_trackedSource != null)
            {
                m_trackedSource.CollectionChanged -= this.ItemsSource_OnCollectionChanged;
                m_trackedSource = null;
            }
        }

        private void RebuildButtons ()
        {
            foreach (ToggleStripButton button in m_buttons)
            {
                button.Detach();
                button.RemoveFromSuperview();
            }

            m_buttons.Clear();
            foreach (ToggleStripItem item in this.Items)
            {
                var button = new ToggleStripButton(item, m_itemPadding) { Enabled = this.Enabled };
                m_buttons.Add(button);
                m_row.AddArrangedSubview(button);
            }

            this.ApplyItemEdges();
            m_lastOverflowLength = -1f;
            this.SetNeedsLayout();
        }

        private void ApplyOrientation ()
        {
            bool isVertical = this.IsVertical;
            NSLayoutConstraint.DeactivateConstraints(isVertical ? m_horizontalConstraints : m_verticalConstraints);
            NSLayoutConstraint.ActivateConstraints(isVertical ? m_verticalConstraints : m_horizontalConstraints);
            m_row.Axis = m_orientation;
            m_outer.Axis = m_orientation;
            m_overflowButton.SetIsVertical(isVertical);

            this.ApplyItemEdges();
            m_lastOverflowLength = -1f;
            this.SetNeedsLayout();
        }

        private void ApplyOverflowBehavior ()
        {
            m_scroller.ScrollEnabled = m_overflowBehavior == eToggleStripOverflow.Scroll;
            if (m_overflowBehavior != eToggleStripOverflow.OverflowPopup)
            {
                m_overflowButton.Hidden = true;
                foreach (ToggleStripButton button in m_buttons)
                {
                    button.Hidden = false;
                }

                this.ApplyItemEdges();
            }

            m_lastOverflowLength = -1f;
            this.SetNeedsLayout();
        }

        // Gives each button showing its separators and rounded corners by its place among the buttons showing, through
        // AJut.UX's ToggleStripItemEdges, so they read as one control the same way on every stack. A hidden (overflowed)
        // button does not count, and the last one showing draws a trailing separator when the overflow button follows it.
        private void ApplyItemEdges ()
        {
            var showing = new List<ToggleStripButton>(m_buttons.Count);
            foreach (ToggleStripButton button in m_buttons)
            {
                if (!button.Hidden)
                {
                    showing.Add(button);
                }
            }

            bool isVertical = this.IsVertical;
            bool overflowShown = !m_overflowButton.Hidden;
            for (int index = 0; index < showing.Count; ++index)
            {
                showing[index].ApplyEdges(ToggleStripItemEdges.For(index, showing.Count, isVertical, overflowShown), m_separatorThickness, m_cornerRadius);
            }
        }

        // A Scroll strip shows a dot just outside each end it can still scroll toward, and none at an end it has reached,
        // by AJut.UX's ToggleStripScrollIndicators, the same rule and sizes as every stack
        private void UpdateScrollIndicators ()
        {
            if (m_overflowBehavior != eToggleStripOverflow.Scroll)
            {
                m_scrollBackIndicator.Hidden = true;
                m_scrollForwardIndicator.Hidden = true;
                return;
            }

            // The scroller's length and content size only settle once it has laid itself out
            m_outer.LayoutIfNeeded();
            m_scroller.LayoutIfNeeded();

            bool isVertical = this.IsVertical;
            ToggleStripScrollIndicators indicators = ToggleStripScrollIndicators.For(
                isVertical ? (double)m_scroller.ContentOffset.Y : (double)m_scroller.ContentOffset.X,
                isVertical ? (double)m_scroller.ContentSize.Height : (double)m_scroller.ContentSize.Width,
                isVertical ? (double)m_scroller.Bounds.Height : (double)m_scroller.Bounds.Width
            );

            m_scrollBackIndicator.Hidden = !indicators.ShowBack;
            m_scrollForwardIndicator.Hidden = !indicators.ShowForward;

            CGRect bounds = this.Bounds;
            double reach = ToggleStripScrollIndicators.kDotCenterOffset;
            m_scrollBackIndicator.Center = isVertical
                ? new CGPoint((double)bounds.GetMidX(), (double)bounds.GetMinY() - reach)
                : new CGPoint((double)bounds.GetMinX() - reach, (double)bounds.GetMidY());
            m_scrollForwardIndicator.Center = isVertical
                ? new CGPoint((double)bounds.GetMidX(), (double)bounds.GetMaxY() + reach)
                : new CGPoint((double)bounds.GetMaxX() + reach, (double)bounds.GetMidY());
        }

        private void UpdateOverflowLayout ()
        {
            // Every length is measured along the strip: widths when horizontal, heights when vertical
            bool isVertical = this.IsVertical;
            var lengths = new double[m_buttons.Count];
            var selected = new bool[m_buttons.Count];
            double longest = 0.0;
            int pickedIndex = -1;
            for (int index = 0; index < m_buttons.Count; ++index)
            {
                CGSize natural = m_buttons[index].IntrinsicContentSize;
                lengths[index] = isVertical ? natural.Height : natural.Width;
                longest = Math.Max(longest, lengths[index]);

                ToggleStripItem item = m_buttons[index].Item;
                selected[index] = item?.IsSelected ?? false;

                // The item just picked (SelectedItem) claims the strip first, so a pick from the menu comes into the strip
                if (selected[index] && Equals(item.Data, m_selectedItem))
                {
                    pickedIndex = index;
                }
            }

            // FillEqually gives every button showing the longest one's length, so that is what each one takes up
            if (m_ensureUniformSize)
            {
                for (int index = 0; index < lengths.Length; ++index)
                {
                    lengths[index] = longest;
                }
            }

            double available = isVertical ? this.Bounds.Height : this.Bounds.Width;
            ToggleStripOverflowResult result = ToggleStripOverflowCalculator.Compute(lengths, selected, available, kOverflowLength, pickedIndex);
            var overflowActions = new List<UIMenuElement>();
            for (int index = 0; index < m_buttons.Count; ++index)
            {
                m_buttons[index].Hidden = false;
            }

            foreach (int overflowIndex in result.OverflowIndices)
            {
                ToggleStripButton button = m_buttons[overflowIndex];
                button.Hidden = true;
                if (button.Item != null)
                {
                    overflowActions.Add(OverflowTarget.CreateAction(button.Item));
                }
            }

            // The menu opens from the overflow button, which takes the rest of the strip past the items showing. UIKit
            // places a button's menu below the button when there is room, which is where every stack opens it.
            m_overflowButton.Hidden = !result.HasOverflow;
            m_overflowButton.Menu = result.HasOverflow ? UIMenu.Create(overflowActions.ToArray()) : null;
            this.ApplyItemEdges();
        }

        // ===========[ Subclasses/structs ]===================================
        /// <summary>The change a <see cref="SelectionChanged"/> reports: the data selected before it, and after it, in strip order.</summary>
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

        // The face every button in the strip shares, the toggle buttons and the overflow button alike, so the strip reads
        // as one piece out to its edge. It paints the way WinUI3's ToggleStripButton does: a base, with an overlay for the
        // state on top. With the AJut theme the base is clear, so the strip's pop-out surface shows through; unthemed it is
        // the system toggle background. The overlay is the one MobileLook.ToggleStrip.ItemFill picks for the state:
        // nothing at rest, the primary highlight's fills pointer over or pressed, a selected button's checked gradients.
        // It is a CAGradientLayer under the content, as WinUI3 draws its state backgrounds under the ContentPresenter.
        private abstract class StripFaceButton : UIButton
        {
            // Every overlay a button can take, by the key MobileLook.ToggleStrip.ItemFill picks for its state. The unthemed
            // ones are WinUI3's defaults (AJutCustomControlStylingDefaults.xaml), with the light set of each checked
            // gradient lightened, the same as the Android strip, so dark text stays readable on a light background.
            private static readonly Dictionary<string, ThemedGradient> kOverlays = new Dictionary<string, ThemedGradient>
            {
                [MobileLook.ToggleStrip.ItemPointerOverFill] = Uniform(MobileLook.ToggleStrip.ItemPointerOverFill, 0x20FFFFFF, 0x20FFFFFF),
                [MobileLook.ToggleStrip.ItemPressedFill] = Uniform(MobileLook.ToggleStrip.ItemPressedFill, 0x40000000, 0x40000000),
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

            private readonly CAGradientLayer m_overlay = new CAGradientLayer
            {
                StartPoint = new CGPoint(0.5, 0.0),
                EndPoint = new CGPoint(0.5, 1.0),
            };

            protected StripFaceButton () : base(CGRect.Empty)
            {
                this.ClipsToBounds = true;
                this.BackgroundColor = ThemedColors.IsThemeLoaded ? UIColor.Clear : ThemedColors.Get(ThemedColorKeys.ToggleButtonBackground);

                // Index 0 alone does not keep the overlay under the content: a configuration based button adds its
                // content view (the title or image) later, at index 0 too. A negative zPosition draws the overlay behind
                // every sibling layer whatever order they arrive in, and still over the button's own background.
                m_overlay.ZPosition = -1;
                this.Layer.InsertSublayer(m_overlay, 0);
            }

            public override void UpdateConfiguration ()
            {
                // The configuration only carries the content and its color; the background is the button's own color
                // plus the overlay layer, which LayoutSubviews paints for the state. base.UpdateConfiguration is skipped
                // so the system does not add its own highlight on top. The foreground stays the same checked or not, as
                // on WinUI3, since the checked overlay darkens rather than replaces the background; over the pressed and
                // pointer-over fills it is the highlight text that reads on them, and disabled it is the disabled text.
                UIButtonConfiguration configuration = this.Configuration ?? UIButtonConfiguration.PlainButtonConfiguration;
                UIBackgroundConfiguration background = configuration.Background;
                background.CornerRadius = 0;
                background.BackgroundColor = UIColor.Clear;
                configuration.Background = background;
                configuration.BaseForegroundColor = ThemedColors.Get(MobileLook.ToggleStrip.ItemTextFor(this.Enabled, this.Highlighted, this.Hovered));
                this.UpdateContent(configuration);
                this.Configuration = configuration;
                this.SetNeedsLayout();
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews();

                // The overlay's colors are CGColors, which a light/dark switch does not reach on its own, so they are
                // worked out here: UIKit calls LayoutSubviews again on an appearance change.
                ThemedGradient overlay = this.PickOverlay();
                CATransaction.Begin();
                CATransaction.DisableActions = true;
                m_overlay.Frame = this.Bounds;
                m_overlay.Hidden = overlay == null;
                if (overlay != null)
                {
                    IReadOnlyList<AJutGradientStop> stops = overlay.Resolve(this.TraitCollection.UserInterfaceStyle);
                    var colors = new CGColor[stops.Count];
                    var locations = new Foundation.NSNumber[stops.Count];
                    for (int index = 0; index < stops.Count; ++index)
                    {
                        colors[index] = stops[index].Color.ToUIColor().CGColor;
                        locations[index] = new Foundation.NSNumber(stops[index].Offset);
                    }

                    m_overlay.Colors = colors;
                    m_overlay.Locations = locations;
                }

                CATransaction.Commit();
            }

            /// <summary>Whether the button shows as selected; only a toggle button can be.</summary>
            protected virtual bool IsSelectedForLook => false;

            /// <summary>The overlay for the button's state, as MobileLook picks it, or null for none.</summary>
            private ThemedGradient PickOverlay ()
            {
                string key = MobileLook.ToggleStrip.ItemFill(this.IsSelectedForLook, this.Highlighted, this.Hovered);
                return (key != null) ? kOverlays[key] : null;
            }

            /// <summary>Puts the button's own content into the configuration it is about to take; nothing by default.</summary>
            protected virtual void UpdateContent (UIButtonConfiguration configuration) { }

            private static ThemedGradient Uniform (string key, uint light, uint dark)
            {
                return new ThemedGradient(
                    key,
                    new[] { ThemedGradient.Stop(0.0, light), ThemedGradient.Stop(1.0, light) },
                    new[] { ThemedGradient.Stop(0.0, dark), ThemedGradient.Stop(1.0, dark) }
                );
            }
        }

        // One toggle button, bound to its item: a tap flips the item's IsSelected (the collection applies the rules),
        // and the item's changes (selection, name) repaint the button. It unhooks from the item when the strip rebuilds
        // or goes away.
        //
        // The button holds its item weakly. The strip keeps the button alive natively (it is a subview), which makes
        // the button's managed peer a GC root, and an item holds its strip (its Host); a strong button -> item link would
        // close that loop and the strip could never be collected. The strip's Items collection is what keeps the item.
        //
        // Checked, its overlay is the AJut_ToggleStripButton_Checked* gradient, top to bottom, and it draws its share of
        // the separators between buttons as lines over its edges.
        private sealed class ToggleStripButton : StripFaceButton
        {
            private readonly WeakReference<ToggleStripItem> m_item;
            private readonly CALayer m_leadingSeparator = new CALayer { Hidden = true };
            private readonly CALayer m_trailingSeparator = new CALayer { Hidden = true };
            private ToggleStripItemEdges m_edges;
            private nfloat m_separatorThickness;

            public ToggleStripButton (ToggleStripItem item, NSDirectionalEdgeInsets padding)
            {
                m_item = new WeakReference<ToggleStripItem>(item);

                // One line, never wrapped. A button title wraps by default, and UIStackView answers wrappable text in a
                // Fill row with its own "text width disambiguation" constraints (priority 950 and up, each button at
                // most 1/n of the row). Those beat the buttons' own sizes, so every button came out the same width and
                // the overflow layout, which measures each button's real width, kept items that then got clipped.
                UIButtonConfiguration configuration = UIButtonConfiguration.PlainButtonConfiguration;
                configuration.TitleLineBreakMode = UILineBreakMode.TailTruncation;
                this.Configuration = configuration;
                this.SetPadding(padding);
                this.HeightAnchor.ConstraintGreaterThanOrEqualTo(MobileLook.ToggleStrip.ItemMinHeight).Active = true;
                this.Layer.AddSublayer(m_leadingSeparator);
                this.Layer.AddSublayer(m_trailingSeparator);
                this.TouchUpInside += this.OnTouchUpInside;
                item.PropertyChanged += this.Item_OnPropertyChanged;
                this.Refresh();
            }

            /// <summary>The item this button shows; null once the strip has let it go.</summary>
            public ToggleStripItem Item => m_item.TryGetTarget(out ToggleStripItem item) ? item : null;

            public void Detach ()
            {
                this.TouchUpInside -= this.OnTouchUpInside;
                ToggleStripItem item = this.Item;
                if (item != null)
                {
                    item.PropertyChanged -= this.Item_OnPropertyChanged;
                }
            }

            public void SetPadding (NSDirectionalEdgeInsets padding)
            {
                UIButtonConfiguration configuration = this.Configuration ?? UIButtonConfiguration.PlainButtonConfiguration;
                configuration.ContentInsets = padding;
                this.Configuration = configuration;
            }

            /// <summary>Sets which edges draw a separator and which corners round, from the button's place in the strip.</summary>
            public void ApplyEdges (ToggleStripItemEdges edges, nfloat separatorThickness, nfloat cornerRadius)
            {
                m_edges = edges;
                m_separatorThickness = separatorThickness;

                // UIKit's y runs down, so MinY is the top: a top left corner is MinXMinY
                CACornerMask corners = 0;
                if (edges.RoundTopLeft)
                {
                    corners |= CACornerMask.MinXMinYCorner;
                }

                if (edges.RoundTopRight)
                {
                    corners |= CACornerMask.MaxXMinYCorner;
                }

                if (edges.RoundBottomRight)
                {
                    corners |= CACornerMask.MaxXMaxYCorner;
                }

                if (edges.RoundBottomLeft)
                {
                    corners |= CACornerMask.MinXMaxYCorner;
                }

                this.Layer.CornerRadius = cornerRadius;
                this.Layer.MaskedCorners = corners;
                this.SetNeedsLayout();
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews();

                // A separator is a line along one edge, laid over the button. Horizontal strips separate on the left and
                // right edges, vertical ones on the top and bottom; ToggleStripItemEdges says which this button draws.
                // Its color is a CGColor, resolved here for the same reason as the overlay's.
                CATransaction.Begin();
                CATransaction.DisableActions = true;
                CGRect bounds = this.Bounds;
                nfloat thickness = m_separatorThickness;
                CGColor separatorColor = ThemedColors.Get(MobileLook.ToggleStrip.StripBorderFor(this.Enabled)).GetResolvedColor(this.TraitCollection).CGColor;
                m_leadingSeparator.Hidden = !(m_edges.SeparatorLeft || m_edges.SeparatorTop);
                m_leadingSeparator.BackgroundColor = separatorColor;
                m_leadingSeparator.Frame = m_edges.SeparatorTop
                    ? new CGRect(0, 0, bounds.Width, thickness)
                    : new CGRect(0, 0, thickness, bounds.Height);
                m_trailingSeparator.Hidden = !(m_edges.SeparatorRight || m_edges.SeparatorBottom);
                m_trailingSeparator.BackgroundColor = separatorColor;
                m_trailingSeparator.Frame = m_edges.SeparatorBottom
                    ? new CGRect(0, bounds.Height - thickness, bounds.Width, thickness)
                    : new CGRect(bounds.Width - thickness, 0, thickness, bounds.Height);

                CATransaction.Commit();
            }

            // Selected, the checked gradients; unselected, the strip's washes, or nothing at rest (WinUI3's near clear #01000000)
            protected override bool IsSelectedForLook => this.Item?.IsSelected ?? false;

            protected override void UpdateContent (UIButtonConfiguration configuration)
            {
                configuration.Title = this.Item?.Name ?? string.Empty;
            }

            private void Refresh ()
            {
                bool isSelected = this.Item?.IsSelected ?? false;
                this.AccessibilityTraits = isSelected ? (UIAccessibilityTrait.Button | UIAccessibilityTrait.Selected) : UIAccessibilityTrait.Button;
                this.SetNeedsUpdateConfiguration();
            }

            private void OnTouchUpInside (object sender, EventArgs e)
            {
                ToggleStripItem item = this.Item;
                if (item != null)
                {
                    item.IsSelected = !item.IsSelected;
                }
            }

            private void Item_OnPropertyChanged (object sender, PropertyChangedEventArgs e)
            {
                this.Refresh();
            }
        }

        // The overflow ("more") button. It takes all of the strip past the last item showing, with the items' face and
        // their pointer over and pressed looks, and its chevron at the trailing end (the foot, on a vertical strip), so
        // the whole of that space is the button and the strip reads as one piece out to its edge.
        private sealed class OverflowButton : StripFaceButton
        {
            public OverflowButton ()
            {
                const float kGlyphInset = 10f;
                const float kHuggingPriority = 1f;

                UIButtonConfiguration configuration = UIButtonConfiguration.PlainButtonConfiguration;
                configuration.Image = UIImage.GetSystemImage("chevron.down");
                configuration.ContentInsets = new NSDirectionalEdgeInsets(kGlyphInset, kGlyphInset, kGlyphInset, kGlyphInset);
                this.Configuration = configuration;
                this.ShowsMenuAsPrimaryAction = true;
                this.AccessibilityLabel = "More options";
                this.Hidden = true;
                this.TranslatesAutoresizingMaskIntoConstraints = false;

                // It hugs its content less than anything else in the strip, so the strip's spare length goes to it
                this.SetContentHuggingPriority(kHuggingPriority, UILayoutConstraintAxis.Horizontal);
                this.SetContentHuggingPriority(kHuggingPriority, UILayoutConstraintAxis.Vertical);
                this.SetIsVertical(false);
            }

            /// <summary>Puts the chevron at the trailing end of a horizontal strip, or the foot of a vertical one.</summary>
            public void SetIsVertical (bool isVertical)
            {
                this.HorizontalAlignment = isVertical ? UIControlContentHorizontalAlignment.Center : UIControlContentHorizontalAlignment.Trailing;
                this.VerticalAlignment = isVertical ? UIControlContentVerticalAlignment.Bottom : UIControlContentVerticalAlignment.Center;
            }
        }

        // The overflow menu's actions are held by the menu, which the overflow button holds natively, and an item holds
        // its strip, so each action holds its item weakly, for the same reason the toggle buttons do.
        private sealed class OverflowTarget
        {
            private readonly WeakReference<ToggleStripItem> m_item;

            private OverflowTarget (ToggleStripItem item)
            {
                m_item = new WeakReference<ToggleStripItem>(item);
            }

            public static UIAction CreateAction (ToggleStripItem item)
            {
                UIAction action = UIAction.Create(item.Name ?? string.Empty, null, null, new OverflowTarget(item).Run);
                action.State = item.IsSelected ? UIMenuElementState.On : UIMenuElementState.Off;
                return action;
            }

            private void Run (UIAction action)
            {
                if (m_item.TryGetTarget(out ToggleStripItem item))
                {
                    item.IsSelected = !item.IsSelected;
                }
            }
        }
    }
}
