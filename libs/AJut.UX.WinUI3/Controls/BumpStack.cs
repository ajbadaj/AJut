namespace AJut.UX.Controls
{
    using System;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Markup;
    using Microsoft.UI.Xaml.Media;
    using Windows.Foundation;
    using DPUtils = AJut.UX.DPUtils<BumpStack>;

    // ===========[ BumpStack ]==================================================
    // A scroll container that uses a chevron "bump" button at each end instead of a scrollbar. The
    // buttons nudge the content, disable at each end, and hide entirely when nothing overflows. The
    // content is inset by the button size so scrolling all the way parks a button over empty padding
    // rather than covering content. WinUI3 port of the WPF BumpStack.
    //
    // Template parts:
    //   PART_ScrollViewer        - hosts and scrolls the content
    //   PART_ContentPanel        - StackPanel holding Children (gets the button-clearance margin)
    //   PART_AnteriorBumpButton  - left/top chevron
    //   PART_PosteriorBumpButton - right/bottom chevron

    [ContentProperty(Name = nameof(Children))]
    [TemplatePart(Name = nameof(PART_ScrollViewer), Type = typeof(ScrollViewer))]
    [TemplatePart(Name = nameof(PART_ContentPanel), Type = typeof(StackPanel))]
    [TemplatePart(Name = nameof(PART_AnteriorBumpButton), Type = typeof(RepeatButton))]
    [TemplatePart(Name = nameof(PART_PosteriorBumpButton), Type = typeof(RepeatButton))]
    public class BumpStack : Control
    {
        // ===========[ Const-like ]===============================================
        private const double kFallbackBumpButtonExtent = 24.0;
        // Segoe MDL2 Assets chevron glyph code points (kept as ints so the source stays plain ASCII).
        private const int kLeftChevronCode = 0xE76B;
        private const int kRightChevronCode = 0xE76C;
        private const int kUpChevronCode = 0xE70E;
        private const int kDownChevronCode = 0xE70D;

        // ===========[ Instance fields ]==========================================
        private ScrollViewer PART_ScrollViewer;
        private StackPanel PART_ContentPanel;
        private RepeatButton PART_AnteriorBumpButton;
        private RepeatButton PART_PosteriorBumpButton;
        private bool m_isHorizontal = true;

        // ===========[ Construction ]=============================================
        public BumpStack ()
        {
            this.DefaultStyleKey = typeof(BumpStack);
            this.Children = new ObservableCollection<UIElement>();
            this.Children.CollectionChanged += this.OnChildrenChanged;
            this.SizeChanged += this.OnSizeChanged;
        }

        // ===========[ Properties ]===============================================
        public ObservableCollection<UIElement> Children { get; }

        // ===========[ Dependency Properties ]====================================
        public static readonly DependencyProperty OrientationProperty = DPUtils.Register(_ => _.Orientation, Orientation.Horizontal, (d, e) => d.OnOrientationChanged());
        public Orientation Orientation
        {
            get => (Orientation)this.GetValue(OrientationProperty);
            set => this.SetValue(OrientationProperty, value);
        }

        // ScrollingEnabled: when false the content is simply clipped and no bump buttons appear (lets a
        // host reuse the BumpStack purely as a clip container in non-scrolling states).
        public static readonly DependencyProperty ScrollingEnabledProperty = DPUtils.Register(_ => _.ScrollingEnabled, true, (d, e) => d.UpdateScrollMode());
        public bool ScrollingEnabled
        {
            get => (bool)this.GetValue(ScrollingEnabledProperty);
            set => this.SetValue(ScrollingEnabledProperty, value);
        }

        public static readonly DependencyProperty BumpAmountProperty = DPUtils.Register(_ => _.BumpAmount, 60.0);
        public double BumpAmount
        {
            get => (double)this.GetValue(BumpAmountProperty);
            set => this.SetValue(BumpAmountProperty, value);
        }

        public static readonly DependencyProperty BumpIntervalProperty = DPUtils.Register(_ => _.BumpInterval, 50);
        public int BumpInterval
        {
            get => (int)this.GetValue(BumpIntervalProperty);
            set => this.SetValue(BumpIntervalProperty, value);
        }

        public static readonly DependencyProperty BumpDelayProperty = DPUtils.Register(_ => _.BumpDelay, 300);
        public int BumpDelay
        {
            get => (int)this.GetValue(BumpDelayProperty);
            set => this.SetValue(BumpDelayProperty, value);
        }

        // EdgeClearanceOffset: extra space (on top of the button size) reserved at each end.
        public static readonly DependencyProperty EdgeClearanceOffsetProperty = DPUtils.Register(_ => _.EdgeClearanceOffset, 0.0, (d, e) => d.UpdateBumpButtons());
        public double EdgeClearanceOffset
        {
            get => (double)this.GetValue(EdgeClearanceOffsetProperty);
            set => this.SetValue(EdgeClearanceOffsetProperty, value);
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

        public static readonly DependencyProperty ButtonFontSizeProperty = DPUtils.Register(_ => _.ButtonFontSize, 12.0);
        public double ButtonFontSize
        {
            get => (double)this.GetValue(ButtonFontSizeProperty);
            set => this.SetValue(ButtonFontSizeProperty, value);
        }

        public static readonly DependencyProperty ButtonPaddingProperty = DPUtils.Register(_ => _.ButtonPadding, new Thickness(4, 0, 4, 0));
        public Thickness ButtonPadding
        {
            get => (Thickness)this.GetValue(ButtonPaddingProperty);
            set => this.SetValue(ButtonPaddingProperty, value);
        }

        // ===========[ Template application ]=====================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            // 1. Unwire old parts.
            if (this.PART_ScrollViewer != null)
            {
                this.PART_ScrollViewer.ViewChanged -= this.OnScrollViewChanged;
            }

            if (this.PART_AnteriorBumpButton != null)
            {
                this.PART_AnteriorBumpButton.Click -= this.OnAnteriorClick;
            }

            if (this.PART_PosteriorBumpButton != null)
            {
                this.PART_PosteriorBumpButton.Click -= this.OnPosteriorClick;
            }

            // 2. Acquire new parts.
            this.PART_ScrollViewer = this.GetTemplateChild(nameof(PART_ScrollViewer)) as ScrollViewer;
            this.PART_ContentPanel = this.GetTemplateChild(nameof(PART_ContentPanel)) as StackPanel;
            this.PART_AnteriorBumpButton = this.GetTemplateChild(nameof(PART_AnteriorBumpButton)) as RepeatButton;
            this.PART_PosteriorBumpButton = this.GetTemplateChild(nameof(PART_PosteriorBumpButton)) as RepeatButton;

            // 3. Wire new parts.
            if (this.PART_ScrollViewer != null)
            {
                this.PART_ScrollViewer.ViewChanged += this.OnScrollViewChanged;
            }

            if (this.PART_AnteriorBumpButton != null)
            {
                this.PART_AnteriorBumpButton.Click += this.OnAnteriorClick;
            }

            if (this.PART_PosteriorBumpButton != null)
            {
                this.PART_PosteriorBumpButton.Click += this.OnPosteriorClick;
            }

            this.ApplyOrientation();
            this.SyncChildren();
            this.UpdateScrollMode();
            this.UpdateBumpButtons();
        }

        // ===========[ Public interface methods ]=================================
        /// <summary>
        /// Scrolls so the first hosted element matching <paramref name="predicate"/> is brought fully
        /// into view, kept clear of the bump buttons. Returns true if a match was found.
        /// </summary>
        public bool ScrollFirstElementIntoView (Func<FrameworkElement, bool> predicate)
        {
            if (this.PART_ScrollViewer == null)
            {
                return false;
            }

            double inset = (this.PART_AnteriorBumpButton?.Visibility == Visibility.Visible)
                ? this.CurrentButtonClearance()
                : 0.0;

            return this.PART_ScrollViewer.ScrollFirstElementIntoView(predicate, inset, inset);
        }

        // ===========[ Events ]===================================================
        private void OnSizeChanged (object sender, SizeChangedEventArgs e) => this.UpdateBumpButtons();
        private void OnScrollViewChanged (object sender, ScrollViewerViewChangedEventArgs e) => this.UpdateBumpButtons();
        private void OnAnteriorClick (object sender, RoutedEventArgs e) => this.Nudge(-this.BumpAmount);
        private void OnPosteriorClick (object sender, RoutedEventArgs e) => this.Nudge(this.BumpAmount);

        private void OnChildrenChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            if (this.PART_ContentPanel == null)
            {
                return;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    int insertAt = e.NewStartingIndex;
                    foreach (UIElement added in e.NewItems)
                    {
                        this.PART_ContentPanel.Children.Insert(insertAt++, added);
                    }

                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (UIElement removed in e.OldItems)
                    {
                        this.PART_ContentPanel.Children.Remove(removed);
                    }

                    break;

                case NotifyCollectionChangedAction.Reset:
                    this.PART_ContentPanel.Children.Clear();
                    break;

                default:
                    this.SyncChildren();
                    return;
            }

            this.UpdateBumpButtons();
        }

        protected override void OnPointerWheelChanged (PointerRoutedEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            // Let vertical mode fall through to native wheel handling; horizontal mode maps the wheel
            // onto horizontal scrolling (there is otherwise no way to wheel a horizontal strip).
            if (!this.ScrollingEnabled || this.PART_ScrollViewer == null || !m_isHorizontal)
            {
                return;
            }

            int delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
            if (delta == 0)
            {
                return;
            }

            this.Nudge(delta > 0 ? -this.BumpAmount : this.BumpAmount);
            e.Handled = true;
        }

        // ===========[ Helpers ]==================================================
        private void OnOrientationChanged ()
        {
            this.ApplyOrientation();
            this.UpdateScrollMode();
            this.UpdateBumpButtons();
        }

        private void ApplyOrientation ()
        {
            m_isHorizontal = this.Orientation == Orientation.Horizontal;

            if (this.PART_ContentPanel != null)
            {
                this.PART_ContentPanel.Orientation = this.Orientation;
            }

            if (this.PART_AnteriorBumpButton != null)
            {
                this.PART_AnteriorBumpButton.Content = this.MakeChevron(m_isHorizontal ? kLeftChevronCode : kUpChevronCode);
                this.PART_AnteriorBumpButton.HorizontalAlignment = m_isHorizontal ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
                this.PART_AnteriorBumpButton.VerticalAlignment = m_isHorizontal ? VerticalAlignment.Stretch : VerticalAlignment.Top;
            }

            if (this.PART_PosteriorBumpButton != null)
            {
                this.PART_PosteriorBumpButton.Content = this.MakeChevron(m_isHorizontal ? kRightChevronCode : kDownChevronCode);
                this.PART_PosteriorBumpButton.HorizontalAlignment = m_isHorizontal ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
                this.PART_PosteriorBumpButton.VerticalAlignment = m_isHorizontal ? VerticalAlignment.Stretch : VerticalAlignment.Bottom;
            }
        }

        private void SyncChildren ()
        {
            if (this.PART_ContentPanel == null)
            {
                return;
            }

            this.PART_ContentPanel.Children.Clear();
            foreach (UIElement child in this.Children)
            {
                this.PART_ContentPanel.Children.Add(child);
            }

            this.UpdateBumpButtons();
        }

        private void UpdateScrollMode ()
        {
            if (this.PART_ScrollViewer == null)
            {
                return;
            }

            // The scrollbar is always hidden - the bump buttons are the affordance. ScrollMode is
            // Disabled when clipping so the content is constrained to the viewport instead of scrolling.
            ScrollMode active = this.ScrollingEnabled ? ScrollMode.Enabled : ScrollMode.Disabled;
            this.PART_ScrollViewer.HorizontalScrollMode = m_isHorizontal ? active : ScrollMode.Disabled;
            this.PART_ScrollViewer.VerticalScrollMode = m_isHorizontal ? ScrollMode.Disabled : active;
            this.PART_ScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            this.PART_ScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;

            this.UpdateBumpButtons();
        }

        // Shows/enables the bump buttons against the current scroll extent, and reserves their width as
        // content-panel margin so scrolling to an end parks a button over empty padding, not content.
        private void UpdateBumpButtons ()
        {
            if (this.PART_AnteriorBumpButton == null || this.PART_PosteriorBumpButton == null)
            {
                return;
            }

            double scrollable = m_isHorizontal
                ? (this.PART_ScrollViewer?.ScrollableWidth ?? 0.0)
                : (this.PART_ScrollViewer?.ScrollableHeight ?? 0.0);
            bool show = this.ScrollingEnabled && scrollable > 0.5;

            Visibility vis = show ? Visibility.Visible : Visibility.Collapsed;
            SetVisibilityIfChanged(this.PART_AnteriorBumpButton, vis);
            SetVisibilityIfChanged(this.PART_PosteriorBumpButton, vis);

            double clearance = show ? this.CurrentButtonClearance() : 0.0;
            Thickness margin = m_isHorizontal
                ? new Thickness(clearance, 0, clearance, 0)
                : new Thickness(0, clearance, 0, clearance);
            if (this.PART_ContentPanel != null && !this.PART_ContentPanel.Margin.Equals(margin))
            {
                this.PART_ContentPanel.Margin = margin;
            }

            if (show)
            {
                double offset = m_isHorizontal ? this.PART_ScrollViewer.HorizontalOffset : this.PART_ScrollViewer.VerticalOffset;
                bool canAnterior = offset > 0.5;
                bool canPosterior = offset < scrollable - 0.5;
                if (this.PART_AnteriorBumpButton.IsEnabled != canAnterior)
                {
                    this.PART_AnteriorBumpButton.IsEnabled = canAnterior;
                }

                if (this.PART_PosteriorBumpButton.IsEnabled != canPosterior)
                {
                    this.PART_PosteriorBumpButton.IsEnabled = canPosterior;
                }
            }
        }

        private void Nudge (double delta)
        {
            if (this.PART_ScrollViewer == null)
            {
                return;
            }

            if (m_isHorizontal)
            {
                double target = Math.Max(0.0, Math.Min(this.PART_ScrollViewer.ScrollableWidth, this.PART_ScrollViewer.HorizontalOffset + delta));
                this.PART_ScrollViewer.ChangeView(target, null, null);
            }
            else
            {
                double target = Math.Max(0.0, Math.Min(this.PART_ScrollViewer.ScrollableHeight, this.PART_ScrollViewer.VerticalOffset + delta));
                this.PART_ScrollViewer.ChangeView(null, target, null);
            }
        }

        private double CurrentButtonClearance () => this.MeasureButtonExtent() + this.EdgeClearanceOffset;

        private double MeasureButtonExtent ()
        {
            if (this.PART_AnteriorBumpButton == null)
            {
                return kFallbackBumpButtonExtent;
            }

            this.PART_AnteriorBumpButton.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double extent = m_isHorizontal ? this.PART_AnteriorBumpButton.DesiredSize.Width : this.PART_AnteriorBumpButton.DesiredSize.Height;
            return extent > 0.0 ? extent : kFallbackBumpButtonExtent;
        }

        private FontIcon MakeChevron (int glyphCode) => new FontIcon { FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = char.ConvertFromUtf32(glyphCode), FontSize = this.ButtonFontSize };

        private static void SetVisibilityIfChanged (UIElement element, Visibility visibility)
        {
            if (element.Visibility != visibility)
            {
                element.Visibility = visibility;
            }
        }
    }
}
