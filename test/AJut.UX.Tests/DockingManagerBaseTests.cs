namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using AJut.Storage;
    using AJut.TypeManagement;
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // ===========[ Tests ]=================================================

    [TestClass]
    public class DockingManagerBaseTests
    {
        // ===========[ Display factory ]=======================================
        [TestMethod]
        public void DMB_RegisteredFactory_BuildsWithItAndAppliesRuleDefaultsBeforeSetup ()
        {
            var manager = new TestDockingManager();
            int factoryCalls = 0;
            manager.RegisterDisplayFactory(
                new DockPanelRegistrationRules { DefaultIsClosable = false, DefaultHideDontClose = true, DefaultCanTearoff = false },
                () => { ++factoryCalls; return new FakeDockPanel(); }
            );

            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();

            Assert.AreEqual(1, factoryCalls);
            Assert.AreEqual(false, panel.IsClosableSeenInSetup, "Rule defaults are applied before Setup, so Setup can override them");
            Assert.IsTrue(panel.DockingAdapter.HideDontClose);
            Assert.IsFalse(panel.DockingAdapter.CanTearoff);
            Assert.AreSame(panel, panel.DockingAdapter.Display);
            Assert.AreSame(manager, panel.DockingAdapter.DockingOwner);
            Assert.AreEqual("Fake Dock Panel", panel.DockingAdapter.TitleContent);
            Assert.IsNotNull(manager.UISyncVM.FindEntry(typeof(FakeDockPanel)));
        }

        [TestMethod]
        public void DMB_UnregisteredType_BuildsByDefaultConstructor ()
        {
            var manager = new TestDockingManager();

            IDockableDisplayElement panel = manager.BuildNewDisplayElement(typeof(OtherFakeDockPanel));

            Assert.IsInstanceOfType(panel, typeof(OtherFakeDockPanel));
            Assert.IsNotNull(panel.DockingAdapter);
            Assert.IsNull(manager.GetPanelRules(typeof(OtherFakeDockPanel)));
        }

        [TestMethod]
        public void DMB_GetPanelRules_ReturnsTheRegisteredRules ()
        {
            var manager = new TestDockingManager();
            manager.RegisterDisplayFactory<FakeDockPanel>(singleInstanceOnly: true);

            Assert.IsTrue(manager.GetPanelRules(typeof(FakeDockPanel)).Value.SingleInstanceOnly);
        }

        // ===========[ Registry and zone search ]==============================
        [TestMethod]
        public void DMB_GetAllRoots_IsEveryRegisteredRootsViewModel ()
        {
            var manager = new TestDockingManager();
            FakeDockZone a = manager.RegisterRoot("a");
            FakeDockZone b = manager.RegisterRoot("b");

            CollectionAssert.AreEqual(new[] { a.ViewModel, b.ViewModel }, manager.GetAllRoots().ToList());
        }

        [TestMethod]
        public void DMB_FindForGroup_PicksThatGroupsLeaf_ElseTheFirstLeaf ()
        {
            var manager = new TestDockingManager();
            FakeDockZone a = manager.RegisterRoot("a");
            FakeDockZone b = manager.RegisterRoot("b");

            Assert.AreSame(b.ViewModel, manager.FindFirstAvailableDockZoneForGroup("b"));
            Assert.AreSame(a.ViewModel, manager.FindFirstAvailableDockZoneForGroup("nope"));
            Assert.AreSame(a.ViewModel, manager.FindTargetZoneForGroup(null));
        }

        [TestMethod]
        public void DMB_FindFirstAvailable_SkipsSplitZones ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            root.ViewModel.Configure(eDockOrientation.Horizontal);
            var leaf = new DockZoneViewModel(manager);
            root.ViewModel.AddChild(leaf);
            root.NestedZones.Add(new FakeDockZone(leaf));

            Assert.AreSame(leaf, manager.FindFirstAvailableDockZone());
        }

        [TestMethod]
        public void DMB_GetWindowForZone_FindsTheHostingWindow_ElseTheRoot ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            var nested = new FakeDockZone(new DockZoneViewModel(manager));
            root.NestedZones.Add(nested);
            var tearoffWindow = new FakeDockWindow("other", default);
            FakeDockZone tearoffZone = manager.RegisterRoot(window: tearoffWindow);

            Assert.AreSame(manager.RootWindow, manager.WindowFor(nested));
            Assert.AreSame(tearoffWindow, manager.WindowFor(tearoffZone));
            Assert.AreSame(manager.RootWindow, manager.WindowFor(new FakeDockZone(null)));
            Assert.AreSame(tearoffZone, manager.RootZoneFor(tearoffWindow));
        }

        [TestMethod]
        public void DMB_DeRegister_DropsTheZoneEverywhere_AndLeavesItsViewModel ()
        {
            var manager = new TestDockingManager();
            FakeDockZone zone = manager.RegisterRoot();

            manager.DeRegisterRootDockZones(zone);

            Assert.AreEqual(0, manager.AllRootZones.Count);
            Assert.AreEqual(0, manager.ZonesFor(manager.RootWindow).Count);
            Assert.IsTrue(zone.WasDeregistered);
            Assert.AreSame(manager, zone.ViewModel.Manager, "Deregistering is not a teardown");
        }

        [TestMethod]
        public void DMB_CleanZoneLayoutHierarchies_FlattensASameOrientationChild ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel root = manager.RegisterRoot().ViewModel;
            root.Configure(eDockOrientation.Horizontal);
            var inner = new DockZoneViewModel(manager);
            inner.Configure(eDockOrientation.Horizontal);
            var leafA = new DockZoneViewModel(manager);
            var leafB = new DockZoneViewModel(manager);
            var leafC = new DockZoneViewModel(manager);
            root.AddChild(leafA);
            root.AddChild(inner);
            inner.AddChild(leafB);
            inner.AddChild(leafC);

            manager.CleanZoneLayoutHierarchies();

            CollectionAssert.AreEqual(new[] { leafA, leafB, leafC }, root.Children.ToList());
        }

        // ===========[ Tearoffs ]==============================================
        [TestMethod]
        public void DMB_CreateTearoffForPanel_NegativePositionTakesTheRootWindows ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();

            Assert.IsTrue(manager.CreateTearoffForPanel(panel.DockingAdapter, -1, 50, 300, 400));

            FakeDockWindow tearoff = manager.TearoffHosting(panel.DockingAdapter);
            Assert.IsNotNull(tearoff);
            Assert.AreEqual(new DockWindowBounds(100, 50, 300, 400), tearoff.Bounds);
            Assert.IsTrue(manager.IsTearoffRootThatWouldOrphan(panel.DockingAdapter.Location));
        }

        [TestMethod]
        public void DMB_IsTearoffRootThatWouldOrphan_FalseForTheRootWindowsZones ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            root.ViewModel.AddDockedContent(manager.BuildNewDisplayElement<FakeDockPanel>());

            Assert.IsFalse(manager.IsTearoffRootThatWouldOrphan(root.ViewModel));
        }

        [TestMethod]
        public void DMB_DoTearoff_MovesThePanelIntoANewWindow ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            var stays = manager.BuildNewDisplayElement<FakeDockPanel>();
            var moves = manager.BuildNewDisplayElement<FakeDockPanel>();
            root.ViewModel.AddDockedContent(stays);
            root.ViewModel.AddDockedContent(moves);

            Result<FakeDockWindow> result = manager.DoTearoff(moves, new DockWindowBounds(5, 6, 7, 8));

            Assert.IsTrue(result);
            CollectionAssert.AreEqual(new[] { stays.DockingAdapter }, root.ViewModel.DockedContent.ToList());
            Assert.AreSame(result.Value, manager.TearoffHosting(moves.DockingAdapter));
            Assert.AreEqual(new DockWindowBounds(5, 6, 7, 8), result.Value.Bounds);
        }

        [TestMethod]
        public void DMB_DoGroupTearoff_OfATearoffRoot_GivesBackItsWindow ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            manager.CreateTearoffForPanel(panel.DockingAdapter, 0, 0, 10, 10);
            FakeDockWindow tearoff = manager.TearoffHosting(panel.DockingAdapter);

            Result<FakeDockWindow> result = manager.DoGroupTearoff(panel.DockingAdapter.Location, default);

            Assert.AreSame(tearoff, result.Value);
            Assert.AreEqual(2, manager.AllWindows.Count, "No new window for a zone that already has one");
        }

        [TestMethod]
        public void DMB_DoGroupTearoff_OfTheRootWindowsRoot_MovesItsContentAndKeepsTheSlot ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            root.ViewModel.AddDockedContent(panel);

            Result<FakeDockWindow> result = manager.DoGroupTearoff(root.ViewModel, new DockWindowBounds(1, 2, 3, 4));

            Assert.IsTrue(result);
            Assert.AreEqual(0, root.ViewModel.DockedContent.Count);
            Assert.IsTrue(manager.AllRootZones.Contains(root), "The root window's zone keeps its place");
            Assert.AreSame(result.Value, manager.TearoffHosting(panel.DockingAdapter));
        }

        [TestMethod]
        public void DMB_CloseAll_AVetoClosesNothing ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            var panel = manager.BuildNewDisplayElement<VetoingFakeDockPanel>();
            panel.HookVeto();
            manager.CreateTearoffForPanel(panel.DockingAdapter, 0, 0, 10, 10);
            FakeDockWindow tearoff = manager.TearoffHosting(panel.DockingAdapter);

            Assert.IsFalse(manager.CloseAll());

            Assert.IsFalse(tearoff.IsClosed);
            Assert.AreEqual(2, manager.AllWindows.Count);
        }

        [TestMethod]
        public void DMB_CloseAll_ClosesEveryTearoff_TearsItDown_AndKeepsTheRoot ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            manager.CreateTearoffForPanel(panel.DockingAdapter, 0, 0, 10, 10);
            DockZoneViewModel tearoffRoot = panel.DockingAdapter.Location;
            FakeDockWindow tearoff = manager.TearoffHosting(panel.DockingAdapter);

            Assert.IsTrue(manager.CloseAll());

            Assert.IsTrue(tearoff.IsClosed);
            Assert.IsNull(tearoffRoot.Manager, "A closed tearoff's zones are torn down");
            CollectionAssert.AreEqual(new[] { manager.RootWindow }, manager.AllWindows.ToList());
            CollectionAssert.AreEqual(new[] { root }, manager.AllRootZones.ToList());
            CollectionAssert.AreEqual(
                new[] { $"stop tracking {tearoff}", $"detach {tearoff}", $"close {tearoff}" },
                manager.HookLog.Where(e => e.EndsWith(tearoff.Name) && !e.StartsWith("create")).ToList()
            );
        }

        [TestMethod]
        public void DMB_UserClosesTearoff_HidesHideablePanels_ClosesTheRest_AndActivatesTheRoot ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            manager.RegisterDisplayFactory<FakeDockPanel>(new DockPanelRegistrationRules { DefaultHideDontClose = true });
            var hideable = manager.BuildNewDisplayElement<FakeDockPanel>();
            var closeable = manager.BuildNewDisplayElement<OtherFakeDockPanel>();
            manager.CreateTearoffForPanel(hideable.DockingAdapter, 0, 0, 10, 10);
            DockZoneViewModel tearoffRoot = hideable.DockingAdapter.Location;
            tearoffRoot.AddDockedContent(closeable);
            FakeDockWindow tearoff = manager.TearoffHosting(hideable.DockingAdapter);

            Assert.IsTrue(manager.UserClosesTearoff(tearoff));

            Assert.IsNull(hideable.DockingAdapter.Location, "The hideable panel is hidden, not left docked");
            Assert.IsNull(closeable.DockingAdapter.Location);
            Assert.IsNull(tearoffRoot.Manager);
            CollectionAssert.AreEqual(new[] { manager.RootWindow }, manager.AllWindows.ToList());
            Assert.AreEqual(1, manager.RootWindow.ActivationCount);
            Assert.IsFalse(tearoff.IsClosed, "The window close path leaves closing the window to the OS");
        }

        [TestMethod]
        public void DMB_UserClosesTearoff_DeregistersItsZones_SoASaveLeavesItOut ()
        {
            string path = TempLayoutPath();
            try
            {
                var manager = MakeLayoutManager();
                FakeDockZone root = manager.RegisterRoot("main");
                var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
                manager.CreateTearoffForPanel(panel.DockingAdapter, 0, 0, 10, 10);
                FakeDockWindow tearoff = manager.TearoffHosting(panel.DockingAdapter);

                Assert.IsTrue(manager.UserClosesTearoff(tearoff));

                CollectionAssert.AreEqual(new[] { root }, manager.AllRootZones.ToList());
                Assert.IsTrue(manager.SaveDockLayoutToFile(path));

                var loader = MakeLayoutManager();
                loader.RegisterRoot("main");
                Assert.IsTrue(loader.LoadDockLayoutFromFile(path));
                CollectionAssert.AreEqual(new[] { loader.RootWindow }, loader.AllWindows.ToList());
            }
            finally
            {
                File.Delete(path);
            }
        }

        // The manager keeps windows it is closing silently in a list while it closes them, and must not keep them there
        // once they are closed
        [TestMethod]
        public void DMB_SilentTearoffCloses_LetTheClosedWindowGo ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();

            WeakReference closedByForcedCloseAll = TearOffAndClose(manager, (m, _) => m.CloseAll(force: true));
            WeakReference closedByCloseAll = TearOffAndClose(manager, (m, _) => m.CloseAll());
            WeakReference closedByEmptying = TearOffAndClose(manager, (m, adapter) => adapter.Location.RemoveDockedContent(adapter));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.IsFalse(closedByForcedCloseAll.IsAlive, "CloseAll(force) holds on to the window it closed");
            Assert.IsFalse(closedByCloseAll.IsAlive, "CloseAll holds on to the window it closed");
            Assert.IsFalse(closedByEmptying.IsAlive, "Emptying a tearoff holds on to the window it closed");
            GC.KeepAlive(manager);
        }

        [TestMethod]
        public void DMB_UserClosesTearoff_AVetoKeepsItOpen ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            var panel = manager.BuildNewDisplayElement<VetoingFakeDockPanel>();
            panel.HookVeto();
            manager.CreateTearoffForPanel(panel.DockingAdapter, 0, 0, 10, 10);
            FakeDockWindow tearoff = manager.TearoffHosting(panel.DockingAdapter);

            Assert.IsFalse(manager.UserClosesTearoff(tearoff));

            Assert.AreEqual(2, manager.AllWindows.Count);
            Assert.AreSame(manager, panel.DockingAdapter.Location.Manager);
        }

        [TestMethod]
        public void DMB_TearoffCloseButton_ClosesTheWindow ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            manager.CreateTearoffForPanel(panel.DockingAdapter, 0, 0, 10, 10);
            FakeDockWindow tearoff = manager.TearoffHosting(panel.DockingAdapter);

            manager.TearoffCloseButton(tearoff);

            Assert.IsTrue(tearoff.IsClosed);
            Assert.AreEqual(1, manager.AllWindows.Count);
        }

        [TestMethod]
        public void DMB_RootWindowClosed_ClosesEveryTearoff ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            var panel = manager.BuildNewDisplayElement<VetoingFakeDockPanel>();
            panel.HookVeto();
            manager.CreateTearoffForPanel(panel.DockingAdapter, 0, 0, 10, 10);
            FakeDockWindow tearoff = manager.TearoffHosting(panel.DockingAdapter);

            manager.RootWindowClosed();

            Assert.IsTrue(tearoff.IsClosed, "The root window closing skips the panels' veto");
            CollectionAssert.AreEqual(new[] { manager.RootWindow }, manager.AllWindows.ToList());
        }

        [TestMethod]
        public void DMB_RemovingATearoffsLastPanel_ClosesTheTearoff ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            manager.CreateTearoffForPanel(panel.DockingAdapter, 0, 0, 10, 10);
            FakeDockWindow tearoff = manager.TearoffHosting(panel.DockingAdapter);

            panel.DockingAdapter.Location.RemoveDockedContent(panel.DockingAdapter);

            Assert.IsTrue(tearoff.IsClosed);
            CollectionAssert.AreEqual(new[] { root }, manager.AllRootZones.ToList());
        }

        [TestMethod]
        public void DMB_TabDrop_MovesTheContent_ClosesTheSource_AndActivatesTheTarget ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            var stays = manager.BuildNewDisplayElement<FakeDockPanel>();
            root.ViewModel.AddDockedContent(stays);
            var dragged = manager.BuildNewDisplayElement<FakeDockPanel>();
            manager.CreateTearoffForPanel(dragged.DockingAdapter, 0, 0, 10, 10);
            FakeDockWindow tearoff = manager.TearoffHosting(dragged.DockingAdapter);

            manager.Drop(root.ViewModel, dragged.DockingAdapter.Location, eDockInsertionDirection.AddToTabbedDisplay, tearoff, manager.RootWindow);

            CollectionAssert.AreEqual(new[] { stays.DockingAdapter, dragged.DockingAdapter }, root.ViewModel.DockedContent.ToList());
            Assert.IsTrue(tearoff.IsClosed);
            Assert.AreEqual(1, manager.RootWindow.ActivationCount);
        }

        // ===========[ Hidden panels ]=========================================
        [TestMethod]
        public void DMB_ReturnCanceledDrag_PutsThePanelBackInItsTabs_AndClosesTheTearoff ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel root = manager.RegisterRoot().ViewModel;
            var first = manager.BuildNewDisplayElement<FakeDockPanel>();
            var dragged = manager.BuildNewDisplayElement<FakeDockPanel>();
            var last = manager.BuildNewDisplayElement<FakeDockPanel>();
            root.AddDockedContent(first);
            root.AddDockedContent(dragged);
            root.AddDockedContent(last);
            root.SelectedIndex = 2;

            DockDragOrigin origin = DockDragOrigin.ForPanel(dragged.DockingAdapter);
            FakeDockWindow tearoff = manager.DoTearoff(dragged, new DockWindowBounds(0, 0, 100, 100)).Value;
            DockZoneViewModel tornOffRoot = dragged.DockingAdapter.Location;

            Assert.IsTrue(manager.ReturnCanceled(origin, tornOffRoot, tearoff));

            CollectionAssert.AreEqual(
                new[] { first.DockingAdapter, dragged.DockingAdapter, last.DockingAdapter },
                root.DockedContent.ToList()
            );
            Assert.AreEqual(2, root.SelectedIndex);
            Assert.IsTrue(tearoff.IsClosed);
            CollectionAssert.AreEqual(new[] { manager.RootWindow }, manager.AllWindows.ToList());
        }

        [TestMethod]
        public void DMB_ReturnCanceledDrag_WithNowhereToGoBack_LeavesTheTearoff ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel root = manager.RegisterRoot().ViewModel;
            var dragged = manager.BuildNewDisplayElement<FakeDockPanel>();
            root.AddDockedContent(dragged);

            DockDragOrigin origin = DockDragOrigin.ForPanel(dragged.DockingAdapter);
            FakeDockWindow tearoff = manager.DoTearoff(dragged, new DockWindowBounds(0, 0, 100, 100)).Value;
            DockZoneViewModel tornOffRoot = dragged.DockingAdapter.Location;

            // The root zone it came from is gone
            root.Teardown();

            Assert.IsFalse(manager.ReturnCanceled(origin, tornOffRoot, tearoff));
            Assert.IsFalse(tearoff.IsClosed);
            Assert.AreSame(tornOffRoot, dragged.DockingAdapter.Location);
        }

        [TestMethod]
        public void DMB_CaptureHideState_TearoffPanelGetsItsWindow_RootPanelGetsNull ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            var rootPanel = manager.BuildNewDisplayElement<FakeDockPanel>();
            root.ViewModel.AddDockedContent(rootPanel);
            var tearoffPanel = manager.BuildNewDisplayElement<FakeDockPanel>();
            manager.CreateTearoffForPanel(tearoffPanel.DockingAdapter, 10, 20, 30, 40);
            FakeDockWindow tearoff = manager.TearoffHosting(tearoffPanel.DockingAdapter);
            AttachZoneUI(manager, tearoff);
            AttachZoneUI(manager, manager.RootWindow);

            HiddenPanelPlatformState state = manager.CaptureHideState(tearoffPanel.DockingAdapter);

            Assert.IsTrue(state.WasInTearoff);
            Assert.AreSame(tearoff, state.TearoffWindowRef);
            Assert.AreEqual(10, state.NextDisplayLocationX);
            Assert.AreEqual(20, state.NextDisplayLocationY);
            Assert.AreEqual(30, state.NextDisplayWidth);
            Assert.AreEqual(40, state.NextDisplayHeight);
            Assert.IsNull(manager.CaptureHideState(rootPanel.DockingAdapter));
        }

        [TestMethod]
        public void DMB_HiddenPanel_ReshowsInTheTearoffItLeft_WhileItIsOpen ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            manager.RegisterDisplayFactory<FakeDockPanel>(new DockPanelRegistrationRules { SingleInstanceOnly = true, DefaultHideDontClose = true });
            var hidden = manager.BuildNewDisplayElement<FakeDockPanel>();
            var keepsItOpen = manager.BuildNewDisplayElement<OtherFakeDockPanel>();
            manager.CreateTearoffForPanel(hidden.DockingAdapter, 0, 0, 10, 10);
            DockZoneViewModel tearoffRoot = hidden.DockingAdapter.Location;
            tearoffRoot.AddDockedContent(keepsItOpen);
            AttachZoneUI(manager, manager.TearoffHosting(hidden.DockingAdapter));

            manager.RemoveOrHidePanel(hidden.DockingAdapter);
            Assert.IsNull(hidden.DockingAdapter.Location);

            manager.AddPanel(typeof(FakeDockPanel));

            Assert.AreSame(tearoffRoot, hidden.DockingAdapter.Location);
        }

        [TestMethod]
        public void DMB_HidingATearoffsLastPanel_ClosesTheTearoff ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            manager.RegisterDisplayFactory<FakeDockPanel>(new DockPanelRegistrationRules { DefaultHideDontClose = true });
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            var other = manager.BuildNewDisplayElement<OtherFakeDockPanel>();
            manager.CreateTearoffForPanel(panel.DockingAdapter, 0, 0, 10, 10);
            panel.DockingAdapter.Location.AddDockedContent(other);
            FakeDockWindow tearoff = manager.TearoffHosting(panel.DockingAdapter);
            AttachZoneUI(manager, tearoff);
            other.DockingAdapter.Location.RemoveDockedContent(other.DockingAdapter);

            manager.RemoveOrHidePanel(panel.DockingAdapter);

            Assert.IsTrue(tearoff.IsClosed);
        }

        // ===========[ Layout loading ]========================================
        [TestMethod]
        public void DMB_SetupDefault_LoadsThePersistentLayout ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            manager.OnLoad = path => path == "persistent.layout";

            Assert.IsTrue(manager.SetupDefaultAndFallbackTo("fallback.layout"));

            CollectionAssert.AreEqual(new[] { "persistent.layout" }, manager.Loaded);
            Assert.AreEqual(0, manager.Saved.Count);
            Assert.IsTrue(manager.WasLoadingDuringLoad);
            Assert.IsFalse(manager.IsLoadingFromLayout);
        }

        [TestMethod]
        public void DMB_SetupDefault_FallsBack_AndSavesTheFallbackAsPersistent ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            manager.OnLoad = path => path == "fallback.layout";

            Assert.IsTrue(manager.SetupDefaultAndFallbackTo("fallback.layout"));

            CollectionAssert.AreEqual(new[] { "persistent.layout", "fallback.layout" }, manager.Loaded);
            CollectionAssert.AreEqual(new[] { "persistent.layout" }, manager.Saved);
        }

        [TestMethod]
        public void DMB_SetupDefault_NothingLoads_StillEnforcesGuaranteedPanels ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            manager.RegisterDisplayFactory<FakeDockPanel>(new DockPanelRegistrationRules { GuaranteedOnStart = true });

            Assert.IsFalse(manager.SetupDefaultAndFallbackTo(null));

            Assert.AreEqual(1, root.ViewModel.DockedContent.Count);
        }

        [TestMethod]
        public void DMB_ReloadThatThrows_ReportsFalse ()
        {
            var manager = new TestDockingManager();
            manager.OnLoad = _ => throw new InvalidOperationException("bad file");

            Assert.IsFalse(manager.ReloadDockLayoutFromPersistentStorage());
            Assert.IsFalse(manager.IsLoadingFromLayout);
        }

        [TestMethod]
        public void DMB_ClearForLoad_ClosesTearoffs_AndEmptiesTheRoots ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            root.ViewModel.AddDockedContent(manager.BuildNewDisplayElement<FakeDockPanel>());
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            manager.CreateTearoffForPanel(panel.DockingAdapter, 0, 0, 10, 10);
            FakeDockWindow tearoff = manager.TearoffHosting(panel.DockingAdapter);

            manager.ClearForLoad();

            Assert.IsTrue(tearoff.IsClosed);
            Assert.AreEqual(0, root.ViewModel.DockedContent.Count);
            CollectionAssert.AreEqual(new[] { manager.RootWindow }, manager.AllWindows.ToList());
        }

        [TestMethod]
        public void DMB_SaveToPersistentStorage_SavesToThePersistentPath ()
        {
            var manager = new TestDockingManager();

            Assert.IsTrue(manager.SaveDockLayoutToPersistentStorage());

            CollectionAssert.AreEqual(new[] { "persistent.layout" }, manager.Saved);
            Assert.AreEqual("persistent.layout~", manager.GenerateDefaultStateStorageAutoSaveTempPath());
        }

        // ===========[ Layout files ]==========================================
        [TestMethod]
        public void DMB_LayoutFile_RoundTripsRootGroupsAndTearoffs ()
        {
            string path = TempLayoutPath();
            try
            {
                var saver = MakeLayoutManager();
                DockZoneViewModel main = saver.RegisterRoot("main").ViewModel;
                main.AddDockedContent(saver.BuildNewDisplayElement<FakeDockPanel>());
                main.AddDockedContent(saver.BuildNewDisplayElement<FakeDockPanel>());
                main.SelectedIndex = 1;
                var torn = saver.BuildNewDisplayElement<FakeDockPanel>();
                saver.CreateTearoffForPanel(torn.DockingAdapter, 10, 20, 300, 400);
                saver.TearoffHosting(torn.DockingAdapter).IsMaximized = true;
                Assert.IsTrue(saver.SaveDockLayoutToFile(path));

                var loader = MakeLayoutManager();
                DockZoneViewModel loadedMain = loader.RegisterRoot("main").ViewModel;
                Assert.IsTrue(loader.LoadDockLayoutFromFile(path));

                Assert.AreEqual(2, loadedMain.DockedContent.Count);
                Assert.AreEqual(1, loadedMain.SelectedIndex);
                FakeDockWindow tearoff = loader.AllWindows.Single(w => w != loader.RootWindow);
                Assert.AreEqual(new DockWindowBounds(10, 20, 300, 400), tearoff.Bounds);
                Assert.IsTrue(tearoff.IsMaximized);
                Assert.AreEqual(1, loader.ZonesFor(tearoff).Single().ViewModel.DockedContent.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void DMB_LayoutFile_KeepsItsShape ()
        {
            string path = TempLayoutPath();
            try
            {
                var saver = MakeLayoutManager();
                saver.RegisterRoot("main").ViewModel.AddDockedContent(saver.BuildNewDisplayElement<FakeDockPanel>());
                var torn = saver.BuildNewDisplayElement<FakeDockPanel>();
                saver.CreateTearoffForPanel(torn.DockingAdapter, 10, 20, 300, 400);
                saver.SaveDockLayoutToFile(path);

                // Every stack reads this shape, so its names are the format: they must not drift
                string text = File.ReadAllText(path);
                foreach (string name in new[] { "Core", "ZoneInfoByRoot", "main", "Ancillary", "WindowX", "WindowY", "WindowWidth", "WindowHeight", "IsMaximized", "IsFullscreen", "State" })
                {
                    StringAssert.Contains(text, name);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void DMB_LayoutFile_UnknownGroupLoadsIntoTheFirstRoot ()
        {
            string path = TempLayoutPath();
            try
            {
                var saver = MakeLayoutManager();
                saver.RegisterRoot("gone").ViewModel.AddDockedContent(saver.BuildNewDisplayElement<FakeDockPanel>());
                saver.SaveDockLayoutToFile(path);

                var loader = MakeLayoutManager();
                DockZoneViewModel first = loader.RegisterRoot("first").ViewModel;
                loader.RegisterRoot("second");
                Assert.IsTrue(loader.LoadDockLayoutFromFile(path));

                Assert.AreEqual(1, first.DockedContent.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void DMB_LayoutFile_UngroupedRootLoadsBackIntoItself ()
        {
            string path = TempLayoutPath();
            try
            {
                // A root zone with no group id saves under the empty string, and it is not the first root here, so
                //  falling back to the first root would put its panels in the wrong zone
                var saver = MakeLayoutManager();
                saver.RegisterRoot("main").ViewModel.AddDockedContent(saver.BuildNewDisplayElement<FakeDockPanel>());
                DockZoneViewModel ungrouped = saver.RegisterRoot().ViewModel;
                ungrouped.AddDockedContent(saver.BuildNewDisplayElement<FakeDockPanel>());
                ungrouped.AddDockedContent(saver.BuildNewDisplayElement<FakeDockPanel>());
                Assert.IsTrue(saver.SaveDockLayoutToFile(path));

                var loader = MakeLayoutManager();
                DockZoneViewModel loadedMain = loader.RegisterRoot("main").ViewModel;
                DockZoneViewModel loadedUngrouped = loader.RegisterRoot().ViewModel;
                Assert.IsTrue(loader.LoadDockLayoutFromFile(path));

                Assert.AreEqual(1, loadedMain.DockedContent.Count);
                Assert.AreEqual(2, loadedUngrouped.DockedContent.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void DMB_LayoutFile_LeavesOutAWindowAlreadyGone ()
        {
            string path = TempLayoutPath();
            try
            {
                var saver = MakeLayoutManager();
                saver.RegisterRoot("main");
                var torn = saver.BuildNewDisplayElement<FakeDockPanel>();
                saver.CreateTearoffForPanel(torn.DockingAdapter, 10, 20, 300, 400);
                saver.TearoffHosting(torn.DockingAdapter).IsClosed = true;
                saver.SaveDockLayoutToFile(path);

                var loader = MakeLayoutManager();
                loader.RegisterRoot("main");
                loader.LoadDockLayoutFromFile(path);

                CollectionAssert.AreEqual(new[] { loader.RootWindow }, loader.AllWindows.ToList());
            }
            finally
            {
                File.Delete(path);
            }
        }

        // A panel whose type id resolves to nothing (trimmed away, renamed, or from another app) is skipped, and the rest
        // of the layout loads around it
        [DataTestMethod]
        [DataRow("\"ajut-tests-panel-from-another-app\"", DisplayName = "An id nothing registered")]
        [DataRow("\"Some.Missing.Panel, Some.Missing.Assembly\"", DisplayName = "A type name whose assembly is missing")]
        [DataRow("\"Some.Panel, [not an assembly name\"", DisplayName = "A malformed type name")]
        [DataRow("null", DisplayName = "No id at all")]
        public void DMB_LayoutFile_UnresolvedPanelTypeIsSkipped (string typeIdJson)
        {
            string path = TempLayoutPath();
            try
            {
                var saver = MakeLayoutManager();
                DockZoneViewModel main = saver.RegisterRoot("main").ViewModel;
                main.AddDockedContent(saver.BuildNewDisplayElement<FakeDockPanel>());
                main.AddDockedContent(saver.BuildNewDisplayElement<FakeDockPanel>());
                Assert.IsTrue(saver.SaveDockLayoutToFile(path));
                ReplaceSecondPanelTypeId(path, typeIdJson);

                var loader = MakeLayoutManager();
                DockZoneViewModel loadedMain = loader.RegisterRoot("main").ViewModel;
                Assert.IsTrue(loader.LoadDockLayoutFromFile(path), "One panel that can't be built does not fail the whole layout");
                Assert.AreEqual(1, loadedMain.DockedContent.Count, "The panel that resolves still loads");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void DMB_LayoutFile_UnresolvedPanelBeforeTheSelectedOne_SelectionStaysOnIt ()
        {
            string path = TempLayoutPath();
            try
            {
                var saver = MakeLayoutManager();
                DockZoneViewModel main = saver.RegisterRoot("main").ViewModel;
                main.AddDockedContent(saver.BuildNewDisplayElement<FakeDockPanel>());
                main.AddDockedContent(saver.BuildNewDisplayElement<FakeDockPanel>());
                main.AddDockedContent(saver.BuildNewDisplayElement<FakeDockPanel>());
                main.SelectedIndex = 2;
                Assert.IsTrue(saver.SaveDockLayoutToFile(path));
                ReplaceSecondPanelTypeId(path, "\"ajut-tests-panel-from-another-app\"");

                var loader = MakeLayoutManager();
                DockZoneViewModel loadedMain = loader.RegisterRoot("main").ViewModel;
                Assert.IsTrue(loader.LoadDockLayoutFromFile(path));

                Assert.AreEqual(2, loadedMain.DockedContent.Count);
                Assert.AreEqual(1, loadedMain.SelectedIndex, "The panel that was selected is still the selected one, one place earlier");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void DMB_LayoutFile_SelectedPanelUnresolved_SelectionStaysInRange ()
        {
            string path = TempLayoutPath();
            try
            {
                var saver = MakeLayoutManager();
                DockZoneViewModel main = saver.RegisterRoot("main").ViewModel;
                main.AddDockedContent(saver.BuildNewDisplayElement<FakeDockPanel>());
                main.AddDockedContent(saver.BuildNewDisplayElement<FakeDockPanel>());
                main.SelectedIndex = 1;
                Assert.IsTrue(saver.SaveDockLayoutToFile(path));
                ReplaceSecondPanelTypeId(path, "\"ajut-tests-panel-from-another-app\"");

                var loader = MakeLayoutManager();
                DockZoneViewModel loadedMain = loader.RegisterRoot("main").ViewModel;
                Assert.IsTrue(loader.LoadDockLayoutFromFile(path));

                Assert.AreEqual(1, loadedMain.DockedContent.Count);
                Assert.AreEqual(0, loadedMain.SelectedIndex, "With the selected panel gone, the selection falls on a panel that loaded");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void DMB_LayoutFile_MissingFileLoadsNothing ()
        {
            var loader = MakeLayoutManager();
            loader.RegisterRoot("main");

            Assert.IsFalse(loader.LoadDockLayoutFromFile(TempLayoutPath()));
        }

        // ===========[ Dispose ]===============================================
        [TestMethod]
        public void DMB_Dispose_ClosesTearoffs_TearsDownRoots_AndEmptiesTheRegistry ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            manager.CreateTearoffForPanel(panel.DockingAdapter, 0, 0, 10, 10);
            FakeDockWindow tearoff = manager.TearoffHosting(panel.DockingAdapter);

            manager.Dispose();

            Assert.IsTrue(tearoff.IsClosed);
            Assert.IsNull(root.ViewModel.Manager);
            Assert.AreEqual(1, manager.DisposingCount);
            Assert.AreEqual(0, manager.AllRootZones.Count);
            Assert.AreEqual(0, manager.AllWindows.Count);
            Assert.IsNull(manager.GetPanelRules(typeof(FakeDockPanel)));
        }

        // ===========[ Helpers ]===============================================
        private static TestDockingManager MakeLayoutManager ()
        {
            TypeIdRegistrar.RegisterTypeId<FakeDockPanel>("ajut-tests-fake-dock-panel");
            var manager = new TestDockingManager { UseRealLayoutFiles = true };
            manager.RegisterDisplayFactory<FakeDockPanel>();
            return manager;
        }

        private static string TempLayoutPath () => Path.Combine(Path.GetTempPath(), "ajut-dock-tests", Guid.NewGuid().ToString("N") + ".layout");

        // Rewrites the type id of the second panel a saved layout file names, as raw JSON (a quoted string, or null)
        private static void ReplaceSecondPanelTypeId (string path, string typeIdJson)
        {
            const string kTypeIdKey = "\"TypeId\"";
            string text = File.ReadAllText(path);
            int first = text.IndexOf(kTypeIdKey, StringComparison.Ordinal);
            int second = text.IndexOf(kTypeIdKey, first + kTypeIdKey.Length, StringComparison.Ordinal);
            Assert.IsTrue(second > first, "The saved layout names at least two panels");

            int valueStart = text.IndexOf(':', second) + 1;
            int valueEnd = text.IndexOfAny(new[] { ',', '\n', '}' }, valueStart);
            File.WriteAllText(path, text.Substring(0, valueStart) + " " + typeIdJson + text.Substring(valueEnd));
        }

        // Tears a panel off and closes its tearoff with close, handing back only a weak reference to the window, so
        // nothing in the calling test keeps it alive. Not inlined, so no local of this method outlives the call either.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference TearOffAndClose (TestDockingManager manager, Action<TestDockingManager, DockingContentAdapterModel> close)
        {
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            manager.CreateTearoffForPanel(panel.DockingAdapter, 0, 0, 10, 10);
            FakeDockWindow tearoff = manager.TearoffHosting(panel.DockingAdapter);
            close(manager, panel.DockingAdapter);

            Assert.IsTrue(tearoff.IsClosed);
            return new WeakReference(tearoff);
        }

        // CaptureHideState finds a panel's window through its zone's UI, so give a window's registered
        // root zones a UI the way a toolkit's zone control would be
        private static void AttachZoneUI (TestDockingManager manager, FakeDockWindow window)
        {
            foreach (FakeDockZone zone in manager.ZonesFor(window))
            {
                zone.ViewModel.UI = zone;
            }
        }
    }
}
