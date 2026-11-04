namespace AJut.UX.Tests
{
    using System;
    using AJut.UX.Theming;
    using Foundation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using UIKit;

    [TestClass]
    public class AppThemeManagerTests
    {
        // Where AppThemeManager keeps a choice in NSUserDefaults, cleared around every test here
        private const string kStoreKey = "AJut_AppThemeConfiguration";

        private UIWindow m_window;
        private UIUserInterfaceStyle m_testAppStyle;

        [TestInitialize]
        public void Setup ()
        {
            NSUserDefaults.StandardUserDefaults.RemoveObject(kStoreKey);

            // A manager sets every window in the app, the test app's own included, so its style is put back after
            m_testAppStyle = TestAppWindow.OverrideUserInterfaceStyle;
        }

        [TestCleanup]
        public void Teardown ()
        {
            if (m_window != null)
            {
                m_window.Hidden = true;
                m_window.Dispose();
                m_window = null;
            }

            TestAppWindow.OverrideUserInterfaceStyle = m_testAppStyle;
            NSUserDefaults.StandardUserDefaults.RemoveObject(kStoreKey);
        }

        // ===[ Applying ]===

        [TestMethod]
        public void AppThemeManager_DarkChoice_PinsTheWindowsDark ()
        {
            using var manager = new AppThemeManager(eAppThemeConfiguration.Dark, persistInUserDefaults: false);

            Assert.AreEqual(UIUserInterfaceStyle.Dark, TestAppWindow.OverrideUserInterfaceStyle);
        }

        [TestMethod]
        public void AppThemeManager_LightChoice_PinsTheWindowsLight ()
        {
            using var manager = new AppThemeManager(eAppThemeConfiguration.Light, persistInUserDefaults: false);

            Assert.AreEqual(UIUserInterfaceStyle.Light, TestAppWindow.OverrideUserInterfaceStyle);
        }

        [TestMethod]
        public void AppThemeManager_UseSameAsOS_LetsTheWindowsFollowTheSystem ()
        {
            TestAppWindow.OverrideUserInterfaceStyle = UIUserInterfaceStyle.Dark;

            using var manager = new AppThemeManager(eAppThemeConfiguration.UseSameAsOS, persistInUserDefaults: false);

            Assert.AreEqual(UIUserInterfaceStyle.Unspecified, TestAppWindow.OverrideUserInterfaceStyle);
        }

        [TestMethod]
        public void AppThemeManager_NewChoice_ReachesTheWindowsShowing ()
        {
            using var manager = new AppThemeManager(eAppThemeConfiguration.Dark, persistInUserDefaults: false);

            manager.ThemeConfiguration = eAppThemeConfiguration.Light;

            Assert.AreEqual(UIUserInterfaceStyle.Light, TestAppWindow.OverrideUserInterfaceStyle);
        }

        [TestMethod]
        public void AppThemeManager_WindowShownLater_TakesTheChoice ()
        {
            using var manager = new AppThemeManager(eAppThemeConfiguration.Dark, persistInUserDefaults: false);

            m_window = new UIWindow(TestAppWindow.WindowScene) { Hidden = false };

            Assert.AreEqual(UIUserInterfaceStyle.Dark, m_window.OverrideUserInterfaceStyle);
        }

        [TestMethod]
        public void AppThemeManager_Disposed_LeavesLaterWindowsAlone ()
        {
            var manager = new AppThemeManager(eAppThemeConfiguration.Dark, persistInUserDefaults: false);
            manager.Dispose();

            m_window = new UIWindow(TestAppWindow.WindowScene) { Hidden = false };

            Assert.AreEqual(UIUserInterfaceStyle.Unspecified, m_window.OverrideUserInterfaceStyle);
        }

        // ===[ The theme in effect ]===

        [TestMethod]
        public void AppThemeManager_UseSameAsOS_CurrentThemeIsTheSystems ()
        {
            eAppTheme system = TestAppWindow.WindowScene.TraitCollection.UserInterfaceStyle == UIUserInterfaceStyle.Dark ? eAppTheme.Dark : eAppTheme.Light;

            using var manager = new AppThemeManager(eAppThemeConfiguration.UseSameAsOS, persistInUserDefaults: false);

            Assert.AreEqual(system, manager.CurrentTheme);
        }

        [TestMethod]
        public void AppThemeManager_BackToUseSameAsOS_ReadsTheSystem_NotTheWindowsPin ()
        {
            eAppTheme system = TestAppWindow.WindowScene.TraitCollection.UserInterfaceStyle == UIUserInterfaceStyle.Dark ? eAppTheme.Dark : eAppTheme.Light;
            eAppThemeConfiguration notTheSystems = system == eAppTheme.Dark ? eAppThemeConfiguration.Light : eAppThemeConfiguration.Dark;
            using var manager = new AppThemeManager(notTheSystems, persistInUserDefaults: false);

            manager.ThemeConfiguration = eAppThemeConfiguration.UseSameAsOS;

            Assert.AreEqual(system, manager.CurrentTheme);
        }

        // ===[ Keeping the choice ]===

        [TestMethod]
        public void AppThemeManager_Persisting_TheChoiceIsThereForTheNextManager ()
        {
            using (var first = new AppThemeManager(eAppThemeConfiguration.UseSameAsOS, persistInUserDefaults: true))
            {
                first.ThemeConfiguration = eAppThemeConfiguration.Light;
            }

            using var second = new AppThemeManager(eAppThemeConfiguration.Dark, persistInUserDefaults: true);
            Assert.AreEqual(eAppThemeConfiguration.Light, second.ThemeConfiguration);
        }

        [TestMethod]
        public void AppThemeManager_SavedValueNotAChoice_StartsFromTheFallback ()
        {
            NSUserDefaults.StandardUserDefaults.SetString("Sepia", kStoreKey);

            using var manager = new AppThemeManager(eAppThemeConfiguration.Light, persistInUserDefaults: true);

            Assert.AreEqual(eAppThemeConfiguration.Light, manager.ThemeConfiguration);
        }

        [TestMethod]
        public void AppThemeManager_NotPersisting_SavesNothing ()
        {
            using (var first = new AppThemeManager(eAppThemeConfiguration.UseSameAsOS, persistInUserDefaults: false))
            {
                first.ThemeConfiguration = eAppThemeConfiguration.Dark;
            }

            Assert.IsNull(NSUserDefaults.StandardUserDefaults.StringForKey(kStoreKey));
        }

        // ===[ Helpers ]===

        private static UIWindow TestAppWindow => TestHost.TestArea?.Window ?? throw new InvalidOperationException("Tests only run inside the test app.");
    }
}
