// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AJutShowRoomWinUI
{
    using AJut;
    using AJut.UX;
    using AJut.UX.Theming;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Windows.Storage;
    using Windows.UI;
    using Windows.UI.Popups;


    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private const string kThemeColorsSettingsKey = "_Hidden_theme_colors";
        private readonly ObservableCollection<string> m_themeColorResolver = new ObservableCollection<string>();
        public MainWindow (WindowManager manager, AppThemeManager themeManager)
        {
            this.Application = App.Instance;
            manager.Setup(this);
            themeManager.ApplyTheme(this);
            this.AppWindow.SetIcon("Assets/app.ico");
            this.InitializeComponent();
            this.Root.SetupFor(this);

            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(800, 1000));

            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.TryGetValue(kThemeColorsSettingsKey, out object themeColorsObj) && themeColorsObj is string[] strs)
            {
                m_themeColorResolver.AddEach(strs);
            }
            else
            {
                m_themeColorResolver.Add("SystemAccentColor");
                m_themeColorResolver.Add("TabViewItemHeaderBackground");
                m_themeColorResolver.Add("TabViewItemHeaderBackgroundPointerOver");
                this.SaveThemeColors();
            }

            this.ThemeColorsList.ItemsSource = m_themeColorResolver;
        }


        public App Application { get; }

        private void AddThemeColor(string themeColor)
        {
            while (m_themeColorResolver.Contains(themeColor))
            {
                m_themeColorResolver.Remove(themeColor);
            }

            m_themeColorResolver.Insert(0, themeColor);
            this.ThemeColorsList.SelectedIndex = 0;
            while (m_themeColorResolver.Count > 10)
            {
                m_themeColorResolver.RemoveAt(10);
            }

            this.SaveThemeColors();
        }

        private void SaveThemeColors()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[kThemeColorsSettingsKey] = m_themeColorResolver.ToArray();
        }

        private async void ThemeResolver_OnClick(object sender, RoutedEventArgs e)
        {
            if (await _DebugWriteBrushInfo(this.SpecificThemeText.Text))
            {
                this.SpecificThemeText.Text = "";
            }

            async Task<bool> _DebugWriteBrushInfo(string resourceName)
            {
                string debugText;
                bool result;

                if (this.Root.TryFindThemedResource(resourceName, out object obj))
                {
                    result = true;
                    string value;
                    if (obj is SolidColorBrush scb)
                    {
                        value = ColorHelper.GetSmallestHexString(scb.Color);
                        this.ColorPicker.Color = scb.Color;
                    }
                    else if (obj is Color color)
                    {
                        value = ColorHelper.GetSmallestHexString(color);
                        this.ColorPicker.Color = color;
                    }
                    else
                    {
                        value = obj.ToString();
                    }
                    
                    debugText = $"Resolved resource '{resourceName}' of type '{obj.GetType().FullName}' - value: {value}";
                }
                else
                {
                    result = false;
                    debugText = $"FAILED to resolve '{resourceName}'";
                }

                Logger.LogInfo(debugText);
                if (result)
                {
                    this.AddThemeColor(resourceName);
                }

                ContentDialog myDialog = new ContentDialog
                {
                    Title = "Resource Search Results",
                    Content = debugText,
                    CloseButtonText = "OK"
                };

                myDialog.XamlRoot = this.SpecificThemeText.XamlRoot;
                await myDialog.ShowAsync();
                return result;
            }
        }

        private void ThemeColorsList_OnDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe && fe.DataContext is string context)
            {
                this.SpecificThemeText.Text = context;
            }
        }
    }
}
