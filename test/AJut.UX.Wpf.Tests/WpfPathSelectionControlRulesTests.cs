namespace AJut.UX.Tests
{
    using System;
    using System.IO;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The WPF PathSelectionControl judging its path through the rules every stack shares (PathSelectionRules), so it gives
    /// their answer and reason. WPF objects need an STA thread, so each test runs on one, and nothing is ever shown.
    /// </summary>
    [TestClass]
    public class WpfPathSelectionControlRulesTests
    {
        [TestMethod]
        public void WpfPathSelectionControl_PathBesideTheFixedRoot_IsInvalidWithTheSharedReason ()
        {
            string root = Path.Combine(Path.GetTempPath(), "fixed-root");
            StaTestThread.Run(() =>
            {
                var control = new PathSelectionControl { FixedRootPath = root };

                control.SelectedPath = Path.Combine(root + "-beside", "notes.txt");

                Assert.IsFalse(control.IsPathValid);
                Assert.AreEqual($"Path does not indicate a location under the fixed root path (fixed root path is '{root}')", control.InvalidPathReason);
            });
        }

        [TestMethod]
        public void WpfPathSelectionControl_FilterGroupWithSeveralPatterns_TakesEachOne ()
        {
            StaTestThread.Run(() =>
            {
                var control = new PathSelectionControl { FileFilter = "Images|*.png;*.jpg" };

                control.SelectedPath = Path.Combine(Path.GetTempPath(), "photo.jpg");

                Assert.IsTrue(control.IsPathValid, control.InvalidPathReason);
            });
        }

        [TestMethod]
        public void WpfPathSelectionControl_RelativePath_ExpandsUnderTheFixedRoot ()
        {
            string root = Path.Combine(Path.GetTempPath(), "fixed-root");
            StaTestThread.Run(() =>
            {
                var control = new PathSelectionControl { FixedRootPath = root };

                control.SelectedPath = "notes.txt";

                Assert.AreEqual(Path.Combine(root, "notes.txt"), control.GetFullyExpandedPath());
                Assert.IsTrue(control.IsPathValid, control.InvalidPathReason);
            });
        }
    }
}
