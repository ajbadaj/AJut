#nullable enable

namespace AJut.UX.Tests
{
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DockDragRulesTests
    {
        // ===[ Starting a drag ]===

        [TestMethod]
        public void DDR_PastThreshold_IsStrict ()
        {
            Assert.IsFalse(DockDragRules.HasMovedPastThreshold(8, 0, 8), "Exactly the threshold has not moved past it");
            Assert.IsTrue(DockDragRules.HasMovedPastThreshold(8.01, 0, 8));
            Assert.IsTrue(DockDragRules.HasMovedPastThreshold(6, 6, 8), "Diagonal distance counts");
            Assert.IsFalse(DockDragRules.HasMovedPastThreshold(5, 5, 8));
        }

        [TestMethod]
        public void DDR_ReachedThreshold_CountsTheThreshold ()
        {
            Assert.IsTrue(DockDragRules.HasReachedThreshold(8, 0, 8));
            Assert.IsFalse(DockDragRules.HasReachedThreshold(7.99, 0, 8));
        }

        [TestMethod]
        public void DDR_TabDrag_ShortOfTheThreshold_IsNothingYet ()
        {
            DockingContentAdapterModel tab = BuildTab();
            Assert.AreEqual(eTabDragKind.None, DockDragRules.DecideTabDrag(tab, 5, 5, 8));
            Assert.AreEqual(eTabDragKind.None, DockDragRules.DecideTabDrag(tab, 0, 7.99, 8));
            Assert.AreEqual(eTabDragKind.Tearoff, DockDragRules.DecideTabDrag(tab, 0, 8, 8), "The threshold itself counts");
        }

        [TestMethod]
        public void DDR_TabDrag_SidewaysReorders_UpOrDownOrDiagonalTearsOff ()
        {
            DockingContentAdapterModel tab = BuildTab();
            Assert.AreEqual(eTabDragKind.Reorder, DockDragRules.DecideTabDrag(tab, 10, 3, 8));
            Assert.AreEqual(eTabDragKind.Reorder, DockDragRules.DecideTabDrag(tab, -10, 3, 8));
            Assert.AreEqual(eTabDragKind.Tearoff, DockDragRules.DecideTabDrag(tab, 3, 10, 8));
            Assert.AreEqual(eTabDragKind.Tearoff, DockDragRules.DecideTabDrag(tab, 6, -6, 8), "A tie tears off");
        }

        [TestMethod]
        public void DDR_TabDrag_APanelThatCantTearOff_StaysInTheStrip ()
        {
            DockingContentAdapterModel tab = BuildTab(canTearoff: false);
            Assert.AreEqual(eTabDragKind.None, DockDragRules.DecideTabDrag(tab, 3, 10, 8), "Up or down is not a tearoff for a panel that can't tear off");
            Assert.AreEqual(eTabDragKind.Reorder, DockDragRules.DecideTabDrag(tab, 10, 3, 8), "Sideways still reorders");
        }

        // ===[ Placing a tearoff ]===

        [TestMethod]
        public void DDR_Placement_DefaultsToCenteredOnTheTitleBar ()
        {
            DockTearoffPlacement placement = DockDragRules.PlaceTearoff(new DockZoneSize(300, 200), new DockPoint(1000, 500), null, 1.0);

            Assert.AreEqual(300, placement.Width);
            Assert.AreEqual(200, placement.Height);
            Assert.AreEqual(new DockPoint(150, DockDragRules.kDefaultCursorOffsetY), placement.CursorOffset);
            Assert.AreEqual(new DockPoint(150, DockDragRules.kDefaultCursorOffsetY), placement.ScreenCursorOffset);
            Assert.AreEqual(new DockPoint(850, 485), placement.Origin);
        }

        [TestMethod]
        public void DDR_Placement_KeepsTheMinimumSize_InWholeUnits ()
        {
            DockTearoffPlacement placement = DockDragRules.PlaceTearoff(new DockZoneSize(120.7, 30), new DockPoint(0, 0), null, 1.0);
            Assert.AreEqual(DockDragRules.kMinTearoffWidth, placement.Width);
            Assert.AreEqual(DockDragRules.kMinTearoffHeight, placement.Height);

            DockTearoffPlacement large = DockDragRules.PlaceTearoff(new DockZoneSize(640.9, 480.6), new DockPoint(0, 0), null, 1.0);
            Assert.AreEqual(640, large.Width);
            Assert.AreEqual(480, large.Height);
        }

        [TestMethod]
        public void DDR_Placement_ClampsAGivenOffsetIntoTheWindow ()
        {
            DockTearoffPlacement placement = DockDragRules.PlaceTearoff(new DockZoneSize(300, 200), new DockPoint(1000, 500), new DockPoint(450, -20), 1.0);

            Assert.AreEqual(new DockPoint(300, 0), placement.CursorOffset);
            Assert.AreEqual(new DockPoint(700, 500), placement.Origin);
        }

        [TestMethod]
        public void DDR_Placement_AtADisplayScale_KeepsThePointerWhereItWasPressed ()
        {
            // Sizes and the offset are in view units, the anchor and origin in pixels at one and a half pixels per unit
            DockTearoffPlacement placement = DockDragRules.PlaceTearoff(new DockZoneSize(300, 200), new DockPoint(1000, 500), new DockPoint(40, 30), 1.5);

            Assert.AreEqual(300, placement.Width, "The window's size stays in view units");
            Assert.AreEqual(new DockPoint(40, 30), placement.CursorOffset);
            Assert.AreEqual(new DockPoint(60, 45), placement.ScreenCursorOffset);
            Assert.AreEqual(new DockPoint(940, 455), placement.Origin);
        }

        [TestMethod]
        public void DDR_Placement_AtADisplayScale_ScalesTheDefaultOffsetToo ()
        {
            DockTearoffPlacement placement = DockDragRules.PlaceTearoff(new DockZoneSize(300, 200), new DockPoint(1000, 500), null, 2.0);

            Assert.AreEqual(new DockPoint(300, 2.0 * DockDragRules.kDefaultCursorOffsetY), placement.ScreenCursorOffset);
            Assert.AreEqual(new DockPoint(700, 500 - (2.0 * DockDragRules.kDefaultCursorOffsetY)), placement.Origin);
        }

        // ===[ Header drags ]===

        [TestMethod]
        public void DDR_HeaderDrag_InTheRootWindow_TakesTheZone ()
        {
            var manager = new TestDockingManager();
            var zone = new DockZoneViewModel(manager);
            Assert.AreEqual(eHeaderDragSubject.ThisZone, DockDragRules.ChooseHeaderDragSubject(zone, isInTearoff: false, tearoffRootZone: null));
        }

        [TestMethod]
        public void DDR_HeaderDrag_InATearoff_DependsOnTheTearoffsRoot ()
        {
            var manager = new TestDockingManager();

            var leafRoot = new DockZoneViewModel(manager);
            leafRoot.AddDockedContent(manager.BuildNewDisplayElement<FakeDockPanel>());
            Assert.AreEqual(eHeaderDragSubject.None, DockDragRules.ChooseHeaderDragSubject(leafRoot, isInTearoff: true, tearoffRootZone: leafRoot), "The root zone's header leaves dragging to the title bar");

            var splitRoot = new DockZoneViewModel(manager);
            splitRoot.Configure(eDockOrientation.Horizontal);
            var inSplit = new DockZoneViewModel(manager);
            Assert.AreEqual(eHeaderDragSubject.ThisZone, DockDragRules.ChooseHeaderDragSubject(inSplit, isInTearoff: true, tearoffRootZone: splitRoot), "The rest of the split keeps the tearoff from emptying");

            var underLeafRoot = new DockZoneViewModel(manager);
            Assert.AreEqual(eHeaderDragSubject.TearoffRoot, DockDragRules.ChooseHeaderDragSubject(underLeafRoot, isInTearoff: true, tearoffRootZone: leafRoot));
        }

        [TestMethod]
        public void DDR_HeaderDrag_AZoneHoldingAPanelThatCantTearOff_StaysPut ()
        {
            var manager = new TestDockingManager();
            var zone = new DockZoneViewModel(manager);
            var pinned = manager.BuildNewDisplayElement<FakeDockPanel>();
            zone.AddDockedContent(manager.BuildNewDisplayElement<FakeDockPanel>());
            zone.AddDockedContent(pinned);
            pinned.DockingAdapter.CanTearoff = false;

            Assert.AreEqual(eHeaderDragSubject.None, DockDragRules.ChooseHeaderDragSubject(zone, isInTearoff: false, tearoffRootZone: null), "In the root window");

            var splitRoot = new DockZoneViewModel(manager);
            splitRoot.Configure(eDockOrientation.Horizontal);
            Assert.AreEqual(eHeaderDragSubject.None, DockDragRules.ChooseHeaderDragSubject(zone, isInTearoff: true, tearoffRootZone: splitRoot), "In a tearoff split");
        }

        [TestMethod]
        public void DDR_HeaderDrag_ATearoffHoldingAPanelThatCantTearOff_StillMovesAsAWhole ()
        {
            // Moving a tearoff that already exists tears nothing off
            var manager = new TestDockingManager();
            var leafRoot = new DockZoneViewModel(manager);
            var pinned = manager.BuildNewDisplayElement<FakeDockPanel>();
            leafRoot.AddDockedContent(pinned);
            pinned.DockingAdapter.CanTearoff = false;
            var underLeafRoot = new DockZoneViewModel(manager);

            Assert.AreEqual(eHeaderDragSubject.TearoffRoot, DockDragRules.ChooseHeaderDragSubject(underLeafRoot, isInTearoff: true, tearoffRootZone: leafRoot));
        }

        // ===[ The drop overlay ]===

        [TestMethod]
        public void DDR_Overlay_WidgetsByZoneShape ()
        {
            Assert.IsFalse(DockDragRules.ShowsSplitWidgets(eDockOrientation.Empty));
            Assert.IsTrue(DockDragRules.IsCenterWidgetEnabled(eDockOrientation.Empty), "An empty zone can only be filled");

            foreach (eDockOrientation leaf in new[] { eDockOrientation.Single, eDockOrientation.Tabbed })
            {
                Assert.IsTrue(DockDragRules.ShowsSplitWidgets(leaf));
                Assert.IsTrue(DockDragRules.IsCenterWidgetEnabled(leaf));
            }

            foreach (eDockOrientation split in new[] { eDockOrientation.Horizontal, eDockOrientation.Vertical })
            {
                Assert.IsTrue(DockDragRules.ShowsSplitWidgets(split));
                Assert.IsFalse(DockDragRules.IsCenterWidgetEnabled(split), "A split holds zones, not panels");
            }
        }

        [TestMethod]
        public void DDR_Overlay_GridIs84By60 ()
        {
            Assert.AreEqual(84, DockDragRules.kOverlayWidth);
            Assert.AreEqual(60, DockDragRules.kOverlayHeight);
        }

        [TestMethod]
        public void DDR_Overlay_DirectionAtAPoint ()
        {
            Assert.AreEqual(eDockInsertionDirection.Top, DockDragRules.FindOverlayDirectionAt(42, 5));
            Assert.AreEqual(eDockInsertionDirection.Left, DockDragRules.FindOverlayDirectionAt(5, 30));
            Assert.AreEqual(eDockInsertionDirection.AddToTabbedDisplay, DockDragRules.FindOverlayDirectionAt(42, 30));
            Assert.AreEqual(eDockInsertionDirection.Right, DockDragRules.FindOverlayDirectionAt(70, 30));
            Assert.AreEqual(eDockInsertionDirection.Bottom, DockDragRules.FindOverlayDirectionAt(42, 50));

            // Cell edges: each cell starts at its edge and stops short of the next
            Assert.AreEqual(eDockInsertionDirection.AddToTabbedDisplay, DockDragRules.FindOverlayDirectionAt(20, 19));
            Assert.AreEqual(eDockInsertionDirection.Right, DockDragRules.FindOverlayDirectionAt(64, 40.9));
        }

        [TestMethod]
        public void DDR_Overlay_CornersAndOutside_HoldNothing ()
        {
            Assert.IsNull(DockDragRules.FindOverlayDirectionAt(5, 5));
            Assert.IsNull(DockDragRules.FindOverlayDirectionAt(80, 55));
            Assert.IsNull(DockDragRules.FindOverlayDirectionAt(-1, 30));
            Assert.IsNull(DockDragRules.FindOverlayDirectionAt(42, 60));
            Assert.IsNull(DockDragRules.FindOverlayDirectionAt(84, 30));
        }

        // ===[ Tab reordering ]===

        [TestMethod]
        public void DDR_TabIndexAtX ()
        {
            double[] widths = { 50, 80, 60 };
            Assert.AreEqual(0, DockDragRules.FindTabIndexAtX(widths, 0));
            Assert.AreEqual(0, DockDragRules.FindTabIndexAtX(widths, 49.9));
            Assert.AreEqual(1, DockDragRules.FindTabIndexAtX(widths, 50));
            Assert.AreEqual(2, DockDragRules.FindTabIndexAtX(widths, 189));
            Assert.AreEqual(2, DockDragRules.FindTabIndexAtX(widths, 500), "Past the last tab counts as the last");
            Assert.AreEqual(0, DockDragRules.FindTabIndexAtX(new double[0], 10), "An empty strip gives 0");
        }

        [TestMethod]
        public void DDR_InsertLine ()
        {
            double[] widths = { 50, 80, 60 };
            Assert.IsNull(DockDragRules.FindInsertLineX(widths, 1, 1), "Over its own place nothing moves");
            Assert.AreEqual(50, DockDragRules.FindInsertLineX(widths, 2, 1), "Before the source: the target's leading edge");
            Assert.AreEqual(0, DockDragRules.FindInsertLineX(widths, 2, 0));
            Assert.AreEqual(130, DockDragRules.FindInsertLineX(widths, 0, 1), "After the source: the target's trailing edge");
            Assert.AreEqual(190, DockDragRules.FindInsertLineX(widths, 0, 2));
            Assert.AreEqual(190, DockDragRules.FindInsertLineX(widths, 0, 7), "A target past the strip ends at its end");
        }

        [TestMethod]
        public void DDR_ReorderCommit ()
        {
            Assert.IsTrue(DockDragRules.ShouldCommitTabReorder(0, 2, 3));
            Assert.IsFalse(DockDragRules.ShouldCommitTabReorder(1, 1, 3));
            Assert.IsFalse(DockDragRules.ShouldCommitTabReorder(0, -1, 3));
            Assert.IsFalse(DockDragRules.ShouldCommitTabReorder(0, 3, 3));
        }

        // ===[ Helpers ]===

        private static DockingContentAdapterModel BuildTab (bool canTearoff = true)
        {
            var manager = new TestDockingManager();
            var zone = new DockZoneViewModel(manager);
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            panel.DockingAdapter.CanTearoff = canTearoff;
            zone.AddDockedContent(panel);
            return panel.DockingAdapter;
        }
    }
}
