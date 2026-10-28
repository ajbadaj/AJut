namespace AJut.UX.Tests
{
    using System;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Graphics;
    using Android.Graphics.Drawables;
    using Android.Views;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using AndroidColor = Android.Graphics.Color;

    [TestClass]
    public class ThemeColorsTests
    {
        private const int kWidth = 200;
        private const int kHeight = 100;

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
            Assert.IsTrue(AJutPalette.For(isDark).TryGetColor("AJut_Color_PrimaryHighlight", out ColorArgb highlight));
            Assert.AreEqual(highlight, ThemeColors.Resolve(TestHost.RequiredContext, kProbe), "The highlight border aliases the primary highlight");
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
        public void Themed_BrushButtonPaintsTheThemedButtonColors ()
        {
            ThemeColors.IsThemeLoaded = true;
            ThemeColors.IsTouchFocused = false;
            var button = new BrushButton(TestHost.RequiredContext);
            bool isDark = ThemeColors.IsDark(TestHost.RequiredContext);
            Assert.IsTrue(AJutPalette.For(isDark, isTouch: false).TryGetColor("ButtonBackground", out ColorArgb expected));

            var surface = (GradientDrawable)button.Background!;
            Assert.AreEqual(expected, new AndroidColor(surface.Color!.DefaultColor).ToColorArgb());
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
        public void Themed_TouchFocused_BrushButtonHasNoBorder ()
        {
            ThemeColors.IsThemeLoaded = true;
            ThemeColors.IsTouchFocused = true;
            var button = new BrushButton(TestHost.RequiredContext) { BorderThickness = 8 };
            ColorArgb fill = ThemeColors.Resolve(TestHost.RequiredContext, kButtonBackground);

            // The stroke is centered on a rectangle inset by half its thickness, so x = 6 is inside both the fill and
            // the border band: the fill shows there only if the border is clear.
            using Bitmap bitmap = _Render(button);
            Assert.AreEqual(fill, _Pixel(bitmap, 6, kHeight / 2), "The border band shows the fill");
        }

        [TestMethod]
        public void Themed_NotTouchFocused_BrushButtonKeepsItsBorder ()
        {
            ThemeColors.IsThemeLoaded = true;
            ThemeColors.IsTouchFocused = false;
            var button = new BrushButton(TestHost.RequiredContext) { BorderThickness = 8 };
            ColorArgb border = ThemeColors.Resolve(TestHost.RequiredContext, ThemedColor.FromArgb("ButtonBorderBrush", 0xFF010203, 0xFF040506));
            Assert.AreEqual(255, (int)border.A, "The desktop palette's button border is opaque, so the check below means something");

            using Bitmap bitmap = _Render(button);
            Assert.AreEqual(border, _Pixel(bitmap, 6, kHeight / 2), "The border band shows the border");
        }

        [TestMethod]
        public void Themed_TouchFocused_GeometryButtonKeepsItsTrace ()
        {
            ThemeColors.IsThemeLoaded = true;
            ThemeColors.IsTouchFocused = true;
            bool isDark = ThemeColors.IsDark(TestHost.RequiredContext);
            Assert.IsTrue(AJutPalette.For(isDark, isTouch: true).TryGetColor("AJut_Brush_BorderCommon", out ColorArgb border));
            Assert.AreEqual(255, (int)border.A, "The common border is opaque, so the check below means something");

            // Filled to the view, the shape is inset by half the trace, so the trace runs from x = 0 to 8 and x = 2 is
            // outside the fill: only the trace paints there.
            var button = new GeometryButton(TestHost.RequiredContext)
            {
                BackgroundGeometry = "M 0,0 H 100 V 50 H 0 Z",
                GeometryStretch = ePathStretch.Fill,
                TraceThickness = 8,
            };

            using Bitmap bitmap = _Render(button);
            Assert.AreEqual(border, _Pixel(bitmap, 2, kHeight / 2), "The trace band shows the border");
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

        [TestMethod]
        public void Themed_NumericEditorFieldPaintsTheTextControlForeground ()
        {
            ThemeColors.IsThemeLoaded = true;
            bool isDark = ThemeColors.IsDark(TestHost.RequiredContext);
            Assert.IsTrue(AJutPalette.For(isDark, ThemeColors.IsTouchFocused).TryGetColor("TextControlForeground", out ColorArgb expected));

            var editor = new NumericEditor(TestHost.RequiredContext);
            Assert.AreEqual(expected, new AndroidColor(editor.Field.CurrentTextColor).ToColorArgb());
        }

        [TestMethod]
        public void Themed_ReadOnlyNumericEditorFieldPaintsTheDisabledText ()
        {
            ThemeColors.IsThemeLoaded = true;
            bool isDark = ThemeColors.IsDark(TestHost.RequiredContext);
            Assert.IsTrue(AJutPalette.For(isDark, ThemeColors.IsTouchFocused).TryGetColor("TextControlForegroundDisabled", out ColorArgb expected));

            var editor = new NumericEditor(TestHost.RequiredContext) { IsReadOnly = true };
            Assert.AreEqual(expected, new AndroidColor(editor.Field.CurrentTextColor).ToColorArgb());
        }

        [TestMethod]
        public void Unthemed_NumericEditorFieldPaintsTheTextLiteral ()
        {
            ThemeColors.IsThemeLoaded = false;
            bool isDark = ThemeColors.IsDark(TestHost.RequiredContext);
            var editor = new NumericEditor(TestHost.RequiredContext);

            // The literal the step buttons' glyphs paint too: light E4000000, dark white
            ColorArgb expected = isDark ? new ColorArgb(0xFF, 0xFF, 0xFF, 0xFF) : new ColorArgb(0xE4, 0x00, 0x00, 0x00);
            Assert.AreEqual(expected, new AndroidColor(editor.Field.CurrentTextColor).ToColorArgb());
        }

        // ===========[ Helper Methods ]===================================
        private static Bitmap _Render (View view)
        {
            view.Measure(
                View.MeasureSpec.MakeMeasureSpec(kWidth, MeasureSpecMode.Exactly),
                View.MeasureSpec.MakeMeasureSpec(kHeight, MeasureSpecMode.Exactly)
            );
            view.Layout(0, 0, kWidth, kHeight);

            Bitmap bitmap = Bitmap.CreateBitmap(kWidth, kHeight, Bitmap.Config.Argb8888!)!;
            using (var canvas = new Canvas(bitmap))
            {
                view.Draw(canvas);
            }

            return bitmap;
        }

        private static ColorArgb _Pixel (Bitmap bitmap, int x, int y) => new AndroidColor(bitmap.GetPixel(x, y)).ToColorArgb();
    }
}
