namespace AJut.Core.UnitTests
{
    using System.Linq;
    using AJut.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class FileHelpersTests
    {
        [TestMethod]
        public void FileHelpers_GenerateEmbeddedResourceName_ReturnsValidName()
        {
            string path = FileHelpers.GenerateEmbeddedResourceName("_TestData/Basic.json");
            Assert.IsNotNull(path);
            Assert.AreEqual("AJut.Core.Tests._TestData.Basic.json", path);
        }

        [TestMethod]
        public void FileHelpers_GenerateEmbeddedResourceStream_ReturnsValidStream()
        {
            string[] manifestResources = typeof(FileHelpersTests).Assembly.GetManifestResourceNames();

            string resourceName = FileHelpers.GenerateEmbeddedResourceName("_TestData/Basic.json");
            Assert.IsTrue(manifestResources.Any(r => r == resourceName));
            using (var stream = FileHelpers.GetEmbeddedResourceStream("_TestData/Basic.json"))
            {
                Assert.IsNotNull(stream);
            }
        }
    }
}
