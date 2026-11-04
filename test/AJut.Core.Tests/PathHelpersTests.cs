namespace AJut.Core.UnitTests
{
    using System;
    using System.Globalization;
    using System.Linq;
    using AJut.IO;
    using AJut.Storage;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Each test follows Windows' path rules unless it simulates another platform, since most of these expect case to be
    /// ignored. Which characters a path can't hold still comes from the OS the tests run on.
    /// </summary>
    [TestClass]
    public class PathHelpersTests
    {
        private IDisposable m_platformRules;

        [TestInitialize]
        public void Setup ()
        {
            m_platformRules = PathPlatform.Simulate(ePathPlatform.Windows);
        }

        [TestCleanup]
        public void Teardown ()
        {
            m_platformRules?.Dispose();
            m_platformRules = null;
        }

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
            Assert.AreEqual(@"\\server\share\test", PathHelpers.NormalizePath(@"\\server\share\bob\..\test"));
            Assert.AreEqual(@"\\server\share\test", PathHelpers.NormalizePath(@"\\server\share\bob\..\test\"));
        }

        [TestMethod]
        public void PathHelpers_Normalize_UpTwoDirsInARow ()
        {
            Assert.AreEqual(@"c", PathHelpers.NormalizePath(@"a\b\..\..\c"));
            Assert.AreEqual(@"c:\c", PathHelpers.NormalizePath(@"C:\a\b\..\..\c"));
            Assert.AreEqual(@"\\server\share\c", PathHelpers.NormalizePath(@"\\server\share\a\b\..\..\c"));
        }

        [TestMethod]
        public void PathHelpers_Normalize_UpPastARoot_Throws ()
        {
            // A drive, a drive's root and a network path's server and share have nothing above them, so a '..' that climbs
            //  past one is nonsense input, not something to clamp
            string[] climbsPastItsRoot =
            {
                @"C:\..\x",
                @"C:\a\..\..\c",
                @"C:\..\..\x",
                @"\..\x",
                "/../x",
                @"\\server\share\..\x",
                @"\\server\share\a\..\..\x",
                @"\\server\..\x",
                @"\\bob\..\test",
                @"\\a\b\..\..\c",
                @"\\..\x",
            };

            foreach (string path in climbsPastItsRoot)
            {
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PathHelpers.NormalizePath(path), path);
            }
        }

        [TestMethod]
        public void PathHelpers_IsValidAsPath_UpPastARoot_IsInvalid ()
        {
            // Written with '/', which separates on every OS
            foreach (string path in new[] { "C:/../x", "C:/a/../../c", "/../x", "//server/share/../x", "//server/../x" })
            {
                Result validity = PathHelpers.EvaluatePathValidity(path);
                Assert.IsFalse(validity, path);
                StringAssert.Contains(validity.GetErrorReport(), "climbs above its root", path);
                Assert.IsFalse(PathHelpers.IsValidAsPath(path), path);
            }

            // Climbing within the path, or from the start of a relative one, is fine
            Assert.IsTrue(PathHelpers.IsValidAsPath("C:/a/../b"));
            Assert.IsTrue(PathHelpers.IsValidAsPath("//server/share/a/../b"));
            Assert.IsTrue(PathHelpers.IsValidAsPath("../x"));
        }

        [TestMethod]
        public void PathHelpers_Normalize_DriveRelativePath_Throws ()
        {
            // c:test is test under drive C's current folder, not c:\test, and c: alone is that current folder
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PathHelpers.NormalizePath("c:test"));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PathHelpers.NormalizePath("C:"));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PathHelpers.NormalizePath(@"c:..\x"));
            Assert.AreEqual(@"c:\test", PathHelpers.NormalizePath("c:/test"));
        }

        [TestMethod]
        public void PathHelpers_IsValidAsPath_DriveRelativePath_IsInvalid ()
        {
            foreach (string path in new[] { "c:test", "C:", "c:sub/file.txt" })
            {
                Result validity = PathHelpers.EvaluatePathValidity(path);
                Assert.IsFalse(validity, path);
                StringAssert.Contains(validity.GetErrorReport(), "drive-relative", path);
                Assert.IsFalse(PathHelpers.IsValidAsPath(path), path);
            }

            Assert.IsTrue(PathHelpers.IsValidAsPath("c:/test"));
        }

        [TestMethod]
        public void PathHelpers_Normalize_UpPastTheStartOfARelativePath_Throws ()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PathHelpers.NormalizePath(@"a\..\..\c"));
        }

        [TestMethod]
        public void PathHelpers_Normalize_RelativeStart ()
        {
            Assert.AreEqual(@".git\config", PathHelpers.NormalizePath(@".git\config"));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PathHelpers.NormalizePath(@"..\a\b"));
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
        public void PathHelpers_ExtensionFromFilterTesting_MultiFileFilterExtensions ()
        {
            string extensionsTest = "Bmp Files (*.bmp)|*.bmp|All files (*.*)|*.*|Icon Files (*.ico)|*.ico";
            var result = PathHelpers.ParseExtensionsFrom(extensionsTest).ToArray();
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual("bmp", result[0]);
            Assert.AreEqual("*", result[1]);
            Assert.AreEqual("ico", result[2]);
        }


        [TestMethod]
        public void PathHelpers_ExtensionFromFilterTesting_FilenameAndExtension ()
        {
            string extensionsTest = "AJut.Core.Dll (Core Dll)|*ajut.core.dll";
            var result = PathHelpers.ParseExtensionsFrom(extensionsTest).ToArray();
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("ajut.core.dll", result[0]);
        }

        [TestMethod]
        public void PathHelpers_ExtensionFromFilterTesting_FindMatching ()
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
            // Splits on the OS's own separator, and this is a Windows path
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("Windows only: splits a Windows path on the OS's separator");
            }

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
        public void PathHelpers_GetAllPathParts_RelativeStart_KeepsEveryPart ()
        {
            CollectionAssert.AreEqual(new[] { ".", "a", "b" }, PathHelpers.GetAllPathParts(@".\a\b", '\\').ToArray());
            CollectionAssert.AreEqual(new[] { "..", "a", "b" }, PathHelpers.GetAllPathParts(@"..\a\b", '\\').ToArray());
            CollectionAssert.AreEqual(new[] { "..", "a" }, PathHelpers.GetAllPathParts(@"..\a", '\\').ToArray());
            CollectionAssert.AreEqual(new[] { ".git", "config" }, PathHelpers.GetAllPathParts(@".git\config", '\\').ToArray());
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
            // Which characters a path can hold is the OS's call: '|' is invalid on Windows but valid on macOS and Linux
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("Windows only: path validity follows the OS, and these are Windows paths");
            }

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
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"\\server\share\test\..\bob", @"\\server\share\bob"));

            Assert.IsTrue(PathHelpers.ArePathsMatching(@"c:\bob", @"c:\test\..\bob"));
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"c:\bob\", @"c:\test\..\bob"));
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"\\server\share\bob\", @"\\server\share\test\..\bob"));
        }

        [TestMethod]
        public void PathHelpers_GetAllPathParts_RootOfTheDrive_KeepsAnEmptyRootPart ()
        {
            CollectionAssert.AreEqual(new[] { "", "a", "b" }, PathHelpers.GetAllPathParts(@"\a\b", '\\').ToArray());
            CollectionAssert.AreEqual(new[] { "", "users", "x" }, PathHelpers.GetAllPathParts("/users/x", '/').ToArray());
        }

        [TestMethod]
        public void PathHelpers_Normalize_RootOfTheDrive_KeepsItsRoot ()
        {
            Assert.AreEqual(@"\a\b", PathHelpers.NormalizePath(@"\a\b"));
            Assert.AreEqual(@"\users\x", PathHelpers.NormalizePath("/Users/x"));
            Assert.IsFalse(PathHelpers.ArePathsMatching("/x/y", "x/y"));
        }

        [TestMethod]
        public void PathHelpers_GetAllPathParts_ShortPaths ()
        {
            CollectionAssert.AreEqual(new[] { "x" }, PathHelpers.GetAllPathParts(@"x\", '\\').ToArray());
            CollectionAssert.AreEqual(Array.Empty<string>(), PathHelpers.GetAllPathParts("", '\\').ToArray());
            CollectionAssert.AreEqual(new[] { "" }, PathHelpers.GetAllPathParts(@"\", '\\').ToArray());
            CollectionAssert.AreEqual(new[] { @"\" }, PathHelpers.GetAllPathParts(@"\\", '\\').ToArray());
            CollectionAssert.AreEqual(new[] { "c:" }, PathHelpers.GetAllPathParts("c:", '\\').ToArray());
        }

        [TestMethod]
        public void PathHelpers_Normalize_ShortPaths ()
        {
            Assert.AreEqual("x", PathHelpers.NormalizePath(@"x\"));
            Assert.AreEqual(@"\", PathHelpers.NormalizePath(@"\"));
            Assert.AreEqual(@"\\", PathHelpers.NormalizePath(@"\\"));
            Assert.AreEqual(@"\", PathHelpers.NormalizePath(@"\a\.."));
            Assert.AreEqual("", PathHelpers.NormalizePath(""));
        }

        [TestMethod]
        public void PathHelpers_Normalize_DropsDotParts ()
        {
            Assert.AreEqual(@"a\b", PathHelpers.NormalizePath(@"a\.\b"));
            Assert.AreEqual(@"a\b", PathHelpers.NormalizePath(@".\a\b"));
            Assert.AreEqual(@"c:\x", PathHelpers.NormalizePath(@"C:\.\x"));
            Assert.AreEqual("b", PathHelpers.NormalizePath(@"a\.\..\b"));
            Assert.IsTrue(PathHelpers.ArePathsMatching(@"a\.\b", @"a\b"));
        }

        [TestMethod]
        public void PathHelpers_ArePathsMatching_IgnoresTheCurrentCulture ()
        {
            // Turkish lowercases I to a dotless i, so lowercasing by the current culture tells BIN from bin
            RunUnderCulture("tr-TR", () => Assert.IsTrue(PathHelpers.ArePathsMatching(@"C:\BIN", @"c:\bin")));
        }

        [TestMethod]
        public void PathHelpers_RelativeDirectory_IgnoresTheCurrentCulture ()
        {
            RunUnderCulture("tr-TR", () => Assert.AreEqual("x.txt", PathHelpers.GenerateRelativePath(@"C:\Bin\", @"C:\BIN\x.txt")));
        }

        [TestMethod]
        public void PathHelpers_IsValidAsPath_TakesEitherSeparator ()
        {
            Assert.IsTrue(PathHelpers.IsValidAsPath(@"c:/a/b.txt"));
            Assert.IsTrue(PathHelpers.IsValidAsPath(@"c:\a/b/"));
            Assert.AreEqual(@"First\thing.xml", PathHelpers.GenerateRelativePath(@"C:/Test/", @"C:/Test/First/thing.xml"));
        }

        [TestMethod]
        public void PathHelpers_FindMatchingExtensions_MatchesAtADot ()
        {
            Assert.IsFalse(PathHelpers.FindMatchingExtensions(@"C:\notajpg", "jpg").Any());
            Assert.IsFalse(PathHelpers.FindMatchingExtensions(@"C:\a.xbmp", "bmp").Any());
            Assert.IsTrue(PathHelpers.FindMatchingExtensions(@"C:\photo.JPG", "jpg").Any());
            Assert.IsTrue(PathHelpers.FindMatchingExtensions(@"C:\photo.jpg", ".jpg").Any());
            Assert.IsTrue(PathHelpers.FindMatchingExtensions(@"C:\bin\ajut.core.dll", "ajut.core.dll").Any());
            Assert.IsTrue(PathHelpers.FindMatchingExtensions(@"C:\anything", "*").Any());
        }

        [TestMethod]
        public void PathHelpers_CaseSensitivePlatform_TellsCaseApart ()
        {
            // Android, iOS and Linux: Photos and photos are two folders
            using (PathPlatform.Simulate(ePathPlatform.CaseSensitive))
            {
                Assert.IsFalse(PathHelpers.ArePathsMatching("/home/a/Photos", "/home/a/photos"));
                Assert.IsTrue(PathHelpers.ArePathsMatching("/home/a/Photos", "/home/a/Photos/"));
                Assert.AreEqual("/Home/Photos", PathHelpers.NormalizePath("/Home/Photos"));
                Assert.AreEqual(@"..\bin\x.txt", PathHelpers.GenerateRelativePath("/a/Bin/", "/a/bin/x.txt"));
            }
        }

        [TestMethod]
        public void PathHelpers_MacOS_IgnoresCase ()
        {
            using (PathPlatform.Simulate(ePathPlatform.MacOS))
            {
                Assert.IsTrue(PathHelpers.ArePathsMatching("/Users/a/Photos", "/users/a/photos"));
                Assert.AreEqual("/users/a/photos", PathHelpers.NormalizePath("/Users/a/Photos"));
                Assert.AreEqual("x.txt", PathHelpers.GenerateRelativePath("/a/Bin/", "/a/bin/x.txt"));
            }
        }

        [TestMethod]
        public void PathHelpers_OffWindows_BackslashIsPartOfAName ()
        {
            // On Linux, Android, iOS and macOS only '/' separates, so a\b is one name, not a folder and a name in it
            foreach (ePathPlatform platform in new[] { ePathPlatform.MacOS, ePathPlatform.CaseSensitive })
            {
                using (PathPlatform.Simulate(platform))
                {
                    Assert.IsFalse(PathHelpers.ArePathsMatching(@"/home/a\b", "/home/a/b"), platform.ToString());
                    Assert.AreEqual(@"/home/a\b", PathHelpers.NormalizePath(@"/home/a\b"), platform.ToString());
                    Assert.AreEqual(@"/home/a\..\..\x", PathHelpers.NormalizePath(@"/home/a\..\..\x"), platform.ToString());
                    Assert.AreEqual(@"../b\c/x.txt", PathHelpers.GenerateRelativePath("/a/b/", @"/a/b\c/x.txt", separatorChar: '/'), platform.ToString());

                    // One file named a\ajut.core.dll, not ajut.core.dll in a folder named a
                    Assert.IsFalse(PathHelpers.FindMatchingExtensions(@"/home/a\ajut.core.dll", "ajut.core.dll").Any(), platform.ToString());
                }
            }
        }

        [TestMethod]
        public void PathHelpers_OffWindows_DriveFollowedByABackslash_IsDriveRelative ()
        {
            // '\' isn't a separator there, so nothing separates the drive from what follows it
            using (PathPlatform.Simulate(ePathPlatform.CaseSensitive))
            {
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PathHelpers.NormalizePath(@"c:\test"));
                Assert.IsFalse(PathHelpers.IsValidAsPath(@"c:\test"));
                Assert.AreEqual("c:/test", PathHelpers.NormalizePath("c:/test"));
            }
        }

        [TestMethod]
        public void PathHelpers_PathLengthLimits_ApplyOnlyOnWindows ()
        {
            string longPath = "/" + String.Join("/", Enumerable.Repeat("folder", 50)) + "/file.txt";
            string longDirectory = "/" + new string('d', 250) + "/f";

            using (PathPlatform.Simulate(ePathPlatform.Windows))
            {
                Assert.IsFalse(PathHelpers.IsValidAsPath(longPath), "Windows: a path past MAX_PATH");
                Assert.IsFalse(PathHelpers.IsValidAsPath(longDirectory), "Windows: a directory past its limit");
            }

            foreach (ePathPlatform platform in new[] { ePathPlatform.MacOS, ePathPlatform.CaseSensitive })
            {
                using (PathPlatform.Simulate(platform))
                {
                    Result longPathValidity = PathHelpers.EvaluatePathValidity(longPath);
                    Assert.IsTrue(longPathValidity, $"{platform}: {longPathValidity.GetErrorReport()}");

                    Result longDirectoryValidity = PathHelpers.EvaluatePathValidity(longDirectory);
                    Assert.IsTrue(longDirectoryValidity, $"{platform}: {longDirectoryValidity.GetErrorReport()}");
                }
            }
        }

        [TestMethod]
        public void PathHelpers_PathLengthLimits_AreReadOnly ()
        {
            Assert.IsTrue(typeof(PathHelpers).GetField(nameof(PathHelpers.kMaxFullPathLength)).IsInitOnly, nameof(PathHelpers.kMaxFullPathLength));
            Assert.IsTrue(typeof(PathHelpers).GetField(nameof(PathHelpers.kMaxPathDirectoryLength)).IsInitOnly, nameof(PathHelpers.kMaxPathDirectoryLength));
        }

        private static void RunUnderCulture (string cultureName, Action test)
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            try
            {
                test();
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }
    }
}
