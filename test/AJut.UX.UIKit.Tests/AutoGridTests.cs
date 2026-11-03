namespace AJut.UX.Tests
{
    using System;
    using System.Linq;
    using AJut.UX.Controls;
    using CoreGraphics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using UIKit;

    [TestClass]
    public class AutoGridTests
    {
        private const float kWidth = 200f;
        private const float kHeight = 100f;

        [TestMethod]
        public void Layout_FixedColumns_FillsEachRowThenWraps ()
        {
            AutoGrid grid = _Grid(columns: 2, subviewCount: 3);
            _AssertFrame(grid.GridChildren[0], 0, 0, 100, 50);
            _AssertFrame(grid.GridChildren[1], 100, 0, 100, 50);
            _AssertFrame(grid.GridChildren[2], 0, 50, 100, 50);
        }

        [TestMethod]
        public void Layout_Padding_InsetsTheCells ()
        {
            AutoGrid grid = _Grid(columns: 2, subviewCount: 2);
            grid.Padding = new UIEdgeInsets(5, 10, 15, 20);
            grid.LayoutIfNeeded();

            _AssertFrame(grid.GridChildren[0], 10, 5, 85, 80);
            _AssertFrame(grid.GridChildren[1], 95, 5, 85, 80);
        }

        [TestMethod]
        public void Layout_BorderThicknessAndPadding_BothInsetTheCellsAndAddToTheSizeWanted ()
        {
            var grid = new AutoGrid
            {
                FixedColumnCount = 1,
                MinElementWidth = 0,
                MinElementHeight = 0,
                BorderThickness = new UIEdgeInsets(2, 2, 2, 2),
                Padding = new UIEdgeInsets(3, 3, 3, 3),
                Frame = new CGRect(0, 0, kWidth, kHeight),
            };
            grid.AddSubview(new UIView(new CGRect(0, 0, 50, 20)));

            Assert.AreEqual(30.0, (double)grid.SizeThatFits(new CGSize(kWidth, nfloat.MaxValue)).Height, 0.01, "The subview's height, then the padding, then the border, on each side");
            grid.LayoutIfNeeded();
            _AssertFrame(grid.GridChildren[0], 5, 5, kWidth - 10, kHeight - 10);
        }

        [TestMethod]
        public void Layout_RightToLeft_PutsTheFirstColumnOnTheRight ()
        {
            AutoGrid grid = _Grid(columns: 2, subviewCount: 2);
            grid.SemanticContentAttribute = UISemanticContentAttribute.ForceRightToLeft;
            grid.SetNeedsLayout();
            grid.LayoutIfNeeded();

            _AssertFrame(grid.GridChildren[0], 100, 0, 100, 100);
            _AssertFrame(grid.GridChildren[1], 0, 0, 100, 100);
        }

        [TestMethod]
        public void Layout_SubviewPastTheLastCell_IsOutOfRangeAndHasNoSize ()
        {
            AutoGrid grid = _Grid(columns: 2, subviewCount: 3);
            grid.FixedRowCount = 1;
            grid.LayoutIfNeeded();

            Assert.IsTrue(grid.GetIsInRange(grid.GridChildren[1]));
            Assert.IsFalse(grid.GetIsInRange(grid.GridChildren[2]), "Two by one holds two");
            Assert.AreEqual(0.0, (double)grid.GridChildren[2].Frame.Width, 0.01);
        }

        [TestMethod]
        public void Layout_HiddenSubview_KeepsItsCell ()
        {
            AutoGrid grid = _Grid(columns: 2, subviewCount: 3);
            grid.GridChildren[1].Hidden = true;
            grid.SetNeedsLayout();
            grid.LayoutIfNeeded();

            _AssertFrame(grid.GridChildren[2], 0, 50, 100, 50);
        }

        [TestMethod]
        public void GridChildren_AreTheSubviewsInOrder_LessTheResizerView ()
        {
            AutoGrid grid = _Grid(columns: 2, subviewCount: 3);
            Assert.AreEqual(3, grid.GridChildren.Length);
            Assert.AreEqual(4, grid.Subviews.Length, "The grid's own view for its resizers");
            Assert.IsFalse(grid.GridChildren.Contains(grid.Subviews.Last()), "The resizers' view stays on top as subviews come in");
        }

        [TestMethod]
        public void SizeThatFits_EachRowIsItsTallestSubview ()
        {
            var grid = new AutoGrid { FixedColumnCount = 2, MinElementHeight = 0, Frame = new CGRect(0, 0, kWidth, kHeight) };
            grid.AddSubview(new UIView(new CGRect(0, 0, 10, 30)));
            grid.AddSubview(new UIView(new CGRect(0, 0, 10, 10)));
            grid.AddSubview(new UIView(new CGRect(0, 0, 10, 20)));

            Assert.AreEqual(50.0, (double)grid.SizeThatFits(new CGSize(kWidth, nfloat.MaxValue)).Height, 0.01, "30 for the first row, 20 for the second");
        }

        [TestMethod]
        public void Layout_AtTheHeightItWants_TheRowsStillShareIt ()
        {
            // UIKit has no measure pass to say the grid has no height to fill, so the rows share what it has
            var grid = new AutoGrid { FixedColumnCount = 1, MinElementHeight = 0, AddRowResizers = true };
            grid.AddSubview(new UIView(new CGRect(0, 0, 10, 20)));
            grid.AddSubview(new UIView(new CGRect(0, 0, 10, 200)));
            nfloat wanted = grid.SizeThatFits(new CGSize(kWidth, nfloat.MaxValue)).Height;
            grid.Frame = new CGRect(0, 0, kWidth, wanted);
            grid.LayoutIfNeeded();

            Assert.AreEqual(220.0, (double)wanted, 0.01, "It wants each row as tall as its tallest subview");
            _AssertFrame(grid.GridChildren[0], 0, 0, kWidth, 110);
            _AssertFrame(grid.GridChildren[1], 0, 110, kWidth, 110);
            Assert.IsFalse(grid.GridChildren.Contains(grid.HitTest(new CGPoint(50, 109), null)), "The row resizer is there to drag");
        }

        [TestMethod]
        public void SetTargetFixedSizeFor_ARowJustAdded_TakesIt ()
        {
            AutoGrid grid = _Grid(columns: 1, subviewCount: 2);
            grid.MinElementHeight = 0;
            grid.AddSubview(new UIView());
            grid.SetTargetFixedSizeFor(2, 0, kWidth, 20);
            grid.LayoutIfNeeded();

            Assert.AreEqual(20.0, (double)grid.GridChildren[2].Frame.Height, 0.01, "The new row has the share 20 of 100 is");
        }

        [TestMethod]
        public void HitTest_OnAResizer_IsTheGrids ()
        {
            AutoGrid grid = _Grid(columns: 2, subviewCount: 2);
            grid.AddColumnResizers = true;
            grid.LayoutIfNeeded();

            UIView onResizer = grid.HitTest(new CGPoint(99, 50), null);
            UIView inACell = grid.HitTest(new CGPoint(40, 50), null);
            Assert.IsNotNull(onResizer);
            Assert.IsFalse(grid.GridChildren.Contains(onResizer), "A touch on the resizer goes to the grid's resizer view, not the cell under it");
            Assert.AreSame(grid.GridChildren[0], inACell, "A touch anywhere else goes through to the cell");
        }

        [TestMethod]
        public void HitTest_DisabledGrid_LetsTheResizerTouchThrough ()
        {
            AutoGrid grid = _Grid(columns: 2, subviewCount: 2);
            grid.AddColumnResizers = true;
            grid.Enabled = false;
            grid.LayoutIfNeeded();

            Assert.AreSame(grid.GridChildren[0], grid.HitTest(new CGPoint(99, 50), null));
        }

        // ===========[ Helper Methods ]===================================
        private static AutoGrid _Grid (int columns, int subviewCount)
        {
            var grid = new AutoGrid { FixedColumnCount = columns, Frame = new CGRect(0, 0, kWidth, kHeight) };
            for (int index = 0; index < subviewCount; ++index)
            {
                grid.AddSubview(new UIView { UserInteractionEnabled = true });
            }

            grid.LayoutIfNeeded();
            return grid;
        }

        private static void _AssertFrame (UIView subview, float x, float y, float width, float height)
        {
            Assert.AreEqual(x, (double)subview.Frame.X, 0.01, "x");
            Assert.AreEqual(y, (double)subview.Frame.Y, 0.01, "y");
            Assert.AreEqual(width, (double)subview.Frame.Width, 0.01, "width");
            Assert.AreEqual(height, (double)subview.Frame.Height, 0.01, "height");
        }
    }
}
