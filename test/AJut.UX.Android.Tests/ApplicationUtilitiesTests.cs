namespace AJut.UX.Tests
{
    using System;
    using System.IO;
    using AJut.UX;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ApplicationUtilitiesTests
    {
        [TestMethod]
        public void DetermineAppDataRoot_IsTheAppsFilesFolder ()
        {
            string expected = TestHost.RequiredContext.ApplicationContext!.FilesDir!.AbsolutePath;
            Assert.AreEqual(expected, ApplicationUtilities.DetermineAppDataRoot(TestHost.RequiredContext, new ApplicationSetupConfig("AndroidTests")));
        }

        [TestMethod]
        public void DetermineAppDataRoot_TakesTheOverrideAsGiven_AndMakesIt ()
        {
            string overrideRoot = Path.Combine(TestHost.RequiredContext.CacheDir!.AbsolutePath, $"root-override-{Guid.NewGuid():N}");
            try
            {
                string root = ApplicationUtilities.DetermineAppDataRoot(TestHost.RequiredContext, new ApplicationSetupConfig("AndroidTests") { StorageRootOverride = overrideRoot });
                Assert.AreEqual(overrideRoot, root, "No project folder is added to an override");
                Assert.IsTrue(Directory.Exists(root));
            }
            finally
            {
                Directory.Delete(overrideRoot, recursive: true);
            }
        }

        [TestMethod]
        public void RunOnetimeSetup_SetsTheAppDataRoot ()
        {
            // Setup is once per process, and nothing else in this app runs it
            ApplicationUtilities.RunOnetimeSetup(TestHost.RequiredContext, new ApplicationSetupConfig("AndroidTests") { SetupLogging = false });

            string expected = TestHost.RequiredContext.ApplicationContext!.FilesDir!.AbsolutePath;
            Assert.AreEqual(expected, AppStorage.AppDataRoot);
            Assert.AreEqual(Path.Combine(expected, "Layouts", "main.json"), ApplicationUtilities.BuildAppDataProjectPath("Layouts", "main.json"));
        }
    }
}
