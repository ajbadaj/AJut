namespace AJut.UX.Tests
{
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Media;
    using System.Windows.Threading;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Where AutoGrid puts its children, how many rows and columns it makes, how it shares them, where its resizers go, and
    /// how its padding and border inset it. It adds and removes resizers on the dispatcher, so each test runs what is queued
    /// before it looks.
    /// </summary>
    [TestClass]
    public class WpfAutoGridTests
    {
        private const double kShareTolerance = 0.001;

        [TestMethod]
        public void WpfAutoGrid_FixedColumns_FillsEachRowBeforeTheNext ()
        {
            StaTestThread.Run(() =>
            {
                var grid = new AutoGrid { FixedColumnCount = 3 };
                UIElement[] children = _AddChildren(grid, 5);
                _PumpDispatcher();

                _AssertCell(children[0], 0, 0);
                _AssertCell(children[1], 0, 1);
                _AssertCell(children[2], 0, 2);
                _AssertCell(children[3], 1, 0);
                _AssertCell(children[4], 1, 1);
                Assert.AreEqual(2, grid.RowDefinitions.Count, "rows");
                Assert.AreEqual(3, grid.ColumnDefinitions.Count, "columns");
            });
        }

        [TestMethod]
        public void WpfAutoGrid_AutoOrderWithOnlyRowsFixed_FillsEachColumnBeforeTheNext ()
        {
            StaTestThread.Run(() =>
            {
                var grid = new AutoGrid { FixedRowCount = 2, AutoPopulationOrder = eAutoPopulationOrder.Auto };
                UIElement[] children = _AddChildren(grid, 3);
                _PumpDispatcher();

                _AssertCell(children[0], 0, 0);
                _AssertCell(children[1], 1, 0);
                _AssertCell(children[2], 0, 1);
            });
        }

        [TestMethod]
        public void WpfAutoGrid_DefaultOrderWithOnlyRowsFixed_FillsTheFirstRow ()
        {
            StaTestThread.Run(() =>
            {
                // Columns first is the default, and with no column count to wrap at every child goes along the first row
                var grid = new AutoGrid { FixedRowCount = 2 };
                UIElement[] children = _AddChildren(grid, 3);
                _PumpDispatcher();

                Assert.AreEqual(eAutoPopulationOrder.ColumnsFirst, grid.AutoPopulationOrder);
                _AssertCell(children[0], 0, 0);
                _AssertCell(children[1], 0, 1);
                _AssertCell(children[2], 0, 2);
            });
        }

        [TestMethod]
        public void WpfAutoGrid_MoreChildrenThanFixedCells_MarksTheExtraOutOfRange ()
        {
            StaTestThread.Run(() =>
            {
                var grid = new AutoGrid { FixedRowCount = 2, FixedColumnCount = 2 };
                UIElement[] children = _AddChildren(grid, 5);
                _PumpDispatcher();

                for (int index = 0; index < 4; ++index)
                {
                    Assert.IsTrue(AutoGrid.GetIsInRange(children[index]), $"child {index} has a cell");
                }

                Assert.IsFalse(AutoGrid.GetIsInRange(children[4]), "The fifth child has no cell in a two by two grid");
            });
        }

        [TestMethod]
        public void WpfAutoGrid_BothCountsFixed_KeepsEveryTrackUnlessShrinking ()
        {
            StaTestThread.Run(() =>
            {
                var grid = new AutoGrid { FixedRowCount = 2, FixedColumnCount = 2 };
                _AddChildren(grid, 1);
                _PumpDispatcher();
                Assert.AreEqual(2, grid.RowDefinitions.Count, "rows kept");
                Assert.AreEqual(2, grid.ColumnDefinitions.Count, "columns kept");

                grid.ShrinkRowColumnsToUtilizedElementCount = true;
                _PumpDispatcher();
                Assert.AreEqual(1, grid.RowDefinitions.Count, "rows shrunk to the one used");
                Assert.AreEqual(1, grid.ColumnDefinitions.Count, "columns shrunk to the one used");
            });
        }

        [TestMethod]
        public void WpfAutoGrid_NewRow_TakesTheAverageShareThenAllAreRescaled ()
        {
            StaTestThread.Run(() =>
            {
                var grid = new AutoGrid { FixedColumnCount = 1 };
                _AddChildren(grid, 2);
                grid.SetTargetSizePercentageFor(0, 0, 0.7, -1.0);
                _AssertShares(grid.RowDefinitions.Select(r => r.Height.Value).ToArray(), 0.7, 0.3);

                // The new row comes in at 1/2, the average of two shares that sum to 1, and then all three are scaled to sum to 1
                _AddChildren(grid, 1);
                _PumpDispatcher();
                _AssertShares(grid.RowDefinitions.Select(r => r.Height.Value).ToArray(), 0.7 / 1.5, 0.3 / 1.5, 0.5 / 1.5);
            });
        }

        [TestMethod]
        public void WpfAutoGrid_TargetShareForRowsOnly_LeavesTheColumnsAsTheyWere ()
        {
            StaTestThread.Run(() =>
            {
                // -1 asks for no change on that axis
                var grid = new AutoGrid { FixedColumnCount = 2 };
                _AddChildren(grid, 4);
                grid.SetTargetSizePercentageFor(0, 0, 0.7, -1.0);

                _AssertShares(grid.RowDefinitions.Select(r => r.Height.Value).ToArray(), 0.7, 0.3);
                _AssertShares(grid.ColumnDefinitions.Select(c => c.Width.Value).ToArray(), 0.5, 0.5);
            });
        }

        [TestMethod]
        public void WpfAutoGrid_LosingARow_LeavesNoResizerPastTheLastRow ()
        {
            StaTestThread.Run(() =>
            {
                var grid = new AutoGrid { FixedColumnCount = 1, AddRowResizers = true };
                UIElement[] children = _AddChildren(grid, 3);
                _PumpDispatcher();

                grid.Children.Remove(children[2]);
                _PumpDispatcher();

                GridSplitter[] resizers = _Resizers(grid);
                Assert.AreEqual(1, resizers.Length, "Two rows have one resizer between them");
                Assert.AreEqual(0, Grid.GetRow(resizers[0]), "The resizer sits on the first row's bottom edge");
            });
        }

        [TestMethod]
        public void WpfAutoGrid_FewerChildrenThanFixedColumns_MakesResizersOnlyBetweenColumnsThereAre ()
        {
            StaTestThread.Run(() =>
            {
                var grid = new AutoGrid { FixedColumnCount = 3, AddColumnResizers = true };
                _AddChildren(grid, 2);
                _PumpDispatcher();

                GridSplitter[] resizers = _Resizers(grid);
                Assert.AreEqual(2, grid.ColumnDefinitions.Count, "columns");
                Assert.AreEqual(1, resizers.Length, "Two columns have one resizer between them");
                Assert.AreEqual(0, Grid.GetColumn(resizers[0]), "The resizer sits on the first column's right edge");
            });
        }

        [TestMethod]
        public void WpfAutoGrid_ShrinkingBothFixedCounts_KeepsOneResizerBetweenEachPairOfUsedRows ()
        {
            StaTestThread.Run(() =>
            {
                var grid = new AutoGrid
                {
                    FixedRowCount = 4,
                    FixedColumnCount = 1,
                    ShrinkRowColumnsToUtilizedElementCount = true,
                    AddRowResizers = true,
                };
                _AddChildren(grid, 2);
                _PumpDispatcher();

                GridSplitter[] resizers = _Resizers(grid);
                Assert.AreEqual(2, grid.RowDefinitions.Count, "rows");
                Assert.AreEqual(1, resizers.Length, "Two used rows have one resizer between them");
                Assert.AreEqual(0, Grid.GetRow(resizers[0]), "The resizer sits on the first row's bottom edge");
            });
        }

        [TestMethod]
        public void WpfAutoGrid_NoHeightToFill_HasNoRowResizerToDrag ()
        {
            StaTestThread.Run(() =>
            {
                // In a StackPanel the rows follow their children, as star rows do with no height to fill, so a row resizer
                //  would move nothing: it takes no room and no input
                var grid = new AutoGrid { FixedColumnCount = 1, AddRowResizers = true, SizerLength = 4 };
                grid.Children.Add(new Border { Height = 20 });
                grid.Children.Add(new Border { Height = 200 });
                var stack = new StackPanel();
                stack.Children.Add(grid);
                _PumpDispatcher();
                stack.Measure(new Size(200, double.PositiveInfinity));
                stack.Arrange(new Rect(0, 0, 200, stack.DesiredSize.Height));

                GridSplitter resizer = _Resizers(grid).Single();
                Assert.AreEqual(220.0, grid.ActualHeight, "The rows are their children's heights");
                // Its height is SizerLength's, so it is the width that goes
                Assert.AreEqual(0.0, resizer.RenderSize.Width * resizer.RenderSize.Height, $"A row resizer with no height to fill has no area, so it draws nothing and takes no press; it is {resizer.RenderSize}");
            });
        }

        [TestMethod]
        public void WpfAutoGrid_WidthToFill_KeepsItsColumnResizer ()
        {
            StaTestThread.Run(() =>
            {
                var grid = new AutoGrid { FixedRowCount = 1, AddColumnResizers = true, SizerLength = 4 };
                _AddChildren(grid, 2);
                _PumpDispatcher();
                _LayOut(grid, 200, 100);

                Assert.AreEqual(new Size(4, 100), _Resizers(grid).Single().RenderSize);
            });
        }

        [TestMethod]
        public void WpfAutoGrid_Padding_InsetsTheChildren ()
        {
            StaTestThread.Run(() =>
            {
                var grid = new AutoGrid { FixedColumnCount = 1 };
                var child = (FrameworkElement)_AddChildren(grid, 1)[0];
                _LayOut(grid, 200, 100);

                grid.Padding = new Thickness(10, 5, 20, 15);
                _LayOut(grid, 200, 100);

                Assert.AreEqual(new Rect(10, 5, 170, 80), LayoutInformation.GetLayoutSlot(child), "The child fills what the padding leaves");
            });
        }

        [TestMethod]
        public void WpfAutoGrid_BorderThicknessAndPadding_BothInsetTheChildrenAndAddToTheSizeWanted ()
        {
            StaTestThread.Run(() =>
            {
                var grid = new AutoGrid { FixedColumnCount = 1, BorderThickness = new Thickness(2), Padding = new Thickness(3) };
                var child = (FrameworkElement)_AddChildren(grid, 1)[0];
                child.Width = 50;
                child.Height = 20;

                grid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.AreEqual(new Size(60, 30), grid.DesiredSize, "The child's size, then the padding, then the border, on each side");

                _LayOut(grid, 200, 100);
                Assert.AreEqual(new Rect(5, 5, 190, 90), LayoutInformation.GetLayoutSlot(child), "The child sits inside the border and the padding");
            });
        }

        [TestMethod]
        public void WpfAutoGrid_BorderBrushAndThickness_DrawABorderAroundTheGrid ()
        {
            StaTestThread.Run(() =>
            {
                var grid = new AutoGrid { FixedColumnCount = 1, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) };
                _AddChildren(grid, 1);
                _LayOut(grid, 200, 100);

                DrawingGroup drawing = VisualTreeHelper.GetDrawing(grid);
                Assert.IsNotNull(drawing, "A grid with a border brush and thickness, and no background, draws nothing");
                Assert.AreEqual(new Rect(0, 0, 200, 100), drawing.Bounds, "The border runs around the grid's edge");
            });
        }

        // ===========[ Helper Methods ]========================================
        private static void _LayOut (AutoGrid grid, double width, double height)
        {
            grid.Measure(new Size(width, height));
            grid.Arrange(new Rect(0, 0, width, height));
        }

        private static UIElement[] _AddChildren (AutoGrid grid, int count)
        {
            var children = new UIElement[count];
            for (int index = 0; index < count; ++index)
            {
                children[index] = new Border();
                grid.Children.Add(children[index]);
            }

            return children;
        }

        private static GridSplitter[] _Resizers (AutoGrid grid) => grid.Children.OfType<GridSplitter>().ToArray();

        private static void _AssertCell (UIElement child, int row, int column)
        {
            Assert.AreEqual(row, Grid.GetRow(child), "row");
            Assert.AreEqual(column, Grid.GetColumn(child), "column");
        }

        private static void _AssertShares (double[] actual, params double[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Length, "track count");
            for (int index = 0; index < expected.Length; ++index)
            {
                Assert.AreEqual(expected[index], actual[index], kShareTolerance, $"share of track {index}");
            }
        }

        // Runs everything queued on this thread's dispatcher, which is where the grid adds and removes its resizers
        private static void _PumpDispatcher ()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(_EndFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        private static object _EndFrame (object frame)
        {
            ((DispatcherFrame)frame).Continue = false;
            return null;
        }
    }
}
