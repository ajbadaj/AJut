// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AJut.UX.Theming.Controls
{
    using Microsoft.UI;
    using Microsoft.UI.Windowing;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Markup;
    using Microsoft.UI.Xaml.Media;
    using Windows.UI;
    using DPUtils = AJut.UX.DPUtils<ThemedWindowRoot>;

    /*
     * TODOs:
     * - Move over converters
     * - Inactive background color
     * 
     */

    [ContentProperty(Name = nameof(WindowContents))]
    public sealed partial class ThemedWindowRoot : UserControl
    {
        private eWindowState m_cachedWindowState = eWindowState.Unknown;
        private Window m_owner;
        private SolidColorBrush m_normalBorderHighlightBrush;
        private bool m_isHighlightingWindowChromeButtons;
        public ThemedWindowRoot ()
        {
            this.InitializeComponent();
        }

        public void SetupFor(Window owner)
        {
            m_owner = owner;
            m_owner.AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            m_owner.SetTitleBar(this.CustomTitleBarContainer);
            m_owner.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            m_owner.TrackActivation(isActivated: true);

            m_owner.SizeChanged -= this.OnSizeChanged;
            m_owner.SizeChanged += this.OnSizeChanged;
            m_owner.Activated -= this.OwnerWindow_OnActivated;
            m_owner.Activated += this.OwnerWindow_OnActivated;


            m_owner.AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            m_owner.AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            m_owner.AppWindow.TitleBar.ButtonHoverBackgroundColor = this.TitleBarButtonHighlightColor;
            //m_owner.AppWindow.TitleBar.ButtonHoverBackgroundColor = this.GetThemeResource<Color>("SystemAccentColor");

            // Other, keep gone
            //this.AppWindow.TitleBar.ButtonPressedBackgroundColor = Colors.Transparent;
            //this.AppWindow.TitleBar.ButtonForegroundColor = Colors.Transparent;
            //this.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Transparent;

            //this.SetWindowAsFrameless();
            this.Loaded += this.OnLoaded;
            if (this.IsLoaded)
            {
                this.HandlePrimaryRefresh();
            }

            this.PointerMoved += this.OnPointerMoved;
            this.PointerExited += this.OnPointerExited;
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

        #endregion // Dependency Properties

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


        private static readonly SolidColorBrush kTransparent = new SolidColorBrush(Colors.Transparent);
        private void HandlePointerNotInWindowChromeButtonRange()
        {
            this.WindowRoot.Background = kTransparent;
            m_isHighlightingWindowChromeButtons = false;
        }

        private void OnPointerMoved (object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Microsoft.UI.Input.PointerPoint point = e.GetCurrentPoint(this);
            if (point.Position.Y < 32)
            {
                if (point.Position.X > this.ActualWidth - 46)
                {
                    this.WindowRoot.Background = this.CloseButtonHighlightBrush;
                    m_isHighlightingWindowChromeButtons = true;
                    return;
                }
                else if (point.Position.X > this.ActualWidth - 138)
                {
                    this.WindowRoot.Background = m_owner.IsActivated() ? m_normalBorderHighlightBrush : this.InactiveTitlebarBackground;
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
            var rect = new Windows.Graphics.RectInt32(
                0,
                0,
                (int)this.CustomTitleBarContainer.ActualWidth,
                (int)this.CustomTitleBarContainer.ActualHeight
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
            eWindowState currentWindowState = m_owner.GetWindowState();
            if (currentWindowState != m_cachedWindowState && currentWindowState != eWindowState.Unknown)
            {
                m_cachedWindowState = currentWindowState;
                switch (m_cachedWindowState)
                {
                    case eWindowState.Maximized:
                    case eWindowState.FullScreened:
                        this.TitleBarBorder.BorderThickness = this.ClientContainer.Margin = new Thickness(0);
                        break;

                    default:
                        this.ClientContainer.Margin = new Thickness(this.TargetBorderThickness.Left, 0, this.TargetBorderThickness.Right, this.TargetBorderThickness.Bottom);
                        this.TitleBarBorder.BorderThickness = new Thickness(this.TargetBorderThickness.Left, this.TargetBorderThickness.Top, this.TargetBorderThickness.Right, 0);
                        break;
                }
            }

            this.SetDragRegion();
        }
    }
}
