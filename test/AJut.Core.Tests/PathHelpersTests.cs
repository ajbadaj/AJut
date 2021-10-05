namespace AJut.Core.UnitTests
{
    using System.Linq;
    using AJut.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PathHelpersTests
    {
        [TestMethod]
        public void PathHelpers_Normalize_BasicTesting()
        {
            Assert.AreEqual(@"c:\dude\this\is\backwards.txt", PathHelpers.NormalizePath(@"C:\dude/this\is\super\..\backwards.txt"));
        }

        [TestMethod]
        public void PathHelpers_Normalize_SmallDir ()
        {
            Assert.AreEqual(@"c:\test", PathHelpers.NormalizePath(@"C:\test"));
            Assert.AreEqual(@"c:\test", PathHelpers.NormalizePath(@"C:\test\"));
            Assert.AreEqual(@"\\test", PathHelpers.NormalizePath(@"\\test"));
            Assert.AreEqual(@"\\test", PathHelpers.NormalizePath(@"\\test\"));
        }

        [TestMethod]
        public void PathHelpers_Normalize_UpADir_Extensive ()
        {
            Assert.AreEqual(@"c:\test", PathHelpers.NormalizePath(@"C:\bob\..\test"));
            Assert.AreEqual(@"c:\test", PathHelpers.NormalizePath(@"C:\bob\..\test\"));
            Assert.AreEqual(@"\\test", PathHelpers.NormalizePath(@"\\bob\..\test"));
            Assert.AreEqual(@"\\test", PathHelpers.NormalizePath(@"\\bob\..\test\"));
        }

        [TestMethod]
        public void PathHelpers_Normalize_Unrooted ()
        {
            Assert.AreEqual(@"\\dude\this\is\sweet.txt", PathHelpers.NormalizePath(@"\\dude\this\is\really\..\sweet.txt"));
        }

        [TestMethod]
        public void PathHelpers_Normalize_DirWithDanglingSeparator ()
        {
            Assert.AreEqual(@"\\dir\with\no\dangle", PathHelpers.NormalizePath(@"\\dir\with\no\dangle\"));
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


        [TestMethod]
        public void PathHelpers_TestExtensionParsing ()
        {
            string extensionsTest = "Bmp Files (*.bmp)|*.bmp|All files (*.*)|*.*|Icon Files (*.ico)|*.ico";
            var result = PathHelpers.ParseExtensionsFrom(extensionsTest).ToArray();
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual("bmp", result[0]);
            Assert.AreEqual("*", result[1]);
            Assert.AreEqual("ico", result[2]);
        }

        [TestMethod]
        public void PathHelpers_TestFilterExamination()
        {
            Assert.IsTrue(PathHelpers.FindMatchingExtensionsFromFilter(
                    @"C:\Test.jpg",
                    @"Any Image|*.bmp;*.dib;*.rle;*.gif;*.jpg;*.jpeg;*.jpe;*.jiff;*.exif;*.jxr;*.wdp;*.wmp;*.tiff;*.tif;*.png;*.heic;*.heif;*.webp;*.avif;*.ico;*.cur|Bitmap Image (*.bmp; *.dib; *.rle)|*.bmp;*.dib;*.rle|Gif Image (*.gif)|*.gif|JPEG Image (*.jpg; *.jpeg; *.jpe; *.jiff; *.exif)|*.jpg;*.jpeg;*.jpe;*.jiff;*.exif|JPEG Image (*.jxr; *.wdp; *.wmp)|*.jxr;*.wdp;*.wmp|Tiff Image (*.tiff; *.tif)|*.tiff;*.tif|Png Image (*.png)|*.png|High Efficiency Image (*.heic; *.heif)|*.heic;*.heif|WebP (*.webp)|*.webp|AVIF (*.avif)|*.avif|Ico (*.ico)|*.ico|Cursor (*.cur)|*.cur"
                ).Any()
            );
        }

        [TestMethod]
        public void PathHelpers_GetAllPathParts_Basic ()
        {
            string[] parts = PathHelpers.GetAllPathParts(@"C:\test\125\log.txt").ToArray();
            Assert.AreEqual(4, parts.Length);
            Assert.AreEqual("C:", parts[0]);
            Assert.AreEqual("test", parts[1]);
            Assert.AreEqual("125", parts[2]);
            Assert.AreEqual("log.txt", parts[3]);
        }


        [TestMethod]
        public void PathHelpers_GetAllPathParts_OtherSeps ()
        {
            string[] parts = PathHelpers.GetAllPathParts(@"C:/test/125/log.txt", '/').ToArray();
            Assert.AreEqual(4, parts.Length);
            Assert.AreEqual("C:", parts[0]);
            Assert.AreEqual("test", parts[1]);
            Assert.AreEqual("125", parts[2]);
            Assert.AreEqual("log.txt", parts[3]);
        }

        [TestMethod]
        public void PathHelpers_GetAllPathParts_EdgeCases ()
        {
            string test = "longstringnoseparators";
            string[] parts = PathHelpers.GetAllPathParts(test).ToArray();
            Assert.AreEqual(1, parts.Length);
            Assert.AreEqual("longstringnoseparators", parts[0]);
        }

        [TestMethod]
        public void PathHelpers_PathValidityTester ()
        {
            _EvalExpectValid(@"C:\test\test.txt");
            _EvalExpectValid(@"C:\test\test2\");
            _EvalExpectValid(@"C:\test\test2");

            Assert.IsFalse(PathHelpers.EvaluatePathValidity(@"C:\1|1.txt"));
            //Assert.IsTrue(PathHelpers.EvaluatePathValidity)

            void _EvalExpectValid (string _path)
            {
                var result = PathHelpers.EvaluatePathValidity(_path);
                Assert.IsTrue(result, result.GetErrorReport());
            }
        }

        [TestMethod]
        public void PathHelpers_ArePathsMatching_Basic ()
        {
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"c:\test", @"c:\test"));
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"\\test", @"\\test"));
            Assert.IsTrue(PathHelpers.ArePathsMatching());
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"C:\Test"));
        }

        [TestMethod]
        public void PathHelpers_ArePathsMatching_NotMatchingReturnsFalse ()
        {
            Assert.IsFalse(PathHelpers.ArePathsMatching(@"c:\test", @"c:\test\123"));
            Assert.IsFalse(PathHelpers.ArePathsMatching(@"\\test", @"\\test\123"));
        }


        [TestMethod]
        public void PathHelpers_ArePathsMatching_CasingDifferences ()
        {
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"c:\TEST", @"C:\test"));
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"\\test", @"\\TEST\"));
        }

        [TestMethod]
        public void PathHelpers_ArePathsMatching_PathCharDifferences ()
        {
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"c:\test", @"c:\test\"));
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"\\test", @"\\test\"));
        }


        [TestMethod]
        public void PathHelpers_ArePathsMatching_SeparatorDifferences ()
        {
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"c:\test", @"c:/test"));
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"\\test", @"//test"));
        }

        [TestMethod]
        public void PathHelpers_ArePathsMatching_PathNavEvaluates ()
        {
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"c:\test\..\bob", @"c:\bob"));
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"c:\test\..\bob", @"c:\bob\"));
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"\\test\..\bob", @"\\bob"));

            Assert.IsTrue(PathHelpers.ArePathsMatching(@"c:\bob", @"c:\test\..\bob"));
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"c:\bob\", @"c:\test\..\bob"));
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"\\bob\", @"\\test\..\bob"));
        }
    }
}
