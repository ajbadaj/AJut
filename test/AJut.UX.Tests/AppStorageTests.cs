namespace AJut.UX.Tests
{
    using System;
    using System.IO;
    using AJut.UX;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Covers <see cref="AppStorage"/>, the app data root every UX package sets and everything that writes files builds on,
    /// and the Windows root and log rules WPF and WinUI3 share through it
    /// </summary>
    [TestClass]
    public class AppStorageTests
    {
        private const string kProjectName = "AJutTest_AppStorage_Project";

        private string m_tempRoot;
        private string m_rootBefore;

        // ===========[ Setup/Construction/Teardown ]===================================

        [TestInitialize]
        public void Setup ()
        {
            m_tempRoot = Path.Combine(Path.GetTempPath(), $"AJutTest_AppStorage_{Guid.NewGuid():N}");
            m_rootBefore = AppStorage.AppDataRoot;
        }

        [TestCleanup]
        public void Teardown ()
        {
            AppStorage.SetAppDataRoot(m_rootBefore);
            if (Directory.Exists(m_tempRoot))
            {
                Directory.Delete(m_tempRoot, recursive: true);
            }

            string localProjectFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), kProjectName);
            if (Directory.Exists(localProjectFolder))
            {
                Directory.Delete(localProjectFolder, recursive: true);
            }
        }

        // ===========[ App data root ]===================================

        [TestMethod]
        public void AppStorage_SetAppDataRoot_IsWhatPathsBuildOn ()
        {
            AppStorage.SetAppDataRoot(m_tempRoot);

            Assert.AreEqual(m_tempRoot, AppStorage.AppDataRoot);
            Assert.AreEqual(Path.Combine(m_tempRoot, "Layouts", "main.layout"), AppStorage.BuildAppDataPath("Layouts", "main.layout"));
        }

        [TestMethod]
        public void AppStorage_SpecialFolderRoot_UsesTheStacksDefaultWhenTheConfigSaysNothing ()
        {
            string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), kProjectName);

            string actual = AppStorage.DetermineSpecialFolderRoot(new ApplicationSetupConfig(kProjectName), Environment.SpecialFolder.LocalApplicationData);

            Assert.AreEqual(expected, actual);
            Assert.IsTrue(Directory.Exists(actual));
        }

        [TestMethod]
        public void AppStorage_SpecialFolderRoot_OverrideIsTakenVerbatim ()
        {
            string actual = AppStorage.DetermineSpecialFolderRoot(
                new ApplicationSetupConfig(kProjectName) { StorageRootOverride = m_tempRoot },
                Environment.SpecialFolder.LocalApplicationData
            );

            Assert.AreEqual(m_tempRoot, actual);
            Assert.IsTrue(Directory.Exists(m_tempRoot));
        }

        // ===========[ Logs ]===================================

        [TestMethod]
        public void AppStorage_Logs_ProjectFolderOnlyWhenTheRootIsShared ()
        {
            Assert.AreEqual(Path.Combine(m_tempRoot, "Logs"), AppStorage.DetermineLogsDirectory(m_tempRoot, kProjectName, null, eStorageRootIsolation.ProjectFolder));
            Assert.AreEqual(Path.Combine(m_tempRoot, "Logs", kProjectName), AppStorage.DetermineLogsDirectory(m_tempRoot, kProjectName, "Shared", eStorageRootIsolation.ProjectFolder));
            Assert.AreEqual(Path.Combine(m_tempRoot, "Logs", kProjectName), AppStorage.DetermineLogsDirectory(m_tempRoot, kProjectName, null, eStorageRootIsolation.PackageVirtualization));
        }

        [TestMethod]
        public void AppStorage_PurgeLogs_DeletesOnlyWhatIsOlderThanTheAge ()
        {
            Directory.CreateDirectory(m_tempRoot);
            string oldLog = Path.Combine(m_tempRoot, "old.log");
            string newLog = Path.Combine(m_tempRoot, "new.log");
            File.WriteAllText(oldLog, "old");
            File.WriteAllText(newLog, "new");
            File.SetLastWriteTime(oldLog, DateTime.Now.AddDays(-20));

            AppStorage.PurgeLogsOlderThan(TimeSpan.FromDays(10), m_tempRoot);

            Assert.IsFalse(File.Exists(oldLog));
            Assert.IsTrue(File.Exists(newLog));
        }

        // ===========[ Users of the root ]===================================

        [TestMethod]
        public void AppStorage_DockingManagerWithNoPath_KeepsItsLayoutUnderTheRoot ()
        {
            AppStorage.SetAppDataRoot(m_tempRoot);

            var manager = new TestDockingManager(persistentPath: null);

            Assert.AreEqual(Path.Combine(m_tempRoot, "Layouts", "test.layout"), manager.DockLayoutPersistentStorageFile);
        }
    }
}
