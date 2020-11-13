namespace AJut.UnitTests.Core
{
    using AJut.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PathHelpersTests
    {
        [TestMethod]
        public void PathHelpers_Normalize_BasicTesting()
        {
            Assert.AreEqual(@"c:\dude\this\is\backwards.txt", PathHelpers.Normalize(@"C:\dude/this\is\super\..\backwards.txt"));
        }

        [TestMethod]
        public void PathHelpers_RelativeDirectory_BasicTesting()
        {
            Assert.AreEqual(@"First\Second\thing.xml", PathHelpers.GenerateRelativePath(@"C:\Test\", @"C:\Test\First\Second\thing.xml"));
            Assert.AreEqual(@".\First\Second\thing.xml", PathHelpers.GenerateRelativePath(@"C:\Test\", @"C:\Test\First\Second\thing.xml", prefix:".\\"));
            Assert.AreEqual("First/Second/thing.xml", PathHelpers.GenerateRelativePath(@"C:\Test\", @"C:\Test\First\Second\thing.xml", separatorChar:'/', prefix: null));
        }

        [TestMethod]
        public void PathHelpers_RelativeDirectory_UpADirectoryTesting ()
        {
            Assert.AreEqual(@"..\Second\thing.xml", PathHelpers.GenerateRelativePath(@"C:\Test\First", @"C:\Test\Second\thing.xml"));
        }

        [TestMethod]
        public void PathHelpers_RelativeDirectory_MatchTesting ()
        {
            Assert.AreEqual(string.Empty, PathHelpers.GenerateRelativePath(@"C:\Test\First", @"C:\Test\First"));

            Assert.AreEqual(@".\", PathHelpers.GenerateRelativePath(@"C:\Test\First", @"C:\Test\First", prefix:".\\"));
            Assert.AreEqual(@".\", PathHelpers.GenerateRelativePath(@"C:\Test\First", @"C:\Test\First\", prefix: ".\\"));
            Assert.AreEqual(@".\", PathHelpers.GenerateRelativePath(@"C:\Test\First\", @"C:\Test\First", prefix: ".\\"));
        }
    }
}
