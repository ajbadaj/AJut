namespace AJut.UX.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Windows.Input;
    using AJut.Storage;
    using AJut.Tree;
    using AJut.UX.PropertyInteraction;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // ===========[ Test helpers ]==========================================

    public class SimpleTestPropertyGrid : IPropertyGrid
    {
        public IEnumerable ItemsSource { get; set; }
        public object SingleItemSource { get; set; }
    }

    // ===========[ Test source types for PGShowIf/HideIf/Button/Coerce ]==========================================

    public class ShowIfTestSource : NotifyPropertyChanged
    {
        private bool m_showExtra;
        public bool ShowExtra
        {
            get => m_showExtra;
            set => this.SetAndRaiseIfChanged(ref m_showExtra, value);
        }

        [PGEditor("Text")]
        public string AlwaysVisible { get; set; } = "Always";

        private string m_conditionalValue = "Conditional";
        [PGEditor("Text")]
        [PGShowIf(nameof(ShowExtra))]
        public string ConditionalValue
        {
            get => m_conditionalValue;
            set => this.SetAndRaiseIfChanged(ref m_conditionalValue, value);
        }
    }

    public class HideIfTestSource
    {
        public bool HideExtra { get; set; }

        [PGEditor("Text")]
        public string AlwaysVisible { get; set; } = "Always";

        [PGEditor("Text")]
        [PGHideIf(nameof(HideExtra), HideWhen = true)]
        public string HiddenWhenTrue { get; set; } = "Conditional";
    }

    public class CoerceTestSource : NotifyPropertyChanged
    {
        private int m_clampedValue = 5;

        [PGCoerce(nameof(ClampValue))]
        [PGEditor("Number")]
        public int ClampedValue
        {
            get => m_clampedValue;
            set => this.SetAndRaiseIfChanged(ref m_clampedValue, value);
        }

        private object ClampValue (object value)
        {
            if (value is int i)
            {
                return Math.Clamp(i, 0, 100);
            }

            return value;
        }
    }

    public class ButtonTestSource : NotifyPropertyChanged
    {
        private int m_counter;
        public int Counter
        {
            get => m_counter;
            set => this.SetAndRaiseIfChanged(ref m_counter, value);
        }

        [PGButton("Increment")]
        public void IncrementCounter ()
        {
            this.Counter = this.Counter + 1;
        }
    }

    public class GroupedShowIfTestSource : NotifyPropertyChanged
    {
        private bool m_showStats;
        public bool ShowStats
        {
            get => m_showStats;
            set => this.SetAndRaiseIfChanged(ref m_showStats, value);
        }

        [PGEditor("Text")]
        public string Name { get; set; } = "Test";

        [PGEditor("Number")]
        [PGGroup("Stats")]
        [PGShowIf(nameof(ShowStats))]
        public int Height { get; set; } = 180;

        [PGEditor("Number")]
        [PGGroup("Stats")]
        public int Weight { get; set; } = 75;
    }

    public class ShowIfMethodTestSource
    {
        [PGEditor("Text")]
        public string AlwaysVisible { get; set; } = "Always";

        [PGEditor("Number")]
        [PGShowIf(nameof(ShouldShowValue))]
        public int ConditionalValue { get; set; } = 42;

        public bool ShouldShowValue () => false;
    }

    public class ShowWhenFalseTestSource
    {
        public bool Flag { get; set; } = true;

        [PGEditor("Text")]
        [PGShowIf(nameof(Flag), ShowWhen = false)]
        public string ShownWhenFlagIsFalse { get; set; } = "Hidden when true";
    }

    public class HideWhenTrueTestSource
    {
        public bool Flag { get; set; } = true;

        [PGEditor("Text")]
        [PGHideIf(nameof(Flag), HideWhen = true)]
        public string HiddenWhenFlagIsTrue { get; set; } = "Hidden";
    }

    public class ConditionalButtonTestSource : NotifyPropertyChanged
    {
        private bool m_showButton = true;
        public bool ShowButton
        {
            get => m_showButton;
            set => this.SetAndRaiseIfChanged(ref m_showButton, value);
        }

        public int CallCount { get; set; }

        [PGButton("Do Thing")]
        [PGShowIf(nameof(ShowButton))]
        public void DoThing ()
        {
            ++this.CallCount;
        }
    }

    // ===========[ IPropertyEditManager + ShowIf test types ]==========================================

    /// <summary>
    /// Simulates the pattern: IPropertyEditManager wraps a different object and
    /// delegates target generation to GenerateForPropertiesOf on that inner object.
    /// ShowIf/HideIf attributes are on the inner object's properties, not the manager's.
    /// </summary>
    public class EditManagerShowIfSource : NotifyPropertyChanged
    {
        private bool m_showConditional;
        public bool ShowConditional
        {
            get => m_showConditional;
            set => this.SetAndRaiseIfChanged(ref m_showConditional, value);
        }

        [PGEditor("Text")]
        public string AlwaysVisible { get; set; } = "Always";

        [PGShowIf(nameof(ShouldShowConditional))]
        [PGEditor("Int32")]
        public int ConditionalValue { get; set; } = 42;

        private bool ShouldShowConditional () => m_showConditional;
    }

    public class TestEditManager : IPropertyEditManager
    {
        private readonly EditManagerShowIfSource m_inner;

        public TestEditManager (EditManagerShowIfSource inner)
        {
            m_inner = inner;
        }

        public EditManagerShowIfSource Inner => m_inner;

        public IEnumerable<PropertyEditTarget> GenerateEditTargets ()
        {
            return PropertyEditTarget.GenerateForPropertiesOf(m_inner);
        }
    }

    // ===========[ ElevateAsParent + ShowIf test types ]==========================================

    public class TestWrapper<T> : NotifyPropertyChanged
    {
        private T m_value;

        [PGElevateAsParent(deferPGAttributesToParent: true)]
        public T Value
        {
            get => m_value;
            set => this.SetAndRaiseIfChanged(ref m_value, value);
        }
    }

    public class ElevateShowIfMethodTestSource : NotifyPropertyChanged
    {
        private bool m_showWrapped;
        public bool ShowWrapped
        {
            get => m_showWrapped;
            set => this.SetAndRaiseIfChanged(ref m_showWrapped, value);
        }

        [PGEditor("Text")]
        public string AlwaysVisible { get; set; } = "Always";

        [PGShowIf(nameof(ShouldShowWrapped))]
        [PGEditor("Int32")]
        public TestWrapper<int> ConditionalWrapped { get; set; } = new TestWrapper<int> { Value = 42 };

        private bool ShouldShowWrapped () => m_showWrapped;
    }

    public class ElevateShowIfPropertyTestSource : NotifyPropertyChanged
    {
        private bool m_showWrapped;
        public bool ShowWrapped
        {
            get => m_showWrapped;
            set => this.SetAndRaiseIfChanged(ref m_showWrapped, value);
        }

        [PGEditor("Text")]
        public string AlwaysVisible { get; set; } = "Always";

        [PGShowIf(nameof(ShowWrapped))]
        [PGEditor("Int32")]
        public TestWrapper<int> ConditionalWrapped { get; set; } = new TestWrapper<int> { Value = 99 };
    }

    // ===========[ ShowIf with private method on base class ]==========================================

    public class ElevateShowIfBaseClass : NotifyPropertyChanged
    {
        private bool m_showWrapped;
        public bool ShowWrapped
        {
            get => m_showWrapped;
            set => this.SetAndRaiseIfChanged(ref m_showWrapped, value);
        }

        [PGEditor("Text")]
        public string AlwaysVisible { get; set; } = "Always";

        [PGShowIf(nameof(ShouldShowWrapped))]
        [PGEditor("Int32")]
        public TestWrapper<int> ConditionalWrapped { get; set; } = new TestWrapper<int> { Value = 42 };

        private bool ShouldShowWrapped () => m_showWrapped;
    }

    public class ElevateShowIfDerivedClass : ElevateShowIfBaseClass
    {
        [PGEditor("Text")]
        public string ExtraProp { get; set; } = "Derived";
    }

    // ===========[ Coercion tests ]==========================================

    [TestClass]
    public class PGCoerceTests
    {
        [TestMethod]
        public void Coerce_ClampsValueAboveMax ()
        {
            var source = new CoerceTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var target = targets.First(t => t.PropertyPathTarget == nameof(CoerceTestSource.ClampedValue));
            target.Setup();

            target.EditValue = 200;

            Assert.AreEqual(100, target.EditValue, "EditValue should be clamped to 100");
            Assert.AreEqual(100, source.ClampedValue, "Source property should also be 100");
        }

        [TestMethod]
        public void Coerce_ClampsValueBelowMin ()
        {
            var source = new CoerceTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var target = targets.First(t => t.PropertyPathTarget == nameof(CoerceTestSource.ClampedValue));
            target.Setup();

            target.EditValue = -50;

            Assert.AreEqual(0, target.EditValue, "EditValue should be clamped to 0");
            Assert.AreEqual(0, source.ClampedValue, "Source property should also be 0");
        }

        [TestMethod]
        public void Coerce_PassesThroughValidValue ()
        {
            var source = new CoerceTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var target = targets.First(t => t.PropertyPathTarget == nameof(CoerceTestSource.ClampedValue));
            target.Setup();

            target.EditValue = 50;

            Assert.AreEqual(50, target.EditValue);
            Assert.AreEqual(50, source.ClampedValue);
        }
    }

    // ===========[ ShowIf/HideIf generation tests ]==========================================

    [TestClass]
    public class PGShowIfHideIfGenerationTests
    {
        [TestMethod]
        public void ShowIf_AlwaysGeneratesTarget ()
        {
            // GenerateForPropertiesOf no longer filters ShowIf/HideIf -
            // filtering is deferred to PropertyGridManager
            var source = new ShowIfTestSource { ShowExtra = false };
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();

            Assert.IsTrue(
                targets.Any(t => t.PropertyPathTarget == nameof(ShowIfTestSource.ConditionalValue)),
                "Conditional target should always be generated regardless of condition state"
            );
        }

        [TestMethod]
        public void HideIf_AlwaysGeneratesTarget ()
        {
            var source = new HideIfTestSource { HideExtra = true };
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();

            Assert.IsTrue(
                targets.Any(t => t.PropertyPathTarget == nameof(HideIfTestSource.HiddenWhenTrue)),
                "HideIf target should always be generated"
            );
        }
    }

    // ===========[ PropertyGridManager tests ]==========================================

    [TestClass]
    public class PropertyGridManagerTests
    {
        // ------ Basic build ------

        [TestMethod]
        public void Build_BasicSource_PopulatesTree ()
        {
            var source = new BasicModel();
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            Assert.IsNotNull(manager.RootNode);
            Assert.IsTrue(manager.RootNode.Children.Count > 0);
        }

        // ------ ShowIf via manager ------

        [TestMethod]
        public void ShowIf_HidesWhenConditionFalse ()
        {
            var source = new ShowIfTestSource { ShowExtra = false };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            var allTargets = _GetAllTargets(manager.RootNode);
            Assert.IsFalse(
                allTargets.Any(t => t.PropertyPathTarget == nameof(ShowIfTestSource.ConditionalValue)),
                "ConditionalValue should be hidden when ShowExtra is false"
            );
            Assert.IsTrue(
                allTargets.Any(t => t.PropertyPathTarget == nameof(ShowIfTestSource.AlwaysVisible)),
                "AlwaysVisible should always be present"
            );
        }

        [TestMethod]
        public void ShowIf_ShowsWhenConditionTrue ()
        {
            var source = new ShowIfTestSource { ShowExtra = true };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            var allTargets = _GetAllTargets(manager.RootNode);
            Assert.IsTrue(
                allTargets.Any(t => t.PropertyPathTarget == nameof(ShowIfTestSource.ConditionalValue)),
                "ConditionalValue should be visible when ShowExtra is true"
            );
        }

        // ------ HideIf via manager ------

        [TestMethod]
        public void HideIf_HidesWhenConditionMatchesHideWhen ()
        {
            // HideWhen = true, Flag = true => hidden
            var source = new HideWhenTrueTestSource();
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            var allTargets = _GetAllTargets(manager.RootNode);
            Assert.IsFalse(
                allTargets.Any(t => t.PropertyPathTarget == nameof(HideWhenTrueTestSource.HiddenWhenFlagIsTrue)),
                "Should be hidden when Flag matches HideWhen (both true)"
            );
        }

        [TestMethod]
        public void HideIf_ShowsWhenConditionDoesNotMatchHideWhen ()
        {
            // HideWhen = true, HideExtra = false => memberValue != HideWhen => visible
            var source = new HideIfTestSource { HideExtra = false };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            var allTargets = _GetAllTargets(manager.RootNode);
            Assert.IsTrue(
                allTargets.Any(t => t.PropertyPathTarget == nameof(HideIfTestSource.HiddenWhenTrue)),
                "Should be visible when HideExtra (false) does not match HideWhen (true)"
            );
        }

        // ------ ShowWhen=false ------

        [TestMethod]
        public void ShowWhenFalse_HidesWhenFlagIsTrue ()
        {
            var source = new ShowWhenFalseTestSource();
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            var allTargets = _GetAllTargets(manager.RootNode);
            Assert.IsFalse(
                allTargets.Any(t => t.PropertyPathTarget == nameof(ShowWhenFalseTestSource.ShownWhenFlagIsFalse)),
                "Should be hidden when Flag=true and ShowWhen=false"
            );
        }

        // ------ ShowIf with method target ------

        [TestMethod]
        public void ShowIf_EvaluatesMethodResult ()
        {
            var source = new ShowIfMethodTestSource();
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            var allTargets = _GetAllTargets(manager.RootNode);
            Assert.IsFalse(
                allTargets.Any(t => t.PropertyPathTarget == nameof(ShowIfMethodTestSource.ConditionalValue)),
                "Should be hidden when ShouldShowValue() returns false"
            );
        }

        // ------ UpdateConditionalVisibility ------

        [TestMethod]
        public void Update_ShowsTargetWhenConditionBecomesTrue ()
        {
            var source = new ShowIfTestSource { ShowExtra = false };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            // Confirm initially hidden
            Assert.IsFalse(_GetAllTargets(manager.RootNode).Any(t => t.PropertyPathTarget == nameof(ShowIfTestSource.ConditionalValue)));

            // Toggle on
            source.ShowExtra = true;
            bool changed = manager.UpdateConditionalVisibility();

            Assert.IsTrue(changed, "Should return true when a condition changed");
            Assert.IsTrue(
                _GetAllTargets(manager.RootNode).Any(t => t.PropertyPathTarget == nameof(ShowIfTestSource.ConditionalValue)),
                "ConditionalValue should appear after ShowExtra becomes true"
            );
        }

        [TestMethod]
        public void Update_HidesTargetWhenConditionBecomesFalse ()
        {
            var source = new ShowIfTestSource { ShowExtra = true };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            // Confirm initially visible
            Assert.IsTrue(_GetAllTargets(manager.RootNode).Any(t => t.PropertyPathTarget == nameof(ShowIfTestSource.ConditionalValue)));

            // Toggle off
            source.ShowExtra = false;
            bool changed = manager.UpdateConditionalVisibility();

            Assert.IsTrue(changed);
            Assert.IsFalse(
                _GetAllTargets(manager.RootNode).Any(t => t.PropertyPathTarget == nameof(ShowIfTestSource.ConditionalValue)),
                "ConditionalValue should disappear after ShowExtra becomes false"
            );
        }

        [TestMethod]
        public void Update_ReturnsFalseWhenNothingChanged ()
        {
            var source = new ShowIfTestSource { ShowExtra = true };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            Assert.IsFalse(manager.UpdateConditionalVisibility(), "Should return false when no conditions changed");
        }

        [TestMethod]
        public void Update_NoConditionalTargets_ReturnsFalse ()
        {
            var pg = new SimpleTestPropertyGrid { SingleItemSource = new BasicModel() };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            Assert.IsFalse(manager.UpdateConditionalVisibility());
        }

        [TestMethod]
        public void Update_MultipleToggleCycles ()
        {
            var source = new ShowIfTestSource { ShowExtra = false };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            // Cycle 1: show
            source.ShowExtra = true;
            manager.UpdateConditionalVisibility();
            Assert.IsTrue(_GetAllTargets(manager.RootNode).Any(t => t.PropertyPathTarget == nameof(ShowIfTestSource.ConditionalValue)));

            // Cycle 2: hide
            source.ShowExtra = false;
            manager.UpdateConditionalVisibility();
            Assert.IsFalse(_GetAllTargets(manager.RootNode).Any(t => t.PropertyPathTarget == nameof(ShowIfTestSource.ConditionalValue)));

            // Cycle 3: show again
            source.ShowExtra = true;
            manager.UpdateConditionalVisibility();
            Assert.IsTrue(_GetAllTargets(manager.RootNode).Any(t => t.PropertyPathTarget == nameof(ShowIfTestSource.ConditionalValue)));
        }

        // ------ Grouped conditional targets ------

        [TestMethod]
        public void Update_GroupedTarget_TogglesWithinGroup ()
        {
            var source = new GroupedShowIfTestSource { ShowStats = false };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            // Stats group should exist (Weight is unconditional)
            var statsGroup = manager.RootNode.Children
                .OfType<PropertyEditTarget>()
                .FirstOrDefault(t => t.PropertyPathTarget == "$group_Stats");
            Assert.IsNotNull(statsGroup, "Stats group header should exist");

            // Height hidden, Weight visible
            var groupChildren = statsGroup.Children.OfType<PropertyEditTarget>().ToList();
            Assert.IsFalse(groupChildren.Any(t => t.PropertyPathTarget == nameof(GroupedShowIfTestSource.Height)));
            Assert.IsTrue(groupChildren.Any(t => t.PropertyPathTarget == nameof(GroupedShowIfTestSource.Weight)));

            // Toggle on
            source.ShowStats = true;
            manager.UpdateConditionalVisibility();

            groupChildren = statsGroup.Children.OfType<PropertyEditTarget>().ToList();
            Assert.IsTrue(
                groupChildren.Any(t => t.PropertyPathTarget == nameof(GroupedShowIfTestSource.Height)),
                "Height should appear in group after ShowStats becomes true"
            );
        }

        // ------ Button targets ------

        [TestMethod]
        public void Button_CreatesButtonTarget ()
        {
            var source = new ButtonTestSource();
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            var allTargets = _GetAllTargets(manager.RootNode);
            var buttonTarget = allTargets.FirstOrDefault(t => t.Editor == "Button");

            Assert.IsNotNull(buttonTarget, "A button target should be generated");
            Assert.AreEqual("Increment", buttonTarget.DisplayName);
        }

        [TestMethod]
        public void Button_CommandExecutesMethod ()
        {
            var source = new ButtonTestSource();
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            var buttonTarget = _GetAllTargets(manager.RootNode).First(t => t.Editor == "Button");

            Assert.AreEqual(0, source.Counter);

            var command = buttonTarget.EditContext as ICommand;
            Assert.IsNotNull(command);
            command.Execute(null);

            Assert.AreEqual(1, source.Counter);
        }

        [TestMethod]
        public void Button_RaisesSourceCommittedOnExecute ()
        {
            var source = new ButtonTestSource();
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            var buttonTarget = _GetAllTargets(manager.RootNode).First(t => t.Editor == "Button");

            bool gotSourceCommitted = false;
            buttonTarget.PropertyChanged += _OnPropertyChanged;

            var command = buttonTarget.EditContext as ICommand;
            command.Execute(null);

            Assert.IsTrue(gotSourceCommitted, "Button should raise SourceCommitted after executing");

            void _OnPropertyChanged (object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == PropertyEditTarget.SourceCommittedPropertyName)
                {
                    gotSourceCommitted = true;
                }
            }
        }

        // ------ Conditional button targets ------

        [TestMethod]
        public void ConditionalButton_TogglesVisibility ()
        {
            var source = new ConditionalButtonTestSource { ShowButton = true };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            // Visible initially
            Assert.IsTrue(_GetAllTargets(manager.RootNode).Any(t => t.Editor == "Button"));

            // Hide
            source.ShowButton = false;
            manager.UpdateConditionalVisibility();
            Assert.IsFalse(_GetAllTargets(manager.RootNode).Any(t => t.Editor == "Button"));

            // Show again
            source.ShowButton = true;
            manager.UpdateConditionalVisibility();
            Assert.IsTrue(_GetAllTargets(manager.RootNode).Any(t => t.Editor == "Button"));
        }

        // ------ ShowIf via IPropertyEditManager (delegates to inner object) ------

        [TestMethod]
        public void ShowIf_ViaEditManager_HiddenWhenConditionFalse ()
        {
            // The IPropertyEditManager generates targets from an inner object,
            // but the PropertyGrid source is the manager itself. ShowIf attributes
            // live on the inner object's type, not the manager's type.
            var inner = new EditManagerShowIfSource { ShowConditional = false };
            var manager = new TestEditManager(inner);
            var pg = new SimpleTestPropertyGrid { SingleItemSource = manager };
            var pgManager = new PropertyGridManager(pg);

            pgManager.RebuildEditTargets();

            var allTargets = _GetAllTargets(pgManager.RootNode);
            Assert.IsFalse(
                allTargets.Any(t => t.PropertyPathTarget == nameof(EditManagerShowIfSource.ConditionalValue)),
                "ConditionalValue should be hidden when ShouldShowConditional() returns false"
            );
        }

        [TestMethod]
        public void ShowIf_ViaEditManager_ShownWhenConditionTrue ()
        {
            var inner = new EditManagerShowIfSource { ShowConditional = true };
            var manager = new TestEditManager(inner);
            var pg = new SimpleTestPropertyGrid { SingleItemSource = manager };
            var pgManager = new PropertyGridManager(pg);

            pgManager.RebuildEditTargets();

            var allTargets = _GetAllTargets(pgManager.RootNode);
            Assert.IsTrue(
                allTargets.Any(t => t.PropertyPathTarget == nameof(EditManagerShowIfSource.ConditionalValue)),
                "ConditionalValue should be visible when ShouldShowConditional() returns true"
            );
        }

        [TestMethod]
        public void ShowIf_ViaEditManager_TogglesCorrectly ()
        {
            var inner = new EditManagerShowIfSource { ShowConditional = false };
            var manager = new TestEditManager(inner);
            var pg = new SimpleTestPropertyGrid { SingleItemSource = manager };
            var pgManager = new PropertyGridManager(pg);

            pgManager.RebuildEditTargets();

            // Initially hidden
            Assert.IsFalse(
                _GetAllTargets(pgManager.RootNode).Any(t => t.PropertyPathTarget == nameof(EditManagerShowIfSource.ConditionalValue)),
                "Should be hidden initially"
            );

            // Toggle on
            inner.ShowConditional = true;
            bool changed = pgManager.UpdateConditionalVisibility();
            Assert.IsTrue(changed, "Should report change");
            Assert.IsTrue(
                _GetAllTargets(pgManager.RootNode).Any(t => t.PropertyPathTarget == nameof(EditManagerShowIfSource.ConditionalValue)),
                "ConditionalValue should appear after toggle on"
            );

            // Toggle off
            inner.ShowConditional = false;
            changed = pgManager.UpdateConditionalVisibility();
            Assert.IsTrue(changed);
            Assert.IsFalse(
                _GetAllTargets(pgManager.RootNode).Any(t => t.PropertyPathTarget == nameof(EditManagerShowIfSource.ConditionalValue)),
                "ConditionalValue should disappear after toggle off"
            );
        }

        // ------ ShowIf + PGElevateAsParent ------

        [TestMethod]
        public void ShowIf_ElevateAsParent_HiddenWhenMethodReturnsFalse ()
        {
            var source = new ElevateShowIfMethodTestSource { ShowWrapped = false };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            var allTargets = _GetAllTargets(manager.RootNode);
            Assert.IsFalse(
                allTargets.Any(t => t.PropertyPathTarget == nameof(ElevateShowIfMethodTestSource.ConditionalWrapped)),
                "ConditionalWrapped should be hidden when ShouldShowWrapped() returns false"
            );
        }

        [TestMethod]
        public void ShowIf_ElevateAsParent_ShownWhenMethodReturnsTrue ()
        {
            var source = new ElevateShowIfMethodTestSource { ShowWrapped = true };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            var allTargets = _GetAllTargets(manager.RootNode);
            Assert.IsTrue(
                allTargets.Any(t => t.PropertyPathTarget == nameof(ElevateShowIfMethodTestSource.ConditionalWrapped)),
                "ConditionalWrapped should be visible when ShouldShowWrapped() returns true"
            );
        }

        [TestMethod]
        public void ShowIf_ElevateAsParent_TogglesCorrectly ()
        {
            var source = new ElevateShowIfMethodTestSource { ShowWrapped = false };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            // Initially hidden
            Assert.IsFalse(_GetAllTargets(manager.RootNode).Any(t => t.PropertyPathTarget == nameof(ElevateShowIfMethodTestSource.ConditionalWrapped)));

            // Show
            source.ShowWrapped = true;
            bool changed = manager.UpdateConditionalVisibility();
            Assert.IsTrue(changed, "Should report change");
            Assert.IsTrue(
                _GetAllTargets(manager.RootNode).Any(t => t.PropertyPathTarget == nameof(ElevateShowIfMethodTestSource.ConditionalWrapped)),
                "ConditionalWrapped should appear after toggle on"
            );

            // Hide again
            source.ShowWrapped = false;
            changed = manager.UpdateConditionalVisibility();
            Assert.IsTrue(changed);
            Assert.IsFalse(
                _GetAllTargets(manager.RootNode).Any(t => t.PropertyPathTarget == nameof(ElevateShowIfMethodTestSource.ConditionalWrapped)),
                "ConditionalWrapped should disappear after toggle off"
            );
        }

        [TestMethod]
        public void ShowIf_ElevateAsParent_PropertyBased_TogglesCorrectly ()
        {
            var source = new ElevateShowIfPropertyTestSource { ShowWrapped = false };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            // Initially hidden
            Assert.IsFalse(_GetAllTargets(manager.RootNode).Any(t => t.PropertyPathTarget == nameof(ElevateShowIfPropertyTestSource.ConditionalWrapped)));

            // Show
            source.ShowWrapped = true;
            manager.UpdateConditionalVisibility();
            Assert.IsTrue(
                _GetAllTargets(manager.RootNode).Any(t => t.PropertyPathTarget == nameof(ElevateShowIfPropertyTestSource.ConditionalWrapped)),
                "ConditionalWrapped should appear after property-based toggle on"
            );
        }

        // ------ ShowIf + PGElevateAsParent with derived class ------

        [TestMethod]
        public void ShowIf_ElevateAsParent_DerivedClass_HiddenWhenFalse ()
        {
            // The private ShouldShowWrapped() method is on the BASE class.
            // When the runtime type is the derived class, GetMethod with NonPublic
            // may not find private members from the base class.
            var source = new ElevateShowIfDerivedClass { ShowWrapped = false };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            var allTargets = _GetAllTargets(manager.RootNode);
            Assert.IsFalse(
                allTargets.Any(t => t.PropertyPathTarget == nameof(ElevateShowIfBaseClass.ConditionalWrapped)),
                "ConditionalWrapped should be hidden when ShouldShowWrapped() on base returns false"
            );
        }

        [TestMethod]
        public void ShowIf_ElevateAsParent_DerivedClass_ShownWhenTrue ()
        {
            var source = new ElevateShowIfDerivedClass { ShowWrapped = true };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            var allTargets = _GetAllTargets(manager.RootNode);
            Assert.IsTrue(
                allTargets.Any(t => t.PropertyPathTarget == nameof(ElevateShowIfBaseClass.ConditionalWrapped)),
                "ConditionalWrapped should be visible when ShouldShowWrapped() on base returns true"
            );
        }

        [TestMethod]
        public void ShowIf_ElevateAsParent_DerivedClass_TogglesCorrectly ()
        {
            var source = new ElevateShowIfDerivedClass { ShowWrapped = false };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            // Initially hidden
            Assert.IsFalse(_GetAllTargets(manager.RootNode).Any(t => t.PropertyPathTarget == nameof(ElevateShowIfBaseClass.ConditionalWrapped)));

            // Toggle on
            source.ShowWrapped = true;
            bool changed = manager.UpdateConditionalVisibility();
            Assert.IsTrue(changed);
            Assert.IsTrue(
                _GetAllTargets(manager.RootNode).Any(t => t.PropertyPathTarget == nameof(ElevateShowIfBaseClass.ConditionalWrapped)),
                "ConditionalWrapped should appear after toggle on via derived instance"
            );
        }

        // ------ Expansion state persistence ------

        [TestMethod]
        public void ExpansionState_PreservedAcrossRebuild ()
        {
            var source = new GroupedShowIfTestSource { ShowStats = true };
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            var statsGroup = manager.RootNode.Children
                .OfType<PropertyEditTarget>()
                .FirstOrDefault(t => t.PropertyPathTarget == "$group_Stats");
            Assert.IsNotNull(statsGroup);
            statsGroup.IsExpanded = true;

            // Rebuild
            manager.RebuildEditTargets();

            statsGroup = manager.RootNode.Children
                .OfType<PropertyEditTarget>()
                .FirstOrDefault(t => t.PropertyPathTarget == "$group_Stats");
            Assert.IsNotNull(statsGroup);
            Assert.IsTrue(statsGroup.IsExpanded, "Group expansion state should be preserved across rebuild");
        }

        // ------ Helper ------

        private static List<PropertyEditTarget> _GetAllTargets (PropertyEditTarget root)
        {
            var results = new List<PropertyEditTarget>();
            foreach (var node in TreeTraversal<IObservableTreeNode>.All(root).OfType<PropertyEditTarget>())
            {
                if (node != root)
                {
                    results.Add(node);
                }
            }

            return results;
        }
    }

    // ===========[ List editing test source types ]==========================================

    public class ListTestSource : NotifyPropertyChanged
    {
        [PGList]
        public List<string> Tags { get; set; } = new List<string> { "alpha", "beta", "gamma" };

        [PGList]
        public int[] Numbers { get; set; } = new int[] { 10, 20, 30 };

        public string Name { get; set; } = "Test";
    }

    public class ListWithCustomMethodsSource : NotifyPropertyChanged
    {
        [PGList(AddMethodName = nameof(AddTag), RemoveMethodName = nameof(RemoveTag))]
        public List<string> Tags { get; set; } = new List<string> { "one", "two" };

        public int AddCallCount { get; private set; }
        public int RemoveCallCount { get; private set; }

        public void AddTag ()
        {
            this.Tags.Add($"tag_{this.Tags.Count}");
            ++this.AddCallCount;
        }

        public void RemoveTag (int index)
        {
            if (index >= 0 && index < this.Tags.Count)
            {
                this.Tags.RemoveAt(index);
                ++this.RemoveCallCount;
            }
        }
    }

    public class ListNoReorderSource
    {
        [PGList(CanReorder = false)]
        public List<int> Items { get; set; } = new List<int> { 1, 2, 3 };
    }

    public class ListNoAddRemoveSource
    {
        [PGList(CanAdd = false, CanRemove = false)]
        public List<int> Items { get; set; } = new List<int> { 1, 2 };
    }

    public class EmptyListSource
    {
        [PGList]
        public List<string> Items { get; set; } = new List<string>();
    }

    public class ComplexListElementSource
    {
        [PGList]
        public List<SubItem> Items { get; set; } = new List<SubItem>
        {
            new SubItem { Name = "First", Value = 1 },
            new SubItem { Name = "Second", Value = 2 },
        };
    }

    public class SubItem
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }

    /// Source type for testing external array replacement on a [PGList] property.
    /// Simulates the pattern in Call Familiar where AspectEditorViewModel.AspectAnimationTimelines
    /// returns a different array when the underlying scene data changes, and raises PropertyChanged.
    public class ExternalArrayReplaceSource : NotifyPropertyChanged
    {
        private string[] m_items = ["alpha", "beta", "gamma"];

        [PGList]
        public string[] Items
        {
            get => m_items;
            set
            {
                m_items = value;
                this.RaisePropertyChanged(nameof(Items));
            }
        }
    }

    // ===========[ PGList tests ]==========================================

    [TestClass]
    public class PGListTests
    {
        // ------ Generation ------

        [TestMethod]
        public void List_GeneratesListTarget ()
        {
            var source = new ListTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();

            var listTarget = targets.FirstOrDefault(t => t.PropertyPathTarget == nameof(ListTestSource.Tags));
            Assert.IsNotNull(listTarget, "Tags list target should be generated");
            Assert.AreEqual("List", listTarget.Editor);
            Assert.IsTrue(listTarget.IsListEditor);
            Assert.IsTrue(listTarget.IsExpandable);
            Assert.IsTrue(listTarget.HasInlineEditor, "List editors should have inline editor AND be expandable");
        }

        [TestMethod]
        public void List_GeneratesChildrenForElements ()
        {
            var source = new ListTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var listTarget = targets.First(t => t.PropertyPathTarget == nameof(ListTestSource.Tags));

            Assert.AreEqual(3, listTarget.Children.Count, "Should have 3 children for 3 list elements");

            var child0 = listTarget.Children[0] as PropertyEditTarget;
            Assert.IsNotNull(child0);
            Assert.AreEqual("Tags[0]", child0.PropertyPathTarget);
            Assert.AreEqual("[0]", child0.DisplayName);
        }

        [TestMethod]
        public void List_ElementEditValueMatchesListContent ()
        {
            var source = new ListTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var listTarget = targets.First(t => t.PropertyPathTarget == nameof(ListTestSource.Tags));

            var child0 = listTarget.Children[0] as PropertyEditTarget;
            child0.Setup();
            Assert.AreEqual("alpha", child0.EditValue);

            var child1 = listTarget.Children[1] as PropertyEditTarget;
            child1.Setup();
            Assert.AreEqual("beta", child1.EditValue);
        }

        [TestMethod]
        public void List_ArrayGeneratesListTarget ()
        {
            var source = new ListTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();

            var arrayTarget = targets.FirstOrDefault(t => t.PropertyPathTarget == nameof(ListTestSource.Numbers));
            Assert.IsNotNull(arrayTarget, "Numbers array target should be generated");
            Assert.AreEqual("List", arrayTarget.Editor);
            Assert.IsTrue(arrayTarget.IsListEditor);
            Assert.AreEqual(3, arrayTarget.Children.Count);
        }

        [TestMethod]
        public void List_ContextHasCorrectCount ()
        {
            var source = new ListTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var listTarget = targets.First(t => t.PropertyPathTarget == nameof(ListTestSource.Tags));

            var context = listTarget.EditContext as PropertyGridListContext;
            Assert.IsNotNull(context);
            Assert.AreEqual(3, context.ElementCount);
            Assert.IsTrue(context.CanAdd);
            Assert.IsTrue(context.CanRemove);
            Assert.IsTrue(context.CanReorder);
        }

        [TestMethod]
        public void List_EmptyListHasNoChildren ()
        {
            var source = new EmptyListSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var listTarget = targets.First(t => t.PropertyPathTarget == nameof(EmptyListSource.Items));

            Assert.AreEqual(0, listTarget.Children.Count);

            var context = listTarget.EditContext as PropertyGridListContext;
            Assert.AreEqual(0, context.ElementCount);
        }

        // ------ Add ------

        [TestMethod]
        public void List_AddElementIncreasesCount ()
        {
            var source = new ListTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var listTarget = targets.First(t => t.PropertyPathTarget == nameof(ListTestSource.Tags));
            var context = listTarget.EditContext as PropertyGridListContext;

            context.AddElement();

            Assert.AreEqual(4, source.Tags.Count);
            Assert.AreEqual(4, context.ElementCount);
            Assert.AreEqual(4, listTarget.Children.Count);
        }

        [TestMethod]
        public void List_AddToArrayCreatesNewArray ()
        {
            var source = new ListTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var arrayTarget = targets.First(t => t.PropertyPathTarget == nameof(ListTestSource.Numbers));
            var context = arrayTarget.EditContext as PropertyGridListContext;

            int[] originalArray = source.Numbers;
            context.AddElement();

            Assert.AreEqual(4, source.Numbers.Length);
            Assert.AreNotSame(originalArray, source.Numbers, "Should create new array");
            Assert.AreEqual(10, source.Numbers[0]);
            Assert.AreEqual(20, source.Numbers[1]);
            Assert.AreEqual(30, source.Numbers[2]);
        }

        [TestMethod]
        public void List_CustomAddMethodCalled ()
        {
            var source = new ListWithCustomMethodsSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var listTarget = targets.First(t => t.PropertyPathTarget == nameof(ListWithCustomMethodsSource.Tags));
            var context = listTarget.EditContext as PropertyGridListContext;

            context.AddElement();

            Assert.AreEqual(1, source.AddCallCount);
            Assert.AreEqual(3, source.Tags.Count);
            Assert.AreEqual("tag_2", source.Tags[2]);
        }

        // ------ Remove ------

        [TestMethod]
        public void List_RemoveElementDecreasesCount ()
        {
            var source = new ListTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var listTarget = targets.First(t => t.PropertyPathTarget == nameof(ListTestSource.Tags));
            var context = listTarget.EditContext as PropertyGridListContext;

            context.RemoveElementAt(1);

            Assert.AreEqual(2, source.Tags.Count);
            Assert.AreEqual("alpha", source.Tags[0]);
            Assert.AreEqual("gamma", source.Tags[1]);
            Assert.AreEqual(2, context.ElementCount);
            Assert.AreEqual(2, listTarget.Children.Count);
        }

        [TestMethod]
        public void List_RemoveFromArrayCreatesNewArray ()
        {
            var source = new ListTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var arrayTarget = targets.First(t => t.PropertyPathTarget == nameof(ListTestSource.Numbers));
            var context = arrayTarget.EditContext as PropertyGridListContext;

            context.RemoveElementAt(0);

            Assert.AreEqual(2, source.Numbers.Length);
            Assert.AreEqual(20, source.Numbers[0]);
            Assert.AreEqual(30, source.Numbers[1]);
        }

        [TestMethod]
        public void List_CustomRemoveMethodCalled ()
        {
            var source = new ListWithCustomMethodsSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var listTarget = targets.First(t => t.PropertyPathTarget == nameof(ListWithCustomMethodsSource.Tags));
            var context = listTarget.EditContext as PropertyGridListContext;

            context.RemoveElementAt(0);

            Assert.AreEqual(1, source.RemoveCallCount);
            Assert.AreEqual(1, source.Tags.Count);
            Assert.AreEqual("two", source.Tags[0]);
        }

        // ------ Reorder ------

        [TestMethod]
        public void List_MoveElement ()
        {
            var source = new ListTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var listTarget = targets.First(t => t.PropertyPathTarget == nameof(ListTestSource.Tags));
            var context = listTarget.EditContext as PropertyGridListContext;

            context.MoveElement(0, 2);

            Assert.AreEqual("beta", source.Tags[0]);
            Assert.AreEqual("gamma", source.Tags[1]);
            Assert.AreEqual("alpha", source.Tags[2]);
        }

        [TestMethod]
        public void List_MoveElementInArray ()
        {
            var source = new ListTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var arrayTarget = targets.First(t => t.PropertyPathTarget == nameof(ListTestSource.Numbers));
            var context = arrayTarget.EditContext as PropertyGridListContext;

            context.MoveElement(2, 0);

            Assert.AreEqual(30, source.Numbers[0]);
            Assert.AreEqual(10, source.Numbers[1]);
            Assert.AreEqual(20, source.Numbers[2]);
        }

        // ------ Capability flags ------

        [TestMethod]
        public void List_NoReorderFlagDisablesReorder ()
        {
            var source = new ListNoReorderSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var listTarget = targets.First(t => t.PropertyPathTarget == nameof(ListNoReorderSource.Items));
            var context = listTarget.EditContext as PropertyGridListContext;

            Assert.IsFalse(context.CanReorder);
        }

        [TestMethod]
        public void List_NoAddRemoveFlagsDisableOperations ()
        {
            var source = new ListNoAddRemoveSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var listTarget = targets.First(t => t.PropertyPathTarget == nameof(ListNoAddRemoveSource.Items));
            var context = listTarget.EditContext as PropertyGridListContext;

            Assert.IsFalse(context.CanAdd);
            Assert.IsFalse(context.CanRemove);
        }

        // ------ Element context ------

        [TestMethod]
        public void List_ChildrenHaveElementContext ()
        {
            var source = new ListTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();
            var listTarget = targets.First(t => t.PropertyPathTarget == nameof(ListTestSource.Tags));

            var child0 = listTarget.Children[0] as PropertyEditTarget;
            var elemCtx = child0.EditContext as PropertyGridListElementContext;
            Assert.IsNotNull(elemCtx);
            Assert.AreEqual(0, elemCtx.Index);
            Assert.IsTrue(elemCtx.CanRemove);
            Assert.IsNotNull(elemCtx.RemoveCommand);
        }

        // ------ Manager integration ------

        [TestMethod]
        public void List_ManagerBuildsListTargets ()
        {
            var source = new ListTestSource();
            var pg = new SimpleTestPropertyGrid { SingleItemSource = source };
            var manager = new PropertyGridManager(pg);

            manager.RebuildEditTargets();

            var allTargets = _GetAllTargets(manager.RootNode);
            Assert.IsTrue(allTargets.Any(t => t.PropertyPathTarget == nameof(ListTestSource.Tags)));
            Assert.IsTrue(allTargets.Any(t => t.PropertyPathTarget == nameof(ListTestSource.Numbers)));
        }

        [TestMethod]
        public void List_NonListPropertiesUnaffected ()
        {
            var source = new ListTestSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();

            // The Name property should be a normal target, not a list
            var nameTarget = targets.First(t => t.PropertyPathTarget == nameof(ListTestSource.Name));
            Assert.IsFalse(nameTarget.IsListEditor);
            Assert.AreEqual("String", nameTarget.Editor);
        }

        // ------ External array replacement ------
        // These tests simulate the pattern where a VM property returns a different
        // array after its backing data changes, and raises PropertyChanged. The PG
        // should rebuild list children to reflect the new array contents.

        [TestMethod]
        public void List_ExternalArrayReplace_ChildrenRebuild ()
        {
            // 1. Build targets from initial 3-element array
            var source = new ExternalArrayReplaceSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();

            var listTarget = targets.First(t => t.PropertyPathTarget == nameof(ExternalArrayReplaceSource.Items));
            var context = listTarget.EditContext as PropertyGridListContext;

            Assert.AreEqual(3, listTarget.Children.Count, "Initial: should have 3 children");
            Assert.AreEqual(3, context.ElementCount, "Initial: context should show 3");

            // 2. Externally replace the array (simulates scene data swap)
            source.Items = ["one", "two"];

            // 3. Simulate what PropertyGrid.OnSourceItemPropertyChanged does:
            //    it calls RecacheEditValue on the matching target.
            listTarget.RecacheEditValue();

            // 4. Verify children rebuilt to match new array.
            Assert.AreEqual(2, listTarget.Children.Count, "After replace: should have 2 children");
            Assert.AreEqual(2, context.ElementCount, "After replace: context should show 2");

            var child0 = listTarget.Children[0] as PropertyEditTarget;
            child0.Setup();
            Assert.AreEqual("one", child0.EditValue, "First child should be 'one'");
        }

        [TestMethod]
        public void List_ExternalArrayReplace_EmptyArrayClearsChildren ()
        {
            var source = new ExternalArrayReplaceSource();
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();

            var listTarget = targets.First(t => t.PropertyPathTarget == nameof(ExternalArrayReplaceSource.Items));
            var context = listTarget.EditContext as PropertyGridListContext;

            Assert.AreEqual(3, listTarget.Children.Count, "Initial: should have 3 children");

            // Replace with empty array, then simulate PG recache
            source.Items = [];
            listTarget.RecacheEditValue();

            Assert.AreEqual(0, listTarget.Children.Count, "After empty replace: should have 0 children");
            Assert.AreEqual(0, context.ElementCount, "After empty replace: context should show 0");
        }

        [TestMethod]
        public void List_ExternalArrayReplace_FromEmptyToPopulated ()
        {
            // Start with empty array
            var source = new ExternalArrayReplaceSource();
            source.Items = [];
            var targets = PropertyEditTarget.GenerateForPropertiesOf(source).ToList();

            var listTarget = targets.First(t => t.PropertyPathTarget == nameof(ExternalArrayReplaceSource.Items));

            Assert.AreEqual(0, listTarget.Children.Count, "Initial: should have 0 children");

            // Externally populate, then simulate PG recache
            source.Items = ["x", "y", "z", "w"];
            listTarget.RecacheEditValue();

            Assert.AreEqual(4, listTarget.Children.Count, "After populate: should have 4 children");
        }

        // ------ Helper ------

        private static List<PropertyEditTarget> _GetAllTargets (PropertyEditTarget root)
        {
            var results = new List<PropertyEditTarget>();
            foreach (var node in TreeTraversal<IObservableTreeNode>.All(root).OfType<PropertyEditTarget>())
            {
                if (node != root)
                {
                    results.Add(node);
                }
            }

            return results;
        }
    }
}
