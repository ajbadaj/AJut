namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // Defects in the shared docking orchestration (DockingManagerBase, DockPanelAddRemoveUISync and
    // DockZoneViewModel), each pinned by what it should do, on the same headless manager the
    // DockingManagerBase tests use
    [TestClass]
    public class DockingDefectTests
    {
        // ===========[ Auto-save ]=============================================
        [TestMethod]
        public void DockDefect_ColdStartWithNoLayout_StillArmsAutoSave ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            manager.AutoSaveMethod = eDockingAutoSaveMethod.AutoSaveOnAllChanges;
            manager.SetupDefaultAndFallbackTo(null);

            manager.AutoSaveNow();

            CollectionAssert.AreEqual(new[] { "persistent.layout" }, manager.Saved, "A first run with no layout yet must still save the layout the user goes on to build");
        }

        // ===========[ Group tearoff ]=========================================
        [TestMethod]
        public void DockDefect_RootZoneTornOffWhole_TearoffZoneKeepsTheManager ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel root = manager.RegisterRoot().ViewModel;
            manager.RegisterDisplayFactory<FakeDockPanel>();
            manager.AddPanel(typeof(FakeDockPanel));

            FakeDockWindow tearoff = manager.DoGroupTearoff(root, new DockWindowBounds(50, 50, 300, 200)).Value;

            // The root window keeps its root zone, so the contents move into a duplicate that becomes the tearoff's root.
            //  A stack that only registers a tearoff root zone made for its own manager (AppKit) refuses one made for no
            //  manager, and that tearoff can then be neither dragged nor closed.
            Assert.AreSame(manager, manager.ZonesFor(tearoff)[0].ViewModel.Manager, "The zone a root zone's contents move into must belong to the same manager");
        }

        // ===========[ Instance counts ]=======================================
        [TestMethod]
        public void DockDefect_TabClose_LowersTheInstanceCount ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            manager.RegisterDisplayFactory<FakeDockPanel>();
            manager.AddPanel(typeof(FakeDockPanel));
            DockingContentAdapterModel adapter = manager.EnumerateAdapters().Single();

            adapter.Location.RequestCloseAndRemoveDockedContent(adapter);

            Assert.AreEqual(0, manager.UISyncVM.FindEntry(typeof(FakeDockPanel)).ActiveInstanceCount, "A panel closed from its tab is gone, so the toolbar and menu must stop showing it");
        }

        [TestMethod]
        public void DockDefect_LayoutLoad_CountsWhatItDocked ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            manager.RegisterDisplayFactory<FakeDockPanel>();
            manager.AddPanel(typeof(FakeDockPanel));
            manager.OnLoad = _ =>
            {
                manager.ClearForLoad();
                root.ViewModel.AddDockedContent(manager.BuildNewDisplayElement<FakeDockPanel>());
                root.ViewModel.AddDockedContent(manager.BuildNewDisplayElement<FakeDockPanel>());
                return true;
            };

            manager.LoadDockLayoutFromFile("saved.layout");

            Assert.AreEqual(2, manager.UISyncVM.FindEntry(typeof(FakeDockPanel)).ActiveInstanceCount, "After a load the count is what the layout docked");
        }

        [TestMethod]
        public void DockDefect_ClosingATearoff_LowersTheInstanceCount ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            manager.RegisterDisplayFactory<FakeDockPanel>();
            manager.AddPanel(typeof(FakeDockPanel));
            DockingContentAdapterModel adapter = manager.EnumerateAdapters().Single();
            FakeDockWindow tearoff = manager.DoTearoff(adapter.Display, new DockWindowBounds(0, 0, 10, 10)).Value;

            manager.UserClosesTearoff(tearoff);

            Assert.AreEqual(0, manager.UISyncVM.FindEntry(typeof(FakeDockPanel)).ActiveInstanceCount);
        }

        // ===========[ Adapter and hidden panel disposal ]=====================
        [TestMethod]
        public void DockDefect_ClosingThroughUISync_DisposesTheAdapter ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            manager.RegisterDisplayFactory<FakeDockPanel>();
            manager.AddPanel(typeof(FakeDockPanel));
            DockingContentAdapterModel adapter = manager.EnumerateAdapters().Single();
            WeakReference weakSubscriber = AttachClosedSubscriberAndForget(adapter);

            manager.RemoveOrHidePanel(adapter);

            ForceFullCollect();
            GC.KeepAlive(adapter);
            Assert.IsFalse(weakSubscriber.IsAlive, "A panel closed for good must release its adapter's subscribers");
        }

        [TestMethod]
        public void DockDefect_Dispose_DisposesHiddenPanels ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            manager.RegisterDisplayFactory<FakeDockPanel>(new DockPanelRegistrationRules { DefaultHideDontClose = true });
            manager.AddPanel(typeof(FakeDockPanel));
            DockingContentAdapterModel adapter = manager.EnumerateAdapters().Single();
            WeakReference weakSubscriber = AttachClosedSubscriberAndForget(adapter);
            manager.RemoveOrHidePanel(adapter);

            manager.Dispose();

            ForceFullCollect();
            GC.KeepAlive(adapter);
            Assert.IsFalse(weakSubscriber.IsAlive, "A hidden panel is still the manager's, so its Dispose must release it");
        }

        // ===========[ Tab selection ]=========================================
        [TestMethod]
        public void DockDefect_RemovingATabBeforeTheSelectedOne_KeepsTheSameTabSelected ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel zone = manager.RegisterRoot().ViewModel;
            var first = manager.BuildNewDisplayElement<FakeDockPanel>();
            var second = manager.BuildNewDisplayElement<FakeDockPanel>();
            var third = manager.BuildNewDisplayElement<FakeDockPanel>();
            zone.AddDockedContent(first);
            zone.AddDockedContent(second);
            zone.AddDockedContent(third);
            zone.SelectedIndex = 2;

            zone.RemoveDockedContent(first.DockingAdapter);

            Assert.AreEqual(1, zone.SelectedIndex, "The third tab moved to index 1 and is still the selected one");
        }

        [TestMethod]
        public void DockDefect_RemovingATabBeforeTheSelectedOne_DoesNotShiftTheSelection ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel zone = manager.RegisterRoot().ViewModel;
            var first = manager.BuildNewDisplayElement<FakeDockPanel>();
            var second = manager.BuildNewDisplayElement<FakeDockPanel>();
            var third = manager.BuildNewDisplayElement<FakeDockPanel>();
            zone.AddDockedContent(first);
            zone.AddDockedContent(second);
            zone.AddDockedContent(third);
            zone.SelectedIndex = 1;

            zone.RemoveDockedContent(first.DockingAdapter);

            Assert.AreSame(second.DockingAdapter, zone.DockedContent[zone.SelectedIndex], "The selected tab must stay selected when an earlier one goes");
        }

        // ===========[ Failed tearoffs ]=======================================
        [TestMethod]
        public void DockDefect_FailedTearoff_LeavesThePanelWhereItWas ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel zone = manager.RegisterRoot().ViewModel;
            var stays = manager.BuildNewDisplayElement<FakeDockPanel>();
            var torn = manager.BuildNewDisplayElement<FakeDockPanel>();
            zone.AddDockedContent(stays);
            zone.AddDockedContent(torn);
            manager.FailNextTearoffWindow = true;

            Assert.IsFalse(manager.DoTearoff(torn, new DockWindowBounds(0, 0, 10, 10)));

            Assert.AreSame(zone, torn.DockingAdapter.Location, "A tearoff whose window could not be made must not lose the panel");
        }

        [TestMethod]
        public void DockDefect_FailedReshowTearoff_DocksThePanelInstead ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            manager.RegisterDisplayFactory<FakeDockPanel>(new DockPanelRegistrationRules { SingleInstanceOnly = true, DefaultHideDontClose = true });
            manager.AddPanel(typeof(FakeDockPanel));
            DockingContentAdapterModel adapter = manager.EnumerateAdapters().Single();
            manager.RemoveOrHidePanel(adapter);
            manager.FailNextTearoffWindow = true;

            manager.AddPanel(typeof(FakeDockPanel));

            Assert.IsNotNull(adapter.Location, "Re-showing a hidden panel must not lose it when its tearoff window could not be made");
            Assert.AreEqual(1, manager.UISyncVM.FindEntry(typeof(FakeDockPanel)).ActiveInstanceCount);
        }

        // ===========[ Tearoff close paths ]===================================
        [TestMethod]
        public void DockDefect_ClearForLoad_DeregistersAndTearsDownTearoffs ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            manager.CreateTearoffForPanel(panel.DockingAdapter, 0, 0, 10, 10);
            DockZoneViewModel tearoffRoot = panel.DockingAdapter.Location;

            manager.ClearForLoad();

            CollectionAssert.AreEqual(new[] { root }, manager.AllRootZones.ToList(), "A closed tearoff's zone must leave the registry, or zone search keeps finding it");
            Assert.IsNull(tearoffRoot.Manager);
        }

        [TestMethod]
        public void DockDefect_RootWindowClosed_DeregistersAndTearsDownTearoffs ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            manager.CreateTearoffForPanel(panel.DockingAdapter, 0, 0, 10, 10);
            DockZoneViewModel tearoffRoot = panel.DockingAdapter.Location;

            manager.RootWindowClosed();

            CollectionAssert.AreEqual(new[] { root }, manager.AllRootZones.ToList());
            Assert.IsNull(tearoffRoot.Manager);
        }

        [TestMethod]
        public void DockDefect_ClosingATearoffOfHideablePanels_LeavesClosingTheWindowToTheOS ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            manager.RegisterDisplayFactory<FakeDockPanel>(new DockPanelRegistrationRules { DefaultHideDontClose = true });
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            manager.CreateTearoffForPanel(panel.DockingAdapter, 0, 0, 10, 10);
            FakeDockWindow tearoff = manager.TearoffHosting(panel.DockingAdapter);

            Assert.IsTrue(manager.UserClosesTearoff(tearoff));

            Assert.IsFalse(tearoff.IsClosed, "Hiding the last panel must not close the window from inside its own close");
            Assert.AreEqual(1, manager.HookLog.Count(e => e == $"stop tracking {tearoff}"), "The window is let go of once");
        }

        // ===========[ Empty orientation ]=====================================
        [TestMethod]
        public void DockDefect_EmptyZone_RefusesAChild ()
        {
            var manager = new TestDockingManager();
            var empty = new DockZoneViewModel(manager);
            var child = new DockZoneViewModel(manager);

            Assert.IsFalse(empty.AddChild(child), "An empty zone has no split direction, so a child would leave it serializing as an empty leaf");
            Assert.AreEqual(0, empty.Children.Count);
        }

        [TestMethod]
        public void DockGuard_BuildFromState_RebuildsASplit ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel zone = manager.RegisterRoot().ViewModel;
            var data = new DockZoneSerializationData(eDockOrientation.Horizontal);
            data.ChildZones.Add(LeafData());
            data.ChildZones.Add(LeafData());

            zone.BuildFromState(data, _ => manager.BuildNewDisplayElement<FakeDockPanel>().DockingAdapter);

            Assert.AreEqual(eDockOrientation.Horizontal, zone.Orientation);
            Assert.AreEqual(2, zone.Children.Count);
            Assert.IsTrue(zone.Children.All(c => c.DockedContent.Count == 1));
        }

        [TestMethod]
        public void DockGuard_TabDropOfASplitOntoAnEmptyZone_CopiesTheSplit ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel target = manager.RegisterRoot().ViewModel;
            DockZoneViewModel source = MakeSplitOfTwoLeaves(manager);

            Assert.IsTrue(target.DropAddSiblingIntoDock(source, eDockInsertionDirection.AddToTabbedDisplay));

            Assert.AreEqual(eDockOrientation.Horizontal, target.Orientation);
            Assert.AreEqual(2, target.Children.Count);
        }

        // ===========[ Drop sizing ]===========================================
        [TestMethod]
        public void DockDefect_TabDropOfAnUnmeasuredSplitOntoAnEmptyZone_SizesWithoutNaN ()
        {
            var manager = new TestDockingManager();
            FakeDockZone targetZone = manager.RegisterRoot();
            targetZone.ViewModel.UI = targetZone;
            targetZone.RenderSize = new DockZoneSize(800, 600);
            DockZoneViewModel source = MakeSplitOfTwoLeaves(manager);

            targetZone.ViewModel.DropAddSiblingIntoDock(source, eDockInsertionDirection.AddToTabbedDisplay);

            Assert.IsFalse(
                targetZone.TargetSizeCalls.SelectMany(s => s).Any(double.IsNaN),
                "Children with no measured size yet must not size to 0/0"
            );
        }

        [TestMethod]
        public void DockDefect_TabDropOfAnEmptyZoneOntoAnEmptyZone_SizesNothing ()
        {
            var manager = new TestDockingManager();
            FakeDockZone targetZone = manager.RegisterRoot();
            targetZone.ViewModel.UI = targetZone;
            targetZone.RenderSize = new DockZoneSize(800, 600);

            targetZone.ViewModel.DropAddSiblingIntoDock(new DockZoneViewModel(manager), eDockInsertionDirection.AddToTabbedDisplay);

            Assert.AreEqual(0, targetZone.TargetSizeCalls.Count, "An empty zone is not a split, so there are no children to size");
        }

        // ===========[ Tearoff size round trips ]==============================
        [TestMethod]
        public void DockDefect_SavedAndLoadedTearoff_KeepsItsSize ()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ajut-dock-tests", Guid.NewGuid().ToString("N") + ".layout");
            try
            {
                TypeManagement.TypeIdRegistrar.RegisterTypeId<FakeDockPanel>("ajut-tests-fake-dock-panel");
                var saver = new TestDockingManager { UseRealLayoutFiles = true, PlacementScale = 2.0 };
                saver.RegisterRoot("main");
                saver.RegisterDisplayFactory<FakeDockPanel>();
                var torn = saver.BuildNewDisplayElement<FakeDockPanel>();
                saver.CreateTearoffForPanel(torn.DockingAdapter, 10, 20, 300, 400);
                DockWindowBounds savedBounds = saver.TearoffHosting(torn.DockingAdapter).Bounds;
                saver.SaveDockLayoutToFile(path);

                var loader = new TestDockingManager { UseRealLayoutFiles = true, PlacementScale = 2.0 };
                loader.RegisterRoot("main");
                loader.RegisterDisplayFactory<FakeDockPanel>();
                loader.LoadDockLayoutFromFile(path);

                FakeDockWindow loaded = loader.AllWindows.Single(w => w != loader.RootWindow);
                Assert.AreEqual(savedBounds, loaded.Bounds, "A tearoff must come back the size it was saved, not grow by the placement scale");
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [TestMethod]
        public void DockDefect_HiddenAndReshownTearoffPanel_KeepsItsSize ()
        {
            var manager = new TestDockingManager { PlacementScale = 2.0 };
            manager.RegisterRoot();
            manager.RegisterDisplayFactory<FakeDockPanel>(new DockPanelRegistrationRules { SingleInstanceOnly = true, DefaultHideDontClose = true });
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            manager.CreateTearoffForPanel(panel.DockingAdapter, 10, 20, 300, 400);
            FakeDockWindow tearoff = manager.TearoffHosting(panel.DockingAdapter);
            DockWindowBounds before = tearoff.Bounds;
            foreach (FakeDockZone zone in manager.ZonesFor(tearoff))
            {
                zone.ViewModel.UI = zone;
            }

            manager.RemoveOrHidePanel(panel.DockingAdapter);
            manager.AddPanel(typeof(FakeDockPanel));

            Assert.AreEqual(before, manager.TearoffHosting(panel.DockingAdapter).Bounds, "A re-shown panel's tearoff must come back the size it was");
        }

        [TestMethod]
        public void DockDefect_TearoffAtTheRootWindowsPosition_IsPlacedThere ()
        {
            // A negative position means "where the root window is", read in placement units, which creation may not take
            var manager = new TestDockingManager { PositionScale = 2.0 };
            manager.RegisterRoot();
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();

            manager.CreateTearoffForPanel(panel.DockingAdapter, -1, -1, 300, 400);

            DockWindowBounds placed = manager.TearoffHosting(panel.DockingAdapter).Bounds;
            Assert.AreEqual(manager.RootWindow.Bounds.X, placed.X, "The tearoff lands on the root window, not at a scaled copy of its position");
            Assert.AreEqual(manager.RootWindow.Bounds.Y, placed.Y);
        }

        // ===========[ Dispose ]===============================================
        [TestMethod]
        public void DockDefect_DisposeTwice_TearsDownOnce ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();

            manager.Dispose();
            manager.Dispose();

            Assert.AreEqual(1, manager.DisposingCount, "A second Dispose must do nothing");
        }

        // ===========[ Helpers ]===============================================
        private static DockZoneSerializationData LeafData ()
        {
            return new DockZoneSerializationData(eDockOrientation.Single)
            {
                DisplayState = new[] { new DockDisplaySerializationData { TypeId = "panel" } },
            };
        }

        private static DockZoneViewModel MakeSplitOfTwoLeaves (TestDockingManager manager)
        {
            var split = new DockZoneViewModel(manager);
            split.Configure(eDockOrientation.Horizontal);
            for (int index = 0; index < 2; ++index)
            {
                var leaf = new DockZoneViewModel(manager);
                leaf.AddDockedContent(manager.BuildNewDisplayElement<FakeDockPanel>());
                split.AddChild(leaf);
            }

            return split;
        }

        // The subscriber's only reference is the adapter's event delegate, so its collection proves the
        // adapter let go
        private static WeakReference AttachClosedSubscriberAndForget (DockingContentAdapterModel adapter)
        {
            var subscriber = new ClosedSubscriber();
            adapter.Closed += subscriber.OnClosed;
            return new WeakReference(subscriber);
        }

        private static void ForceFullCollect ()
        {
            for (int pass = 0; pass < 2; ++pass)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        private sealed class ClosedSubscriber
        {
            public void OnClosed (object sender, ClosedEventArgs e) { }
        }
    }
}
