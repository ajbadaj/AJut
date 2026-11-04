namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The rules every stack's WindowManager shares through <see cref="WindowManagerBase{TWindow}"/>: activation order,
    /// tracking, and showing and hiding the root's children. The windows are stand-ins that record what was done to them.
    /// </summary>
    [TestClass]
    public class WindowManagerBaseTests
    {
        // ===========[ Tracking and order ]====================================
        [TestMethod]
        public void Setup_RootIsTrackedAndSubscribedTo ()
        {
            var root = new TestWindow("root");
            var manager = new TestWindowManager();

            manager.Setup(root);

            Assert.AreSame(root, manager.Root);
            CollectionAssert.AreEqual(new[] { root }, manager.ToList());
            Assert.IsTrue(root.IsSubscribed);
            Assert.IsTrue(root.IsRootSubscribed);
        }

        [TestMethod]
        public void Setup_NullRoot_Throws ()
        {
            var manager = new TestWindowManager();
            Assert.ThrowsExactly<ArgumentNullException>(() => manager.Setup(null));
        }

        [TestMethod]
        public void Setup_Again_LetsGoOfTheFirstRootAndItsChildren ()
        {
            var firstRoot = new TestWindow("first root");
            var child = new TestWindow("child");
            var secondRoot = new TestWindow("second root");
            var manager = new TestWindowManager();
            manager.Setup(firstRoot);
            manager.Track(child);

            manager.Setup(secondRoot);

            CollectionAssert.AreEqual(new[] { secondRoot }, manager.ToList());
            Assert.IsFalse(firstRoot.IsSubscribed);
            Assert.IsFalse(firstRoot.IsRootSubscribed);
            Assert.IsFalse(child.IsSubscribed);
        }

        [TestMethod]
        public void Track_NewWindow_GoesToTheFrontAndIsPresented ()
        {
            (TestWindowManager manager, TestWindow root, TestWindow child) = MakeManagerWithOneChild();

            CollectionAssert.AreEqual(new[] { child, root }, manager.ToList());
            Assert.IsTrue(child.IsSubscribed);
            Assert.AreEqual(1, child.PresentCount);
        }

        [TestMethod]
        public void Track_NullOrAlreadyTracked_ReturnsFalse ()
        {
            (TestWindowManager manager, TestWindow _, TestWindow child) = MakeManagerWithOneChild();

            Assert.IsFalse(manager.Track(null));
            Assert.IsFalse(manager.Track(child));
            Assert.AreEqual(2, manager.Count);
        }

        [TestMethod]
        public void OnWindowActivated_MovesTheWindowToTheFrontAndRaisesIt ()
        {
            (TestWindowManager manager, TestWindow root, TestWindow child) = MakeManagerWithOneChild();
            int raisesBefore = root.RaiseCount;

            manager.ReportActivated(root);

            CollectionAssert.AreEqual(new[] { root, child }, manager.ToList());
            Assert.AreEqual(raisesBefore + 1, root.RaiseCount);
        }

        [TestMethod]
        public void ForEach_GoesFrontToBack_AndForEachBackToFront_TheOtherWay ()
        {
            (TestWindowManager manager, TestWindow root, TestWindow first) = MakeManagerWithOneChild();
            var second = new TestWindow("second");
            manager.Track(second);

            var frontToBack = new List<TestWindow>();
            manager.ForEach(true, frontToBack.Add);
            var backToFront = new List<TestWindow>();
            manager.ForEachBackToFront(true, backToFront.Add);
            var childrenOnly = new List<TestWindow>();
            manager.ForEach(false, childrenOnly.Add);

            CollectionAssert.AreEqual(new[] { second, first, root }, frontToBack);
            CollectionAssert.AreEqual(new[] { root, first, second }, backToFront);
            CollectionAssert.AreEqual(new[] { second, first }, childrenOnly);
        }

        [TestMethod]
        public void ForEach_AnActionThatActivatesAWindow_LeavesTheOrderAlone ()
        {
            (TestWindowManager manager, TestWindow root, TestWindow child) = MakeManagerWithOneChild();

            manager.ForEachBackToFront(true, manager.ReportActivated);

            CollectionAssert.AreEqual(new[] { child, root }, manager.ToList());
        }

        [TestMethod]
        public void StopTracking_RemovesAndUnsubscribes ()
        {
            (TestWindowManager manager, TestWindow root, TestWindow child) = MakeManagerWithOneChild();

            Assert.IsTrue(manager.StopTracking(child));

            CollectionAssert.AreEqual(new[] { root }, manager.ToList());
            Assert.IsFalse(child.IsSubscribed);
            Assert.IsFalse(manager.StopTracking(child));
            Assert.IsFalse(manager.StopTracking(null));
        }

        [TestMethod]
        public void OnWindowClosed_Child_StopsTrackingIt ()
        {
            (TestWindowManager manager, TestWindow root, TestWindow child) = MakeManagerWithOneChild();

            manager.ReportClosed(child);

            CollectionAssert.AreEqual(new[] { root }, manager.ToList());
            Assert.IsFalse(child.IsSubscribed);
        }

        [TestMethod]
        public void OnWindowClosed_Root_ClosesAndStopsTrackingEveryChild ()
        {
            (TestWindowManager manager, TestWindow root, TestWindow child) = MakeManagerWithOneChild();

            manager.ReportClosed(root);

            Assert.AreEqual(0, manager.Count);
            Assert.IsTrue(child.IsClosed);
            Assert.IsFalse(child.IsSubscribed);
            Assert.IsFalse(root.IsSubscribed);
        }

        [TestMethod]
        public void Dispose_UnsubscribesEveryWindowAndLetsGoOfTheRoot ()
        {
            (TestWindowManager manager, TestWindow root, TestWindow child) = MakeManagerWithOneChild();

            manager.Dispose();

            Assert.IsNull(manager.Root);
            Assert.AreEqual(0, manager.Count);
            Assert.IsFalse(root.IsSubscribed);
            Assert.IsFalse(root.IsRootSubscribed);
            Assert.IsFalse(child.IsSubscribed);
        }

        [TestMethod]
        public void ShowHideChildrenWhen_RaisesPropertyChangedOnlyOnAChange ()
        {
            var manager = new TestWindowManager();
            var changed = new List<string>();
            ((INotifyPropertyChanged)manager).PropertyChanged += _OnPropertyChanged;

            manager.ShowHideChildrenWhen = eChildWindowDisplayCoordinationStyle.RootActivationDeactivation;
            manager.ShowHideChildrenWhen = eChildWindowDisplayCoordinationStyle.RootActivationDeactivation;

            ((INotifyPropertyChanged)manager).PropertyChanged -= _OnPropertyChanged;
            CollectionAssert.AreEqual(new[] { nameof(WindowManagerBase<TestWindow>.ShowHideChildrenWhen) }, changed);

            void _OnPropertyChanged (object _sender, PropertyChangedEventArgs _e)
            {
                if (_e.PropertyName == nameof(WindowManagerBase<TestWindow>.ShowHideChildrenWhen))
                {
                    changed.Add(_e.PropertyName);
                }
            }
        }

        // ===========[ Root activation and deactivation ]======================
        [TestMethod]
        public void RootActivationDeactivation_EveryWindowDeactivated_HidesTheChildren ()
        {
            (TestWindowManager manager, TestWindow root, TestWindow child) = MakeManagerWithOneChild(eChildWindowDisplayCoordinationStyle.RootActivationDeactivation);
            manager.ReportActivated(child);

            // The app lost focus: the active window deactivates, and nothing else activates
            manager.ReportDeactivated(child);
            manager.RunSettledActivationWork();

            Assert.IsFalse(child.IsVisible, "With no window of the app active, the root's children hide");
            Assert.IsTrue(root.IsVisible, "The root itself stays");
        }

        [TestMethod]
        public void RootActivationDeactivation_FocusMovingToAnotherTrackedWindow_KeepsTheChildren ()
        {
            (TestWindowManager manager, TestWindow root, TestWindow child) = MakeManagerWithOneChild(eChildWindowDisplayCoordinationStyle.RootActivationDeactivation);
            manager.ReportActivated(root);

            // Clicking the child: the root deactivates first, then the child activates
            manager.ReportDeactivated(root);
            manager.ReportActivated(child);
            manager.RunSettledActivationWork();

            Assert.IsTrue(child.IsVisible);
        }

        [TestMethod]
        public void RootActivationDeactivation_RootActivatedWithChildrenHidden_ShowsThemAndRaisesTheRoot ()
        {
            (TestWindowManager manager, TestWindow root, TestWindow child) = MakeManagerWithOneChild(eChildWindowDisplayCoordinationStyle.RootActivationDeactivation);
            child.IsVisible = false;
            int rootRaisesBefore = root.RaiseCount;

            manager.ReportActivated(root);

            Assert.IsTrue(child.IsVisible);
            Assert.IsTrue(root.RaiseCount >= rootRaisesBefore + 2, "Raised once for the activation, and again over the children it showed");
        }

        [TestMethod]
        public void Manually_EveryWindowDeactivated_LeavesTheChildren ()
        {
            (TestWindowManager manager, TestWindow _, TestWindow child) = MakeManagerWithOneChild(eChildWindowDisplayCoordinationStyle.Manually);
            manager.ReportActivated(child);

            manager.ReportDeactivated(child);
            manager.RunSettledActivationWork();

            Assert.IsTrue(child.IsVisible);
        }

        // ===========[ Root minimization and restoration ]=====================
        [TestMethod]
        public void RootMinimizationRestoration_RootMinimizedThenRestored_HidesThenShowsTheChildren ()
        {
            (TestWindowManager manager, TestWindow root, TestWindow child) = MakeManagerWithOneChild(eChildWindowDisplayCoordinationStyle.RootMinimizationRestoration);

            manager.ReportRootMinimized();
            Assert.IsFalse(child.IsVisible);
            Assert.IsTrue(root.IsVisible, "Minimizing the root is the platform's business; only the children are hidden here");

            manager.ReportRootRestored();
            Assert.IsTrue(child.IsVisible);
        }

        [TestMethod]
        public void RootActivationDeactivation_RootMinimized_LeavesTheChildren ()
        {
            (TestWindowManager manager, TestWindow _, TestWindow child) = MakeManagerWithOneChild(eChildWindowDisplayCoordinationStyle.RootActivationDeactivation);

            manager.ReportRootMinimized();

            Assert.IsTrue(child.IsVisible);
        }

        // ===========[ Helper Methods ]========================================
        private static (TestWindowManager Manager, TestWindow Root, TestWindow Child) MakeManagerWithOneChild (eChildWindowDisplayCoordinationStyle style = eChildWindowDisplayCoordinationStyle.Manually)
        {
            var root = new TestWindow("root");
            var child = new TestWindow("child");
            var manager = new TestWindowManager { ShowHideChildrenWhen = style };
            manager.Setup(root);
            manager.Track(child);
            return (manager, root, child);
        }

        // ===========[ Subclasses/structs ]====================================
        // A window that records what the manager did to it
        private sealed class TestWindow
        {
            public TestWindow (string name)
            {
                this.Name = name;
            }

            public string Name { get; }
            public bool IsVisible { get; set; } = true;
            public bool IsClosed { get; set; }
            public bool IsSubscribed { get; set; }
            public bool IsRootSubscribed { get; set; }
            public int PresentCount { get; set; }
            public int RaiseCount { get; set; }

            public override string ToString () => this.Name;
        }

        // Reports what the tests say happened, and queues the settled work until the test runs it, the way a UI thread
        //  would run it after the events already queued
        private sealed class TestWindowManager : WindowManagerBase<TestWindow>
        {
            private readonly Queue<Action> m_settledWork = new Queue<Action>();

            public void ReportActivated (TestWindow window) => this.OnWindowActivated(window);
            public void ReportDeactivated (TestWindow window) => this.OnWindowDeactivated(window);
            public void ReportClosed (TestWindow window)
            {
                window.IsClosed = true;
                this.OnWindowClosed(window);
            }

            public void ReportRootMinimized () => this.OnRootMinimized();
            public void ReportRootRestored () => this.OnRootRestored();

            public void RunSettledActivationWork ()
            {
                while (m_settledWork.Count > 0)
                {
                    m_settledWork.Dequeue()();
                }
            }

            protected override void SubscribeTo (TestWindow window) => window.IsSubscribed = true;
            protected override void UnsubscribeFrom (TestWindow window) => window.IsSubscribed = false;
            protected override void OnRootSetup (TestWindow root) => root.IsRootSubscribed = true;
            protected override void OnRootTeardown (TestWindow root) => root.IsRootSubscribed = false;

            protected override void PresentWindow (TestWindow window)
            {
                window.IsVisible = true;
                ++window.PresentCount;
            }

            protected override void RaiseToFront (TestWindow window) => ++window.RaiseCount;
            protected override void ShowWindow (TestWindow window) => window.IsVisible = true;
            protected override void HideWindow (TestWindow window) => window.IsVisible = false;
            protected override void CloseWindow (TestWindow window)
            {
                window.IsClosed = true;
                window.IsVisible = false;
                this.OnWindowClosed(window);
            }

            protected override bool IsWindowVisible (TestWindow window) => window.IsVisible;
            protected override void RunAfterActivationSettles (Action action) => m_settledWork.Enqueue(action);
        }
    }
}
