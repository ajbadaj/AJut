#nullable enable

namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Windows.Input;
    using AJut.UX.CodeBinding;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BindingSetTests
    {
        private QueuedContext m_mainThread = null!;
        private SynchronizationContext? m_previousContext;

        // Each test runs as if the test thread were the main thread: its SynchronizationContext is a queue the test
        // drains by hand, so "posted, not run inline" is something a test can see.
        [TestInitialize]
        public void Setup ()
        {
            m_previousContext = SynchronizationContext.Current;
            m_mainThread = new QueuedContext();
            SynchronizationContext.SetSynchronizationContext(m_mainThread);
        }

        [TestCleanup]
        public void Teardown ()
        {
            SynchronizationContext.SetSynchronizationContext(m_previousContext);
        }

        // ===[ Construction ]===

        [TestMethod]
        public void BS_Construct_NoContextOnThread_Throws ()
        {
            SynchronizationContext.SetSynchronizationContext(null);
            Assert.ThrowsException<InvalidOperationException>(() => new BindingSet());
        }

        [TestMethod]
        public void BS_Construct_CapturesCurrentContext ()
        {
            using var set = new BindingSet();
            Assert.AreSame(m_mainThread, set.MainThread);
        }

        // ===[ Watch ]===

        [TestMethod]
        public void BS_Watch_PushesOnBind ()
        {
            var source = new TestSource();
            int pushes = 0;
            using var set = new BindingSet();

            set.Watch(source, nameof(TestSource.Name), () => ++pushes);

            Assert.AreEqual(1, pushes);
        }

        [TestMethod]
        public void BS_Watch_PushesOnNamedChange_NotOnOtherNames ()
        {
            var source = new TestSource();
            int pushes = 0;
            using var set = new BindingSet();
            set.Watch(source, nameof(TestSource.Name), () => ++pushes);

            source.Name = "changed";
            source.Count = 5;

            Assert.AreEqual(2, pushes, "The opening push plus the Name change; the Count change must not reach it");
        }

        [TestMethod]
        public void BS_Watch_EmptyNameChange_ReachesEveryPush ()
        {
            var source = new TestSource();
            int namePushes = 0;
            int countPushes = 0;
            using var set = new BindingSet();
            set.Watch(source, nameof(TestSource.Name), () => ++namePushes);
            set.Watch(source, nameof(TestSource.Count), () => ++countPushes);

            source.RaiseEverythingChanged();

            Assert.AreEqual(2, namePushes);
            Assert.AreEqual(2, countPushes);
        }

        [TestMethod]
        public void BS_Watch_ManyNames_PushesOnceOnBind_AndOnEach ()
        {
            var source = new TestSource();
            int pushes = 0;
            using var set = new BindingSet();
            set.Watch(source, new[] { nameof(TestSource.Name), nameof(TestSource.Count) }, () => ++pushes);

            Assert.AreEqual(1, pushes);
            source.Name = "a";
            source.Count = 1;
            Assert.AreEqual(3, pushes);

            source.RaiseEverythingChanged();
            Assert.AreEqual(4, pushes, "An empty-name change runs a push watching two names once, not once per name");
        }

        [TestMethod]
        public void BS_Watch_OneSubscriptionPerSource ()
        {
            var source = new TestSource();
            using var set = new BindingSet();
            set.Watch(source, nameof(TestSource.Name), Nothing);
            set.Watch(source, nameof(TestSource.Count), Nothing);

            Assert.AreEqual(1, source.SubscriberCount);
        }

        [TestMethod]
        public void BS_Watch_SourcesThatCompareEqual_EachGetTheirOwnSubscription ()
        {
            var first = new AlwaysEqualSource();
            var second = new AlwaysEqualSource();
            int secondPushes = 0;
            using var set = new BindingSet();
            set.Watch(first, nameof(TestSource.Name), Nothing);
            set.Watch(second, nameof(TestSource.Name), () => ++secondPushes);

            second.Name = "changed";

            Assert.AreEqual(1, first.SubscriberCount);
            Assert.AreEqual(1, second.SubscriberCount);
            Assert.AreEqual(2, secondPushes);
        }

        [TestMethod]
        public void BS_Watch_PushThatHooksTheSameSource_DoesNotThrow ()
        {
            var source = new TestSource();
            int latePushes = 0;
            using var set = new BindingSet();
            bool hooked = false;
            set.Watch(source, nameof(TestSource.Name), _HookOnce);

            source.Name = "changed";

            Assert.AreEqual(1, latePushes, "The late hookup runs its opening push only, not the change that added it");

            void _HookOnce ()
            {
                if (source.Name == "changed" && !hooked)
                {
                    hooked = true;
                    set.Watch(source, nameof(TestSource.Name), () => ++latePushes);
                }
            }
        }

        // ===[ Main thread ]===

        [TestMethod]
        public void BS_Watch_ChangeOffTheMainThread_IsPostedNotRunInline ()
        {
            var source = new TestSource();
            int pushes = 0;
            using var set = new BindingSet();
            set.Watch(source, nameof(TestSource.Name), () => ++pushes);

            RunOnWorkerThread(() => source.Name = "from a worker");

            Assert.AreEqual(1, pushes, "Only the opening push may have run before the main thread drains");
            Assert.AreEqual(1, m_mainThread.PendingCount);

            m_mainThread.RunAll();
            Assert.AreEqual(2, pushes);
        }

        [TestMethod]
        public void BS_Watch_PushQueuedBeforeDispose_IsSkipped ()
        {
            var source = new TestSource();
            int pushes = 0;
            var set = new BindingSet();
            set.Watch(source, nameof(TestSource.Name), () => ++pushes);

            RunOnWorkerThread(() => source.Name = "from a worker");
            set.Dispose();
            m_mainThread.RunAll();

            Assert.AreEqual(1, pushes, "Only the opening push ran; the one queued before Dispose must be skipped");
        }

        [TestMethod]
        public void BS_Watch_ChangeOnTheMainThread_RunsInline_WhateverItsCurrentContext ()
        {
            // The main thread is the thread the set was made on, not whichever context object it reports right now: a
            // toolkit that hands out a different context instance on the main thread must not turn inline pushes async.
            var source = new TestSource();
            int pushes = 0;
            using var set = new BindingSet(m_mainThread);
            set.Watch(source, nameof(TestSource.Name), () => ++pushes);
            SynchronizationContext.SetSynchronizationContext(new QueuedContext());

            source.Name = "changed";

            Assert.AreEqual(2, pushes, "A change raised on the thread the set was made on runs inline");
            Assert.AreEqual(0, m_mainThread.PendingCount);
        }

        [TestMethod]
        public void BS_Watch_ChangeOffTheMainThread_FansOutOnTheMainThread ()
        {
            // Off the main thread nothing may read the set's push lists, since the main thread changes them (a new Watch,
            // a Dispose) at any moment. So the change is posted once, and the lookup happens when it runs on the main
            // thread: a push hooked between the raise and the drain is reached by it.
            var source = new TestSource();
            int latePushes = 0;
            using var set = new BindingSet();
            set.Watch(source, nameof(TestSource.Name), Nothing);

            RunOnWorkerThread(() => source.Name = "from a worker");
            set.Watch(source, nameof(TestSource.Name), () => ++latePushes);
            m_mainThread.RunAll();

            Assert.AreEqual(2, latePushes, "The opening push, then the posted change, looked up on the main thread");
        }

        [TestMethod]
        public void BS_Hookups_OffTheMainThread_Throw ()
        {
            // Hookups and Dispose change the set's lists, which only the main thread may touch
            var source = new TestSource();
            using var set = new BindingSet();
            Exception? watchFailure = null;
            Exception? disposeFailure = null;

            RunOnWorkerThread(_TryFromWorker);

            Assert.IsInstanceOfType(watchFailure, typeof(InvalidOperationException));
            Assert.IsInstanceOfType(disposeFailure, typeof(InvalidOperationException));
            Assert.AreEqual(0, source.SubscriberCount);

            void _TryFromWorker ()
            {
                try { set.Watch(source, nameof(TestSource.Name), Nothing); }
                catch (Exception exc) { watchFailure = exc; }

                try { set.Dispose(); }
                catch (Exception exc) { disposeFailure = exc; }
            }
        }

        // ===[ Two way ]===

        [TestMethod]
        public void BS_TwoWay_PushesSourceToTargetOnBind ()
        {
            var source = new TestSource { Name = "start" };
            var target = new TestTarget<string>();
            using var set = new BindingSet();

            BindName(set, source, target);

            Assert.AreEqual("start", target.Value);
            Assert.AreEqual(1, target.WriteCount);
        }

        [TestMethod]
        public void BS_TwoWay_SourceChange_PushesToTarget ()
        {
            var source = new TestSource { Name = "start" };
            var target = new TestTarget<string>();
            using var set = new BindingSet();
            BindName(set, source, target);

            source.Name = "changed";

            Assert.AreEqual("changed", target.Value);
        }

        [TestMethod]
        public void BS_TwoWay_TargetEdit_WritesSourceOnce ()
        {
            var source = new TestSource { Name = "start" };
            var target = new TestTarget<string>();
            using var set = new BindingSet();
            BindingSet.TwoWayChannel channel = BindName(set, source, target);
            int writesBefore = source.NameWriteCount;

            target.Value = "typed";
            channel.OnTargetEdited();

            Assert.AreEqual("typed", source.Name);
            Assert.AreEqual(writesBefore + 1, source.NameWriteCount);
            Assert.AreEqual(1, target.WriteCount, "Writing the source raises a change that pushes back an equal value, which must leave the target alone");
        }

        [TestMethod]
        public void BS_TwoWay_EditRaisedDuringAPush_IsNotWrittenBack ()
        {
            // Android's TextChanged and CheckedChange fire for programmatic sets, so a push makes the target raise
            // its own edit event. The guard has to swallow that echo.
            var source = new TestSource { Name = "start" };
            var target = new TestTarget<string>();
            using var set = new BindingSet();
            BindingSet.TwoWayChannel channel = null!;
            target.OnWritten = () => channel?.OnTargetEdited();
            channel = BindName(set, source, target);
            int writesBefore = source.NameWriteCount;

            source.Name = "changed";

            Assert.AreEqual("changed", target.Value);
            Assert.AreEqual(writesBefore + 1, source.NameWriteCount, "Only the test's own write may reach the source");
        }

        [TestMethod]
        public void BS_TwoWay_TargetAlreadyShowsTheValue_IsNotWritten ()
        {
            var source = new TestSource { Name = "same" };
            var target = new TestTarget<string> { Value = "same" };
            using var set = new BindingSet();

            BindName(set, source, target);
            source.RaiseEverythingChanged();

            Assert.AreEqual(0, target.WriteCount);
        }

        [TestMethod]
        public void BS_TwoWay_Conversion_ConvertsBothWays_AndARefusalWritesNothing ()
        {
            var source = new TestSource { Count = 7 };
            var target = new TestTarget<string>();
            using var set = new BindingSet();
            var conversion = new BindingConversion<int, string>(_ToText, _TryParse);
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, nameof(TestSource.Count),
                () => source.Count, value => source.Count = value,
                () => target.Value, target.Push,
                conversion
            );

            Assert.AreEqual("7", target.Value);

            target.Value = "not a number";
            channel.OnTargetEdited();
            Assert.AreEqual(7, source.Count, "A refused ConvertBack must leave the source alone");

            target.Value = "42";
            channel.OnTargetEdited();
            Assert.AreEqual(42, source.Count);

            static string _ToText (int value) => value.ToString();
            static bool _TryParse (string text, out int value) => int.TryParse(text, out value);
        }

        [TestMethod]
        public void BS_TwoWay_NoConvertBack_ShowsTheSource_AndIgnoresEdits ()
        {
            var source = new TestSource { Count = 3 };
            var target = new TestTarget<string>();
            using var set = new BindingSet();
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, nameof(TestSource.Count),
                () => source.Count, value => source.Count = value,
                () => target.Value, target.Push,
                new BindingConversion<int, string>(_ToText)
            );

            target.Value = "99";
            channel.OnTargetEdited();

            Assert.AreEqual(3, source.Count);
            source.Count = 4;
            Assert.AreEqual("4", target.Value);

            static string _ToText (int value) => value.ToString();
        }

        [TestMethod]
        public void BS_TwoWay_ToleranceComparer_LeavesANearlyEqualTargetAlone ()
        {
            var source = new TestSource { Ratio = 0.5 };
            var target = new TestTarget<double> { Value = 0.5000000001 };
            using var set = new BindingSet();

            set.TwoWay(
                source, nameof(TestSource.Ratio),
                () => source.Ratio, value => source.Ratio = value,
                () => target.Value, target.Push,
                BindingComparers.Tolerance(1e-6)
            );

            Assert.AreEqual(0, target.WriteCount);
            source.Ratio = 0.75;
            Assert.AreEqual(0.75, target.Value);
        }

        [TestMethod]
        public void BS_ToleranceComparer_TreatsEqualNaNAndInfinityAsEqual ()
        {
            IEqualityComparer<double> comparer = BindingComparers.Tolerance(1e-6);

            Assert.IsTrue(comparer.Equals(double.NaN, double.NaN));
            Assert.IsTrue(comparer.Equals(double.PositiveInfinity, double.PositiveInfinity));
            Assert.IsFalse(comparer.Equals(double.PositiveInfinity, double.NegativeInfinity));
            Assert.IsTrue(comparer.Equals(1.0, 1.0000001));
        }

        [TestMethod]
        public void BS_TwoWay_DefaultComparer_PushesASmallRealChange ()
        {
            var source = new TestSource { Ratio = 0.5 };
            var target = new TestTarget<double>();
            using var set = new BindingSet();
            set.TwoWay(
                source, nameof(TestSource.Ratio),
                () => source.Ratio, value => source.Ratio = value,
                () => target.Value, target.Push
            );

            source.Ratio = 0.5000001;

            Assert.AreEqual(0.5000001, target.Value, "With no tolerance asked for, a small change made from code must reach the target");
        }

        [TestMethod]
        public void BS_TwoWay_EditAfterDispose_IsIgnored ()
        {
            var source = new TestSource { Name = "start" };
            var target = new TestTarget<string>();
            var set = new BindingSet();
            BindingSet.TwoWayChannel channel = BindName(set, source, target);
            set.Dispose();

            target.Value = "late";
            channel.OnTargetEdited();

            Assert.AreEqual("start", source.Name);
        }

        // ===[ Nested ]===

        [TestMethod]
        public void BS_WatchNested_BindsTheChild ()
        {
            var child = new TestSource { Name = "child" };
            var source = new TestSource { Child = child };
            string? shown = null;
            using var set = new BindingSet();

            set.WatchNested(source, nameof(TestSource.Child), () => source.Child, (childSet, bound) => childSet.Watch(bound, nameof(TestSource.Name), () => shown = bound.Name));
            Assert.AreEqual("child", shown);

            child.Name = "renamed";
            Assert.AreEqual("renamed", shown);
        }

        [TestMethod]
        public void BS_WatchNested_ChildSwap_UnhooksTheOldChild_AndHooksTheNewOne ()
        {
            var oldChild = new TestSource { Name = "old" };
            var newChild = new TestSource { Name = "new" };
            var source = new TestSource { Child = oldChild };
            string? shown = null;
            using var set = new BindingSet();
            set.WatchNested(source, nameof(TestSource.Child), () => source.Child, (childSet, bound) => childSet.Watch(bound, nameof(TestSource.Name), () => shown = bound.Name));

            source.Child = newChild;

            Assert.AreEqual("new", shown);
            Assert.AreEqual(0, oldChild.SubscriberCount, "The old child must be fully unhooked");
            Assert.AreEqual(1, newChild.SubscriberCount);

            oldChild.Name = "stale";
            Assert.AreEqual("new", shown, "A change on the old child must not reach the binding any more");

            newChild.Name = "fresh";
            Assert.AreEqual("fresh", shown);
        }

        [TestMethod]
        public void BS_WatchNested_NullChild_BindsNothing_UntilOneArrives ()
        {
            var source = new TestSource();
            int bindCount = 0;
            using var set = new BindingSet();
            set.WatchNested(source, nameof(TestSource.Child), () => source.Child, (childSet, bound) => ++bindCount);

            Assert.AreEqual(0, bindCount);

            source.Child = new TestSource();
            Assert.AreEqual(1, bindCount);

            source.Child = null;
            Assert.AreEqual(1, bindCount);
        }

        [TestMethod]
        public void BS_WatchNested_SameChildReraised_KeepsItsBindings ()
        {
            var child = new TestSource();
            var source = new TestSource { Child = child };
            int bindCount = 0;
            using var set = new BindingSet();
            set.WatchNested(source, nameof(TestSource.Child), () => source.Child, _Bind);

            source.Raise(nameof(TestSource.Child));
            source.RaiseEverythingChanged();

            Assert.AreEqual(1, bindCount, "A change that leaves the same child in place must not tear down and rebind");
            Assert.AreEqual(1, child.SubscriberCount);

            void _Bind (BindingSet childSet, TestSource bound)
            {
                ++bindCount;
                childSet.Watch(bound, nameof(TestSource.Name), Nothing);
            }
        }

        [TestMethod]
        public void BS_WatchNested_TwoLevelsDeep_ReHooksAtTheMiddle ()
        {
            var leafA = new TestSource { Name = "a" };
            var leafB = new TestSource { Name = "b" };
            var middle = new TestSource { Child = leafA };
            var root = new TestSource { Child = middle };
            string? shown = null;
            using var set = new BindingSet();
            set.WatchNested(root, nameof(TestSource.Child), () => root.Child, _BindMiddle);

            middle.Child = leafB;

            Assert.AreEqual("b", shown);
            Assert.AreEqual(0, leafA.SubscriberCount);

            void _BindMiddle (BindingSet middleSet, TestSource boundMiddle)
            {
                middleSet.WatchNested(boundMiddle, nameof(TestSource.Child), () => boundMiddle.Child, _BindLeaf);
            }

            void _BindLeaf (BindingSet leafSet, TestSource boundLeaf)
            {
                leafSet.Watch(boundLeaf, nameof(TestSource.Name), () => shown = boundLeaf.Name);
            }
        }

        [TestMethod]
        public void BS_WatchNested_Dispose_UnhooksTheChild ()
        {
            var child = new TestSource();
            var source = new TestSource { Child = child };
            var set = new BindingSet();
            set.WatchNested(source, nameof(TestSource.Child), () => source.Child, (childSet, bound) => childSet.Watch(bound, nameof(TestSource.Name), Nothing));

            set.Dispose();

            Assert.AreEqual(0, child.SubscriberCount);
            Assert.AreEqual(0, source.SubscriberCount);
        }

        // ===[ Commands ]===

        [TestMethod]
        public void BS_Command_PushesCanExecuteOnBind_AndOnChange ()
        {
            var command = new TestCommand { CanExecuteValue = false };
            var enabledPushes = new List<bool>();
            using var set = new BindingSet();

            set.Command(command, null, enabledPushes.Add);
            command.CanExecuteValue = true;
            command.RaiseCanExecuteChanged();

            CollectionAssert.AreEqual(new[] { false, true }, enabledPushes);
        }

        [TestMethod]
        public void BS_Command_Activate_ExecutesWithTheParameter ()
        {
            var command = new TestCommand { CanExecuteValue = true };
            using var set = new BindingSet();
            BindingSet.CommandChannel channel = set.Command(command, "the parameter", Nothing);

            channel.OnActivated();

            Assert.AreEqual(1, command.ExecuteCount);
            Assert.AreEqual("the parameter", command.LastParameter);
        }

        [TestMethod]
        public void BS_Command_ActivateWhileItCannotExecute_DoesNotExecute ()
        {
            // A disabled view can still be clicked from code, so CanExecute is checked at activation too.
            var command = new TestCommand { CanExecuteValue = false };
            using var set = new BindingSet();
            BindingSet.CommandChannel channel = set.Command(command, null, Nothing);

            channel.OnActivated();

            Assert.AreEqual(0, command.ExecuteCount);
        }

        [TestMethod]
        public void BS_Command_CanExecuteChangedOffTheMainThread_IsPosted ()
        {
            var command = new TestCommand { CanExecuteValue = false };
            var enabledPushes = new List<bool>();
            using var set = new BindingSet();
            set.Command(command, null, enabledPushes.Add);
            command.CanExecuteValue = true;

            RunOnWorkerThread(command.RaiseCanExecuteChanged);

            Assert.AreEqual(1, enabledPushes.Count);
            m_mainThread.RunAll();
            CollectionAssert.AreEqual(new[] { false, true }, enabledPushes);
        }

        [TestMethod]
        public void BS_Command_AfterDispose_HasNoSubscribers_AndActivateDoesNothing ()
        {
            var command = new TestCommand { CanExecuteValue = true };
            var set = new BindingSet();
            BindingSet.CommandChannel channel = set.Command(command, null, Nothing);

            set.Dispose();
            channel.OnActivated();

            Assert.AreEqual(0, command.SubscriberCount);
            Assert.AreEqual(0, command.ExecuteCount);
        }

        // ===[ Collections ]===

        [TestMethod]
        public void BS_WatchCollection_ResetsOnBind_ThenHandsOnEachChange ()
        {
            var collection = new TestCollection();
            var changes = new List<NotifyCollectionChangedEventArgs>();
            using var set = new BindingSet();

            set.WatchCollection(collection, changes.Add);
            var added = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, "item", 3);
            collection.Raise(added);

            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(NotifyCollectionChangedAction.Reset, changes[0].Action, "The opening call is a Reset, so an adapter loads everything");
            Assert.AreSame(added, changes[1], "A change reaches the adapter as raised, so a table can apply it in place");
        }

        [TestMethod]
        public void BS_WatchCollection_ChangeOffTheMainThread_IsPosted ()
        {
            var collection = new TestCollection();
            var changes = new List<NotifyCollectionChangedEventArgs>();
            using var set = new BindingSet();
            set.WatchCollection(collection, changes.Add);

            RunOnWorkerThread(collection.RaiseReset);

            Assert.AreEqual(1, changes.Count);
            m_mainThread.RunAll();
            Assert.AreEqual(2, changes.Count);
        }

        // ===[ Teardown ]===

        [TestMethod]
        public void BS_Dispose_EmptiesEveryInvocationList ()
        {
            var source = new TestSource { Child = new TestSource() };
            var collection = new TestCollection();
            var command = new TestCommand();
            var target = new TestTarget<string>();
            var set = new BindingSet();
            set.Watch(source, nameof(TestSource.Count), Nothing);
            BindName(set, source, target);
            set.WatchNested(source, nameof(TestSource.Child), () => source.Child, (childSet, bound) => childSet.Watch(bound, nameof(TestSource.Name), Nothing));
            set.WatchCollection(collection, Nothing);
            set.Command(command, null, Nothing);

            set.Dispose();

            Assert.AreEqual(0, source.SubscriberCount);
            Assert.AreEqual(0, source.Child!.SubscriberCount);
            Assert.AreEqual(0, collection.SubscriberCount);
            Assert.AreEqual(0, command.SubscriberCount);
            Assert.IsTrue(set.IsDisposed);
        }

        [TestMethod]
        public void BS_Dispose_RunsAddedLinks_InReverseOrder ()
        {
            var order = new List<string>();
            var set = new BindingSet();
            set.Add(new RecordingLink("first", order));
            set.Add(new RecordingLink("second", order));

            set.Dispose();
            set.Dispose();

            CollectionAssert.AreEqual(new[] { "second", "first" }, order, "Reverse order, and only once");
        }

        [TestMethod]
        public void BS_Dispose_ALinkThatThrows_DoesNotStopTheRest ()
        {
            // An adapter whose native view is already gone can throw while unhooking; every other link must still unhook
            var order = new List<string>();
            var set = new BindingSet();
            set.Add(new RecordingLink("first", order));
            set.Add(new ThrowingLink());

            set.Dispose();

            CollectionAssert.AreEqual(new[] { "first" }, order, "The link added before the throwing one must still be disposed");
            Assert.IsTrue(set.IsDisposed);
        }

        [TestMethod]
        public void BS_Hookups_AfterDispose_Throw ()
        {
            // A late hookup on a disposed set would subscribe and never be undone: a leak with no symptom.
            var source = new TestSource();
            var set = new BindingSet();
            set.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => set.Watch(source, nameof(TestSource.Name), Nothing));
            Assert.ThrowsException<ObjectDisposedException>(() => set.Watch(source, new[] { nameof(TestSource.Name) }, Nothing));
            Assert.ThrowsException<ObjectDisposedException>(() => set.WatchCollection(new TestCollection(), Nothing));
            Assert.ThrowsException<ObjectDisposedException>(() => set.WatchNested(source, nameof(TestSource.Child), () => source.Child, (childSet, bound) => { }));
            Assert.ThrowsException<ObjectDisposedException>(() => set.TwoWay(source, nameof(TestSource.Name), () => source.Name, value => source.Name = value, () => "", value => { }));
            Assert.ThrowsException<ObjectDisposedException>(() => set.Command(new TestCommand(), null, Nothing));
            Assert.ThrowsException<ObjectDisposedException>(() => set.Add(new RecordingLink("late", new List<string>())));
            Assert.AreEqual(0, source.SubscriberCount);
        }

        [TestMethod]
        public void BS_Dispose_DuringAPush_StopsTheRemainingPushes ()
        {
            var source = new TestSource();
            int laterPushes = 0;
            var set = new BindingSet();
            set.Watch(source, nameof(TestSource.Name), _DisposeOnChange);
            set.Watch(source, nameof(TestSource.Name), () => ++laterPushes);

            source.Name = "changed";

            Assert.AreEqual(1, laterPushes, "Only the opening push; the set was disposed before the change reached it");
            Assert.AreEqual(0, source.SubscriberCount);

            void _DisposeOnChange ()
            {
                if (source.Name == "changed")
                {
                    set.Dispose();
                }
            }
        }

        [TestMethod]
        public void BS_Dispose_LeavesTheTargetCollectable ()
        {
            var source = new TestSource { Name = "start" };
            var set = new BindingSet();
            WeakReference target = BindToATargetOnlyTheSetHolds(set, source);

            set.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.IsFalse(target.IsAlive, "After Dispose the long lived source must not keep the target alive");
        }

        // ===========[ Helper Methods ]===================================
        private static void Nothing () { }
        private static void Nothing (bool _) { }
        private static void Nothing (NotifyCollectionChangedEventArgs _) { }

        private static BindingSet.TwoWayChannel BindName (BindingSet set, TestSource source, TestTarget<string> target)
        {
            return set.TwoWay(
                source, nameof(TestSource.Name),
                () => source.Name, value => source.Name = value,
                () => target.Value, target.Push
            );
        }

        // Kept out of line so the JIT holds no hidden local reference to the target in the calling test.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference BindToATargetOnlyTheSetHolds (BindingSet set, TestSource source)
        {
            var target = new TestTarget<string>();
            BindName(set, source, target);
            return new WeakReference(target);
        }

        private static void RunOnWorkerThread (Action action)
        {
            var worker = new Thread(new ThreadStart(action));
            worker.Start();
            worker.Join();
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class QueuedContext : SynchronizationContext
        {
            private readonly Queue<PendingPost> m_pending = new Queue<PendingPost>();
            private readonly object m_lock = new object();

            public int PendingCount
            {
                get
                {
                    lock (m_lock)
                    {
                        return m_pending.Count;
                    }
                }
            }

            public override void Post (SendOrPostCallback d, object? state)
            {
                lock (m_lock)
                {
                    m_pending.Enqueue(new PendingPost(d, state));
                }
            }

            public void RunAll ()
            {
                while (true)
                {
                    PendingPost next;
                    lock (m_lock)
                    {
                        if (m_pending.Count == 0)
                        {
                            return;
                        }

                        next = m_pending.Dequeue();
                    }

                    next.Callback(next.State);
                }
            }

            private sealed record PendingPost (SendOrPostCallback Callback, object? State);
        }

        private class TestSource : INotifyPropertyChanged
        {
            private PropertyChangedEventHandler? m_propertyChanged;
            private string m_name = "";
            private int m_count;
            private double m_ratio;
            private TestSource? m_child;

            public event PropertyChangedEventHandler? PropertyChanged
            {
                add => m_propertyChanged += value;
                remove => m_propertyChanged -= value;
            }

            public int SubscriberCount => m_propertyChanged?.GetInvocationList().Length ?? 0;
            public int NameWriteCount { get; private set; }

            public string Name
            {
                get => m_name;
                set
                {
                    ++this.NameWriteCount;
                    m_name = value;
                    this.Raise(nameof(Name));
                }
            }

            public int Count
            {
                get => m_count;
                set
                {
                    m_count = value;
                    this.Raise(nameof(Count));
                }
            }

            public double Ratio
            {
                get => m_ratio;
                set
                {
                    m_ratio = value;
                    this.Raise(nameof(Ratio));
                }
            }

            public TestSource? Child
            {
                get => m_child;
                set
                {
                    m_child = value;
                    this.Raise(nameof(Child));
                }
            }

            public void Raise (string propertyName) => m_propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            public void RaiseEverythingChanged () => this.Raise(string.Empty);
        }

        private sealed class AlwaysEqualSource : TestSource
        {
            public override bool Equals (object? obj) => obj is AlwaysEqualSource;
            public override int GetHashCode () => 0;
        }

        // Stands in for a view property. The binding writes it through Push, which is counted; a test's own setup
        // and the user's edits set Value directly, which is not.
        private sealed class TestTarget<T>
        {
            public T Value { get; set; } = default!;
            public int WriteCount { get; private set; }

            /// <summary>Runs after every Push, the way an Android view raises its edit event for a programmatic set.</summary>
            public Action? OnWritten { get; set; }

            public void Push (T value)
            {
                ++this.WriteCount;
                this.Value = value;
                this.OnWritten?.Invoke();
            }
        }

        private sealed class TestCommand : ICommand
        {
            private EventHandler? m_canExecuteChanged;

            public event EventHandler? CanExecuteChanged
            {
                add => m_canExecuteChanged += value;
                remove => m_canExecuteChanged -= value;
            }

            public bool CanExecuteValue { get; set; } = true;
            public int ExecuteCount { get; private set; }
            public object? LastParameter { get; private set; }
            public int SubscriberCount => m_canExecuteChanged?.GetInvocationList().Length ?? 0;

            public bool CanExecute (object? parameter) => this.CanExecuteValue;

            public void Execute (object? parameter)
            {
                ++this.ExecuteCount;
                this.LastParameter = parameter;
            }

            public void RaiseCanExecuteChanged () => m_canExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        private sealed class TestCollection : INotifyCollectionChanged
        {
            private NotifyCollectionChangedEventHandler? m_collectionChanged;

            public event NotifyCollectionChangedEventHandler? CollectionChanged
            {
                add => m_collectionChanged += value;
                remove => m_collectionChanged -= value;
            }

            public int SubscriberCount => m_collectionChanged?.GetInvocationList().Length ?? 0;

            public void RaiseReset () => this.Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            public void Raise (NotifyCollectionChangedEventArgs change) => m_collectionChanged?.Invoke(this, change);
        }

        private sealed class RecordingLink : IDisposable
        {
            private readonly string m_name;
            private readonly List<string> m_order;

            public RecordingLink (string name, List<string> order)
            {
                m_name = name;
                m_order = order;
            }

            public void Dispose () => m_order.Add(m_name);
        }

        private sealed class ThrowingLink : IDisposable
        {
            public void Dispose () => throw new InvalidOperationException("The view behind this link is already gone");
        }
    }
}
