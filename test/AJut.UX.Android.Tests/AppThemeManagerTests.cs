namespace AJut.UX.Tests
{
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Views;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AppThemeManagerTests
    {
        // Where AppThemeManager keeps a choice: its shared preferences file and key, cleared around every test here
        private const string kPreferencesName = "AJut_AppThemeManager";
        private const string kStoreKey = "AJut_AppThemeConfiguration";

        // Read before any test here changes it, so it can be put back
        private static readonly bool kUsesTouchVariantAtStart = ThemeColors.UsesTouchVariant;

        [TestInitialize]
        public void Setup ()
        {
            ClearSavedChoice();
        }

        [TestCleanup]
        public void Teardown ()
        {
            ClearSavedChoice();
            ThemeColors.UsesTouchVariant = kUsesTouchVariantAtStart;
        }

        // ===[ Applying ]===

        [TestMethod]
        public void AppThemeManager_DarkChoice_MakesAContextDark ()
        {
            using var manager = new AppThemeManager(TestHost.RequiredContext, eAppThemeConfiguration.Dark, persistInSharedPreferences: false);
            ContextThemeWrapper context = MakeContext();

            manager.ApplyTo(context);

            Assert.IsTrue(ThemeColors.IsDark(context));
        }

        [TestMethod]
        public void AppThemeManager_LightChoice_MakesAContextLight ()
        {
            using var manager = new AppThemeManager(TestHost.RequiredContext, eAppThemeConfiguration.Light, persistInSharedPreferences: false);
            ContextThemeWrapper context = MakeContext();

            manager.ApplyTo(context);

            Assert.IsFalse(ThemeColors.IsDark(context));
        }

        [TestMethod]
        public void AppThemeManager_UseSameAsOS_LeavesTheDevicesNightMode ()
        {
            using var manager = new AppThemeManager(TestHost.RequiredContext, eAppThemeConfiguration.UseSameAsOS, persistInSharedPreferences: false);
            ContextThemeWrapper context = MakeContext();

            manager.ApplyTo(context);

            Assert.AreEqual(IsDeviceDark(), ThemeColors.IsDark(context));
        }

        [TestMethod]
        public void AppThemeManager_ContextNotAnActivity_KeepsWhatItWasGiven ()
        {
            using var manager = new AppThemeManager(TestHost.RequiredContext, eAppThemeConfiguration.Dark, persistInSharedPreferences: false);
            ContextThemeWrapper context = MakeContext();
            manager.ApplyTo(context);

            manager.ThemeConfiguration = eAppThemeConfiguration.Light;

            Assert.IsTrue(ThemeColors.IsDark(context), "Only an activity follows later changes, by being recreated");
        }

        // ===[ The theme in effect ]===

        [TestMethod]
        public void AppThemeManager_UseSameAsOS_CurrentThemeIsTheDevices ()
        {
            using var manager = new AppThemeManager(TestHost.RequiredContext, eAppThemeConfiguration.UseSameAsOS, persistInSharedPreferences: false);

            Assert.AreEqual(IsDeviceDark() ? eAppTheme.Dark : eAppTheme.Light, manager.CurrentTheme);
        }

        [TestMethod]
        public void AppThemeManager_AChoice_IsTheThemeInEffect_WhateverTheDevice ()
        {
            eAppThemeConfiguration notTheDevices = IsDeviceDark() ? eAppThemeConfiguration.Light : eAppThemeConfiguration.Dark;
            using var manager = new AppThemeManager(TestHost.RequiredContext, eAppThemeConfiguration.UseSameAsOS, persistInSharedPreferences: false);

            manager.ThemeConfiguration = notTheDevices;

            Assert.AreEqual(notTheDevices == eAppThemeConfiguration.Dark ? eAppTheme.Dark : eAppTheme.Light, manager.CurrentTheme);
        }

        // ===[ Keeping the choice ]===

        [TestMethod]
        public void AppThemeManager_Persisting_TheChoiceIsThereForTheNextManager ()
        {
            using (var first = new AppThemeManager(TestHost.RequiredContext, eAppThemeConfiguration.UseSameAsOS, persistInSharedPreferences: true))
            {
                first.ThemeConfiguration = eAppThemeConfiguration.Light;
            }

            using var second = new AppThemeManager(TestHost.RequiredContext, eAppThemeConfiguration.Dark, persistInSharedPreferences: true);
            Assert.AreEqual(eAppThemeConfiguration.Light, second.ThemeConfiguration);
        }

        [TestMethod]
        public void AppThemeManager_NothingSaved_StartsFromTheFallback ()
        {
            using var manager = new AppThemeManager(TestHost.RequiredContext, eAppThemeConfiguration.Dark, persistInSharedPreferences: true);

            Assert.AreEqual(eAppThemeConfiguration.Dark, manager.ThemeConfiguration);
        }

        [TestMethod]
        public void AppThemeManager_SavedValueNotAChoice_StartsFromTheFallback ()
        {
            Preferences().Edit()!.PutString(kStoreKey, "Sepia")!.Commit();

            using var manager = new AppThemeManager(TestHost.RequiredContext, eAppThemeConfiguration.Light, persistInSharedPreferences: true);

            Assert.AreEqual(eAppThemeConfiguration.Light, manager.ThemeConfiguration);
        }

        [TestMethod]
        public void AppThemeManager_NotPersisting_SavesNothing ()
        {
            using (var first = new AppThemeManager(TestHost.RequiredContext, eAppThemeConfiguration.UseSameAsOS, persistInSharedPreferences: false))
            {
                first.ThemeConfiguration = eAppThemeConfiguration.Dark;
            }

            Assert.IsNull(Preferences().GetString(kStoreKey, null));
        }

        // ===[ Touch ]===

        [TestMethod]
        public void AppThemeManager_UsesTouchVariant_IsThePalettesSwitch ()
        {
            using var manager = new AppThemeManager(TestHost.RequiredContext, eAppThemeConfiguration.UseSameAsOS, persistInSharedPreferences: false);

            manager.UsesTouchVariant = !kUsesTouchVariantAtStart;

            Assert.AreEqual(!kUsesTouchVariantAtStart, ThemeColors.UsesTouchVariant);
        }

        // ===[ Helpers ]===

        // A fresh context nothing has read the resources of yet, which is what an activity is in its AttachBaseContext
        private static ContextThemeWrapper MakeContext () => new ContextThemeWrapper(TestHost.RequiredContext.ApplicationContext, Android.Resource.Style.ThemeDeviceDefault);

        private static bool IsDeviceDark () => ThemeColors.IsDark(TestHost.RequiredContext.ApplicationContext!);

        private static ISharedPreferences Preferences () => TestHost.RequiredContext.GetSharedPreferences(kPreferencesName, FileCreationMode.Private)!;

        private static void ClearSavedChoice () => Preferences().Edit()!.Remove(kStoreKey)!.Commit();
    }
}
