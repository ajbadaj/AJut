namespace AJut.UX.WinUI3.Tests
{
    using AJut.UX.Controls;
    using Microsoft.UI.Xaml;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The WinUI3 ToggleStrip's mapping of the shared <see cref="ToggleStripItemEdges"/> onto each button's
    /// BorderThickness and CornerRadius, checked without making the control.
    /// </summary>
    [TestClass]
    public class ToggleStripEdgeStylingTests
    {
        private const double kSeparator = 2.0;
        private static readonly CornerRadius kStripRadius = new CornerRadius(3, 5, 7, 11);

        // ===[ Horizontal ]===

        [TestMethod]
        public void Horizontal_MatchesTheInlineRulesItReplaced ()
        {
            // The horizontal strip worked these out inline before the shared rules: a left separator on every button
            // but the first, a right one on the last only before an overflow button, the left corners round on the
            // first and the right corners on the last unless an overflow button follows it.
            foreach (int count in new[] { 1, 2, 3, 5 })
            {
                foreach (bool trailingSeparator in new[] { false, true })
                {
                    for (int index = 0; index < count; ++index)
                    {
                        bool isFirst = index == 0;
                        bool isLast = index == count - 1;
                        bool roundLeft = isFirst;
                        bool roundRight = isLast && !trailingSeparator;
                        Thickness expectedThickness = new Thickness(isFirst ? 0.0 : kSeparator, 0, (isLast && trailingSeparator) ? kSeparator : 0.0, 0);
                        CornerRadius expectedRadius = new CornerRadius(
                            roundLeft ? kStripRadius.TopLeft : 0,
                            roundRight ? kStripRadius.TopRight : 0,
                            roundRight ? kStripRadius.BottomRight : 0,
                            roundLeft ? kStripRadius.BottomLeft : 0
                        );

                        ToggleStripItemEdges edges = ToggleStripItemEdges.For(index, count, isVertical: false, trailingSeparator);
                        string where = $"button {index} of {count}, trailing separator {trailingSeparator}";
                        Assert.AreEqual(expectedThickness, ToggleStrip.EdgeStyling.BorderThicknessFor(edges, kSeparator), where);
                        Assert.AreEqual(expectedRadius, ToggleStrip.EdgeStyling.CornerRadiusFor(edges, kStripRadius), where);
                    }
                }
            }
        }

        // ===[ Vertical ]===

        [TestMethod]
        public void Vertical_FirstMiddleLast_TopSeparatorsAndRoundedTopAndBottom ()
        {
            ToggleStripItemEdges first = ToggleStripItemEdges.For(0, 3, isVertical: true, trailingSeparator: false);
            ToggleStripItemEdges middle = ToggleStripItemEdges.For(1, 3, isVertical: true, trailingSeparator: false);
            ToggleStripItemEdges last = ToggleStripItemEdges.For(2, 3, isVertical: true, trailingSeparator: false);

            Assert.AreEqual(new Thickness(0), ToggleStrip.EdgeStyling.BorderThicknessFor(first, kSeparator), "The first has no separator");
            Assert.AreEqual(new CornerRadius(3, 5, 0, 0), ToggleStrip.EdgeStyling.CornerRadiusFor(first, kStripRadius), "The first rounds its top corners with the strip's");

            Assert.AreEqual(new Thickness(0, kSeparator, 0, 0), ToggleStrip.EdgeStyling.BorderThicknessFor(middle, kSeparator), "A middle one has a separator on its top");
            Assert.AreEqual(new CornerRadius(0), ToggleStrip.EdgeStyling.CornerRadiusFor(middle, kStripRadius), "A middle one is square");

            Assert.AreEqual(new Thickness(0, kSeparator, 0, 0), ToggleStrip.EdgeStyling.BorderThicknessFor(last, kSeparator), "The last has a separator on its top");
            Assert.AreEqual(new CornerRadius(0, 0, 7, 11), ToggleStrip.EdgeStyling.CornerRadiusFor(last, kStripRadius), "The last rounds its bottom corners with the strip's");
        }

        [TestMethod]
        public void Vertical_LastBeforeAnOverflowButton_BottomSeparatorAndSquare ()
        {
            ToggleStripItemEdges last = ToggleStripItemEdges.For(2, 3, isVertical: true, trailingSeparator: true);
            Assert.AreEqual(new Thickness(0, kSeparator, 0, kSeparator), ToggleStrip.EdgeStyling.BorderThicknessFor(last, kSeparator));
            Assert.AreEqual(new CornerRadius(0), ToggleStrip.EdgeStyling.CornerRadiusFor(last, kStripRadius));
        }

        [TestMethod]
        public void Vertical_OnlyButton_TakesTheStripsFourCorners ()
        {
            ToggleStripItemEdges only = ToggleStripItemEdges.For(0, 1, isVertical: true, trailingSeparator: false);
            Assert.AreEqual(new Thickness(0), ToggleStrip.EdgeStyling.BorderThicknessFor(only, kSeparator));
            Assert.AreEqual(kStripRadius, ToggleStrip.EdgeStyling.CornerRadiusFor(only, kStripRadius));
        }
    }
}
