namespace AJut.UX.Tests
{
    using System;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Graphics.Drawables;
    using Android.Views;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using AndroidColor = Android.Graphics.Color;

    [TestClass]
    public class ThemeColorsTests
    {
        private static readonly ThemedColor kProbe = ThemedColor.FromArgb(AJutThemeKeys.ColorEditIngressControl_HighlightBorderBrush, 0xFF010203, 0xFF040506);
        private static readonly ThemedColor kButtonBackground = ThemedColor.FromArgb("ButtonBackground", 0xFF010203, 0xFF040506);

        // Read before any test here changes it, so the default can be checked and put back
        private static readonly bool kIsTouchFocusedAtStart = ThemeColors.IsTouchFocused;

        [TestCleanup]
        public void Teardown ()
        {
            ThemeColors.IsThemeLoaded = false;
            ThemeColors.IsTouchFocused = kIsTouchFocusedAtStart;
        }

        [TestMethod]
        public void Unthemed_PaintsTheLiteralForTheNightMode ()
        {
            ThemeColors.IsThemeLoaded = false;
            bool isDark = ThemeColors.IsDark(TestHost.RequiredContext);
            Assert.AreEqual(isDark ? kProbe.Dark : kProbe.Light, ThemeColors.Resolve(TestHost.RequiredContext, kProbe));
        }

        [TestMethod]
        public void Themed_PaintsThePaletteValue ()
        {
            ThemeColors.IsThemeLoaded = true;
            bool isDark = ThemeColors.IsDark(TestHost.RequiredContext);
            Assert.IsTrue(AJutPalette.For(isDark, ThemeColors.IsTouchFocused).TryGetColor(kProbe.Key, out ColorArgb themed), "The palette has a color for the probe's key, so the check below means something");
            Assert.AreEqual(themed, ThemeColors.Resolve(TestHost.RequiredContext, kProbe));
        }

        [TestMethod]
        public void Themed_KeyThePaletteLacksFallsBackToTheLiteral ()
        {
            ThemeColors.IsThemeLoaded = true;
            var unknown = ThemedColor.FromArgb("AJut_NoSuchControl_NoSuchBrush", 0xFF010203, 0xFF040506);
            bool isDark = ThemeColors.IsDark(TestHost.RequiredContext);
            Assert.AreEqual(isDark ? unknown.Dark : unknown.Light, ThemeColors.Resolve(TestHost.RequiredContext, unknown));
        }

        [TestMethod]
        public void TouchFocused_IsOnByDefault ()
        {
            Assert.IsTrue(kIsTouchFocusedAtStart, "An Android UI is touch first");
        }

        [TestMethod]
        public void Themed_TouchFocused_PaintsTheTouchPalette ()
        {
            ThemeColors.IsThemeLoaded = true;
            ThemeColors.IsTouchFocused = true;
            bool isDark = ThemeColors.IsDark(TestHost.RequiredContext);
            Assert.IsTrue(AJutPalette.For(isDark, isTouch: true).TryGetColor("ButtonBackground", out ColorArgb touch));
            Assert.IsTrue(AJutPalette.For(isDark, isTouch: false).TryGetColor("ButtonBackground", out ColorArgb desktop));
            Assert.AreNotEqual(desktop, touch, "The touch palette has its own button fill, so the check below means something");

            Assert.AreEqual(touch, ThemeColors.Resolve(TestHost.RequiredContext, kButtonBackground));
        }

        [TestMethod]
        public void Themed_NotTouchFocused_PaintsTheDesktopPalette ()
        {
            ThemeColors.IsThemeLoaded = true;
            ThemeColors.IsTouchFocused = false;
            bool isDark = ThemeColors.IsDark(TestHost.RequiredContext);
            Assert.IsTrue(AJutPalette.For(isDark, isTouch: false).TryGetColor("ButtonBackground", out ColorArgb desktop));
            Assert.AreEqual(desktop, ThemeColors.Resolve(TestHost.RequiredContext, kButtonBackground));
        }

        [TestMethod]
        public void Unthemed_TouchFocusedChangesNothing ()
        {
            ThemeColors.IsThemeLoaded = false;
            ThemeColors.IsTouchFocused = true;
            bool isDark = ThemeColors.IsDark(TestHost.RequiredContext);
            Assert.AreEqual(isDark ? kButtonBackground.Dark : kButtonBackground.Light, ThemeColors.Resolve(TestHost.RequiredContext, kButtonBackground));
        }

        [TestMethod]
        public void Themed_AutoBrushButtonWithNoAccent_DerivesFromTheThemesAccent ()
        {
            ThemeColors.IsThemeLoaded = true;
            bool isDark = ThemeColors.IsDark(TestHost.RequiredContext);
            Assert.IsTrue(AJutPalette.For(isDark, ThemeColors.IsTouchFocused).TryGetColor("SystemAccentColor", out ColorArgb accent));

            var button = new AutoBrushButton(TestHost.RequiredContext);
            Assert.AreEqual(accent, button.PropagatedAccentColor.ToColorArgb(), "The accent is the theme's");

            var surface = (GradientDrawable)button.Background!;
            Assert.AreEqual(InteractiveSurfaceColors.BuildFromAccent(accent).Normal.Background, new AndroidColor(surface.Color!.DefaultColor).ToColorArgb(), "And the surface is derived from it");
        }

        [TestMethod]
        public void Themed_AutoBrushButtonAccentFromItsLayout_IsKept ()
        {
            ThemeColors.IsThemeLoaded = true;
            View root = LayoutInflater.From(TestHost.RequiredContext)!.Inflate(Resource.Layout.brush_buttons_test, null)!;
            var button = root.FindViewById<AutoBrushButton>(Resource.Id.auto_brush_button)!;
            Assert.AreEqual(unchecked((int)0xFF7A1FA2), button.PropagatedAccentColor.ToArgb());
        }

        [TestMethod]
        public void Unthemed_AutoBrushButtonWithNoAccent_TakesTheSystemAccent ()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                Assert.Inconclusive("The system accent palette is Android 12 and later");
                return;
            }

            ThemeColors.IsThemeLoaded = false;
            Context context = TestHost.RequiredContext;
            int systemAccent = context.Resources!.GetColor(Android.Resource.Color.SystemAccent1500, context.Theme);
            var button = new AutoBrushButton(context);
            Assert.AreEqual(systemAccent, button.PropagatedAccentColor.ToArgb());
        }
    }
}
