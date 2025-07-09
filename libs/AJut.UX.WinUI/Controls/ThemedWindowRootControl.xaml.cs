// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AJut.UX.Controls
{
    using System;
    using Microsoft.UI;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Windowing;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Markup;
    using Microsoft.UI.Xaml.Media;
    using Windows.Foundation;
    using Windows.UI;
    using DPUtils = DPUtils<ThemedWindowRootControl>;

    /*
     * TODOs:
     * - Move over converters
     * - Inactive background color
     * 
     */

    [ContentProperty(Name = nameof(WindowContents))]
    [TemplatePart(Name = nameof(PART_CustomTitleBarContainer), Type = typeof(Panel))]
    [TemplatePart(Name = nameof(PART_TitleBarBorder), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_ClientContainer), Type = typeof(ContentControl))]
    [TemplatePart(Name = nameof(PART_WindowRoot), Type = typeof(UIElement))]
    [TemplatePart(Name = nameof(PART_EnterFullscreenButton), Type = typeof(Button))]
    [TemplatePart(Name = nameof(PART_ExitFullscreenButton), Type = typeof(Button))] 
    public class ThemedWindowRootControl : Control
    {
        private static readonly SolidColorBrush kTransparent = new SolidColorBrush(Colors.Transparent);

        private eWindowState m_cachedWindowState = eWindowState.Unknown;
        private Window m_owner;
        private SolidColorBrush m_normalBorderHighlightBrush;
        private bool m_isHighlightingWindowChromeButtons;
        private DispatcherQueueTimer m_winUI3BugFixTimer;
        private uint m_activePointerId = 0;

        public ThemedWindowRootControl ()
        {
            this.DefaultStyleKey = typeof(ThemedWindowRootControl);
            this.Loaded += this.OnLoaded;
            this.PointerMoved += this.OnPointerMoved;
            this.PointerExited += this.OnPointerExited;
        }

        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
            this.PART_WindowRoot = (Panel)this.GetTemplateChild(nameof(PART_WindowRoot));
            this.PART_CustomTitleBarContainer = (Panel)this.GetTemplateChild(nameof(PART_CustomTitleBarContainer));
            this.PART_TitleBarBorder = (Border)this.GetTemplateChild(nameof(PART_TitleBarBorder));
            this.PART_ClientContainer = (ContentControl)this.GetTemplateChild(nameof(PART_ClientContainer));

            if (this.PART_EnterFullscreenButton != null)
            {
                this.PART_EnterFullscreenButton.Click -= this.EnterFullScreen_OnClick;
            }
            this.PART_EnterFullscreenButton = (Button)this.GetTemplateChild(nameof(PART_EnterFullscreenButton));
            this.PART_EnterFullscreenButton.Click += this.EnterFullScreen_OnClick;

            this.PART_ExitFullscreenButton = (Button)this.GetTemplateChild(nameof(PART_ExitFullscreenButton));
            this.PART_ExitFullscreenButton.Click += this.ExitFullScreen_OnClick;
            if (m_owner != null)
            {
                m_owner.SetTitleBar(this.PART_CustomTitleBarContainer);
                this.ResetFullscreenButtons();
            }

            //if (m_winUI3BugFixTimer == null)
            //{
            //    m_winUI3BugFixTimer = this.DispatcherQueue.CreateTimer();
            //    m_winUI3BugFixTimer.Interval = TimeSpan.FromMilliseconds(1000);
            //    m_winUI3BugFixTimer.IsRepeating = true;
            //    m_winUI3BugFixTimer.Tick += (s, e) =>
            //    {
            //        if (m_owner.IsActivated() && m_isHighlightingWindowChromeButtons)
            //        {
            //            if (m_activePointerId != 0)
            //            {
            //                Windows.UI.Input.PointerPoint pointer = Windows.UI.Input.PointerPoint.GetCurrentPoint(m_activePointerId);
            //                this.HandlePointerMoved(pointer.Position);
            //            }
            //        }
            //        else
            //        {
            //            this.HandlePointerNotInWindowChromeButtonRange();
            //        }
            //    };
            //    m_winUI3BugFixTimer.Start();
            //}
        }

        private void EnterFullScreen_OnClick (object sender, RoutedEventArgs e)
        {
            m_owner?.EnterFullscreen();
        }

        private void ExitFullScreen_OnClick (object sender, RoutedEventArgs e)
        {
            m_owner?.ExitFullscreen(this.MinimumRestoredWindowWidth, this.MinimumRestoredWindowHeight);
        }

        public Panel PART_WindowRoot { get; private set; }
        public Panel PART_CustomTitleBarContainer { get; private set; }
        public Border PART_TitleBarBorder { get; private set; }
        public ContentControl PART_ClientContainer { get; private set; }
        public Button PART_EnterFullscreenButton { get; private set; }
        public Button PART_ExitFullscreenButton { get; private set; }
        public void SetupFor(Window owner)
        {
            m_owner = owner;
            m_owner.AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            m_owner.PerformPresenterTask((OverlappedPresenter p) =>
            {
                p.PreferredMinimumWidth = this.MinimumRestoredWindowWidth;
                p.PreferredMinimumHeight = this.MinimumRestoredWindowWidth;
            });

            if (this.PART_CustomTitleBarContainer != null)
            {
                m_owner.SetTitleBar(this.PART_CustomTitleBarContainer);
            }

            m_owner.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            m_owner.TrackActivation(isActivated: true);

            m_owner.SizeChanged -= this.OnSizeChanged;
            m_owner.SizeChanged += this.OnSizeChanged;
            m_owner.Activated -= this.OwnerWindow_OnActivated;
            m_owner.Activated += this.OwnerWindow_OnActivated;
            m_owner.AppWindow.Changed -= this.OwnerWindow_OnAppWindowChanged;
            m_owner.AppWindow.Changed += this.OwnerWindow_OnAppWindowChanged;

            m_owner.AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            m_owner.AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            m_owner.AppWindow.TitleBar.ButtonHoverBackgroundColor = this.TitleBarButtonHighlightColor;
            //m_owner.AppWindow.TitleBar.ButtonHoverBackgroundColor = this.GetThemeResource<Color>("SystemAccentColor");

            // Other, keep gone
            //this.AppWindow.TitleBar.ButtonPressedBackgroundColor = Colors.Transparent;
            //this.AppWindow.TitleBar.ButtonForegroundColor = Colors.Transparent;
            //this.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Transparent;

            //this.SetWindowAsFrameless();
            if (this.IsLoaded)
            {
                this.HandlePrimaryRefresh();
            }
        }

        private void OwnerWindow_OnAppWindowChanged (AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPresenterChange)
            {
                this.ResetFullscreenButtons();
            }
        }

        public void DisconnectFromOwnerWindow()
        {
            if (m_owner == null)
            {
                return;
            }

            m_owner.Activated -= this.OwnerWindow_OnActivated;
            m_owner.SizeChanged -= this.OnSizeChanged;
            m_owner.AppWindow.Changed -= this.OwnerWindow_OnAppWindowChanged;
            m_owner.StopTrackingActivation();
            m_owner = null;
        }


        #region Dependency Properties

        public static readonly DependencyProperty CustomTilteBarProperty = DPUtils.Register(_ => _.CustomTilteBar);
        public UIElement CustomTilteBar
        {
            get => (UIElement)this.GetValue(CustomTilteBarProperty);
            set => this.SetValue(CustomTilteBarProperty, value);
        }


        public static readonly DependencyProperty WindowContentsProperty = DPUtils.Register(_ => _.WindowContents);
        public UIElement WindowContents
        {
            get => (UIElement)this.GetValue(WindowContentsProperty);
            set => this.SetValue(WindowContentsProperty, value);
        }

        public static readonly DependencyProperty MinimumRestoredWindowWidthProperty = DPUtils.Register(_ => _.MinimumRestoredWindowWidth, 1);
        public int MinimumRestoredWindowWidth
        {
            get => (int)this.GetValue(MinimumRestoredWindowWidthProperty);
            set => this.SetValue(MinimumRestoredWindowWidthProperty, value);
        }

        public static readonly DependencyProperty MinimumRestoredWindowHeightProperty = DPUtils.Register(_ => _.MinimumRestoredWindowHeight, 1);
        public int MinimumRestoredWindowHeight
        {
            get => (int)this.GetValue(MinimumRestoredWindowHeightProperty);
            set => this.SetValue(MinimumRestoredWindowHeightProperty, value);
        }

        public static readonly DependencyProperty TitleBarButtonHighlightColorProperty = DPUtils.Register(_ => _.TitleBarButtonHighlightColor, (d,e)=>d.OnTitleBarButtonHighlightColorChanged());
        public Color TitleBarButtonHighlightColor
        {
            get => (Color)this.GetValue(TitleBarButtonHighlightColorProperty);
            set => this.SetValue(TitleBarButtonHighlightColorProperty, value);
        }
        public static readonly DependencyProperty CloseButtonHighlightBrushProperty = DPUtils.Register(_ => _.CloseButtonHighlightBrush, CoerceUtils.CoerceBrushFrom("#E81123"));
        public Brush CloseButtonHighlightBrush
        {
            get => (Brush)this.GetValue(CloseButtonHighlightBrushProperty);
            set => this.SetValue(CloseButtonHighlightBrushProperty, value);
        }

        public static readonly DependencyProperty ActiveTitlebarBackgroundProperty = DPUtils.Register(_ => _.ActiveTitlebarBackground, CoerceUtils.CoerceBrushFrom("#202020"));
        public Brush ActiveTitlebarBackground
        {
            get => (Brush)this.GetValue(ActiveTitlebarBackgroundProperty);
            set => this.SetValue(ActiveTitlebarBackgroundProperty, value);
        }

        public static readonly DependencyProperty InactiveTitlebarBackgroundProperty = DPUtils.Register(_ => _.InactiveTitlebarBackground, CoerceUtils.CoerceBrushFrom("#2E2E2E"));
        public Brush InactiveTitlebarBackground
        {
            get => (Brush)this.GetValue(InactiveTitlebarBackgroundProperty);
            set => this.SetValue(InactiveTitlebarBackgroundProperty, value);
        }

        public static readonly DependencyProperty CurrentlySetTitlebarBackgroundProperty = DPUtils.Register(_ => _.CurrentlySetTitlebarBackground);
        public Brush CurrentlySetTitlebarBackground
        {
            get => (Brush)this.GetValue(CurrentlySetTitlebarBackgroundProperty);
            set => this.SetValue(CurrentlySetTitlebarBackgroundProperty, value);
        }

        public static readonly DependencyProperty TargetBorderThicknessProperty = DPUtils.Register(_ => _.TargetBorderThickness, new Thickness(2));
        public Thickness TargetBorderThickness
        {
            get => (Thickness)this.GetValue(TargetBorderThicknessProperty);
            set => this.SetValue(TargetBorderThicknessProperty, value);
        }

        public static readonly DependencyProperty ShowFullscreenButtonsProperty = DPUtils.Register(_ => _.ShowFullscreenButtons, true, (d,e)=>d.ResetFullscreenButtons());
        public bool ShowFullscreenButtons
        {
            get => (bool)this.GetValue(ShowFullscreenButtonsProperty);
            set => this.SetValue(ShowFullscreenButtonsProperty, value);
        }

        #endregion // Dependency Properties

        private void ResetFullscreenButtons()
        {
            if (m_owner == null || this.PART_EnterFullscreenButton == null || this.PART_ExitFullscreenButton == null)
            {
                return;
            }

            if (this.ShowFullscreenButtons)
            {
                if (m_owner.IsFullScreened())
                {
                    this.PART_EnterFullscreenButton.Visibility = Visibility.Collapsed;
                    this.PART_ExitFullscreenButton.Visibility = Visibility.Visible;
                }
                else
                {
                    this.PART_EnterFullscreenButton.Visibility = Visibility.Visible;
                    this.PART_ExitFullscreenButton.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                this.PART_EnterFullscreenButton.Visibility = Visibility.Collapsed;
                this.PART_ExitFullscreenButton.Visibility = Visibility.Collapsed;
            }
        }

        private void OwnerWindow_OnActivated (object sender, WindowActivatedEventArgs args)
        {
            if (m_owner.IsActivated())
            {
                this.CurrentlySetTitlebarBackground = this.ActiveTitlebarBackground;
            }
            else
            {
                this.HandlePointerNotInWindowChromeButtonRange();
                this.CurrentlySetTitlebarBackground = this.InactiveTitlebarBackground;
                m_activePointerId = 0;
            }
        }
        private void OnTitleBarButtonHighlightColorChanged ()
        {
            if (m_owner != null)
            {
                m_owner.AppWindow.TitleBar.ButtonHoverBackgroundColor = this.TitleBarButtonHighlightColor;
            }

            m_normalBorderHighlightBrush = new SolidColorBrush(this.TitleBarButtonHighlightColor);
        }

        private void HandlePointerNotInWindowChromeButtonRange()
        {
            if (this.PART_WindowRoot != null)
            {
                this.PART_WindowRoot.Background = kTransparent;
            }

            m_isHighlightingWindowChromeButtons = false;
        }

        private void OnPointerMoved (object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            m_activePointerId = e.Pointer.PointerId;
            this.HandlePointerMoved(e.GetCurrentPoint(this).Position);
        }

        private void HandlePointerMoved(Point point)
        {
            if (this.PART_WindowRoot == null)
            {
                return;
            }

            if (m_owner.IsFullScreened())
            {
                return;
            }

            if (point.Y < 32 && point.Y > this.TargetBorderThickness.Top && point.X < this.ActualWidth - this.TargetBorderThickness.Right)
            {
                if (point.X > this.ActualWidth - 46)
                {
                    this.PART_WindowRoot.Background = this.CloseButtonHighlightBrush;
                    m_isHighlightingWindowChromeButtons = true;
                    return;
                }
                else if (point.X > this.ActualWidth - 138)
                {
                    this.PART_WindowRoot.Background = m_owner.IsActivated() ? m_normalBorderHighlightBrush : this.InactiveTitlebarBackground;
                    m_isHighlightingWindowChromeButtons = true;
                    return;
                }
            }

            this.HandlePointerNotInWindowChromeButtonRange();
        }


        private void OnPointerExited (object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.HandlePointerNotInWindowChromeButtonRange();
        }


        private void OnLoaded (object sender, RoutedEventArgs e)
        {
            this.HandlePrimaryRefresh();
        }

        private void SetDragRegion ()
        {
            if (m_owner == null || this.PART_CustomTitleBarContainer == null)
            {
                return;
            }

            double dpiScale = m_owner.DetermineActiveDPIScale();
            var rect = new Windows.Graphics.RectInt32(
                0,
                0,
                (int)(this.PART_CustomTitleBarContainer.ActualWidth * dpiScale),
                (int)(this.PART_CustomTitleBarContainer.ActualHeight * dpiScale)
            );

            // Set the drag rectangles
            m_owner.AppWindow.TitleBar.SetDragRectangles([rect]);
        }

        private void OnSizeChanged (object sender, WindowSizeChangedEventArgs args)
        {
            this.HandlePrimaryRefresh();
        }

        private void HandlePrimaryRefresh()
        {
            if (this.PART_ClientContainer == null || this.PART_TitleBarBorder == null || m_owner == null)
            {
                return;
            }

            eWindowState currentWindowState = m_owner.GetWindowState();
            if (currentWindowState != m_cachedWindowState && currentWindowState != eWindowState.Unknown)
            {
                m_cachedWindowState = currentWindowState;
                switch (m_cachedWindowState)
                {
                    case eWindowState.Maximized:
                    case eWindowState.FullScreened:
                        this.PART_TitleBarBorder.BorderThickness = this.PART_ClientContainer.Margin = new Thickness(0);
                        break;

                    default:
                        this.PART_ClientContainer.Margin = new Thickness(this.TargetBorderThickness.Left, 0, this.TargetBorderThickness.Right, this.TargetBorderThickness.Bottom);
                        this.PART_TitleBarBorder.BorderThickness = new Thickness(this.TargetBorderThickness.Left, this.TargetBorderThickness.Top, this.TargetBorderThickness.Right, 0);
                        break;
                }
            }

            this.SetDragRegion();
        }
    }
}
