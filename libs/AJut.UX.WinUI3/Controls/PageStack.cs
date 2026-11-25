namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Automation;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Markup;
    using Microsoft.UI.Xaml.Media;
    using Windows.Foundation;
    using Windows.System;
    using DPUtils = AJut.UX.DPUtils<PageStack>;

    // ===========[ PageStack ]==================================================
    // BumpStack's sibling for paging: one child (a page) shows at a time, with a page button at each end and, optionally,
    // a strip of dots. A click on a page button or a dot runs an animated transition to that page; the buttons are plain
    // single click buttons, not repeat buttons. The neighbor pages sit just past the shown one, faded and mostly clipped
    // away, so a sliver of each shows past its page button as a hint that there is more. Which page shows, where each page
    // sits, how faded it is, how a transition eases, and which dots the strip shows are AJut.UX's PageStackRules and
    // PageStackDotWindow, which WPF's and AppKit's PageStacks follow too.
    //
    // Each child goes in a page host the stack owns (a ContentControl), so the stack can move, fade and disable a page
    // without writing to the consumer's own element. The hosts are stacked in one grid cell, so the stack sizes across
    // itself to its largest page, and each is moved along the stack by a render transform. A page that is not the shown
    // one is disabled and takes no hits, so its peek can't be clicked or tabbed into.
    //
    // A transition runs on CompositionTarget.Rendering, rather than a storyboard, so all three stacks step through the
    // same shared position math. The stack subscribes only while a transition runs, and unsubscribes when it lands or the
    // stack unloads: Rendering is static, so a subscription left behind would pin the stack.
    //
    // The dot strip slides with the pages: a pick turns its dot current at once, and the strip then shifts by however many
    // dots the pick was away, on the same clock and easing as the page transition, so the current dot settles back where
    // the window puts it. While it slides the strip holds the dots of both windows, so the ones leaving slide out of view
    // rather than vanish. Each dot stays with its page through a slide, so only a dot whose kind changes animates its
    // size. A jump too far for the two strips to share a dot takes its new place at once.
    //
    // Template parts:
    //   PART_PagesClip     - the area the pages and page buttons share, clipped to its bounds
    //   PART_PageViewport  - the shown page's area, inset from both ends; holds the page hosts
    //   PART_BackButton    - left/top page button
    //   PART_ForwardButton - right/bottom page button
    //   PART_DotsHost      - the dot strip, padded by DotStripPadding
    //   PART_DotsClip      - a canvas sized and clipped to the strip's dots
    //   PART_DotsPanel     - holds the dots, and slides along the canvas
    //   PART_PageCountText - the page count ("3 of 7"), after the dots
    /// <summary>
    /// Shows one of its children at a time as a page, with a page button at each end and an optional dot strip, and runs an
    /// animated transition to whichever page is picked. The neighbor pages peek in, faded, past the page buttons.
    /// </summary>
    [ContentProperty(Name = nameof(Children))]
    [TemplatePart(Name = nameof(PART_PagesClip), Type = typeof(Grid))]
    [TemplatePart(Name = nameof(PART_PageViewport), Type = typeof(Grid))]
    [TemplatePart(Name = nameof(PART_BackButton), Type = typeof(Button))]
    [TemplatePart(Name = nameof(PART_ForwardButton), Type = typeof(Button))]
    [TemplatePart(Name = nameof(PART_DotsHost), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_DotsClip), Type = typeof(Canvas))]
    [TemplatePart(Name = nameof(PART_DotsPanel), Type = typeof(StackPanel))]
    [TemplatePart(Name = nameof(PART_PageCountText), Type = typeof(TextBlock))]
    [TemplateVisualState(GroupName = "OrientationStates", Name = kOrientationStateHorizontal)]
    [TemplateVisualState(GroupName = "OrientationStates", Name = kOrientationStateVertical)]
    public class PageStack : Control
    {
        private const string kOrientationStateHorizontal = "Horizontal";
        private const string kOrientationStateVertical = "Vertical";
        private const double kFallbackButtonLength = 24.0;
        private const double kDefaultDotStripGap = 4.0;
        private const double kDefaultPageGap = 5.0;
        private const double kFallbackDotPadding = 3.0;

        private readonly List<ContentControl> m_pageHosts = new List<ContentControl>();
        private readonly List<PageStackDot> m_dots = new List<PageStackDot>();
        private bool m_isHorizontal = true;
        private bool m_isAdjustingSelection;
        private double m_position;
        private double m_transitionFrom;
        private double m_transitionTo;
        private long m_transitionStartTimestamp;
        private bool m_isTransitioning;
        private int m_dotSpanFirstPage;
        private double m_dotCellLength;
        private double m_dotScroll;
        private double m_dotScrollFrom;
        private int m_dotScrollTo;
        private bool m_isDotSliding;

        // ===========[ Setup/Construction/Teardown ]===========================
        public PageStack ()
        {
            this.DefaultStyleKey = typeof(PageStack);
            this.Children = new ObservableCollection<UIElement>();
            this.Children.CollectionChanged += this.OnChildrenChanged;
            this.Unloaded += this.OnUnloaded;
        }

        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            // 1. Unwire the old parts, and take the page hosts and dots out of them, since an element can only have one parent
            if (this.PART_PagesClip != null)
            {
                this.PART_PagesClip.SizeChanged -= this.OnPagesClipSizeChanged;
            }

            if (this.PART_PageViewport != null)
            {
                this.PART_PageViewport.SizeChanged -= this.OnPageViewportSizeChanged;
                this.PART_PageViewport.Children.Clear();
            }

            if (this.PART_BackButton != null)
            {
                this.PART_BackButton.Click -= this.OnBackClick;
            }

            if (this.PART_ForwardButton != null)
            {
                this.PART_ForwardButton.Click -= this.OnForwardClick;
            }

            this.PART_DotsPanel?.Children.Clear();

            // 2. Acquire the new parts
            this.PART_PagesClip = this.GetTemplateChild(nameof(PART_PagesClip)) as Grid;
            this.PART_PageViewport = this.GetTemplateChild(nameof(PART_PageViewport)) as Grid;
            this.PART_BackButton = this.GetTemplateChild(nameof(PART_BackButton)) as Button;
            this.PART_ForwardButton = this.GetTemplateChild(nameof(PART_ForwardButton)) as Button;
            this.PART_DotsHost = this.GetTemplateChild(nameof(PART_DotsHost)) as Border;
            this.PART_DotsClip = this.GetTemplateChild(nameof(PART_DotsClip)) as Canvas;
            this.PART_DotsPanel = this.GetTemplateChild(nameof(PART_DotsPanel)) as StackPanel;
            this.PART_PageCountText = this.GetTemplateChild(nameof(PART_PageCountText)) as TextBlock;

            // 3. Wire them, and put the page hosts and dots back in
            if (this.PART_PagesClip != null)
            {
                this.PART_PagesClip.SizeChanged += this.OnPagesClipSizeChanged;
            }

            if (this.PART_PageViewport != null)
            {
                this.PART_PageViewport.SizeChanged += this.OnPageViewportSizeChanged;
                foreach (ContentControl host in m_pageHosts)
                {
                    this.PART_PageViewport.Children.Add(host);
                }
            }

            if (this.PART_BackButton != null)
            {
                this.PART_BackButton.Click += this.OnBackClick;
            }

            if (this.PART_ForwardButton != null)
            {
                this.PART_ForwardButton.Click += this.OnForwardClick;
            }

            if (this.PART_DotsPanel != null)
            {
                foreach (PageStackDot dot in m_dots)
                {
                    this.PART_DotsPanel.Children.Add(dot);
                }
            }

            this.ApplyOrientation(useTransitions: false);
            this.JumpTo(this.EffectiveSelectedIndex);
            this.RefreshPageGeometry();
            this.RefreshButtonsAndDots();
        }

        // ===========[ Events ]================================================
        private void OnChildrenChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            int oldPageCount = m_pageHosts.Count;
            int selectedIndex = this.SelectedIndex;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    for (int i = 0; i < e.NewItems.Count; ++i)
                    {
                        this.InsertPageHost(e.NewStartingIndex + i, (UIElement)e.NewItems[i]);
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

                case NotifyCollectionChangedAction.Replace:
                    for (int i = 0; i < e.NewItems.Count; ++i)
                    {
                        m_pageHosts[e.NewStartingIndex + i].Content = e.NewItems[i];
                    }

                    break;

                default:
                    this.SyncPageHosts();
                    break;
            }

            // A page added or removed moves the shown page at once: only a pick runs a transition
            if (selectedIndex != this.SelectedIndex)
            {
                m_isAdjustingSelection = true;
                try
                {
                    this.SelectedIndex = selectedIndex;
                }
                finally
                {
                    m_isAdjustingSelection = false;
                }
            }

            this.JumpTo(this.EffectiveSelectedIndex);
            this.RefreshPageGeometry();
            this.RefreshButtonsAndDots();
        }

        private void OnUnloaded (object sender, RoutedEventArgs e)
        {
            // Lands any running transition, which lets go of CompositionTarget.Rendering
            if (m_isTransitioning)
            {
                this.JumpTo((int)m_transitionTo);
            }
        }

        private void OnPagesClipSizeChanged (object sender, SizeChangedEventArgs e)
        {
            // WinUI3 has no ClipToBounds, and the neighbor pages are moved past the area by render transforms, which layout
            //  never clips, so the area is clipped to its own bounds here
            this.PART_PagesClip.Clip = new RectangleGeometry { Rect = new Rect(0.0, 0.0, e.NewSize.Width, e.NewSize.Height) };
        }

        private void OnPageViewportSizeChanged (object sender, SizeChangedEventArgs e) => this.RefreshPageLayout();
        private void OnBackClick (object sender, RoutedEventArgs e) => this.GoTo(this.EffectiveSelectedIndex - 1);
        private void OnForwardClick (object sender, RoutedEventArgs e) => this.GoTo(this.EffectiveSelectedIndex + 1);

        private void OnDotClick (object sender, RoutedEventArgs e)
        {
            if (sender is PageStackDot dot)
            {
                this.GoTo(dot.PageIndex);
            }
        }

        private void OnRendering (object sender, object e)
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(m_transitionStartTimestamp);
            m_position = PageStackRules.TransitionPosition(m_transitionFrom, m_transitionTo, elapsed, this.TransitionDuration);
            if (m_isDotSliding)
            {
                m_dotScroll = PageStackRules.TransitionPosition(m_dotScrollFrom, m_dotScrollTo, elapsed, this.TransitionDuration);
                this.ApplyDotScroll();
            }

            if (PageStackRules.IsTransitionDone(elapsed, this.TransitionDuration))
            {
                this.StopTransition();
                this.LandDotSlide();
            }

            this.RefreshPageLayout();
        }

        protected override void OnKeyDown (KeyRoutedEventArgs e)
        {
            base.OnKeyDown(e);

            // Only with the stack itself focused: arrow keys inside a page belong to that page
            if (e.Handled || !ReferenceEquals(e.OriginalSource, this))
            {
                return;
            }

            int target;
            switch (e.Key)
            {
                case VirtualKey.Left when m_isHorizontal:
                case VirtualKey.Up when !m_isHorizontal:
                    target = this.EffectiveSelectedIndex - 1;
                    break;

                case VirtualKey.Right when m_isHorizontal:
                case VirtualKey.Down when !m_isHorizontal:
                    target = this.EffectiveSelectedIndex + 1;
                    break;

                case VirtualKey.Home:
                    target = 0;
                    break;

                case VirtualKey.End:
                    target = this.Children.Count - 1;
                    break;

                default:
                    return;
            }

            e.Handled = this.GoTo(target);
        }

        // ===========[ Dependency Properties ]=================================
        public static readonly DependencyProperty OrientationProperty = DPUtils.Register(_ => _.Orientation, Orientation.Horizontal, (d, e) => d.OnOrientationChanged());
        /// <summary>Which way the pages run: Horizontal (the default) or Vertical</summary>
        public Orientation Orientation
        {
            get => (Orientation)this.GetValue(OrientationProperty);
            set => this.SetValue(OrientationProperty, value);
        }

        public static readonly DependencyProperty SelectedIndexProperty = DPUtils.Register(_ => _.SelectedIndex, 0, (d, e) => d.OnSelectedIndexChanged());
        /// <summary>
        /// The index of the page shown. An index past either end shows the page at that end, and is kept as given, so one
        /// set before the pages are added still lands once they are. Setting it runs a transition to that page.
        /// </summary>
        public int SelectedIndex
        {
            get => (int)this.GetValue(SelectedIndexProperty);
            set => this.SetValue(SelectedIndexProperty, value);
        }

        private void OnSelectedIndexChanged ()
        {
            if (m_isAdjustingSelection)
            {
                return;
            }

            this.BeginTransitionTo(this.EffectiveSelectedIndex);
            this.RefreshButtonsAndDots(slideDots: m_isTransitioning);
        }

        public static readonly DependencyProperty ShowDotsProperty = DPUtils.Register(_ => _.ShowDots, false, (d, e) => d.RefreshButtonsAndDots());
        /// <summary>Whether a strip of dots shows under the pages (beside them when vertical), one per page in the dot window</summary>
        public bool ShowDots
        {
            get => (bool)this.GetValue(ShowDotsProperty);
            set => this.SetValue(ShowDotsProperty, value);
        }

        public static readonly DependencyProperty DotReachProperty = DPUtils.Register(_ => _.DotReach, 5, (d, e) => d.RefreshButtonsAndDots());
        /// <summary>
        /// How many dots show behind and ahead of the current page's dot: 5 shows 11 dots, plus a smaller, faded dot at
        /// each end while there are more pages past them. Clicking one of those goes to the page just past the window.
        /// </summary>
        public int DotReach
        {
            get => (int)this.GetValue(DotReachProperty);
            set => this.SetValue(DotReachProperty, value);
        }

        public static readonly DependencyProperty TransitionDurationProperty = DPUtils.Register(_ => _.TransitionDuration, TimeSpan.FromMilliseconds(250));
        /// <summary>How long a transition from one page to another takes, eased out; zero jumps straight there</summary>
        public TimeSpan TransitionDuration
        {
            get => (TimeSpan)this.GetValue(TransitionDurationProperty);
            set => this.SetValue(TransitionDurationProperty, value);
        }

        public static readonly DependencyProperty PageSpacingProperty = DPUtils.Register(_ => _.PageSpacing, 8.0, (d, e) => d.RefreshPageGeometry());
        /// <summary>The gap between one page and the next</summary>
        public double PageSpacing
        {
            get => (double)this.GetValue(PageSpacingProperty);
            set => this.SetValue(PageSpacingProperty, value);
        }

        public static readonly DependencyProperty PagePeekProperty = DPUtils.Register(_ => _.PagePeek, 16.0, (d, e) => d.RefreshPageGeometry());
        /// <summary>How much of each neighbor page shows past its page button, as a hint that there is more</summary>
        public double PagePeek
        {
            get => (double)this.GetValue(PagePeekProperty);
            set => this.SetValue(PagePeekProperty, value);
        }

        public static readonly DependencyProperty InactivePageOpacityProperty = DPUtils.Register(_ => _.InactivePageOpacity, 0.25, (d, e) => d.RefreshPageLayout());
        /// <summary>How opaque the pages that are not shown draw, so their peeks read as hints rather than content</summary>
        public double InactivePageOpacity
        {
            get => (double)this.GetValue(InactivePageOpacityProperty);
            set => this.SetValue(InactivePageOpacityProperty, value);
        }

        public static readonly DependencyProperty ButtonBackgroundProperty = DPUtils.Register(_ => _.ButtonBackground);
        public Brush ButtonBackground
        {
            get => (Brush)this.GetValue(ButtonBackgroundProperty);
            set => this.SetValue(ButtonBackgroundProperty, value);
        }

        public static readonly DependencyProperty ButtonForegroundProperty = DPUtils.Register(_ => _.ButtonForeground);
        public Brush ButtonForeground
        {
            get => (Brush)this.GetValue(ButtonForegroundProperty);
            set => this.SetValue(ButtonForegroundProperty, value);
        }

        public static readonly DependencyProperty ButtonBorderProperty = DPUtils.Register(_ => _.ButtonBorder);
        public Brush ButtonBorder
        {
            get => (Brush)this.GetValue(ButtonBorderProperty);
            set => this.SetValue(ButtonBorderProperty, value);
        }

        public static readonly DependencyProperty ButtonFontSizeProperty = DPUtils.Register(_ => _.ButtonFontSize, 12.0, (d, e) => d.RefreshPageGeometry());
        public double ButtonFontSize
        {
            get => (double)this.GetValue(ButtonFontSizeProperty);
            set => this.SetValue(ButtonFontSizeProperty, value);
        }

        public static readonly DependencyProperty ButtonPaddingProperty = DPUtils.Register(_ => _.ButtonPadding, new Thickness(4, 0, 4, 0), (d, e) => d.RefreshPageGeometry());
        public Thickness ButtonPadding
        {
            get => (Thickness)this.GetValue(ButtonPaddingProperty);
            set => this.SetValue(ButtonPaddingProperty, value);
        }

        public static readonly DependencyProperty DotBrushProperty = DPUtils.Register(_ => _.DotBrush, (d, e) => d.RefreshButtonsAndDots());
        /// <summary>The fill of every dot but the current page's</summary>
        public Brush DotBrush
        {
            get => (Brush)this.GetValue(DotBrushProperty);
            set => this.SetValue(DotBrushProperty, value);
        }

        public static readonly DependencyProperty CurrentDotBrushProperty = DPUtils.Register(_ => _.CurrentDotBrush, (d, e) => d.RefreshButtonsAndDots());
        /// <summary>The fill of the current page's dot</summary>
        public Brush CurrentDotBrush
        {
            get => (Brush)this.GetValue(CurrentDotBrushProperty);
            set => this.SetValue(CurrentDotBrushProperty, value);
        }

        public static readonly DependencyProperty DotSizeProperty = DPUtils.Register(_ => _.DotSize, 8.0, (d, e) => d.RefreshButtonsAndDots());
        /// <summary>The diameter of a full size dot; the edge dots draw smaller</summary>
        public double DotSize
        {
            get => (double)this.GetValue(DotSizeProperty);
            set => this.SetValue(DotSizeProperty, value);
        }

        public static readonly DependencyProperty DotStripPaddingProperty = DPUtils.Register(_ => _.DotStripPadding, new Thickness(double.NaN), (d, e) => d.RefreshButtonsAndDots());
        /// <summary>
        /// The space around the dot strip. Left unset, it is 4 on the side facing the pages and nothing elsewhere: the top
        /// when horizontal, the left when vertical.
        /// </summary>
        public Thickness DotStripPadding
        {
            get => (Thickness)this.GetValue(DotStripPaddingProperty);
            set => this.SetValue(DotStripPaddingProperty, value);
        }

        public static readonly DependencyProperty ShowPageCountProperty = DPUtils.Register(_ => _.ShowPageCount, false, (d, e) => d.RefreshButtonsAndDots());
        /// <summary>Whether the page count ("3 of 7") shows after the dots, or on its own where the dots would be when they are off</summary>
        public bool ShowPageCount
        {
            get => (bool)this.GetValue(ShowPageCountProperty);
            set => this.SetValue(ShowPageCountProperty, value);
        }

        public static readonly DependencyProperty PageCountFormatProperty = DPUtils.Register(_ => _.PageCountFormat, PageStackRules.kDefaultPageCountFormat, (d, e) => d.RefreshButtonsAndDots());
        /// <summary>
        /// The page count's text, with {0} for the shown page's number from one and {1} for the page count: "{0} of {1}"
        /// by default. When vertical, each word goes on a line of its own.
        /// </summary>
        public string PageCountFormat
        {
            get => (string)this.GetValue(PageCountFormatProperty);
            set => this.SetValue(PageCountFormatProperty, value);
        }

        public static readonly DependencyProperty PagePaddingProperty = DPUtils.Register(_ => _.PagePadding, new Thickness(double.NaN), (d, e) => d.RefreshPageGeometry());
        /// <summary>
        /// The space around the pages, inside the page buttons. Left unset, it is 5 on the side the pages would otherwise
        /// crowd and nothing elsewhere: the top when horizontal, the left when vertical.
        /// </summary>
        public Thickness PagePadding
        {
            get => (Thickness)this.GetValue(PagePaddingProperty);
            set => this.SetValue(PagePaddingProperty, value);
        }

        // ===========[ Properties ]============================================
        /// <summary>The pages, in order. In XAML, nest them directly inside the PageStack.</summary>
        public ObservableCollection<UIElement> Children { get; }

        private Grid PART_PagesClip { get; set; }
        private Grid PART_PageViewport { get; set; }
        private Button PART_BackButton { get; set; }
        private Button PART_ForwardButton { get; set; }
        private Border PART_DotsHost { get; set; }
        private Canvas PART_DotsClip { get; set; }
        private StackPanel PART_DotsPanel { get; set; }
        private TextBlock PART_PageCountText { get; set; }

        private int EffectiveSelectedIndex => PageStackRules.EffectiveSelectedIndex(this.SelectedIndex, this.Children.Count);

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

        private void OnOrientationChanged ()
        {
            this.ApplyOrientation(useTransitions: false);
            this.RefreshPageGeometry();
            this.RefreshButtonsAndDots();
        }

        private void ApplyOrientation (bool useTransitions)
        {
            m_isHorizontal = this.Orientation == Orientation.Horizontal;
            VisualStateManager.GoToState(this, m_isHorizontal ? kOrientationStateHorizontal : kOrientationStateVertical, useTransitions);
        }

        private void InsertPageHost (int index, UIElement page)
        {
            // The host fills the page area, and its page fills the host
            var host = new ContentControl
            {
                Content = page,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                IsTabStop = false,
                RenderTransform = new TranslateTransform(),
            };

            m_pageHosts.Insert(index, host);
            this.PART_PageViewport?.Children.Insert(index, host);
        }

        private void RemovePageHostAt (int index)
        {
            ContentControl host = m_pageHosts[index];
            m_pageHosts.RemoveAt(index);
            this.PART_PageViewport?.Children.Remove(host);

            // Lets go of the page, so it can go into another parent
            host.Content = null;
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
            if ((pageIndex < 0) || !this.IsLoaded || (this.PART_PageViewport == null) || (this.TransitionDuration <= TimeSpan.Zero))
            {
                this.JumpTo(pageIndex);
                return;
            }

            // Started from wherever the stack is now, so a pick during a transition carries on from mid way
            m_transitionFrom = m_position;
            m_transitionTo = pageIndex;
            m_transitionStartTimestamp = Stopwatch.GetTimestamp();
            if (!m_isTransitioning)
            {
                m_isTransitioning = true;
                CompositionTarget.Rendering -= this.OnRendering;
                CompositionTarget.Rendering += this.OnRendering;
            }
        }

        private void JumpTo (int pageIndex)
        {
            this.StopTransition();
            this.LandDotSlide();
            m_position = Math.Max(0, pageIndex);
            this.RefreshPageLayout();
        }

        private void StopTransition ()
        {
            if (m_isTransitioning)
            {
                CompositionTarget.Rendering -= this.OnRendering;
                m_isTransitioning = false;
            }
        }

        // Shows the page buttons while there is another page, and insets the page area from both ends by a button, the
        //  peek and the gap, so a sliver of each neighbor shows past its button
        private void RefreshPageGeometry ()
        {
            if ((this.PART_PageViewport == null) || (this.PART_BackButton == null) || (this.PART_ForwardButton == null))
            {
                return;
            }

            bool areButtonsShown = PageStackRules.ArePageButtonsShown(this.Children.Count);
            Visibility visibility = areButtonsShown ? Visibility.Visible : Visibility.Collapsed;
            if (this.PART_BackButton.Visibility != visibility)
            {
                this.PART_BackButton.Visibility = visibility;
                this.PART_ForwardButton.Visibility = visibility;
            }

            double clearance = PageStackRules.PageClearance(areButtonsShown, this.MeasureButtonLength(), this.PagePeek, this.PageSpacing);
            Thickness padding = EffectivePadding(this.PagePadding, m_isHorizontal, kDefaultPageGap);
            Thickness margin = m_isHorizontal
                ? new Thickness(clearance + padding.Left, padding.Top, clearance + padding.Right, padding.Bottom)
                : new Thickness(padding.Left, clearance + padding.Top, padding.Right, clearance + padding.Bottom);
            if (!this.PART_PageViewport.Margin.Equals(margin))
            {
                this.PART_PageViewport.Margin = margin;
            }

            this.RefreshPageLayout();
        }

        // Moves and fades each page for where the stack is now, mid transition or not
        private void RefreshPageLayout ()
        {
            if (this.PART_PageViewport == null)
            {
                return;
            }

            double pageLength = m_isHorizontal ? this.PART_PageViewport.ActualWidth : this.PART_PageViewport.ActualHeight;
            int shownPage = this.EffectiveSelectedIndex;
            for (int i = 0; i < m_pageHosts.Count; ++i)
            {
                ContentControl host = m_pageHosts[i];
                double offset = PageStackRules.PageOffset(i, m_position, pageLength, this.PageSpacing);
                var transform = (TranslateTransform)host.RenderTransform;
                transform.X = m_isHorizontal ? offset : 0.0;
                transform.Y = m_isHorizontal ? 0.0 : offset;
                host.Opacity = PageStackRules.PageOpacity(i, m_position, this.InactivePageOpacity);

                bool isShown = i == shownPage;
                if (host.IsEnabled != isShown)
                {
                    host.IsEnabled = isShown;
                    host.IsHitTestVisible = isShown;
                }
            }
        }

        // Enables the page buttons for the shown page, and matches the dot strip to it. With slideDots, the strip slides to
        //  its new place on the running transition's clock rather than taking it at once.
        private void RefreshButtonsAndDots (bool slideDots = false)
        {
            int pageCount = this.Children.Count;
            int shownPage = this.EffectiveSelectedIndex;
            if (this.PART_BackButton != null)
            {
                this.PART_BackButton.IsEnabled = PageStackRules.CanGoBack(shownPage);
            }

            if (this.PART_ForwardButton != null)
            {
                this.PART_ForwardButton.IsEnabled = PageStackRules.CanGoForward(shownPage, pageCount);
            }

            if ((this.PART_DotsHost == null) || (this.PART_DotsClip == null) || (this.PART_DotsPanel == null))
            {
                return;
            }

            bool areDotsShown = PageStackRules.AreDotsShown(this.ShowDots, pageCount);
            bool isPageCountShown = PageStackRules.IsPageCountShown(this.ShowPageCount, pageCount);
            this.PART_DotsHost.Visibility = (areDotsShown || isPageCountShown) ? Visibility.Visible : Visibility.Collapsed;
            this.PART_DotsHost.Padding = EffectivePadding(this.DotStripPadding, m_isHorizontal, kDefaultDotStripGap);
            this.PART_DotsClip.Visibility = areDotsShown ? Visibility.Visible : Visibility.Collapsed;
            if (this.PART_PageCountText != null)
            {
                this.PART_PageCountText.Visibility = isPageCountShown ? Visibility.Visible : Visibility.Collapsed;
                this.PART_PageCountText.Text = isPageCountShown
                    ? PageStackRules.PageCountText(this.PageCountFormat, shownPage, pageCount, isVertical: !m_isHorizontal)
                    : String.Empty;
            }

            if (!areDotsShown)
            {
                m_isDotSliding = false;
                this.SyncDotSpan(new PageStackRules.DotSpan(0, -1));
                return;
            }

            // 1. Where the strip is going, and whether it slides there or takes its place at once
            PageStackDotWindow window = PageStackDotWindow.For(pageCount, shownPage, this.DotReach);
            int toFirstDotPage = window.FirstDotPage;
            m_isDotSliding = slideDots && PageStackRules.ShouldSlideDots(m_dotScroll, toFirstDotPage, window.DotCount);
            if (!m_isDotSliding)
            {
                m_dotScroll = toFirstDotPage;
            }

            m_dotScrollFrom = m_dotScroll;
            m_dotScrollTo = toFirstDotPage;

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
                PageStackDot dot = m_dots[i];
                dot.PageIndex = page;
                dot.Kind = window.KindOf(page);
                dot.DotBrush = this.DotBrush;
                dot.CurrentDotBrush = this.CurrentDotBrush;
                dot.DotSize = this.DotSize;
                AutomationProperties.SetName(dot, $"Page {page + 1} of {pageCount}");
            }

            // 4. Size and clip the canvas to the new window's dots, and place the strip along it
            m_dotCellLength = this.MeasureDotCellLength();
            double stripLength = window.DotCount * m_dotCellLength;
            double width = m_isHorizontal ? stripLength : m_dotCellLength;
            double height = m_isHorizontal ? m_dotCellLength : stripLength;
            if ((this.PART_DotsClip.Width != width) || (this.PART_DotsClip.Height != height))
            {
                this.PART_DotsClip.Width = width;
                this.PART_DotsClip.Height = height;
                this.PART_DotsClip.Clip = new RectangleGeometry { Rect = new Rect(0.0, 0.0, width, height) };
            }

            this.ApplyDotScroll();
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
            var dot = new PageStackDot();
            dot.Click += this.OnDotClick;
            m_dots.Insert(index, dot);
            this.PART_DotsPanel.Children.Insert(index, dot);
        }

        private void RemoveDotAt (int index)
        {
            PageStackDot dot = m_dots[index];
            dot.Click -= this.OnDotClick;
            m_dots.RemoveAt(index);
            this.PART_DotsPanel?.Children.Remove(dot);
        }

        // Moves the strip's dots along the canvas so the dot for the page at m_dotScroll sits at its start
        private void ApplyDotScroll ()
        {
            if (this.PART_DotsPanel?.RenderTransform is TranslateTransform transform)
            {
                double offset = (m_dotSpanFirstPage - m_dotScroll) * m_dotCellLength;
                transform.X = m_isHorizontal ? offset : 0.0;
                transform.Y = m_isHorizontal ? 0.0 : offset;
            }
        }

        // Ends a dot slide where it was going, and lets go of the dots that slid out of view
        private void LandDotSlide ()
        {
            if (m_isDotSliding)
            {
                m_isDotSliding = false;
                m_dotScroll = m_dotScrollTo;
                this.RefreshButtonsAndDots();
            }
        }

        // A padding property left unset (its NaN default) is the gap given on the top when horizontal, the left when vertical
        private static Thickness EffectivePadding (Thickness padding, bool isHorizontal, double defaultGap)
        {
            if (double.IsNaN(padding.Left) || double.IsNaN(padding.Top) || double.IsNaN(padding.Right) || double.IsNaN(padding.Bottom))
            {
                return isHorizontal ? new Thickness(0, defaultGap, 0, 0) : new Thickness(defaultGap, 0, 0, 0);
            }

            return padding;
        }

        // A dot's length along the strip, padding included. Every dot takes the same room whatever its kind, since an edge
        //  dot shrinks by a render transform, so one dot's measure is every dot's.
        private double MeasureDotCellLength ()
        {
            double fallback = this.DotSize + (2.0 * kFallbackDotPadding);
            if (m_dots.Count == 0)
            {
                return fallback;
            }

            PageStackDot dot = m_dots[0];
            dot.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double length = m_isHorizontal ? dot.DesiredSize.Width : dot.DesiredSize.Height;
            return length > 0.0 ? length : fallback;
        }

        private double MeasureButtonLength ()
        {
            if (this.PART_BackButton == null)
            {
                return kFallbackButtonLength;
            }

            this.PART_BackButton.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double length = m_isHorizontal ? this.PART_BackButton.DesiredSize.Width : this.PART_BackButton.DesiredSize.Height;
            return length > 0.0 ? length : kFallbackButtonLength;
        }
    }
}
