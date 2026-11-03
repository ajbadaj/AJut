namespace AJut.UX.Tests
{
    using System.Linq;
    using AJut.UX.Controls;
    using Android.Graphics.Drawables;
    using Android.Views;
    using Android.Widget;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BusyWaitTests
    {
        // ===[ Inline ]===

        [TestMethod]
        public void BusyWait_SpinsByDefault ()
        {
            var busy = new BusyWait(TestHost.RequiredContext);

            Assert.IsTrue(busy.IsSpinning);
            Assert.AreEqual(ViewStates.Visible, SpinnerOf(busy).Visibility);
        }

        [TestMethod]
        public void BusyWait_Stopped_HidesTheSpinner_ButKeepsItsRoom ()
        {
            var busy = new BusyWait(TestHost.RequiredContext) { Text = "Loading" };

            busy.IsSpinning = false;

            Assert.AreEqual(ViewStates.Invisible, SpinnerOf(busy).Visibility, "Invisible, not gone, so the text does not move when it spins again");
            Assert.AreEqual(ViewStates.Visible, TextOf(busy).Visibility);
        }

        [TestMethod]
        public void BusyWait_Inline_TakesNoTouches ()
        {
            var busy = new BusyWait(TestHost.RequiredContext);

            Assert.IsFalse(busy.Clickable);
        }

        [TestMethod]
        public void BusyWait_NoText_ShowsTheSpinnerAlone ()
        {
            var busy = new BusyWait(TestHost.RequiredContext);

            Assert.AreEqual(ViewStates.Gone, TextOf(busy).Visibility);
        }

        [TestMethod]
        public void BusyWait_SpinnerDock_PutsTheSpinnerOnThatSide ()
        {
            var busy = new BusyWait(TestHost.RequiredContext) { Text = "Loading" };
            LinearLayout row = RowOf(busy);

            busy.SpinnerDock = eBusyWaitSpinnerDock.Left;
            Assert.AreEqual(Orientation.Horizontal, row.Orientation);
            Assert.IsInstanceOfType(row.GetChildAt(0), typeof(ProgressBar));

            busy.SpinnerDock = eBusyWaitSpinnerDock.Right;
            Assert.AreEqual(Orientation.Horizontal, row.Orientation);
            Assert.IsInstanceOfType(row.GetChildAt(1), typeof(ProgressBar));

            busy.SpinnerDock = eBusyWaitSpinnerDock.Top;
            Assert.AreEqual(Orientation.Vertical, row.Orientation);
            Assert.IsInstanceOfType(row.GetChildAt(0), typeof(ProgressBar));

            busy.SpinnerDock = eBusyWaitSpinnerDock.Bottom;
            Assert.AreEqual(Orientation.Vertical, row.Orientation);
            Assert.IsInstanceOfType(row.GetChildAt(1), typeof(ProgressBar));
        }

        // ===[ Overlay ]===

        [TestMethod]
        public void BusyWait_Overlay_Spinning_CoversAndTakesTouches ()
        {
            var busy = new BusyWait(TestHost.RequiredContext) { IsOverlay = true };

            Assert.IsTrue(busy.Clickable, "A clickable view keeps the touches that land on it from what is under it");
            Assert.IsInstanceOfType(busy.Background, typeof(ColorDrawable));
            Assert.AreEqual(ViewStates.Visible, RowOf(busy).Visibility);
        }

        [TestMethod]
        public void BusyWait_Overlay_Stopped_DrawsNothing_AndLetsTouchesThrough ()
        {
            var busy = new BusyWait(TestHost.RequiredContext) { IsOverlay = true };

            busy.IsSpinning = false;

            Assert.IsFalse(busy.Clickable);
            Assert.IsNull(busy.Background);
            Assert.AreEqual(ViewStates.Invisible, RowOf(busy).Visibility);
        }

        [TestMethod]
        public void BusyWait_BackToInline_TakesTheOverlaysFillOff ()
        {
            var busy = new BusyWait(TestHost.RequiredContext) { IsOverlay = true };

            busy.IsOverlay = false;

            Assert.IsFalse(busy.Clickable);
            Assert.IsNull(busy.Background);
        }

        [TestMethod]
        public void BusyWait_Overlay_Stopped_GivesBackWhatItReplaced ()
        {
            var busy = new BusyWait(TestHost.RequiredContext);
            var appFill = new ColorDrawable(Android.Graphics.Color.Purple);
            busy.Background = appFill;

            busy.IsOverlay = true;
            Assert.AreNotSame(appFill, busy.Background, "Spinning, the overlay shows its own fill");
            Assert.IsTrue(busy.TranslationZ > 0f, "Raised over siblings Android would otherwise draw and hand touches to first");

            busy.IsSpinning = false;
            Assert.AreSame(appFill, busy.Background);
            Assert.AreEqual(0f, busy.TranslationZ);
        }

        [TestMethod]
        public void BusyWait_Unthemed_Inline_LeavesTheSpinnersOwnTint ()
        {
            AJut.UX.Theming.ThemeColors.IsThemeLoaded = false;

            var busy = new BusyWait(TestHost.RequiredContext);

            Assert.IsNull(SpinnerOf(busy).IndeterminateTintList, "Setting any tint, even none, replaces the one the spinner's drawable carries");
        }

        [TestMethod]
        public void BusyWait_OverlayBackToInline_Unthemed_GivesTheSpinnerTheThemesAccent ()
        {
            AJut.UX.Theming.ThemeColors.IsThemeLoaded = false;
            var busy = new BusyWait(TestHost.RequiredContext) { IsOverlay = true };

            busy.IsOverlay = false;

            Assert.IsNotNull(SpinnerOf(busy).IndeterminateTintList, "Once tinted, the spinner goes back to the theme's colorControlActivated, not to no tint");
        }

        [TestMethod]
        public void BusyWait_Inline_KeepsABackgroundTheAppGaveIt ()
        {
            var busy = new BusyWait(TestHost.RequiredContext);
            var appFill = new ColorDrawable(Android.Graphics.Color.Purple);
            busy.Background = appFill;

            busy.IsSpinning = false;
            busy.IsSpinning = true;

            Assert.AreSame(appFill, busy.Background);
        }

        // ===[ Layout ]===

        [TestMethod]
        public void BusyWait_FromALayout_TakesItsAttributes ()
        {
            View root = LayoutInflater.From(TestHost.RequiredContext)!.Inflate(Resource.Layout.busy_wait_test, null)!;
            var busy = root.FindViewById<BusyWait>(Resource.Id.busy_wait)!;

            Assert.AreEqual("Saving", busy.Text);
            Assert.IsFalse(busy.IsSpinning);
            Assert.IsTrue(busy.IsOverlay);
            Assert.AreEqual(eBusyWaitSpinnerDock.Top, busy.SpinnerDock);
            Assert.AreEqual(40f, busy.SpinnerSize, 0.5f);
        }

        // ===[ Helpers ]===

        private static LinearLayout RowOf (BusyWait busy) => (LinearLayout)busy.GetChildAt(0)!;

        private static ProgressBar SpinnerOf (BusyWait busy) => Children(RowOf(busy)).OfType<ProgressBar>().Single();

        private static TextView TextOf (BusyWait busy) => Children(RowOf(busy)).OfType<TextView>().Single();

        private static System.Collections.Generic.IEnumerable<View> Children (ViewGroup group)
        {
            for (int index = 0; index < group.ChildCount; ++index)
            {
                yield return group.GetChildAt(index)!;
            }
        }
    }
}
