namespace AJut.UX.Tests
{
    using System;
    using System.IO;
    using AJut.UX;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Covers the WPF <see cref="ApplicationUtilities"/> setup decisions - where app data lands, and what seeds crypto
    /// obfuscation. The rest of RunOnetimeSetup needs a live Application and only runs once per process, so these two
    /// decisions are what is reachable from a test.
    /// </summary>
    [TestClass]
    public class WpfApplicationUtilitiesTests
    {
        private const string kProjectName = "AJutTest_AppUtils_Project";
        private const string kSharedProjectName = "AJutTest_AppUtils_Shared";

        private string m_overrideRoot;

        // ===========[ Setup/Construction/Teardown ]===================================

        [TestInitialize]
        public void Setup ()
        {
            m_overrideRoot = Path.Combine(Path.GetTempPath(), $"AJutTest_AppUtils_{Guid.NewGuid():N}");
        }

        [TestCleanup]
        public void Teardown ()
        {
            DeleteDirectoryIfPresent(m_overrideRoot);
            DeleteDirectoryIfPresent(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), kProjectName));
            DeleteDirectoryIfPresent(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), kSharedProjectName));
            DeleteDirectoryIfPresent(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), kProjectName));
        }

        // ===========[ App Data Root ]===================================

        [TestMethod]
        public void AppUtils_NoOverride_UsesRoamingAppDataWithProjectNameAppended ()
        {
            string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), kProjectName);
            Assert.AreEqual(expected, ApplicationUtilities.DetermineAppDataRoot(new ApplicationSetupConfig(kProjectName)));
        }

        [TestMethod]
        public void AppUtils_NoOverrideWithSharedProjectName_UsesTheSharedNameInstead ()
        {
            string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), kSharedProjectName);
            string actual = ApplicationUtilities.DetermineAppDataRoot(new ApplicationSetupConfig(kProjectName)
            {
                SharedProjectName = kSharedProjectName,
            });

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AppUtils_NoOverride_HonorsTheRequestedSpecialFolder ()
        {
            string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), kProjectName);
            string actual = ApplicationUtilities.DetermineAppDataRoot(new ApplicationSetupConfig(kProjectName)
            {
                ApplicationStorageRoot = Environment.SpecialFolder.LocalApplicationData,
            });

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AppUtils_OverrideGiven_WinsOverTheSpecialFolder ()
        {
            string specialFolderResult = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), kProjectName);
            string actual = ApplicationUtilities.DetermineAppDataRoot(new ApplicationSetupConfig(kProjectName)
            {
                ApplicationStorageRoot = Environment.SpecialFolder.LocalApplicationData,
                StorageRootOverride = m_overrideRoot,
            });

            Assert.AreEqual(m_overrideRoot, actual);
            Assert.AreNotEqual(specialFolderResult, actual);
        }

        [TestMethod]
        public void AppUtils_OverrideGiven_IsTakenVerbatimWithNothingAppended ()
        {
            // This is the whole reason the override exists - a process handed somebody else's storage root has to land
            //  on that root, not in a project named subfolder of it, or the two sides stop agreeing about where things live.
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

        // ===========[ Crypto Seed ]===================================

        [TestMethod]
        public void AppUtils_CryptoSeed_NoSharedProjectName_NeedsNoChoice ()
        {
            // Without a shared project name both answers are the same string, so there is nothing to ask about
            Assert.AreEqual(kProjectName, ApplicationUtilities.DetermineCryptoSeed(new ApplicationSetupConfig(kProjectName)));
        }

        [TestMethod]
        public void AppUtils_CryptoSeed_SharedProjectNameWithNoChoiceStated_Throws ()
        {
            var config = new ApplicationSetupConfig(kProjectName)
            {
                SharedProjectName = kSharedProjectName,
            };

            Assert.ThrowsException<InvalidOperationException>(() => ApplicationUtilities.DetermineCryptoSeed(config));
        }

        [TestMethod]
        public void AppUtils_CryptoSeed_SharedProjectNameFirst_SeedsFromTheSharedName ()
        {
            string actual = ApplicationUtilities.DetermineCryptoSeed(new ApplicationSetupConfig(kProjectName)
            {
                SharedProjectName = kSharedProjectName,
                CryptoSeedSource = eCryptoSeedSource.SharedProjectNameFirst,
            });

            Assert.AreEqual(kSharedProjectName, actual);
        }

        [TestMethod]
        public void AppUtils_CryptoSeed_ProjectNameOnly_KeepsTheHistoricalWpfSeed ()
        {
            string actual = ApplicationUtilities.DetermineCryptoSeed(new ApplicationSetupConfig(kProjectName)
            {
                SharedProjectName = kSharedProjectName,
                CryptoSeedSource = eCryptoSeedSource.ProjectNameOnly,
            });

            Assert.AreEqual(kProjectName, actual);
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
