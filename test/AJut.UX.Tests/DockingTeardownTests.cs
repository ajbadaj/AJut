namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // Headless teardown/leak regressions for the platform-agnostic docking model. Each asserts the
    // post-teardown state deterministically (weak reference + forced GC), so they run with no UI host.
    [TestClass]
    public class DockingTeardownTests
    {
        // AM-1: A docked panel routinely subscribes to its adapter's CanClose (veto) and Closed
        // (cleanup) events in Setup. When the panel is closed and removed, the adapter must release
        // those subscribers - otherwise a lingering adapter pins the panel (and everything it reaches)
        // through the event delegate's target. This is the consumer-facing shape of the leak.
        [TestMethod]
        public void ClosingDockedContent_ReleasesAdapterEventSubscribers ()
        {
            var zone = new DockZoneViewModel(manager: null);
            var adapter = new DockingContentAdapterModel(null);
            WeakReference weakSubscriber = AttachVetoSubscriberAndForget(adapter);

            zone.AddDockedContent(adapter);
            bool closed = zone.RequestCloseAndRemoveDockedContent(adapter);

            Assert.IsTrue(closed, "the panel should have closed cleanly (no veto)");
            ForceFullCollect();
            Assert.IsFalse(
                weakSubscriber.IsAlive,
                "closing a docked panel must release its adapter's CanClose/Closed subscribers so the panel can collect"
            );
        }

        // AM-1 (method contract): Dispose() is the adapter's teardown. It must release every event
        // subscriber, not just the internal CollectionChanged hook, so a disposed adapter pins nothing.
        [TestMethod]
        public void DisposingAdapter_ReleasesEventSubscribers ()
        {
            var adapter = new DockingContentAdapterModel(null);
            WeakReference weakSubscriber = AttachVetoSubscriberAndForget(adapter);

            adapter.Dispose();

            ForceFullCollect();
            Assert.IsFalse(
                weakSubscriber.IsAlive,
                "DockingContentAdapterModel.Dispose() must release event subscribers, not just the CollectionChanged hook"
            );
        }

        // VM-2: Configuring a leaf zone that still holds docked content into a split orientation clears
        // the docked content. It must DETACH those adapters (SetNewLocation(null)) first, not silently
        // drop them - otherwise each orphaned adapter keeps Location pointing at the zone and its
        // DockedContent CollectionChanged subscription attached.
        [TestMethod]
        public void ConfiguringLeafIntoSplit_DetachesDockedAdapters ()
        {
            var zone = new DockZoneViewModel(manager: null);
            var adapter = new DockingContentAdapterModel(null);
            zone.AddDockedContent(adapter);
            Assert.AreSame(zone, adapter.Location, "sanity: the adapter is docked into the zone");

            zone.Configure(eDockOrientation.Horizontal);

            Assert.IsNull(
                adapter.Location,
                "configuring a content-bearing leaf into a split must detach its adapters, not orphan them with a live back-reference"
            );
        }

        // VM-1: When the manager permanently drops a zone it must sever Manager/Parent on EVERY zone in
        // the subtree, not just the root. A zone whose native peer (or a stray delegate) outlives the
        // logical tree would otherwise re-root the manager through Manager - and the whole graph through
        // the parent chain. Teardown is the manager-driven terminal sweep that breaks both links.
        [TestMethod]
        public void TearingDownZone_SeversManagerAndParentForEveryZoneInTheTree ()
        {
            var manager = new InertDockingManager();
            var root = new DockZoneViewModel(manager);
            root.Configure(eDockOrientation.Horizontal);

            var leafA = new DockZoneViewModel(manager);
            leafA.AddDockedContent(new DockingContentAdapterModel(null));
            var leafB = new DockZoneViewModel(manager);
            leafB.AddDockedContent(new DockingContentAdapterModel(null));
            root.AddChild(leafA);
            root.AddChild(leafB);

            Assert.AreSame(manager, leafA.Manager, "sanity: a child picks up the manager when added");
            Assert.AreSame(root, leafA.Parent, "sanity: a child is parented under root when added");

            root.Teardown();

            Assert.IsNull(root.Manager, "the root must drop its manager on teardown");
            Assert.IsNull(leafA.Manager, "every descendant must drop its manager on teardown");
            Assert.IsNull(leafB.Manager, "every descendant must drop its manager on teardown");
            Assert.IsNull(leafA.Parent, "every descendant must drop its parent on teardown");
            Assert.IsNull(leafB.Parent, "every descendant must drop its parent on teardown");
        }

        // VM-1 (the leak shape): the consumer-facing failure is a single zone the manager already dropped
        // still pinning the manager. After teardown a surviving leaf must root neither the manager nor its
        // former ancestors, so both collect even while the leaf is held alive.
        [TestMethod]
        public void TearingDownZone_LeavesNoUpwardReferenceFromASurvivingZone ()
        {
            DockZoneViewModel survivor = BuildAndTeardownTreeKeepingOneLeaf(out WeakReference weakManager, out WeakReference weakRoot);

            ForceFullCollect();

            GC.KeepAlive(survivor);
            Assert.IsFalse(weakManager.IsAlive, "a torn-down zone must not keep the manager rooted");
            Assert.IsFalse(weakRoot.IsAlive, "a torn-down zone must not keep its old root / ancestors rooted");
        }

        // Builds a small tree, tears it down, and hands back one leaf as the "stuck" survivor. The manager
        // and root locals fall out of scope on return, so only the leaf could still be rooting them.
        private static DockZoneViewModel BuildAndTeardownTreeKeepingOneLeaf (out WeakReference weakManager, out WeakReference weakRoot)
        {
            var manager = new InertDockingManager();
            var root = new DockZoneViewModel(manager);
            root.Configure(eDockOrientation.Horizontal);

            var leafA = new DockZoneViewModel(manager);
            leafA.AddDockedContent(new DockingContentAdapterModel(null));
            var leafB = new DockZoneViewModel(manager);
            leafB.AddDockedContent(new DockingContentAdapterModel(null));
            root.AddChild(leafA);
            root.AddChild(leafB);

            weakManager = new WeakReference(manager);
            weakRoot = new WeakReference(root);

            root.Teardown();
            return leafA;
        }

        // The subscriber's only reference is the adapter's event delegate, so its collection proves the
        // adapter let go.
        private static WeakReference AttachVetoSubscriberAndForget (DockingContentAdapterModel adapter)
        {
            var subscriber = new PanelSubscriberStandin();
            adapter.CanClose += subscriber.OnCanClose;
            adapter.Closed += subscriber.OnClosed;
            return new WeakReference(subscriber);
        }

        private static void ForceFullCollect ()
        {
            for (int i = 0; i < 2; ++i)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        private sealed class PanelSubscriberStandin
        {
            public void OnCanClose (object sender, IsReadyToCloseEventArgs e) { /* allow the close */ }
            public void OnClosed (object sender, ClosedEventArgs e) { }
        }

        // Inert IDockingManager: the model-layer teardown never calls back into the manager, so every
        // member can throw. It exists only so a zone can hold a non-null Manager we can prove gets nulled
        // (and so we can weak-reference it to prove a torn-down zone stops rooting it).
        private sealed class InertDockingManager : IDockingManager
        {
            public double MinPanelDimension { get; set; }
            public DockPanelAddRemoveUISync UISyncVM => throw new NotImplementedException();
            public IDockableDisplayElement BuildNewDisplayElement (Type elementType) => throw new NotImplementedException();
            public IEnumerable<DockZoneViewModel> GetAllRoots () => throw new NotImplementedException();
            public bool LoadDockLayoutFromFile (string filePath) => throw new NotImplementedException();
            public bool SaveDockLayoutToFile (string filePath = null) => throw new NotImplementedException();
            public bool SaveDockLayoutToPersistentStorage () => throw new NotImplementedException();
            public bool ReloadDockLayoutFromPersistentStorage () => throw new NotImplementedException();
            public void AddPanel (Type panelType) => throw new NotImplementedException();
            public void TogglePanel (Type panelType) => throw new NotImplementedException();
            public void RemoveOrHidePanel (DockingContentAdapterModel adapter) => throw new NotImplementedException();
            public DockPanelRegistrationRules? GetPanelRules (Type panelType) => throw new NotImplementedException();
            public DockZoneViewModel FindTargetZoneForGroup (string groupId) => throw new NotImplementedException();
            public HiddenPanelPlatformState CaptureHideState (DockingContentAdapterModel adapter) => throw new NotImplementedException();
            public bool TryRestoreFromHideState (object hideState, DockingContentAdapterModel adapter) => throw new NotImplementedException();
            public void AfterPanelHidden (object hideState) => throw new NotImplementedException();
            public bool CreateTearoffForPanel (DockingContentAdapterModel adapter, double x, double y, double width, double height) => throw new NotImplementedException();
            public bool IsTearoffRootThatWouldOrphan (DockZoneViewModel zone) => throw new NotImplementedException();
            public void CloseTearoffForRootZone (DockZoneViewModel rootZone) => throw new NotImplementedException();
        }
    }
}
