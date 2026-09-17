namespace AJut.UX.Tests
{
    using System;
    using AJut.UX;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ApplicationSetupConfigTests
    {
        private const string kProjectName = "CoolProjClient";
        private const string kSharedProjectName = "CoolProj";

        [TestMethod]
        public void SetupConfig_Defaults_MatchTheOldParameterDefaults ()
        {
            var config = new ApplicationSetupConfig(kProjectName);

            Assert.AreEqual(kProjectName, config.ProjectName);
            Assert.IsNull(config.SharedProjectName);
            Assert.IsTrue(config.SetupLogging);
            Assert.AreEqual(10, config.AgeMaxInDaysToKeepLogs);
            Assert.IsNull(config.OnExceptionReceived);
            Assert.IsNull(config.StorageRootOverride);

            // Both of these mean "whatever the stack running setup has always done", which is not a value this type can name
            Assert.IsNull(config.ApplicationStorageRoot);
            Assert.IsNull(config.CryptoSeedSource);
        }

        [TestMethod]
        public void SetupConfig_StorageRootProjectName_PrefersTheSharedName ()
        {
            Assert.AreEqual(kProjectName, new ApplicationSetupConfig(kProjectName).StorageRootProjectName);

            var shared = new ApplicationSetupConfig(kProjectName) { SharedProjectName = kSharedProjectName };
            Assert.AreEqual(kSharedProjectName, shared.StorageRootProjectName);
        }

        [TestMethod]
        public void SetupConfig_CryptoSeed_UnsetResolvesLikeSharedProjectNameFirst ()
        {
            var config = new ApplicationSetupConfig(kProjectName) { SharedProjectName = kSharedProjectName };
            Assert.AreEqual(kSharedProjectName, config.DetermineCryptoSeed());
        }

        [TestMethod]
        public void SetupConfig_CryptoSeed_SharedProjectNameFirst_FollowsTheStorageRoot ()
        {
            var config = new ApplicationSetupConfig(kProjectName)
            {
                SharedProjectName = kSharedProjectName,
                CryptoSeedSource = eCryptoSeedSource.SharedProjectNameFirst,
            };

            // The point of the setting: seed and storage root agree, so projects sharing a root can read each other
            Assert.AreEqual(config.StorageRootProjectName, config.DetermineCryptoSeed());
        }

        [TestMethod]
        public void SetupConfig_CryptoSeed_ProjectNameOnly_IgnoresTheSharedName ()
        {
            var config = new ApplicationSetupConfig(kProjectName)
            {
                SharedProjectName = kSharedProjectName,
                CryptoSeedSource = eCryptoSeedSource.ProjectNameOnly,
            };

            Assert.AreEqual(kProjectName, config.DetermineCryptoSeed());
        }

        [TestMethod]
        public void SetupConfig_CryptoSeed_WithNoSharedName_BothSourcesAgree ()
        {
            var sharedFirst = new ApplicationSetupConfig(kProjectName) { CryptoSeedSource = eCryptoSeedSource.SharedProjectNameFirst };
            var projectOnly = new ApplicationSetupConfig(kProjectName) { CryptoSeedSource = eCryptoSeedSource.ProjectNameOnly };

            // This is why the drift only ever bit projects passing a shared project name
            Assert.AreEqual(kProjectName, sharedFirst.DetermineCryptoSeed());
            Assert.AreEqual(kProjectName, projectOnly.DetermineCryptoSeed());
        }

        [TestMethod]
        public void SetupConfig_ExceptionReport_SurfacesTheExceptionWhenThereIsOne ()
        {
            var exception = new InvalidOperationException("nope");
            var report = new UnhandledExceptionReport(exception, true);

            Assert.AreSame(exception, report.Exception);
            Assert.AreSame(exception, report.ExceptionObject);
            Assert.AreEqual(true, report.IsTerminating);
        }

        [TestMethod]
        public void SetupConfig_ExceptionReport_HandlesAThrownNonException ()
        {
            // AppDomain.UnhandledException genuinely can hand over something that is not an exception
            var report = new UnhandledExceptionReport("just a string", null);

            Assert.IsNull(report.Exception);
            Assert.AreEqual("just a string", report.ExceptionObject);
            Assert.IsNull(report.IsTerminating);
        }
    }
}
