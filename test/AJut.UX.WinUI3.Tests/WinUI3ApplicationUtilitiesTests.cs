namespace AJut.UX.WinUI3.Tests
{
    using System;
    using System.IO;
    using AJut.UX;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Covers where the WinUI3 <see cref="ApplicationUtilities"/> puts app data and logs. The rest of RunOnetimeSetup needs
    /// a live Application and only runs once per process, so these decisions are what is reachable from a test.
    /// </summary>
    [TestClass]
    public class WinUI3ApplicationUtilitiesTests
    {
        private const string kProjectName = "AJutTest_WinUI3AppUtils_Project";
        private const string kSharedProjectName = "AJutTest_WinUI3AppUtils_Shared";
        private const string kAppDataRoot = @"C:\AJutTest\AppDataRoot";

        private string m_overrideRoot;

        // ===========[ Setup/Construction/Teardown ]===================================

        [TestInitialize]
        public void Setup ()
        {
            m_overrideRoot = Path.Combine(Path.GetTempPath(), $"AJutTest_WinUI3AppUtils_{Guid.NewGuid():N}");
        }

        [TestCleanup]
        public void Teardown ()
        {
            DeleteDirectoryIfPresent(m_overrideRoot);
            DeleteDirectoryIfPresent(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), kProjectName));
            DeleteDirectoryIfPresent(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), kSharedProjectName));
            DeleteDirectoryIfPresent(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), kProjectName));
        }

        // ===========[ App Data Root ]===================================

        [TestMethod]
        public void AppUtils_NoOverride_UsesLocalAppDataWithProjectNameAppended ()
        {
            // Never the bare special folder. That is shared by every app on the machine, and so would be anything built
            //  with BuildAppDataProjectPath.
            string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), kProjectName);
            Assert.AreEqual(expected, ApplicationUtilities.DetermineAppDataRoot(new ApplicationSetupConfig(kProjectName)));
        }

        [TestMethod]
        public void AppUtils_NoOverrideWithSharedProjectName_UsesTheSharedNameInstead ()
        {
            string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), kSharedProjectName);
            string actual = ApplicationUtilities.DetermineAppDataRoot(new ApplicationSetupConfig(kProjectName)
            {
                SharedProjectName = kSharedProjectName,
            });

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AppUtils_NoOverride_HonorsTheRequestedSpecialFolder ()
        {
            string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), kProjectName);
            string actual = ApplicationUtilities.DetermineAppDataRoot(new ApplicationSetupConfig(kProjectName)
            {
                ApplicationStorageRoot = Environment.SpecialFolder.ApplicationData,
            });

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AppUtils_PackageVirtualizationIsolation_UsesTheSpecialFolderAsIs ()
        {
            string expected = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string actual = ApplicationUtilities.DetermineAppDataRoot(new ApplicationSetupConfig(kProjectName)
            {
                SharedProjectName = kSharedProjectName,
                StorageRootIsolation = eStorageRootIsolation.PackageVirtualization,
            });

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AppUtils_PackageVirtualizationIsolation_StillHonorsTheRequestedSpecialFolder ()
        {
            string expected = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string actual = ApplicationUtilities.DetermineAppDataRoot(new ApplicationSetupConfig(kProjectName)
            {
                ApplicationStorageRoot = Environment.SpecialFolder.ApplicationData,
                StorageRootIsolation = eStorageRootIsolation.PackageVirtualization,
            });

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AppUtils_OverrideGiven_WinsOverTheSpecialFolder ()
        {
            string specialFolderResult = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), kProjectName);
            string actual = ApplicationUtilities.DetermineAppDataRoot(new ApplicationSetupConfig(kProjectName)
            {
                ApplicationStorageRoot = Environment.SpecialFolder.ApplicationData,
                StorageRootOverride = m_overrideRoot,
            });

            Assert.AreEqual(m_overrideRoot, actual);
            Assert.AreNotEqual(specialFolderResult, actual);
        }

        [TestMethod]
        public void AppUtils_OverrideGiven_WinsOverPackageVirtualizationIsolation ()
        {
            string actual = ApplicationUtilities.DetermineAppDataRoot(new ApplicationSetupConfig(kProjectName)
            {
                StorageRootIsolation = eStorageRootIsolation.PackageVirtualization,
                StorageRootOverride = m_overrideRoot,
            });

            Assert.AreEqual(m_overrideRoot, actual);
        }

        [TestMethod]
        public void AppUtils_OverrideGiven_IsTakenVerbatimWithNothingAppended ()
        {
            string actual = ApplicationUtilities.DetermineAppDataRoot(new ApplicationSetupConfig(kProjectName)
            {
                StorageRootOverride = m_overrideRoot,
            });

            Assert.AreEqual(m_overrideRoot, actual);
        }

        [TestMethod]
        public void AppUtils_OverrideGivenWithSharedProjectName_IsStillVerbatim ()
        {
            string actual = ApplicationUtilities.DetermineAppDataRoot(new ApplicationSetupConfig(kProjectName)
            {
                SharedProjectName = kSharedProjectName,
                StorageRootOverride = m_overrideRoot,
            });

            Assert.AreEqual(m_overrideRoot, actual);
        }

        [TestMethod]
        public void AppUtils_OverrideGiven_FolderIsCreated ()
        {
            Assert.IsFalse(Directory.Exists(m_overrideRoot), "Test setup issue: the override folder existed before the call");
            ApplicationUtilities.DetermineAppDataRoot(new ApplicationSetupConfig(kProjectName)
            {
                StorageRootOverride = m_overrideRoot,
            });

            Assert.IsTrue(Directory.Exists(m_overrideRoot));
        }

        // ===========[ Logs Directory ]===================================

        [TestMethod]
        public void AppUtils_Logs_UnsharedRoot_GoStraightIntoLogs ()
        {
            string actual = ApplicationUtilities.DetermineLogsDirectory(kAppDataRoot, kProjectName, null, eStorageRootIsolation.ProjectFolder);
            Assert.AreEqual(Path.Combine(kAppDataRoot, "Logs"), actual);
        }

        [TestMethod]
        public void AppUtils_Logs_SharedProjectName_GetAProjectFolder ()
        {
            string actual = ApplicationUtilities.DetermineLogsDirectory(kAppDataRoot, kProjectName, kSharedProjectName, eStorageRootIsolation.ProjectFolder);
            Assert.AreEqual(Path.Combine(kAppDataRoot, "Logs", kProjectName), actual);
        }

        [TestMethod]
        public void AppUtils_Logs_PackageVirtualization_GetAProjectFolderEvenWithoutASharedName ()
        {
            // A virtualized package root is shared by everything in the package, and by every app on the machine if the
            //  setting was wrong - either way nobody else's logs should end up in the same folder
            string actual = ApplicationUtilities.DetermineLogsDirectory(kAppDataRoot, kProjectName, null, eStorageRootIsolation.PackageVirtualization);
            Assert.AreEqual(Path.Combine(kAppDataRoot, "Logs", kProjectName), actual);
        }

        // ===========[ Helper Methods ]===================================

        private static void DeleteDirectoryIfPresent (string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
