namespace AJut.UX.WinUI3.Tests
{
    using System;
    using System.Threading;
    using AJut.UX.Controls;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Hosting;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using DPUtils = AJut.UX.DPUtils<EnumComboBoxTwoWayBindingTests.BoundProbe>;

    /// <summary>
    /// EnumComboBox.SelectedItem bound two-way straight to another object's enum dependency property, the way
    /// <c>SelectedItem="{x:Bind Other.Value, Mode=TwoWay}"</c> binds it.
    /// </summary>
    /// <remarks>
    /// The test project has no XAML compiler, so <see cref="TwoWayXBindStandIn"/> does what the generated binding code
    /// does. For a two-way x:Bind between two dependency properties, the generated code registers a property changed
    /// callback on each side, and each one copies the value across with the other side's CLR setter, comparing nothing.
    /// The only thing that ends a round trip is a side that takes an equal value without raising a change.
    /// </remarks>
    [TestClass]
    public class EnumComboBoxTwoWayBindingTests
    {
        private static readonly TimeSpan kWait = TimeSpan.FromSeconds(10);

        [TestMethod]
        public void EnumComboBox_TwoWayToADependencyProperty_PickingAValueSettles ()
        {
            BindingOutcome outcome = RunBound(eChoice.First, PickInComboBox);
            AssertSettledOn(eChoice.Third, outcome);
        }

        [TestMethod]
        public void EnumComboBox_TwoWayToADependencyProperty_SettingTheBoundPropertySettles ()
        {
            BindingOutcome outcome = RunBound(eChoice.First, SetOnProbe);
            AssertSettledOn(eChoice.Third, outcome);
        }

        private static void PickInComboBox (EnumComboBox comboBox, BoundProbe probe) => comboBox.SelectedItem = eChoice.Third;

        private static void SetOnProbe (EnumComboBox comboBox, BoundProbe probe) => probe.Value = eChoice.Third;

        private static void AssertSettledOn (eChoice expected, BindingOutcome outcome)
        {
            Assert.IsNull(outcome.Failure, $"The bound run threw: {outcome.Failure}");
            Assert.IsFalse(
                outcome.HitHopLimit,
                $"The binding never settled: it copied to the bound property {outcome.ToSourceCount} times and to SelectedItem {outcome.ToTargetCount} times before the test stopped it"
            );
            Assert.AreEqual(expected, outcome.SelectedItem);
            Assert.AreEqual(expected, outcome.ProbeValue);
        }

        // Dependency objects need XAML running on their thread, which a test thread doesn't have, so the run happens on a
        // dispatcher queue thread of its own with XAML started there
        private static BindingOutcome RunBound (eChoice start, Action<EnumComboBox, BoundProbe> change)
        {
            DispatcherQueueController controller = DispatcherQueueController.CreateOnDedicatedThread();
            try
            {
                var run = new BoundRun(start, change);
                Assert.IsTrue(controller.DispatcherQueue.TryEnqueue(run.Run), "The queue takes work");
                Assert.IsTrue(run.Done.Wait(kWait), "The bound run finishes");
                return run.Outcome;
            }
            finally
            {
                Assert.IsTrue(controller.ShutdownQueueAsync().AsTask().Wait(kWait), "The queue shuts down");
            }
        }

        // ===========[ Subclasses/structs ]===================================
        public enum eChoice
        {
            First,
            Second,
            Third,
        }

        /// <summary>
        /// Another object's enum dependency property, with a plain CLR wrapper the way most are written: nothing in it
        /// skips an equal value.
        /// </summary>
        public sealed class BoundProbe : DependencyObject
        {
            public static readonly DependencyProperty ValueProperty = DPUtils.Register(_ => _.Value);
            public eChoice Value
            {
                get => (eChoice)this.GetValue(ValueProperty);
                set => this.SetValue(ValueProperty, value);
            }
        }

