namespace AJut.UX.Tests
{
    using System;
    using AJut.UX.Controls;
    using CoreGraphics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using UIKit;

    [TestClass]
    public class SimpleWrapPanelTests
    {
        private const float kChildWidth = 40f;
        private const float kChildHeight = 20f;

        [TestMethod]
        public void Layout_SubviewThatWouldRunPastTheWidth_StartsTheNextLine ()
        {
            // 40 + 8 + 40 = 88 fits in 100; another 8 + 40 does not
            SimpleWrapPanel panel = _Panel(3, width: 100);
            _AssertAt(panel.Subviews[0], 0, 0);
            _AssertAt(panel.Subviews[1], 48, 0);
            _AssertAt(panel.Subviews[2], 0, 26);
            Assert.AreEqual(46.0, (double)panel.SizeThatFits(new CGSize(100, nfloat.MaxValue)).Height, 0.01);
        }

        [TestMethod]
        public void Layout_HiddenSubview_TakesNoPlace ()
        {
            SimpleWrapPanel panel = _Panel(3, width: 100);
            panel.Subviews[1].Hidden = true;
            panel.SetNeedsLayout();
            panel.LayoutIfNeeded();
            _AssertAt(panel.Subviews[2], 48, 0);
        }

        [TestMethod]
        public void IntrinsicContentSize_IsTheHeightForTheWidthItHas ()
        {
            SimpleWrapPanel panel = _Panel(3, width: 100);
            Assert.AreEqual(UIView.NoIntrinsicMetric, panel.IntrinsicContentSize.Width);
            Assert.AreEqual(46.0, (double)panel.IntrinsicContentSize.Height, 0.01);
        }

        [TestMethod]
        public void SizeThatFits_WideEnough_IsOneLineOfTheSubviews ()
        {
            SimpleWrapPanel panel = _Panel(3, width: 100);
            CGSize size = panel.SizeThatFits(new CGSize(1000, nfloat.MaxValue));
            Assert.AreEqual((3 * kChildWidth) + (2 * 8), (double)size.Width, 0.01);
            Assert.AreEqual(kChildHeight, (double)size.Height, 0.01);
        }

        [TestMethod]
        public void Layout_RightToLeft_FlowsFromTheRight ()
        {
            SimpleWrapPanel panel = _Panel(2, width: 100);
            panel.SemanticContentAttribute = UISemanticContentAttribute.ForceRightToLeft;
            panel.SetNeedsLayout();
            panel.LayoutIfNeeded();
            _AssertAt(panel.Subviews[0], 100 - kChildWidth, 0);
            _AssertAt(panel.Subviews[1], 100 - kChildWidth - 8 - kChildWidth, 0);
        }

        // ===========[ Helper Methods ]===================================
        // A plain view's SizeThatFits is its own size, so each subview wants the frame it is given here
        private static SimpleWrapPanel _Panel (int subviewCount, float width)
        {
            var panel = new SimpleWrapPanel { HorizontalSpacing = 8, VerticalSpacing = 6, Frame = new CGRect(0, 0, width, 200) };
            for (int index = 0; index < subviewCount; ++index)
            {
                panel.AddSubview(new UIView(new CGRect(0, 0, kChildWidth, kChildHeight)));
            }

            panel.LayoutIfNeeded();
            return panel;
        }

        private static void _AssertAt (UIView subview, float x, float y)
        {
            Assert.AreEqual(x, (double)subview.Frame.X, 0.01, "x");
            Assert.AreEqual(y, (double)subview.Frame.Y, 0.01, "y");
            Assert.AreEqual(kChildWidth, (double)subview.Frame.Width, 0.01, "A subview keeps the size it wants");
        }
    }
}
