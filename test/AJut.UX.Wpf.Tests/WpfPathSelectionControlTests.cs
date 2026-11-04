namespace AJut.UX.Tests
{
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The WPF PathSelectionControl's validation of a typed path. WPF objects need an STA thread, so each test runs on one,
    /// and nothing is ever shown.
    /// </summary>
    [TestClass]
    public class WpfPathSelectionControlTests
    {
        // Windows ignores case in a path, so a path under the fixed root by any casing is under it
        [TestMethod]
        public void WpfPathSelectionControl_PathUnderTheFixedRootByAnotherCase_IsValid ()
        {
            StaTestThread.Run(() =>
            {
                var control = new PathSelectionControl();
                control.FixedRootPath = @"c:\fixed-root";

                control.SelectedPath = @"C:\Fixed-Root\notes.txt";

                Assert.IsTrue(control.IsPathValid, control.InvalidPathReason);
            });
        }

        [TestMethod]
        public void WpfPathSelectionControl_PathOutsideTheFixedRoot_IsInvalid ()
        {
            StaTestThread.Run(() =>
            {
                var control = new PathSelectionControl();
                control.FixedRootPath = @"c:\fixed-root";

                control.SelectedPath = @"C:\elsewhere\notes.txt";

                Assert.IsFalse(control.IsPathValid);
                StringAssert.Contains(control.InvalidPathReason, "fixed root");
            });
        }
    }
}
