namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using System;
    using DPUtils = AJut.UX.DPUtils<TitleBarCaptionButton>;

    public partial class TitleBarCaptionButton : Button
    {
        private Window m_currentWindow = null;

        // ======================[ Construction / Setup ]==================================
        public TitleBarCaptionButton()
        {
            this.DefaultStyleKey = typeof(TitleBarCaptionButton);
            this.PointerEntered += this.OnPointerEntered;
            this.PointerExited += this.OnPointerExited;
            this.ActualThemeChanged += this.OnThemeChanged;
        }
        public void SetupFor(Window window)
        {
            if (m_currentWindow != null)
            {
                m_currentWindow.Activated -= _OnOwnerActivated;
            }

            m_currentWindow = window;
            if (m_currentWindow != null)
            {
                m_currentWindow.Activated += _OnOwnerActivated;
                WindowXT.TrackActivation(m_currentWindow, true);
            }

            this.EvaluateState();

            void _OnOwnerActivated(object sender, WindowActivatedEventArgs args)
            {
                this.EvaluateState();
            }
        }

        // ======================[ Events ]==========================
        public event EventHandler<EventArgs<bool>> IsPointerInsideChanged;

        // ======================[ Properties ]==========================
        public static readonly DependencyProperty GlyphProperty = DPUtils.Register(_ => _.Glyph);
        public string Glyph
        {
            get => (string)this.GetValue(GlyphProperty);
            set => this.SetValue(GlyphProperty, value);
        }

        // ======================[ Public Interface Methods ]==========================
        public void EvaluateState()
        {
            if (m_currentWindow == null)
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

        // ======================[ Event Handlers ]==========================
        private void OnThemeChanged(FrameworkElement sender, object args)
        {
            this.EvaluateState();
        }

        private void OnPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.IsPointerInsideChanged?.Invoke(this, new EventArgs<bool>(true));
            this.EvaluateState();
        }

        private void OnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.IsPointerInsideChanged?.Invoke(this, new EventArgs<bool>(false));
            this.EvaluateState();
        }
    }
}
