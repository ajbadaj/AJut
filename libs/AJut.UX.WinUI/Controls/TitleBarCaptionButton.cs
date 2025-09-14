namespace AJut.UX.Controls
{
    using Microsoft.UI;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using System;
    using DPUtils = AJut.UX.DPUtils<TitleBarCaptionButton>;

    public partial class TitleBarCaptionButton : Button
    {
        public TitleBarCaptionButton()
        {
            this.DefaultStyleKey = typeof(TitleBarCaptionButton);
            this.Loaded += this.OnLoaded;
            this.PointerEntered += this.OnPointerEntered;
            this.PointerExited += this.OnPointerExited;
            this.ActualThemeChanged += this.OnThemeChanged;
        }

        private void OnThemeChanged(FrameworkElement sender, object args)
        {
            this.EvaluteCurrentBrushes();
        }

        public event EventHandler<EventArgs<bool>> IsPointerInsideChanged;
        private void OnPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.IsPointerInsideChanged?.Invoke(this, new EventArgs<bool>(true));
            this.EvaluteCurrentBrushes();
        }

        private void OnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.IsPointerInsideChanged?.Invoke(this, new EventArgs<bool>(false));
            this.EvaluteCurrentBrushes();
        }

        Window m_currentWindow = null;
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _SetWindow(null);

            var windowRoot = this.XamlRoot.Content.GetFirstChildOf<ThemedWindowRootControl>();
            if (windowRoot != null)
            {
                windowRoot.IsSetupChanged += _OnOwnerFirstSetup;
                if (windowRoot.IsSetup)
                {
                    _SetWindow(windowRoot.Owner);
                }

                void _OnOwnerFirstSetup(object sender, EventArgs e)
                {
                    windowRoot.IsSetupChanged -= _OnOwnerFirstSetup;
                    _SetWindow(windowRoot.Owner);
                }
            }

            if (Window.Current != null)
            {
                Window.Current.Activated += this.CurrentWindowOnActivated;
                this.EvaluteCurrentBrushes();
            }

            void _SetWindow(Window newCurrentWindow)
            {
                if (m_currentWindow != null)
                {
                    m_currentWindow.Activated -= _OnOwnerActivated;
                }

                m_currentWindow = newCurrentWindow;
                if (m_currentWindow != null)
                {
                    m_currentWindow.Activated += _OnOwnerActivated;
                    WindowXT.TrackActivation(m_currentWindow, true);
                }

                this.EvaluteCurrentBrushes();
            }

            void _OnOwnerActivated(object sender, WindowActivatedEventArgs args)
            {
                this.EvaluteCurrentBrushes();
            }
        }

        private void CurrentWindowOnActivated(object sender, WindowActivatedEventArgs args)
        {
            this.EvaluteCurrentBrushes();
        }

        public static readonly DependencyProperty GlyphProperty = DPUtils.Register(_ => _.Glyph);
        public string Glyph
        {
            get => (string)this.GetValue(GlyphProperty);
            set => this.SetValue(GlyphProperty, value);
        }

        public int OnWindowIsActivatedChanged { get; private set; }

        private static SolidColorBrush g_transparent = new SolidColorBrush(Colors.Transparent);

        private void EvaluteCurrentBrushes()
        {
            if (m_currentWindow == null || m_currentWindow.AppWindow == null || m_currentWindow.AppWindow.TitleBar == null)
            {
                return;
            }

            if (this.IsPressed)
            {
                VisualStateManager.GoToState(this, "Pressed", true);
                return;
            }
            else if (this.IsPointerOver)
            {
                VisualStateManager.GoToState(this, "PointerOver", true);
                return;
            }
            VisualStateManager.GoToState(this, m_currentWindow.IsActivated() ? "Normal" : "Inactive", true);
            


            // CaptionButtonNormalForegroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonForegroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}"
            // CaptionButtonHoverForegroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonHoverForegroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}"
            // CaptionButtonPressedForegroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonPressedForegroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}"
            // CaptionButtonInactiveForegroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonInactiveForegroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}"
            // CaptionButtonNormalBackgroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonBackgroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}"
            // CaptionButtonHoverBackgroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonHoverBackgroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}"
            // CaptionButtonPressedBackgroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonPressedBackgroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}"
            // CaptionButtonInactiveBackgroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonInactiveBackgroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}" /> -->
        }
    }
}
