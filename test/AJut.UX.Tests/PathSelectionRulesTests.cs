namespace AJut.UX.Tests
{
    using System;
    using System.IO;
    using AJut.IO;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// What every stack's PathSelectionControl decides about a path, through <see cref="PathSelectionRules"/>. Each test
    /// gets a folder of its own under the temp folder, holding a file and a folder, so existence is checked for real.
    /// </summary>
    [TestClass]
    public class PathSelectionRulesTests
    {
        private const string kTextFilter = "Text (*.txt)|*.txt";
        private const string kFileName = "notes.txt";
        private const string kFolderName = "sub";

        private string m_root;
        private string m_file;
        private string m_folder;

        // ===========[ Setup/Construction/Teardown ]===========================
        [TestInitialize]
        public void Setup ()
        {
            m_root = Path.Combine(Path.GetTempPath(), "ajut-path-rules-" + Guid.NewGuid().ToString("N"));
            m_folder = Path.Combine(m_root, kFolderName);
            m_file = Path.Combine(m_root, kFileName);
            Directory.CreateDirectory(m_folder);
            File.WriteAllText(m_file, "notes");
        }

        [TestCleanup]
        public void Teardown ()
        {
            Directory.Delete(m_root, recursive: true);
        }

        // ===========[ Evaluate ]==============================================
        [TestMethod]
        public void Evaluate_EmptyPath_IsValidAndNotThere ()
        {
            PathSelectionRules.Evaluation evaluation = Evaluate(String.Empty);

            Assert.IsTrue(evaluation.IsPathValid);
            Assert.IsFalse(evaluation.DoesPathExist);
            Assert.IsNull(evaluation.InvalidPathReason);
        }

        [TestMethod]
        public void Evaluate_EmptyPathTreatedAsInvalid_SaysItIsEmpty ()
        {
            PathSelectionRules.Evaluation evaluation = PathSelectionRules.Evaluate(null, ePathType.File, kTextFilter, null, treatEmptyPathAsInvalid: true, treatNonExistentPathAsInvalid: false);

            Assert.IsFalse(evaluation.IsPathValid);
            Assert.AreEqual("Empty path", evaluation.InvalidPathReason);
        }

        [TestMethod]
        public void Evaluate_ExistingFileWithAFilteredExtension_IsValidAndThere ()
        {
            PathSelectionRules.Evaluation evaluation = Evaluate(m_file);

            Assert.IsTrue(evaluation.IsPathValid, evaluation.InvalidPathReason);
            Assert.IsTrue(evaluation.DoesPathExist);
        }

        [TestMethod]
        public void Evaluate_FileWithAnExtensionNotInTheFilter_ListsTheFilter ()
        {
            string path = Path.Combine(m_root, "picture.png");

            PathSelectionRules.Evaluation evaluation = Evaluate(path);

            Assert.IsFalse(evaluation.IsPathValid);
            StringAssert.StartsWith(evaluation.InvalidPathReason, path + "\r\nIndicated file path does not use one of the allowed file extensions:\r\n");
            StringAssert.EndsWith(evaluation.InvalidPathReason, "txt");
        }

        [TestMethod]
        public void Evaluate_AnyFileFilter_TakesAnyExtension ()
        {
            string path = Path.Combine(m_root, "picture.png");

            PathSelectionRules.Evaluation evaluation = PathSelectionRules.Evaluate(path, ePathType.File, PathHelpers.kAnyFileFilter, null, false, false);
            Assert.IsTrue(evaluation.IsPathValid, evaluation.InvalidPathReason);
        }

        [TestMethod]
        public void Evaluate_PlainFilterWithNoDescription_IsReadAsItsPatterns ()
        {
            // A filter can be just its patterns, as WinUI3's default *.* is
            string picture = Path.Combine(m_root, "picture.png");

            PathSelectionRules.Evaluation anyFile = PathSelectionRules.Evaluate(picture, ePathType.File, "*.*", null, false, false);
            Assert.IsTrue(anyFile.IsPathValid, anyFile.InvalidPathReason);

            PathSelectionRules.Evaluation textFile = PathSelectionRules.Evaluate(m_file, ePathType.File, "*.txt", null, false, false);
            Assert.IsTrue(textFile.IsPathValid, textFile.InvalidPathReason);
            Assert.IsFalse(PathSelectionRules.Evaluate(picture, ePathType.File, "*.txt", null, false, false).IsPathValid);
        }

        [TestMethod]
        public void Evaluate_FilterGroupWithSeveralPatterns_TakesEachOne ()
        {
            const string kImageFilter = "Images (*.png;*.jpg)|*.png;*.jpg";

            foreach (string name in new[] { "picture.png", "picture.jpg" })
            {
                PathSelectionRules.Evaluation evaluation = PathSelectionRules.Evaluate(Path.Combine(m_root, name), ePathType.File, kImageFilter, null, false, false);
                Assert.IsTrue(evaluation.IsPathValid, $"{name}: {evaluation.InvalidPathReason}");
            }
        }

        [TestMethod]
        public void Evaluate_NoFilter_TakesAnyExtension ()
        {
            string path = Path.Combine(m_root, "picture.png");

            PathSelectionRules.Evaluation evaluation = PathSelectionRules.Evaluate(path, ePathType.File, null, null, false, false);
            Assert.IsTrue(evaluation.IsPathValid, evaluation.InvalidPathReason);
        }

        [TestMethod]
        public void Evaluate_FolderPath_SkipsTheExtensionCheck ()
        {
            PathSelectionRules.Evaluation evaluation = PathSelectionRules.Evaluate(m_folder, ePathType.Folder, kTextFilter, null, false, false);

            Assert.IsTrue(evaluation.IsPathValid, evaluation.InvalidPathReason);
            Assert.IsTrue(evaluation.DoesPathExist);
        }

        [TestMethod]
        public void Evaluate_FilePathThatIsAFolder_SaysSo ()
        {
            PathSelectionRules.Evaluation evaluation = PathSelectionRules.Evaluate(m_folder, ePathType.File, PathHelpers.kAnyFileFilter, null, false, false);

            Assert.IsFalse(evaluation.IsPathValid);
            Assert.AreEqual("Looking for a file, path is an existing directory", evaluation.InvalidPathReason);
        }

        [TestMethod]
        public void Evaluate_FolderPathThatIsAFile_SaysSo ()
        {
            PathSelectionRules.Evaluation evaluation = PathSelectionRules.Evaluate(m_file, ePathType.Folder, kTextFilter, null, false, false);

            Assert.IsFalse(evaluation.IsPathValid);
            Assert.AreEqual("Looking for a directory, path is an existing file", evaluation.InvalidPathReason);
        }

        [TestMethod]
        public void Evaluate_MissingPath_IsValidUnlessMissingIsInvalid ()
        {
            string missingFile = Path.Combine(m_root, "missing.txt");
            string missingFolder = Path.Combine(m_root, "missing");

            PathSelectionRules.Evaluation allowed = Evaluate(missingFile);
            Assert.IsTrue(allowed.IsPathValid);
            Assert.IsFalse(allowed.DoesPathExist);

            PathSelectionRules.Evaluation missingFileInvalid = PathSelectionRules.Evaluate(missingFile, ePathType.File, kTextFilter, null, false, treatNonExistentPathAsInvalid: true);
            Assert.IsFalse(missingFileInvalid.IsPathValid);
            Assert.AreEqual("File does not exist", missingFileInvalid.InvalidPathReason);

            PathSelectionRules.Evaluation missingFolderInvalid = PathSelectionRules.Evaluate(missingFolder, ePathType.Folder, kTextFilter, null, false, treatNonExistentPathAsInvalid: true);
            Assert.IsFalse(missingFolderInvalid.IsPathValid);
            Assert.AreEqual("Folder does not exist", missingFolderInvalid.InvalidPathReason);
        }

        [TestMethod]
        public void Evaluate_RootedPathOutsideTheFixedRoot_SaysWhereTheRootIs ()
        {
            string elsewhere = Path.Combine(Path.GetTempPath(), "elsewhere", kFileName);

            PathSelectionRules.Evaluation evaluation = EvaluateUnderRoot(elsewhere);

            Assert.IsFalse(evaluation.IsPathValid);
            Assert.AreEqual($"Path does not indicate a location under the fixed root path (fixed root path is '{m_root}')", evaluation.InvalidPathReason);
        }

        [TestMethod]
        public void Evaluate_RelativePathWithAFixedRoot_IsLookedForUnderTheRoot ()
        {
            PathSelectionRules.Evaluation evaluation = EvaluateUnderRoot(kFileName);

            Assert.IsTrue(evaluation.IsPathValid, evaluation.InvalidPathReason);
            Assert.IsTrue(evaluation.DoesPathExist);
        }

        [TestMethod]
        public void Evaluate_PathBesideTheFixedRootThatSharesItsStart_IsOutsideIt ()
        {
            // The root's own name with more after it is a folder beside the root, not in it
            string beside = m_root + "-beside" + Path.DirectorySeparatorChar + kFileName;

            PathSelectionRules.Evaluation evaluation = EvaluateUnderRoot(beside);

            Assert.IsFalse(evaluation.IsPathValid, "A folder named like the root with more on the end is beside it, not under it");
        }

        [TestMethod]
        public void Evaluate_PathUnderTheFixedRootByAnotherCase_IsUnderIt ()
        {
            // Windows and macOS, where a typed path is ever checked, ignore case in a path
            string otherCase = Path.Combine(m_root.ToUpperInvariant(), kFileName);

            PathSelectionRules.Evaluation evaluation = EvaluateUnderRoot(otherCase);

            Assert.IsTrue(evaluation.IsPathValid, evaluation.InvalidPathReason);
        }

        // ===========[ GetFullyExpandedPath ]==================================
        [TestMethod]
        public void GetFullyExpandedPath_RelativeWithARoot_IsUnderTheRoot_AndAnythingElseIsLeftAlone ()
        {
            Assert.AreEqual(m_file, PathSelectionRules.GetFullyExpandedPath(kFileName, m_root));
            Assert.AreEqual(m_file, PathSelectionRules.GetFullyExpandedPath(m_file, Path.GetTempPath()));
            Assert.AreEqual(kFileName, PathSelectionRules.GetFullyExpandedPath(kFileName, null));
        }

        // ===========[ TryShortenToFixedRoot ]=================================
        [TestMethod]
        public void TryShortenToFixedRoot_PathUnderTheRoot_IsMadeRelativeToIt ()
        {
            Assert.IsTrue(PathSelectionRules.TryShortenToFixedRoot(m_file, m_root, out string shortened));
            Assert.AreEqual(kFileName, shortened);
        }

        [TestMethod]
        public void TryShortenToFixedRoot_NoPath_IsLeftAlone ()
        {
            Assert.IsFalse(PathSelectionRules.TryShortenToFixedRoot(null, m_root, out string shortened));
            Assert.IsNull(shortened);
        }

        [TestMethod]
        public void TryShortenToFixedRoot_TheRootItself_IsLeftAlone ()
        {
            // Shortened to nothing it would read as no path at all
            Assert.IsFalse(PathSelectionRules.TryShortenToFixedRoot(m_root, m_root, out string shortened));
            Assert.AreEqual(m_root, shortened);
        }

        [TestMethod]
        public void TryShortenToFixedRoot_PathBesideTheRootThatSharesItsStart_IsLeftAlone ()
        {
            string beside = m_root + "-beside" + Path.DirectorySeparatorChar + kFileName;

            Assert.IsFalse(PathSelectionRules.TryShortenToFixedRoot(beside, m_root, out string shortened));
            Assert.AreEqual(beside, shortened);
        }

        // ===========[ FindBrowseStartFolder ]=================================
        [TestMethod]
        public void FindBrowseStartFolder_TheFixedRootComesFirst ()
        {
            Assert.AreEqual(m_root, PathSelectionRules.FindBrowseStartFolder(Path.Combine(m_folder, kFileName), ePathType.File, null, m_root));
        }

        [TestMethod]
        public void FindBrowseStartFolder_WithNoRoot_ATheFilesFolderOrTheFolderItself ()
        {
            Assert.AreEqual(m_root, PathSelectionRules.FindBrowseStartFolder(m_file, ePathType.File, null, null));
            Assert.AreEqual(m_folder, PathSelectionRules.FindBrowseStartFolder(m_folder, ePathType.Folder, null, null));
        }

        [TestMethod]
        public void FindBrowseStartFolder_NothingSelected_UsesTheInitialBrowseRoot ()
        {
            Assert.AreEqual(m_folder, PathSelectionRules.FindBrowseStartFolder(null, ePathType.File, m_folder, null));
        }

        [TestMethod]
        public void FindBrowseStartFolder_NothingThere_IsNull ()
        {
            string missing = Path.Combine(m_root, "missing");
            Assert.IsNull(PathSelectionRules.FindBrowseStartFolder(Path.Combine(missing, kFileName), ePathType.File, null, null));
        }

        // ===========[ TryGetParentFolder ]====================================
        [TestMethod]
        public void TryGetParentFolder_RootedFolder_GivesTheFolderAboveIt ()
        {
            Assert.IsTrue(PathSelectionRules.TryGetParentFolder(m_folder, null, out string parent));
            Assert.AreEqual(m_root, parent);
        }

        [TestMethod]
        public void TryGetParentFolder_NoPath_HasNothingAboveIt ()
        {
            Assert.IsFalse(PathSelectionRules.TryGetParentFolder(String.Empty, null, out string parent));
            Assert.IsNull(parent);
        }

        [TestMethod]
        public void TryGetParentFolder_RelativeFolderWithAFixedRoot_GoesUpUnderTheRoot ()
        {
            string relative = Path.Combine(kFolderName, "inner");

            Assert.IsTrue(PathSelectionRules.TryGetParentFolder(relative, m_root, out string parent));
            Assert.AreEqual(m_folder, parent, "A relative path is under the fixed root, not under wherever the app happens to be running");
        }

        // ===========[ Helper Methods ]========================================
        private static PathSelectionRules.Evaluation Evaluate (string path)
        {
            return PathSelectionRules.Evaluate(path, ePathType.File, kTextFilter, null, treatEmptyPathAsInvalid: false, treatNonExistentPathAsInvalid: false);
        }

        private PathSelectionRules.Evaluation EvaluateUnderRoot (string path)
        {
            return PathSelectionRules.Evaluate(path, ePathType.File, kTextFilter, m_root, treatEmptyPathAsInvalid: false, treatNonExistentPathAsInvalid: false);
        }
    }
}
