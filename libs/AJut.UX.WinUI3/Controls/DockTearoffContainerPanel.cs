namespace AJut.UX.Controls
{
    using System;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using Windows.Foundation;
    using DPUtils = AJut.UX.DPUtils<DockTearoffContainerPanel>;

    // ===========[ DockTearoffContainerPanel ]===================================
    // WinUI3-specific: no WPF equivalent.
    // Window content for DockingManager tearoff windows. Provides a two-row
    // layout: a custom title bar (drag handle + close button) in row 0 and a
    // DockZone in row 1.
    //
    // The title bar region (PART_TitleBarArea) detects pointer presses and
    // raises TitleBarDragInitiated once the cursor travels more than
    // DragThresholdPixels from the original press point.  PART_CloseButton
    // handles its own pointer events before they bubble to the title bar, so
    // clicking the close button never accidentally starts a drag.
    //
    // DockingManager creates an instance of this panel, subscribes to
    // TitleBarDragInitiated and CloseRequested, then assigns it as the
    // window's Content.
    //
    // Template parts:
    //   PART_TitleBarArea  - FrameworkElement receiving drag pointer events
    //   PART_CloseButton   - Button that raises CloseRequested on click
    //   PART_ZonePresenter - ContentPresenter displaying DockZoneContent
    //
    // WinUI3 TemplateBinding does not backfill values set before template
    // application, so DockZoneContent is pushed to PART_ZonePresenter.Content
    // in both OnApplyTemplate and the DP change handler.

    [TemplatePart(Name = nameof(PART_TitleBarArea), Type = typeof(FrameworkElement))]
    [TemplatePart(Name = nameof(PART_CloseButton), Type = typeof(Button))]
    [TemplatePart(Name = nameof(PART_ZonePresenter), Type = typeof(ContentPresenter))]
    public sealed class DockTearoffContainerPanel : Control
    {
        // ===========[ Constants ]============================================
        private const double kDefaultDragThresholdPixels = 8.0;

        // ===========[ Fields ]===============================================
        private FrameworkElement m_titleBarArea;
        private Button m_closeButton;
        private bool m_dragPending;
        private Point m_localPressPt;

        // ===========[ Construction ]=========================================
        public DockTearoffContainerPanel()
        {
            this.DefaultStyleKey = typeof(DockTearoffContainerPanel);
            this.DragThresholdPixels = kDefaultDragThresholdPixels;
        }

        // ===========[ Events ]===============================================

        // Raised when the user drags the title bar past DragThresholdPixels.
        // The Point argument is the cursor position within the title bar at
        // the time of the original press (i.e. the cursor-in-window offset).
        public event EventHandler<Point> TitleBarDragInitiated;

        // Raised when PART_CloseButton is clicked.
        public event EventHandler CloseRequested;

        // ===========[ Template Parts ]=======================================

        public FrameworkElement PART_TitleBarArea { get; private set; }
        public Button PART_CloseButton { get; private set; }
        public ContentPresenter PART_ZonePresenter { get; private set; }

        // ===========[ Dependency Properties ]================================

        public static readonly DependencyProperty DockZoneContentProperty = DPUtils.Register(_ => _.DockZoneContent, (d, e) => d.ApplyDockZoneContent());
        public DockZone DockZoneContent
        {
            get => (DockZone)this.GetValue(DockZoneContentProperty);
            set => this.SetValue(DockZoneContentProperty, value);
        }

        public static readonly DependencyProperty TitleBarBackgroundProperty = DPUtils.Register(_ => _.TitleBarBackground);
        public Brush TitleBarBackground
        {
            get => (Brush)this.GetValue(TitleBarBackgroundProperty);
            set => this.SetValue(TitleBarBackgroundProperty, value);
        }

        public static readonly DependencyProperty TitleBarForegroundProperty = DPUtils.Register(_ => _.TitleBarForeground);
        public Brush TitleBarForeground
        {
            get => (Brush)this.GetValue(TitleBarForegroundProperty);
            set => this.SetValue(TitleBarForegroundProperty, value);
        }

        public static readonly DependencyProperty TitleBarHeightProperty = DPUtils.Register(_ => _.TitleBarHeight);
        public double TitleBarHeight
        {
            get => (double)this.GetValue(TitleBarHeightProperty);
            set => this.SetValue(TitleBarHeightProperty, value);
        }

        // ===========[ Properties ]============================================

        // Pixels the pointer must travel before TitleBarDragInitiated is raised.
        // Set from DockingManager.DragThresholdPixels after construction.
        public double DragThresholdPixels { get; set; }

        // ===========[ Setup/Construction/Teardown ]===========================

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Unhook previous parts
            if (m_titleBarArea != null)
            {
                m_titleBarArea.PointerPressed -= this.OnTitleBarPointerPressed;
                m_titleBarArea.PointerMoved -= this.OnTitleBarPointerMoved;
                m_titleBarArea.PointerReleased -= this.OnTitleBarPointerReleased;
                m_titleBarArea.PointerCaptureLost -= this.OnTitleBarPointerCaptureLost;
            }

            if (m_closeButton != null)
            {
                m_closeButton.Click -= this.OnCloseButtonClicked;
            }

            // Resolve new parts
            this.PART_TitleBarArea = m_titleBarArea = this.GetTemplateChild(nameof(PART_TitleBarArea)) as FrameworkElement;
            this.PART_CloseButton = m_closeButton = this.GetTemplateChild(nameof(PART_CloseButton)) as Button;
            this.PART_ZonePresenter = this.GetTemplateChild(nameof(PART_ZonePresenter)) as ContentPresenter;

            // Hook new parts
            if (m_titleBarArea != null)
            {
                m_titleBarArea.PointerPressed += this.OnTitleBarPointerPressed;
                m_titleBarArea.PointerMoved += this.OnTitleBarPointerMoved;
                m_titleBarArea.PointerReleased += this.OnTitleBarPointerReleased;
                m_titleBarArea.PointerCaptureLost += this.OnTitleBarPointerCaptureLost;
            }

            if (m_closeButton != null)
            {
                m_closeButton.Click += this.OnCloseButtonClicked;
            }

            this.ApplyDockZoneContent();
        }

        // ===========[ Events ]===============================================

        private void OnTitleBarPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;
            m_dragPending = true;
            m_localPressPt = e.GetCurrentPoint(m_titleBarArea).Position;
            m_titleBarArea.CapturePointer(e.Pointer);
        }

        private void OnTitleBarPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!m_dragPending)
            {
                return;
            }

            var cur = e.GetCurrentPoint(m_titleBarArea).Position;
            double dx = cur.X - m_localPressPt.X;
            double dy = cur.Y - m_localPressPt.Y;
            double threshold = this.DragThresholdPixels;

            if ((dx * dx + dy * dy) > (threshold * threshold))
            {
                m_dragPending = false;
                m_titleBarArea.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
                this.TitleBarDragInitiated?.Invoke(this, m_localPressPt);
            }
        }

        private void OnTitleBarPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            m_dragPending = false;
            m_titleBarArea.ReleasePointerCapture(e.Pointer);
        }

        private void OnTitleBarPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            m_dragPending = false;
        }

        private void OnCloseButtonClicked(object sender, RoutedEventArgs e)
        {
            this.CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        // ===========[ Private Helpers ]======================================

        private void ApplyDockZoneContent()
        {
            if (this.PART_ZonePresenter != null)
            {
                this.PART_ZonePresenter.Content = this.DockZoneContent;
            }
        }
    }
}
