namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using AJut.TypeManagement;
    using AJut.UX.PropertyInteraction;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // ===========[ Test model types ]============================================

    /// <summary>Simple model with basic property types for baseline generation tests.</summary>
    public class BasicModel
    {
        public string Name { get; set; } = "Default";
        public int Count { get; set; } = 5;
        public double Value { get; set; } = 1.0;
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>Model that exercises [PGHidden] and [PGShowReadonly].</summary>
    public class VisibilityModel
    {
        public string Shown { get; set; } = "visible";

        [PGHidden]
        public string Hidden { get; set; } = "hidden";

        public string ReadOnlyNotShown { get; }

        [PGShowReadonly]
        public string ReadOnlyShown { get; } = "readonly";
    }

    /// <summary>Model that exercises [PGEditor].</summary>
    public class EditorModel
    {
        [PGEditor("Slider")]
        public double Volume { get; set; }

        public string PlainText { get; set; }
    }

    /// <summary>Model that exercises [DisplayName].</summary>
    public class DisplayNameModel
    {
        [DisplayName("Full Name")]
        public string UserName { get; set; }
    }

    /// <summary>Model that exercises [PGLabel].</summary>
    public class LabelModel
    {
        [PGLabel("Angle")]
        public double Rotation { get; set; }

        [PGLabel("Speed", "Measured in m/s")]
        public double Velocity { get; set; }

        public int PlainProperty { get; set; }
    }

    /// <summary>Model where [PGLabel] overrides [DisplayName].</summary>
    public class LabelOverridesDisplayNameModel
    {
        [DisplayName("Old Name")]
        [PGLabel("New Name", "Takes priority")]
        public string Foo { get; set; }
    }

    /// <summary>Model with a nullable property.</summary>
    public class NullableModel
    {
        public int? MaybeCount { get; set; }
        public float? MaybeValue { get; set; } = 3.14f;
    }

    /// <summary>Model with [PGOverrideDefault].</summary>
    public class DefaultOverrideModel
    {
        [PGOverrideDefault(42)]
        public int Score { get; set; } = 42;

        [PGOverrideDefault(10)]
        public int Modified { get; set; } = 99;
    }

    /// <summary>Sub-object for expansion tests.</summary>
    public class InnerObject
    {
        public string Detail { get; set; } = "inner";
    }

    /// <summary>Model with a complex sub-object property.</summary>
    public class ExpandableModel
    {
        public string TopLevel { get; set; } = "top";
        public InnerObject Sub { get; set; } = new InnerObject();
    }

    /// <summary>Sub-object with an elevated property.</summary>
    public class ElevatedInnerObject
    {
        [PGElevateAsParent]
        public string MainValue { get; set; } = "elevated";

        public string OtherValue { get; set; } = "other";
    }

    /// <summary>Model with child property elevation via [PGElevateAsParent].</summary>
    public class ElevateAsParentModel
    {
        public ElevatedInnerObject Settings { get; set; } = new ElevatedInnerObject();
    }

    /// <summary>Typed wrapper used to test elevation default delegation with DeferPGAttributesToParent.</summary>
    public class TypedWrapper<T>
    {
        [PGElevateAsParent(deferPGAttributesToParent: true)]
        public T Value { get; set; }
    }

    /// <summary>Model with a typed-wrapper property to test that ResetToDefault delegates to the inner value.</summary>
    public class WrapperElevationModel
    {
        [PGEditor("Int32")]
        public TypedWrapper<int> Number { get; set; } = new TypedWrapper<int>();
    }

    /// <summary>Sub-object for [PGElevateChildProperty].</summary>
    public class ChildElevationTarget
    {
        public int X { get; set; } = 10;
        public int Y { get; set; } = 20;
    }

    /// <summary>Model with [PGElevateChildProperty].</summary>
    public class ElevateChildModel
    {
        [PGElevateChildProperty("X")]
        public ChildElevationTarget Position { get; set; } = new ChildElevationTarget();
    }

    /// <summary>Model with [PGAltPropertyAlias].</summary>
    public class AltAliasModel
    {
        public string FirstName { get; set; } = "John";

        [PGAltPropertyAlias("FirstName")]
        public string FullDisplay { get; set; } = "John Doe";
    }

    /// <summary>EditContext type for PGEditContextBuilder tests.</summary>
    [TypeId("PG::TestEditCtx")]
    public class TestEditContext
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public double Step { get; set; }
    }

    /// <summary>Model with [PGEditContextBuilder].</summary>
    public class EditContextBuilderModel
    {
        [PGEditor("Numeric")]
        [PGEditContextBuilder("PG::TestEditCtx", "{ \"Min\": 0.5, \"Max\": 10, \"Step\": 2 }")]
        public double Amount { get; set; } = 5.0;

        public string NoContext { get; set; } = "plain";
    }

    /// <summary>Model that combines PGLabel + PGEditContextBuilder + PGEditor.</summary>
    public class CombinedModel
    {
        [PGEditor("Numeric")]
        [PGLabel("Rotation", "Specified 0-180")]
        [PGEditContextBuilder("PG::TestEditCtx", "{ \"Min\": 0, \"Max\": 180, \"Step\": 1 }")]
        public double Rotation { get; set; } = 90.0;
    }

    /// <summary>Model to test PGEditContextBuilder with unregistered TypeId.</summary>
    public class UnregisteredContextModel
    {
        [PGEditContextBuilder("PG::DoesNotExist", "{ \"Foo\": 1 }")]
        public int BadProp { get; set; }
    }

    /// <summary>Model to test Nullable + PGEditContextBuilder - Nullable wins for editContext.</summary>
    public class NullableWithContextModel
    {
        [PGEditContextBuilder("PG::TestEditCtx", "{ \"Min\": 0, \"Max\": 100, \"Step\": 1 }")]
        public int? NullableCount { get; set; }
    }

    /// <summary>Model where all read-only properties are shown via class-level attribute.</summary>
    [PGShowReadonly]
    public class ShowAllReadonlyModel
    {
        public string Editable { get; set; } = "edit";
        public string ReadOnly1 { get; } = "ro1";
        public string ReadOnly2 { get; } = "ro2";
    }

    // ===========[ Tests ]=====================================================

    [TestClass]
    public class PropertyEditTargetTests
    {
        [ClassInitialize]
        public static void ClassSetup (TestContext context)
        {
            // Register TypeIds from this assembly so PGEditContextBuilder can resolve them
            TypeIdRegistrar.RegisterAllTypeIds(typeof(PropertyEditTargetTests).Assembly);
        }

        // ===[ Baseline generation tests ]===

        [TestMethod]
        public void PET_GenerateBasic_ProducesCorrectTargets ()
        {
            var model = new BasicModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();
            SetupAll(targets);

            Assert.AreEqual(4, targets.Count, "BasicModel has 4 public settable properties");

            var name = Find(targets, "Name");
            Assert.AreEqual("Name", name.DisplayName);
            Assert.AreEqual("String", name.Editor);
            Assert.AreEqual("Default", name.EditValue);

            var count = Find(targets, "Count");
            Assert.AreEqual("Count", count.DisplayName);
            Assert.AreEqual("Int32", count.Editor);
            Assert.AreEqual(5, count.EditValue);

            var value = Find(targets, "Value");
            Assert.AreEqual("Double", value.Editor);

            var enabled = Find(targets, "IsEnabled");
            Assert.AreEqual("Boolean", enabled.Editor);
        }

        [TestMethod]
        public void PET_GenerateBasic_FriendlyDisplayName ()
        {
            var model = new BasicModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            // "IsEnabled" -> "Is Enabled" via ConvertToFriendlyEn
            var enabled = Find(targets, "IsEnabled");
            Assert.AreEqual("Is Enabled", enabled.DisplayName);
        }

        // ===[ Visibility tests ]===

        [TestMethod]
        public void PET_PGHidden_PropertyIsExcluded ()
        {
            var model = new VisibilityModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            Assert.IsNull(targets.FirstOrDefault(t => t.PropertyPathTarget == "Hidden"),
                "[PGHidden] property should not appear");
            Assert.IsNotNull(Find(targets, "Shown"),
                "Non-hidden settable property should appear");
        }

        [TestMethod]
        public void PET_PGShowReadonly_PropertyLevel ()
        {
            var model = new VisibilityModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            // ReadOnlyShown has [PGShowReadonly] -> included
            Assert.IsNotNull(Find(targets, "ReadOnlyShown"),
                "[PGShowReadonly] property should be included");

            // ReadOnlyNotShown has no attribute and no setter -> excluded
            Assert.IsNull(targets.FirstOrDefault(t => t.PropertyPathTarget == "ReadOnlyNotShown"),
                "Readonly without [PGShowReadonly] should be excluded");
        }

        [TestMethod]
        public void PET_PGShowReadonly_ClassLevel ()
        {
            var model = new ShowAllReadonlyModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            Assert.AreEqual(3, targets.Count, "Class-level [PGShowReadonly] should expose all 3 properties");
            Assert.IsNotNull(Find(targets, "ReadOnly1"));
            Assert.IsNotNull(Find(targets, "ReadOnly2"));
            Assert.IsNotNull(Find(targets, "Editable"));
        }

        // ===[ PGEditor tests ]===

        [TestMethod]
        public void PET_PGEditor_OverridesEditorKey ()
        {
            var model = new EditorModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            Assert.AreEqual("Slider", Find(targets, "Volume").Editor);
            Assert.AreEqual("String", Find(targets, "PlainText").Editor);
        }

        // ===[ DisplayName tests ]===

        [TestMethod]
        public void PET_DisplayNameAttribute_OverridesDefault ()
        {
            var model = new DisplayNameModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();
            Assert.AreEqual("Full Name", Find(targets, "UserName").DisplayName);
        }

        // ===[ PGLabel tests ]===

        [TestMethod]
        public void PET_PGLabel_OverridesDisplayName ()
        {
            var model = new LabelModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            Assert.AreEqual("Angle", Find(targets, "Rotation").DisplayName);
        }

        [TestMethod]
        public void PET_PGLabel_SetsSubtitle ()
        {
            var model = new LabelModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            Assert.AreEqual("Speed", Find(targets, "Velocity").DisplayName);
            Assert.AreEqual("Measured in m/s", Find(targets, "Velocity").Subtitle);
        }

        [TestMethod]
        public void PET_PGLabel_NoSubtitleWhenOmitted ()
        {
            var model = new LabelModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            // Rotation has [PGLabel("Angle")] with no subtitle
            Assert.IsNull(Find(targets, "Rotation").Subtitle);

            // PlainProperty has no PGLabel at all
            Assert.IsNull(Find(targets, "PlainProperty").Subtitle);
        }

        [TestMethod]
        public void PET_PGLabel_TakesPriorityOverDisplayName ()
        {
            var model = new LabelOverridesDisplayNameModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            var foo = Find(targets, "Foo");
            Assert.AreEqual("New Name", foo.DisplayName, "PGLabel should take priority over DisplayName");
            Assert.AreEqual("Takes priority", foo.Subtitle);
        }

        // ===[ Nullable tests ]===

        [TestMethod]
        public void PET_Nullable_EditorAndContext ()
        {
            var model = new NullableModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            var maybeCount = Find(targets, "MaybeCount");
            Assert.AreEqual("Nullable", maybeCount.Editor);
            Assert.IsInstanceOfType(maybeCount.EditContext, typeof(NullableEditorContext));
            var ctx = (NullableEditorContext)maybeCount.EditContext;
            Assert.AreEqual("Int32", ctx.InnerEditorKey);
            Assert.AreEqual(typeof(int), ctx.InnerType);

            var maybeValue = Find(targets, "MaybeValue");
            Assert.AreEqual("Nullable", maybeValue.Editor);
            var vCtx = (NullableEditorContext)maybeValue.EditContext;
            Assert.AreEqual("Single", vCtx.InnerEditorKey);
        }

        [TestMethod]
        public void PET_Nullable_ContextWinsOverPGEditContextBuilder ()
        {
            var model = new NullableWithContextModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            var target = Find(targets, "NullableCount");
            Assert.AreEqual("Nullable", target.Editor, "Nullable should take priority over PGEditContextBuilder for editor key");
            Assert.IsInstanceOfType(target.EditContext, typeof(NullableEditorContext),
                "Nullable EditContext should take priority over PGEditContextBuilder");
        }

        // ===[ Default value tests ]===

        [TestMethod]
        public void PET_DefaultTracking_Basic ()
        {
            var model = new BasicModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            SetupAll(targets);

            // Value type defaults: int=0, double=0.0, bool=false
            // Count is 5 but default is 0 -> not at default
            Assert.IsFalse(Find(targets, "Count").IsAtDefaultValue);

            // String default is null, current is "Default" -> not at default
            Assert.IsFalse(Find(targets, "Name").IsAtDefaultValue);
        }

        [TestMethod]
        public void PET_PGOverrideDefault_TracksCorrectly ()
        {
            var model = new DefaultOverrideModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            SetupAll(targets);

            var score = Find(targets, "Score");
            Assert.IsTrue(score.HasDefaultValue);
            Assert.AreEqual(42, score.DefaultValue);
            Assert.IsTrue(score.IsAtDefaultValue, "Score=42 matches [PGOverrideDefault(42)]");

            var modified = Find(targets, "Modified");
            Assert.IsTrue(modified.HasDefaultValue);
            Assert.AreEqual(10, modified.DefaultValue);
            Assert.IsFalse(modified.IsAtDefaultValue, "Modified=99 does not match [PGOverrideDefault(10)]");
        }

        [TestMethod]
        public void PET_ResetToDefault_Works ()
        {
            var model = new DefaultOverrideModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            SetupAll(targets);

            var modified = Find(targets, "Modified");
            Assert.IsFalse(modified.IsAtDefaultValue);

            modified.ResetToDefault();
            Assert.IsTrue(modified.IsAtDefaultValue);
            Assert.AreEqual(10, modified.EditValue);
            Assert.AreEqual(10, model.Modified, "ResetToDefault should push the value back to the source");
        }

        // ===[ Expandable sub-object tests ]===

        [TestMethod]
        public void PET_ComplexSubObject_IsExpandable ()
        {
            var model = new ExpandableModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            var top = Find(targets, "TopLevel");
            Assert.IsFalse(top.IsExpandable);

            var sub = Find(targets, "Sub");
            Assert.IsTrue(sub.IsExpandable);
            Assert.IsTrue(sub.Children.Count > 0, "Expandable node should have children");

            // The child should be "Detail" from InnerObject
            var detail = sub.Children.OfType<PropertyEditTarget>().FirstOrDefault(t => t.PropertyPathTarget == "Detail");
            Assert.IsNotNull(detail);
            Assert.AreEqual("String", detail.Editor);
        }

        // ===[ Elevation tests ]===

        [TestMethod]
        public void PET_ElevateAsParent_InlinesChild ()
        {
            var model = new ElevateAsParentModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            var settings = Find(targets, "Settings");
            Assert.IsFalse(settings.IsExpandable, "Should not be expandable when elevation is active");
            Assert.IsNotNull(settings.ElevatedChildTarget);
            Assert.AreEqual("MainValue", settings.ElevatedChildTarget.PropertyPathTarget);
            Assert.AreEqual("String", settings.ElevatedChildTarget.Editor);
            Assert.IsTrue(settings.HasInlineEditor);
            Assert.AreSame(settings.ElevatedChildTarget, settings.EffectiveEditorTarget);
        }

        [TestMethod]
        public void PET_ElevateChildProperty_InlinesNamedChild ()
        {
            var model = new ElevateChildModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            var position = Find(targets, "Position");
            Assert.IsFalse(position.IsExpandable);
            Assert.IsNotNull(position.ElevatedChildTarget);
            Assert.AreEqual("X", position.ElevatedChildTarget.PropertyPathTarget);
            Assert.AreEqual("Int32", position.ElevatedChildTarget.Editor);
        }

        [TestMethod]
        public void PET_ElevateAsParent_IsAtDefaultValue_DelegatesFromChild ()
        {
            var model = new WrapperElevationModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();
            SetupAll(targets);

            var number = Find(targets, "Number");
            Assert.IsNotNull(number.ElevatedChildTarget);

            // Child int starts at 0 which is default(int) - both should be at default
            Assert.IsTrue(number.ElevatedChildTarget.IsAtDefaultValue, "Child should start at default (0)");
            Assert.IsTrue(number.IsAtDefaultValue, "Parent should reflect child's IsAtDefaultValue");

            // Modify the child value away from default
            number.ElevatedChildTarget.EditValue = 42;
            Assert.IsFalse(number.ElevatedChildTarget.IsAtDefaultValue, "Child should not be at default after edit");
            Assert.IsFalse(number.IsAtDefaultValue, "Parent should sync when child leaves default");
        }

        [TestMethod]
        public void PET_ElevateAsParent_ResetToDefault_ResetsChildNotWrapper ()
        {
            var model = new WrapperElevationModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();
            SetupAll(targets);

            var number = Find(targets, "Number");
            Assert.IsNotNull(number.ElevatedChildTarget);

            // Move child away from default, then reset via parent
            number.ElevatedChildTarget.EditValue = 99;
            Assert.IsFalse(number.IsAtDefaultValue);

            number.ResetToDefault();

            // Wrapper object must still exist - only the inner value was reset
            Assert.IsNotNull(model.Number, "Wrapper object must not be nulled out by ResetToDefault");
            Assert.AreEqual(0, model.Number.Value, "Inner value should be reset to default(int)");
            Assert.IsTrue(number.IsAtDefaultValue, "Parent IsAtDefaultValue should be true after reset");
        }

        // ===[ AltPropertyAlias tests ]===

        [TestMethod]
        public void PET_AltPropertyAlias_SetsAdditionalEvalTargets ()
        {
            var model = new AltAliasModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            var fullDisplay = Find(targets, "FullDisplay");
            Assert.IsNotNull(fullDisplay.AdditionalEvalTargets);
            CollectionAssert.Contains(fullDisplay.AdditionalEvalTargets, "FirstName");
            Assert.IsTrue(fullDisplay.ShouldEvaluateFor("FirstName"),
                "ShouldEvaluateFor should return true for aliased property");
        }

        // ===[ PGEditContextBuilder tests ]===

        [TestMethod]
        public void PET_EditContextBuilder_DeserializesContext ()
        {
            var model = new EditContextBuilderModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            var amount = Find(targets, "Amount");
            Assert.AreEqual("Numeric", amount.Editor);
            Assert.IsNotNull(amount.EditContext, "EditContext should be set by PGEditContextBuilder");
            Assert.IsInstanceOfType(amount.EditContext, typeof(TestEditContext));

            var ctx = (TestEditContext)amount.EditContext;
            Assert.AreEqual(0.5, ctx.Min);
            Assert.AreEqual(10.0, ctx.Max);
            Assert.AreEqual(2.0, ctx.Step);
        }

        [TestMethod]
        public void PET_EditContextBuilder_NoContextWithoutAttribute ()
        {
            var model = new EditContextBuilderModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            var noCtx = Find(targets, "NoContext");
            Assert.IsNull(noCtx.EditContext, "Property without PGEditContextBuilder should have null EditContext");
        }

        [TestMethod]
        public void PET_EditContextBuilder_UnregisteredTypeId_GracefulFail ()
        {
            var model = new UnregisteredContextModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            var bad = Find(targets, "BadProp");
            Assert.IsNull(bad.EditContext, "Unregistered TypeId should result in null EditContext, not an exception");
        }

        // ===[ Combined attribute tests ]===

        [TestMethod]
        public void PET_Combined_LabelEditorContext ()
        {
            var model = new CombinedModel();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            var rotation = Find(targets, "Rotation");
            Assert.AreEqual("Rotation", rotation.DisplayName);
            Assert.AreEqual("Specified 0-180", rotation.Subtitle);
            Assert.AreEqual("Numeric", rotation.Editor);
            Assert.IsInstanceOfType(rotation.EditContext, typeof(TestEditContext));

            var ctx = (TestEditContext)rotation.EditContext;
            Assert.AreEqual(0.0, ctx.Min);
            Assert.AreEqual(180.0, ctx.Max);
            Assert.AreEqual(1.0, ctx.Step);
        }

        // ===[ EditValue read/write tests ]===

        [TestMethod]
        public void PET_EditValue_WritesPushesToSource ()
        {
            var model = new BasicModel { Count = 5 };
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            SetupAll(targets);

            var count = Find(targets, "Count");
            count.EditValue = 42;
            Assert.AreEqual(42, model.Count, "Setting EditValue should push the value to the source object");
            Assert.AreEqual(42, count.EditValue);
        }

        [TestMethod]
        public void PET_ReadOnly_ManualTarget_DoesNotWrite ()
        {
            // Manually create a readonly target (no setter) to test IsReadOnly behavior
            string value = "original";
            var target = new PropertyEditTarget("Test", () => value, setValue: null);
            target.Setup();

            Assert.IsTrue(target.IsReadOnly);
            Assert.AreEqual("original", target.EditValue);

            // Attempting to set EditValue on a readonly target is a no-op
            target.EditValue = "changed";
            Assert.AreEqual("original", target.EditValue, "ReadOnly target should not accept writes");
        }

        // ===[ RecacheEditValue tests ]===

        [TestMethod]
        public void PET_RecacheEditValue_ReflectsSourceChange ()
        {
            var model = new BasicModel { Name = "Original" };
            var targets = PropertyEditTarget.GenerateForPropertiesOf(model).ToList();

            SetupAll(targets);

            var name = Find(targets, "Name");
            Assert.AreEqual("Original", name.EditValue);

            // Change the source directly (not through EditValue)
            model.Name = "Changed";
            Assert.AreEqual("Original", name.EditValue, "Before recache, EditValue should be stale");

            name.RecacheEditValue();
            Assert.AreEqual("Changed", name.EditValue, "After recache, EditValue should reflect source");
        }

        // ===[ Helpers ]==========================================================

        private static PropertyEditTarget Find (List<PropertyEditTarget> targets, string propertyPath)
        {
            return targets.FirstOrDefault(t => t.PropertyPathTarget == propertyPath);
        }

        private static void SetupAll (List<PropertyEditTarget> targets)
        {
            foreach (var t in targets)
            {
                t.Setup();
            }
        }
    }
}
