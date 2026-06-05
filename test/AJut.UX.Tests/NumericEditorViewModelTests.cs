namespace AJut.UX.Tests
{
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NumericEditorViewModelTests
    {
        // ===[ ResyncTextToSourceValue - clamp reconciliation ]===

        [TestMethod]
        public void NEVM_ResyncTextToSourceValue_SnapsAboveMaxTextToClampedValue ()
        {
            var settings = new TestSettings { Maximum = 10.0 };
            var vm = new NumericEditorViewModel(settings, 0.0f);

            // Typing past the max caps the source value immediately, but the entered text lingers.
            vm.Text = "5500";
            Assert.AreEqual(10.0f, vm.SourceValue, "Source value should be capped to the max as text is entered");
            Assert.AreEqual("5500", vm.Text, "Entered text lingers until the edit is committed");
            Assert.IsTrue(vm.IsTextInErrorState, "Out of range text should flag an error before commit");

            vm.ResyncTextToSourceValue();
            Assert.AreEqual("10", vm.Text, "Commit should snap the text to the clamped value");
            Assert.AreEqual(10.0f, vm.SourceValue);
            Assert.IsFalse(vm.IsTextInErrorState, "Error should clear once the text matches the clamped value");
        }

        [TestMethod]
        public void NEVM_ResyncTextToSourceValue_SnapsBelowMinTextToClampedValue ()
        {
            var settings = new TestSettings { Minimum = 5.0 };
            var vm = new NumericEditorViewModel(settings, 20.0f);

            vm.Text = "2";
            Assert.AreEqual(5.0f, vm.SourceValue, "Source value should be capped to the min as text is entered");
            Assert.AreEqual("2", vm.Text);
            Assert.IsTrue(vm.IsTextInErrorState);

            vm.ResyncTextToSourceValue();
            Assert.AreEqual("5", vm.Text);
            Assert.AreEqual(5.0f, vm.SourceValue);
            Assert.IsFalse(vm.IsTextInErrorState);
        }

        [TestMethod]
        public void NEVM_ResyncTextToSourceValue_LeavesInRangeValueUnchanged ()
        {
            var settings = new TestSettings { Minimum = 0.0, Maximum = 10.0 };
            var vm = new NumericEditorViewModel(settings, 0.0f);

            vm.Text = "7";
            Assert.AreEqual(7.0f, vm.SourceValue);
            Assert.IsFalse(vm.IsTextInErrorState);

            vm.ResyncTextToSourceValue();
            Assert.AreEqual("7", vm.Text, "An in range value should not be altered by commit");
            Assert.AreEqual(7.0f, vm.SourceValue);
        }

        // ===[ Test settings ]====================================================

        private class TestSettings : INumericEditorSettings
        {
            public int DecimalPlacesAllowed { get; set; } = -1;
            public object Minimum { get; set; }
            public object Maximum { get; set; }
        }
    }
}
