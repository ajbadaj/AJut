namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics;
    using Android.Runtime;
    using Android.Util;
    using Android.Views;
    using Android.Widget;
    using Orientation = Android.Widget.Orientation;

    // ===========[ PageStack ]==================================================
    // BumpStack's sibling for paging, the Android port of AppKit's PageStack: one child (a page) shows at a time, with a
    // page button at each end and, optionally, a strip of dots. A tap on a page button or a dot runs an animated
    // transition to that page; the buttons activate once per tap, they never repeat. The neighbor pages sit just past the
    // shown one, faded and mostly clipped away, so a sliver of each shows past its page button as a hint that there is
    // more. Which page shows, where each page sits, how faded it is, how a transition eases, how a swipe follows the finger
    // and where it lands, and which dots the strip shows are AJut.UX's PageStackRules and PageStackDotRange, which every
    // other PageStack follows too. The look (the keys, the dot fades and the count's sizes) is MobileLook.PageStack's,
    // which UIKit's PageStack draws too.
    //
    // Each child goes in a page host the stack owns, filling it, so the stack can move and fade a page without writing to
    // the consumer's own view. The hosts sit in a view that clips to its bounds, each laid out where the shown page goes,
    // and a page is moved along the stack by its host's translation, so a transition or a swipe moves views every frame
    // without a layout pass. The page buttons run the stack's full height (width, when vertical), past the dot strip. A
    // page that is not the shown one takes no touches and no focus and is hidden from accessibility, and one that can't be
    // seen at all is invisible. Across the stack it is as thick as its thickest page measured at the page length (plus the
    // page padding and the dot strip); along the stack it takes the room it is given.
    //
    // Swiping (IsSwipeEnabled, on by default): a drag that goes further along the stack than across it, past the touch
    // slop, is taken from whatever is under the finger (OnInterceptTouchEvent), and the parents are asked not to take it
    // back, so a swipe that starts on a page's own button still pages. The pages then follow the finger from wherever they
    // were, mid transition or not, resisting past either end. Let go, the stack lands on the page PageStackRules picks for
    // where the pages are and how fast the finger was moving (in dp per second, the rules' units), with an eased
    // transition from where the finger left them; a canceled swipe goes back to the page it was on. A drag mostly across
    // the stack is left alone, so a scroller around it still scrolls. On a page that scrolls the same way as the stack, a
    // drag that starts on the stack pages it rather than scrolling the page; a drag that starts anywhere else on the page
    // still scrolls it. A second finger can take over a swipe when the first lifts.
    //
    // The dot strip slides with the pages: a pick turns its dot current at once, and the strip then shifts by however many
    // dots the pick was away, on the same clock and easing as the page transition, so the current dot settles back where
    // the range puts it. While it slides the strip holds the dots of both ranges, so the ones leaving slide out of view
    // rather than vanish. Each dot stays with its page through a slide, so only a dot whose kind changes eases its size.
    // A jump too far for the two strips to share a dot takes its new place at once. An edge dot grows to full size while
    // pressed (or under a mouse), staying faded, so it reads as something to tap.
    //
    // A transition steps on a Choreographer frame callback, posted each frame while a transition runs and taken off when
    // it lands, when the stack leaves its window, and on dispose. A dot easing to a new size or fade steps on a second one,
    // which runs only while a dot is easing and is let go of the same ways. Each callback holds the stack weakly, since
    // the Choreographer holds the callback while it is posted. The stack's handler on its own Children collection, and
    // each button's and dot's handler on its own Click, are self-subscriptions.
    /// <summary>
    /// Shows one of its children at a time as a page, with a page button at each end and an optional dot strip, and runs an
    /// animated transition to whichever page is picked or swiped to. The neighbor pages peek in, faded, past the page
    /// buttons.
    /// </summary>
    /// <remarks>
    /// In a layout, the views declared inside the tag become the pages, in order. ajutOrientation, ajutSelectedIndex,
    /// ajutShowDots, ajutDotReach, ajutShowPageCount, ajutPageCountFormat, ajutTransitionDuration (milliseconds),
    /// ajutIsSwipeEnabled, ajutPageSpacing, ajutPagePeek, ajutInactivePageOpacity and ajutDotSize set the matching
    /// properties, and android:enabled disables the stack. Lengths are in pixels, as every Android view's are; the defaults
    /// are given in dp.
    /// </remarks>
    [Register("ajut.ux.controls.PageStack")]
    public class PageStack : ViewGroup
    {
        private const float kBorderThicknessDp = 1f;
        private const float kDotPaddingDp = 3f;
        private const float kDefaultDotStripGapDp = 4f;
        private const float kDefaultPageGapDp = 5f;
        private const float kPageCountGapDp = 8f;
        private const float kDefaultPageSpacingDp = 8f;
        private const float kDefaultPagePeekDp = 16f;
        private const float kDefaultButtonFontSizeDp = 12f;
        private const float kDefaultButtonPaddingDp = 4f;
        private const float kDefaultDotSizeDp = 8f;
        private const int kDefaultDotReach = 5;
        private const double kDefaultInactivePageOpacity = 0.25;
        private const int kDefaultTransitionMilliseconds = 250;
        private const int kVelocityUnitsPerSecond = 1000;

        // The unthemed look is WinUI3's unthemed PageStack's: the solid base fill for the page buttons, the secondary text
        // for the chevrons and dots, the primary text for the current dot, and the control stroke for the outline
        private static readonly ThemedColor kButtonBackground = ThemedColor.FromArgb(MobileLook.PageStack.ButtonBackground, 0xFFF3F3F3, 0xFF202020);
        private static readonly ThemedColor kButtonForeground = ThemedColor.FromArgb(MobileLook.PageStack.ButtonForeground, 0x9E000000, 0xC5FFFFFF);
        private static readonly ThemedColor kButtonBorder = ThemedColor.FromArgb(MobileLook.PageStack.ButtonBorder, 0x29000000, 0x18FFFFFF);
        private static readonly ThemedColor kDot = ThemedColor.FromArgb(MobileLook.PageStack.Dot, 0x9E000000, 0xC5FFFFFF);
        private static readonly ThemedColor kCurrentDot = ThemedColor.FromArgb(MobileLook.PageStack.CurrentDot, 0xE4000000, 0xFFFFFFFF);

        private readonly List<PageHost> m_pageHosts = new List<PageHost>();
        private readonly List<PageDot> m_dots = new List<PageDot>();
        private ClipBox m_pagesClip = null!;
        private ClipBox m_dotsClip = null!;
        private PageCountView m_pageCount = null!;
        private PageButton m_backButton = null!;
        private PageButton m_forwardButton = null!;
        private FrameTicker m_transitionTicker = null!;
        private FrameTicker m_dotEaseTicker = null!;
        private float m_density = 1f;
        private int m_touchSlop;
        private Orientation m_orientation = Orientation.Horizontal;
        private int m_selectedIndex;
        private bool m_showDots;
        private int m_dotReach = kDefaultDotReach;
        private bool m_showPageCount;
        private string m_pageCountFormat = PageStackRules.kDefaultPageCountFormat;
        private float m_pageSpacing;
        private float m_pagePeek;
        private double m_inactivePageOpacity = kDefaultInactivePageOpacity;
        private float m_buttonFontSize;
        private Insets m_buttonPadding;
        private Insets? m_pagePadding;
        private Insets? m_dotStripPadding;
        private float m_dotSize;
        private ThemedColor m_buttonBackgroundColor = kButtonBackground;
        private ThemedColor m_buttonForegroundColor = kButtonForeground;
        private ThemedColor m_buttonBorderColor = kButtonBorder;
        private ThemedColor m_dotColor = kDot;
        private ThemedColor m_currentDotColor = kCurrentDot;
        private float m_pageLength;
        private double m_position;
        private double m_transitionFrom;
        private double m_transitionTo;
        private long m_transitionStartTimestamp;
        private bool m_isTransitioning;
        private int m_dotSpanFirstPage;
        private int m_rangeDotCount;
        private double m_dotScroll;
        private double m_dotScrollFrom;
        private int m_dotScrollTo;
        private bool m_isDotSliding;
        private VelocityTracker? m_velocityTracker;
        private bool m_isSwipeEnabled = true;
        private int m_activePointerId;
        private bool m_isSwipeCandidate;
        private bool m_isSwiping;
        private float m_downX;
        private float m_downY;
        private float m_swipeOrigin;
        private double m_swipeStartPosition;

        // ===========[ Setup/Construction/Teardown ]===========================
        public PageStack (Context context) : base(context)
        {
            this.Initialize(context, null);
        }

        public PageStack (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            this.Initialize(context, attrs);
        }

        public PageStack (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.Initialize(context, attrs);
        }

        /// <summary>
        /// Used by the runtime to wrap a Java stack whose managed peer is gone. It only answers Android's calls: none of the
        /// managed state survives, so dispose a stack only once Android is done with it.
        /// </summary>
        protected PageStack (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        private void Initialize (Context context, IAttributeSet? attrs)
        {
            m_density = context.Resources?.DisplayMetrics?.Density ?? 1f;
            const float kFallbackTouchSlopDp = 8f;
            m_touchSlop = ViewConfiguration.Get(context)?.ScaledTouchSlop ?? (int)(kFallbackTouchSlopDp * m_density);
            m_pageSpacing = kDefaultPageSpacingDp * m_density;
            m_pagePeek = kDefaultPagePeekDp * m_density;
            m_buttonFontSize = kDefaultButtonFontSizeDp * m_density;
            int buttonPadding = (int)(kDefaultButtonPaddingDp * m_density);
            m_buttonPadding = new Insets(buttonPadding, 0, buttonPadding, 0);
            m_dotSize = kDefaultDotSizeDp * m_density;
            m_transitionTicker = new FrameTicker(this, isDotEase: false);
            m_dotEaseTicker = new FrameTicker(this, isDotEase: true);

            // 1. The parts, the page buttons over the pages so they draw over the peeks
            m_pagesClip = new ClipBox(context);
            this.AddView(m_pagesClip);
            m_dotsClip = new ClipBox(context);
            this.AddView(m_dotsClip);
            m_pageCount = new PageCountView(context, this);
            this.AddView(m_pageCount);
            m_backButton = new PageButton(context, this, isForward: false);
            m_forwardButton = new PageButton(context, this, isForward: true);
            this.AddView(m_backButton);
            this.AddView(m_forwardButton);

            // 2. Taken by the keyboard for the arrow keys, Home and End, but never by a touch, which goes to the pages
            this.Focusable = true;
            this.FocusableInTouchMode = false;

            // A scroller around the stack is asked about a drag before the stack is, since its intercept runs first, and one
            //  that scrolls the same way as the stack takes the drag past the touch slop before the stack can tell it is a
            //  swipe. As a nested scrolling child the stack starts a nested scroll along its axis as the press goes down,
            //  and a ScrollView around it then leaves the drag alone. A scroller that runs the other way never wanted the
            //  drag, so the nested scroll changes nothing there. A scroller inside a page needs none of this: the stack's
            //  own intercept sees the drag before that scroller does.
            this.NestedScrollingEnabled = true;
            this.Children.CollectionChanged += this.OnChildrenChanged;

            // 3. Layout attributes
            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutPageStack);
                m_orientation = (Orientation)values.GetInt(Resource.Styleable.AJutPageStack_ajutOrientation, (int)Orientation.Horizontal);
                this.Enabled = values.GetBoolean(Resource.Styleable.AJutPageStack_android_enabled, true);
                m_selectedIndex = values.GetInt(Resource.Styleable.AJutPageStack_ajutSelectedIndex, 0);
                m_showDots = values.GetBoolean(Resource.Styleable.AJutPageStack_ajutShowDots, false);
                m_dotReach = values.GetInt(Resource.Styleable.AJutPageStack_ajutDotReach, kDefaultDotReach);
                m_showPageCount = values.GetBoolean(Resource.Styleable.AJutPageStack_ajutShowPageCount, false);
                m_pageCountFormat = values.GetString(Resource.Styleable.AJutPageStack_ajutPageCountFormat) ?? PageStackRules.kDefaultPageCountFormat;
                int transitionMilliseconds = values.GetInt(Resource.Styleable.AJutPageStack_ajutTransitionDuration, kDefaultTransitionMilliseconds);
                this.TransitionDuration = TimeSpan.FromMilliseconds(transitionMilliseconds);
                this.IsSwipeEnabled = values.GetBoolean(Resource.Styleable.AJutPageStack_ajutIsSwipeEnabled, true);
                m_pageSpacing = values.GetDimension(Resource.Styleable.AJutPageStack_ajutPageSpacing, m_pageSpacing);
                m_pagePeek = values.GetDimension(Resource.Styleable.AJutPageStack_ajutPagePeek, m_pagePeek);
                m_inactivePageOpacity = values.GetFloat(Resource.Styleable.AJutPageStack_ajutInactivePageOpacity, (float)kDefaultInactivePageOpacity);
                m_dotSize = values.GetDimension(Resource.Styleable.AJutPageStack_ajutDotSize, m_dotSize);
                values.Recycle();
            }

            this.ApplyOrientation();
            this.RefreshDots();
        }

        // Views declared inside the tag were added as the stack's own children: they become its pages
        protected override void OnFinishInflate ()
        {
            base.OnFinishInflate();
            var declared = new List<View>();
            for (int index = 0; index < this.ChildCount; ++index)
            {
                View? child = this.GetChildAt(index);
                if ((child != null) && !this.IsPart(child))
                {
                    declared.Add(child);
                }
            }

            foreach (View page in declared)
            {
                this.RemoveView(page);
                this.Children.Add(page);
            }
        }

        protected override void OnDetachedFromWindow ()
        {
            // Lands any running transition, swipe and dot ease, which takes their frame callbacks off
            if (m_isSwiping || m_isSwipeCandidate)
            {
                this.EndSwipeTracking();
                this.JumpTo(this.EffectiveSelectedIndex);
            }
            else if (m_isTransitioning)
            {
                this.JumpTo((int)m_transitionTo);
            }

            this.StopDotEase();
            foreach (PageDot dot in m_dots)
            {
                dot.LandLook();
            }

            base.OnDetachedFromWindow();
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing && (m_transitionTicker != null))
            {
                this.StopTransition();
                this.StopDotEase();
                this.EndSwipeTracking();
                this.Children.CollectionChanged -= this.OnChildrenChanged;
                m_transitionTicker.Dispose();
                m_dotEaseTicker.Dispose();
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        /// <summary>
        /// Raised when <see cref="SelectedIndex"/> changes, whether set from code, a tap, a swipe, a key, or a page added or
        /// removed
        /// </summary>
        public event EventHandler<EventArgs>? SelectedIndexChanged;

        private void OnChildrenChanged (object? sender, NotifyCollectionChangedEventArgs e)
        {
            int oldPageCount = m_pageHosts.Count;
            int selectedIndex = m_selectedIndex;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    for (int i = 0; i < e.NewItems!.Count; ++i)
                    {
                        this.InsertPageHost(e.NewStartingIndex + i, (View)e.NewItems[i]!);
                    }

                    selectedIndex = PageStackRules.SelectedIndexAfterInsert(selectedIndex, oldPageCount, e.NewStartingIndex, e.NewItems.Count);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    for (int i = 0; i < e.OldItems!.Count; ++i)
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
            this.EndSwipeTracking();
            this.JumpTo(this.EffectiveSelectedIndex);
            this.RefreshDots();
            this.RefreshPageCount();
            this.RequestLayout();
            if (hasChanged)
            {
                this.SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // Steps the running transition, posting the next frame until it lands
        private void OnTransitionFrame ()
        {
            if (!m_isTransitioning)
            {
                return;
            }

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
            else
            {
                m_transitionTicker.Post();
            }

            this.PlacePages();
            this.PlaceDots();
        }

        // Steps every dot easing to a new look, posting the next frame while any still is
        private void OnDotEaseFrame ()
        {
            bool isAnyEasing = false;
            foreach (PageDot dot in m_dots)
            {
                isAnyEasing |= dot.StepLook();
            }

            if (isAnyEasing)
            {
                m_dotEaseTicker.Post();
            }
        }

        public override bool OnInterceptTouchEvent (MotionEvent? ev)
        {
            return ((ev != null) && this.TrackSwipe(ev)) || base.OnInterceptTouchEvent(ev);
        }

        public override bool OnTouchEvent (MotionEvent? e)
        {
            if (e == null)
            {
                return base.OnTouchEvent(e);
            }

            // Takes a down that nothing under the finger took, so it hears the drag that follows. The intercept has already
            //  started following that down, so following it again here would only start the tracking over.
            if (e.ActionMasked == MotionEventActions.Down)
            {
                if (!m_isSwipeCandidate)
                {
                    this.TrackSwipe(e);
                }

                return this.CanSwipe || base.OnTouchEvent(e);
            }

            return this.TrackSwipe(e) || base.OnTouchEvent(e);
        }

        // A page's own child that takes the touch for itself (a scroller, a web view) asks its parents to leave it be, the
        //  stack among them, so the stack lets go of a touch it was only watching. One it has already taken as a swipe
        //  it keeps: that request comes from the stack asking its own parents.
        public override void RequestDisallowInterceptTouchEvent (bool disallowIntercept)
        {
            if (disallowIntercept && !m_isSwiping)
            {
                this.EndSwipeTracking();
            }

            base.RequestDisallowInterceptTouchEvent(disallowIntercept);
        }

        public override bool OnKeyDown ([GeneratedEnum] Keycode keyCode, KeyEvent? e)
        {
            int target;
            bool isHorizontal = this.IsHorizontal;
            switch (keyCode)
            {
                case Keycode.DpadLeft when isHorizontal:
                case Keycode.DpadUp when !isHorizontal:
                    target = this.EffectiveSelectedIndex - 1;
                    break;

                case Keycode.DpadRight when isHorizontal:
                case Keycode.DpadDown when !isHorizontal:
                    target = this.EffectiveSelectedIndex + 1;
                    break;

                case Keycode.MoveHome:
                    target = 0;
                    break;

                case Keycode.MoveEnd:
                    target = this.Children.Count - 1;
                    break;

                default:
                    return base.OnKeyDown(keyCode, e);
            }

            return this.GoTo(target) || base.OnKeyDown(keyCode, e);
        }

        // ===========[ Properties ]============================================
        /// <summary>The pages, in order. Each fills the page area, so it takes the size the stack gives it.</summary>
        public ObservableCollection<View> Children { get; } = new ObservableCollection<View>();

        /// <summary>Which way the pages run: Horizontal (the default) or Vertical</summary>
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
                    // A set mid swipe wins, so the finger's next move or lift doesn't fight it
                    this.EndSwipeTracking();
                    m_selectedIndex = value;
                    this.BeginTransitionTo(this.EffectiveSelectedIndex);
                    this.RefreshDots(slideDots: m_isTransitioning);
                    this.RefreshPageCount();
                    this.SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>Whether a strip of dots shows under the pages (beside them when vertical), one per page in the dot range</summary>
        public bool ShowDots
        {
            get => m_showDots;
            set
            {
                m_showDots = value;
                this.RefreshDots();
                this.RefreshPageCount();
            }
        }

        /// <summary>
        /// How many dots show behind and ahead of the current page's dot: 5 shows 11 dots, plus a smaller, faded dot at
        /// each end while there are more pages past them. Tapping one of those goes to the page just past the range.
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
        public TimeSpan TransitionDuration { get; set; } = TimeSpan.FromMilliseconds(kDefaultTransitionMilliseconds);

        /// <summary>
        /// Whether a swipe along the stack pages it, the pages following the finger and landing on the nearest page when
        /// let go, or on the next one for a quick flick. On by default. The page buttons and dots page either way.
        /// </summary>
        public bool IsSwipeEnabled
        {
            get => m_isSwipeEnabled;
            set
            {
                m_isSwipeEnabled = value;
                if (!value)
                {
                    this.CancelSwipe();
                }
            }
        }

        /// <summary>The gap between one page and the next, in pixels; 8 dp by default</summary>
        public float PageSpacing
        {
            get => m_pageSpacing;
            set
            {
                m_pageSpacing = value;
                this.RequestLayout();
            }
        }

        /// <summary>How much of each neighbor page shows past its page button, in pixels, as a hint that there is more; 16 dp by default</summary>
        public float PagePeek
        {
            get => m_pagePeek;
            set
            {
                m_pagePeek = value;
                this.RequestLayout();
            }
        }

        /// <summary>
        /// The space around the pages, inside the page buttons, in pixels. Left unset (null), it is 5 dp on the side the
        /// pages would otherwise crowd: the top when horizontal, the left when vertical. When neither the dots nor the page
        /// count shows, the bottom (or right) gets 5 dp as well, since no strip sits there.
        /// </summary>
        public Rect? PagePadding
        {
            get => m_pagePadding?.ToRect();
            set
            {
                m_pagePadding = value == null ? null : Insets.From(value);
                this.RequestLayout();
            }
        }

        /// <summary>
        /// The space around the dot strip, in pixels. Left unset (null), it is 4 dp on the side facing the pages: the top
        /// when horizontal, the left when vertical. When vertical with no dots, the right gets 4 dp as well, so the page
        /// count stays off the edge.
        /// </summary>
        public Rect? DotStripPadding
        {
            get => m_dotStripPadding?.ToRect();
            set
            {
                m_dotStripPadding = value == null ? null : Insets.From(value);
                this.RequestLayout();
            }
        }

        /// <summary>How opaque the pages that are not shown draw, so their peeks read as hints rather than content</summary>
        public double InactivePageOpacity
        {
            get => m_inactivePageOpacity;
            set
            {
                m_inactivePageOpacity = value;
                this.PlacePages();
            }
        }

        /// <summary>The color the page buttons fill with: <see cref="MobileLook.PageStack.ButtonBackground"/> by default</summary>
        public ThemedColor ButtonBackgroundColor
        {
            get => m_buttonBackgroundColor;
            set
            {
                m_buttonBackgroundColor = value;
                this.RedrawButtonsAndDots();
            }
        }

        /// <summary>The color the page buttons' chevrons are drawn in: <see cref="MobileLook.PageStack.ButtonForeground"/> by default</summary>
        public ThemedColor ButtonForegroundColor
        {
            get => m_buttonForegroundColor;
            set
            {
                m_buttonForegroundColor = value;
                this.RedrawButtonsAndDots();
            }
        }

        /// <summary>
        /// The color the page buttons are outlined in, so they stand apart from the peeks under them:
        /// <see cref="MobileLook.PageStack.ButtonBorder"/> by default
        /// </summary>
        public ThemedColor ButtonBorderColor
        {
            get => m_buttonBorderColor;
            set
            {
                m_buttonBorderColor = value;
                this.RedrawButtonsAndDots();
            }
        }

        /// <summary>
        /// The color every dot but the current page's fills with, and the page count is drawn in:
        /// <see cref="MobileLook.PageStack.Dot"/> by default
        /// </summary>
        public ThemedColor DotColor
        {
            get => m_dotColor;
            set
            {
                m_dotColor = value;
                this.RedrawButtonsAndDots();
            }
        }

        /// <summary>The color the current page's dot fills with: <see cref="MobileLook.PageStack.CurrentDot"/> by default</summary>
        public ThemedColor CurrentDotColor
        {
            get => m_currentDotColor;
            set
            {
                m_currentDotColor = value;
                this.RedrawButtonsAndDots();
            }
        }

        /// <summary>The size of the page buttons' chevrons, in pixels (the glyph's font size on the desktop stacks); 12 dp by default</summary>
        public float ButtonFontSize
        {
            get => m_buttonFontSize;
            set
            {
                m_buttonFontSize = value;
                this.RequestLayout();
                this.RedrawButtonsAndDots();
            }
        }

        /// <summary>The space around each page button's chevron, in pixels; 4 dp on either side along the stack by default</summary>
        public Rect ButtonPadding
        {
            get => m_buttonPadding.ToRect();
            set
            {
                m_buttonPadding = Insets.From(value);
                this.RequestLayout();
            }
        }

        /// <summary>The diameter of a full size dot, in pixels; the edge dots draw smaller. 8 dp by default.</summary>
        public float DotSize
        {
            get => m_dotSize;
            set
            {
                m_dotSize = value;
                this.RequestLayout();
                this.RedrawButtonsAndDots();
            }
        }

        // A ViewGroup's Enabled does not reach its children, so the page buttons, dots and pages are set to match here
        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;
                if (m_backButton == null)
                {
                    return;
                }

                foreach (PageDot dot in m_dots)
                {
                    dot.Enabled = value;
                }

                this.CancelSwipe();
                this.PlacePages();
            }
        }

        private bool IsHorizontal => m_orientation == Orientation.Horizontal;
        private int EffectiveSelectedIndex => PageStackRules.EffectiveSelectedIndex(m_selectedIndex, this.Children.Count);
        private bool CanSwipe => this.IsSwipeEnabled && this.Enabled && (this.Children.Count > 1) && (m_pageLength > 0f);

        // ===========[ Public Interface Methods ]==============================
        // Along the stack it takes the room it is given; across, it is as thick as its thickest page measured at the page
        //  length, plus the page padding and the dot strip, unless it is given an exact size
        protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
        {
            if (m_pagesClip == null)
            {
                base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
                return;
            }

            bool isHorizontal = this.IsHorizontal;
            int alongSpec = isHorizontal ? widthMeasureSpec : heightMeasureSpec;
            int acrossSpec = isHorizontal ? heightMeasureSpec : widthMeasureSpec;
            int along = GetDefaultSize(0, alongSpec);

            // 1. The page length, and the thickest page at that length
            Insets padding = this.EffectivePagePadding();
            float buttonLength = this.ButtonLength();
            float clearance = (float)PageStackRules.PageClearance(
                PageStackRules.ArePageButtonsShown(this.Children.Count),
                buttonLength,
                m_pagePeek,
                m_pageSpacing
            );
            int paddingAlong = isHorizontal ? padding.Left + padding.Right : padding.Top + padding.Bottom;
            int paddingAcross = isHorizontal ? padding.Top + padding.Bottom : padding.Left + padding.Right;
            m_pageLength = Math.Max(0f, along - (2f * clearance) - paddingAlong);
            int pageLengthSpec = MeasureSpec.MakeMeasureSpec((int)m_pageLength, MeasureSpecMode.Exactly);
            int unspecified = MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified);
            int thickest = 0;
            foreach (PageHost host in m_pageHosts)
            {
                host.Measure(isHorizontal ? pageLengthSpec : unspecified, isHorizontal ? unspecified : pageLengthSpec);
                thickest = Math.Max(thickest, isHorizontal ? host.MeasuredHeight : host.MeasuredWidth);
            }

            // 2. The stack's own size
            float dotStrip = this.DotStripThickness();
            int across = ResolveSize((int)Math.Ceiling(thickest + paddingAcross + dotStrip), acrossSpec);
            this.SetMeasuredDimension(isHorizontal ? along : across, isHorizontal ? across : along);

            // 3. Every part at the size it is laid out at
            int pagesAcross = Math.Max(0, (int)(across - dotStrip));
            int pageThickness = Math.Max(0, pagesAcross - paddingAcross);
            int pageThicknessSpec = MeasureSpec.MakeMeasureSpec(pageThickness, MeasureSpecMode.Exactly);
            foreach (PageHost host in m_pageHosts)
            {
                host.Measure(isHorizontal ? pageLengthSpec : pageThicknessSpec, isHorizontal ? pageThicknessSpec : pageLengthSpec);
            }

            _MeasureExactly(m_pagesClip, isHorizontal ? along : pagesAcross, isHorizontal ? pagesAcross : along);
            int buttonLengthPixels = (int)Math.Round(buttonLength);
            _MeasureExactly(m_backButton, isHorizontal ? buttonLengthPixels : across, isHorizontal ? across : buttonLengthPixels);
            _MeasureExactly(m_forwardButton, isHorizontal ? buttonLengthPixels : across, isHorizontal ? across : buttonLengthPixels);
            int cell = (int)Math.Round(this.DotCellLength());
            foreach (PageDot dot in m_dots)
            {
                _MeasureExactly(dot, cell, cell);
            }

            int dotsLength = m_rangeDotCount * cell;
            _MeasureExactly(m_dotsClip, isHorizontal ? dotsLength : cell, isHorizontal ? cell : dotsLength);
            m_pageCount.Measure(unspecified, unspecified);

            static void _MeasureExactly (View view, int width, int height)
            {
                view.Measure(
                    MeasureSpec.MakeMeasureSpec(Math.Max(0, width), MeasureSpecMode.Exactly),
                    MeasureSpec.MakeMeasureSpec(Math.Max(0, height), MeasureSpecMode.Exactly)
                );
            }
        }

        protected override void OnLayout (bool changed, int l, int t, int r, int b)
        {
            if (m_pagesClip == null)
            {
                return;
            }

            int width = r - l;
            int height = b - t;
            float dotStrip = this.DotStripThickness();
            this.LayoutPagesAndButtons(width, height, dotStrip);
            this.LayoutDotStrip(width, height, dotStrip);
            this.PlacePages();
            this.PlaceDots();
        }

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

        private bool IsPart (View view)
        {
            return ReferenceEquals(view, m_pagesClip) || ReferenceEquals(view, m_dotsClip) || ReferenceEquals(view, m_pageCount)
                || ReferenceEquals(view, m_backButton) || ReferenceEquals(view, m_forwardButton);
        }

        private void ApplyOrientation ()
        {
            if (m_backButton == null)
            {
                return;
            }

            m_backButton.Invalidate();
            m_forwardButton.Invalidate();
            this.RefreshPageCount();
            this.RequestLayout();
        }

        private void InsertPageHost (int index, View page)
        {
            var host = new PageHost(this.Context!);
            host.AddView(page, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
            m_pageHosts.Insert(index, host);
            m_pagesClip.AddView(host);
        }

        private void RemovePageHostAt (int index)
        {
            PageHost host = m_pageHosts[index];
            m_pageHosts.RemoveAt(index);

            // Lets go of the page, so it can go into another parent
            host.RemoveAllViews();
            m_pagesClip.RemoveView(host);
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
            if ((pageIndex < 0) || !this.IsAttachedToWindow || (this.TransitionDuration <= TimeSpan.Zero))
            {
                this.JumpTo(pageIndex);
                return;
            }

            // Started from wherever the stack is now, so a pick during a transition or a swipe carries on from there
            m_transitionFrom = m_position;
            m_transitionTo = pageIndex;
            m_transitionStartTimestamp = Stopwatch.GetTimestamp();
            m_isTransitioning = true;
            m_transitionTicker.Post();
            this.PlacePages();
        }

        private void JumpTo (int pageIndex)
        {
            this.StopTransition();
            this.LandDotSlide();
            m_position = Math.Max(0, pageIndex);
            this.PlacePages();
            this.PlaceDots();
        }

        // Stops a running transition where it has got to
        private void StopTransition ()
        {
            m_isTransitioning = false;
            m_transitionTicker?.Cancel();
        }

        // Starts stepping the dots easing to a new size or fade, unless that is already running: false when the stack is in
        //  no window, where nothing would step them
        private bool StartDotEase ()
        {
            if (!this.IsAttachedToWindow)
            {
                return false;
            }

            m_dotEaseTicker.Post();
            return true;
        }

        private void StopDotEase ()
        {
            m_dotEaseTicker?.Cancel();
        }

        // Follows a touch for a swipe, from the intercept and from the stack's own touch handling alike: true once the
        //  stack has taken the drag as a swipe, so it has every event after
        private bool TrackSwipe (MotionEvent e)
        {
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    this.EndSwipeTracking();
                    if (!this.CanSwipe)
                    {
                        return false;
                    }

                    m_velocityTracker = VelocityTracker.Obtain();
                    m_velocityTracker?.AddMovement(e);
                    m_activePointerId = e.GetPointerId(0);
                    m_downX = e.GetX();
                    m_downY = e.GetY();
                    m_isSwipeCandidate = true;
                    this.StartNestedScroll(this.IsHorizontal ? ScrollAxis.Horizontal : ScrollAxis.Vertical);
                    return false;

                case MotionEventActions.PointerUp:
                    this.OnSwipePointerUp(e);
                    return m_isSwiping;

                case MotionEventActions.Move:
                    if (!m_isSwipeCandidate && !m_isSwiping)
                    {
                        return false;
                    }

                    // Followed by the finger it started with, or the one that took over when that one lifted
                    int pointerIndex = e.FindPointerIndex(m_activePointerId);
                    if (pointerIndex < 0)
                    {
                        return m_isSwiping;
                    }

                    m_velocityTracker?.AddMovement(e);
                    float x = e.GetX(pointerIndex);
                    float y = e.GetY(pointerIndex);
                    float along = this.IsHorizontal ? x - m_downX : y - m_downY;
                    float across = this.IsHorizontal ? y - m_downY : x - m_downX;
                    if (!m_isSwiping)
                    {
                        // Past the slop the drag says which way it is going: along the stack is a swipe, and across it is
                        //  left to whatever else wants it for the rest of the touch
                        if (Math.Max(Math.Abs(along), Math.Abs(across)) <= m_touchSlop)
                        {
                            return false;
                        }

                        if (!PageStackRules.IsSwipeAlongStack(along, across))
                        {
                            this.EndSwipeTracking();
                            return false;
                        }

                        this.BeginSwipe(along);
                    }

                    m_position = PageStackRules.SwipePosition(
                        m_swipeStartPosition,
                        along - m_swipeOrigin,
                        m_pageLength,
                        m_pageSpacing,
                        this.Children.Count
                    );
                    this.PlacePages();
                    return true;

                case MotionEventActions.Up:
                    if (m_isSwiping)
                    {
                        m_velocityTracker?.AddMovement(e);
                        this.EndSwipe(isCanceled: false);
                        return true;
                    }

                    this.EndSwipeTracking();
                    return false;

                case MotionEventActions.Cancel:
                    if (m_isSwiping)
                    {
                        this.EndSwipe(isCanceled: true);
                        return true;
                    }

                    this.EndSwipeTracking();
                    return false;
            }

            return m_isSwiping;
        }

        // When the finger being followed lifts with another still down, the other one takes over, as ScrollView does. The
        //  down point moves by however far apart the two fingers are, so the distance along the stack carries on from where
        //  it was rather than jumping to the new finger. The velocity starts over, since the new finger's movement has
        //  nothing to do with the old one's.
        private void OnSwipePointerUp (MotionEvent e)
        {
            int liftedIndex = e.ActionIndex;
            if (!(m_isSwipeCandidate || m_isSwiping) || (e.GetPointerId(liftedIndex) != m_activePointerId))
            {
                return;
            }

            int takingOverIndex = liftedIndex == 0 ? 1 : 0;
            m_downX += e.GetX(takingOverIndex) - e.GetX(liftedIndex);
            m_downY += e.GetY(takingOverIndex) - e.GetY(liftedIndex);
            m_activePointerId = e.GetPointerId(takingOverIndex);
            m_velocityTracker?.Clear();
        }

        // Takes the drag: the parents leave it be, and the pages are taken from wherever they are, mid transition or not
        private void BeginSwipe (float along)
        {
            m_isSwiping = true;
            m_swipeOrigin = along;
            this.Parent?.RequestDisallowInterceptTouchEvent(true);
            this.StopTransition();
            this.LandDotSlide();
            m_swipeStartPosition = m_position;
        }

        // Lands a swipe let go of on the page the rules pick for where the pages are and how fast the finger was going, or
        //  for a canceled one, back on the page it was on
        private void EndSwipe (bool isCanceled)
        {
            int target = this.EffectiveSelectedIndex;
            if (!isCanceled && (m_velocityTracker != null))
            {
                m_velocityTracker.ComputeCurrentVelocity(kVelocityUnitsPerSecond);
                float velocity = this.IsHorizontal ? m_velocityTracker.XVelocity : m_velocityTracker.YVelocity;
                target = PageStackRules.SwipeTargetPage(m_position, velocity / m_density, this.Children.Count);
            }

            this.EndSwipeTracking();
            if (target != this.EffectiveSelectedIndex)
            {
                // The setter ends the tracking too, which is already done
                this.SelectedIndex = target;
            }
            else
            {
                this.BeginTransitionTo(target);
            }
        }

        // Stops a swipe the stack can no longer take: one under way goes back to the page it was on, so the pages never stay
        //  between two pages, and one only being watched is let go of
        private void CancelSwipe ()
        {
            if (m_isSwiping)
            {
                this.EndSwipe(isCanceled: true);
            }
            else
            {
                this.EndSwipeTracking();
            }
        }

        private void EndSwipeTracking ()
        {
            if (m_isSwipeCandidate || m_isSwiping)
            {
                this.StopNestedScroll();
            }

            m_isSwiping = false;
            m_isSwipeCandidate = false;
            m_velocityTracker?.Recycle();
            m_velocityTracker = null;
        }

        // A page button's length along the stack: its chevron, the padding on either side of it along the stack, and its outline
        private float ButtonLength ()
        {
            float border = 2f * kBorderThicknessDp * m_density;
            return this.IsHorizontal
                ? m_buttonPadding.Left + m_buttonPadding.Right + m_buttonFontSize + border
                : m_buttonPadding.Top + m_buttonPadding.Bottom + m_buttonFontSize + border;
        }

        private float DotCellLength () => m_dotSize + (2f * kDotPaddingDp * m_density);

        // How thick the dot strip is across the stack: the dots or the page count, whichever is thicker, and its padding
        private float DotStripThickness ()
        {
            int pageCount = this.Children.Count;
            bool areDotsShown = PageStackRules.AreDotsShown(m_showDots, pageCount);
            bool isPageCountShown = PageStackRules.IsPageCountShown(m_showPageCount, pageCount);
            if (!areDotsShown && !isPageCountShown)
            {
                return 0f;
            }

            float dotsThickness = areDotsShown ? this.DotCellLength() : 0f;
            float countThickness = isPageCountShown ? (this.IsHorizontal ? m_pageCount.ContentHeight : m_pageCount.ContentWidth) : 0f;
            Insets padding = this.EffectiveDotStripPadding();
            float paddingAcross = this.IsHorizontal ? padding.Top + padding.Bottom : padding.Left + padding.Right;
            return (float)Math.Ceiling(Math.Max(dotsThickness, countThickness) + paddingAcross);
        }

        // Lays each page host out where the shown page goes, inside the page padding, and the page buttons at the ends,
        //  the whole way across the stack. Where each page sits for the stack's position is its translation (PlacePages).
        private void LayoutPagesAndButtons (int width, int height, float dotStrip)
        {
            bool isHorizontal = this.IsHorizontal;
            int pagesWidth = isHorizontal ? width : Math.Max(0, (int)(width - dotStrip));
            int pagesHeight = isHorizontal ? Math.Max(0, (int)(height - dotStrip)) : height;
            m_pagesClip.Layout(0, 0, pagesWidth, pagesHeight);

            bool areButtonsShown = PageStackRules.ArePageButtonsShown(this.Children.Count);
            float buttonLength = this.ButtonLength();
            int clearance = (int)Math.Round(PageStackRules.PageClearance(areButtonsShown, buttonLength, m_pagePeek, m_pageSpacing));
            Insets padding = this.EffectivePagePadding();
            foreach (PageHost host in m_pageHosts)
            {
                int left = isHorizontal ? clearance + padding.Left : padding.Left;
                int top = isHorizontal ? padding.Top : clearance + padding.Top;
                host.Layout(left, top, left + host.MeasuredWidth, top + host.MeasuredHeight);
            }

            int buttonLengthPixels = m_backButton.MeasuredWidth;
            if (isHorizontal)
            {
                m_backButton.Layout(0, 0, buttonLengthPixels, height);
                m_forwardButton.Layout(width - buttonLengthPixels, 0, width, height);
            }
            else
            {
                buttonLengthPixels = m_backButton.MeasuredHeight;
                m_backButton.Layout(0, 0, width, buttonLengthPixels);
                m_forwardButton.Layout(0, height - buttonLengthPixels, width, height);
            }
        }

        // Lays the dot strip out past the pages: the dots' clip and the page count after it, centered along the stack inside
        //  the strip's padding. Where each dot sits for how far the strip has slid is its translation (PlaceDots).
        private void LayoutDotStrip (int width, int height, float dotStrip)
        {
            if (dotStrip <= 0f)
            {
                return;
            }

            // 1. The strip, inside its padding
            bool isHorizontal = this.IsHorizontal;
            Insets padding = this.EffectiveDotStripPadding();
            float stripX = isHorizontal ? 0f : width - dotStrip;
            float stripY = isHorizontal ? height - dotStrip : 0f;
            float stripWidth = isHorizontal ? width : dotStrip;
            float stripHeight = isHorizontal ? dotStrip : height;
            float innerX = stripX + padding.Left;
            float innerY = stripY + padding.Top;
            float innerWidth = Math.Max(0f, stripWidth - padding.Left - padding.Right);
            float innerHeight = Math.Max(0f, stripHeight - padding.Top - padding.Bottom);

            // 2. How long the dots and count run together, so the pair centers along the stack
            int pageCount = this.Children.Count;
            bool areDotsShown = PageStackRules.AreDotsShown(m_showDots, pageCount);
            bool isPageCountShown = PageStackRules.IsPageCountShown(m_showPageCount, pageCount);
            int cell = (int)Math.Round(this.DotCellLength());
            int dotsLength = areDotsShown ? m_rangeDotCount * cell : 0;
            int countWidth = m_pageCount.MeasuredWidth;
            int countHeight = m_pageCount.MeasuredHeight;
            int countLength = isPageCountShown ? (isHorizontal ? countWidth : countHeight) : 0;
            float gap = (areDotsShown && isPageCountShown) ? kPageCountGapDp * m_density : 0f;
            float runLength = dotsLength + gap + countLength;
            float runStart = isHorizontal ? innerX + ((innerWidth - runLength) / 2f) : innerY + ((innerHeight - runLength) / 2f);

            // 3. The dots' clip, and each dot at its start
            if (areDotsShown)
            {
                int clipLeft = (int)(isHorizontal ? runStart : innerX + ((innerWidth - cell) / 2f));
                int clipTop = (int)(isHorizontal ? innerY + ((innerHeight - cell) / 2f) : runStart);
                m_dotsClip.Layout(clipLeft, clipTop, clipLeft + (isHorizontal ? dotsLength : cell), clipTop + (isHorizontal ? cell : dotsLength));
                foreach (PageDot dot in m_dots)
                {
                    dot.Layout(0, 0, cell, cell);
                }
            }

            // 4. The page count after them
            if (isPageCountShown)
            {
                float countStart = runStart + dotsLength + gap;
                int countLeft = (int)(isHorizontal ? countStart : innerX + ((innerWidth - countWidth) / 2f));
                int countTop = (int)(isHorizontal ? innerY + ((innerHeight - countHeight) / 2f) : countStart);
                m_pageCount.Layout(countLeft, countTop, countLeft + countWidth, countTop + countHeight);
            }
        }

        // Moves and fades each page for where the stack is now, mid transition or swipe or not, and enables the page buttons
        //  for the page shown
        private void PlacePages ()
        {
            if (m_backButton == null)
            {
                return;
            }

            int pageCount = this.Children.Count;
            int shownPage = this.EffectiveSelectedIndex;
            for (int i = 0; i < m_pageHosts.Count; ++i)
            {
                PageHost host = m_pageHosts[i];
                float offset = (float)PageStackRules.PageOffset(i, m_position, m_pageLength, m_pageSpacing);
                host.TranslationX = this.IsHorizontal ? offset : 0f;
                host.TranslationY = this.IsHorizontal ? 0f : offset;
                host.Alpha = (float)PageStackRules.PageOpacity(i, m_position, m_inactivePageOpacity);
                host.IsShownPage = (i == shownPage) && this.Enabled;

                // A page past its neighbors' peeks can't be seen at all
                host.Visibility = Math.Abs(i - m_position) > 1.0 ? ViewStates.Invisible : ViewStates.Visible;
            }

            ViewStates buttonVisibility = PageStackRules.ArePageButtonsShown(pageCount) ? ViewStates.Visible : ViewStates.Invisible;
            m_backButton.Visibility = buttonVisibility;
            m_forwardButton.Visibility = buttonVisibility;
            m_backButton.Enabled = this.Enabled && PageStackRules.CanGoBack(shownPage);
            m_forwardButton.Enabled = this.Enabled && PageStackRules.CanGoForward(shownPage, pageCount);
        }

        // Moves each dot along its clip for how far the strip has slid
        private void PlaceDots ()
        {
            float cell = (float)Math.Round(this.DotCellLength());
            for (int i = 0; i < m_dots.Count; ++i)
            {
                float along = (float)((m_dotSpanFirstPage + i - m_dotScroll) * cell);
                m_dots[i].TranslationX = this.IsHorizontal ? along : 0f;
                m_dots[i].TranslationY = this.IsHorizontal ? 0f : along;
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
            m_dotsClip.Visibility = areDotsShown ? ViewStates.Visible : ViewStates.Gone;
            this.RequestLayout();
            if (!areDotsShown)
            {
                m_isDotSliding = false;
                m_rangeDotCount = 0;
                this.SyncDotSpan(new PageStackRules.DotSpan(0, -1));
                return;
            }

            // 1. Where the strip is going, and whether it slides there or takes its place at once
            PageStackDotRange range = PageStackDotRange.For(pageCount, this.EffectiveSelectedIndex, m_dotReach);
            int toFirstDotPage = range.FirstDotPage;
            m_isDotSliding = slideDots && PageStackRules.ShouldSlideDots(m_dotScroll, toFirstDotPage, range.DotCount);
            if (!m_isDotSliding)
            {
                m_dotScroll = toFirstDotPage;
            }

            m_dotScrollFrom = m_dotScroll;
            m_dotScrollTo = toFirstDotPage;
            m_rangeDotCount = range.DotCount;

            // 2. The dots it holds: the new range's, and while sliding the old one's too
            this.SyncDotSpan(
                m_isDotSliding
                    ? PageStackRules.DotSlideSpan(m_dotScroll, toFirstDotPage, range.DotCount, pageCount)
                    : new PageStackRules.DotSpan(range.FirstDotPage, range.LastDotPage)
            );

            // 3. Fill each for its page, against the new range, so a dot sliding out of view already looks like an edge dot
            for (int i = 0; i < m_dots.Count; ++i)
            {
                int page = m_dotSpanFirstPage + i;
                PageDot dot = m_dots[i];
                dot.PageIndex = page;
                dot.SetKind(range.KindOf(page));
                dot.ContentDescription = $"Page {page + 1} of {pageCount}";
            }

            this.PlaceDots();
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
            var dot = new PageDot(this.Context!, this) { Enabled = this.Enabled };
            m_dots.Insert(index, dot);
            m_dotsClip.AddView(dot);
        }

        private void RemoveDotAt (int index)
        {
            PageDot dot = m_dots[index];
            m_dots.RemoveAt(index);
            m_dotsClip.RemoveView(dot);
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
            m_pageCount.Visibility = isPageCountShown ? ViewStates.Visible : ViewStates.Gone;
            m_pageCount.SetParts(
                isPageCountShown ? PageStackRules.SplitPageCount(m_pageCountFormat, shownPage, pageCount, isVertical: !this.IsHorizontal) : default,
                isVertical: !this.IsHorizontal
            );
            m_pageCount.ContentDescription = isPageCountShown ? PageStackRules.PageCountText(m_pageCountFormat, shownPage, pageCount) : String.Empty;
            this.RequestLayout();
        }

        private void RedrawButtonsAndDots ()
        {
            if (m_backButton == null)
            {
                return;
            }

            m_backButton.Invalidate();
            m_forwardButton.Invalidate();
            m_pageCount.Invalidate();
            foreach (PageDot dot in m_dots)
            {
                dot.Invalidate();
            }
        }

        // The pages' padding; left unset, it pads their far side too while no strip sits there
        private Insets EffectivePagePadding ()
        {
            bool isDotStripShown = PageStackRules.IsDotStripShown(m_showDots, m_showPageCount, this.Children.Count);
            return EffectiveInsets(
                m_pagePadding,
                this.IsHorizontal,
                kDefaultPageGapDp * m_density,
                isGapOnBothSides: !isDotStripShown
            );
        }

        // The dot strip's padding. A vertical count with no dots beside it would run to the stack's right edge, since text
        //  has no side bearing to keep it off, so left unset it gets the gap there too.
        private Insets EffectiveDotStripPadding ()
        {
            bool areDotsShown = PageStackRules.AreDotsShown(m_showDots, this.Children.Count);
            return EffectiveInsets(
                m_dotStripPadding,
                this.IsHorizontal,
                kDefaultDotStripGapDp * m_density,
                isGapOnBothSides: !this.IsHorizontal && !areDotsShown
            );
        }

        // An inset left unset (null) is the gap given on the top when horizontal, the left when vertical, and with
        //  isGapOnBothSides on the bottom or right as well
        private static Insets EffectiveInsets (Insets? insets, bool isHorizontal, float defaultGap, bool isGapOnBothSides)
        {
            if (insets != null)
            {
                return insets.Value;
            }

            int gap = (int)Math.Round(defaultGap);
            int farGap = isGapOnBothSides ? gap : 0;
            return isHorizontal ? new Insets(0, gap, 0, farGap) : new Insets(gap, 0, farGap, 0);
        }

        private Color ResolveColor (ThemedColor color) => ThemeColors.Resolve(this.Context!, color).ToAndroidColor();

        // ===========[ Subclasses/structs ]====================================
        // Four insets in pixels, kept managed so the layout reads them without a call into Java each time; a Rect only at
        //  the public properties
        private readonly record struct Insets (int Left, int Top, int Right, int Bottom)
        {
            public static Insets From (Rect rect) => new Insets(rect.Left, rect.Top, rect.Right, rect.Bottom);

            public Rect ToRect () => new Rect(this.Left, this.Top, this.Right, this.Bottom);
        }

        // What the Choreographer calls on the next frame: the stack's transition step or its dot ease step. The
        //  Choreographer holds this while it is posted, so it holds the stack weakly.
        private sealed class FrameTicker : Java.Lang.Object, Choreographer.IFrameCallback
        {
            private readonly WeakReference<PageStack> m_owner;
            private readonly bool m_isDotEase;
            private bool m_isPosted;

            public FrameTicker (PageStack owner, bool isDotEase)
            {
                m_owner = new WeakReference<PageStack>(owner);
                m_isDotEase = isDotEase;
            }

            /// <summary>Asks for a call on the next frame, unless one is already asked for</summary>
            public void Post ()
            {
                if (!m_isPosted)
                {
                    m_isPosted = true;
                    Choreographer.Instance?.PostFrameCallback(this);
                }
            }

            /// <summary>Takes back a call asked for and not yet made</summary>
            public void Cancel ()
            {
                if (m_isPosted)
                {
                    m_isPosted = false;
                    Choreographer.Instance?.RemoveFrameCallback(this);
                }
            }

            public void DoFrame (long frameTimeNanos)
            {
                m_isPosted = false;
                if (!m_owner.TryGetTarget(out PageStack? owner))
                {
                    return;
                }

                if (m_isDotEase)
                {
                    owner.OnDotEaseFrame();
                }
                else
                {
                    owner.OnTransitionFrame();
                }
            }
        }

        // A view whose children the stack lays out itself, clipped to its bounds: the pages' area, and the dots' range
        private sealed class ClipBox : ViewGroup
        {
            public ClipBox (Context context) : base(context)
            {
                this.SetClipChildren(true);
            }

            private ClipBox (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
            {
            }

            protected override void OnLayout (bool changed, int l, int t, int r, int b)
            {
            }
        }

        // Holds one page, filling it. Only the shown page takes touches and focus and is read out, so a peek can't be
        //  tapped, tabbed or read into.
        private sealed class PageHost : FrameLayout
        {
            private bool m_isShownPage = true;

            public PageHost (Context context) : base(context)
            {
            }

            private PageHost (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
            {
            }

            public bool IsShownPage
            {
                get => m_isShownPage;
                set
                {
                    if (m_isShownPage == value)
                    {
                        return;
                    }

                    m_isShownPage = value;
                    this.DescendantFocusability = value ? DescendantFocusability.AfterDescendants : DescendantFocusability.BlockDescendants;
                    this.ImportantForAccessibility = value ? ImportantForAccessibility.Auto : ImportantForAccessibility.NoHideDescendants;
                }
            }

            public override bool DispatchTouchEvent (MotionEvent? e) => m_isShownPage && base.DispatchTouchEvent(e);
        }

        // A page button: the stack's button fill and outline with a chevron pointing its way, faded the way BumpStack's
        //  are (brighter while pressed or under a mouse, dimmed when there is no page that way). It activates once per tap.
        private sealed class PageButton : View
        {
            private const float kNormalOpacity = 0.8f;
            private const float kHighlightedOpacity = 1f;
            private const float kDisabledOpacity = 0.4f;
            private const float kChevronLineWidthDp = 1.5f;

            // How far the chevron's point sits ahead of its middle, and how far its arms reach out, from the glyph size, as
            //  a font glyph's would
            private const float kPointRatio = 0.2f;
            private const float kSpreadRatio = 0.35f;

            private readonly PageStack? m_owner;
            private readonly bool m_isForward;
            private readonly Paint m_paint = new Paint(PaintFlags.AntiAlias);
            private readonly Path m_chevron = new Path();

            public PageButton (Context context, PageStack owner, bool isForward) : base(context)
            {
                m_owner = owner;
                m_isForward = isForward;
                this.Clickable = true;
                this.Focusable = false;
                this.ContentDescription = isForward ? "Next page" : "Previous page";
                this.Click += this.OnClicked;
                this.Alpha = kNormalOpacity;
            }

            private PageButton (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
            {
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing)
                {
                    this.Click -= this.OnClicked;
                }

                base.Dispose(disposing);
            }

            protected override void DrawableStateChanged ()
            {
                base.DrawableStateChanged();
                if (!this.Enabled)
                {
                    this.Alpha = kDisabledOpacity;
                }
                else
                {
                    this.Alpha = (this.Pressed || this.Hovered) ? kHighlightedOpacity : kNormalOpacity;
                }
            }

            protected override void OnDraw (Canvas canvas)
            {
                if (m_owner == null)
                {
                    return;
                }

                // 1. Fill and outline, the outline inside the bounds
                float density = m_owner.m_density;
                float border = kBorderThicknessDp * density;
                m_paint.SetStyle(Paint.Style.Fill);
                m_paint.Color = m_owner.ResolveColor(m_owner.m_buttonBackgroundColor);
                canvas.DrawRect(0, 0, this.Width, this.Height, m_paint);
                m_paint.SetStyle(Paint.Style.Stroke);
                m_paint.StrokeWidth = border;
                m_paint.Color = m_owner.ResolveColor(m_owner.m_buttonBorderColor);
                canvas.DrawRect(border / 2f, border / 2f, this.Width - (border / 2f), this.Height - (border / 2f), m_paint);

                // 2. The chevron, pointing the way the button pages
                float centerX = this.Width / 2f;
                float centerY = this.Height / 2f;
                float pointLength = m_owner.m_buttonFontSize * kPointRatio;
                float spread = m_owner.m_buttonFontSize * kSpreadRatio;
                float direction = m_isForward ? 1f : -1f;
                m_chevron.Reset();
                if (m_owner.IsHorizontal)
                {
                    m_chevron.MoveTo(centerX - (direction * pointLength), centerY - spread);
                    m_chevron.LineTo(centerX + (direction * pointLength), centerY);
                    m_chevron.LineTo(centerX - (direction * pointLength), centerY + spread);
                }
                else
                {
                    m_chevron.MoveTo(centerX - spread, centerY - (direction * pointLength));
                    m_chevron.LineTo(centerX, centerY + (direction * pointLength));
                    m_chevron.LineTo(centerX + spread, centerY - (direction * pointLength));
                }

                m_paint.StrokeWidth = kChevronLineWidthDp * density;
                m_paint.StrokeCap = Paint.Cap.Round;
                m_paint.StrokeJoin = Paint.Join.Round;
                m_paint.Color = m_owner.ResolveColor(m_owner.m_buttonForegroundColor);
                canvas.DrawPath(m_chevron, m_paint);
            }

            private void OnClicked (object? sender, EventArgs e)
            {
                m_owner?.GoTo(m_owner.EffectiveSelectedIndex + (m_isForward ? 1 : -1));
            }
        }

        // One dot in the strip: filled in the dot color, or the current dot color for the current page, and an edge dot
        //  (more pages past the range) smaller and faded, growing to full size, still faded, while pressed. The whole dot
        //  fades with its state the way WinUI3's do. A change of kind or of the press eases its size and fade over a short,
        //  even step, stepped by the stack's dot ease callback; a new dot takes its first look at once.
        private sealed class PageDot : View
        {
            private readonly PageStack? m_owner;
            private readonly Paint m_paint = new Paint(PaintFlags.AntiAlias);
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

            public PageDot (Context context, PageStack owner) : base(context)
            {
                m_owner = owner;
                this.Clickable = true;
                this.Focusable = false;
                this.Click += this.OnClicked;
            }

            private PageDot (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
            {
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing)
                {
                    this.Click -= this.OnClicked;
                }

                base.Dispose(disposing);
            }

            /// <summary>The page tapping this dot goes to</summary>
            public int PageIndex { get; set; }

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

                double progress = Math.Min(1.0, Stopwatch.GetElapsedTime(m_easeStartTimestamp).TotalSeconds / MobileLook.PageStack.DotEaseSeconds);
                m_scale = m_fromScale + ((m_toScale - m_fromScale) * progress);
                m_fade = m_fromFade + ((m_toFade - m_fromFade) * progress);
                m_isEasing = progress < 1.0;
                this.Invalidate();
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
                    this.Invalidate();
                }
            }

            // A press eases an edge dot's size. Android reports states while the dot is still being made, before it stands
            //  for a page, and those leave the first look to SetKind.
            protected override void DrawableStateChanged ()
            {
                base.DrawableStateChanged();
                if (m_hasLook)
                {
                    this.UpdateLook(animate: true);
                }
            }

            protected override void OnDraw (Canvas canvas)
            {
                if (m_owner == null)
                {
                    return;
                }

                double opacity;
                if (!this.Enabled)
                {
                    opacity = MobileLook.PageStack.DotDisabledOpacity;
                }
                else
                {
                    opacity = (this.Pressed || this.Hovered) ? MobileLook.PageStack.DotPressedOpacity : MobileLook.PageStack.DotOpacity;
                }

                Color color = m_owner.ResolveColor(m_kind == ePageStackDotKind.Current ? m_owner.m_currentDotColor : m_owner.m_dotColor);
                color.A = (byte)Math.Round(color.A * opacity * m_fade);
                m_paint.Color = color;
                canvas.DrawCircle(this.Width / 2f, this.Height / 2f, (float)(m_owner.m_dotSize * m_scale / 2.0), m_paint);
            }

            // Aims the dot's size and fade at its kind's look, with the press taken into account for an edge dot, either at
            //  once or eased there
            private void UpdateLook (bool animate)
            {
                bool isEdge = (m_kind == ePageStackDotKind.BackEdge) || (m_kind == ePageStackDotKind.ForwardEdge);
                bool isPressed = this.Pressed || this.Hovered;
                double toScale = (isEdge && !isPressed) ? MobileLook.PageStack.EdgeDotScale : 1.0;
                double toFade = isEdge ? MobileLook.PageStack.EdgeDotFade : 1.0;
                if (m_hasLook && (toScale == m_toScale) && (toFade == m_toFade))
                {
                    this.Invalidate();
                    return;
                }

                m_toScale = toScale;
                m_toFade = toFade;
                this.Invalidate();
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
                if ((m_owner == null) || !m_owner.StartDotEase())
                {
                    this.LandLook();
                }
            }

            private void OnClicked (object? sender, EventArgs e)
            {
                m_owner?.GoTo(this.PageIndex);
            }
        }

        // The page count: up to two counts and the format's own words around them, each drawn on its own so the words
        //  ("of") draw smaller and fainter than the numbers, in the dot color. Horizontal, the pieces share a baseline, with
        //  the words raised a little so they center on the numbers; vertical, each line is centered under the last. The
        //  pieces are measured when they are set, and drawn in the theme's color of the moment.
        private sealed class PageCountView : View
        {
            private const float kWordRaiseDp = 2f;

            private readonly PageStack? m_owner;
            private readonly List<Piece> m_pieces = new List<Piece>();
            private readonly Paint m_countPaint = new Paint(PaintFlags.AntiAlias);
            private readonly Paint m_wordPaint = new Paint(PaintFlags.AntiAlias);

            public PageCountView (Context context, PageStack owner) : base(context)
            {
                m_owner = owner;
                float density = context.Resources?.DisplayMetrics?.Density ?? 1f;
                m_countPaint.TextSize = (float)MobileLook.PageStack.CountFontSize * density;
                m_wordPaint.TextSize = (float)MobileLook.PageStack.CountWordFontSize * density;
                this.ImportantForAccessibility = ImportantForAccessibility.Yes;
            }

            private PageCountView (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
            {
            }

            /// <summary>The width the pieces take together, as last set</summary>
            public float ContentWidth { get; private set; }

            /// <summary>The height the pieces take together, as last set</summary>
            public float ContentHeight { get; private set; }

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

                // 2. Place them, each by its baseline
                float width = 0f;
                float height = 0f;
                if (isVertical)
                {
                    foreach (Piece piece in m_pieces)
                    {
                        width = Math.Max(width, piece.Width);
                    }

                    for (int i = 0; i < m_pieces.Count; ++i)
                    {
                        Piece piece = m_pieces[i];
                        m_pieces[i] = piece with { X = (width - piece.Width) / 2f, Baseline = height + piece.Ascent };
                        height += piece.Ascent + piece.Descent;
                    }
                }
                else
                {
                    // Every baseline sits under the tallest ascent, the words raised, then all shift so the highest top is at zero
                    float maxAscent = 0f;
                    foreach (Piece piece in m_pieces)
                    {
                        maxAscent = Math.Max(maxAscent, piece.Ascent);
                    }

                    float raise = kWordRaiseDp * (this.Context?.Resources?.DisplayMetrics?.Density ?? 1f);
                    float minTop = float.MaxValue;
                    var tops = new float[m_pieces.Count];
                    for (int i = 0; i < m_pieces.Count; ++i)
                    {
                        tops[i] = maxAscent - m_pieces[i].Ascent - (m_pieces[i].IsWord ? raise : 0f);
                        minTop = Math.Min(minTop, tops[i]);
                    }

                    for (int i = 0; i < m_pieces.Count; ++i)
                    {
                        Piece piece = m_pieces[i];
                        float top = tops[i] - minTop;
                        m_pieces[i] = piece with { X = width, Baseline = top + piece.Ascent };
                        width += piece.Width;
                        height = Math.Max(height, top + piece.Ascent + piece.Descent);
                    }
                }

                this.ContentWidth = width;
                this.ContentHeight = height;
                this.RequestLayout();
                this.Invalidate();

                void _Add (string? text, bool isWord)
                {
                    if (String.IsNullOrEmpty(text))
                    {
                        return;
                    }

                    Paint paint = isWord ? m_wordPaint : m_countPaint;
                    Paint.FontMetrics? metrics = paint.GetFontMetrics();
                    float ascent = -(metrics?.Ascent ?? 0f);
                    float descent = metrics?.Descent ?? 0f;
                    string[] lines = isVertical ? text.Split('\n') : new[] { text };
                    foreach (string line in lines)
                    {
                        m_pieces.Add(new Piece(line, isWord, paint.MeasureText(line), ascent, descent, 0f, 0f));
                    }
                }
            }

            protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
            {
                this.SetMeasuredDimension((int)Math.Ceiling(this.ContentWidth), (int)Math.Ceiling(this.ContentHeight));
            }

            protected override void OnDraw (Canvas canvas)
            {
                if (m_owner == null)
                {
                    return;
                }

                Color countColor = m_owner.ResolveColor(m_owner.m_dotColor);
                Color wordColor = countColor;
                wordColor.A = (byte)Math.Round(countColor.A * MobileLook.PageStack.CountWordOpacity);
                m_countPaint.Color = countColor;
                m_wordPaint.Color = wordColor;
                foreach (Piece piece in m_pieces)
                {
                    canvas.DrawText(piece.Text, piece.X, piece.Baseline, piece.IsWord ? m_wordPaint : m_countPaint);
                }
            }

            // One piece of the count: its text, whether it is the format's own words, its measured width and its font's
            //  ascent and descent, and where it draws: its left and its baseline
            private readonly record struct Piece (string Text, bool IsWord, float Width, float Ascent, float Descent, float X, float Baseline);
        }
    }
}
