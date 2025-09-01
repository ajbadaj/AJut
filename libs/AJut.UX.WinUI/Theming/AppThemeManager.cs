namespace AJut.UX.Theming
{
    using System;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml;
    using Windows.UI.ViewManagement;

    public class AppThemeManager : NotifyPropertyChanged, IDisposable
    {
        private Application m_targetApplication;
        private ElementTheme m_themeConfiguration = ElementTheme.Default;
        private ApplicationTheme m_currentTheme = ApplicationTheme.Dark;
        private UISettings m_uiSettings;
        private WindowManager m_windows;

        public event EventHandler<EventArgs<ApplicationTheme>> ThemeChanged;

        public void Setup(Application target, WindowManager windows, string lightThemeXamlUriPath = null, string darkThemeXamlUriPath = null)
        {
            m_windows = windows;

            m_targetApplication = target ?? Application.Current;

            Application.Current.Resources.ThemeDictionaries["Light"] = new ResourceDictionary { Source = new Uri(lightThemeXamlUriPath ?? "ms-appx:///Themes/LightThemeColors.xaml") };
            Application.Current.Resources.ThemeDictionaries["Dark"] = new ResourceDictionary { Source = new Uri(darkThemeXamlUriPath ?? "ms-appx:///Themes/DarkThemeColors.xaml") };


            // Insert the initial theme
            SetTheme(m_targetApplication.RequestedTheme);

            m_uiSettings = new UISettings();
            m_uiSettings.ColorValuesChanged += OnColorValuesChanged;
        }

        public void Dispose ()
        {
            if (m_uiSettings != null)
            {
                m_uiSettings.ColorValuesChanged -= OnColorValuesChanged;
                m_uiSettings = null;
            }
        }

        public ElementTheme[] ThemeConfigurationOptions { get; } = Enum.GetValues<ElementTheme>();

        public ElementTheme ThemeConfiguration
        {
            get => m_themeConfiguration;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_themeConfiguration, value))
                {
                    if (value == ElementTheme.Default)
                    {
                        SetTheme(GetSystemTheme());
                    }
                    else
                    {
                        SetTheme(value == ElementTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light);
                    }
                }
            }
        }

        public ApplicationTheme CurrentTheme => m_currentTheme;

        private void SetTheme (ApplicationTheme theme)
        {
            if (m_currentTheme == theme)
            {
                return;
            }

            // Update the WinUI defaults for each content root
            foreach (Window window in m_windows)
            {
                if (window.Content is FrameworkElement fe)
                {
                    fe.RequestedTheme = theme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                }
            }

            m_currentTheme = theme;
            this.ThemeChanged?.Invoke(this, theme);
        }

        private void OnColorValuesChanged (UISettings sender, object args)
        {
            if (this.ThemeConfiguration == ElementTheme.Default)
            {
                // UI thread dispatch required
                DispatcherQueue.GetForCurrentThread()?.TryEnqueue(()=>
                {
                    this.SetTheme(m_targetApplication.RequestedTheme);
                });
            }
        }

        private static ApplicationTheme GetSystemTheme ()
        {
            var uiSettings = new UISettings();
            var background = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
            // Heuristic: dark background means dark theme
            return background.R < 128 && background.G < 128 && background.B < 128
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;
        }
    }
}
