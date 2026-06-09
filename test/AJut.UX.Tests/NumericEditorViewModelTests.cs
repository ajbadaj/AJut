namespace AJut.UX.Tests
{
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NumericEditorViewModelTests
    {
        // ===[ Value is always clamped, regardless of response mode ]===

        [TestMethod]
        public void NEVM_AboveMaxText_ClampsSourceValueImmediately ()
        {
            var vm = new NumericEditorViewModel(new TestSettings { Maximum = 10.0 }, 0.0f);

            vm.Text = "5500";
            Assert.AreEqual(10.0f, vm.SourceValue, "Source value should be capped to the max as text is entered");
            Assert.AreEqual("5500", vm.Text, "The entered text lingers until the edit is committed");
        }

        // ===[ ErrorAndToolTip (default) ]===

        [TestMethod]
        public void NEVM_ErrorAndToolTip_ShowsErrorAndLeavesTextOnCommit ()
        {
            var vm = new NumericEditorViewModel(new TestSettings { Maximum = 10.0, OutOfBoundsResponse = eOutOfBoundsResponse.ErrorAndToolTip }, 0.0f);

            vm.Text = "5500";
            Assert.IsTrue(vm.IsTextInErrorState);
            Assert.IsTrue(vm.ShouldShowError, "ErrorAndToolTip should surface the error while out of bounds");

            vm.CommitEdit();
            Assert.AreEqual("5500", vm.Text, "ErrorAndToolTip must leave the out of bounds text in place on commit");
            Assert.IsTrue(vm.ShouldShowError, "The error should persist after commit until the user corrects it");
            Assert.AreEqual(10.0f, vm.SourceValue);
        }

        // ===[ FixOnCommit ]===

        [TestMethod]
        public void NEVM_FixOnCommit_SuppressesErrorAndSnapsTextOnCommit ()
        {
            var vm = new NumericEditorViewModel(new TestSettings { Maximum = 10.0, OutOfBoundsResponse = eOutOfBoundsResponse.FixOnCommit }, 0.0f);

            vm.Text = "5500";
            Assert.IsTrue(vm.IsTextInErrorState, "The raw error state is still tracked internally");
            Assert.IsFalse(vm.ShouldShowError, "FixOnCommit should not surface the error to the user");

            vm.CommitEdit();
            Assert.AreEqual("10", vm.Text, "FixOnCommit should snap the text to the clamped value on commit");
            Assert.AreEqual(10.0f, vm.SourceValue);
            Assert.IsFalse(vm.ShouldShowError);
            Assert.IsFalse(vm.IsTextInErrorState, "Once the text matches the clamped value the error clears");
        }

        [TestMethod]
        public void NEVM_FixOnCommit_SnapsBelowMinTextOnCommit ()
        {
            var vm = new NumericEditorViewModel(new TestSettings { Minimum = 5.0, OutOfBoundsResponse = eOutOfBoundsResponse.FixOnCommit }, 20.0f);

            vm.Text = "2";
            Assert.AreEqual(5.0f, vm.SourceValue);

            vm.CommitEdit();
            Assert.AreEqual("5", vm.Text);
            Assert.IsFalse(vm.IsTextInErrorState);
        }

        // ===[ In range entries are untouched in either mode ]===

        [TestMethod]
        public void NEVM_InRangeValue_NoErrorAndUnchangedOnCommit ()
        {
            var vm = new NumericEditorViewModel(new TestSettings { Minimum = 0.0, Maximum = 10.0, OutOfBoundsResponse = eOutOfBoundsResponse.FixOnCommit }, 0.0f);

            vm.Text = "7";
            Assert.AreEqual(7.0f, vm.SourceValue);
            Assert.IsFalse(vm.ShouldShowError);

            vm.CommitEdit();
            Assert.AreEqual("7", vm.Text, "An in range value should not be altered by commit");
            Assert.AreEqual(7.0f, vm.SourceValue);
        }

        // ===[ ResyncTextToSourceValue primitive ]===

        [TestMethod]
        public void NEVM_ResyncTextToSourceValue_SnapsTextRegardlessOfMode ()
        {
            // The primitive always reconciles - the mode only governs whether CommitEdit calls it.
            var vm = new NumericEditorViewModel(new TestSettings { Maximum = 10.0, OutOfBoundsResponse = eOutOfBoundsResponse.ErrorAndToolTip }, 0.0f);

            vm.Text = "5500";
            vm.ResyncTextToSourceValue();
            Assert.AreEqual("10", vm.Text);
            Assert.IsFalse(vm.IsTextInErrorState);
        }

        // ===[ Test settings ]====================================================

        private class TestSettings : INumericEditorSettings
        {
            public int DecimalPlacesAllowed { get; set; } = -1;
            public object Minimum { get; set; }
            public object Maximum { get; set; }
            public eOutOfBoundsResponse OutOfBoundsResponse { get; set; } = eOutOfBoundsResponse.ErrorAndToolTip;
        }
    }
}
