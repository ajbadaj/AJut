namespace AJut.UX.Controls
{
    using System;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using Foundation;
    using ObjCRuntime;

    // ===========[ BumpStack ]==================================================
    // A scroll container with a chevron "bump" button at each end instead of a scroll bar, the AppKit port of WinUI3's
    // BumpStack. The buttons show only while the content runs past the stack, each disables at its end, and holding one
    // keeps bumping. While they show, the content is inset by a button's length (plus EdgeClearanceOffset) at both ends,
    // so scrolling all the way parks a button over empty space rather than over content. When, what is enabled, and how
    // far a bump goes are AJut.UX's BumpStackRules, which WinUI3's BumpStack follows too.
    //
    // The content is a FillStack in a scroll view with no scrollers. A child sits at its own size along the stack and is
    // stretched across it, so give each child a size of its own along the stack (an intrinsic size, or a constraint): one
    // with none takes no room. The buttons sit over the scroll view at its two ends.
    //
    // A mouse wheel only scrolls vertically, so on a horizontal stack a mostly vertical scroll is turned into a sideways
    // one, the way AppKit's ToggleStrip does in its Scroll mode. A trackpad swipe along the stack scrolls it as normal.
    //
    // Lifetime: the scroll view and the buttons are retained by the stack natively, so each holds the stack weakly and
    //  asks for it when it needs it. The stack's handler on its own Children collection is a self-subscription.
    /// <summary>
    /// A stack that scrolls along one axis with a bump button at each end instead of a scroll bar. The buttons appear
    /// only while the content overflows, disable at each end, and repeat while held.
    /// </summary>
    public class BumpStack : ThemedView
    {
        private const double kBorderThickness = 1.0;

        private readonly BumpScrollView m_scrollView;
        private readonly FillStack m_content;
        private readonly BumpButton m_backButton;
        private readonly BumpButton m_forwardButton;
        private NSLayoutConstraint[] m_orientationConstraints = Array.Empty<NSLayoutConstraint>();
        private NSUserInterfaceLayoutOrientation m_orientation = NSUserInterfaceLayoutOrientation.Horizontal;
        private bool m_scrollingEnabled = true;
        private double m_edgeClearanceOffset;
        private double m_buttonFontSize = 12.0;
        private NSEdgeInsets m_buttonPadding = new NSEdgeInsets(0, 4, 0, 4);
        private int m_bumpInterval = 50;
        private int m_bumpDelay = 300;
        private string m_buttonBackgroundColorKey = ThemedColorKeys.BumpStack_ButtonBackground;
        private string m_buttonForegroundColorKey = ThemedColorKeys.BumpStack_ButtonForeground;
        private string m_buttonBorderColorKey = ThemedColorKeys.BumpStack_ButtonBorder;
        private double m_currentClearance;
        private double m_contentLengthWithoutClearance;
        private double m_contentThickness;

        // ===========[ Setup/Construction/Teardown ]===========================
        public BumpStack ()
        {
            // 1. The content, in a scroll view with no scrollers: the bump buttons are the affordance
            m_content = new FillStack(m_orientation);
            m_content.TranslatesAutoresizingMaskIntoConstraints = false;
            m_scrollView = new BumpScrollView(this)
            {
                HasHorizontalScroller = false,
                HasVerticalScroller = false,
                DrawsBackground = false,
                TranslatesAutoresizingMaskIntoConstraints = false,
                DocumentView = m_content,
            };
            this.AddSubview(m_scrollView);

            // 2. The bump buttons, over the scroll view's two ends
            m_backButton = new BumpButton(this, isForward: false);
            m_forwardButton = new BumpButton(this, isForward: true);
            this.AddSubview(m_backButton);
            this.AddSubview(m_forwardButton);

            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_scrollView.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_scrollView.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_scrollView.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_scrollView.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                m_content.TopAnchor.ConstraintEqualTo(m_scrollView.ContentView.TopAnchor),
                m_content.LeadingAnchor.ConstraintEqualTo(m_scrollView.ContentView.LeadingAnchor),
            });

            this.Children = new ObservableCollection<NSView>();
            this.Children.CollectionChanged += this.OnChildrenChanged;

            this.ApplyOrientation();
            this.ApplyButtonTiming();
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native stack whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls: the native stack keeps the subviews it already has.
        /// None of its managed state survives a dispose, so a call that needs that state can still fail: dispose it only
        /// once AppKit is done with it.
        /// </summary>
        protected BumpStack (NativeHandle handle) : base(handle)
        {
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing && (this.Children != null))
            {
                this.Children.CollectionChanged -= this.OnChildrenChanged;
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        private void OnChildrenChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    int insertAt = e.NewStartingIndex;
                    foreach (NSView added in e.NewItems)
                    {
                        m_content.Insert(insertAt++, added);
                    }

                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (NSView removed in e.OldItems)
                    {
                        m_content.Remove(removed);
                    }

                    break;

                default:
                    this.SyncChildren();
                    break;
            }

            this.InvalidateIntrinsicContentSize();
            this.NeedsLayout = true;
        }

        // ===========[ Properties ]============================================
        /// <summary>
        /// The views the stack holds, in order. Each sits at its own size along the stack and is stretched across it, so
        /// each needs a size of its own along the stack.
        /// </summary>
        public ObservableCollection<NSView> Children { get; }

        /// <summary>Which axis the stack runs and scrolls along: Horizontal (the default) or Vertical</summary>
        public NSUserInterfaceLayoutOrientation Orientation
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
        /// When false the content is only clipped: it doesn't scroll, and no bump buttons appear. For a host that uses the
        /// stack as a clip container in its non-scrolling states.
        /// </summary>
        public bool ScrollingEnabled
        {
            get => m_scrollingEnabled;
            set
            {
                if (m_scrollingEnabled != value)
                {
                    m_scrollingEnabled = value;
                    this.RefreshBumpButtons();
                }
            }
        }

        /// <summary>How far one bump scrolls, in points</summary>
        public double BumpAmount { get; set; } = 60.0;

        /// <summary>How often a held bump button bumps again once it repeats, in milliseconds</summary>
        public int BumpInterval
        {
            get => m_bumpInterval;
            set
            {
                m_bumpInterval = value;
                this.ApplyButtonTiming();
            }
        }

        /// <summary>How long a held bump button waits after its first bump before it repeats, in milliseconds</summary>
        public int BumpDelay
        {
            get => m_bumpDelay;
            set
            {
                m_bumpDelay = value;
                this.ApplyButtonTiming();
            }
        }

        /// <summary>Extra space, beyond a button's length, kept clear at each end of the content while the buttons show</summary>
        public double EdgeClearanceOffset
        {
            get => m_edgeClearanceOffset;
            set
            {
                m_edgeClearanceOffset = value;
                this.RefreshBumpButtons();
            }
        }

        /// <summary>The <see cref="ThemedColors"/> key the bump buttons fill with</summary>
        public string ButtonBackgroundColorKey
        {
            get => m_buttonBackgroundColorKey;
            set
            {
                m_buttonBackgroundColorKey = value;
                this.RedrawButtons();
            }
        }

        /// <summary>The <see cref="ThemedColors"/> key the bump buttons' chevrons are drawn in</summary>
        public string ButtonForegroundColorKey
        {
            get => m_buttonForegroundColorKey;
            set
            {
                m_buttonForegroundColorKey = value;
                this.RedrawButtons();
            }
        }

        /// <summary>The <see cref="ThemedColors"/> key the bump buttons are outlined in, so they stand apart from what scrolls under them</summary>
        public string ButtonBorderColorKey
        {
            get => m_buttonBorderColorKey;
            set
            {
                m_buttonBorderColorKey = value;
                this.RedrawButtons();
            }
        }

        /// <summary>The size of the bump buttons' chevrons, in points: the glyph's font size on the other stacks</summary>
        public double ButtonFontSize
        {
            get => m_buttonFontSize;
            set
            {
                m_buttonFontSize = value;
                this.ApplyOrientation();
            }
        }

        /// <summary>The space around each bump button's chevron</summary>
        public NSEdgeInsets ButtonPadding
        {
            get => m_buttonPadding;
            set
            {
                m_buttonPadding = value;
                this.ApplyOrientation();
            }
        }

        private bool IsHorizontal => m_orientation == NSUserInterfaceLayoutOrientation.Horizontal;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Scrolls so the first child matching <paramref name="predicate"/> is brought fully into view, kept clear of the
        /// bump buttons. Returns true if a child matched.
        /// </summary>
        public bool ScrollFirstElementIntoView (Func<NSView, bool> predicate)
        {
            if (m_scrollView == null)
            {
                return false;
            }

            foreach (NSView child in this.Children)
            {
                if (!predicate(child))
                {
                    continue;
                }

                CGRect visible = m_scrollView.ContentView.Bounds;
                CGRect frame = child.Frame;
                double? offset = this.IsHorizontal
                    ? ScrollIntoViewMath.ResolveAxisOffset((double)(frame.X - visible.X), (double)frame.Width, (double)visible.X, (double)visible.Width, this.ScrollableLength(), m_currentClearance, m_currentClearance)
                    : ScrollIntoViewMath.ResolveAxisOffset((double)(frame.Y - visible.Y), (double)frame.Height, (double)visible.Y, (double)visible.Height, this.ScrollableLength(), m_currentClearance, m_currentClearance);

                if (offset.HasValue)
                {
                    this.ScrollTo(offset.Value);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Scrolls by <paramref name="delta"/> points along the stack, kept inside the scrollable range: for a host
        /// following a drag. Does nothing while scrolling is off.
        /// </summary>
        public void ScrollBy (double delta)
        {
            if (m_scrollingEnabled)
            {
                this.Bump(delta);
            }
        }

        // Across the stack it is as thick as its thickest child, as WinUI3's sizes to its content, so a host that sizes to
        //  content gives it room. Along the stack it has no size of its own: it is meant to be narrower than its content.
        //  The thickness is measured in Layout, never here: AppKit asks for this inside its constraint pass, where asking
        //  for a fitting size is not safe.
        public override CGSize IntrinsicContentSize
        {
            get
            {
                if (m_content == null)
                {
                    return base.IntrinsicContentSize;
                }

                return this.IsHorizontal
                    ? new CGSize(NSView.NoIntrinsicMetric, (nfloat)m_contentThickness)
                    : new CGSize((nfloat)m_contentThickness, NSView.NoIntrinsicMetric);
            }
        }

        public override void Layout ()
        {
            base.Layout();
            if (m_content == null)
            {
                return;
            }

            this.MeasureContent();
            this.RefreshBumpButtons();
        }

        // ===========[ Helper Methods ]========================================
        private void SyncChildren ()
        {
            m_content.Clear();
            foreach (NSView child in this.Children)
            {
                m_content.Add(child);
            }
        }

        // Turns the content, the scroll view's pinning and the buttons for the orientation
        private void ApplyOrientation ()
        {
            if (m_content == null)
            {
                return;
            }

            m_content.Orientation = m_orientation;
            m_backButton.IsHorizontal = this.IsHorizontal;
            m_forwardButton.IsHorizontal = this.IsHorizontal;

            // Across the stack the content follows the visible area; along it the content is as long as its children
            NSLayoutConstraint.DeactivateConstraints(m_orientationConstraints);
            NSClipView clip = m_scrollView.ContentView;
            double buttonLength = this.ButtonLength();
            m_orientationConstraints = this.IsHorizontal
                ? new[]
                {
                    m_content.HeightAnchor.ConstraintEqualTo(clip.HeightAnchor),
                    m_backButton.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                    m_backButton.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                    m_backButton.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                    m_backButton.WidthAnchor.ConstraintEqualTo((nfloat)buttonLength),
                    m_forwardButton.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                    m_forwardButton.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                    m_forwardButton.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                    m_forwardButton.WidthAnchor.ConstraintEqualTo((nfloat)buttonLength),
                }
                : new[]
                {
                    m_content.WidthAnchor.ConstraintEqualTo(clip.WidthAnchor),
                    m_backButton.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                    m_backButton.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                    m_backButton.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                    m_backButton.HeightAnchor.ConstraintEqualTo((nfloat)buttonLength),
                    m_forwardButton.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                    m_forwardButton.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                    m_forwardButton.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                    m_forwardButton.HeightAnchor.ConstraintEqualTo((nfloat)buttonLength),
                };

            NSLayoutConstraint.ActivateConstraints(m_orientationConstraints);

            // A bounce on the axis that doesn't scroll would only show the content shaking
            m_scrollView.HorizontalScrollElasticity = this.IsHorizontal ? NSScrollElasticity.Automatic : NSScrollElasticity.None;
            m_scrollView.VerticalScrollElasticity = this.IsHorizontal ? NSScrollElasticity.None : NSScrollElasticity.Automatic;
            m_scrollView.IsHorizontal = this.IsHorizontal;

            // The content is measured along the other axis now, which the next layout does
            m_currentClearance = -1.0;
            this.InvalidateIntrinsicContentSize();
            this.NeedsLayout = true;
            this.RefreshBumpButtons();
        }

        private void ApplyButtonTiming ()
        {
            if (m_backButton == null)
            {
                return;
            }

            foreach (BumpButton button in new[] { m_backButton, m_forwardButton })
            {
                button.RepeatDelay = TimeSpan.FromMilliseconds(m_bumpDelay);
                button.RepeatInterval = TimeSpan.FromMilliseconds(m_bumpInterval);
            }
        }

        // A button's length along the stack: its chevron, the padding on either side of it along the stack, and its outline
        private double ButtonLength ()
        {
            return this.IsHorizontal
                ? (double)(m_buttonPadding.Left + m_buttonPadding.Right) + m_buttonFontSize + (2.0 * kBorderThickness)
                : (double)(m_buttonPadding.Top + m_buttonPadding.Bottom) + m_buttonFontSize + (2.0 * kBorderThickness);
        }

        private double ScrollOffset ()
        {
            CGRect visible = m_scrollView.ContentView.Bounds;
            return (double)(this.IsHorizontal ? visible.X : visible.Y);
        }

        private double ScrollableLength ()
        {
            CGRect visible = m_scrollView.ContentView.Bounds;
            CGRect content = m_content.Frame;
            return Math.Max(0.0, this.IsHorizontal ? (double)(content.Width - visible.Width) : (double)(content.Height - visible.Height));
        }

        // How long the content is along the stack without the clearance at its ends, and how thick it is across the stack.
        //  Both come from the content's fitting size, which follows its constraints as they are now, where its frame
        //  catches up with a new padding only once the clip view lays it out again. Only from Layout, which runs after
        //  AppKit's constraint pass.
        private void MeasureContent ()
        {
            CGSize fitting = m_content.FittingSize;
            NSEdgeInsets padding = m_content.Padding;
            m_contentLengthWithoutClearance = this.IsHorizontal
                ? (double)(fitting.Width - padding.Left - padding.Right)
                : (double)(fitting.Height - padding.Top - padding.Bottom);

            double thickness = (double)(this.IsHorizontal ? fitting.Height : fitting.Width);
            if (thickness != m_contentThickness)
            {
                m_contentThickness = thickness;
                this.InvalidateIntrinsicContentSize();
            }
        }

        // Shows the bump buttons while the content runs past the stack, and keeps their length clear at the content's ends
        //  while they show. Whether they show is judged on the content without that clearance, so putting the clearance in
        //  can't be what decides there is somewhere to scroll.
        private void RefreshBumpButtons ()
        {
            if (m_scrollView == null)
            {
                return;
            }

            NSClipView clip = m_scrollView.ContentView;
            double viewportLength = (double)(this.IsHorizontal ? clip.Bounds.Width : clip.Bounds.Height);
            bool areShown = BumpStackRules.For(m_scrollingEnabled, 0.0, m_contentLengthWithoutClearance - viewportLength).AreShown;

            double clearance = BumpStackRules.ButtonClearance(areShown, this.ButtonLength(), m_edgeClearanceOffset);
            if (clearance != m_currentClearance)
            {
                m_currentClearance = clearance;
                m_content.Padding = this.IsHorizontal
                    ? new NSEdgeInsets(0, (nfloat)clearance, 0, (nfloat)clearance)
                    : new NSEdgeInsets((nfloat)clearance, 0, (nfloat)clearance, 0);
            }

            m_backButton.Hidden = !areShown;
            m_forwardButton.Hidden = !areShown;
            this.RefreshEnabledButtons();

            // Scrolling off puts the content back at its start, clipped there
            if (!m_scrollingEnabled && (this.ScrollOffset() != 0.0))
            {
                this.ScrollTo(0.0);
            }
        }

        // Which bump button can go further, for where the content is scrolled to: on every scroll
        private void RefreshEnabledButtons ()
        {
            if (m_scrollView == null)
            {
                return;
            }

            BumpStackRules buttons = BumpStackRules.For(m_scrollingEnabled, this.ScrollOffset(), this.ScrollableLength());
            m_backButton.Enabled = buttons.CanBumpBack;
            m_forwardButton.Enabled = buttons.CanBumpForward;
        }

        private void Bump (double delta)
        {
            this.ScrollTo(BumpStackRules.NudgedOffset(this.ScrollOffset(), delta, this.ScrollableLength()));
        }

        private void ScrollTo (double offset)
        {
            NSClipView clip = m_scrollView.ContentView;
            CGRect visible = clip.Bounds;
            clip.ScrollToPoint(this.IsHorizontal ? new CGPoint((nfloat)offset, visible.Y) : new CGPoint(visible.X, (nfloat)offset));
            m_scrollView.ReflectScrolledClipView(clip);
        }

        private void RedrawButtons ()
        {
            if (m_backButton != null)
            {
                m_backButton.NeedsDisplay = true;
                m_forwardButton.NeedsDisplay = true;
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // The scroller: tells the stack when the content moves, whatever moved it, and turns a vertical wheel sideways on a
        //  horizontal stack. It holds the stack weakly, since the stack retains it.
        private sealed class BumpScrollView : NSScrollView
        {
            private readonly WeakReference<BumpStack> m_owner;

            public BumpScrollView (BumpStack owner)
            {
                m_owner = new WeakReference<BumpStack>(owner);
            }

            private BumpScrollView (NativeHandle handle) : base(handle)
            {
            }

            public bool IsHorizontal { get; set; } = true;

            // AppKit calls this after every scroll of the clip view, whatever moved it
            public override void ReflectScrolledClipView (NSClipView cView)
            {
                base.ReflectScrolledClipView(cView);
                if ((m_owner != null) && m_owner.TryGetTarget(out BumpStack owner))
                {
                    owner.RefreshEnabledButtons();
                }
            }

            public override void ScrollWheel (NSEvent theEvent)
            {
                BumpStack owner = null;
                if ((m_owner == null) || !m_owner.TryGetTarget(out owner) || !owner.m_scrollingEnabled)
                {
                    // Not scrolling, so the wheel goes on to whatever holds the stack
                    this.NextResponder?.ScrollWheel(theEvent);
                    return;
                }

                if (BumpStackRules.TryTurnWheelSideways(this.IsHorizontal, (double)theEvent.ScrollingDeltaX, (double)theEvent.ScrollingDeltaY, theEvent.HasPreciseScrollingDeltas, out double offsetDelta))
                {
                    owner.Bump(offsetDelta);
                    return;
                }

                base.ScrollWheel(theEvent);
            }
        }

        // A bump button: the stack's button fill and outline with a chevron pointing its way, faded the way WinUI3's are
        //  (brighter under the pointer and while pressed, dimmed when it can't go further). It bumps on press and repeats
        //  while held, and holds the stack weakly, since the stack retains it.
        private sealed class BumpButton : ManagedButton
        {
            private readonly WeakReference<BumpStack> m_owner;
            private readonly bool m_isForward;
            private bool m_isHorizontal = true;

            public BumpButton (BumpStack owner, bool isForward)
            {
                m_owner = new WeakReference<BumpStack>(owner);
                m_isForward = isForward;
                this.IsRepeating = true;
                this.TranslatesAutoresizingMaskIntoConstraints = false;
                this.Activated += this.OnActivated;
                this.ApplyAccessibilityLabel();
            }

            private BumpButton (NativeHandle handle) : base(handle)
            {
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing)
                {
                    this.Activated -= this.OnActivated;
                }

                base.Dispose(disposing);
            }

            public bool IsHorizontal
            {
                get => m_isHorizontal;
                set
                {
                    m_isHorizontal = value;
                    this.ApplyAccessibilityLabel();
                    this.NeedsDisplay = true;
                }
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                if ((m_owner == null) || !m_owner.TryGetTarget(out BumpStack owner))
                {
                    return;
                }

                CGContext context = NSGraphicsContext.CurrentContext.CGContext;
                context.SetAlpha((nfloat)BumpChevron.OpacityFor(this.CurrentState));
                context.BeginTransparencyLayer();

                // 1. Fill and outline, the outline inside the bounds
                CGRect outline = this.Bounds.Inset((nfloat)(kBorderThickness / 2.0), (nfloat)(kBorderThickness / 2.0));
                ThemedColors.Get(owner.m_buttonBackgroundColorKey).SetFill();
                NSBezierPath.FillRect(this.Bounds);
                ThemedColors.Get(owner.m_buttonBorderColorKey).SetStroke();
                NSBezierPath border = NSBezierPath.FromRect(outline);
                border.LineWidth = (nfloat)kBorderThickness;
                border.Stroke();

                // 2. The chevron, pointing the way the button scrolls, sized from the font size as a glyph would be
                BumpChevron.Stroke(this.Bounds, m_isForward, m_isHorizontal, owner.m_buttonFontSize, ThemedColors.Get(owner.m_buttonForegroundColorKey));

                context.EndTransparencyLayer();
            }

            // The buttons sit beside the scroll view rather than in it, so a wheel over one would go on to the stack's host;
            //  it is handed to the scroller instead, so the wheel scrolls the stack wherever the pointer is over it, as on
            //  WinUI3. Not handled on the stack itself: the scroller passes a wheel it doesn't take up to the stack, and
            //  from there it must go on.
            public override void ScrollWheel (NSEvent theEvent)
            {
                if ((m_owner != null) && m_owner.TryGetTarget(out BumpStack owner) && (owner.m_scrollView != null))
                {
                    owner.m_scrollView.ScrollWheel(theEvent);
                    return;
                }

                base.ScrollWheel(theEvent);
            }

            private void OnActivated (object sender, EventArgs e)
            {
                if ((m_owner != null) && m_owner.TryGetTarget(out BumpStack owner))
                {
                    owner.Bump(m_isForward ? owner.BumpAmount : -owner.BumpAmount);
                }
            }

            private void ApplyAccessibilityLabel ()
            {
                string direction = m_isHorizontal ? (m_isForward ? "right" : "left") : (m_isForward ? "down" : "up");
                this.AccessibilityLabel = "Scroll " + direction;
            }
        }
    }
}