        private sealed record BindingOutcome (
            object SelectedItem,
            eChoice ProbeValue,
            int ToSourceCount,
            int ToTargetCount,
            bool HitHopLimit,
            Exception Failure
        );

        private sealed class BoundRun
        {
            private readonly eChoice m_start;
            private readonly Action<EnumComboBox, BoundProbe> m_change;

            public BoundRun (eChoice start, Action<EnumComboBox, BoundProbe> change)
            {
                m_start = start;
                m_change = change;
            }

            public ManualResetEventSlim Done { get; } = new ManualResetEventSlim();

            public BindingOutcome Outcome { get; private set; }

            public void Run ()
            {
                WindowsXamlManager xaml = null;
                try
                {
                    xaml = WindowsXamlManager.InitializeForCurrentThread();
                    var comboBox = new EnumComboBox();
                    var probe = new BoundProbe { Value = m_start };
                    var binding = new TwoWayXBindStandIn(comboBox, probe);

                    m_change(comboBox, probe);
                    binding.Detach();

                    this.Outcome = new BindingOutcome(
                        comboBox.SelectedItem,
                        probe.Value,
                        binding.ToSourceCount,
                        binding.ToTargetCount,
                        binding.HitHopLimit,
                        null
                    );
                }
                catch (Exception failure)
                {
                    this.Outcome = new BindingOutcome(null, default, 0, 0, false, failure);
                }
                finally
                {
                    xaml?.Dispose();
                    this.Done.Set();
                }
            }
        }

        /// <summary>
        /// Does what the code generated for <c>SelectedItem="{x:Bind Probe.Value, Mode=TwoWay}"</c> does: sets
        /// SelectedItem from the source once, then copies each change across, in both directions, through the CLR setters.
        /// It stops copying after <see cref="kHopLimit"/> copies, so a binding that never settles fails the test instead
        /// of overflowing the stack and taking the test host down with it.
        /// </summary>
        private sealed class TwoWayXBindStandIn
        {
            private const int kHopLimit = 32;
            private readonly EnumComboBox m_comboBox;
            private readonly BoundProbe m_probe;
            private readonly long m_comboBoxToken;
            private readonly long m_probeToken;

            public TwoWayXBindStandIn (EnumComboBox comboBox, BoundProbe probe)
            {
                m_comboBox = comboBox;
                m_probe = probe;

                m_comboBox.SelectedItem = m_probe.Value;
                m_comboBoxToken = m_comboBox.RegisterPropertyChangedCallback(
                    EnumComboBox.SelectedItemProperty,
                    this.ComboBox_OnSelectedItemChanged
                );
                m_probeToken = m_probe.RegisterPropertyChangedCallback(
                    BoundProbe.ValueProperty,
                    this.Probe_OnValueChanged
                );
            }

            public int ToSourceCount { get; private set; }

            public int ToTargetCount { get; private set; }

            public bool HitHopLimit => this.ToSourceCount + this.ToTargetCount >= kHopLimit;

            public void Detach ()
            {
                m_comboBox.UnregisterPropertyChangedCallback(EnumComboBox.SelectedItemProperty, m_comboBoxToken);
                m_probe.UnregisterPropertyChangedCallback(BoundProbe.ValueProperty, m_probeToken);
            }

            // The generated UpdateTwoWay: target to source
            private void ComboBox_OnSelectedItemChanged (DependencyObject sender, DependencyProperty property)
            {
                if (this.HitHopLimit)
                {
                    return;
                }

                ++this.ToSourceCount;
                m_probe.Value = (eChoice)m_comboBox.SelectedItem;
            }

            // The generated source tracking: source to target
            private void Probe_OnValueChanged (DependencyObject sender, DependencyProperty property)
            {
                if (this.HitHopLimit)
                {
                    return;
                }

                ++this.ToTargetCount;
                m_comboBox.SelectedItem = m_probe.Value;
            }
        }
    }
}
