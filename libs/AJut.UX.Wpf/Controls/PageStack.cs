namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Automation;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Markup;
    using System.Windows.Media;
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
    // one is disabled and takes no hits, so its peek can't be clicked or tabbed into. This is the WPF port of WinUI3's
    // PageStack; the orientation is the template's triggers here, where WinUI3 uses visual states.
    //
    // A transition runs on CompositionTarget.Rendering, rather than a storyboard, so all three stacks step through the
    // same shared position math. The stack subscribes only while a transition runs, and unsubscribes when it lands or the
    // stack unloads: Rendering is static, so a subscription left behind would pin the stack.
    //
    // Template parts:
    //   PART_PageViewport  - the shown page's area, inset from both ends; holds the page hosts
    //   PART_BackButton    - left/top page button
    //   PART_ForwardButton - right/bottom page button
    //   PART_DotsPanel     - holds the dots
    /// <summary>
    /// Shows one of its children at a time as a page, with a page button at each end and an optional dot strip, and runs an
    /// animated transition to whichever page is picked. The neighbor pages peek in, faded, past the page buttons.
    /// </summary>
    [ContentProperty(nameof(Children))]
    [DefaultProperty(nameof(Children))]
    [TemplatePart(Name = nameof(PART_PageViewport), Type = typeof(Grid))]
    [TemplatePart(Name = nameof(PART_BackButton), Type = typeof(Button))]
    [TemplatePart(Name = nameof(PART_ForwardButton), Type = typeof(Button))]
    [TemplatePart(Name = nameof(PART_DotsPanel), Type = typeof(StackPanel))]
    public class PageStack : Control
    {
        private const double kFallbackButtonLength = 24.0;

        private readonly List<ContentControl> m_pageHosts = new List<ContentControl>();
        private readonly List<PageStackDot> m_dots = new List<PageStackDot>();
        private bool m_isHorizontal = true;
        private bool m_isAdjustingSelection;
        private double m_position;
        private double m_transitionFrom;
        private double m_transitionTo;
        private long m_transitionStartTimestamp;
        private bool m_isTransitioning;

        // ===========[ Setup/Construction/Teardown ]===========================
        static PageStack ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PageStack), new FrameworkPropertyMetadata(typeof(PageStack)));
        }

        public PageStack ()
        {
            this.Children = new ObservableCollection<UIElement>();
            this.Children.CollectionChanged += this.OnChildrenChanged;
            this.Unloaded += this.OnUnloaded;
        }

        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            // 1. Unwire the old parts, and take the page hosts and dots out of them, since an element can only have one parent
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
            this.PART_PageViewport = this.GetTemplateChild(nameof(PART_PageViewport)) as Grid;
            this.PART_BackButton = this.GetTemplateChild(nameof(PART_BackButton)) as Button;
            this.PART_ForwardButton = this.GetTemplateChild(nameof(PART_ForwardButton)) as Button;
            this.PART_DotsPanel = this.GetTemplateChild(nameof(PART_DotsPanel)) as StackPanel;

            // 3. Wire them, and put the page hosts and dots back in
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

            m_isHorizontal = this.Orientation == Orientation.Horizontal;
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

        private void OnRendering (object sender, EventArgs e)
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(m_transitionStartTimestamp);
            m_position = PageStackRules.TransitionPosition(m_transitionFrom, m_transitionTo, elapsed, this.TransitionDuration);
            if (PageStackRules.IsTransitionDone(elapsed, this.TransitionDuration))
            {
                this.StopTransition();
            }

            this.RefreshPageLayout();
        }

        protected override void OnKeyDown (KeyEventArgs e)
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
                case Key.Left when m_isHorizontal:
                case Key.Up when !m_isHorizontal:
                    target = this.EffectiveSelectedIndex - 1;
                    break;

                case Key.Right when m_isHorizontal:
                case Key.Down when !m_isHorizontal:
                    target = this.EffectiveSelectedIndex + 1;
                    break;

                case Key.Home:
                    target = 0;
                    break;

                case Key.End:
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
            this.RefreshButtonsAndDots();
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

        public static readonly DependencyProperty ButtonFontSizeProperty = DPUtils.Register(_ => _.ButtonFontSize, 16.0, (d, e) => d.RefreshPageGeometry());
        public double ButtonFontSize
        {
            get => (double)this.GetValue(ButtonFontSizeProperty);
            set => this.SetValue(ButtonFontSizeProperty, value);
        }

        public static readonly DependencyProperty ButtonPaddingProperty = DPUtils.Register(_ => _.ButtonPadding, new Thickness(7.0), (d, e) => d.RefreshPageGeometry());
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

        // ===========[ Properties ]============================================
        /// <summary>The pages, in order. In XAML, nest them directly inside the PageStack.</summary>
        public ObservableCollection<UIElement> Children { get; }

                private Grid PART_PageViewport { get; set; }
        private Button PART_BackButton { get; set; }
        private Button PART_ForwardButton { get; set; }
        private StackPanel PART_DotsPanel { get; set; }

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

        // The template's triggers turn the page buttons and the dot strip
        private void OnOrientationChanged ()
        {
            m_isHorizontal = this.Orientation == Orientation.Horizontal;
            this.RefreshPageGeometry();
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
                Focusable = false,
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
            Thickness margin = m_isHorizontal ? new Thickness(clearance, 0, clearance, 0) : new Thickness(0, clearance, 0, clearance);
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

        private void RefreshButtonsAndDots ()
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

            if (this.PART_DotsPanel == null)
            {
                return;
            }

            bool areDotsShown = PageStackRules.AreDotsShown(this.ShowDots, pageCount);
            this.PART_DotsPanel.Visibility = areDotsShown ? Visibility.Visible : Visibility.Collapsed;
            PageStackDotWindow window = areDotsShown ? PageStackDotWindow.For(pageCount, shownPage, this.DotReach) : default;
            int dotCount = areDotsShown ? window.DotCount : 0;

            // 1. Match the dot count, reusing the dots there are
            while (m_dots.Count > dotCount)
            {
                PageStackDot extra = m_dots[m_dots.Count - 1];
                extra.Click -= this.OnDotClick;
                m_dots.RemoveAt(m_dots.Count - 1);
                this.PART_DotsPanel.Children.Remove(extra);
            }

            while (m_dots.Count < dotCount)
            {
                var dot = new PageStackDot();
                dot.Click += this.OnDotClick;
                m_dots.Add(dot);
                this.PART_DotsPanel.Children.Add(dot);
            }

            // 2. Fill each for the page it stands for
            for (int i = 0; i < dotCount; ++i)
            {
                PageStackDotWindow.Dot info = window.DotAt(i);
                PageStackDot dot = m_dots[i];
                dot.PageIndex = info.PageIndex;
                dot.Kind = info.Kind;
                dot.DotBrush = this.DotBrush;
                dot.CurrentDotBrush = this.CurrentDotBrush;
                dot.DotSize = this.DotSize;
                AutomationProperties.SetName(dot, $"Page {info.PageIndex + 1} of {pageCount}");
            }
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
