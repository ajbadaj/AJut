namespace AJut.UX.Tests
{
    using AJut.UX.Controls;
    using Android.Views;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SimpleWrapPanelTests
    {
        private const int kChildWidth = 40;
        private const int kChildHeight = 20;

        [TestMethod]
        public void Layout_ChildThatWouldRunPastTheWidth_StartsTheNextLine ()
        {
            // 40 + 8 + 40 = 88 fits in 100; another 8 + 40 does not
            SimpleWrapPanel panel = _Panel(3);
            _LayOut(panel, 100);
            _AssertAt(panel.GetChildAt(0)!, 0, 0);
            _AssertAt(panel.GetChildAt(1)!, 48, 0);
            _AssertAt(panel.GetChildAt(2)!, 0, 26);
            Assert.AreEqual(46, panel.MeasuredHeight, "Two lines of 20 with 6 between");
        }

        [TestMethod]
        public void Layout_GoneChild_TakesNoPlace ()
        {
            SimpleWrapPanel panel = _Panel(3);
            panel.GetChildAt(1)!.Visibility = ViewStates.Gone;
            _LayOut(panel, 100);
            _AssertAt(panel.GetChildAt(2)!, 48, 0);
            Assert.AreEqual(kChildHeight, panel.MeasuredHeight, "One line");
        }

        [TestMethod]
        public void Measure_UnboundedWidth_IsOneLine ()
        {
            SimpleWrapPanel panel = _Panel(3);
            int unspecified = View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified);
            panel.Measure(unspecified, unspecified);
            Assert.AreEqual((3 * kChildWidth) + (2 * 8), panel.MeasuredWidth);
            Assert.AreEqual(kChildHeight, panel.MeasuredHeight);
        }

        [TestMethod]
        public void Layout_Padding_InsetsTheFlow ()
        {
            SimpleWrapPanel panel = _Panel(2);
            panel.SetPadding(5, 7, 5, 7);
            _LayOut(panel, 100);
            _AssertAt(panel.GetChildAt(0)!, 5, 7);
            _AssertAt(panel.GetChildAt(1)!, 53, 7);
            Assert.AreEqual(kChildHeight + 14, panel.MeasuredHeight);
        }

        [TestMethod]
        public void Layout_RightToLeft_FlowsFromTheRight ()
        {
            SimpleWrapPanel panel = _Panel(2);
            panel.LayoutDirection = LayoutDirection.Rtl;
            _LayOut(panel, 100);
            _AssertAt(panel.GetChildAt(0)!, 100 - kChildWidth, 0);
            _AssertAt(panel.GetChildAt(1)!, 100 - kChildWidth - 8 - kChildWidth, 0);
        }

        [TestMethod]
        public void Panel_InflatesFromLayoutWithItsSpacing ()
        {
            View root = LayoutInflater.From(TestHost.RequiredContext)!.Inflate(Resource.Layout.simple_wrap_panel_test, null)!;
            var panel = root.FindViewById<SimpleWrapPanel>(Resource.Id.simple_wrap_panel)!;
            Assert.AreEqual(10f, panel.HorizontalSpacing);
            Assert.AreEqual(3f, panel.VerticalSpacing);
            Assert.AreEqual(2, panel.ChildCount);
        }

        // ===========[ Helper Methods ]===================================
        // Spacing in pixels here, so the positions are the same on any density
        private static SimpleWrapPanel _Panel (int childCount)
        {
            var panel = new SimpleWrapPanel(TestHost.RequiredContext) { HorizontalSpacing = 8, VerticalSpacing = 6 };
            for (int index = 0; index < childCount; ++index)
            {
                panel.AddView(new View(TestHost.RequiredContext) { LayoutParameters = new ViewGroup.LayoutParams(kChildWidth, kChildHeight) });
            }

            return panel;
        }

        private static void _LayOut (SimpleWrapPanel panel, int width)
        {
            panel.Measure(
                View.MeasureSpec.MakeMeasureSpec(width, MeasureSpecMode.Exactly),
                View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified)
            );
            panel.Layout(0, 0, width, panel.MeasuredHeight);
        }

        private static void _AssertAt (View child, int left, int top)
        {
            Assert.AreEqual(left, child.Left, "left");
            Assert.AreEqual(top, child.Top, "top");
            Assert.AreEqual(kChildWidth, child.Width, "A child keeps the size it wants");
        }
    }
}
