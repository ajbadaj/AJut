namespace AJut.UX.Controls
{
    using AJut.UX.Theming;
    using Microsoft.UI;
    using Microsoft.UI.Input;
    using Microsoft.UI.Windowing;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Markup;
    using Microsoft.UI.Xaml.Media;
    using System;
    using Windows.Foundation;
    using Windows.Graphics;
    using Windows.UI;
    using DPUtils = DPUtils<ThemedWindowRootControl>;

    public interface ITitleBarDragSizer
    {
        event EventHandler<EventArgs> ReadyToGenerateTitleBarDragRectangles;

        Windows.Graphics.RectInt32[] GenerateTitleBarDragRectangles();
        bool IsReadyToGenerateTitleBarDragRectangles() => true;
    }

    [ContentProperty(Name = nameof(WindowContents))]
    [TemplatePart(Name = nameof(PART_TitleBarBorder), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_ClientContainer), Type = typeof(ContentControl))]
    [TemplatePart(Name = nameof(PART_WindowRoot), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_EnterFullscreenButton), Type = typeof(TitleBarCaptionButton))]
    [TemplatePart(Name = nameof(PART_ExitFullscreenButton), Type = typeof(TitleBarCaptionButton))]
    [TemplatePart(Name = nameof(PART_CustomTitleBarContent), Type = typeof(ContentControl))]
    public partial class ThemedWindowRootControl : Control, ITitleBarDragSizer
    {
        private static readonly SolidColorBrush kTransparent = new(Colors.Transparent);
        private const int kBuiltInChromeButtonsWidth = 245;
        private const int kChromeButtonsAreaWidth = 218;

        private eWindowState m_cachedWindowState = eWindowState.Unknown;
        private Window m_owner;
        private bool m_hasTriggeredSetup = false;

        // ============================[ Construction / Setup / Teardown ]========================================
        public void SetupFor(Window owner)
        {
            m_owner = owner;
            m_owner.AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            m_owner.PerformPresenterTask((OverlappedPresenter p) =>
            {
                p.PreferredMinimumWidth = this.MinimumRestoredWindowWidth;
                p.PreferredMinimumHeight = this.MinimumRestoredWindowWidth;
            });


            m_owner.TrackActivation(isActivated: true);

            m_owner.Activated -= this.OwnerWindow_OnActivated;
            m_owner.Activated += this.OwnerWindow_OnActivated;
            m_owner.AppWindow.Changed -= this.OwnerWindow_OnAppWindowChanged;
            m_owner.AppWindow.Changed += this.OwnerWindow_OnAppWindowChanged;

            if (this.IsLoaded)
            {
                this.HandlePrimaryRefresh();
            }
        }

        public ThemedWindowRootControl ()
        {
            this.DefaultStyleKey = typeof(ThemedWindowRootControl);
            this.Loaded += this.OnLoaded;

            this.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(this.OnPointerMoved), true);
            this.AddHandler(UIElement.PointerExitedEvent, new PointerEventHandler(this.OnPointerExited), true);
            //this.PointerMoved += this.OnPointerMoved;
            //this.PointerExited += this.OnPointerExited;

            this.ActualThemeChanged += (s, a) => this.ApplyTitleBarTheming();
        }

        public void DisconnectFromOwnerWindow()
        {
            if (m_owner == null)
            {
                return;
            }

            m_owner.Activated -= this.OwnerWindow_OnActivated;
            m_owner.AppWindow.Changed -= this.OwnerWindow_OnAppWindowChanged;
            m_owner = null;
        }
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            this.PART_WindowRoot = (Border)this.GetTemplateChild(nameof(PART_WindowRoot));
            this.PART_TitleBarBorder = (Border)this.GetTemplateChild(nameof(PART_TitleBarBorder));
            this.PART_ClientContainer = (ContentControl)this.GetTemplateChild(nameof(PART_ClientContainer));
            this.PART_CustomTitleBarContent = (ContentControl)this.GetTemplateChild(nameof(PART_CustomTitleBarContent));

            if (this.PART_EnterFullscreenButton != null)
            {
                this.PART_EnterFullscreenButton.Click -= this.EnterFullScreen_OnClick;
            }

            this.PART_EnterFullscreenButton = (TitleBarCaptionButton)this.GetTemplateChild(nameof(PART_EnterFullscreenButton));
            this.PART_EnterFullscreenButton.Click += this.EnterFullScreen_OnClick;

            this.PART_ExitFullscreenButton = (TitleBarCaptionButton)this.GetTemplateChild(nameof(PART_ExitFullscreenButton));
            this.PART_ExitFullscreenButton.Click += this.ExitFullScreen_OnClick;
            this.PART_EnterFullscreenButton.SetupFor(m_owner);
            this.PART_ExitFullscreenButton.SetupFor(m_owner);
            if (m_owner != null)
            {
                this.HandlePrimaryRefresh();
            }
        }
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.PART_EnterFullscreenButton.SetupFor(m_owner);
            this.PART_ExitFullscreenButton.SetupFor(m_owner);
            this.HandlePrimaryRefresh();
        }

        // ============================[ Events ]========================================
        public event EventHandler<EventArgs> ReadyToGenerateTitleBarDragRectangles;
        public event EventHandler<EventArgs> IsSetupChanged;

        // ============================[ Properties ]========================================
        public Border PART_WindowRoot { get; private set; }
        public Border PART_TitleBarBorder { get; private set; }
        public ContentControl PART_ClientContainer { get; private set; }
        public TitleBarCaptionButton PART_EnterFullscreenButton { get; private set; }
        public TitleBarCaptionButton PART_ExitFullscreenButton { get; private set; }
        public ContentControl PART_CustomTitleBarContent { get; private set; }

        public bool IsSetup => m_owner != null;

        #region ===[ Dependency Properties ]===
        public static readonly DependencyProperty IsFullScreenedProperty = DPUtils.Register(_ => _.IsFullScreened);
        public bool IsFullScreened
        {
            get => (bool)this.GetValue(IsFullScreenedProperty);
            set => this.SetValue(IsFullScreenedProperty, value);
        }

        public Window Owner => m_owner;
        public AppWindow OwnerAppWindow => m_owner.AppWindow;


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

        public static readonly DependencyProperty TitleBarHeightProperty = DPUtils.Register(_ => _.TitleBarHeight);
        public double TitleBarHeight
        {
            get => (double)this.GetValue(TitleBarHeightProperty);
            set => this.SetValue(TitleBarHeightProperty, value);
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

        public static readonly DependencyProperty ActiveTitleBarBackgroundProperty = DPUtils.Register(_ => _.ActiveTitleBarBackground, CoerceUtils.CoerceBrushFrom("#202020"));
        public Brush ActiveTitleBarBackground
        {
            get => (Brush)this.GetValue(ActiveTitleBarBackgroundProperty);
            set => this.SetValue(ActiveTitleBarBackgroundProperty, value);
        }

        public static readonly DependencyProperty InactiveTitlebarBackgroundProperty = DPUtils.Register(_ => _.InactiveTitlebarBackground, CoerceUtils.CoerceBrushFrom("#2E2E2E"));
        public Brush InactiveTitlebarBackground
        {
            get => (Brush)this.GetValue(InactiveTitlebarBackgroundProperty);
            set => this.SetValue(InactiveTitlebarBackgroundProperty, value);
        }

        public static readonly DependencyProperty CloseHoverBackgroundProperty = DPUtils.Register(_ => _.CloseHoverBackground);
        public Brush CloseHoverBackground
        {
            get => (Brush)this.GetValue(CloseHoverBackgroundProperty);
            set => this.SetValue(CloseHoverBackgroundProperty, value);
        }

        public static readonly DependencyProperty TargetBorderThicknessProperty = DPUtils.Register(_ => _.TargetBorderThickness, new Thickness(2));
        public Thickness TargetBorderThickness
        {
            get => (Thickness)this.GetValue(TargetBorderThicknessProperty);
            set => this.SetValue(TargetBorderThicknessProperty, value);
        }

        public static readonly DependencyProperty ShowFullscreenButtonsProperty = DPUtils.Register(_ => _.ShowFullscreenButtons, true, (d, e) => d.ResetFullscreenButtons());
        public bool ShowFullscreenButtons
        {
            get => (bool)this.GetValue(ShowFullscreenButtonsProperty);
            set => this.SetValue(ShowFullscreenButtonsProperty, value);
        }

        public static readonly DependencyProperty TitleBarDragRectanglesCustomizerProperty = DPUtils.Register(_ => _.TitleBarDragRectanglesCustomizer);
        public ITitleBarDragSizer TitleBarDragRectanglesCustomizer
        {
            get => (ITitleBarDragSizer)this.GetValue(TitleBarDragRectanglesCustomizerProperty);
            set => this.SetValue(TitleBarDragRectanglesCustomizerProperty, value);
        }

        public static readonly DependencyProperty IsMaximizedOrFullScreenedProperty = DPUtils.Register(_ => _.IsMaximizedOrFullScreened);
        public bool IsMaximizedOrFullScreened
        {
            get => (bool)this.GetValue(IsMaximizedOrFullScreenedProperty);
            set => this.SetValue(IsMaximizedOrFullScreenedProperty, value);
        }

        public static readonly DependencyProperty LastStateProperty = DPUtils.Register(_ => _.LastState);
        public string LastState
        {
            get => (string)this.GetValue(LastStateProperty);
            set => this.SetValue(LastStateProperty, value);
        }


        public static readonly DependencyProperty IsHoveringOverCloseProperty = DPUtils.Register(_ => _.IsHoveringOverClose);
        public bool IsHoveringOverClose
        {
            get => (bool)this.GetValue(IsHoveringOverCloseProperty);
            set => this.SetValue(IsHoveringOverCloseProperty, value);
        }

        #endregion // === [ Dependency Properties ] ===

        // ============================[ Public Interface Methods ]================================

        public void ResetTitleBarDragRectangles()
        {
            if (m_owner?.AppWindow?.TitleBar == null)
            {
                return;
            }

            if (this.IsFullScreened)
            {
                //m_owner.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                //m_owner.ExtendsContentIntoTitleBar = true;
                //m_owner.SetTitleBar(new Grid());
                //m_owner.ExtendsContentIntoTitleBar = false;
                //m_owner.AppWindow.TitleBar.SetDragRectangles([new Windows.Graphics.RectInt32()]);
                _SetCaptionRectsTo(Array.Empty<RectInt32>());
                return;
            }

            ITitleBarDragSizer titleBarDragRectanglesCustomizer = this.TitleBarDragRectanglesCustomizer ?? this;
            if (titleBarDragRectanglesCustomizer.IsReadyToGenerateTitleBarDragRectangles())
            {
                if (titleBarDragRectanglesCustomizer.IsReadyToGenerateTitleBarDragRectangles())
                {
                    _HandleCustomTitleBarSetup();
                }
                else
                {
                    titleBarDragRectanglesCustomizer.ReadyToGenerateTitleBarDragRectangles -= _OnReadyToGenerate;
                    titleBarDragRectanglesCustomizer.ReadyToGenerateTitleBarDragRectangles += _OnReadyToGenerate;
                }
            }

            void _OnReadyToGenerate(object sender, EventArgs e)
            {
                titleBarDragRectanglesCustomizer.ReadyToGenerateTitleBarDragRectangles -= _OnReadyToGenerate;
                _HandleCustomTitleBarSetup();
            }

            void _HandleCustomTitleBarSetup()
            {
                // Set the drag rectangles
                //m_owner.SetTitleBar(this.PART_CustomTitleBarContent);
                m_owner.ExtendsContentIntoTitleBar = true;

                _SetCaptionRectsTo(titleBarDragRectanglesCustomizer.GenerateTitleBarDragRectangles());
            }

            void _SetCaptionRectsTo(RectInt32[] rects)
            {
                InputNonClientPointerSource ipc = InputNonClientPointerSource.GetForWindowId(m_owner.GetWindowId());
                if (ipc != null)
                {
                    ipc.SetRegionRects(NonClientRegionKind.Caption, rects);
                }
                else
                {
                    m_owner.AppWindow.TitleBar.SetDragRectangles(rects);
                }
            }
        }

        // ============================[ Private Methods ]================================

        private void HandlePrimaryRefresh()
        {
            if (this.PART_ClientContainer == null || this.PART_TitleBarBorder == null || m_owner == null)
            {
                return;
            }

            this.ResetTitleBarDragRectangles();
            this.ResetFullscreenButtons();
            this.ApplyTitleBarTheming();

            eWindowState currentWindowState = m_owner.GetWindowState();
            if (currentWindowState != m_cachedWindowState && currentWindowState != eWindowState.Unknown)
            {
                m_cachedWindowState = currentWindowState;
                switch (m_cachedWindowState)
                {
                    case eWindowState.Maximized:
                    case eWindowState.FullScreened:
                        this.PART_TitleBarBorder.BorderThickness = new Thickness(0);
                        break;

                    default:
                        this.PART_TitleBarBorder.BorderThickness = new Thickness(this.TargetBorderThickness.Left, this.TargetBorderThickness.Top, this.TargetBorderThickness.Right, this.TargetBorderThickness.Bottom);
                        break;
                }
            }

            if (!m_hasTriggeredSetup)
            {
                if (this.TitleBarDragRectanglesCustomizer == null)
                {
                    this.ReadyToGenerateTitleBarDragRectangles?.Invoke(this, EventArgs.Empty);
                }

                m_hasTriggeredSetup = true;
                this.IsSetupChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        bool ITitleBarDragSizer.IsReadyToGenerateTitleBarDragRectangles() => m_owner?.AppWindow?.TitleBar != null && this.PART_CustomTitleBarContent != null && this.IsLoaded;

        private void ApplyTitleBarTheming()
        {
            if (m_owner?.AppWindow?.Title == null || this.PART_WindowRoot == null)
            {
                return;
            }

            // Set the theme colors
            m_owner.AppWindow.TitleBar.ButtonBackgroundColor = _TryGetColor("WindowCaptionBackground");
            m_owner.AppWindow.TitleBar.ButtonInactiveBackgroundColor = _TryGetColor("WindowCaptionBackground");
            m_owner.AppWindow.TitleBar.ButtonHoverBackgroundColor = _TryGetColor("WindowCaptionButtonBackgroundPointerOver");
            m_owner.AppWindow.TitleBar.ButtonPressedBackgroundColor = _TryGetColor("WindowCaptionButtonBackgroundPressed");

            m_owner.AppWindow.TitleBar.ButtonForegroundColor = _TryGetColor("WindowCaptionForeground");
            m_owner.AppWindow.TitleBar.ButtonInactiveForegroundColor = _TryGetColor("TitleBarCaptionButtonInactiveForegroundColor");
            m_owner.AppWindow.TitleBar.ButtonHoverForegroundColor = _TryGetColor("WindowCaptionButtonStrokePointerOver");
            m_owner.AppWindow.TitleBar.ButtonPressedForegroundColor = _TryGetColor("WindowCaptionButtonStrokePressed");

            Color? _TryGetColor(string resourceName)
            {
                if (this.PART_WindowRoot.TryFindThemedResource(resourceName, out object result))
                {
                    if (result is Color color)
                    {
                        return color;
                    }

                    if (result is SolidColorBrush scb)
                    {
                        return scb.Color;
                    }
                }

                return null;
            }
        }

        RectInt32[] ITitleBarDragSizer.GenerateTitleBarDragRectangles()
        {
            double dpiScale = m_owner.DetermineActiveDPIScale();
            var transform = this.PART_CustomTitleBarContent.TransformToVisual(this);
            var toCustomTitleBarStart = transform.TransformPoint(new Point());

            return [
                new RectInt32(
                    (int)(dpiScale * toCustomTitleBarStart.X), 
                    (int)(dpiScale * toCustomTitleBarStart.Y),
                    (int)(dpiScale * (this.PART_CustomTitleBarContent.ActualWidth - kChromeButtonsAreaWidth)),
                    (int)(dpiScale * (this.PART_CustomTitleBarContent.ActualHeight))
                )
            ];
        }

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
                    this.IsMaximizedOrFullScreened = true;
                }
                else
                {
                    this.PART_EnterFullscreenButton.Visibility = Visibility.Visible;
                    this.PART_ExitFullscreenButton.Visibility = Visibility.Collapsed;
                    this.IsMaximizedOrFullScreened = m_owner.GetWindowState() == eWindowState.Maximized;
                }
            }
            else
            {
                this.PART_EnterFullscreenButton.Visibility = Visibility.Collapsed;
                this.PART_ExitFullscreenButton.Visibility = Visibility.Collapsed;
                this.IsMaximizedOrFullScreened = m_owner.GetWindowState() == eWindowState.Maximized;
            }
        }

        private void GoToState(string state, bool useTransitions = true, bool transitionButtonsToo = true)
        {
            if (state == this.LastState)
            {
                return;
            }

            this.LastState = state;
            VisualStateManager.GoToState(this, state, useTransitions);
        }

        // ============================[ Event Hanlders ]================================
        private void OwnerWindow_OnActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                this.GoToState("Normal", false);
            }
            else
            {
                this.GoToState("Inactive");
            }
        }

        private void EnterFullScreen_OnClick(object sender, RoutedEventArgs e)
        {
            m_owner?.EnterFullscreen();
        }

        private void ExitFullScreen_OnClick(object sender, RoutedEventArgs e)
        {
            m_owner?.ExitFullscreen(this.MinimumRestoredWindowWidth, this.MinimumRestoredWindowHeight);
        }

        private void OwnerWindow_OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPositionChange
                && !args.DidPresenterChange
                && !args.DidVisibilityChange
                && !args.DidZOrderChange
                && !args.IsZOrderAtBottom
                && !args.IsZOrderAtTop)
            {
                // Exclusively position changed
                return;
            }

            this.IsFullScreened = m_owner.IsFullScreened();

            if (args.DidPresenterChange)
            {
                this.ResetFullscreenButtons();

                this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    this.ResetTitleBarDragRectangles();
                });
            }
            else if (args.DidSizeChange)
            {
                this.ResetTitleBarDragRectangles();
            }
        }

        private void OnPointerMoved (object sender, PointerRoutedEventArgs e)
        {
            this.HandlePointerMoved(e.GetCurrentPoint(this).Position);
        }

        private void HandlePointerMoved(Point point)
        {
            if (this.PART_WindowRoot == null || m_owner == null)
            {
                this.IsHoveringOverClose = false;
                return;
            }

            if (m_owner.IsFullScreened())
            {
                this.IsHoveringOverClose = false;
                return;
            }

            if (point.Y < 32 && point.Y > this.TargetBorderThickness.Top && point.X < this.ActualWidth - this.TargetBorderThickness.Right)
            {
                if (point.X > this.ActualWidth - 46)
                {
                    this.IsHoveringOverClose = true;
                    this.GoToState("CloseHover", transitionButtonsToo: false);
                }
                else if (point.X > this.ActualWidth - kBuiltInChromeButtonsWidth
                            || this.PART_EnterFullscreenButton.IsPointerOver
                            || this.PART_ExitFullscreenButton.IsPointerOver)
                {
                    this.IsHoveringOverClose = false;
                    this.GoToState("ChromeButtonsHover", transitionButtonsToo: false);
                }
                else
                {
                    this.IsHoveringOverClose = false;
                    this.GoToState(m_owner.IsActivated() ? "Normal" : "Inactive");
                }
            }
            else
            {
                this.GoToState(m_owner.IsActivated() ? "Normal" : "Inactive");
                this.IsHoveringOverClose = false;
            }
        }

        private void OnPointerExited (object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.IsHoveringOverClose = false;
            this.GoToState(m_owner.IsActivated() ? "Normal" : "Inactive");
        }
    }
}
