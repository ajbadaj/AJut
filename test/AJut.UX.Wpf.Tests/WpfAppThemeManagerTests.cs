namespace AJut.UX.Tests
{
    using System;
    using System.Windows;
    using System.Windows.Media;
    using AJut.UX.Theming;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The WPF AppThemeManager swaps the app's theme colors dictionary in and out at the index it was given, and lays the
    /// touch overlay just after it when touch focused. These run on
    /// the shared STA thread against the one Application a process can have, and put the app's resources back as they
    /// found them, so no other test sees a theme.
    /// </summary>
    [TestClass]
    public class WpfAppThemeManagerTests
    {
        private const string kLightColors = "/AJut.UX.Wpf;component/Resources/LightThemeColorsBase.xaml";
        private const string kDarkColors = "/AJut.UX.Wpf;component/Resources/DarkThemeColorsBase.xaml";

        [TestMethod]
        public void WpfAppThemeManager_ThemeSwitch_KeepsTheColorsAtTheirInsertionIndex ()
        {
            StaTestThread.Run(() =>
            {
                Application app = Application.Current ?? new Application();
                var appOwnResources = new ResourceDictionary();
                app.Resources.MergedDictionaries.Clear();
                app.Resources.MergedDictionaries.Add(appOwnResources);
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(kDarkColors, UriKind.Relative) });
                try
                {
                    using var themes = new AppThemeManager(app, kLightColors, kDarkColors, themeColorsInsertionIndex: 1);

                    themes.ThemeConfiguration = eAppThemeConfiguration.Light;
                    themes.ThemeConfiguration = eAppThemeConfiguration.Dark;

                    Assert.AreEqual(2, app.Resources.MergedDictionaries.Count, "One theme colors dictionary is swapped for another, nothing more");
                    Assert.AreSame(appOwnResources, app.Resources.MergedDictionaries[0], "The dictionary below the insertion index is left where it was");
                    Assert.AreEqual(eAppTheme.Dark, themes.CurrentTheme);
                    Assert.IsTrue(app.Resources.MergedDictionaries[1].Contains("AJut_Brush_PanelBackground"), "The theme colors sit at the insertion index");
                }
                finally
                {
                    app.Resources.MergedDictionaries.Clear();
                }
            });
        }

        [TestMethod]
        public void WpfAppThemeManager_IsTouchFocused_LaysTheTouchOverlayOverEverythingLast ()
        {
            StaTestThread.Run(() =>
            {
                Application app = Application.Current ?? new Application();
                var appResourcesBefore = new ResourceDictionary();
                var appResourcesAfter = new ResourceDictionary();
                app.Resources.MergedDictionaries.Clear();
                app.Resources.MergedDictionaries.Add(appResourcesBefore);
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(kDarkColors, UriKind.Relative) });
                app.Resources.MergedDictionaries.Add(appResourcesAfter);
                try
                {
                    using var themes = new AppThemeManager(app, kLightColors, kDarkColors, themeColorsInsertionIndex: 1);
                    Assert.IsFalse(themes.IsTouchFocused, "Off by default");
                    Assert.AreEqual(3, app.Resources.MergedDictionaries.Count, "Nothing is laid over the colors while it's off");
                    Assert.AreEqual(byte.MaxValue, BrushColor(app, "AJut_Brush_ButtonBorder").A, "Buttons have borders while it's off");

                    themes.IsTouchFocused = true;

                    Assert.AreEqual(4, app.Resources.MergedDictionaries.Count, "The touch overlay is added");
                    Assert.AreSame(appResourcesBefore, app.Resources.MergedDictionaries[0], "The app's dictionaries keep their indexes");
                    Assert.AreSame(appResourcesAfter, app.Resources.MergedDictionaries[2], "The app's dictionaries keep their indexes");
                    Assert.IsTrue(app.Resources.MergedDictionaries[3].Contains("AJut_Brush_ButtonBorder"), "The overlay is the last merged dictionary");
                    Assert.AreEqual(0, BrushColor(app, "AJut_Brush_ButtonBorder").A, "Button borders are off on touch");
                    Assert.AreEqual(Color.FromRgb(0x2E, 0x41, 0x57), BrushColor(app, "AJut_Brush_ControlBackground"), "Button surfaces take the dark touch color");
                    Assert.IsTrue(app.Resources.Contains("AJut_Brush_PanelBackground"), "Every key the overlay leaves alone still comes from the theme colors");

                    themes.IsTouchFocused = false;

                    Assert.AreEqual(3, app.Resources.MergedDictionaries.Count, "The overlay is taken off again");
                    Assert.AreSame(appResourcesAfter, app.Resources.MergedDictionaries[2]);
                    Assert.AreEqual(byte.MaxValue, BrushColor(app, "AJut_Brush_ButtonBorder").A, "Buttons have their borders back");
                }
                finally
                {
                    app.Resources.MergedDictionaries.Clear();
                }
            });
        }

        [TestMethod]
        public void WpfAppThemeManager_ThemeSwitchWhileTouchFocused_SwapsTheOverlayToo ()
        {
            StaTestThread.Run(() =>
            {
                Application app = Application.Current ?? new Application();
                var appResourcesBefore = new ResourceDictionary();
                var appResourcesAfter = new ResourceDictionary();
                app.Resources.MergedDictionaries.Clear();
                app.Resources.MergedDictionaries.Add(appResourcesBefore);
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(kDarkColors, UriKind.Relative) });
                app.Resources.MergedDictionaries.Add(appResourcesAfter);
                try
                {
                    using var themes = new AppThemeManager(app, kLightColors, kDarkColors, themeColorsInsertionIndex: 1);
                    themes.IsTouchFocused = true;

                    themes.ThemeConfiguration = eAppThemeConfiguration.Light;

                    Assert.AreEqual(4, app.Resources.MergedDictionaries.Count, "One set of theme colors and one overlay");
                    Assert.AreSame(appResourcesBefore, app.Resources.MergedDictionaries[0]);
                    Assert.AreSame(appResourcesAfter, app.Resources.MergedDictionaries[2]);
                    Assert.IsTrue(app.Resources.MergedDictionaries[3].Contains("AJut_Brush_ButtonBorder"), "The overlay is still the last merged dictionary");
                    Assert.IsFalse(app.Resources.MergedDictionaries[3].Contains("AJut_Brush_NormalText"), "It is the light overlay, which leaves text alone");
                    Assert.AreEqual(Color.FromRgb(0xCF, 0xE0, 0xF2), BrushColor(app, "AJut_Brush_ControlBackground"), "Button surfaces take the light touch color");
                    Assert.AreEqual(0, BrushColor(app, "AJut_Brush_ButtonBorder").A, "Button borders stay off");
                }
                finally
                {
                    app.Resources.MergedDictionaries.Clear();
                }
            });
        }

        private static Color BrushColor (Application app, string key) => ((SolidColorBrush)app.Resources[key]).Color;
    }
}
