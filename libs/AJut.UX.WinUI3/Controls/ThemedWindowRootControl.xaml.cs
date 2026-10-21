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

    // ===========[ ThemedWindowRootControl ]=======================================
    // WinUI3-specific: no WPF equivalent.
    // Custom-chrome window root: replaces the system title bar, hosts caption buttons
    // (fullscreen toggle), tracks activation/presenter state, and manages
    // InputNonClientPointerSource drag rectangles for the custom title bar area.
    //
    // The chrome state machine is LastState (Normal, Inactive, ChromeButtonsHover, CloseHover).
    // The template carries no visual states for it: every state-dependent brush is pushed into
    // a template part from code when the state or one of the brush properties changes. A
    // consumer that wants its own reaction binds to LastState or IsHoveringOverClose; the
    // window border, the title bar background and the caption button highlights are handled here.
    [ContentProperty(Name = nameof(WindowContents))]
    [TemplatePart(Name = nameof(PART_TitleBarBorder), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_TitleBarBackground), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_ClientContainer), Type = typeof(ContentControl))]
    [TemplatePart(Name = nameof(PART_WindowRoot), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_EnterFullscreenButton), Type = typeof(TitleBarCaptionButton))]
    [TemplatePart(Name = nameof(PART_ExitFullscreenButton), Type = typeof(TitleBarCaptionButton))]
    [TemplatePart(Name = nameof(PART_CustomTitleBarContent), Type = typeof(ContentControl))]
    [TemplatePart(Name = nameof(PART_MinimizeButtonHighlight), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_MaximizeButtonHighlight), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_CloseButtonHighlight), Type = typeof(Border))]
    public partial class ThemedWindowRootControl : Control, ITitleBarDragSizer
    {
        public const string kStateNormal = "Normal";
        public const string kStateInactive = "Inactive";
        public const string kStateChromeButtonsHover = "ChromeButtonsHover";
        public const string kStateCloseHover = "CloseHover";

        private const int kBuiltInChromeButtonsWidth = 245;
        private const int kChromeButtonsAreaWidth = 218;

        // The system draws its caption strip at a fixed size whatever TitleBarHeight is: 32 tall, and 46 wide per
        // button. These are the fallbacks for when the AppWindow has not reported the real inset yet.
        private const double kFallbackCaptionButtonWidth = 46;
        private const double kFallbackCaptionButtonHeight = 32;
        private const int kCaptionButtonCount = 3;

        private eWindowState m_cachedWindowState = eWindowState.Unknown;
        private Window m_owner;
        private AppWindow m_ownerAppWindow;
        private InputNonClientPointerSource m_nonClientPointerSource;
        private bool m_hasTriggeredSetup = false;
        private NonClientRegionKind? m_hoveredCaptionRegion;
        private NonClientRegionKind? m_pressedCaptionRegion;

        // ============================[ Construction / Setup / Teardown ]========================================
        public void SetupFor(Window owner)
        {
            m_owner = owner;
            m_ownerAppWindow = owner.AppWindow;
            m_owner.AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            m_owner.PerformPresenterTask((OverlappedPresenter p) =>
            {
                p.PreferredMinimumWidth = this.MinimumRestoredWindowWidth;
                p.PreferredMinimumHeight = this.MinimumRestoredWindowWidth;
            });


            m_owner.TrackActivation(isActivated: true);

            m_owner.Activated -= this.OwnerWindow_OnActivated;
            m_owner.Activated += this.OwnerWindow_OnActivated;
            m_ownerAppWindow.Changed -= this.OwnerWindow_OnAppWindowChanged;
            m_ownerAppWindow.Changed += this.OwnerWindow_OnAppWindowChanged;

            // Self-cleaning teardown. Without it a consumer who never calls
            // DisconnectFromOwnerWindow leaves the window pinned by its own chrome, since the
            // window's Activated list holds this control and this control holds the window right
            // back. Closed only fires for a close that actually went through, so a cancelled
            // close keeps its wiring intact.
            m_owner.Closed -= this.OwnerWindow_OnClosed;
            m_owner.Closed += this.OwnerWindow_OnClosed;

            this.SubscribeToNonClientPointer();

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

            this.ActualThemeChanged += this.OnActualThemeChanged;

            // BorderBrush is the consumer's normal border, and the chrome swaps it for the inactive and close-hover
            // brushes, so a change to it has to flow through the same refresh. This is the control watching its own
            // property, so there is nothing to pin and nothing to unregister.
            this.RegisterPropertyChangedCallback(Control.BorderBrushProperty, this.OnBorderBrushChanged);
        }

        public void DisconnectFromOwnerWindow()
        {
            // Ahead of the m_owner null check on purpose - the caption buttons hold their own
            // subscription to the same window and have to be let go even when this control has
            // already been disconnected once.
            this.PART_EnterFullscreenButton?.DisconnectFromOwnerWindow();
            this.PART_ExitFullscreenButton?.DisconnectFromOwnerWindow();

            this.UnsubscribeFromNonClientPointer();

            if (m_owner == null)
            {
                return;
            }

            m_owner.Activated -= this.OwnerWindow_OnActivated;
            m_owner.Closed -= this.OwnerWindow_OnClosed;

            // Off the cached AppWindow rather than m_owner.AppWindow - this also runs from the
            // Closed handler, and re-asking a closing window for its AppWindow is a native call
            // that has no reason to still be safe by then.
            if (m_ownerAppWindow != null)
            {
                m_ownerAppWindow.Changed -= this.OwnerWindow_OnAppWindowChanged;
                m_ownerAppWindow = null;
            }

            m_owner = null;
        }
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            this.PART_WindowRoot = (Border)this.GetTemplateChild(nameof(PART_WindowRoot));
            this.PART_TitleBarBorder = (Border)this.GetTemplateChild(nameof(PART_TitleBarBorder));
            this.PART_TitleBarBackground = (Border)this.GetTemplateChild(nameof(PART_TitleBarBackground));
            this.PART_ClientContainer = (ContentControl)this.GetTemplateChild(nameof(PART_ClientContainer));
            this.PART_CustomTitleBarContent = (ContentControl)this.GetTemplateChild(nameof(PART_CustomTitleBarContent));
            this.PART_MinimizeButtonHighlight = (Border)this.GetTemplateChild(nameof(PART_MinimizeButtonHighlight));
            this.PART_MaximizeButtonHighlight = (Border)this.GetTemplateChild(nameof(PART_MaximizeButtonHighlight));
            this.PART_CloseButtonHighlight = (Border)this.GetTemplateChild(nameof(PART_CloseButtonHighlight));

            if (this.PART_EnterFullscreenButton != null)
            {
                this.PART_EnterFullscreenButton.Click -= this.EnterFullScreen_OnClick;
            }

            if (this.PART_ExitFullscreenButton != null)
            {
                this.PART_ExitFullscreenButton.Click -= this.ExitFullScreen_OnClick;
            }

            this.PART_EnterFullscreenButton = (TitleBarCaptionButton)this.GetTemplateChild(nameof(PART_EnterFullscreenButton));
            this.PART_EnterFullscreenButton.Click += this.EnterFullScreen_OnClick;

            this.PART_ExitFullscreenButton = (TitleBarCaptionButton)this.GetTemplateChild(nameof(PART_ExitFullscreenButton));
            this.PART_ExitFullscreenButton.Click += this.ExitFullScreen_OnClick;
            this.PART_EnterFullscreenButton.SetupFor(m_owner);
            this.PART_ExitFullscreenButton.SetupFor(m_owner);

            if (string.IsNullOrEmpty(this.LastState))
            {
                this.LastState = kStateNormal;
            }

            this.RefreshChromeBrushes();
            this.RefreshCaptionButtonHighlights();

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
        public Border PART_TitleBarBackground { get; private set; }
        public ContentControl PART_ClientContainer { get; private set; }
        public TitleBarCaptionButton PART_EnterFullscreenButton { get; private set; }
        public TitleBarCaptionButton PART_ExitFullscreenButton { get; private set; }
        public ContentControl PART_CustomTitleBarContent { get; private set; }
        public Border PART_MinimizeButtonHighlight { get; private set; }
        public Border PART_MaximizeButtonHighlight { get; private set; }
        public Border PART_CloseButtonHighlight { get; private set; }

        public bool IsSetup => m_owner != null;

        /// <summary>
        /// Whether the window's non-client pointer source has reported pointer input over the system's caption
        /// buttons at least once. Close hover over the native close button depends on those events arriving, so a
        /// consumer or a probe can read this to tell whether that path is live on the machine it runs on.
        /// </summary>
        public bool HasSeenNonClientCaptionInput { get; private set; }

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

        /// <summary>
        /// The title bar background while the window is active. Drawn under the custom title bar content and under
        /// the system's caption buttons.
        /// </summary>
        public static readonly DependencyProperty ActiveTitleBarBackgroundProperty = DPUtils.Register(_ => _.ActiveTitleBarBackground, CoerceUtils.CoerceBrushFrom("#202020"), (d, e) => d.RefreshChromeBrushes());
        public Brush ActiveTitleBarBackground
        {
            get => (Brush)this.GetValue(ActiveTitleBarBackgroundProperty);
            set => this.SetValue(ActiveTitleBarBackgroundProperty, value);
        }

        /// <summary>
        /// The title bar background while the window is inactive. Null falls back to <see cref="ActiveTitleBarBackground"/>.
        /// </summary>
        public static readonly DependencyProperty InactiveTitlebarBackgroundProperty = DPUtils.Register(_ => _.InactiveTitlebarBackground, CoerceUtils.CoerceBrushFrom("#2E2E2E"), (d, e) => d.RefreshChromeBrushes());
        public Brush InactiveTitlebarBackground
        {
            get => (Brush)this.GetValue(InactiveTitlebarBackgroundProperty);
            set => this.SetValue(InactiveTitlebarBackgroundProperty, value);
        }

        /// <summary>
        /// The title bar background while the pointer is over the close button. Null, the default, keeps
        /// <see cref="ActiveTitleBarBackground"/>, so only the border reacts; set it when the whole title bar should.
        /// </summary>
        public static readonly DependencyProperty CloseHoverBackgroundProperty = DPUtils.Register(_ => _.CloseHoverBackground, (d, e) => d.RefreshChromeBrushes());
        public Brush CloseHoverBackground
        {
            get => (Brush)this.GetValue(CloseHoverBackgroundProperty);
            set => this.SetValue(CloseHoverBackgroundProperty, value);
        }

        /// <summary>
        /// The window border while the window is inactive. Null falls back to BorderBrush.
        /// </summary>
        public static readonly DependencyProperty InactiveBorderBrushProperty = DPUtils.Register(_ => _.InactiveBorderBrush, (d, e) => d.RefreshChromeBrushes());
        public Brush InactiveBorderBrush
        {
            get => (Brush)this.GetValue(InactiveBorderBrushProperty);
            set => this.SetValue(InactiveBorderBrushProperty, value);
        }

        /// <summary>
        /// The window border while the pointer is over the close button: the red edge that says a click here closes
        /// the window. Null falls back to BorderBrush.
        /// </summary>
        public static readonly DependencyProperty CloseHoverBorderBrushProperty = DPUtils.Register(_ => _.CloseHoverBorderBrush, (d, e) => d.RefreshChromeBrushes());
        public Brush CloseHoverBorderBrush
        {
            get => (Brush)this.GetValue(CloseHoverBorderBrushProperty);
            set => this.SetValue(CloseHoverBorderBrushProperty, value);
        }

        /// <summary>
        /// Drawn under the minimize or maximize button while the pointer is over it.
        /// </summary>
        public static readonly DependencyProperty CaptionButtonHoverBackgroundProperty = DPUtils.Register(_ => _.CaptionButtonHoverBackground, (d, e) => d.RefreshCaptionButtonHighlights());
        public Brush CaptionButtonHoverBackground
        {
            get => (Brush)this.GetValue(CaptionButtonHoverBackgroundProperty);
            set => this.SetValue(CaptionButtonHoverBackgroundProperty, value);
        }

        /// <summary>
        /// Drawn under the minimize or maximize button while it is pressed.
        /// </summary>
        public static readonly DependencyProperty CaptionButtonPressedBackgroundProperty = DPUtils.Register(_ => _.CaptionButtonPressedBackground, (d, e) => d.RefreshCaptionButtonHighlights());
        public Brush CaptionButtonPressedBackground
        {
            get => (Brush)this.GetValue(CaptionButtonPressedBackgroundProperty);
            set => this.SetValue(CaptionButtonPressedBackgroundProperty, value);
        }

        /// <summary>
        /// Drawn under the close button while the pointer is over it.
        /// </summary>
        public static readonly DependencyProperty CloseButtonHoverBackgroundProperty = DPUtils.Register(_ => _.CloseButtonHoverBackground, (d, e) => d.RefreshCaptionButtonHighlights());
        public Brush CloseButtonHoverBackground
        {
            get => (Brush)this.GetValue(CloseButtonHoverBackgroundProperty);
            set => this.SetValue(CloseButtonHoverBackgroundProperty, value);
        }

        /// <summary>
        /// Drawn under the close button while it is pressed.
        /// </summary>
        public static readonly DependencyProperty CloseButtonPressedBackgroundProperty = DPUtils.Register(_ => _.CloseButtonPressedBackground, (d, e) => d.RefreshCaptionButtonHighlights());
        public Brush CloseButtonPressedBackground
        {
            get => (Brush)this.GetValue(CloseButtonPressedBackgroundProperty);
            set => this.SetValue(CloseButtonPressedBackgroundProperty, value);
        }

        /// <summary>
        /// True, the default, has this control draw the caption button hover and pressed backgrounds itself, inside
        /// the window border, and tells the system to draw its buttons with transparent backgrounds. False leaves
        /// the system drawing its own button backgrounds, which paint over whatever border sits under them.
        /// </summary>
        public static readonly DependencyProperty DrawCaptionButtonHighlightsProperty = DPUtils.Register(_ => _.DrawCaptionButtonHighlights, true, (d, e) => d.OnDrawCaptionButtonHighlightsChanged());
        public bool DrawCaptionButtonHighlights
        {
            get => (bool)this.GetValue(DrawCaptionButtonHighlightsProperty);
            set => this.SetValue(DrawCaptionButtonHighlightsProperty, value);
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

        /// <summary>
        /// The chrome state: Normal, Inactive, ChromeButtonsHover or CloseHover (the kState constants). Read it, or
        /// bind to it, for chrome of your own that follows the window; the control sets it.
        /// </summary>
        public static readonly DependencyProperty LastStateProperty = DPUtils.Register(_ => _.LastState, (d, e) => d.RefreshChromeBrushes());
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

            this.ApplyTitleBarTheming();
            this.RefreshWindowStateChrome();
            this.RefreshChromeBrushes();
            this.RefreshCaptionButtonHighlights();

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

        // Recomputes the state-dependent chrome: drag rectangles, fullscreen button visibility +
        // IsMaximizedOrFullScreened, and the title-bar border. Maximize and restore keep the
        // OverlappedPresenter kind (only its State flips), so DidPresenterChange does not reliably
        // fire for them - this is what keeps the chrome in sync across those transitions when it is
        // run from the size-change path.
        private void RefreshWindowStateChrome()
        {
            if (this.PART_TitleBarBorder == null || m_owner == null)
            {
                return;
            }

            this.ResetTitleBarDragRectangles();
            this.ResetFullscreenButtons();
            this.RefreshTitleBarBorderForWindowState();
        }

        private void RefreshTitleBarBorderForWindowState()
        {
            eWindowState currentWindowState = m_owner.GetWindowState();
            if (currentWindowState == m_cachedWindowState || currentWindowState == eWindowState.Unknown)
            {
                return;
            }

            m_cachedWindowState = currentWindowState;
            switch (m_cachedWindowState)
            {
                case eWindowState.Maximized:
                case eWindowState.FullScreened:
                    this.PART_TitleBarBorder.BorderThickness = new Thickness(0);
                    break;

                default:
                    this.PART_TitleBarBorder.BorderThickness = this.TargetBorderThickness;
                    break;
            }
        }
        bool ITitleBarDragSizer.IsReadyToGenerateTitleBarDragRectangles() => m_owner?.AppWindow?.TitleBar != null && this.PART_CustomTitleBarContent != null && this.IsLoaded;

        private void ApplyTitleBarTheming()
        {
            if (m_owner?.AppWindow?.Title == null || this.PART_WindowRoot == null)
            {
                return;
            }

            AppWindowTitleBar titleBar = m_owner.AppWindow.TitleBar;

            // The system draws the minimize, maximize and close buttons over the top right of the window, on top of
            // any XAML there, so an opaque button background cuts a notch out of the window border wherever a button
            // sits. With this control drawing the highlights itself the system's backgrounds go transparent in every
            // state, the border and title bar show through, and the hover and pressed feedback comes from the
            // highlight parts underneath, which stay inside the border. The glyph colors are still the system's job.
            if (this.DrawCaptionButtonHighlights)
            {
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonHoverBackgroundColor = Colors.Transparent;
                titleBar.ButtonPressedBackgroundColor = Colors.Transparent;
            }
            else
            {
                titleBar.ButtonBackgroundColor = _TryGetColor("WindowCaptionBackground");
                titleBar.ButtonInactiveBackgroundColor = _TryGetColor("WindowCaptionBackground");
                titleBar.ButtonHoverBackgroundColor = _TryGetColor("WindowCaptionButtonBackgroundPointerOver");
                titleBar.ButtonPressedBackgroundColor = _TryGetColor("WindowCaptionButtonBackgroundPressed");
            }

            titleBar.ButtonForegroundColor = _TryGetColor("WindowCaptionForeground");
            titleBar.ButtonInactiveForegroundColor = _TryGetColor("TitleBarCaptionButtonInactiveForegroundColor");
            titleBar.ButtonHoverForegroundColor = _TryGetColor("WindowCaptionButtonStrokePointerOver");
            titleBar.ButtonPressedForegroundColor = _TryGetColor("WindowCaptionButtonStrokePressed");

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

        private void GoToState(string state)
        {
            if (state == this.LastState)
            {
                return;
            }

            // The LastState change handler pushes the brushes for the new state into the template parts.
            this.LastState = state;
        }

        private string ActivationState () => m_owner.IsActivated() ? kStateNormal : kStateInactive;

        /// <summary>
        /// Pushes the border and title bar brushes for the current state into the template. The brushes themselves
        /// are dependency properties so that a style can set them; which one shows is decided here.
        /// </summary>
        private void RefreshChromeBrushes ()
        {
            if (this.PART_WindowRoot == null)
            {
                return;
            }

            Brush border = this.BorderBrush;
            Brush titleBar = this.ActiveTitleBarBackground;
            switch (this.LastState)
            {
                case kStateInactive:
                    border = this.InactiveBorderBrush ?? border;
                    titleBar = this.InactiveTitlebarBackground ?? titleBar;
                    break;

                case kStateCloseHover:
                    border = this.CloseHoverBorderBrush ?? border;
                    titleBar = this.CloseHoverBackground ?? titleBar;
                    break;
            }

            this.PART_WindowRoot.BorderBrush = border;
            if (this.PART_TitleBarBackground != null)
            {
                this.PART_TitleBarBackground.Background = titleBar;
            }
        }

        /// <summary>
        /// Sizes the three highlight parts to the system's caption buttons and paints the one whose button is hovered
        /// or pressed. The system reports the strip's total width and height in physical pixels through the AppWindow
        /// title bar, which is the only reliable way to match the buttons across DPI and title bar height options.
        /// </summary>
        private void RefreshCaptionButtonHighlights ()
        {
            if (this.PART_MinimizeButtonHighlight == null || this.PART_MaximizeButtonHighlight == null || this.PART_CloseButtonHighlight == null)
            {
                return;
            }

            double buttonWidth = kFallbackCaptionButtonWidth;
            double buttonHeight = kFallbackCaptionButtonHeight;
            if (m_owner != null && m_ownerAppWindow?.TitleBar != null)
            {
                double dpiScale = m_owner.DetermineActiveDPIScale();
                if (m_ownerAppWindow.TitleBar.RightInset > 0)
                {
                    buttonWidth = m_ownerAppWindow.TitleBar.RightInset / dpiScale / kCaptionButtonCount;
                }

                if (m_ownerAppWindow.TitleBar.Height > 0)
                {
                    buttonHeight = m_ownerAppWindow.TitleBar.Height / dpiScale;
                }
            }

            _Place(this.PART_CloseButtonHighlight, 0);
            _Place(this.PART_MaximizeButtonHighlight, 1);
            _Place(this.PART_MinimizeButtonHighlight, 2);

            bool draw = this.DrawCaptionButtonHighlights && !this.IsFullScreened;
            this.PART_CloseButtonHighlight.Background = draw ? _BrushFor(NonClientRegionKind.Close, this.CloseButtonHoverBackground, this.CloseButtonPressedBackground) : null;
            this.PART_MaximizeButtonHighlight.Background = draw ? _BrushFor(NonClientRegionKind.Maximize, this.CaptionButtonHoverBackground, this.CaptionButtonPressedBackground) : null;
            this.PART_MinimizeButtonHighlight.Background = draw ? _BrushFor(NonClientRegionKind.Minimize, this.CaptionButtonHoverBackground, this.CaptionButtonPressedBackground) : null;

            void _Place (Border highlight, int slotFromRight)
            {
                highlight.Width = buttonWidth;
                highlight.Height = buttonHeight;
                highlight.Margin = new Thickness(0, 0, buttonWidth * slotFromRight, 0);
            }

            Brush _BrushFor (NonClientRegionKind region, Brush hover, Brush pressed)
            {
                if (m_pressedCaptionRegion == region)
                {
                    return pressed ?? hover;
                }

                return m_hoveredCaptionRegion == region ? hover : null;
            }
        }

        private void OnDrawCaptionButtonHighlightsChanged ()
        {
            this.ApplyTitleBarTheming();
            this.RefreshCaptionButtonHighlights();
        }

        // The system owns the pointer while it is over its own caption buttons, so the XAML pointer events on this
        // control never fire there: the only way to see a hover over the native close button is the window's
        // non-client pointer source, which reports every non-client region the pointer crosses with its kind.
        private void SubscribeToNonClientPointer ()
        {
            this.UnsubscribeFromNonClientPointer();
            if (m_owner == null)
            {
                return;
            }

            m_nonClientPointerSource = InputNonClientPointerSource.GetForWindowId(m_owner.GetWindowId());
            if (m_nonClientPointerSource == null)
            {
                return;
            }

            m_nonClientPointerSource.PointerEntered += this.NonClientPointer_OnMoved;
            m_nonClientPointerSource.PointerMoved += this.NonClientPointer_OnMoved;
            m_nonClientPointerSource.PointerExited += this.NonClientPointer_OnExited;
            m_nonClientPointerSource.PointerPressed += this.NonClientPointer_OnPressed;
            m_nonClientPointerSource.PointerReleased += this.NonClientPointer_OnReleased;
        }

        private void UnsubscribeFromNonClientPointer ()
        {
            if (m_nonClientPointerSource == null)
            {
                return;
            }

            m_nonClientPointerSource.PointerEntered -= this.NonClientPointer_OnMoved;
            m_nonClientPointerSource.PointerMoved -= this.NonClientPointer_OnMoved;
            m_nonClientPointerSource.PointerExited -= this.NonClientPointer_OnExited;
            m_nonClientPointerSource.PointerPressed -= this.NonClientPointer_OnPressed;
            m_nonClientPointerSource.PointerReleased -= this.NonClientPointer_OnReleased;
            m_nonClientPointerSource = null;
        }

        private static bool IsCaptionButtonRegion (NonClientRegionKind region)
        {
            return region == NonClientRegionKind.Close
                || region == NonClientRegionKind.Maximize
                || region == NonClientRegionKind.Minimize;
        }

        private void ApplyCaptionRegion (NonClientRegionKind? hovered, NonClientRegionKind? pressed)
        {
            m_hoveredCaptionRegion = hovered;
            m_pressedCaptionRegion = pressed;

            if (m_owner == null)
            {
                this.IsHoveringOverClose = false;
                this.RefreshCaptionButtonHighlights();
                return;
            }

            if (hovered == NonClientRegionKind.Close)
            {
                this.IsHoveringOverClose = true;
                this.GoToState(kStateCloseHover);
            }
            else if (hovered.HasValue)
            {
                this.IsHoveringOverClose = false;
                this.GoToState(kStateChromeButtonsHover);
            }
            else
            {
                this.IsHoveringOverClose = false;
                this.GoToState(this.ActivationState());
            }

            this.RefreshCaptionButtonHighlights();
        }

        // ============================[ Event Handlers ]================================
        private void OwnerWindow_OnClosed(object sender, WindowEventArgs args)
        {
            this.DisconnectFromOwnerWindow();
        }

        private void OwnerWindow_OnActivated(object sender, WindowActivatedEventArgs args)
        {
            this.GoToState(args.WindowActivationState != WindowActivationState.Deactivated ? kStateNormal : kStateInactive);
        }

        private void OnActualThemeChanged (FrameworkElement sender, object args)
        {
            this.ApplyTitleBarTheming();
        }

        private void OnBorderBrushChanged (DependencyObject sender, DependencyProperty property)
        {
            this.RefreshChromeBrushes();
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
                && !args.DidSizeChange
                && !args.DidPresenterChange
                && !args.DidVisibilityChange
                && !args.DidZOrderChange
                && !args.IsZOrderAtBottom
                && !args.IsZOrderAtTop)
            {
                // Window was only dragged - nothing chrome-related to redo. The DidSizeChange
                // exclusion is load-bearing: a maximize/restore changes size AND position, so
                // without it this guard swallows those transitions as if they were a plain move.
                return;
            }

            this.IsFullScreened = m_owner.IsFullScreened();

            if (args.DidPresenterChange)
            {
                // Presenter kind flipped (fullscreen enter/exit). Recompute the chrome now, then
                // re-run the drag-rect pass once the new presenter's layout has settled.
                this.RefreshWindowStateChrome();
                this.RefreshCaptionButtonHighlights();

                this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, this.ResetTitleBarDragRectangles);
            }
            else if (args.DidSizeChange)
            {
                // Size changed without a presenter-kind change: a maximize or restore (the
                // OverlappedPresenter keeps its kind, only its State flips). The chrome bindings
                // still have to track it, so recompute the full state, not just the drag rectangles.
                this.RefreshWindowStateChrome();
                this.RefreshCaptionButtonHighlights();
            }
        }

        private void NonClientPointer_OnMoved (InputNonClientPointerSource sender, NonClientPointerEventArgs args)
        {
            if (IsCaptionButtonRegion(args.RegionKind))
            {
                this.HasSeenNonClientCaptionInput = true;
                this.ApplyCaptionRegion(args.RegionKind, m_pressedCaptionRegion == args.RegionKind ? m_pressedCaptionRegion : null);
            }
            else if (m_hoveredCaptionRegion != null)
            {
                // Off a caption button and onto the caption or a border: the button is no longer hovered. Over the
                // caption itself the XAML pointer events still arrive as well, and they are the ones that know about
                // this control's own fullscreen buttons, so the state is left to them from here; forcing Normal on
                // every non-client move made the two handlers flicker against each other.
                this.ApplyCaptionRegion(null, null);
            }
        }

        private void NonClientPointer_OnExited (InputNonClientPointerSource sender, NonClientPointerEventArgs args)
        {
            this.ApplyCaptionRegion(null, null);
        }

        private void NonClientPointer_OnPressed (InputNonClientPointerSource sender, NonClientPointerEventArgs args)
        {
            if (IsCaptionButtonRegion(args.RegionKind))
            {
                this.ApplyCaptionRegion(args.RegionKind, args.RegionKind);
            }
        }

        private void NonClientPointer_OnReleased (InputNonClientPointerSource sender, NonClientPointerEventArgs args)
        {
            this.ApplyCaptionRegion(IsCaptionButtonRegion(args.RegionKind) ? args.RegionKind : null, null);
        }

        private void OnPointerMoved (object sender, PointerRoutedEventArgs e)
        {
            this.HandlePointerMoved(e.GetCurrentPoint(this).Position);
        }

        // Client-area pointer tracking. The system's caption strip is a fixed 32 tall and the close button a fixed
        // 46 wide whatever TitleBarHeight is, because the system draws them, so those numbers stay as they are. Input
        // over the strip itself goes to the non-client handlers above; this covers the pointer's way in and out, and
        // this control's own fullscreen buttons.
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

            // While the pointer is over one of the system's caption buttons the non-client handlers own the state.
            // XAML still reports moves there, with a position that sits right at the window's edge, and letting this
            // path answer them flickered the border between the hover state and Normal on every move.
            if (m_hoveredCaptionRegion != null)
            {
                return;
            }

            if (point.Y < 32 && point.Y > this.TargetBorderThickness.Top && point.X < this.ActualWidth - this.TargetBorderThickness.Right)
            {
                if (point.X > this.ActualWidth - 46)
                {
                    this.IsHoveringOverClose = true;
                    this.GoToState(kStateCloseHover);
                }
                else if (point.X > this.ActualWidth - kBuiltInChromeButtonsWidth
                            || this.PART_EnterFullscreenButton.IsPointerOver
                            || this.PART_ExitFullscreenButton.IsPointerOver)
                {
                    this.IsHoveringOverClose = false;
                    this.GoToState(kStateChromeButtonsHover);
                }
                else
                {
                    this.IsHoveringOverClose = false;
                    this.GoToState(this.ActivationState());
                }
            }
            else
            {
                this.GoToState(this.ActivationState());
                this.IsHoveringOverClose = false;
            }
        }

        private void OnPointerExited (object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.IsHoveringOverClose = false;

            // Same null guard HandlePointerMoved above carries, and for the same reason: the pointer
            // leaves the window on its way out, so this handler runs after OwnerWindow_OnClosed has
            // already sent DisconnectFromOwnerWindow through and nulled the owner. There is no
            // activation state to reflect once the window is gone, so there is nothing to do here.
            if (m_owner == null)
            {
                return;
            }

            // Leaving the client area for the caption strip arrives as an exit here and an enter in the non-client
            // handlers, in that order, so this only steps back to the activation state when no caption button holds
            // the pointer.
            if (m_hoveredCaptionRegion == null)
            {
                this.GoToState(this.ActivationState());
            }
        }
    }
}
