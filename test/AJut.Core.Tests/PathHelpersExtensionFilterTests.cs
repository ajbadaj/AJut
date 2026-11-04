namespace AJut.Core.UnitTests
{
    using System.Linq;
    using AJut.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>Reading the extensions out of a file filter, and matching a file name against them</summary>
    [TestClass]
    public class PathHelpersExtensionFilterTests
    {
        [TestMethod]
        public void ParseExtensionsFrom_GroupWithSeveralPatterns_GivesEachOne ()
        {
            string[] extensions = PathHelpers.ParseExtensionsFrom("Images (*.png;*.jpg;*.gif)|*.png;*.jpg;*.gif|All files (*.*)|*.*");

            CollectionAssert.AreEqual(new[] { "png", "jpg", "gif", "*" }, extensions, "Got: " + string.Join(", ", extensions));
        }

        [TestMethod]
        public void ParseExtensionsFrom_PatternsWithNoDescription_GivesEachOne ()
        {
            string[] extensions = PathHelpers.ParseExtensionsFrom("*.txt;*.md");

            CollectionAssert.AreEqual(new[] { "txt", "md" }, extensions, "Got: " + string.Join(", ", extensions));
        }

        [TestMethod]
        public void FindMatchingExtensionsFromFilter_FileMatchingALaterPatternInAGroup_Matches ()
        {
            Assert.IsTrue(PathHelpers.FindMatchingExtensionsFromFilter("photo.jpg", "Images|*.png;*.jpg").Any());
        }
    }
}
