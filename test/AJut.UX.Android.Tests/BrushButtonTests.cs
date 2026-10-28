namespace AJut.UX.Tests
{
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Android.Graphics.Drawables;
    using Android.Views;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using AndroidColor = Android.Graphics.Color;

    [TestClass]
    public class BrushButtonTests
    {
        [TestMethod]
        public void AutoBrushButton_PaintsTheDerivedPaletteForEachState ()
        {
            var accent = new AndroidColor(unchecked((int)0xFF2E7D32));
            var button = new AutoBrushButton(TestHost.RequiredContext) { PropagatedAccentColor = accent };
            InteractiveSurfaceColors expected = InteractiveSurfaceColors.BuildFromAccent(accent.ToColorArgb());
            _AssertPainted(button, expected.Normal, "Normal");

            button.Pressed = true;
            Assert.AreEqual(eManagedButtonState.Pressed, button.ActiveState);
            _AssertPainted(button, expected.Pressed, "Pressed");

            button.Pressed = false;
            button.Enabled = false;
            Assert.AreEqual(eManagedButtonState.Disabled, button.ActiveState);
            _AssertPainted(button, expected.Disabled, "Disabled");

            button.Enabled = true;
            Assert.AreEqual(eManagedButtonState.Normal, button.ActiveState);
            _AssertPainted(button, expected.Normal, "Normal again");
        }

        [TestMethod]
        public void AutoBrushButton_NewAccentRepaints ()
        {
            var button = new AutoBrushButton(TestHost.RequiredContext);
            var accent = new AndroidColor(unchecked((int)0xFFFFEB3B));
            button.PropagatedAccentColor = accent;
            _AssertPainted(button, InteractiveSurfaceColors.BuildFromAccent(accent.ToColorArgb()).Normal, "After the accent changed");
        }

        [TestMethod]
        public void BrushButton_UnsetSlotsPaintTheStandardButtonColors ()
        {
            var button = new BrushButton(TestHost.RequiredContext);
            bool isDark = ThemeColors.IsDark(TestHost.RequiredContext);
            ColorArgb background = _SurfaceColor(button);
            ColorArgb foreground = new AndroidColor(button.CurrentTextColor).ToColorArgb();

            // The unthemed literals: light F3F3F3 on 1A1A1A, dark 2D2D2D on white.
            Assert.AreEqual(isDark ? new ColorArgb(0xFF, 0x2D, 0x2D, 0x2D) : new ColorArgb(0xFF, 0xF3, 0xF3, 0xF3), background);
            Assert.AreEqual(isDark ? new ColorArgb(0xFF, 0xFF, 0xFF, 0xFF) : new ColorArgb(0xFF, 0x1A, 0x1A, 0x1A), foreground);
        }

        [TestMethod]
        public void BrushButton_SetSlotPaintsAndClearingItFallsBack ()
        {
            var button = new BrushButton(TestHost.RequiredContext);
            ColorArgb standard = _SurfaceColor(button);

            button.NormalBackground = new AndroidColor(unchecked((int)0xFF123456));
            Assert.AreEqual(new ColorArgb(0xFF, 0x12, 0x34, 0x56), _SurfaceColor(button));

            button.NormalBackground = null;
            Assert.AreEqual(standard, _SurfaceColor(button));
        }

        [TestMethod]
        public void ManagedBrushButton_KeepsTheButtonPadding ()
        {
            var stock = new Android.Widget.Button(TestHost.RequiredContext);
            var button = new AutoBrushButton(TestHost.RequiredContext);
            Assert.IsTrue(stock.PaddingLeft > 0, "The stock button is expected to pad its text");
            Assert.AreEqual(stock.PaddingLeft, button.PaddingLeft);
            Assert.AreEqual(stock.PaddingTop, button.PaddingTop);
            Assert.AreEqual(stock.PaddingRight, button.PaddingRight);
            Assert.AreEqual(stock.PaddingBottom, button.PaddingBottom);
        }

        [TestMethod]
        public void BrushButtons_InflateFromLayoutWithTheirAttributes ()
        {
            View root = LayoutInflater.From(TestHost.RequiredContext)!.Inflate(Resource.Layout.brush_buttons_test, null)!;

            var auto = root.FindViewById<AutoBrushButton>(Resource.Id.auto_brush_button)!;
            Assert.AreEqual(unchecked((int)0xFF7A1FA2), auto.PropagatedAccentColor.ToArgb());
            Assert.AreEqual(10f, auto.CornerRadius);
            Assert.AreEqual(3, auto.BorderThickness);

            var brush = root.FindViewById<BrushButton>(Resource.Id.brush_button)!;
            Assert.AreEqual(new ColorArgb(0xFF, 0x11, 0x22, 0x33), _SurfaceColor(brush));
            Assert.AreEqual(unchecked((int)0xFFAABBCC), brush.PressedForeground!.Value.ToArgb());
            Assert.IsNull(brush.NormalForeground, "A slot the layout leaves out stays unset");
        }

        // ===========[ Helper Methods ]===================================
        private static void _AssertPainted (_ManagedBrushButton button, SurfaceStateColors expected, string state)
        {
            Assert.AreEqual(expected.Background, _SurfaceColor(button), $"{state} background");
            Assert.AreEqual(expected.Foreground, new AndroidColor(button.CurrentTextColor).ToColorArgb(), $"{state} foreground");
        }

        private static ColorArgb _SurfaceColor (_ManagedBrushButton button)
        {
            var surface = (GradientDrawable)button.Background!;
            return new AndroidColor(surface.Color!.DefaultColor).ToColorArgb();
        }
    }
}
