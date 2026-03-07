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
}
