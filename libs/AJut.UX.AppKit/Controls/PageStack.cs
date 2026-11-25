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
    // writing to the consumer's own view. The hosts, page buttons and dots are placed by frame in Layout, the hosts in a
    // view that clips to its bounds. A page that is not the shown one takes no clicks, and one that can't be seen at all
    // is hidden, which also takes it out of the key view loop. Across the stack it is as thick as its thickest page (plus
    // the dot strip), as WinUI3's sizes to its largest page; along the stack it has no size of its own.
    //
    // A transition steps on a repeating timer at display rate, started when a transition starts and invalidated when it
    // lands, when the stack leaves its window, and on dispose.
    //
    // Lifetime: the page buttons and dots are retained by the stack natively, so each holds the stack weakly and asks for
    //  it when it needs it. The stack's handler on its own Children collection is a self-subscription.
    /// <summary>
    /// Shows one of its children at a time as a page, with a page button at each end and an optional dot strip, and runs an
    /// animated transition to whichever page is picked. The neighbor pages peek in, faded, past the page buttons.
    /// </summary>
    public class PageStack : ThemedView
    {
        private const double kBorderThickness = 1.0;
        private const double kDotPadding = 3.0;
        private const double kDotStripGap = 4.0;
        private const double kTransitionFrameSeconds = 1.0 / 60.0;
        private const ushort kLeftArrowKeyCode = 123;
        private const ushort kRightArrowKeyCode = 124;
        private const ushort kDownArrowKeyCode = 125;
        private const ushort kUpArrowKeyCode = 126;
        private const ushort kHomeKeyCode = 115;
        private const ushort kEndKeyCode = 119;

        private readonly NSView m_pagesClip;
        private readonly List<PageHost> m_pageHosts = new List<PageHost>();
        private readonly List<PageDot> m_dots = new List<PageDot>();
        private readonly PageButton m_backButton;
        private readonly PageButton m_forwardButton;
        private NSUserInterfaceLayoutOrientation m_orientation = NSUserInterfaceLayoutOrientation.Horizontal;
        private int m_selectedIndex;
        private bool m_showDots;
        private int m_dotReach = 5;
        private double m_pageSpacing = 8.0;
        private double m_pagePeek = 16.0;
        private double m_inactivePageOpacity = 0.25;
        private double m_buttonFontSize = 12.0;
        private NSEdgeInsets m_buttonPadding = new NSEdgeInsets(0, 4, 0, 4);
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

        // ===========[ Setup/Construction/Teardown ]===========================
        public PageStack ()
        {
            m_pagesClip = new NSView { ClipsToBounds = true };
            this.AddSubview(m_pagesClip);

            m_backButton = new PageButton(this, isForward: false);
            m_forwardButton = new PageButton(this, isForward: true);
            m_pagesClip.AddSubview(m_backButton);
            m_pagesClip.AddSubview(m_forwardButton);

            this.Children = new ObservableCollection<NSView>();
            this.Children.CollectionChanged += this.OnChildrenChanged;
            this.ApplyOrientation();
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
            if (PageStackRules.IsTransitionDone(elapsed, duration))
            {
                this.StopTransition();
            }

            this.NeedsLayout = true;
        }

        public override void ViewDidMoveToWindow ()
        {
            base.ViewDidMoveToWindow();

            // Lands any running transition, which invalidates its timer
            if ((this.Window == null) && (m_transitionTimer != null))
            {
                this.JumpTo((int)m_transitionTo);
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
                this.NeedsLayout = true;
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

        /// <summary>The <see cref="ThemedColors"/> key every dot but the current page's fills with</summary>
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
            }
        }

        public override bool AcceptsFirstResponder () => true;
        public override CGRect FocusRingMaskBounds => this.Bounds;

        // Across the stack it is as thick as its thickest page, plus the dot strip; along it, it has no size of its own.
        //  The thickness is measured in Layout, never here: AppKit asks for this inside its constraint pass, where asking
        //  for a fitting size is not safe.
        public override CGSize IntrinsicContentSize
        {
            get
            {
                if (m_pagesClip == null)
                {
                    return base.IntrinsicContentSize;
                }

                double thickness = m_pageThickness + this.DotStripThickness();
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

            this.LayoutPagesAndButtons();
            this.LayoutDots();
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

            // Under the page buttons, so they draw over the peeks
            m_pagesClip.AddSubview(host, NSWindowOrderingMode.Below, m_backButton);
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

        // A page button's length along the stack: its chevron, the padding on either side of it along the stack, and its outline
        private double ButtonLength ()
        {
            return this.IsHorizontal
                ? (double)(m_buttonPadding.Left + m_buttonPadding.Right) + m_buttonFontSize + (2.0 * kBorderThickness)
                : (double)(m_buttonPadding.Top + m_buttonPadding.Bottom) + m_buttonFontSize + (2.0 * kBorderThickness);
        }

        private double DotCellLength () => m_dotSize + (2.0 * kDotPadding);

        private double DotStripThickness ()
        {
            return PageStackRules.AreDotsShown(m_showDots, this.Children.Count) ? this.DotCellLength() + kDotStripGap : 0.0;
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

        // Places and fades each page for where the stack is now, mid transition or not, and the page buttons at the ends
        private void LayoutPagesAndButtons ()
        {
            CGRect bounds = this.Bounds;
            double dotStrip = this.DotStripThickness();
            CGRect pagesArea = this.IsHorizontal
                ? new CGRect(0, 0, bounds.Width, (nfloat)Math.Max(0.0, (double)bounds.Height - dotStrip))
                : new CGRect(0, 0, (nfloat)Math.Max(0.0, (double)bounds.Width - dotStrip), bounds.Height);
            m_pagesClip.Frame = pagesArea;

            int pageCount = this.Children.Count;
            int shownPage = this.EffectiveSelectedIndex;
            bool areButtonsShown = PageStackRules.ArePageButtonsShown(pageCount);
            double buttonLength = this.ButtonLength();
            double clearance = PageStackRules.PageClearance(areButtonsShown, buttonLength, m_pagePeek, m_pageSpacing);
            double width = (double)pagesArea.Width;
            double height = (double)pagesArea.Height;
            double pageLength = Math.Max(0.0, (this.IsHorizontal ? width : height) - (2.0 * clearance));

            for (int i = 0; i < m_pageHosts.Count; ++i)
            {
                PageHost host = m_pageHosts[i];
                double start = clearance + PageStackRules.PageOffset(i, m_position, pageLength, m_pageSpacing);
                host.Frame = this.IsHorizontal
                    ? new CGRect((nfloat)start, 0, (nfloat)pageLength, (nfloat)height)
                    : new CGRect(0, (nfloat)start, (nfloat)width, (nfloat)pageLength);
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
                m_backButton.Frame = new CGRect(0, 0, (nfloat)buttonLength, (nfloat)height);
                m_forwardButton.Frame = new CGRect((nfloat)(width - buttonLength), 0, (nfloat)buttonLength, (nfloat)height);
            }
            else
            {
                m_backButton.Frame = new CGRect(0, 0, (nfloat)width, (nfloat)buttonLength);
                m_forwardButton.Frame = new CGRect(0, (nfloat)(height - buttonLength), (nfloat)width, (nfloat)buttonLength);
            }
        }

        // Matches the dots to the dot window, reusing the ones there are, and centers them in the strip
        private void LayoutDots ()
        {
            int pageCount = this.Children.Count;
            bool areDotsShown = PageStackRules.AreDotsShown(m_showDots, pageCount);
            PageStackDotWindow window = areDotsShown ? PageStackDotWindow.For(pageCount, this.EffectiveSelectedIndex, m_dotReach) : default;
            int dotCount = areDotsShown ? window.DotCount : 0;

            // 1. Match the dot count
            while (m_dots.Count > dotCount)
            {
                PageDot extra = m_dots[m_dots.Count - 1];
                m_dots.RemoveAt(m_dots.Count - 1);
                extra.RemoveFromSuperview();
                extra.Dispose();
            }

            while (m_dots.Count < dotCount)
            {
                var dot = new PageDot(this);
                m_dots.Add(dot);
                this.AddSubview(dot);
            }

            if (dotCount == 0)
            {
                return;
            }

            // 2. Fill and place each, the row centered along the stack in the strip past the pages
            CGRect bounds = this.Bounds;
            double cell = this.DotCellLength();
            double rowLength = cell * dotCount;
            double stripStart = (this.IsHorizontal ? (double)bounds.Height : (double)bounds.Width) - cell;
            double rowStart = ((this.IsHorizontal ? (double)bounds.Width : (double)bounds.Height) - rowLength) / 2.0;
            for (int i = 0; i < dotCount; ++i)
            {
                PageStackDotWindow.Dot info = window.DotAt(i);
                PageDot dot = m_dots[i];
                dot.Info = info;
                dot.AccessibilityLabel = $"Page {info.PageIndex + 1} of {pageCount}";
                double along = rowStart + (i * cell);
                dot.Frame = this.IsHorizontal
                    ? new CGRect((nfloat)along, (nfloat)stripStart, (nfloat)cell, (nfloat)cell)
                    : new CGRect((nfloat)stripStart, (nfloat)along, (nfloat)cell, (nfloat)cell);
                dot.NeedsDisplay = true;
            }
        }

        private void RedrawButtonsAndDots ()
        {
            if (m_backButton == null)
            {
                return;
            }

            m_backButton.NeedsDisplay = true;
            m_forwardButton.NeedsDisplay = true;
            foreach (PageDot dot in m_dots)
            {
                dot.NeedsDisplay = true;
            }
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
        //  (more pages past the window) smaller and faded. The whole dot fades with its state the way WinUI3's do. It holds
        //  the stack weakly, since the stack retains it.
        private sealed class PageDot : ManagedButton
        {
            private const double kNormalOpacity = 0.85;
            private const double kHighlightedOpacity = 1.0;
            private const double kDisabledOpacity = 0.4;
            private const double kEdgeOpacity = 0.45;
            private const double kEdgeScale = 0.6;

            private readonly WeakReference<PageStack> m_owner;

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

            public PageStackDotWindow.Dot Info { get; set; }

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

                double size = owner.m_dotSize;
                if (this.Info.IsEdge)
                {
                    opacity *= kEdgeOpacity;
                    size *= kEdgeScale;
                }

                CGContext context = NSGraphicsContext.CurrentContext.CGContext;
                context.SetAlpha((nfloat)opacity);
                string colorKey = this.Info.Kind == ePageStackDotKind.Current ? owner.m_currentDotColorKey : owner.m_dotColorKey;
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

            private void OnActivated (object sender, EventArgs e)
            {
                if ((m_owner != null) && m_owner.TryGetTarget(out PageStack owner))
                {
                    owner.GoTo(this.Info.PageIndex);
                }
            }
        }
    }
}
