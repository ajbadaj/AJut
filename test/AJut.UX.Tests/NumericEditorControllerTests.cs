#nullable enable

namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NumericEditorControllerTests
    {
        // ===[ Value changes ]===

        [TestMethod]
        public void NEC_ValueChanged_ShowsItsText_AndKeepsItsType ()
        {
            var host = new TestHost();
            var controller = new NumericEditorController(host);

            controller.OnValueChanged(42);

            Assert.AreEqual("42", host.LastText);
            Assert.IsInstanceOfType(controller.Value, typeof(int));
            Assert.IsNull(host.LastError);
            Assert.AreEqual(0, host.Values.Count, "A Value change from the host is not written back to it");
        }

        [TestMethod]
        public void NEC_ValueChanged_Null_IsTreatedAsZero ()
        {
            var host = new TestHost();
            var controller = new NumericEditorController(host);

            controller.OnValueChanged(null);

            Assert.AreEqual(0.0, controller.Value);
            Assert.AreEqual("0", host.LastText);
        }

        [TestMethod]
        public void NEC_ValueChanged_ANewType_IsDetectedAgain ()
        {
            var host = new TestHost();
            var controller = new NumericEditorController(host);
            controller.OnValueChanged(1.5f);

            controller.OnValueChanged((short)7);

            Assert.IsInstanceOfType(controller.Value, typeof(short));
        }

        [TestMethod]
        public void NEC_ValueChanged_AsText_IsWrittenBackAsTheNumberItParsedTo ()
        {
            var host = new TestHost();
            var controller = new NumericEditorController(host);

            controller.OnValueChanged("3.5");

            Assert.AreEqual(3.5f, host.LastValue, "Text with no enforced type parses as a float");
            Assert.AreEqual("3.5", host.LastText);
        }

        // ===[ Enforced type ]===

        [TestMethod]
        public void NEC_EnforcedType_ValueAsText_ParsesAsIt_AndIsWrittenBack ()
        {
            var host = new TestHost();
            var controller = new NumericEditorController(host);
            controller.OnEnforcedTypeChanged(typeof(int));

            controller.OnValueChanged("42");

            Assert.AreEqual(42, controller.Value);
            Assert.AreEqual(42, host.LastValue);
        }

        [TestMethod]
        public void NEC_EnforcedType_SetAfterTheValue_ConvertsIt_AndWritesItBack ()
        {
            var host = new TestHost();
            var controller = new NumericEditorController(host);
            controller.OnValueChanged("42");

            controller.OnEnforcedTypeChanged(typeof(int));

            Assert.AreEqual(42, controller.Value);
            Assert.AreEqual(42, host.LastValue);
            Assert.AreEqual("42", host.LastText);
        }

        [TestMethod]
        public void NEC_EnforcedType_ANumberOfAnotherType_IsKeptInIt_ThroughEdits ()
        {
            var host = new TestHost();
            var controller = new NumericEditorController(host);
            controller.OnEnforcedTypeChanged(typeof(double));
            controller.OnValueChanged(1);

            Assert.AreEqual(1.0, controller.Value);
            Assert.AreEqual(1.0, host.LastValue, "The converted value is written back to the host at once");

            controller.OnTextEdited("2.5");
            Assert.AreEqual(2.5, host.LastValue);

            controller.Nudge(positive: true, amount: 1.0);
            Assert.AreEqual(3.5, host.LastValue);
        }

        [TestMethod]
        public void NEC_EnforcedType_Cleared_DetectsTheTypeAgain ()
        {
            var host = new TestHost();
            var controller = new NumericEditorController(host);
            controller.OnEnforcedTypeChanged(typeof(int));
            controller.OnValueChanged(5);

            controller.OnEnforcedTypeChanged(null);
            controller.OnValueChanged(1.5f);

            Assert.AreEqual(1.5f, controller.Value);
        }

        // ===[ Text edits ]===

        [TestMethod]
        public void NEC_TextEdited_InRange_WritesTheTypedValue ()
        {
            var host = new TestHost();
            var controller = new NumericEditorController(host);
            controller.OnValueChanged(1);

            controller.OnTextEdited("12");

            Assert.AreEqual(12, host.LastValue);
            Assert.IsNull(host.LastError);
        }

        [TestMethod]
        public void NEC_TextEdited_BeforeAnyValue_StartsFromADouble ()
        {
            var host = new TestHost();
            var controller = new NumericEditorController(host);

            controller.OnTextEdited("2.5");

            Assert.AreEqual(2.5, host.LastValue);
        }

        [TestMethod]
        public void NEC_TextEdited_AboveMax_CapsTheValue_AndShowsTheError ()
        {
            var host = new TestHost { Maximum = 10.0 };
            var controller = new NumericEditorController(host);
            controller.OnValueChanged(5);

            controller.OnTextEdited("5500");

            Assert.AreEqual(10, host.LastValue);
            Assert.IsNotNull(host.LastError);
        }

        [TestMethod]
        public void NEC_TextEdited_AboveMax_FixOnCommit_ShowsNoError ()
        {
            var host = new TestHost { Maximum = 10.0, OutOfBoundsResponse = eOutOfBoundsResponse.FixOnCommit };
            var controller = new NumericEditorController(host);
            controller.OnValueChanged(5);

            controller.OnTextEdited("5500");

            Assert.AreEqual(10, host.LastValue);
            Assert.IsNull(host.LastError);
        }

        // ===[ Commit ]===

        [TestMethod]
        public void NEC_Commit_FixOnCommit_SnapsTheTextToTheClampedValue ()
        {
            var host = new TestHost { Maximum = 10.0, OutOfBoundsResponse = eOutOfBoundsResponse.FixOnCommit };
            var controller = new NumericEditorController(host);
            controller.OnValueChanged(5);
            controller.OnTextEdited("5500");

            controller.Commit();

            Assert.AreEqual("10", host.LastText);
            Assert.AreEqual(10, host.LastValue);
        }

        [TestMethod]
        public void NEC_Commit_ErrorAndToolTip_LeavesTheTextFlagged ()
        {
            var host = new TestHost { Maximum = 10.0 };
            var controller = new NumericEditorController(host);
            controller.OnValueChanged(5);
            controller.OnTextEdited("5500");

            controller.Commit();

            Assert.AreEqual("5500", host.LastText);
            Assert.AreEqual(10, host.LastValue);
            Assert.IsNotNull(host.LastError);
        }

        [TestMethod]
        public void NEC_Commit_Twice_IsHarmless ()
        {
            var host = new TestHost { Maximum = 10.0, OutOfBoundsResponse = eOutOfBoundsResponse.FixOnCommit };
            var controller = new NumericEditorController(host);
            controller.OnValueChanged(5);
            controller.OnTextEdited("5500");

            controller.Commit();
            controller.Commit();

            Assert.AreEqual("10", host.LastText);
            Assert.AreEqual(10, host.LastValue);
            Assert.IsNull(host.LastError);
        }

        [TestMethod]
        public void NEC_Commit_BeforeAnyValue_DoesNothing ()
        {
            var host = new TestHost();
            var controller = new NumericEditorController(host);

            controller.Commit();

            Assert.AreEqual(0, host.Texts.Count);
            Assert.AreEqual(0, host.Values.Count);
            Assert.AreEqual(0, host.Errors.Count);
        }

        // ===[ Nudge ]===

        [TestMethod]
        public void NEC_Nudge_StepsTheValue_AndShowsIt ()
        {
            var host = new TestHost();
            var controller = new NumericEditorController(host);
            controller.OnValueChanged(1.0f);

            controller.Nudge(positive: true, 1.0);

            Assert.AreEqual(2.0f, host.LastValue);
            Assert.AreEqual("2", host.LastText);
        }

        [TestMethod]
        public void NEC_Nudge_Down_IsCappedAtTheMinimum ()
        {
            var host = new TestHost { Minimum = 0.0 };
            var controller = new NumericEditorController(host);
            controller.OnValueChanged(2);

            controller.Nudge(positive: false, 5.0);

            Assert.AreEqual(0, host.LastValue);
        }

        [TestMethod]
        public void NEC_Nudge_BeforeAnyValue_StartsFromZero ()
        {
            var host = new TestHost();
            var controller = new NumericEditorController(host);

            controller.Nudge(positive: true, 1.0);

            Assert.AreEqual(1.0, host.LastValue);
        }

        // ===[ Re-entrancy ]===

        [TestMethod]
        public void NEC_HostEchoes_WhileThePushIsInFlight_AreIgnored ()
        {
            // A WinUI3 host's Value DP raises its change handler for the controller's own write, and an Android host's
            // text field raises TextChanged for a programmatic set. Both route straight back into the controller.
            var host = new TestHost();
            var controller = new NumericEditorController(host);
            host.OnShowText = controller.OnTextEdited;
            host.OnShowValue = controller.OnValueChanged;
            controller.OnValueChanged(3);
            host.Values.Clear();

            controller.Nudge(positive: true, 1.0);

            Assert.AreEqual(1, host.Values.Count, "The echoed text edit must not write the value a second time");
            Assert.IsInstanceOfType(controller.Value, typeof(int), "The echoed Value change must not rebuild the view model");
            Assert.IsFalse(controller.IsPushing);
        }

        // ===[ Display refresh ]===

        [TestMethod]
        public void NEC_RefreshDisplay_BeforeAnyValue_ShowsNoText ()
        {
            var host = new TestHost();
            var controller = new NumericEditorController(host);

            controller.RefreshDisplay();

            Assert.AreEqual(0, host.Texts.Count);
            Assert.AreEqual(1, host.Errors.Count);
            Assert.IsNull(host.LastError);
        }

        [TestMethod]
        public void NEC_RefreshDisplay_AfterAValue_ShowsItsTextAgain ()
        {
            var host = new TestHost();
            var controller = new NumericEditorController(host);
            controller.OnValueChanged(8);
            host.Texts.Clear();

            controller.RefreshDisplay();

            CollectionAssert.AreEqual(new[] { "8" }, host.Texts);
        }

        [TestMethod]
        public void NEC_RefreshErrorState_FollowsAChangedResponse ()
        {
            var host = new TestHost { Maximum = 10.0 };
            var controller = new NumericEditorController(host);
            controller.OnValueChanged(5);
            controller.OnTextEdited("5500");
            Assert.IsNotNull(host.LastError);

            host.OutOfBoundsResponse = eOutOfBoundsResponse.FixOnCommit;
            controller.RefreshErrorState();

            Assert.IsNull(host.LastError);
        }

        // ===[ Static helpers ]===

        [TestMethod]
        public void NEC_Bounds_OnlyTheirOwnSidesInfinityIsUnconstrained ()
        {
            // The same mapping WinUI3's NumericEditor always had: -inf is an open Minimum and +inf an open Maximum, and
            // the opposite infinity on either side is passed through as a value
            Assert.IsNull(NumericEditorController.ToMinimumBound(double.NegativeInfinity));
            Assert.AreEqual(double.PositiveInfinity, NumericEditorController.ToMinimumBound(double.PositiveInfinity));
            Assert.AreEqual(3.5, NumericEditorController.ToMinimumBound(3.5));

            Assert.IsNull(NumericEditorController.ToMaximumBound(double.PositiveInfinity));
            Assert.AreEqual(double.NegativeInfinity, NumericEditorController.ToMaximumBound(double.NegativeInfinity));
            Assert.AreEqual(3.5, NumericEditorController.ToMaximumBound(3.5));
        }

        [TestMethod]
        public void NEC_PickNudgeAmount_BigWinsOverSmall ()
        {
            Assert.AreEqual(1.0, NumericEditorController.PickNudgeAmount(false, false, 1.0, 5.0, 0.5));
            Assert.AreEqual(5.0, NumericEditorController.PickNudgeAmount(true, false, 1.0, 5.0, 0.5));
            Assert.AreEqual(0.5, NumericEditorController.PickNudgeAmount(false, true, 1.0, 5.0, 0.5));
            Assert.AreEqual(5.0, NumericEditorController.PickNudgeAmount(true, true, 1.0, 5.0, 0.5));
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class TestHost : INumericEditorHost
        {
            public double Minimum { get; set; } = double.NegativeInfinity;
            public double Maximum { get; set; } = double.PositiveInfinity;
            public eOutOfBoundsResponse OutOfBoundsResponse { get; set; } = eOutOfBoundsResponse.ErrorAndToolTip;

            public List<string?> Texts { get; } = new List<string?>();
            public List<object> Values { get; } = new List<object>();
            public List<string?> Errors { get; } = new List<string?>();

            public string? LastText => this.Texts.Count == 0 ? null : this.Texts[this.Texts.Count - 1];
            public object? LastValue => this.Values.Count == 0 ? null : this.Values[this.Values.Count - 1];
            public string? LastError => this.Errors.Count == 0 ? null : this.Errors[this.Errors.Count - 1];

            /// <summary>Runs after each ShowText or ShowValue, the way a host's own change event would.</summary>
            public Action<string?>? OnShowText { get; set; }
            public Action<object?>? OnShowValue { get; set; }

            int INumericEditorSettings.DecimalPlacesAllowed => -1;
            // INumericEditorSettings has no nullable annotations, and null is its "unconstrained"
#nullable disable
            object INumericEditorSettings.Minimum => NumericEditorController.ToMinimumBound(this.Minimum);
            object INumericEditorSettings.Maximum => NumericEditorController.ToMaximumBound(this.Maximum);
#nullable enable

            public void ShowText (string? text)
            {
                this.Texts.Add(text);
                this.OnShowText?.Invoke(text);
            }

            public void ShowValue (object value)
            {
                this.Values.Add(value);
                this.OnShowValue?.Invoke(value);
            }

            public void ShowError (string? errorMessage)
            {
                this.Errors.Add(errorMessage);
            }
        }
    }
}
