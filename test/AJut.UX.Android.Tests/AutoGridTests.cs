namespace AJut.UX.Tests
{
    using AJut.UX.Controls;
    using Android.OS;
    using Android.Views;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AutoGridTests
    {
        private const int kWidth = 200;
        private const int kHeight = 100;

        [TestMethod]
        public void Layout_FixedColumns_FillsEachRowThenWraps ()
        {
            AutoGrid grid = _Grid(columns: 2, childCount: 3);
            _LayOut(grid, kWidth, kHeight);

            _AssertBounds(grid.GetChildAt(0)!, 0, 0, 100, 50);
            _AssertBounds(grid.GetChildAt(1)!, 100, 0, 100, 50);
            _AssertBounds(grid.GetChildAt(2)!, 0, 50, 100, 50);
        }

        [TestMethod]
        public void Layout_Padding_InsetsTheCells ()
        {
            AutoGrid grid = _Grid(columns: 2, childCount: 2);
            grid.SetPadding(10, 5, 20, 15);
            _LayOut(grid, kWidth, kHeight);

            _AssertBounds(grid.GetChildAt(0)!, 10, 5, 85, 80);
            _AssertBounds(grid.GetChildAt(1)!, 95, 5, 85, 80);
        }

        [TestMethod]
        public void Layout_BorderThicknessAndPadding_BothInsetTheCellsAndAddToTheSizeWanted ()
        {
            var grid = new AutoGrid(TestHost.RequiredContext) { FixedColumnCount = 1, MinElementWidth = 0, MinElementHeight = 0 };
            grid.AddView(_Child(height: 20, width: 50));
            grid.SetBorderThickness(2, 2, 2, 2);
            grid.SetPadding(3, 3, 3, 3);

            int unspecified = View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified);
            grid.Measure(unspecified, unspecified);
            Assert.AreEqual(60, grid.MeasuredWidth, "The child's width, then the padding, then the border, on each side");
            Assert.AreEqual(30, grid.MeasuredHeight);

            _LayOut(grid, kWidth, kHeight);
            _AssertBounds(grid.GetChildAt(0)!, 5, 5, kWidth - 10, kHeight - 10);
        }

        [TestMethod]
        public void Layout_RightToLeft_PutsTheFirstColumnOnTheRight ()
        {
            AutoGrid grid = _Grid(columns: 2, childCount: 2);
            grid.LayoutDirection = LayoutDirection.Rtl;
            _LayOut(grid, kWidth, kHeight);

            _AssertBounds(grid.GetChildAt(0)!, 100, 0, 100, 100);
            _AssertBounds(grid.GetChildAt(1)!, 0, 0, 100, 100);
        }

        [TestMethod]
        public void Measure_NoHeightToFill_EachRowIsItsTallestChild ()
        {
            var grid = new AutoGrid(TestHost.RequiredContext) { FixedColumnCount = 2, MinElementHeight = 0 };
            grid.AddView(_Child(height: 30));
            grid.AddView(_Child(height: 10));
            grid.AddView(_Child(height: 20));

            grid.Measure(
                View.MeasureSpec.MakeMeasureSpec(kWidth, MeasureSpecMode.Exactly),
                View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified)
            );
            grid.Layout(0, 0, kWidth, grid.MeasuredHeight);

            Assert.AreEqual(50, grid.MeasuredHeight, "30 for the first row, 20 for the second");
            _AssertBounds(grid.GetChildAt(1)!, 100, 0, 100, 30);
            _AssertBounds(grid.GetChildAt(2)!, 0, 30, 100, 20);
        }

        [TestMethod]
        public void Layout_ChildPastTheLastCell_IsOutOfRangeAndHasNoSize ()
        {
            AutoGrid grid = _Grid(columns: 2, childCount: 3);
            grid.FixedRowCount = 1;
            _LayOut(grid, kWidth, kHeight);

            Assert.IsTrue(grid.GetIsInRange(grid.GetChildAt(1)!));
            Assert.IsFalse(grid.GetIsInRange(grid.GetChildAt(2)!), "Two by one holds two");
            Assert.AreEqual(0, grid.GetChildAt(2)!.Width);
            Assert.AreEqual(0, grid.GetChildAt(2)!.Height);
        }

        [TestMethod]
        public void Layout_GoneChild_KeepsItsCell ()
        {
            AutoGrid grid = _Grid(columns: 2, childCount: 3);
            grid.GetChildAt(1)!.Visibility = ViewStates.Gone;
            _LayOut(grid, kWidth, kHeight);

            _AssertBounds(grid.GetChildAt(2)!, 0, 50, 100, 50);
        }

        [TestMethod]
        public void Drag_ColumnResizer_TradesWidthBetweenTheColumns ()
        {
            AutoGrid grid = _Grid(columns: 2, childCount: 2);
            grid.AddColumnResizers = true;
            _LayOut(grid, kWidth, kHeight);

            _Drag(grid, 99, 50, 129, 50);
            _LayOut(grid, kWidth, kHeight);

            Assert.AreEqual(130, grid.GetChildAt(0)!.Width);
            Assert.AreEqual(70, grid.GetChildAt(1)!.Width);
        }

        [TestMethod]
        public void Drag_RowResizerInsideAChild_IsTheGridsNotTheChilds ()
        {
            AutoGrid grid = _Grid(columns: 1, childCount: 2);
            grid.AddRowResizers = true;
            var tapped = new TouchCounter();
            View child = grid.GetChildAt(0)!;
            child.Clickable = true;
            child.Touch += tapped.OnTouch;
            _LayOut(grid, kWidth, kHeight);

            _Drag(grid, 50, 49, 50, 69);
            _LayOut(grid, kWidth, kHeight);
            child.Touch -= tapped.OnTouch;

            Assert.AreEqual(70, grid.GetChildAt(0)!.Height);
            Assert.AreEqual(0, tapped.DownCount, "The press landed on the resizer, so the child never saw it come down");
        }

        [TestMethod]
        public void Drag_Disabled_ChangesNothing ()
        {
            AutoGrid grid = _Grid(columns: 2, childCount: 2);
            grid.AddColumnResizers = true;
            grid.Enabled = false;
            _LayOut(grid, kWidth, kHeight);

            _Drag(grid, 99, 50, 129, 50);
            _LayOut(grid, kWidth, kHeight);

            Assert.AreEqual(100, grid.GetChildAt(0)!.Width);
        }

        [TestMethod]
        public void Drag_AwayFromAResizer_ChangesNothing ()
        {
            // The grab band is a finger wide in dp, so a wide grid keeps the press well clear of it on any density
            const int kWideWidth = 4000;
            AutoGrid grid = _Grid(columns: 2, childCount: 2);
            grid.AddColumnResizers = true;
            _LayOut(grid, kWideWidth, kHeight);

            _Drag(grid, 400, 50, 700, 50);
            _LayOut(grid, kWideWidth, kHeight);

            Assert.AreEqual(kWideWidth / 2, grid.GetChildAt(0)!.Width);
        }

        [TestMethod]
        public void SetTargetFixedSizeFor_ARowJustAdded_TakesIt ()
        {
            AutoGrid grid = _Grid(columns: 1, childCount: 2);
            grid.MinElementHeight = 0;
            _LayOut(grid, kWidth, kHeight);

            grid.AddView(new View(TestHost.RequiredContext));
            grid.SetTargetFixedSizeFor(2, 0, kWidth, 20);
            _LayOut(grid, kWidth, kHeight);

            Assert.AreEqual(20, grid.GetChildAt(2)!.Height, "The new row has the share 20 of 100 is");
        }

        [TestMethod]
        public void Drag_RowResizerWithNoHeightToFill_IsNotThere ()
        {
            // The rows follow their children, so a resizer would move nothing; the press goes to the child
            var grid = new AutoGrid(TestHost.RequiredContext) { FixedColumnCount = 1, AddRowResizers = true, SizerLength = 2 };
            grid.AddView(_Child(height: 50));
            grid.AddView(_Child(height: 50));
            var tapped = new TouchCounter();
            View child = grid.GetChildAt(0)!;
            child.Clickable = true;
            child.Touch += tapped.OnTouch;
            grid.Measure(
                View.MeasureSpec.MakeMeasureSpec(kWidth, MeasureSpecMode.Exactly),
                View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified)
            );
            grid.Layout(0, 0, kWidth, grid.MeasuredHeight);

            _Drag(grid, 50, 49, 50, 69);
            child.Touch -= tapped.OnTouch;

            Assert.AreEqual(1, tapped.DownCount, "The child gets the press");
            Assert.AreEqual(50, grid.GetChildAt(0)!.Height);
        }

        [TestMethod]
        public void Measure_WrapContentChildrenWiderThanTheSpace_ShareTheSpace ()
        {
            var grid = new AutoGrid(TestHost.RequiredContext) { FixedColumnCount = 2 };
            grid.AddView(_Child(width: 300, height: 10));
            grid.AddView(_Child(width: 100, height: 10));

            grid.Measure(
                View.MeasureSpec.MakeMeasureSpec(kWidth, MeasureSpecMode.AtMost),
                View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified)
            );
            grid.Layout(0, 0, grid.MeasuredWidth, grid.MeasuredHeight);

            Assert.AreEqual(kWidth, grid.MeasuredWidth, "No wider than it may be");
            Assert.AreEqual(100, grid.GetChildAt(0)!.Width, "The columns share the width, measured at what they get");
            Assert.AreEqual(100, grid.GetChildAt(1)!.Width);
        }

        [TestMethod]
        public void Grid_InflatesFromLayoutWithItsSettings ()
        {
            View root = LayoutInflater.From(TestHost.RequiredContext)!.Inflate(Resource.Layout.auto_grid_test, null)!;
            var grid = root.FindViewById<AutoGrid>(Resource.Id.auto_grid)!;
            Assert.AreEqual(2, grid.FixedColumnCount);
            Assert.AreEqual(3, grid.FixedRowCount);
            Assert.AreEqual(eAutoPopulationOrder.RowsFirst, grid.AutoPopulationOrder);
            Assert.IsTrue(grid.AddColumnResizers);
            Assert.IsTrue(grid.AddRowResizers);
            Assert.IsTrue(grid.ShrinkRowColumnsToUtilizedElementCount);
            Assert.IsFalse(grid.Enabled, "android:enabled, which a ViewGroup ignores on its own");
            Assert.AreEqual(3, grid.BorderThicknessLeft, "ajutBorderThickness sets every side");
            Assert.AreEqual(3, grid.BorderThicknessBottom);
            Assert.AreEqual(unchecked((int)0xFFFF0000), grid.BorderBrush!.Value.ToArgb());
            Assert.AreEqual(4, grid.ChildCount);
        }

        // ===========[ Helper Methods ]===================================
        // Sizes in pixels here, with the resizers 2 pixels thick, so the positions are the same on any density
        private static AutoGrid _Grid (int columns, int childCount)
        {
            var grid = new AutoGrid(TestHost.RequiredContext) { FixedColumnCount = columns, SizerLength = 2 };
            for (int index = 0; index < childCount; ++index)
            {
                grid.AddView(new View(TestHost.RequiredContext));
            }

            return grid;
        }

        private static View _Child (int height, int width = ViewGroup.LayoutParams.WrapContent)
        {
            return new View(TestHost.RequiredContext) { LayoutParameters = new ViewGroup.LayoutParams(width, height) };
        }

        private static void _LayOut (AutoGrid grid, int width, int height)
        {
            grid.Measure(
                View.MeasureSpec.MakeMeasureSpec(width, MeasureSpecMode.Exactly),
                View.MeasureSpec.MakeMeasureSpec(height, MeasureSpecMode.Exactly)
            );
            grid.Layout(0, 0, width, height);
        }

        private static void _Drag (AutoGrid grid, float fromX, float fromY, float toX, float toY)
        {
            long now = SystemClock.UptimeMillis();
            _Send(grid, now, MotionEventActions.Down, fromX, fromY);
            _Send(grid, now, MotionEventActions.Move, (fromX + toX) / 2, (fromY + toY) / 2);
            _Send(grid, now, MotionEventActions.Move, toX, toY);
            _Send(grid, now, MotionEventActions.Up, toX, toY);
        }

        private static void _Send (AutoGrid grid, long downTime, MotionEventActions action, float x, float y)
        {
            using MotionEvent motion = MotionEvent.Obtain(downTime, SystemClock.UptimeMillis(), action, x, y, 0)!;
            grid.DispatchTouchEvent(motion);
        }

        private static void _AssertBounds (View child, int left, int top, int width, int height)
        {
            Assert.AreEqual(left, child.Left, "left");
            Assert.AreEqual(top, child.Top, "top");
            Assert.AreEqual(width, child.Width, "width");
            Assert.AreEqual(height, child.Height, "height");
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class TouchCounter
        {
            public int DownCount { get; private set; }

            public void OnTouch (object? sender, View.TouchEventArgs e)
            {
                if (e.Event?.ActionMasked == MotionEventActions.Down)
                {
                    ++this.DownCount;
                }

                e.Handled = false;
            }
        }
    }
}
