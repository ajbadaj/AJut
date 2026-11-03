namespace AJut.UX.WinUI3.Tests
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The WinUI3 SimpleWrapPanel's layout, now AJut.UX's <see cref="SimpleWrapLayout"/>, checked against the inline
    /// measure and arrange it replaced, without making the panel.
    /// </summary>
    [TestClass]
    public class SimpleWrapPanelLayoutTests
    {
        // 40, 48 and 42.5 are exactly two 20 wide children with each spacing between them, the edge of wrapping
        private static readonly double[] kWidths = { double.PositiveInfinity, double.NaN, 0.0, 1.0, 15.0, 30.0, 31.0, 40.0, 42.5, 45.0, 48.0, 50.0, 64.0, 100.0, 1000.0 };
        private static readonly double[][] kSpacings = { new[] { 8.0, 6.0 }, new[] { 0.0, 0.0 }, new[] { 2.5, 11.0 } };
        private static readonly WrapItemSize[][] kChildSets =
        {
            Array.Empty<WrapItemSize>(),
            new[] { Size(20, 10) },
            new[] { Size(20, 10), Size(20, 10), Size(20, 10), Size(20, 10) },
            new[] { Size(12, 8), Size(30, 20), Size(5, 5), Size(44, 9), Size(17, 31), Size(9, 9) },

            // A collapsed child in the middle, one at each end, and one that wants a width of nothing but some height
            new[] { Size(0, 0), Size(20, 10), Size(0, 0), Size(20, 12), Size(0, 0) },
            new[] { Size(20, 10), Size(0, 15), Size(20, 10), Size(0, 0), Size(25, 5) },

            // A child wider than any of the widths, alone, first, and in the middle
            new[] { Size(2000, 40) },
            new[] { Size(2000, 40), Size(10, 10), Size(10, 10) },
            new[] { Size(10, 10), Size(2000, 40), Size(10, 10) },
        };

        [TestMethod]
        public void SimpleWrapLayout_MatchesTheInlineRulesItReplaced ()
        {
            int checkedCount = 0;
            foreach (WrapItemSize[] children in kChildSets)
            {
                foreach (double[] spacing in kSpacings)
                {
                    foreach (double width in kWidths)
                    {
                        string context = $"{children.Length} children, spacing {spacing[0]}/{spacing[1]}, width {width}";
                        SimpleWrapLayout layout = SimpleWrapLayout.Compute(children, width, spacing[0], spacing[1]);

                        ReferenceMeasure(children, width, spacing[0], spacing[1], out double expectedWidth, out double expectedHeight);
                        Assert.AreEqual(expectedWidth, layout.Width, $"Measured width, {context}");
                        Assert.AreEqual(expectedHeight, layout.Height, $"Measured height, {context}");

                        // The panel only arranges at a real width
                        if (double.IsFinite(width))
                        {
                            List<double[]> expectedRects = ReferenceArrange(children, width, spacing[0], spacing[1]);
                            for (int index = 0; index < children.Length; ++index)
                            {
                                SimpleWrapLayout.Slot slot = layout.Slots[index];
                                double[] actualRect = slot.IsPlaced ? new[] { slot.X, slot.Y, slot.Width, slot.Height } : new[] { 0.0, 0.0, 0.0, 0.0 };
                                CollectionAssert.AreEqual(expectedRects[index], actualRect, $"Child {index}'s rect, {context}");
                            }
                        }

                        ++checkedCount;
                    }
                }
            }

            Assert.AreEqual(kChildSets.Length * kSpacings.Length * kWidths.Length, checkedCount);
        }

        // ===========[ The rules replaced ]===================================
        // SimpleWrapPanel's MeasureOverride and ArrangeOverride as they were before the shared layout, word for word
        // apart from taking the children's desired sizes as WrapItemSize and returning plain numbers

        private static void ReferenceMeasure (WrapItemSize[] children, double available, double horizontalSpacing, double verticalSpacing, out double resultWidth, out double resultHeight)
        {
            bool unbounded = double.IsInfinity(available) || double.IsNaN(available);

            double lineWidth = 0;
            double lineHeight = 0;
            double widest = 0;
            double totalHeight = 0;

            foreach (WrapItemSize desired in children)
            {
                if ((desired.Width <= 0) && (desired.Height <= 0))
                {
                    continue;
                }

                if (!unbounded && (lineWidth > 0) && (lineWidth + horizontalSpacing + desired.Width > available))
                {
                    widest = Math.Max(widest, lineWidth);
                    totalHeight += lineHeight + verticalSpacing;
                    lineWidth = desired.Width;
                    lineHeight = desired.Height;
                }
                else
                {
                    lineWidth += (lineWidth > 0) ? horizontalSpacing + desired.Width : desired.Width;
                    lineHeight = Math.Max(lineHeight, desired.Height);
                }
            }

            widest = Math.Max(widest, lineWidth);
            totalHeight += lineHeight;

            resultWidth = unbounded ? widest : Math.Min(widest, available);
            resultHeight = totalHeight;
        }

        private static List<double[]> ReferenceArrange (WrapItemSize[] children, double available, double horizontalSpacing, double verticalSpacing)
        {
            var arranged = new List<double[]>();
            double x = 0;
            double y = 0;
            double lineHeight = 0;

            foreach (WrapItemSize desired in children)
            {
                if ((desired.Width <= 0) && (desired.Height <= 0))
                {
                    arranged.Add(new[] { 0.0, 0.0, 0.0, 0.0 });
                    continue;
                }

                if ((x > 0) && (x + horizontalSpacing + desired.Width > available))
                {
                    x = 0;
                    y += lineHeight + verticalSpacing;
                    lineHeight = 0;
                }

                if (x > 0)
                {
                    x += horizontalSpacing;
                }

                arranged.Add(new[] { x, y, desired.Width, desired.Height });
                x += desired.Width;
                lineHeight = Math.Max(lineHeight, desired.Height);
            }

            return arranged;
        }

        private static WrapItemSize Size (double width, double height) => new WrapItemSize(width, height);
    }
}
