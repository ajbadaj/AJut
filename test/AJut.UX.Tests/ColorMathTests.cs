namespace AJut.UX.Tests
{
    using System;
    using AJut.UX.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ColorMathTests
    {
        // WCAG AA for normal text. PickReadableForeground anchors at pure white/black, so its worst case
        // (a background on the luminance crossover) still clears this.
        private const double kReadableContrast = 4.5;
        // Hover/pressed hold the resting foreground polarity (no flip), which can trade some contrast on
        // borderline accents; they should still clear WCAG AA for large text.
        private const double kReadableLargeContrast = 3.0;

        [TestMethod]
        public void CM_RgbToHsl_KnownColors ()
        {
            _DoAssert(ColorArgb.FromRgb(255, 0, 0), 0, 1, 0.5);     // red
            _DoAssert(ColorArgb.FromRgb(0, 255, 0), 120, 1, 0.5);   // green
            _DoAssert(ColorArgb.FromRgb(0, 0, 255), 240, 1, 0.5);   // blue
            _DoAssert(ColorArgb.FromRgb(255, 255, 255), 0, 0, 1.0); // white
            _DoAssert(ColorArgb.FromRgb(0, 0, 0), 0, 0, 0.0);       // black

            static void _DoAssert (ColorArgb color, double hue, double saturation, double lightness)
            {
                HslColor hsl = AJutColorHelper.RgbToHsl(color);
                Assert.AreEqual(hue, hsl.Hue, 0.5);
                Assert.AreEqual(saturation, hsl.Saturation, 0.01);
                Assert.AreEqual(lightness, hsl.Lightness, 0.01);
            }
        }

        [TestMethod]
        public void CM_HslRoundTrip_StaysWithinTolerance ()
        {
            _DoAssert(ColorArgb.FromRgb(139, 0, 0));      // dark red
            _DoAssert(ColorArgb.FromRgb(13, 131, 216));   // AJut primary highlight
            _DoAssert(ColorArgb.FromRgb(204, 107, 80));
            _DoAssert(ColorArgb.FromRgb(33, 150, 243));
            _DoAssert(new ColorArgb(128, 70, 200, 30));   // carries alpha through untouched

            static void _DoAssert (ColorArgb color)
            {
                ColorArgb back = AJutColorHelper.HslToRgb(AJutColorHelper.RgbToHsl(color), color.A);
                Assert.AreEqual(color.A, back.A);
                Assert.IsTrue(Math.Abs(color.R - back.R) <= 2, $"R off: {color.R} vs {back.R}");
                Assert.IsTrue(Math.Abs(color.G - back.G) <= 2, $"G off: {color.G} vs {back.G}");
                Assert.IsTrue(Math.Abs(color.B - back.B) <= 2, $"B off: {color.B} vs {back.B}");
            }
        }

        [TestMethod]
        public void CM_Luminance_BlackAndWhiteAnchors ()
        {
            Assert.AreEqual(0.0, AJutColorHelper.GetRelativeLuminance(ColorArgb.FromRgb(0, 0, 0)), 0.0001);
            Assert.AreEqual(1.0, AJutColorHelper.GetRelativeLuminance(ColorArgb.FromRgb(255, 255, 255)), 0.0001);
        }

        [TestMethod]
        public void CM_ContrastRatio_BlackOnWhiteIsMax ()
        {
            double ratio = AJutColorHelper.GetContrastRatio(ColorArgb.FromRgb(0, 0, 0), ColorArgb.FromRgb(255, 255, 255));
            Assert.AreEqual(21.0, ratio, 0.1);
        }

        [TestMethod]
        public void CM_PickReadableForeground_ContrastsBackground ()
        {
            _DoAssert(ColorArgb.FromRgb(139, 0, 0));     // dark red -> light fg
            _DoAssert(ColorArgb.FromRgb(255, 255, 255)); // white -> dark fg
            _DoAssert(ColorArgb.FromRgb(13, 131, 216));
            _DoAssert(ColorArgb.FromRgb(255, 235, 59));  // bright yellow -> dark fg
            _DoAssert(ColorArgb.FromRgb(128, 128, 128)); // mid gray (near the crossover)

            static void _DoAssert (ColorArgb background)
            {
                ColorArgb foreground = AJutColorHelper.PickReadableForeground(background);
                double ratio = AJutColorHelper.GetContrastRatio(background, foreground);
                Assert.IsTrue(ratio >= kReadableContrast, $"Contrast only {ratio:0.0} for background {background}");
            }
        }

        [TestMethod]
        public void CM_PickReadableForeground_DarkRedGivesLight ()
        {
            ColorArgb foreground = AJutColorHelper.PickReadableForeground(ColorArgb.FromRgb(139, 0, 0));
            Assert.IsFalse(AJutColorHelper.IsDark(foreground), "Expected a light foreground for dark red");
        }

        [TestMethod]
        public void CM_AdjustLightness_MovesAndClamps ()
        {
            ColorArgb mid = ColorArgb.FromRgb(100, 100, 100);
            Assert.IsTrue(AJutColorHelper.GetRelativeLuminance(AJutColorHelper.Lighten(mid, 0.2)) > AJutColorHelper.GetRelativeLuminance(mid));
            Assert.IsTrue(AJutColorHelper.GetRelativeLuminance(AJutColorHelper.Darken(mid, 0.2)) < AJutColorHelper.GetRelativeLuminance(mid));

            // Lightening white stays white, darkening black stays black.
            Assert.AreEqual(255, AJutColorHelper.Lighten(ColorArgb.FromRgb(255, 255, 255), 0.5).R);
            Assert.AreEqual(0, AJutColorHelper.Darken(ColorArgb.FromRgb(0, 0, 0), 0.5).R);
        }

        [TestMethod]
        public void CM_ScaleSaturation_ZeroIsGray ()
        {
            ColorArgb gray = AJutColorHelper.ScaleSaturation(ColorArgb.FromRgb(200, 50, 50), 0.0);
            Assert.AreEqual(gray.R, gray.G);
            Assert.AreEqual(gray.G, gray.B);
        }

        [TestMethod]
        public void CM_Blend_MidpointAndEndpoints ()
        {
            ColorArgb mid = AJutColorHelper.Blend(ColorArgb.FromRgb(0, 0, 0), ColorArgb.FromRgb(255, 255, 255), 0.5);
            Assert.IsTrue(Math.Abs(mid.R - 128) <= 1);
            Assert.IsTrue(Math.Abs(mid.G - 128) <= 1);
            Assert.IsTrue(Math.Abs(mid.B - 128) <= 1);

            ColorArgb a = new ColorArgb(255, 10, 20, 30);
            ColorArgb b = new ColorArgb(100, 200, 150, 90);
            Assert.AreEqual(a, AJutColorHelper.Blend(a, b, 0.0));
            Assert.AreEqual(b, AJutColorHelper.Blend(a, b, 1.0));
        }

        [TestMethod]
        public void CM_BuildFromAccent_NormalKeepsAccentAndDerivesStates ()
        {
            ColorArgb accent = ColorArgb.FromRgb(139, 0, 0); // dark red
            InteractiveSurfaceColors palette = InteractiveSurfaceColors.BuildFromAccent(accent);

            Assert.AreEqual(accent, palette.Normal.Background);
            Assert.IsTrue(AJutColorHelper.GetContrastRatio(palette.Normal.Background, palette.Normal.Foreground) >= kReadableContrast);
            Assert.AreNotEqual(palette.Normal.Background, palette.PointerOver.Background);
            Assert.AreNotEqual(palette.Normal.Background, palette.Pressed.Background);
            Assert.IsTrue(palette.Disabled.Background.A < 255, "Disabled background should be faded");
        }

        [TestMethod]
        public void CM_BuildFromAccent_AllInteractiveStatesReadable ()
        {
            foreach (ColorArgb accent in _SampleAccents())
            {
                InteractiveSurfaceColors palette = InteractiveSurfaceColors.BuildFromAccent(accent);

                // Resting state always uses the accent's best polarity, so it clears AA.
                double restRatio = AJutColorHelper.GetContrastRatio(palette.Normal.Background, palette.Normal.Foreground);
                Assert.IsTrue(restRatio >= kReadableContrast, $"Rest contrast only {restRatio:0.0} for accent {accent}");

                // Hover/pressed hold that polarity (no flip), so they clear at least AA-large.
                _AssertReadableLarge(palette.PointerOver, accent);
                _AssertReadableLarge(palette.Pressed, accent);
            }

            static void _AssertReadableLarge (SurfaceStateColors state, ColorArgb accent)
            {
                double ratio = AJutColorHelper.GetContrastRatio(state.Background, state.Foreground);
                Assert.IsTrue(ratio >= kReadableLargeContrast, $"Contrast only {ratio:0.0} for accent {accent}");
            }
        }

        [TestMethod]
        public void CM_BuildFromAccent_ForegroundPolarityHeldAcrossStates ()
        {
            // The reported bug: #186E11 went white text at rest then flipped to black on hover/press.
            // Foreground polarity must be identical across rest / hover / pressed for every accent.
            foreach (ColorArgb accent in _SampleAccents())
            {
                InteractiveSurfaceColors palette = InteractiveSurfaceColors.BuildFromAccent(accent);
                bool restIsLight = !AJutColorHelper.IsDark(palette.Normal.Foreground);
                Assert.AreEqual(restIsLight, !AJutColorHelper.IsDark(palette.PointerOver.Foreground), $"Hover foreground flipped for accent {accent}");
                Assert.AreEqual(restIsLight, !AJutColorHelper.IsDark(palette.Pressed.Foreground), $"Pressed foreground flipped for accent {accent}");
            }
        }

        private static ColorArgb[] _SampleAccents () => new[]
        {
            ColorArgb.FromRgb(139, 0, 0),    // dark red
            ColorArgb.FromRgb(24, 110, 17),  // #186E11 - the reported flip case
            ColorArgb.FromRgb(13, 131, 216), // AJut primary highlight
            ColorArgb.FromRgb(255, 235, 59), // bright yellow
            ColorArgb.FromRgb(46, 204, 113), // green
            ColorArgb.FromRgb(128, 128, 128),// mid gray
        };
    }
}
