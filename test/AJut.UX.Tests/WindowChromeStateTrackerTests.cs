#nullable enable

namespace AJut.UX.Tests
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class WindowChromeStateTrackerTests
    {
        // ===[ Activation ]===

        [TestMethod]
        public void WCST_StartsActive_Normal_WithItsBorder ()
        {
            var chrome = new WindowChromeStateTracker();

            Assert.IsTrue(chrome.IsActivated);
            Assert.AreEqual(eWindowChromeState.Normal, chrome.State);
            Assert.IsFalse(chrome.IsHoveringOverClose);
            Assert.IsTrue(chrome.ShowsWindowBorder);
            Assert.AreEqual(eWindowState.Unknown, chrome.WindowState);
        }

        [TestMethod]
        public void WCST_Deactivating_IsInactive_AndReactivating_IsNormal ()
        {
            var chrome = new WindowChromeStateTracker();

            chrome.IsActivated = false;
            Assert.AreEqual(eWindowChromeState.Inactive, chrome.State);

            chrome.IsActivated = true;
            Assert.AreEqual(eWindowChromeState.Normal, chrome.State);
        }

        // ===[ System caption buttons ]===

        [TestMethod]
        public void WCST_SystemClose_IsCloseHover ()
        {
            var chrome = new WindowChromeStateTracker();

            chrome.SetSystemCaptionButtonPointer(eCaptionButton.Close, null);

            Assert.AreEqual(eWindowChromeState.CloseHover, chrome.State);
            Assert.IsTrue(chrome.IsHoveringOverClose);
            Assert.AreEqual(eCaptionButton.Close, chrome.HoveredCaptionButton);
        }

        [TestMethod]
        public void WCST_OtherSystemButtons_AreChromeButtonsHover ()
        {
            var chrome = new WindowChromeStateTracker();

            chrome.SetSystemCaptionButtonPointer(eCaptionButton.Minimize, null);
            Assert.AreEqual(eWindowChromeState.ChromeButtonsHover, chrome.State);

            chrome.SetSystemCaptionButtonPointer(eCaptionButton.Maximize, eCaptionButton.Maximize);
            Assert.AreEqual(eWindowChromeState.ChromeButtonsHover, chrome.State);
            Assert.AreEqual(eCaptionButton.Maximize, chrome.PressedCaptionButton);
            Assert.IsFalse(chrome.IsHoveringOverClose);
        }

        [TestMethod]
        public void WCST_LeavingTheSystemButtons_ReturnsToTheActivationLook ()
        {
            var chrome = new WindowChromeStateTracker { IsActivated = false };

            chrome.SetSystemCaptionButtonPointer(eCaptionButton.Close, null);
            chrome.SetSystemCaptionButtonPointer(null, null);

            Assert.AreEqual(eWindowChromeState.Inactive, chrome.State);
            Assert.IsFalse(chrome.IsHoveringOverClose);
            Assert.IsNull(chrome.HoveredCaptionButton);
        }

        // ===[ The title bar's own hover zones ]===

        [TestMethod]
        public void WCST_TitleBarZones_SetTheirStates ()
        {
            var chrome = new WindowChromeStateTracker();

            chrome.SetTitleBarHover(eChromeHover.Close);
            Assert.AreEqual(eWindowChromeState.CloseHover, chrome.State);
            Assert.IsTrue(chrome.IsHoveringOverClose);

            chrome.SetTitleBarHover(eChromeHover.ChromeButtons);
            Assert.AreEqual(eWindowChromeState.ChromeButtonsHover, chrome.State);
            Assert.IsFalse(chrome.IsHoveringOverClose);

            chrome.SetTitleBarHover(eChromeHover.None);
            Assert.AreEqual(eWindowChromeState.Normal, chrome.State);
        }

        [TestMethod]
        public void WCST_TitleBarZones_ArePassedOver_WhileASystemButtonHasThePointer ()
        {
            var chrome = new WindowChromeStateTracker();
            chrome.SetSystemCaptionButtonPointer(eCaptionButton.Minimize, null);

            chrome.SetTitleBarHover(eChromeHover.Close);
            chrome.SetTitleBarHover(eChromeHover.None);

            Assert.AreEqual(eWindowChromeState.ChromeButtonsHover, chrome.State);
            Assert.IsFalse(chrome.IsHoveringOverClose);
        }

        [TestMethod]
        public void WCST_LeavingTheWindow_ReturnsToTheActivationLook ()
        {
            var chrome = new WindowChromeStateTracker { IsActivated = false };
            chrome.SetTitleBarHover(eChromeHover.Close);

            chrome.ClearTitleBarHover();

            Assert.AreEqual(eWindowChromeState.Inactive, chrome.State);
            Assert.IsFalse(chrome.IsHoveringOverClose);
        }

        [TestMethod]
        public void WCST_LeavingTheWindow_ForASystemButton_KeepsItsState ()
        {
            var chrome = new WindowChromeStateTracker();
            chrome.SetSystemCaptionButtonPointer(eCaptionButton.Maximize, null);

            chrome.ClearTitleBarHover();

            Assert.AreEqual(eWindowChromeState.ChromeButtonsHover, chrome.State);
        }

        [TestMethod]
        public void WCST_ReleasingCloseHover_LeavesTheState ()
        {
            var chrome = new WindowChromeStateTracker();
            chrome.SetTitleBarHover(eChromeHover.ChromeButtons);

            chrome.ReleaseCloseHover();

            Assert.AreEqual(eWindowChromeState.ChromeButtonsHover, chrome.State);
            Assert.IsFalse(chrome.IsHoveringOverClose);
        }

        // ===[ Activation while hovering (APPKIT-34) ]===

        [TestMethod]
        public void WCST_ActivationChange_WhileOverClose_KeepsCloseHover ()
        {
            var chrome = new WindowChromeStateTracker();
            chrome.SetSystemCaptionButtonPointer(eCaptionButton.Close, null);

            chrome.IsActivated = false;

            Assert.AreEqual(eWindowChromeState.CloseHover, chrome.State, "The pointer is still on close, so the warning look stays");
        }

        [TestMethod]
        public void WCST_ActivationChange_WhileOverAChromeButton_KeepsChromeButtonsHover ()
        {
            var chrome = new WindowChromeStateTracker { IsActivated = false };
            chrome.SetTitleBarHover(eChromeHover.ChromeButtons);

            chrome.IsActivated = true;

            Assert.AreEqual(eWindowChromeState.ChromeButtonsHover, chrome.State);
        }

        [TestMethod]
        public void WCST_ActivationChange_WhileOverClose_KeepsCloseHoverInStep ()
        {
            var chrome = new WindowChromeStateTracker();
            chrome.SetTitleBarHover(eChromeHover.Close);

            chrome.IsActivated = false;

            Assert.AreEqual(chrome.State == eWindowChromeState.CloseHover, chrome.IsHoveringOverClose, "IsHoveringOverClose agrees with State");
        }

        [TestMethod]
        public void WCST_LeavingTheWindow_ForTheSystemClose_KeepsCloseHoverInStep ()
        {
            var chrome = new WindowChromeStateTracker();
            chrome.SetSystemCaptionButtonPointer(eCaptionButton.Close, null);

            chrome.ClearTitleBarHover();

            Assert.AreEqual(eWindowChromeState.CloseHover, chrome.State);
            Assert.IsTrue(chrome.IsHoveringOverClose, "IsHoveringOverClose agrees with State");
        }

        // ===[ Window state and the border ]===

        [TestMethod]
        public void WCST_Border_HidesWhenMaximizedOrFullscreen ()
        {
            var chrome = new WindowChromeStateTracker();

            chrome.WindowState = eWindowState.Maximized;
            Assert.IsFalse(chrome.ShowsWindowBorder);

            chrome.WindowState = eWindowState.Restored;
            Assert.IsTrue(chrome.ShowsWindowBorder);

            chrome.WindowState = eWindowState.FullScreened;
            Assert.IsFalse(chrome.ShowsWindowBorder);

            chrome.WindowState = eWindowState.Minimized;
            Assert.IsTrue(chrome.ShowsWindowBorder);
        }

        [TestMethod]
        public void WCST_UnknownWindowState_ChangesNothing ()
        {
            var chrome = new WindowChromeStateTracker { WindowState = eWindowState.Maximized };
            var changed = TrackProperties(chrome);

            chrome.WindowState = eWindowState.Unknown;

            Assert.AreEqual(eWindowState.Maximized, chrome.WindowState);
            Assert.IsFalse(chrome.ShowsWindowBorder);
            Assert.AreEqual(0, changed.Count);
        }

        // ===[ Picking for a state ]===

        [TestMethod]
        public void WCST_PickForState_FallsBackToNormal ()
        {
            Assert.AreEqual("n", WindowChromeStateTracker.PickForState(eWindowChromeState.Normal, "n", "i", "c"));
            Assert.AreEqual("i", WindowChromeStateTracker.PickForState(eWindowChromeState.Inactive, "n", "i", "c"));
            Assert.AreEqual("c", WindowChromeStateTracker.PickForState(eWindowChromeState.CloseHover, "n", "i", "c"));
            Assert.AreEqual("n", WindowChromeStateTracker.PickForState(eWindowChromeState.ChromeButtonsHover, "n", "i", "c"));
            Assert.AreEqual("n", WindowChromeStateTracker.PickForState(eWindowChromeState.CloseHover, "n", "i", null));
            Assert.AreEqual("n", WindowChromeStateTracker.PickForState(eWindowChromeState.Inactive, "n", null, "c"));
        }

        // ===[ Helpers ]===

        private static List<string> TrackProperties (WindowChromeStateTracker chrome)
        {
            var names = new List<string>();
            chrome.PropertyChanged += (sender, e) => names.Add(e.PropertyName!);
            return names;
        }
    }
}
