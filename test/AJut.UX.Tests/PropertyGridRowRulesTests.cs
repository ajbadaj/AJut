namespace AJut.UX.Tests
{
    using System;
    using System.Linq;
    using AJut.UX.PropertyInteraction;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public class NullableSampleSource
    {
        public float? Scale { get; set; }
        public string Label { get; set; } = "label";
    }

    [TestClass]
    public class PropertyGridRowRulesTests
    {
        // ===========[ Editor keys ]===========================================
        [TestMethod]
        public void PGRowRules_EditorKey_IsTheEditorOrInvalid ()
        {
            Assert.AreEqual("Text", PropertyGridRowRules.GetEditorKey(new PropertyEditTarget("A", () => null) { Editor = "Text" }));
            Assert.AreEqual(PropertyGridRowRules.kInvalidEditorKey, PropertyGridRowRules.GetEditorKey(new PropertyEditTarget("A", () => null) { Editor = null }));
        }

        [TestMethod]
        public void PGRowRules_BuiltInEditor_AppliesOnlyWhenNotRegistered ()
        {
            var nullable = new PropertyEditTarget("A", () => null) { Editor = PropertyGridRowRules.kNullableEditorKey };
            var button = new PropertyEditTarget("B", () => null) { Editor = PropertyGridRowRules.kButtonEditorKey };
            var list = new PropertyEditTarget("C", () => null) { Editor = PropertyGridRowRules.kListEditorKey };
            var text = new PropertyEditTarget("D", () => null) { Editor = "Text" };

            Assert.AreEqual(eBuiltInPropertyEditor.Nullable, PropertyGridRowRules.GetBuiltInEditor(nullable, _NothingRegistered));
            Assert.AreEqual(eBuiltInPropertyEditor.Button, PropertyGridRowRules.GetBuiltInEditor(button, _NothingRegistered));
            Assert.AreEqual(eBuiltInPropertyEditor.List, PropertyGridRowRules.GetBuiltInEditor(list, _NothingRegistered));
            Assert.AreEqual(eBuiltInPropertyEditor.None, PropertyGridRowRules.GetBuiltInEditor(text, _NothingRegistered));

            Assert.AreEqual(eBuiltInPropertyEditor.None, PropertyGridRowRules.GetBuiltInEditor(nullable, _EverythingRegistered));
            Assert.AreEqual(eBuiltInPropertyEditor.None, PropertyGridRowRules.GetBuiltInEditor(button, _EverythingRegistered));
            Assert.AreEqual(eBuiltInPropertyEditor.None, PropertyGridRowRules.GetBuiltInEditor(list, _EverythingRegistered));

            static bool _NothingRegistered (string key) => false;
            static bool _EverythingRegistered (string key) => true;
        }

        // ===========[ Labels ]================================================
        [TestMethod]
        public void PGRowRules_LabelWidth_TakesOffIndentAndExpander ()
        {
            Assert.AreEqual(150.0, PropertyGridRowRules.ComputeLabelWidth(200.0, 32.0, 18.0), 0.0001);
        }

        [TestMethod]
        public void PGRowRules_LabelWidth_NeverNegative ()
        {
            Assert.AreEqual(0.0, PropertyGridRowRules.ComputeLabelWidth(40.0, 32.0, 18.0), 0.0001);
        }

        [TestMethod]
        public void PGRowRules_LabelWidth_NaNMeansSizeToContent ()
        {
            Assert.IsTrue(double.IsNaN(PropertyGridRowRules.ComputeLabelWidth(double.NaN, 32.0, 18.0)));
        }

        // ===========[ Label splitter ]========================================
        [TestMethod]
        public void PGRowRules_ToLabelColumnWidth_ReversesComputeLabelWidth ()
        {
            double labelWidth = PropertyGridRowRules.ComputeLabelWidth(200.0, 32.0, 18.0);
            Assert.AreEqual(200.0, PropertyGridRowRules.ToLabelColumnWidth(labelWidth, 32.0, 18.0), 0.0001);
        }

        [TestMethod]
        public void PGRowRules_SplitterHit_WithinTenOnTheLabelSide ()
        {
            Assert.IsTrue(PropertyGridRowRules.IsNearLabelBoundary(190.0, 200.0));
            Assert.IsTrue(PropertyGridRowRules.IsNearLabelBoundary(200.0, 200.0));
            Assert.IsFalse(PropertyGridRowRules.IsNearLabelBoundary(189.0, 200.0));
        }

        [TestMethod]
        public void PGRowRules_SplitterHit_NoneOnTheEditorSide ()
        {
            // Editors take clicks and labels don't, so a press just past the boundary belongs to the editor
            Assert.IsFalse(PropertyGridRowRules.IsNearLabelBoundary(201.0, 200.0), "Just past the boundary");
            Assert.IsFalse(PropertyGridRowRules.IsNearLabelBoundary(210.0, 200.0), "Ten into the editor");
        }

        [TestMethod]
        public void PGRowRules_SplitterDrag_MovesTheWidthByThePointer ()
        {
            Assert.AreEqual(230.0, PropertyGridRowRules.ComputeDraggedLabelColumnWidth(200.0, 30.0, 600.0), 0.0001);
            Assert.AreEqual(170.0, PropertyGridRowRules.ComputeDraggedLabelColumnWidth(200.0, -30.0, 600.0), 0.0001);
        }

        [TestMethod]
        public void PGRowRules_SplitterDrag_KeepsANarrowColumnAndRoomForEditors ()
        {
            Assert.AreEqual(40.0, PropertyGridRowRules.ComputeDraggedLabelColumnWidth(200.0, -500.0, 600.0), 0.0001, "Never narrower than the minimum column");
            Assert.AreEqual(540.0, PropertyGridRowRules.ComputeDraggedLabelColumnWidth(200.0, 500.0, 600.0), 0.0001, "Always leaves the editors their minimum");
            Assert.AreEqual(700.0, PropertyGridRowRules.ComputeDraggedLabelColumnWidth(200.0, 500.0, double.NaN), 0.0001, "An unknown width puts no upper limit on it");
            Assert.AreEqual(20.0, PropertyGridRowRules.ComputeDraggedLabelColumnWidth(200.0, 0.0, 80.0), 0.0001, "In a grid too narrow for both, the editors' room wins");
        }

        [TestMethod]
        public void PGRowRules_SplitterFit_TakesTheWidestRow ()
        {
            Assert.AreEqual(260.0, PropertyGridRowRules.FitLabelColumnWidth(new[] { 120.0, 260.0, 90.0 }, 600.0), 0.0001);
            Assert.AreEqual(540.0, PropertyGridRowRules.FitLabelColumnWidth(new[] { 900.0 }, 600.0), 0.0001, "Fit keeps to the same limits as a drag");
            Assert.IsTrue(double.IsNaN(PropertyGridRowRules.FitLabelColumnWidth(Array.Empty<double>(), 600.0)), "No rows means nothing to fit");
        }

        [TestMethod]
        public void PGRowRules_DefaultValueLabel_ForDefaultsExpandablesAndNoTarget ()
        {
            var manager = new PropertyGridManager(new SimpleTestPropertyGrid { SingleItemSource = new DefaultedValueSource() });
            manager.RebuildEditTargets();
            PropertyEditTarget size = manager.Items.First(t => t.PropertyPathTarget == nameof(DefaultedValueSource.Size));
            PropertyEditTarget child = manager.Items.First(t => t.PropertyPathTarget == nameof(DefaultedValueSource.Child));

            // Size is 7 against a default of 5, so it shows modified; the expandable child never does
            Assert.IsFalse(PropertyGridRowRules.UsesDefaultValueLabel(size));
            Assert.IsTrue(PropertyGridRowRules.UsesDefaultValueLabel(child));
            Assert.IsTrue(PropertyGridRowRules.UsesDefaultValueLabel(null));

            size.ResetToDefault();
            Assert.IsTrue(PropertyGridRowRules.UsesDefaultValueLabel(size));
        }

        // ===========[ Tooltips ]==============================================
        [TestMethod]
        public void PGRowRules_ToolTipPlacement_NameAndValue ()
        {
            Assert.IsFalse(PropertyGridRowRules.ShowsToolTipOnName(ePropertyToolTipPlacement.None));
            Assert.IsFalse(PropertyGridRowRules.ShowsToolTipOnValue(ePropertyToolTipPlacement.None));

            Assert.IsTrue(PropertyGridRowRules.ShowsToolTipOnName(ePropertyToolTipPlacement.PropertyNameOnly));
            Assert.IsFalse(PropertyGridRowRules.ShowsToolTipOnValue(ePropertyToolTipPlacement.PropertyNameOnly));

            Assert.IsFalse(PropertyGridRowRules.ShowsToolTipOnName(ePropertyToolTipPlacement.ValueOnly));
            Assert.IsTrue(PropertyGridRowRules.ShowsToolTipOnValue(ePropertyToolTipPlacement.ValueOnly));

            Assert.IsTrue(PropertyGridRowRules.ShowsToolTipOnName(ePropertyToolTipPlacement.PropertyNameAndValue));
            Assert.IsTrue(PropertyGridRowRules.ShowsToolTipOnValue(ePropertyToolTipPlacement.PropertyNameAndValue));
        }

        // ===========[ Nullable proxy ]========================================
        [TestMethod]
        public void NullableProxy_ReadsTheInnerDefaultWhileNull_AndWritesThrough ()
        {
            var source = new NullableSampleSource();
            var manager = new PropertyGridManager(new SimpleTestPropertyGrid { SingleItemSource = source });
            manager.RebuildEditTargets();
            PropertyEditTarget outer = manager.Items.First(t => t.PropertyPathTarget == nameof(NullableSampleSource.Scale));

            PropertyEditTarget proxy = NullableEditorContext.BuildProxyTarget(outer, outer.EditContext as NullableEditorContext);

            Assert.AreEqual("Single", proxy.Editor);
            Assert.AreEqual(outer.DisplayName, proxy.DisplayName);
            Assert.AreEqual(0f, proxy.EditValue);

            proxy.EditValue = 2.5f;
            Assert.AreEqual(2.5f, source.Scale);
        }

        [TestMethod]
        public void NullableProxy_WithNoContext_UsesObject ()
        {
            var outer = new PropertyEditTarget("Thing", () => null);
            outer.Setup();

            PropertyEditTarget proxy = NullableEditorContext.BuildProxyTarget(outer, null);

            Assert.AreEqual(nameof(System.Object), proxy.Editor);
            Assert.IsNull(proxy.EditValue);
        }
    }
}
