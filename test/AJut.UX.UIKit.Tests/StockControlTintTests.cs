namespace AJut.UX.Tests
{
    using AJut.UX.Theming;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using UIKit;

    /// <summary>
    /// What StockControlTint sets and what it leaves alone. The colors themselves are looks, judged by eye, so nothing here
    /// checks a value: only that the switch appearance and a window's tint take the AJut colors with the theme loaded, and
    /// go back or stay put without it. UISwitch's appearance is app wide, so every test puts it back.
    /// </summary>
    [TestClass]
    public class StockControlTintTests
    {
        private UIColor m_switchOnTintAtStart;

        [TestInitialize]
        public void Setup ()
        {
            m_switchOnTintAtStart = UISwitch.Appearance.OnTintColor;
        }

        [TestCleanup]
        public void Teardown ()
        {
            ThemedColors.IsThemeLoaded = false;
            UISwitch.Appearance.OnTintColor = m_switchOnTintAtStart;
        }

        [TestMethod]
        public void StockControlTint_Themed_GivesSwitchesTheAJutOnColor ()
        {
            ThemedColors.IsThemeLoaded = true;

            StockControlTint.Apply();

            Assert.IsNotNull(UISwitch.Appearance.OnTintColor);
        }

        [TestMethod]
        public void StockControlTint_Unthemed_GivesSwitchesBackTheSystemsColor ()
        {
            ThemedColors.IsThemeLoaded = true;
            StockControlTint.Apply();

            ThemedColors.IsThemeLoaded = false;
            StockControlTint.Apply();

            Assert.IsNull(UISwitch.Appearance.OnTintColor);
        }

        [TestMethod]
        public void StockControlTint_Themed_TintsAWindowWithTheAccent ()
        {
            ThemedColors.IsThemeLoaded = true;
            var window = new UIWindow(TestAppScene);

            StockControlTint.ApplyTo(window);

            Assert.AreEqual(StockControlTint.Accent, window.TintColor);
        }

        [TestMethod]
        public void StockControlTint_Unthemed_LeavesAWindowsTintAsTheAppSetIt ()
        {
            ThemedColors.IsThemeLoaded = false;
            var window = new UIWindow(TestAppScene) { TintColor = UIColor.SystemPink };

            StockControlTint.ApplyTo(window);

            Assert.AreEqual(UIColor.SystemPink, window.TintColor);
        }

        // ===[ Helpers ]===

        private static UIWindowScene TestAppScene => TestHost.TestArea?.Window?.WindowScene ?? throw new System.InvalidOperationException("Tests only run inside the test app.");
    }
}
