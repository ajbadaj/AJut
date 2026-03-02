namespace AJut.UX.Tests
{
    using System;
    using AJut.UX.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ColorHelperTests
    {
        [TestMethod]
        public void CH_ColorFromStr_Basic()
        {
            _DoAssert("#FF00FF00", 255, 0, 255, 0);
            _DoAssert("#CC6B50", 255, 204, 107, 80);
            _DoAssert("#BE450414", 190, 69, 4, 20);
            _DoAssert("#ABCD", 170, 187, 204, 221);
            _DoAssert("#ABC", 255, 170, 187, 204);

            static void _DoAssert(string color, params byte[] expectedBytes)
            {
                Assert.IsTrue(AJutColorHelper.TryGetColorFromHex(color, out byte[] argb));
                Assert.IsNotNull(argb);
                Assert.AreEqual(4, argb.Length);
                Assert.AreEqual(expectedBytes[0], argb[0]);
                Assert.AreEqual(expectedBytes[1], argb[1]);
                Assert.AreEqual(expectedBytes[2], argb[2]);
                Assert.AreEqual(expectedBytes[3], argb[3]);
            }
        }

        [TestMethod]
        public void CH_SmallestHex_Basic()
        {
            _DoAssert("#ABC", 255, 170, 187, 204);
            _DoAssert("#ABCD", 170, 187, 204, 221);
            _DoAssert("#BE450414", 190, 69, 4, 20);

            static void _DoAssert(string colorExpected, params byte[] inputBytes)
            {
                string hex = AJutColorHelper.GetSmallestHexString(inputBytes[0], inputBytes[1], inputBytes[2], inputBytes[3]);
                Assert.IsNotNull(hex);
                Assert.AreEqual(colorExpected, hex, ignoreCase: true);
            }
        }

        [TestMethod]
        public void CH_GradientFromStr_Basic()
        {
            // Simple linear, no angle: defaults to 180° (to bottom)
            _DoAssert(
                "linear-gradient(#ff8c00, #e91e63)",
                eBrushGradientType.Linear, 180f,
                new(0f, "#FF8C00"), new(1f, "#e91e63")
            );

            // Explicit degree angle
            _DoAssert(
                "linear-gradient(90deg, #FF0000, #0000FF)",
                eBrushGradientType.Linear, 90f,
                new(0f, "#FF0000"), new(1f, "#0000FF")
            );

            // Direction keyword → to right = 90°
            _DoAssert(
                "linear-gradient(to right, #FF0000, #0000FF)",
                eBrushGradientType.Linear, 90f,
                new(0f, "#FF0000"), new(1f, "#0000FF")
            );

            // Turn unit (0.25turn = 90°)
            _DoAssert(
                "linear-gradient(0.25turn, #FF0000, #0000FF)",
                eBrushGradientType.Linear, 90f,
                new(0f, "#FF0000"), new(1f, "#0000FF")
            );

            // Explicit stop positions
            _DoAssert(
                "linear-gradient(#FF0000 25%, #0000FF 75%)",
                eBrushGradientType.Linear, 180f,
                new(0.25f, "#FF0000"), new(0.75f, "#0000FF")
            );

            // Three stops, middle auto-distributed to 50%
            _DoAssert(
                "linear-gradient(#FF0000, #00FF00, #0000FF)",
                eBrushGradientType.Linear, 180f,
                new(0f, "#FF0000"), new(0.5f, "#00FF00"), new(1f, "#0000FF")
            );

            // rgba() color in stop
            _DoAssert(
                "linear-gradient(rgba(255, 0, 0, 0.5), #0000FF)",
                eBrushGradientType.Linear, 180f,
                new(0f, 128, 255, 0, 0), new(1f, "#0000FF")
            );

            // rgb() color in stop
            _DoAssert(
                "linear-gradient(rgb(255, 128, 0), #000000)",
                eBrushGradientType.Linear, 180f,
                new(0f, 255, 255, 128, 0), new(1f, "#000000")
            );

            // transparent keyword
            _DoAssert(
                "linear-gradient(transparent, #000000)",
                eBrushGradientType.Linear, 180f,
                new(0f, 0, 0, 0, 0), new(1f, "#000000")
            );

            // Radial, basic circle
            _DoAssert(
                "radial-gradient(circle, #FF0000, #0000FF)",
                eBrushGradientType.Radial, null,
                new(0f, "#FF0000"), new(1f, "#0000FF")
            );

            // Radial with explicit center position
            _DoAssert(
                "radial-gradient(circle at 25% 75%, #FF0000, #0000FF)",
                eBrushGradientType.Radial, null,
                new(0f, "#FF0000"), new(1f, "#0000FF")
            );

            static void _DoAssert(string gradientStr, eBrushGradientType expectedType, float? expectedAngle, params StopTST[] expectedStops)
            {
                Assert.IsTrue(AJutColorHelper.TryGetGradientFromString(gradientStr, out GradientBuilder builder), $"Parse failed for: {gradientStr}");
                Assert.AreEqual(expectedType, builder.Type);
                Assert.IsNotNull(builder.Stops);
                Assert.AreEqual(expectedStops.Length, builder.Stops.Length, $"Stop count mismatch for: {gradientStr}");

                if (expectedAngle.HasValue && expectedType == eBrushGradientType.Linear)
                {
                    Assert.IsTrue(Math.Abs(expectedAngle.Value - builder.LinearParams.AngleDegrees) < 0.01f,
                        $"Angle mismatch for '{gradientStr}': expected {expectedAngle.Value}° got {builder.LinearParams.AngleDegrees}°");
                }

                for (int i = 0; i < expectedStops.Length; ++i)
                {
                    Assert.IsTrue(Math.Abs(expectedStops[i].Offset - builder.Stops[i].Offset) < 0.001f,
                        $"Stop {i} offset mismatch for '{gradientStr}': expected {expectedStops[i].Offset} got {builder.Stops[i].Offset}");
                    for (int b = 0; b < 4; b++)
                    {
                        Assert.AreEqual(expectedStops[i].ColorARGB[b], builder.Stops[i].ARGB[b],
                            $"Stop {i} ARGB[{b}] mismatch for '{gradientStr}'");
                    }
                }
            }
        }

        [TestMethod]
        public void CH_GradientFromStr_RadialParams()
        {
            // Default center + radius
            _DoAssertRadial("radial-gradient(circle, #FF0000, #0000FF)", 0.5f, 0.5f, 0.5f, 0.5f);

            // Named center position keywords
            _DoAssertRadial("radial-gradient(circle at top left, #FF0000, #0000FF)", 0f, 0f, 0.5f, 0.5f);
            _DoAssertRadial("radial-gradient(circle at bottom right, #FF0000, #0000FF)", 1f, 1f, 0.5f, 0.5f);
            _DoAssertRadial("radial-gradient(circle at center, #FF0000, #0000FF)", 0.5f, 0.5f, 0.5f, 0.5f);

            // Percentage center position
            _DoAssertRadial("radial-gradient(circle at 25% 75%, #FF0000, #0000FF)", 0.25f, 0.75f, 0.5f, 0.5f);

            // Explicit circle radius
            _DoAssertRadial("radial-gradient(circle 30% at center, #FF0000, #0000FF)", 0.5f, 0.5f, 0.3f, 0.3f);

            // Explicit ellipse radii
            _DoAssertRadial("radial-gradient(ellipse 40% 20% at center, #FF0000, #0000FF)", 0.5f, 0.5f, 0.4f, 0.2f);

            // Size keywords default to 0.5 (see gap docs in ColorHelpers.cs)
            _DoAssertRadial("radial-gradient(circle closest-side at center, #FF0000, #0000FF)", 0.5f, 0.5f, 0.5f, 0.5f);

            static void _DoAssertRadial(string gradientStr, float expCx, float expCy, float expRx, float expRy)
            {
                Assert.IsTrue(AJutColorHelper.TryGetGradientFromString(gradientStr, out GradientBuilder builder), $"Parse failed for: {gradientStr}");
                Assert.AreEqual(eBrushGradientType.Radial, builder.Type);
                float delta = 0.001f;
                Assert.IsTrue(Math.Abs(expCx - builder.RadialParams.CenterX) < delta, $"CenterX mismatch for '{gradientStr}': expected {expCx} got {builder.RadialParams.CenterX}");
                Assert.IsTrue(Math.Abs(expCy - builder.RadialParams.CenterY) < delta, $"CenterY mismatch for '{gradientStr}': expected {expCy} got {builder.RadialParams.CenterY}");
                Assert.IsTrue(Math.Abs(expRx - builder.RadialParams.RadiusX) < delta, $"RadiusX mismatch for '{gradientStr}': expected {expRx} got {builder.RadialParams.RadiusX}");
                Assert.IsTrue(Math.Abs(expRy - builder.RadialParams.RadiusY) < delta, $"RadiusY mismatch for '{gradientStr}': expected {expRy} got {builder.RadialParams.RadiusY}");
            }
        }

        private struct StopTST
        {
            // Constructs from a hex color string
            public StopTST(float offset, string color)
            {
                this.Offset = offset;
                if (color.Equals("transparent", StringComparison.OrdinalIgnoreCase))
                    this.ColorARGB = [0, 0, 0, 0];
                else
                {
                    Assert.IsTrue(AJutColorHelper.TryGetColorFromHex(color, out byte[] argb), $"Invalid color in test: {color}");
                    this.ColorARGB = argb;
                }
            }

            // Constructs from explicit ARGB bytes
            public StopTST(float offset, byte a, byte r, byte g, byte b)
            {
                this.Offset = offset;
                this.ColorARGB = [a, r, g, b];
            }

            public float Offset { get; }
            public byte[] ColorARGB { get; }
        }
    }
}
