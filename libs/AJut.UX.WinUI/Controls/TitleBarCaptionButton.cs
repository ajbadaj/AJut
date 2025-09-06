namespace AJut.UX.Controls
{
    using Microsoft.UI;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using System;
    using Windows.UI;
    using DPUtils = AJut.UX.DPUtils<TitleBarCaptionButton>;

    public partial class TitleBarCaptionButton : Button
    {
        public TitleBarCaptionButton()
        {
            this.DefaultStyleKey = typeof(TitleBarCaptionButton);
            this.Loaded += this.OnLoaded;
            this.PointerEntered += this.OnPointerEntered;
            this.PointerExited += this.OnPointerExited;
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
            if (m_currentWindow != null)
            {
                m_currentWindow.Activated -= _OnOwnerActivated;
                m_currentWindow = null;
            }

            if (this.XamlRoot.Content is  ThemedWindowRootControl windowRoot)
            {
                windowRoot.IsSetupChanged += _OnOwnerFirstSetup;
                if (windowRoot.IsSetup)
                {
                    SetWindow(windowRoot.Owner);
                }

                void _OnOwnerFirstSetup(object sender, EventArgs e)
                {
                    windowRoot.IsSetupChanged -= _OnOwnerFirstSetup;
                    SetWindow(windowRoot.Owner);
                }
            }

            if (Window.Current != null)
            {
                Window.Current.Activated += this.CurrentWindowOnActivated;
                this.EvaluteCurrentBrushes();
            }

            void SetWindow(Window newCurrentWindow)
            {
                m_currentWindow = newCurrentWindow;
                m_currentWindow.Activated += _OnOwnerActivated;
                WindowXT.TrackActivation(m_currentWindow, true);
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


        public static readonly DependencyProperty CurrentForegroundProperty = DPUtils.Register(_ => _.CurrentForeground);
        public Brush CurrentForeground
        {
            get => (Brush)this.GetValue(CurrentForegroundProperty);
            set => this.SetValue(CurrentForegroundProperty, value);
        }


        public static readonly DependencyProperty CurrentBackgroundProperty = DPUtils.Register(_ => _.CurrentBackground);
        public Brush CurrentBackground
        {
            get => (Brush)this.GetValue(CurrentBackgroundProperty);
            set => this.SetValue(CurrentBackgroundProperty, value);
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
                this.CurrentForeground = GetBrushOrFallback(m_currentWindow.AppWindow.TitleBar.ButtonPressedForegroundColor, "TitleBarCaptionButtonPressedForegroundColor");
                this.CurrentBackground = GetBrushOrFallback(m_currentWindow.AppWindow.TitleBar.ButtonPressedBackgroundColor, "WindowCaptionButtonBackground");
                return;
            }
            else if (this.IsPointerOver)
            {
                this.CurrentForeground = GetBrushOrFallback(m_currentWindow.AppWindow.TitleBar.ButtonHoverForegroundColor, "TitleBarCaptionButtonHoverForegroundColor");
                this.CurrentBackground = GetBrushOrFallback(m_currentWindow.AppWindow.TitleBar.ButtonHoverBackgroundColor, "WindowCaptionButtonBackground");
                return;
            }
            if (!m_currentWindow.IsActivated())
            {
                this.CurrentForeground = GetBrushOrFallback(m_currentWindow.AppWindow.TitleBar.ButtonInactiveForegroundColor, "TitleBarCaptionButtonInactiveForegroundColor");
                this.CurrentBackground = GetBrushOrFallback(m_currentWindow.AppWindow.TitleBar.ButtonInactiveBackgroundColor, "WindowCaptionButtonBackground");
                return;
            }

            this.CurrentForeground = GetBrushOrFallback(m_currentWindow.AppWindow.TitleBar.ButtonForegroundColor, "TitleBarCaptionButtonForegroundColor");
            this.CurrentBackground = GetBrushOrFallback(m_currentWindow.AppWindow.TitleBar.ButtonBackgroundColor, "WindowCaptionButtonBackground");

            Brush GetBrushOrFallback(Color? color, string fallbackResourceKey)
            {
                if (color.HasValue)
                {
                    return new SolidColorBrush(color.Value);
                }

                if (fallbackResourceKey != null && (Application.Current?.Resources?.TryGetValue(fallbackResourceKey, out object resourceValue) ?? false))
                {
                    if (resourceValue is Brush brush)
                    {
                        return brush;
                    }
                    if (resourceValue is Color c)
                    {
                        return new SolidColorBrush(c);
                    }
                }

                return g_transparent;
            }

            //< !--local:ThemedWindowRootControl.CaptionButtonNormalForegroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonForegroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}"
            //                            local: ThemedWindowRootControl.CaptionButtonHoverForegroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonHoverForegroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}"
            //                            local: ThemedWindowRootControl.CaptionButtonPressedForegroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonPressedForegroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}"
            //                            local: ThemedWindowRootControl.CaptionButtonInactiveForegroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonInactiveForegroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}"
            //                            local: ThemedWindowRootControl.CaptionButtonNormalBackgroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonBackgroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}"
            //                            local: ThemedWindowRootControl.CaptionButtonHoverBackgroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonHoverBackgroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}"
            //                            local: ThemedWindowRootControl.CaptionButtonPressedBackgroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonPressedBackgroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}"
            //                            local: ThemedWindowRootControl.CaptionButtonInactiveBackgroundColor = "{Binding OwnerAppWindow.TitleBar.ButtonInactiveBackgroundColor, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}" /> -->
        }
    }
}
