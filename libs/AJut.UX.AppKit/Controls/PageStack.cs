namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using Foundation;
    using ObjCRuntime;

    // ===========[ PageStack ]==================================================
    // BumpStack's sibling for paging, the AppKit port of WinUI3's PageStack: one child (a page) shows at a time, with a
    // page button at each end and, optionally, a strip of dots. A click on a page button or a dot runs an animated
    // transition to that page; the buttons activate once per click, they never repeat. The neighbor pages sit just past
    // the shown one, faded and mostly clipped away, so a sliver of each shows past its page button as a hint that there is
    // more. Which page shows, where each page sits, how faded it is, how a transition eases, and which dots the strip shows
    // are AJut.UX's PageStackRules and PageStackDotWindow, which WinUI3's and WPF's PageStacks follow too.
    //
    // Each child goes in a page host the stack owns, pinned to its edges, so the stack can move and fade a page without
    // writing to the consumer's own view. The hosts, page buttons, dots and page count are placed by frame in Layout, the
    // hosts in a view that clips to its bounds. The page buttons run the stack's full height (width, when vertical), past
    // the dot strip. A page that is not the shown one takes no clicks, and one that can't be seen at all is hidden, which
    // also takes it out of the key view loop. Across the stack it is as thick as its thickest page (plus the page padding
    // and the dot strip), as WinUI3's sizes to its largest page; along the stack it has no size of its own.
    //
    // The dot strip slides with the pages: a pick turns its dot current at once, and the strip then shifts by however many
    // dots the pick was away, on the same clock and easing as the page transition, so the current dot settles back where
    // the window puts it. While it slides the strip holds the dots of both windows, so the ones leaving slide out of view
    // rather than vanish. Each dot stays with its page through a slide, so only a dot whose kind changes eases its size.
    // A jump too far for the two strips to share a dot takes its new place at once. An edge dot grows to full size under
    // the pointer, staying faded, so it reads as something to click.
    //
    // A transition steps on a repeating timer at display rate, started when a transition starts and invalidated when it
    // lands, when the stack leaves its window, and on dispose. A dot easing to a new size or fade steps on a second one,
    // which runs only while a dot is easing and is let go of the same ways.
    //
    // Lifetime: the page buttons, dots and page count are retained by the stack natively, so each holds the stack weakly
    //  and asks for it when it needs it. The stack's handler on its own Children collection is a self-subscription.
    /// <summary>
    /// Shows one of its children at a time as a page, with a page button at each end and an optional dot strip, and runs an
    /// animated transition to whichever page is picked. The neighbor pages peek in, faded, past the page buttons.
    /// </summary>
    public class PageStack : ThemedView
    {
        private const double kBorderThickness = 1.0;
        private const double kDotPadding = 3.0;
        private const double kDefaultDotStripGap = 4.0;
        private const double kDefaultPageGap = 5.0;
        private const double kPageCountGap = 8.0;
        private const double kTransitionFrameSeconds = 1.0 / 60.0;
        private const ushort kLeftArrowKeyCode = 123;
        private const ushort kRightArrowKeyCode = 124;
        private const ushort kDownArrowKeyCode = 125;
        private const ushort kUpArrowKeyCode = 126;
        private const ushort kHomeKeyCode = 115;
        private const ushort kEndKeyCode = 119;

        /// <summary>An inset left unset: <see cref="PagePadding"/> and <see cref="DotStripPadding"/> start here, and then pad the side facing the other (the pages pad both sides while no strip shows)</summary>
        private static readonly NSEdgeInsets kUnsetInsets = new NSEdgeInsets(nfloat.NaN, nfloat.NaN, nfloat.NaN, nfloat.NaN);

        private readonly NSView m_pagesClip;
        private readonly DotsClip m_dotsClip;
        private readonly PageCountView m_pageCount;
        private readonly List<PageHost> m_pageHosts = new List<PageHost>();
        private readonly List<PageDot> m_dots = new List<PageDot>();
        private readonly PageButton m_backButton;
        private readonly PageButton m_forwardButton;
        private NSUserInterfaceLayoutOrientation m_orientation = NSUserInterfaceLayoutOrientation.Horizontal;
        private int m_selectedIndex;
        private bool m_showDots;
        private int m_dotReach = 5;
        private bool m_showPageCount;
        private string m_pageCountFormat = PageStackRules.kDefaultPageCountFormat;
        private double m_pageSpacing = 8.0;
        private double m_pagePeek = 16.0;
        private double m_inactivePageOpacity = 0.25;
        private double m_buttonFontSize = 12.0;
        private NSEdgeInsets m_buttonPadding = new NSEdgeInsets(0, 4, 0, 4);
        private NSEdgeInsets m_pagePadding = kUnsetInsets;
        private NSEdgeInsets m_dotStripPadding = kUnsetInsets;
        private double m_dotSize = 8.0;
        private string m_buttonBackgroundColorKey = ThemedColorKeys.PageStack_ButtonBackground;
        private string m_buttonForegroundColorKey = ThemedColorKeys.PageStack_ButtonForeground;
        private string m_buttonBorderColorKey = ThemedColorKeys.PageStack_ButtonBorder;
        private string m_dotColorKey = ThemedColorKeys.PageStack_Dot;
        private string m_currentDotColorKey = ThemedColorKeys.PageStack_CurrentDot;
        private double m_pageThickness;
        private bool m_needsPageMeasure = true;
        private double m_position;
        private double m_transitionFrom;
        private double m_transitionTo;
        private long m_transitionStartTimestamp;
        private NSTimer m_transitionTimer;
        private NSTimer m_dotEaseTimer;
        private int m_dotSpanFirstPage;
        private int m_windowDotCount;
        private double m_dotScroll;
        private double m_dotScrollFrom;
        private int m_dotScrollTo;
        private bool m_isDotSliding;

        // ===========[ Setup/Construction/Teardown ]===========================
        public PageStack ()
        {
            m_pagesClip = new NSView { ClipsToBounds = true };
            this.AddSubview(m_pagesClip);

            m_dotsClip = new DotsClip();
            this.AddSubview(m_dotsClip);
            m_pageCount = new PageCountView(this);
            this.AddSubview(m_pageCount);

            // Over the pages, so they draw over the peeks
            m_backButton = new PageButton(this, isForward: false);
            m_forwardButton = new PageButton(this, isForward: true);
            this.AddSubview(m_backButton);
            this.AddSubview(m_forwardButton);

            this.Children = new ObservableCollection<NSView>();
            this.Children.CollectionChanged += this.OnChildrenChanged;
            this.ApplyOrientation();
            this.RefreshDots();
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native stack whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls: the native stack keeps the subviews it already has.
        /// None of its managed state survives a dispose, so a call that needs that state can still fail: dispose it only
        /// once AppKit is done with it.
        /// </summary>
        protected PageStack (NativeHandle handle) : base(handle)
        {
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                this.StopTransition();
                this.StopDotEase();
                if (this.Children != null)
                {
                    this.Children.CollectionChanged -= this.OnChildrenChanged;
                }
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        /// <summary>Raised when <see cref="SelectedIndex"/> changes, whether set from code, a click, a key, or a page added or removed</summary>
        public event EventHandler<EventArgs> SelectedIndexChanged;

        private void OnChildrenChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            int oldPageCount = m_pageHosts.Count;
            int selectedIndex = m_selectedIndex;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    for (int i = 0; i < e.NewItems.Count; ++i)
                    {
                        this.InsertPageHost(e.NewStartingIndex + i, (NSView)e.NewItems[i]);
                    }

                    selectedIndex = PageStackRules.SelectedIndexAfterInsert(selectedIndex, oldPageCount, e.NewStartingIndex, e.NewItems.Count);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    for (int i = 0; i < e.OldItems.Count; ++i)
                    {
                        this.RemovePageHostAt(e.OldStartingIndex);
                    }

                    selectedIndex = PageStackRules.SelectedIndexAfterRemoval(selectedIndex, oldPageCount, e.OldStartingIndex, e.OldItems.Count);
                    break;

                default:
                    this.SyncPageHosts();
                    break;
            }

            // A page added or removed moves the shown page at once: only a pick runs a transition
            bool hasChanged = selectedIndex != m_selectedIndex;
            m_selectedIndex = selectedIndex;
            this.JumpTo(this.EffectiveSelectedIndex);
            this.RefreshDots();
            this.RefreshPageCount();
            m_needsPageMeasure = true;
            this.InvalidateIntrinsicContentSize();
            if (hasChanged)
            {
                this.SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnTransitionTick (NSTimer timer)
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(m_transitionStartTimestamp);
            TimeSpan duration = this.TransitionDuration;
            m_position = PageStackRules.TransitionPosition(m_transitionFrom, m_transitionTo, elapsed, duration);
            if (m_isDotSliding)
            {
                m_dotScroll = PageStackRules.TransitionPosition(m_dotScrollFrom, m_dotScrollTo, elapsed, duration);
            }

            if (PageStackRules.IsTransitionDone(elapsed, duration))
            {
                this.StopTransition();
                this.LandDotSlide();
            }

            this.NeedsLayout = true;
        }

        private void OnDotEaseTick (NSTimer timer)
        {
            bool isAnyEasing = false;
            foreach (PageDot dot in m_dots)
            {
                isAnyEasing |= dot.StepLook();
            }

            if (!isAnyEasing)
            {
                this.StopDotEase();
            }
        }

        public override void ViewDidMoveToWindow ()
        {
            base.ViewDidMoveToWindow();

            // Lands any running transition and dot ease, which invalidates their timers
            if (this.Window == null)
            {
                if (m_transitionTimer != null)
                {
                    this.JumpTo((int)m_transitionTo);
                }

                this.StopDotEase();
                foreach (PageDot dot in m_dots)
                {
                    dot.LandLook();
                }
            }
        }

        public override void KeyDown (NSEvent theEvent)
        {
            int target;
            bool isHorizontal = this.IsHorizontal;
            switch (theEvent.KeyCode)
            {
                case kLeftArrowKeyCode when isHorizontal:
                case kUpArrowKeyCode when !isHorizontal:
                    target = this.EffectiveSelectedIndex - 1;
                    break;

                case kRightArrowKeyCode when isHorizontal:
                case kDownArrowKeyCode when !isHorizontal:
                    target = this.EffectiveSelectedIndex + 1;
                    break;

                case kHomeKeyCode:
                    target = 0;
                    break;

                case kEndKeyCode:
                    target = this.Children.Count - 1;
                    break;

                default:
                    base.KeyDown(theEvent);
                    return;
            }

            if (!this.GoTo(target))
            {
                base.KeyDown(theEvent);
            }
        }

        // ===========[ Properties ]============================================
        /// <summary>The pages, in order. Each is pinned to the page area's edges, so it takes the size the stack gives it.</summary>
        public ObservableCollection<NSView> Children { get; }

        /// <summary>Which way the pages run: Horizontal (the default) or Vertical</summary>
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
        /// The index of the page shown. An index past either end shows the page at that end, and is kept as given, so one
        /// set before the pages are added still lands once they are. Setting it runs a transition to that page.
        /// </summary>
        public int SelectedIndex
        {
            get => m_selectedIndex;
            set
            {
                if (m_selectedIndex != value)
                {
                    m_selectedIndex = value;
                    this.BeginTransitionTo(this.EffectiveSelectedIndex);
                    this.RefreshDots(slideDots: m_transitionTimer != null);
                    this.RefreshPageCount();
                    this.SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>Whether a strip of dots shows under the pages (beside them when vertical), one per page in the dot window</summary>
        public bool ShowDots
        {
            get => m_showDots;
            set
            {
                m_showDots = value;
                this.RefreshDots();
                this.InvalidateIntrinsicContentSize();
                this.NeedsLayout = true;
            }
        }

        /// <summary>
        /// How many dots show behind and ahead of the current page's dot: 5 shows 11 dots, plus a smaller, faded dot at
        /// each end while there are more pages past them. Clicking one of those goes to the page just past the window.
        /// </summary>
        public int DotReach
        {
            get => m_dotReach;
            set
            {
                m_dotReach = value;
                this.RefreshDots();
            }
        }

        /// <summary>Whether the page count ("3 of 7") shows after the dots, or on its own where the dots would be when they are off</summary>
        public bool ShowPageCount
        {
            get => m_showPageCount;
            set
            {
                m_showPageCount = value;
                this.RefreshPageCount();
                this.InvalidateIntrinsicContentSize();
                this.NeedsLayout = true;
            }
        }

        /// <summary>
        /// The page count's text, with {0} for the shown page's number from one and {1} for the page count: "{0} of {1}"
        /// by default. The format's own words draw smaller and fainter than the counts, and when vertical, each word goes
        /// on a line of its own.
        /// </summary>
        public string PageCountFormat
        {
            get => m_pageCountFormat;
            set
            {
                m_pageCountFormat = value;
                this.RefreshPageCount();
            }
        }

        /// <summary>How long a transition from one page to another takes, eased out; zero jumps straight there</summary>
        public TimeSpan TransitionDuration { get; set; } = TimeSpan.FromMilliseconds(250);

        /// <summary>The gap between one page and the next, in points</summary>
        public double PageSpacing
        {
            get => m_pageSpacing;
            set
            {
                m_pageSpacing = value;
                this.NeedsLayout = true;
            }
        }

        /// <summary>How much of each neighbor page shows past its page button, in points, as a hint that there is more</summary>
        public double PagePeek
        {
            get => m_pagePeek;
            set
            {
                m_pagePeek = value;
                this.NeedsLayout = true;
            }
        }

        /// <summary>
        /// The space around the pages, inside the page buttons. Left unset, it is 5 on the side the pages would otherwise
        /// crowd: the top when horizontal, the left when vertical. When neither the dots nor the page count shows, the
        /// bottom (or right) gets 5 as well, since no strip sits there.
        /// </summary>
        public NSEdgeInsets PagePadding
        {
            get => m_pagePadding;
            set
            {
                m_pagePadding = value;
                this.InvalidateIntrinsicContentSize();
                this.NeedsLayout = true;
            }
        }

        /// <summary>
        /// The space around the dot strip. Left unset, it is 4 on the side facing the pages: the top when horizontal, the
        /// left when vertical. When vertical with no dots, the right gets 4 as well, so the page count stays off the edge.
        /// </summary>
        public NSEdgeInsets DotStripPadding
        {
            get => m_dotStripPadding;
            set
            {
                m_dotStripPadding = value;
                this.InvalidateIntrinsicContentSize();
                this.NeedsLayout = true;
            }
        }

        /// <summary>How opaque the pages that are not shown draw, so their peeks read as hints rather than content</summary>
        public double InactivePageOpacity
        {
            get => m_inactivePageOpacity;
            set
            {
                m_inactivePageOpacity = value;
                this.NeedsLayout = true;
            }
        }

        /// <summary>The <see cref="ThemedColors"/> key the page buttons fill with</summary>
        public string ButtonBackgroundColorKey
        {
            get => m_buttonBackgroundColorKey;
            set
            {
                m_buttonBackgroundColorKey = value;
                this.RedrawButtonsAndDots();
            }
        }

        /// <summary>The <see cref="ThemedColors"/> key the page buttons' chevrons are drawn in</summary>
        public string ButtonForegroundColorKey
        {
            get => m_buttonForegroundColorKey;
            set
            {
                m_buttonForegroundColorKey = value;
                this.RedrawButtonsAndDots();
            }
        }

        /// <summary>The <see cref="ThemedColors"/> key the page buttons are outlined in, so they stand apart from the peeks under them</summary>
        public string ButtonBorderColorKey
        {
            get => m_buttonBorderColorKey;
            set
            {
                m_buttonBorderColorKey = value;
                this.RedrawButtonsAndDots();
            }
        }

        /// <summary>The <see cref="ThemedColors"/> key every dot but the current page's fills with, and the page count is drawn in</summary>
        public string DotColorKey
        {
            get => m_dotColorKey;
            set
            {
                m_dotColorKey = value;
                this.RedrawButtonsAndDots();
            }
        }

        /// <summary>The <see cref="ThemedColors"/> key the current page's dot fills with</summary>
        public string CurrentDotColorKey
        {
            get => m_currentDotColorKey;
            set
            {
                m_currentDotColorKey = value;
                this.RedrawButtonsAndDots();
            }
        }

        /// <summary>The size of the page buttons' chevrons, in points: the glyph's font size on the other stacks</summary>
        public double ButtonFontSize
        {
            get => m_buttonFontSize;
            set
            {
                m_buttonFontSize = value;
                this.NeedsLayout = true;
                this.RedrawButtonsAndDots();
            }
        }

        /// <summary>The space around each page button's chevron</summary>
        public NSEdgeInsets ButtonPadding
        {
            get => m_buttonPadding;
            set
            {
                m_buttonPadding = value;
                this.NeedsLayout = true;
            }
        }

        /// <summary>The diameter of a full size dot, in points; the edge dots draw smaller</summary>
        public double DotSize
        {
            get => m_dotSize;
            set
            {
                m_dotSize = value;
                this.InvalidateIntrinsicContentSize();
                this.NeedsLayout = true;
                this.RedrawButtonsAndDots();
            }
        }

        public override bool AcceptsFirstResponder () => true;
        public override CGRect FocusRingMaskBounds => this.Bounds;

        // Across the stack it is as thick as its thickest page, plus the page padding and the dot strip; along it, it has
        //  no size of its own. The thickness is measured in Layout, never here: AppKit asks for this inside its constraint
        //  pass, where asking for a fitting size is not safe.
        public override CGSize IntrinsicContentSize
        {
            get
            {
                if (m_pagesClip == null)
                {
                    return base.IntrinsicContentSize;
                }

                NSEdgeInsets padding = this.EffectivePagePadding();
                double paddingAcross = this.IsHorizontal ? (double)(padding.Top + padding.Bottom) : (double)(padding.Left + padding.Right);
                double thickness = m_pageThickness + paddingAcross + this.DotStripThickness();
                return this.IsHorizontal
                    ? new CGSize(NSView.NoIntrinsicMetric, (nfloat)thickness)
                    : new CGSize((nfloat)thickness, NSView.NoIntrinsicMetric);
            }
        }

        private bool IsHorizontal => m_orientation == NSUserInterfaceLayoutOrientation.Horizontal;
        private int EffectiveSelectedIndex => PageStackRules.EffectiveSelectedIndex(m_selectedIndex, this.Children.Count);

        // ===========[ Public Interface Methods ]==============================
        public override void DrawFocusRingMask ()
        {
            NSBezierPath.FillRect(this.Bounds);
        }

        public override void Layout ()
        {
            base.Layout();
            if (m_pagesClip == null)
            {
                return;
            }

            if (m_needsPageMeasure)
            {
                this.MeasurePages();
            }

            double dotStrip = this.DotStripThickness();
            this.LayoutPagesAndButtons(dotStrip);
            this.LayoutDotStrip(dotStrip);
        }

        // ===========[ Helper Methods ]========================================
        // Picks a page as a person does, through a page button, a dot or a key: false when there is no such page, or the
        //  stack already shows it
        private bool GoTo (int pageIndex)
        {
            if ((pageIndex < 0) || (pageIndex >= this.Children.Count) || (pageIndex == this.EffectiveSelectedIndex))
            {
                return false;
            }

            this.SelectedIndex = pageIndex;
            return true;
        }

        private void ApplyOrientation ()
        {
            if (m_backButton == null)
            {
                return;
            }

            m_backButton.IsHorizontal = this.IsHorizontal;
            m_forwardButton.IsHorizontal = this.IsHorizontal;
            m_needsPageMeasure = true;
            this.RefreshPageCount();
            this.InvalidateIntrinsicContentSize();
            this.NeedsLayout = true;
        }

        private void InsertPageHost (int index, NSView page)
        {
            var host = new PageHost();
            page.TranslatesAutoresizingMaskIntoConstraints = false;
            host.AddSubview(page);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                page.TopAnchor.ConstraintEqualTo(host.TopAnchor),
                page.BottomAnchor.ConstraintEqualTo(host.BottomAnchor),
                page.LeadingAnchor.ConstraintEqualTo(host.LeadingAnchor),
                page.TrailingAnchor.ConstraintEqualTo(host.TrailingAnchor),
            });

            m_pageHosts.Insert(index, host);
            m_pagesClip.AddSubview(host);
        }

        private void RemovePageHostAt (int index)
        {
            PageHost host = m_pageHosts[index];
            m_pageHosts.RemoveAt(index);

            // Lets go of the page, so it can go into another parent; its constraints to the host go with it
            foreach (NSView page in host.Subviews)
            {
                page.RemoveFromSuperview();
            }

            host.RemoveFromSuperview();
            host.Dispose();
        }

        private void SyncPageHosts ()
        {
            while (m_pageHosts.Count > 0)
            {
                this.RemovePageHostAt(m_pageHosts.Count - 1);
            }

            for (int i = 0; i < this.Children.Count; ++i)
            {
                this.InsertPageHost(i, this.Children[i]);
            }
        }

        private void BeginTransitionTo (int pageIndex)
        {
            if ((pageIndex < 0) || (this.Window == null) || (this.TransitionDuration <= TimeSpan.Zero))
            {
                this.JumpTo(pageIndex);
                return;
            }

            // Started from wherever the stack is now, so a pick during a transition carries on from mid way
            m_transitionFrom = m_position;
            m_transitionTo = pageIndex;
            m_transitionStartTimestamp = Stopwatch.GetTimestamp();
            if (m_transitionTimer == null)
            {
                m_transitionTimer = NSTimer.CreateRepeatingScheduledTimer(TimeSpan.FromSeconds(kTransitionFrameSeconds), this.OnTransitionTick);
            }

            this.NeedsLayout = true;
        }

        private void JumpTo (int pageIndex)
        {
            this.StopTransition();
            this.LandDotSlide();
            m_position = Math.Max(0, pageIndex);
            this.NeedsLayout = true;
        }

        private void StopTransition ()
        {
            if (m_transitionTimer != null)
            {
                m_transitionTimer.Invalidate();
                m_transitionTimer.Dispose();
                m_transitionTimer = null;
            }
        }

        // Starts stepping the dots easing to a new size or fade, unless that is already running: false when the stack is in
        //  no window, where nothing would step them
        private bool StartDotEase ()
        {
            if (this.Window == null)
            {
                return false;
            }

            if (m_dotEaseTimer == null)
            {
                m_dotEaseTimer = NSTimer.CreateRepeatingScheduledTimer(TimeSpan.FromSeconds(kTransitionFrameSeconds), this.OnDotEaseTick);
            }

            return true;
        }

        private void StopDotEase ()
        {
            if (m_dotEaseTimer != null)
            {
                m_dotEaseTimer.Invalidate();
                m_dotEaseTimer.Dispose();
                m_dotEaseTimer = null;
            }
        }

        // A page button's length along the stack: its chevron, the padding on either side of it along the stack, and its outline
        private double ButtonLength ()
        {
            return this.IsHorizontal
                ? (double)(m_buttonPadding.Left + m_buttonPadding.Right) + m_buttonFontSize + (2.0 * kBorderThickness)
                : (double)(m_buttonPadding.Top + m_buttonPadding.Bottom) + m_buttonFontSize + (2.0 * kBorderThickness);
        }

        private double DotCellLength () => m_dotSize + (2.0 * kDotPadding);

        // How thick the dot strip is across the stack: the dots or the page count, whichever is thicker, and its padding
        private double DotStripThickness ()
        {
            int pageCount = this.Children.Count;
            bool areDotsShown = PageStackRules.AreDotsShown(m_showDots, pageCount);
            bool isPageCountShown = PageStackRules.IsPageCountShown(m_showPageCount, pageCount);
            if (!areDotsShown && !isPageCountShown)
            {
                return 0.0;
            }

            CGSize countSize = m_pageCount.ContentSize;
            double dotsThickness = areDotsShown ? this.DotCellLength() : 0.0;
            double countThickness = isPageCountShown ? (double)(this.IsHorizontal ? countSize.Height : countSize.Width) : 0.0;
            NSEdgeInsets padding = this.EffectiveDotStripPadding();
            double paddingAcross = this.IsHorizontal ? (double)(padding.Top + padding.Bottom) : (double)(padding.Left + padding.Right);
            return Math.Max(dotsThickness, countThickness) + paddingAcross;
        }

        // How thick the thickest page is across the stack, from each page's fitting size. Only from Layout, which runs after
        //  AppKit's constraint pass.
        private void MeasurePages ()
        {
            m_needsPageMeasure = false;
            double thickness = 0.0;
            foreach (NSView page in this.Children)
            {
                CGSize fitting = page.FittingSize;
                thickness = Math.Max(thickness, (double)(this.IsHorizontal ? fitting.Height : fitting.Width));
            }

            if (thickness != m_pageThickness)
            {
                m_pageThickness = thickness;
                this.InvalidateIntrinsicContentSize();
            }
        }

        // Places and fades each page for where the stack is now, mid transition or not, inside the page padding, and the
        //  page buttons at the ends, the whole way across the stack
        private void LayoutPagesAndButtons (double dotStrip)
        {
            CGRect bounds = this.Bounds;
            CGRect pagesArea = this.IsHorizontal
                ? new CGRect(0, 0, bounds.Width, (nfloat)Math.Max(0.0, (double)bounds.Height - dotStrip))
                : new CGRect(0, 0, (nfloat)Math.Max(0.0, (double)bounds.Width - dotStrip), bounds.Height);
            m_pagesClip.Frame = pagesArea;

            int pageCount = this.Children.Count;
            int shownPage = this.EffectiveSelectedIndex;
            bool areButtonsShown = PageStackRules.ArePageButtonsShown(pageCount);
            double buttonLength = this.ButtonLength();
            double clearance = PageStackRules.PageClearance(areButtonsShown, buttonLength, m_pagePeek, m_pageSpacing);
            NSEdgeInsets padding = this.EffectivePagePadding();
            double width = (double)pagesArea.Width;
            double height = (double)pagesArea.Height;
            double pageLength = this.IsHorizontal
                ? Math.Max(0.0, width - (2.0 * clearance) - (double)(padding.Left + padding.Right))
                : Math.Max(0.0, height - (2.0 * clearance) - (double)(padding.Top + padding.Bottom));
            double pageThickness = this.IsHorizontal
                ? Math.Max(0.0, height - (double)(padding.Top + padding.Bottom))
                : Math.Max(0.0, width - (double)(padding.Left + padding.Right));

            for (int i = 0; i < m_pageHosts.Count; ++i)
            {
                PageHost host = m_pageHosts[i];
                double offset = PageStackRules.PageOffset(i, m_position, pageLength, m_pageSpacing);
                host.Frame = this.IsHorizontal
                    ? new CGRect((nfloat)(clearance + (double)padding.Left + offset), padding.Top, (nfloat)pageLength, (nfloat)pageThickness)
                    : new CGRect(padding.Left, (nfloat)(clearance + (double)padding.Top + offset), (nfloat)pageThickness, (nfloat)pageLength);
                host.AlphaValue = (nfloat)PageStackRules.PageOpacity(i, m_position, m_inactivePageOpacity);
                host.IsShownPage = i == shownPage;

                // A page past its neighbors' peeks can't be seen at all
                host.Hidden = Math.Abs(i - m_position) > 1.0;
            }

            m_backButton.Hidden = !areButtonsShown;
            m_forwardButton.Hidden = !areButtonsShown;
            m_backButton.Enabled = PageStackRules.CanGoBack(shownPage);
            m_forwardButton.Enabled = PageStackRules.CanGoForward(shownPage, pageCount);
            if (this.IsHorizontal)
            {
                m_backButton.Frame = new CGRect(0, 0, (nfloat)buttonLength, bounds.Height);
                m_forwardButton.Frame = new CGRect((nfloat)((double)bounds.Width - buttonLength), 0, (nfloat)buttonLength, bounds.Height);
            }
            else
            {
                m_backButton.Frame = new CGRect(0, 0, bounds.Width, (nfloat)buttonLength);
                m_forwardButton.Frame = new CGRect(0, (nfloat)((double)bounds.Height - buttonLength), bounds.Width, (nfloat)buttonLength);
            }
        }

        // Places the dot strip past the pages: the dots' clip and the page count after it, centered along the stack inside
        //  the strip's padding, and each dot along the clip for where the strip has slid to
        private void LayoutDotStrip (double dotStrip)
        {
            if (dotStrip <= 0.0)
            {
                return;
            }

            // 1. The strip, inside its padding
            CGRect bounds = this.Bounds;
            NSEdgeInsets padding = this.EffectiveDotStripPadding();
            CGRect strip = this.IsHorizontal
                ? new CGRect(0, (nfloat)((double)bounds.Height - dotStrip), bounds.Width, (nfloat)dotStrip)
                : new CGRect((nfloat)((double)bounds.Width - dotStrip), 0, (nfloat)dotStrip, bounds.Height);
            double innerX = (double)(strip.X + padding.Left);
            double innerY = (double)(strip.Y + padding.Top);
            double innerWidth = Math.Max(0.0, (double)(strip.Width - padding.Left - padding.Right));
            double innerHeight = Math.Max(0.0, (double)(strip.Height - padding.Top - padding.Bottom));

            // 2. How long the dots and count run together, so the pair centers along the stack
            int pageCount = this.Children.Count;
            bool areDotsShown = PageStackRules.AreDotsShown(m_showDots, pageCount);
            bool isPageCountShown = PageStackRules.IsPageCountShown(m_showPageCount, pageCount);
            double cell = this.DotCellLength();
            double dotsLength = areDotsShown ? m_windowDotCount * cell : 0.0;
            CGSize countSize = m_pageCount.ContentSize;
            double countLength = isPageCountShown ? (double)(this.IsHorizontal ? countSize.Width : countSize.Height) : 0.0;
            double gap = (areDotsShown && isPageCountShown) ? kPageCountGap : 0.0;
            double runLength = dotsLength + gap + countLength;
            double runStart = this.IsHorizontal
                ? innerX + ((innerWidth - runLength) / 2.0)
                : innerY + ((innerHeight - runLength) / 2.0);

            // 3. The dots' clip, and each dot along it
            if (areDotsShown)
            {
                m_dotsClip.Frame = this.IsHorizontal
                    ? new CGRect((nfloat)runStart, (nfloat)(innerY + ((innerHeight - cell) / 2.0)), (nfloat)dotsLength, (nfloat)cell)
                    : new CGRect((nfloat)(innerX + ((innerWidth - cell) / 2.0)), (nfloat)runStart, (nfloat)cell, (nfloat)dotsLength);
                for (int i = 0; i < m_dots.Count; ++i)
                {
                    double along = (m_dotSpanFirstPage + i - m_dotScroll) * cell;
                    m_dots[i].Frame = this.IsHorizontal
                        ? new CGRect((nfloat)along, 0, (nfloat)cell, (nfloat)cell)
                        : new CGRect(0, (nfloat)along, (nfloat)cell, (nfloat)cell);
                }
            }

            // 4. The page count after them
            if (isPageCountShown)
            {
                double countStart = runStart + dotsLength + gap;
                m_pageCount.Frame = this.IsHorizontal
                    ? new CGRect((nfloat)countStart, (nfloat)(innerY + ((innerHeight - (double)countSize.Height) / 2.0)), countSize.Width, countSize.Height)
                    : new CGRect((nfloat)(innerX + ((innerWidth - (double)countSize.Width) / 2.0)), (nfloat)countStart, countSize.Width, countSize.Height);
            }
        }

        // Matches the dot strip to the shown page. With slideDots, the strip slides to its new place on the running
        //  transition's clock rather than taking it at once.
        private void RefreshDots (bool slideDots = false)
        {
            if (m_dotsClip == null)
            {
                return;
            }

            int pageCount = this.Children.Count;
            bool areDotsShown = PageStackRules.AreDotsShown(m_showDots, pageCount);
            m_dotsClip.Hidden = !areDotsShown;
            this.NeedsLayout = true;
            if (!areDotsShown)
            {
                m_isDotSliding = false;
                m_windowDotCount = 0;
                this.SyncDotSpan(new PageStackRules.DotSpan(0, -1));
                return;
            }

            // 1. Where the strip is going, and whether it slides there or takes its place at once
            PageStackDotWindow window = PageStackDotWindow.For(pageCount, this.EffectiveSelectedIndex, m_dotReach);
            int toFirstDotPage = window.FirstDotPage;
            m_isDotSliding = slideDots && PageStackRules.ShouldSlideDots(m_dotScroll, toFirstDotPage, window.DotCount);
            if (!m_isDotSliding)
            {
                m_dotScroll = toFirstDotPage;
            }

            m_dotScrollFrom = m_dotScroll;
            m_dotScrollTo = toFirstDotPage;
            m_windowDotCount = window.DotCount;

            // 2. The dots it holds: the new window's, and while sliding the old one's too
            this.SyncDotSpan(
                m_isDotSliding
                    ? PageStackRules.DotSlideSpan(m_dotScroll, toFirstDotPage, window.DotCount, pageCount)
                    : new PageStackRules.DotSpan(window.FirstDotPage, window.LastDotPage)
            );

            // 3. Fill each for its page, against the new window, so a dot sliding out of view already looks like an edge dot
            for (int i = 0; i < m_dots.Count; ++i)
            {
                int page = m_dotSpanFirstPage + i;
                PageDot dot = m_dots[i];
                dot.PageIndex = page;
                dot.SetKind(window.KindOf(page));
                dot.AccessibilityLabel = $"Page {page + 1} of {pageCount}";
            }
        }

        // Keeps a dot for each page of the span, in order. The span only ever grows or shrinks at its ends, so each dot
        //  stays with its page and only the dots at the ends come and go.
        private void SyncDotSpan (PageStackRules.DotSpan span)
        {
            int currentLastPage = m_dotSpanFirstPage + m_dots.Count - 1;
            if ((span.Count == 0) || (span.LastPage < m_dotSpanFirstPage) || (span.FirstPage > currentLastPage))
            {
                while (m_dots.Count > 0)
                {
                    this.RemoveDotAt(m_dots.Count - 1);
                }
            }

            if (m_dots.Count == 0)
            {
                m_dotSpanFirstPage = span.FirstPage;
            }

            // 1. Drop the dots past either end of the span
            while ((m_dots.Count > 0) && (m_dotSpanFirstPage < span.FirstPage))
            {
                this.RemoveDotAt(0);
                ++m_dotSpanFirstPage;
            }

            while ((m_dots.Count > 0) && ((m_dotSpanFirstPage + m_dots.Count - 1) > span.LastPage))
            {
                this.RemoveDotAt(m_dots.Count - 1);
            }

            // 2. Add the dots the span reaches past what there is
            while ((span.Count > 0) && (m_dotSpanFirstPage > span.FirstPage))
            {
                this.InsertDotAt(0);
                --m_dotSpanFirstPage;
            }

            while ((m_dotSpanFirstPage + m_dots.Count - 1) < span.LastPage)
            {
                this.InsertDotAt(m_dots.Count);
            }
        }

        private void InsertDotAt (int index)
        {
            var dot = new PageDot(this);
            m_dots.Insert(index, dot);
            m_dotsClip.AddSubview(dot);
        }

        private void RemoveDotAt (int index)
        {
            PageDot dot = m_dots[index];
            m_dots.RemoveAt(index);
            dot.RemoveFromSuperview();
            dot.Dispose();
        }

        // Ends a dot slide where it was going, and lets go of the dots that slid out of view
        private void LandDotSlide ()
        {
            if (m_isDotSliding)
            {
                m_isDotSliding = false;
                m_dotScroll = m_dotScrollTo;
                this.RefreshDots();
            }
        }

        // Fills the page count from PageCountFormat for the shown page, split for the orientation, and names the whole
        //  count for accessibility
        private void RefreshPageCount ()
        {
            if (m_pageCount == null)
            {
                return;
            }

            int pageCount = this.Children.Count;
            int shownPage = this.EffectiveSelectedIndex;
            bool isPageCountShown = PageStackRules.IsPageCountShown(m_showPageCount, pageCount);
            m_pageCount.Hidden = !isPageCountShown;
            m_pageCount.SetParts(
                isPageCountShown ? PageStackRules.SplitPageCount(m_pageCountFormat, shownPage, pageCount, isVertical: !this.IsHorizontal) : default,
                isVertical: !this.IsHorizontal
            );
            m_pageCount.AccessibilityLabel = isPageCountShown ? PageStackRules.PageCountText(m_pageCountFormat, shownPage, pageCount) : String.Empty;
            this.InvalidateIntrinsicContentSize();
            this.NeedsLayout = true;
        }

        private void RedrawButtonsAndDots ()
        {
            if (m_backButton == null)
            {
                return;
            }

            m_backButton.NeedsDisplay = true;
            m_forwardButton.NeedsDisplay = true;
            m_pageCount.NeedsDisplay = true;
            foreach (PageDot dot in m_dots)
            {
                dot.NeedsDisplay = true;
            }
        }

        // The pages' padding; left unset, it pads their far side too while no strip sits there
        private NSEdgeInsets EffectivePagePadding ()
        {
            bool isDotStripShown = PageStackRules.IsDotStripShown(m_showDots, m_showPageCount, this.Children.Count);
            return EffectiveInsets(
                m_pagePadding,
                this.IsHorizontal,
                kDefaultPageGap,
                isGapOnBothSides: !isDotStripShown
            );
        }

        // The dot strip's padding. A vertical count with no dots beside it would run to the stack's right edge, since text
        //  has no side bearing to keep it off, so left unset it gets the gap there too.
        private NSEdgeInsets EffectiveDotStripPadding ()
        {
            bool areDotsShown = PageStackRules.AreDotsShown(m_showDots, this.Children.Count);
            return EffectiveInsets(
                m_dotStripPadding,
                this.IsHorizontal,
                kDefaultDotStripGap,
                isGapOnBothSides: !this.IsHorizontal && !areDotsShown
            );
        }

        // An inset left unset (kUnsetInsets) is the gap given on the top when horizontal, the left when vertical, and with
        //  isGapOnBothSides on the bottom or right as well
        private static NSEdgeInsets EffectiveInsets (NSEdgeInsets insets, bool isHorizontal, double defaultGap, bool isGapOnBothSides = false)
        {
            if (nfloat.IsNaN(insets.Top) || nfloat.IsNaN(insets.Left) || nfloat.IsNaN(insets.Bottom) || nfloat.IsNaN(insets.Right))
            {
                nfloat farGap = isGapOnBothSides ? (nfloat)defaultGap : 0;
                return isHorizontal
                    ? new NSEdgeInsets((nfloat)defaultGap, 0, farGap, 0)
                    : new NSEdgeInsets(0, (nfloat)defaultGap, 0, farGap);
            }

            return insets;
        }

        // ===========[ Subclasses/structs ]====================================
        // Holds one page, pinned to its edges. Only the shown page takes clicks, so a peek can't be clicked into.
        private sealed class PageHost : NSView
        {
            public PageHost ()
            {
            }

            private PageHost (NativeHandle handle) : base(handle)
            {
            }

            public override bool IsFlipped => true;

            public bool IsShownPage { get; set; }

            public override NSView HitTest (CGPoint aPoint) => this.IsShownPage ? base.HitTest(aPoint) : null;
        }

        // Clips the dots to the strip's window while they slide along it, top down like the stack
        private sealed class DotsClip : NSView
        {
            public DotsClip ()
            {
                this.ClipsToBounds = true;
            }

            private DotsClip (NativeHandle handle) : base(handle)
            {
            }

            public override bool IsFlipped => true;
        }

        // A page button: the stack's button fill and outline with a chevron pointing its way, faded the way BumpStack's
        //  are (brighter under the pointer and while pressed, dimmed when there is no page that way). It activates once per
        //  click, and holds the stack weakly, since the stack retains it.
        private sealed class PageButton : ManagedButton
        {
            private readonly WeakReference<PageStack> m_owner;
            private readonly bool m_isForward;
            private bool m_isHorizontal = true;

            public PageButton (PageStack owner, bool isForward)
            {
                m_owner = new WeakReference<PageStack>(owner);
                m_isForward = isForward;
                this.Activated += this.OnActivated;
                this.AccessibilityLabel = isForward ? "Next page" : "Previous page";
            }

            private PageButton (NativeHandle handle) : base(handle)
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
                    this.NeedsDisplay = true;
                }
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                if ((m_owner == null) || !m_owner.TryGetTarget(out PageStack owner))
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

                // 2. The chevron, pointing the way the button pages
                BumpChevron.Stroke(this.Bounds, m_isForward, m_isHorizontal, owner.m_buttonFontSize, ThemedColors.Get(owner.m_buttonForegroundColorKey));

                context.EndTransparencyLayer();
            }

            private void OnActivated (object sender, EventArgs e)
            {
                if ((m_owner != null) && m_owner.TryGetTarget(out PageStack owner))
                {
                    owner.GoTo(owner.EffectiveSelectedIndex + (m_isForward ? 1 : -1));
                }
            }
        }

        // One dot in the strip: filled in the dot color, or the current dot color for the current page, and an edge dot
        //  (more pages past the window) smaller and faded, growing to full size, still faded, under the pointer. The whole
        //  dot fades with its state the way WinUI3's do. A change of kind or of the pointer eases its size and fade over a
        //  short, even step, as WinUI3's visual state transition does, stepped by the stack's dot ease timer; a new dot takes
        //  its first look at once. It holds the stack weakly, since the stack retains it.
        private sealed class PageDot : ManagedButton
        {
            private const double kNormalOpacity = 0.85;
            private const double kHighlightedOpacity = 1.0;
            private const double kDisabledOpacity = 0.4;
            private const double kEdgeOpacity = 0.45;
            private const double kEdgeScale = 0.6;
            private const double kEaseSeconds = 0.15;

            private readonly WeakReference<PageStack> m_owner;
            private ePageStackDotKind m_kind;
            private bool m_hasLook;
            private bool m_isEasing;
            private double m_scale = 1.0;
            private double m_fade = 1.0;
            private double m_fromScale;
            private double m_fromFade;
            private double m_toScale = 1.0;
            private double m_toFade = 1.0;
            private long m_easeStartTimestamp;

            public PageDot (PageStack owner)
            {
                m_owner = new WeakReference<PageStack>(owner);
                this.Activated += this.OnActivated;
            }

            private PageDot (NativeHandle handle) : base(handle)
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

            // A dot leaves the keyboard to the stack
            public override bool AcceptsFirstResponder () => false;

            /// <summary>The page clicking this dot goes to</summary>
            public int PageIndex { get; set; }

            public override void MouseEntered (NSEvent theEvent)
            {
                base.MouseEntered(theEvent);
                this.UpdateLook(animate: true);
            }

            public override void MouseExited (NSEvent theEvent)
            {
                base.MouseExited(theEvent);
                this.UpdateLook(animate: true);
            }

            /// <summary>Sets what the dot stands for, easing to its look for that unless it is new</summary>
            public void SetKind (ePageStackDotKind kind)
            {
                m_kind = kind;
                this.UpdateLook(animate: m_hasLook);
            }

            /// <summary>Steps an ease in progress for the time gone by: true while it is still easing</summary>
            public bool StepLook ()
            {
                if (!m_isEasing)
                {
                    return false;
                }

                double progress = Math.Min(1.0, Stopwatch.GetElapsedTime(m_easeStartTimestamp).TotalSeconds / kEaseSeconds);
                m_scale = m_fromScale + ((m_toScale - m_fromScale) * progress);
                m_fade = m_fromFade + ((m_toFade - m_fromFade) * progress);
                m_isEasing = progress < 1.0;
                this.NeedsDisplay = true;
                return m_isEasing;
            }

            /// <summary>Ends an ease in progress at its look</summary>
            public void LandLook ()
            {
                if (m_isEasing)
                {
                    m_isEasing = false;
                    m_scale = m_toScale;
                    m_fade = m_toFade;
                    this.NeedsDisplay = true;
                }
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                if ((m_owner == null) || !m_owner.TryGetTarget(out PageStack owner))
                {
                    return;
                }

                double opacity;
                switch (this.CurrentState)
                {
                    case eManagedButtonState.PointerOver:
                    case eManagedButtonState.Pressed:
                        opacity = kHighlightedOpacity;
                        break;

                    case eManagedButtonState.Disabled:
                        opacity = kDisabledOpacity;
                        break;

                    default:
                        opacity = kNormalOpacity;
                        break;
                }

                double size = owner.m_dotSize * m_scale;
                CGContext context = NSGraphicsContext.CurrentContext.CGContext;
                context.SetAlpha((nfloat)(opacity * m_fade));
                string colorKey = m_kind == ePageStackDotKind.Current ? owner.m_currentDotColorKey : owner.m_dotColorKey;
                ThemedColors.Get(colorKey).SetFill();

                CGRect bounds = this.Bounds;
                var dotRect = new CGRect(
                    (nfloat)((double)bounds.GetMidX() - (size / 2.0)),
                    (nfloat)((double)bounds.GetMidY() - (size / 2.0)),
                    (nfloat)size,
                    (nfloat)size
                );
                NSBezierPath.FromOvalInRect(dotRect).Fill();
            }

            // Aims the dot's size and fade at its kind's look, with the pointer taken into account for an edge dot, either
            //  at once or eased there
            private void UpdateLook (bool animate)
            {
                bool isEdge = (m_kind == ePageStackDotKind.BackEdge) || (m_kind == ePageStackDotKind.ForwardEdge);
                bool isPointerOver = (this.CurrentState == eManagedButtonState.PointerOver) || (this.CurrentState == eManagedButtonState.Pressed);
                double toScale = (isEdge && !isPointerOver) ? kEdgeScale : 1.0;
                double toFade = isEdge ? kEdgeOpacity : 1.0;
                if (m_hasLook && (toScale == m_toScale) && (toFade == m_toFade))
                {
                    return;
                }

                m_toScale = toScale;
                m_toFade = toFade;
                this.NeedsDisplay = true;
                if (!animate || !m_hasLook)
                {
                    m_hasLook = true;
                    m_isEasing = false;
                    m_scale = toScale;
                    m_fade = toFade;
                    return;
                }

                m_fromScale = m_scale;
                m_fromFade = m_fade;
                m_easeStartTimestamp = Stopwatch.GetTimestamp();
                m_isEasing = true;
                if ((m_owner == null) || !m_owner.TryGetTarget(out PageStack owner) || !owner.StartDotEase())
                {
                    this.LandLook();
                }
            }

            private void OnActivated (object sender, EventArgs e)
            {
                if ((m_owner != null) && m_owner.TryGetTarget(out PageStack owner))
                {
                    owner.GoTo(this.PageIndex);
                }
            }
        }

        // The page count: up to two counts and the format's own words around them, each drawn on its own so the words
        //  ("of") draw smaller and fainter than the numbers, in the dot color. Horizontal, the pieces share a baseline, with
        //  the words raised a little so they center on the numbers; vertical, each line is centered under the last. The
        //  pieces are measured when they are set, and drawn in the theme's color of the moment. It holds the stack weakly,
        //  since the stack retains it.
        private sealed class PageCountView : NSView
        {
            private const double kCountFontSize = 13.0;
            private const double kWordFontSize = 10.0;
            private const double kWordOpacity = 0.8;
            private const double kWordRaise = 2.0;

            private readonly WeakReference<PageStack> m_owner;
            private readonly List<Piece> m_pieces = new List<Piece>();

            public PageCountView (PageStack owner)
            {
                m_owner = new WeakReference<PageStack>(owner);
            }

            private PageCountView (NativeHandle handle) : base(handle)
            {
            }

            public override bool IsFlipped => true;
            public override bool AccessibilityElement => true;
            public override string AccessibilityRole => NSAccessibilityRoles.StaticTextRole;

            /// <summary>The size the pieces take together, as last set</summary>
            public CGSize ContentSize { get; private set; }

            /// <summary>Sets the pieces to draw, measuring and placing each for the orientation</summary>
            public void SetParts (PageStackRules.PageCountParts parts, bool isVertical)
            {
                // 1. The pieces in order, a line each when vertical
                m_pieces.Clear();
                _Add(parts.LeadingWords, isWord: true);
                _Add(parts.FirstCount, isWord: false);
                _Add(parts.MiddleWords, isWord: true);
                _Add(parts.SecondCount, isWord: false);
                _Add(parts.TrailingWords, isWord: true);

                // 2. Place them
                double width = 0.0;
                double height = 0.0;
                if (isVertical)
                {
                    foreach (Piece piece in m_pieces)
                    {
                        width = Math.Max(width, (double)piece.Size.Width);
                    }

                    for (int i = 0; i < m_pieces.Count; ++i)
                    {
                        Piece piece = m_pieces[i];
                        m_pieces[i] = piece with { Origin = new CGPoint((nfloat)((width - (double)piece.Size.Width) / 2.0), (nfloat)height) };
                        height += (double)piece.Size.Height;
                    }
                }
                else
                {
                    // Each piece's top sits so the baselines meet, the words raised, then all shift so the highest is at zero
                    double maxAscender = 0.0;
                    foreach (Piece piece in m_pieces)
                    {
                        maxAscender = Math.Max(maxAscender, (double)FontFor(piece.IsWord).Ascender);
                    }

                    double minTop = double.MaxValue;
                    var tops = new double[m_pieces.Count];
                    for (int i = 0; i < m_pieces.Count; ++i)
                    {
                        tops[i] = maxAscender - (double)FontFor(m_pieces[i].IsWord).Ascender - (m_pieces[i].IsWord ? kWordRaise : 0.0);
                        minTop = Math.Min(minTop, tops[i]);
                    }

                    for (int i = 0; i < m_pieces.Count; ++i)
                    {
                        Piece piece = m_pieces[i];
                        double top = tops[i] - minTop;
                        m_pieces[i] = piece with { Origin = new CGPoint((nfloat)width, (nfloat)top) };
                        width += (double)piece.Size.Width;
                        height = Math.Max(height, top + (double)piece.Size.Height);
                    }
                }

                this.ContentSize = new CGSize((nfloat)width, (nfloat)height);
                this.NeedsDisplay = true;

                void _Add (string text, bool isWord)
                {
                    if (String.IsNullOrEmpty(text))
                    {
                        return;
                    }

                    string[] lines = isVertical ? text.Split('\n') : new[] { text };
                    foreach (string line in lines)
                    {
                        CGSize size = new NSAttributedString(line, new NSStringAttributes { Font = FontFor(isWord) }).Size;
                        m_pieces.Add(new Piece(line, isWord, size, CGPoint.Empty));
                    }
                }
            }

            public override void ViewDidChangeEffectiveAppearance ()
            {
                base.ViewDidChangeEffectiveAppearance();
                this.NeedsDisplay = true;
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                if ((m_owner == null) || !m_owner.TryGetTarget(out PageStack owner))
                {
                    return;
                }

                NSColor countColor = ThemedColors.Get(owner.m_dotColorKey);
                NSColor wordColor = countColor.ColorWithAlphaComponent((nfloat)((double)countColor.AlphaComponent * kWordOpacity));
                foreach (Piece piece in m_pieces)
                {
                    var text = new NSAttributedString(
                        piece.Text,
                        new NSStringAttributes
                        {
                            Font = FontFor(piece.IsWord),
                            ForegroundColor = piece.IsWord ? wordColor : countColor,
                        }
                    );
                    text.DrawAtPoint(piece.Origin);
                }
            }

            private static NSFont FontFor (bool isWord) => NSFont.SystemFontOfSize((nfloat)(isWord ? kWordFontSize : kCountFontSize));

            // One piece of the count: its text, whether it is the format's own words, its measured size, and where it draws
            private readonly record struct Piece (string Text, bool IsWord, CGSize Size, CGPoint Origin);
        }
    }
}
