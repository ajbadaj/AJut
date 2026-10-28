namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;
    using AJut;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Graphics;
    using Android.Views;
    using Android.Widget;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using AndroidColor = Android.Graphics.Color;

    /// <summary>
    /// The Android ToggleStrip, EnumToggleStrip and EnumComboBox on real views. Their selection rules are AJut.UX's and
    /// are tested headless there; these cover the Android side: the buttons, taps, the source subscription's lifetime,
    /// layout attributes, and the binding adapters.
    /// </summary>
    [TestClass]
    public class ToggleStripTests
    {
        private const float kStripLengthDp = 150f;
        private const float kDotHostPaddingDp = 20f;
        private static readonly string[] kManyItems = new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight" };

        // ===========[ ToggleStrip ]===================================
        [TestMethod]
        public void ItemsSource_MakesOneButtonPerItem_AndSelectsTheFirst ()
        {
            var strip = new ToggleStrip(TestHost.RequiredContext) { ItemsSource = new[] { "A", "B", "C" } };
            using IDisposable attached = TestHost.Attach(strip);

            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, _ButtonTexts(strip));
            Assert.AreEqual("A", strip.SelectedItem);
        }

        [TestMethod]
        public void TappingAButton_SelectsItsItem_AndRaisesOneChange ()
        {
            var strip = new ToggleStrip(TestHost.RequiredContext) { ItemsSource = new[] { "A", "B", "C" } };
            using IDisposable attached = TestHost.Attach(strip);
            var changes = new List<ToggleStripSelectionChange>();
            strip.SelectionChanged += _OnChanged;

            _Buttons(strip)[1].PerformClick();
            strip.SelectionChanged -= _OnChanged;

            Assert.AreEqual("B", strip.SelectedItem);
            Assert.AreEqual(1, changes.Count);
            CollectionAssert.AreEqual(new[] { "A" }, changes[0].PreviousSelection);

            void _OnChanged (object? sender, ToggleStripSelectionChange e) => changes.Add(e);
        }

        [TestMethod]
        public void SettingSelectedItem_SelectsThatItem ()
        {
            var strip = new ToggleStrip(TestHost.RequiredContext) { ItemsSource = new[] { "A", "B", "C" } };
            strip.SelectedItem = "C";
            Assert.IsTrue(strip.Items[2].IsSelected);
            Assert.IsFalse(strip.Items[0].IsSelected);
        }

        [TestMethod]
        public void ObservableSource_InsertShowsAtItsIndex ()
        {
            var source = new ObservableCollection<string> { "A", "B" };
            var strip = new ToggleStrip(TestHost.RequiredContext) { ItemsSource = source };
            using IDisposable attached = TestHost.Attach(strip);

            source.Insert(1, "X");

            CollectionAssert.AreEqual(new[] { "A", "X", "B" }, _ButtonTexts(strip));
        }

        [TestMethod]
        public void Detached_TheSourceIsNotHeld_AndReattachCatchesUp ()
        {
            var source = new ObservableCollection<string> { "A", "B" };
            var strip = new ToggleStrip(TestHost.RequiredContext) { ItemsSource = source };
            IDisposable attached = TestHost.Attach(strip);
            attached.Dispose();

            source.Add("C");
            Assert.AreEqual(2, strip.Items.Count, "A detached strip is not listening");

            using IDisposable reattached = TestHost.Attach(strip);
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, _ButtonTexts(strip), "Attaching again catches up");
        }

        [TestMethod]
        public void InflatesFromLayoutWithItsAttributes ()
        {
            View root = LayoutInflater.From(TestHost.RequiredContext)!.Inflate(Resource.Layout.toggle_strip_test, null)!;
            var strip = root.FindViewById<ToggleStrip>(Resource.Id.toggle_strip)!;
            Assert.IsTrue(strip.AllowMultiSelect);
            Assert.AreEqual(eToggleStripOverflow.Scroll, strip.OverflowBehavior);
            CollectionAssert.AreEqual(new object[] { "Low", "Medium", "High" }, strip.Items.Select(item => item.Data).ToArray());
        }

        [TestMethod]
        public void Vertical_StacksTheButtonsTopToBottom_EachAsWideAsTheWidest ()
        {
            var strip = new ToggleStrip(TestHost.RequiredContext) { Orientation = Orientation.Vertical, ItemsSource = new[] { "A", "A much longer one", "C" } };
            using IDisposable attached = TestHost.Attach(strip);
            _LayOut(strip, MeasureSpecMode.Unspecified, 0, MeasureSpecMode.Unspecified, 0);

            List<TextView> buttons = _Buttons(strip);
            Assert.IsTrue(buttons[0].Top < buttons[1].Top && buttons[1].Top < buttons[2].Top, "The buttons run top to bottom");
            Assert.AreEqual(buttons[1].Width, buttons[0].Width, "A short button stretches to the widest");
            Assert.AreEqual(buttons[1].Width, buttons[2].Width, "A short button stretches to the widest");
            Assert.AreEqual(buttons[0].Left, buttons[2].Left, "Every button starts at the same left edge");
        }

        [TestMethod]
        public void Vertical_EnsureUniformSize_MakesEveryButtonAsTallAsTheTallest ()
        {
            var strip = new ToggleStrip(TestHost.RequiredContext) { Orientation = Orientation.Vertical, EnsureUniformSize = true, ItemsSource = new[] { "One", "Two\nlines\nhere", "Three" } };
            using IDisposable attached = TestHost.Attach(strip);
            _LayOut(strip, MeasureSpecMode.Unspecified, 0, MeasureSpecMode.Unspecified, 0);

            List<TextView> buttons = _Buttons(strip);
            Assert.AreEqual(buttons[1].Height, buttons[0].Height);
            Assert.AreEqual(buttons[1].Height, buttons[2].Height);
        }

        [TestMethod]
        public async Task Vertical_OverflowPopup_MovesWhatDoesNotFitItsHeight ()
        {
            var strip = new ToggleStrip(TestHost.RequiredContext) { Orientation = Orientation.Vertical, OverflowBehavior = eToggleStripOverflow.OverflowPopup, ItemsSource = new[] { "1", "2", "3", "4", "5", "6", "7" } };
            using IDisposable attached = TestHost.Attach(strip);
            _LayOut(strip, MeasureSpecMode.Unspecified, 0, MeasureSpecMode.Unspecified, 0);
            int oneButton = _Buttons(strip)[0].Height;

            // Room for three buttons along the height, and plenty across it. The height goes in the layout params too, so
            // the window's own layout pass keeps it while the test waits.
            strip.LayoutParameters = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, oneButton * 3);
            _LayOut(strip, MeasureSpecMode.Exactly, 400, MeasureSpecMode.Exactly, oneButton * 3);
            await _WaitUntilAsync(() => _Buttons(strip).Any(button => button.Visibility == ViewStates.Gone));

            List<TextView> buttons = _Buttons(strip);
            Assert.AreEqual(ViewStates.Visible, buttons[0].Visibility, "The selected first item stays in the strip");
            Assert.AreEqual(ViewStates.Gone, buttons[6].Visibility, "The last item does not fit the height");
            var overflowButton = (TextView)strip.GetChildAt(1)!;
            Assert.AreEqual(ViewStates.Visible, overflowButton.Visibility);
            Assert.AreEqual(GravityFlags.Bottom, ((FrameLayout.LayoutParams)overflowButton.LayoutParameters!).Gravity, "The overflow button sits along the bottom");
        }

        [TestMethod]
        public async Task MultiSelectOverflowing_AnItemPickedFromTheMenuComesIntoTheStrip ()
        {
            var strip = new ToggleStrip(TestHost.RequiredContext) { Orientation = Orientation.Vertical, AllowMultiSelect = true, OverflowBehavior = eToggleStripOverflow.OverflowPopup, ItemsSource = new[] { "1", "2", "3", "4", "5", "6", "7" } };
            using IDisposable attached = TestHost.Attach(strip);
            _LayOut(strip, MeasureSpecMode.Unspecified, 0, MeasureSpecMode.Unspecified, 0);
            int oneButton = _Buttons(strip)[0].Height;
            strip.LayoutParameters = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, oneButton * 4);
            _LayOut(strip, MeasureSpecMode.Exactly, 400, MeasureSpecMode.Exactly, oneButton * 4);
            strip.Items[1].IsSelected = true;
            strip.Items[2].IsSelected = true;
            await _WaitUntilAsync(() => _Buttons(strip)[6].Visibility == ViewStates.Gone);
            Assert.AreEqual(ViewStates.Gone, _Buttons(strip)[6].Visibility, "The seventh starts in the menu");

            // What a pick from the overflow menu does
            strip.Items[6].IsSelected = true;
            await _WaitUntilAsync(() => _Buttons(strip)[6].Visibility == ViewStates.Visible);

            Assert.AreEqual(ViewStates.Visible, _Buttons(strip)[6].Visibility, "The item just picked comes into the strip, ahead of the ones selected before it");
        }

        [TestMethod]
        public void Vertical_InflatesFromLayout_AndEnumToggleStripPassesItOn ()
        {
            View root = LayoutInflater.From(TestHost.RequiredContext)!.Inflate(Resource.Layout.toggle_strip_test, null)!;
            Assert.AreEqual(Orientation.Horizontal, root.FindViewById<ToggleStrip>(Resource.Id.toggle_strip)!.Orientation, "Horizontal is the default");
            Assert.AreEqual(Orientation.Vertical, root.FindViewById<ToggleStrip>(Resource.Id.toggle_strip_vertical)!.Orientation);
            Assert.AreEqual(Orientation.Vertical, root.FindViewById<EnumToggleStrip>(Resource.Id.enum_toggle_strip_vertical)!.Strip.Orientation);
        }

        // ===========[ Scroll mode end dots ]===================================
        [TestMethod]
        public async Task Scroll_ShowsADotPastEachEndItCanScrollToward ()
        {
            var host = _DotHost();
            var strip = new ToggleStrip(TestHost.RequiredContext) { OverflowBehavior = eToggleStripOverflow.Scroll, ItemsSource = kManyItems };
            host.AddView(strip, new FrameLayout.LayoutParams(_Dp(kStripLengthDp), ViewGroup.LayoutParams.WrapContent));
            using IDisposable attached = TestHost.Attach(host);
            await _WaitUntilAsync(() => strip.Width > 0);

            int centerY = strip.Top + (strip.Height / 2);
            using (Bitmap atStart = _Render(host))
            {
                Assert.AreEqual(0, _AlphaAt(atStart, strip.Left - _DotOffset(), centerY), "At the start there is nothing back to scroll to");
                Assert.IsTrue(_AlphaAt(atStart, strip.Right + _DotOffset(), centerY) > 0, "There is more past the trailing end");
            }

            // HorizontalScrollView clamps a scroll to its content, so this is the far end
            ((HorizontalScrollView)strip.GetChildAt(0)!).ScrollTo(int.MaxValue / 2, 0);
            using (Bitmap atEnd = _Render(host))
            {
                Assert.IsTrue(_AlphaAt(atEnd, strip.Left - _DotOffset(), centerY) > 0, "At the end there is more back toward the start");
                Assert.AreEqual(0, _AlphaAt(atEnd, strip.Right + _DotOffset(), centerY), "And nothing past the trailing end");
            }
        }

        [TestMethod]
        public async Task Scroll_Vertical_PutsTheDotsAboveAndBelow ()
        {
            var host = _DotHost();
            var strip = new ToggleStrip(TestHost.RequiredContext) { Orientation = Orientation.Vertical, OverflowBehavior = eToggleStripOverflow.Scroll, ItemsSource = kManyItems };
            host.AddView(strip, new FrameLayout.LayoutParams(_Dp(kStripLengthDp), _Dp(kStripLengthDp)));
            using IDisposable attached = TestHost.Attach(host);
            await _WaitUntilAsync(() => strip.Height > 0);

            int centerX = strip.Left + (strip.Width / 2);
            using Bitmap atStart = _Render(host);
            Assert.AreEqual(0, _AlphaAt(atStart, centerX, strip.Top - _DotOffset()), "Nothing above at the start");
            Assert.IsTrue(_AlphaAt(atStart, centerX, strip.Bottom + _DotOffset()) > 0, "More below the bottom");
        }

        [TestMethod]
        public async Task NotScrolling_ShowsNoDots ()
        {
            var host = _DotHost();
            var strip = new ToggleStrip(TestHost.RequiredContext) { ItemsSource = kManyItems };
            host.AddView(strip, new FrameLayout.LayoutParams(_Dp(kStripLengthDp), ViewGroup.LayoutParams.WrapContent));
            using IDisposable attached = TestHost.Attach(host);
            await _WaitUntilAsync(() => strip.Width > 0);

            using Bitmap rendered = _Render(host);
            int centerY = strip.Top + (strip.Height / 2);
            Assert.AreEqual(0, _AlphaAt(rendered, strip.Right + _DotOffset(), centerY), "A clipped strip shows no dot, even with more past its end");
        }

        // ===========[ EnumToggleStrip ]===================================
        [TestMethod]
        public void EnumToggleStrip_OffersTheSelectableValues_AndFollowsATap ()
        {
            var strip = new EnumToggleStrip(TestHost.RequiredContext) { SelectedItem = eSize.Small };
            using IDisposable attached = TestHost.Attach(strip);
            int raised = 0;
            strip.SelectedItemChanged += _OnChanged;

            CollectionAssert.AreEqual(new[] { "Small", "Medium", "Large" }, _ButtonTexts(strip.Strip), "The hidden value is left out");
            _Buttons(strip.Strip)[2].PerformClick();
            strip.SelectedItemChanged -= _OnChanged;

            Assert.AreEqual(eSize.Large, strip.SelectedItem);
            Assert.AreEqual(1, raised);

            void _OnChanged (object? sender, EventArgs<object?> e) => ++raised;
        }

        [TestMethod]
        public void EnumToggleStrip_MultiSelectFlags_CombinesTheSelection ()
        {
            var strip = new EnumToggleStrip(TestHost.RequiredContext) { AllowMultiSelect = true };
            strip.SelectedItem = eParts.Head | eParts.Tail;
            using IDisposable attached = TestHost.Attach(strip);

            CollectionAssert.AreEqual(new object[] { eParts.Head, eParts.Tail }, strip.SelectedItems.Cast<object>().ToArray());

            // The options are None, Head, Body and Tail: a [Flags] enum's None is offered too, the same as on WinUI3.
            _Buttons(strip.Strip)[2].PerformClick();
            Assert.AreEqual(eParts.Head | eParts.Body | eParts.Tail, strip.SelectedItem);
        }

        [TestMethod]
        public void EnumToggleStrip_MultiSelectFlags_PickingNoneTurnsTheRestOff ()
        {
            var strip = new EnumToggleStrip(TestHost.RequiredContext) { AllowMultiSelect = true };
            strip.SelectedItem = eParts.Head | eParts.Tail;
            using IDisposable attached = TestHost.Attach(strip);

            // None is the first option
            _Buttons(strip.Strip)[0].PerformClick();
            Assert.AreEqual(eParts.None, strip.SelectedItem);
            CollectionAssert.AreEqual(new object[] { eParts.None }, strip.SelectedItems.Cast<object>().ToArray(), "Only None is left picked");

            _Buttons(strip.Strip)[2].PerformClick();
            Assert.AreEqual(eParts.Body, strip.SelectedItem);
            CollectionAssert.AreEqual(new object[] { eParts.Body }, strip.SelectedItems.Cast<object>().ToArray(), "Picking a flag turns None off");
        }

        // ===========[ EnumComboBox ]===================================
        [TestMethod]
        public void EnumComboBox_OffersTheSelectableValues_AndShowsTheSelection ()
        {
            var comboBox = new EnumComboBox(TestHost.RequiredContext) { SelectedItem = eSize.Large };
            CollectionAssert.AreEqual(new Enum[] { eSize.Small, eSize.Medium, eSize.Large }, comboBox.Options.ToArray());
            Assert.AreEqual(2, comboBox.Spinner.SelectedItemPosition);
        }

        [TestMethod]
        public async Task EnumComboBox_APickInTheSpinner_SetsSelectedItem ()
        {
            var comboBox = new EnumComboBox(TestHost.RequiredContext) { SelectedItem = eSize.Small };
            using IDisposable attached = TestHost.Attach(comboBox);
            await Task.Delay(50);

            comboBox.Spinner.SetSelection(1);
            await _WaitUntilAsync(() => Equals(comboBox.SelectedItem, eSize.Medium));
            Assert.AreEqual(eSize.Medium, comboBox.SelectedItem);
        }

        // ===========[ Binding ]===================================
        [TestMethod]
        public async Task BindSelectedValue_EnumComboBox_BothWays ()
        {
            var model = new Model();
            var comboBox = new EnumComboBox(TestHost.RequiredContext);
            using IDisposable attached = TestHost.Attach(comboBox);
            using var bindings = new BindingSet();
            bindings.BindSelectedValue(comboBox, model, nameof(Model.Size), () => model.Size, v => model.Size = v);
            Assert.AreEqual(eSize.Medium, comboBox.SelectedItem);

            model.Size = eSize.Large;
            Assert.AreEqual(eSize.Large, comboBox.SelectedItem);

            comboBox.Spinner.SetSelection(0);
            await _WaitUntilAsync(() => model.Size == eSize.Small);
            Assert.AreEqual(eSize.Small, model.Size);
        }

        [TestMethod]
        public void BindSelectedValue_EnumToggleStrip_BothWays ()
        {
            var model = new Model();
            var strip = new EnumToggleStrip(TestHost.RequiredContext);
            using IDisposable attached = TestHost.Attach(strip);
            using var bindings = new BindingSet();
            bindings.BindSelectedValue(strip, model, nameof(Model.Size), () => model.Size, v => model.Size = v);
            Assert.AreEqual(eSize.Medium, strip.SelectedItem);

            _Buttons(strip.Strip)[0].PerformClick();
            Assert.AreEqual(eSize.Small, model.Size);
        }

        [TestMethod]
        public void BindSelectedItem_ToggleStrip_BothWays ()
        {
            var model = new Model();
            var strip = new ToggleStrip(TestHost.RequiredContext) { ItemsSource = new[] { "A", "B", "C" } };
            using IDisposable attached = TestHost.Attach(strip);
            using var bindings = new BindingSet();
            bindings.BindSelectedItem(strip, model, nameof(Model.Letter), () => model.Letter, v => model.Letter = (string?)v);
            Assert.AreEqual("B", strip.SelectedItem);

            _Buttons(strip)[2].PerformClick();
            Assert.AreEqual("C", model.Letter);
        }

        // ===========[ Helper Methods ]===================================
        private static List<TextView> _Buttons (ToggleStrip strip)
        {
            // The strip is a scroller around a row of item buttons, with the overflow button beside it.
            var row = (ViewGroup)((ViewGroup)strip.GetChildAt(0)!).GetChildAt(0)!;
            var buttons = new List<TextView>();
            for (int index = 0; index < row.ChildCount; ++index)
            {
                buttons.Add((TextView)row.GetChildAt(index)!);
            }

            return buttons;
        }

        private static string[] _ButtonTexts (ToggleStrip strip) => _Buttons(strip).Select(button => button.Text ?? string.Empty).ToArray();

        // Runs a measure and layout pass on the strip directly, so the tests read real positions and sizes
        private static void _LayOut (View view, MeasureSpecMode widthMode, int width, MeasureSpecMode heightMode, int height)
        {
            view.Measure(View.MeasureSpec.MakeMeasureSpec(width, widthMode), View.MeasureSpec.MakeMeasureSpec(height, heightMode));
            view.Layout(0, 0, view.MeasuredWidth, view.MeasuredHeight);
        }

        // The dots draw in the strip's parent's overlay, past the strip's ends, so the strip sits inside a padded parent
        // that has room for them, and the parent is what gets rendered.
        private static FrameLayout _DotHost ()
        {
            var host = new FrameLayout(TestHost.RequiredContext);
            int padding = _Dp(kDotHostPaddingDp);
            host.SetPadding(padding, padding, padding, padding);
            return host;
        }

        private static Bitmap _Render (View view)
        {
            Bitmap bitmap = Bitmap.CreateBitmap(view.Width, view.Height, Bitmap.Config.Argb8888!)!;
            using (var canvas = new Canvas(bitmap))
            {
                view.Draw(canvas);
            }

            return bitmap;
        }

        private static int _AlphaAt (Bitmap bitmap, int x, int y) => new AndroidColor(bitmap.GetPixel(x, y)).A;

        private static int _DotOffset () => _Dp((float)ToggleStripScrollIndicators.kDotCenterOffset);

        private static int _Dp (float dp) => (int)Math.Round(dp * TestHost.RequiredContext.Resources!.DisplayMetrics!.Density);

        private static async Task _WaitUntilAsync (Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (!condition() && (DateTime.UtcNow < deadline))
            {
                await Task.Delay(10);
            }
        }

        // ===========[ Subclasses/structs ]===================================
        public enum eSize
        {
            Small,
            [System.ComponentModel.Browsable(false)]
            Unlisted,
            Medium,
            Large,
        }

        [Flags]
        public enum eParts
        {
            None = 0,
            Head = 1,
            Body = 2,
            Tail = 4,
        }

        private sealed class Model : INotifyPropertyChanged
        {
            private eSize m_size = eSize.Medium;
            private string? m_letter = "B";

            public event PropertyChangedEventHandler? PropertyChanged;

            public eSize Size
            {
                get => m_size;
                set
                {
                    if (m_size != value)
                    {
                        m_size = value;
                        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Size)));
                    }
                }
            }

            public string? Letter
            {
                get => m_letter;
                set
                {
                    if (m_letter != value)
                    {
                        m_letter = value;
                        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Letter)));
                    }
                }
            }
        }
    }
}
