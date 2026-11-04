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

        // ===[ Nudging an integer type ]===

        [TestMethod]
        public void NEVM_IntegerNudge_FractionalStepRoundsAwayFromZero_NeverBelowOne ()
        {
            // The default SmallNudge is 0.5, so on an integer editor a Shift-nudge has to move by a whole step
            var vm = new NumericEditorViewModel(new TestSettings(), 5);

            vm.Nudge(positive: true, 0.5);
            Assert.AreEqual(6, vm.SourceValue, "A 0.5 nudge up on an int must step by 1, not round to a zero step");

            vm.Nudge(positive: false, 0.5);
            Assert.AreEqual(5, vm.SourceValue, "A 0.5 nudge down on an int must step by 1");

            vm.Nudge(positive: true, 0.2);
            Assert.AreEqual(6, vm.SourceValue, "A step that rounds to nothing still moves an int by 1");

            vm.Nudge(positive: true, 2.5);
            Assert.AreEqual(9, vm.SourceValue, "2.5 rounds away from zero to a step of 3, not to even (2)");
        }

        [TestMethod]
        public void NEVM_FloatNudge_FractionalStepIsKept ()
        {
            var vm = new NumericEditorViewModel(new TestSettings(), 5.0f);

            vm.Nudge(positive: true, 0.5);

            Assert.AreEqual(5.5f, vm.SourceValue);
        }

        // ===[ Types and bounds, pinned ahead of the move off dynamic ]===

        [TestMethod]
        public void NEVM_Decimal_KeepsItsType_ThroughTextAndNudge ()
        {
            var vm = new NumericEditorViewModel(new TestSettings { Minimum = 0.0, Maximum = 10.0 }, 2.5m);

            vm.Nudge(positive: true, 0.25);
            Assert.AreEqual(2.75m, vm.SourceValue);

            vm.Text = "12.5";
            Assert.AreEqual(10m, vm.SourceValue, "Capped to the maximum, still a decimal");
            Assert.IsTrue(vm.IsAtMaximum);
            Assert.IsNotNull(vm.TextErrorMessage);
        }

        [TestMethod]
        public void NEVM_Byte_NudgePastItsOwnRange_CapsAtTheTypeBound ()
        {
            var vm = new NumericEditorViewModel(new TestSettings(), (byte)250);

            vm.Nudge(positive: true, 10.0);
            Assert.AreEqual((byte)255, vm.SourceValue);
            Assert.IsTrue(vm.IsAtMaximum);

            // The 300 step is itself cast to a byte (255) before it is applied, so 255 - 255 lands exactly on 0 without
            // needing a cap, and IsAtMinimum (which means "was capped at the minimum") stays false
            vm.Nudge(positive: false, 300.0);
            Assert.AreEqual((byte)0, vm.SourceValue);
            Assert.IsFalse(vm.IsAtMinimum);
        }

        [TestMethod]
        public void NEVM_Long_TextBelowMinimum_ReportsTheError ()
        {
            var vm = new NumericEditorViewModel(new TestSettings { Minimum = -5.0 }, 0L);

            vm.Text = "-20";

            Assert.AreEqual(-5L, vm.SourceValue);
            Assert.IsTrue(vm.IsAtMinimum);
            Assert.IsTrue(vm.TextErrorMessage.Contains("below the min"));
        }

        [TestMethod]
        public void NEVM_StringValue_IsParsedAsFloat ()
        {
            var vm = new NumericEditorViewModel(new TestSettings(), "3.5");

            Assert.AreEqual(3.5f, vm.SourceValue);
            Assert.AreEqual(typeof(float), vm.ValueType);
        }

        [TestMethod]
        public void NEVM_EnforcedType_WinsOverTheValuesType ()
        {
            var vm = new NumericEditorViewModel(new TestSettings(), 4, typeof(double));

            vm.Text = "4.25";

            Assert.AreEqual(4.25, vm.SourceValue);
            Assert.AreEqual(typeof(double), vm.ValueType);
        }

        [TestMethod]
        public void NEVM_Short_NudgesBelowZero ()
        {
            var vm = new NumericEditorViewModel(new TestSettings(), (short)0);

            vm.Nudge(positive: false, 1.0);

            Assert.AreEqual((short)-1, vm.SourceValue, "A short is signed, so a step down from zero goes negative");
            Assert.IsFalse(vm.IsAtMinimum);
        }

        // ===[ Fractional text on an integer type ]===

        [TestMethod]
        public void NEVM_Integer_FractionalText_IsRefusedLoudly ()
        {
            var vm = new NumericEditorViewModel(new TestSettings(), -43);

            vm.Text = "-43.5";

            Assert.AreEqual("-43.5", vm.Text, "The typed text stays, so the user sees what was refused");
            Assert.IsTrue(vm.IsTextInErrorState);
            Assert.AreEqual(-43, vm.SourceValue, "The real value does not take the text's whole part");
        }

        [TestMethod]
        public void NEVM_Integer_FractionalText_EndingTheEditPutsBackTheRealValue ()
        {
            var vm = new NumericEditorViewModel(new TestSettings(), -43);
            vm.Text = "-43.5";

            vm.CommitEdit();

            Assert.AreEqual("-43", vm.Text);
            Assert.IsFalse(vm.IsTextInErrorState);
            Assert.AreEqual(-43, vm.SourceValue);
        }

        [TestMethod]
        public void NEVM_Integer_FractionalText_ANudgeStepsTheRealValueAndClearsTheError ()
        {
            var vm = new NumericEditorViewModel(new TestSettings(), -43);
            vm.Text = "-43.5";

            vm.Nudge(positive: true, 1.0);

            Assert.AreEqual(-42, vm.SourceValue);
            Assert.AreEqual("-42", vm.Text);
            Assert.IsFalse(vm.IsTextInErrorState);
        }

        [TestMethod]
        public void NEVM_Integer_FractionalText_ANudgeDownStepsTheRealValueToo ()
        {
            var vm = new NumericEditorViewModel(new TestSettings(), -43);
            vm.Text = "-43.5";

            vm.Nudge(positive: false, 1.0);

            Assert.AreEqual(-44, vm.SourceValue);
            Assert.AreEqual("-44", vm.Text);
            Assert.IsFalse(vm.IsTextInErrorState);
        }

        [TestMethod]
        public void NEVM_Integer_FractionalText_ANudgeThatCannotMoveStillPutsBackTheRealValue ()
        {
            // At the maximum, a nudge up leaves the value where it is, and the refused text still has to go
            var vm = new NumericEditorViewModel(new TestSettings { Maximum = 10.0 }, 10);
            vm.Text = "9.5";

            vm.Nudge(positive: true, 1.0);

            Assert.AreEqual(10, vm.SourceValue);
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
