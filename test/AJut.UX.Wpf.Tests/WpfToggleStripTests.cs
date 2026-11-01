namespace AJut.UX.Tests
{
    using System.Collections;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Media;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The WPF ToggleStrip's item buttons, realized from its default template: each button draws its share of the strip's
    /// separators and rounded corners. WPF objects need an STA thread, so each test runs on one, and nothing is ever shown.
    /// </summary>
    [TestClass]
    public class WpfToggleStripTests
    {
        private const double kSeparator = 2.0;
        private static readonly Size kMeasureSize = new Size(600, 600);
        private static readonly CornerRadius kStripCorners = new CornerRadius(4);

        // ===========[ Item edges follow the items ]=========================
        [TestMethod]
        public void WpfToggleStrip_ItemAddedAfterTheLast_TheOldLastGetsItsSeparatorAndSquareEnd ()
        {
            StaTestThread.Run(() =>
            {
                var source = new ObservableCollection<string> { "A" };
                ToggleStrip strip = MakeStrip(source);
                StackPanel host = MakeHost(strip);
                Assert.AreEqual(new Thickness(0), InnerBorderOf(strip, "A").BorderThickness, "An only item draws no separator");
                Assert.AreEqual(eCorner.All, RoundedCornersOf(strip, "A"), "An only item takes all of the strip's corners");

                source.Add("B");
                LayOut(host);

                Assert.AreEqual(new Thickness(kSeparator, 0, 0, 0), InnerBorderOf(strip, "B").BorderThickness, "B draws the separator between them, on its leading edge");
                Assert.AreEqual(new Thickness(0), InnerBorderOf(strip, "A").BorderThickness, "The first item draws no separator");
                Assert.AreEqual(eCorner.TopLeft | eCorner.BottomLeft, RoundedCornersOf(strip, "A"), "Once B follows it, A only rounds its left end");
            });
        }

        [TestMethod]
        public void WpfToggleStrip_LastItemRemoved_TheItemLeftTakesAllTheCorners ()
        {
            StaTestThread.Run(() =>
            {
                ToggleStrip strip = MakeStrip(new[] { "A", "B" });
                StackPanel host = MakeHost(strip);
                Assert.AreEqual(eCorner.TopLeft | eCorner.BottomLeft, RoundedCornersOf(strip, "A"), "With B after it, A only rounds its left end");

                // Removed through the strip's own items: WPF's ItemsSource remove does not reach them yet (it looks for
                // the removed data among the items, which never matches), and that is a separate defect.
                strip.Items.RemoveAt(1);
                LayOut(host);

                Assert.AreEqual(new Thickness(0), InnerBorderOf(strip, "A").BorderThickness, "With B gone, A is the only item and draws no separator");
                Assert.AreEqual(eCorner.All, RoundedCornersOf(strip, "A"), "With B gone, A takes all of the strip's corners");
            });
        }

        // ===========[ The selected look ]=====================================
        [TestMethod]
        public void WpfEnumToggleStrip_Themed_SelectedItemLooksLikeAToggleStrips ()
        {
            StaTestThread.Run(() =>
            {
                StackPanel host = MakeThemedHost();
                ToggleStrip strip = MakeStrip(new[] { "A", "B" });
                var enumStrip = new EnumToggleStrip { SelectedItem = eSample.First };
                host.Children.Add(strip);
                host.Children.Add(enumStrip);
                LayOut(host);

                Color[] expected = SelectedStopColors(strip, "A");
                Color[] actual = SelectedStopColors(enumStrip.PART_ToggleStrip, eSample.First);
                CollectionAssert.AreEqual(
                    expected,
                    actual,
                    $"A themed EnumToggleStrip's selected item takes the same pressed-in gradient as a themed ToggleStrip's: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]"
                );
            });
        }

        // ===========[ Corners and the overflow edge ]=========================
        [TestMethod]
        public void WpfToggleStrip_Overflowing_LastShownItemDrawsATrailingSeparatorAndStaysSquare ()
        {
            StaTestThread.Run(() =>
            {
                ToggleStrip strip = MakeStrip(new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven" });
                strip.OverflowBehavior = eToggleStripOverflow.OverflowPopup;
                strip.Width = 150;
                strip.HorizontalAlignment = HorizontalAlignment.Left;
                MakeHost(strip);
                Assert.IsTrue(strip.HasOverflow, "Seven items do not fit 150 wide");

                string lastShown = strip.Items.Last(item => !item.IsInOverflow).Data as string;
                Assert.AreNotEqual("One", lastShown, "More than one item fits");
                Assert.AreEqual(kSeparator, InnerBorderOf(strip, lastShown).BorderThickness.Right, "The last item showing is separated from the overflow button");
                Assert.AreEqual(eCorner.None, RoundedCornersOf(strip, lastShown), "The strip's border rounds the overflow button's end, not the last item showing");
                Assert.AreEqual(eCorner.TopLeft | eCorner.BottomLeft, RoundedCornersOf(strip, "One"), "The first item still rounds the strip's left end");
            });
        }

        [TestMethod]
        public void WpfToggleStrip_Overflowing_PopupItemsAreASquareListSeparatedTopToBottom ()
        {
            StaTestThread.Run(() =>
            {
                ToggleStrip strip = MakeStrip(new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven" });
                strip.OverflowBehavior = eToggleStripOverflow.OverflowPopup;
                strip.Width = 150;
                strip.HorizontalAlignment = HorizontalAlignment.Left;
                MakeHost(strip);

                ToggleStrip.ToggleItem[] inPopup = strip.Items.Where(item => item.IsInOverflow).ToArray();
                Assert.IsTrue(inPopup.Length > 1, "More than one item overflows");
                Assert.AreEqual(default(ToggleStripItemEdges), inPopup[0].Edges, "The first in the popup has no separator and square corners");
                Assert.AreEqual(new ToggleStripItemEdges(false, true, false, false, false, false, false, false), inPopup[1].Edges, "The rest are separated along the top, and square");
            });
        }

        [TestMethod]
        public void WpfToggleStrip_MultiSelectOverflowing_AnItemPickedFromThePopupComesIntoTheStrip ()
        {
            StaTestThread.Run(() =>
            {
                ToggleStrip strip = MakeStrip(new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven" });
                strip.AllowMultiSelect = true;
                strip.OverflowBehavior = eToggleStripOverflow.OverflowPopup;
                strip.Width = 150;
                strip.HorizontalAlignment = HorizontalAlignment.Left;
                StackPanel host = MakeHost(strip);
                strip.Items[1].IsSelected = true;
                strip.Items[2].IsSelected = true;
                LayOut(host);
                Assert.IsTrue(strip.Items[6].IsInOverflow, "Seven starts in the popup");

                // What the popup's button does when it is clicked
                strip.Items[6].IsSelected = true;
                LayOut(host);

                Assert.IsFalse(strip.Items[6].IsInOverflow, "The item just picked comes into the strip, ahead of the ones selected before it");
            });
        }

        // ===========[ Orientation ]===========================================
        [TestMethod]
        public void WpfToggleStrip_Vertical_StacksTopToBottom_EachAsWideAsTheWidest ()
        {
            StaTestThread.Run(() =>
            {
                ToggleStrip strip = MakeStrip(new[] { "A", "A much longer one", "C" });
                strip.Orientation = Orientation.Vertical;
                MakeHost(strip);

                Point[] tops = new[] { "A", "A much longer one", "C" }.Select(data => FindButton(strip, data).TranslatePoint(new Point(0, 0), strip)).ToArray();
                Assert.IsTrue((tops[0].Y < tops[1].Y) && (tops[1].Y < tops[2].Y), "The buttons run top to bottom");
                Assert.AreEqual(tops[0].X, tops[2].X, "Every button starts at the same left edge");
                Assert.AreEqual(FindButton(strip, "A much longer one").ActualWidth, FindButton(strip, "A").ActualWidth, "A short button stretches to the widest");
            });
        }

        [TestMethod]
        public void WpfToggleStrip_OrientationChangedAfterLayout_TheEdgesFollow ()
        {
            StaTestThread.Run(() =>
            {
                ToggleStrip strip = MakeStrip(new[] { "A", "B" });
                StackPanel host = MakeHost(strip);
                Assert.AreEqual(new Thickness(kSeparator, 0, 0, 0), InnerBorderOf(strip, "B").BorderThickness);

                strip.Orientation = Orientation.Vertical;
                LayOut(host);

                Assert.AreEqual(new Thickness(0, kSeparator, 0, 0), InnerBorderOf(strip, "B").BorderThickness, "The separator moves to the top");
                Assert.AreEqual(eCorner.TopLeft | eCorner.TopRight, RoundedCornersOf(strip, "A"), "The rounded end moves to the top");
            });
        }

        [TestMethod]
        public void WpfToggleStrip_Vertical_OverflowPopup_MovesWhatDoesNotFitItsHeight ()
        {
            StaTestThread.Run(() =>
            {
                ToggleStrip strip = MakeStrip(new[] { "1", "2", "3", "4", "5", "6", "7" });
                strip.Orientation = Orientation.Vertical;
                strip.OverflowBehavior = eToggleStripOverflow.OverflowPopup;
                strip.Height = 90;
                MakeHost(strip);

                Assert.IsTrue(strip.HasOverflow, "Seven buttons do not fit 90 tall");
                Assert.IsFalse(strip.Items[0].IsInOverflow, "The selected first item stays in the strip");
                Assert.IsTrue(strip.Items[6].IsInOverflow, "The last item moves to the popup");
            });
        }

        // ===========[ Helpers ]===============================================
        private static ToggleStrip MakeStrip (IEnumerable itemsSource)
        {
            return new ToggleStrip
            {
                ItemsSource = itemsSource,
                SeparatorThickness = kSeparator,
                CornerRadius = kStripCorners,
            };
        }

        // A measure and arrange applies the strip's template and makes its item buttons
        private static StackPanel MakeHost (FrameworkElement strip)
        {
            var host = new StackPanel();
            host.Children.Add(strip);
            LayOut(host);
            return host;
        }

        // The AJut theme as an app merges it: the dark palette, then the themed control styles over it
        private static StackPanel MakeThemedHost ()
        {
            var host = new StackPanel();
            host.Resources.MergedDictionaries.Add(LoadAJutResources("Resources/DarkThemeColorsBase.xaml"));
            host.Resources.MergedDictionaries.Add(LoadAJutResources("Resources/ThemedControlStylesBase.xaml"));
            return host;
        }

        // UpdateLayout runs the layout WPF has queued for anything invalidated inside (as an app's dispatcher would), which a
        // second Measure of the host at the same size would skip
        private static void LayOut (FrameworkElement host)
        {
            host.Measure(kMeasureSize);
            host.Arrange(new Rect(kMeasureSize));
            host.UpdateLayout();
        }

        // The item template's inner border draws the separator, its outer border the rounded corners
        private static Border InnerBorderOf (ToggleStrip strip, string data) => TemplatePartOf(strip, data, "InnerBorder");
        private static Border RootBorderOf (ToggleStrip strip, string data) => TemplatePartOf(strip, data, "RootBorder");

        private static Border TemplatePartOf (ToggleStrip strip, string data, string partName)
        {
            ToggleButton button = FindButton(strip, data);
            Assert.IsNotNull(button, $"No button was made for {data}");
            return (Border)button.Template.FindName(partName, button);
        }

        // Which of an item's corners its template rounds. How far it rounds them is a look, so only rounded against square
        // is told apart.
        private static eCorner RoundedCornersOf (ToggleStrip strip, string data)
        {
            CornerRadius corners = RootBorderOf(strip, data).CornerRadius;
            return ((corners.TopLeft > 0) ? eCorner.TopLeft : eCorner.None)
                | ((corners.TopRight > 0) ? eCorner.TopRight : eCorner.None)
                | ((corners.BottomRight > 0) ? eCorner.BottomRight : eCorner.None)
                | ((corners.BottomLeft > 0) ? eCorner.BottomLeft : eCorner.None);
        }

        // The colors of the selected item's background, the IsChecked trigger's gradient
        private static Color[] SelectedStopColors (ToggleStrip strip, object data)
        {
            ToggleButton button = FindButton(strip, data);
            Assert.IsNotNull(button, $"No button was made for {data}");
            Assert.IsTrue(button.IsChecked == true, $"{data} is selected");
            var root = (Border)button.Template.FindName("RootBorder", button);
            var gradient = root.Background as LinearGradientBrush;
            Assert.IsNotNull(gradient, "A selected item's background is the pressed gradient");
            return gradient.GradientStops.Select(stop => stop.Color).ToArray();
        }

        private static ResourceDictionary LoadAJutResources (string path)
        {
            return (ResourceDictionary)Application.LoadComponent(new System.Uri($"/AJut.UX.Wpf;component/{path}", System.UriKind.Relative));
        }

        private static ToggleButton FindButton (DependencyObject parent, object data)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); ++index)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if ((child is ToggleButton button) && (button.DataContext is ToggleStrip.ToggleItem item) && Equals(item.Data, data))
                {
                    return button;
                }

                ToggleButton found = FindButton(child, data);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // ===========[ Subclasses/structs ]====================================
        public enum eSample
        {
            First,
            Second,
        }

        /// <summary>
        /// An item button's corners, to say which of them it rounds
        /// </summary>
        [System.Flags]
        private enum eCorner
        {
            None = 0,
            TopLeft = 1,
            TopRight = 2,
            BottomRight = 4,
            BottomLeft = 8,
            All = TopLeft | TopRight | BottomRight | BottomLeft,
        }
    }
}
