namespace AJut.UX.Tests
{
    using System;
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
    }
}
