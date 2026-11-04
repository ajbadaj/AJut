#nullable enable

namespace AJut.UX.Tests
{
    using System.Collections.Generic;
    using System.Globalization;
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DockDragOriginTests
    {
        private static readonly DockWindowBounds kTearoffBounds = new DockWindowBounds(50, 50, 300, 200);

        // ===[ A panel torn off on its own ]===

        [TestMethod]
        public void DDO_PanelFromTabs_GoesBackToItsTabPosition_WithTheSameSelection ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel zone = manager.RegisterRoot().ViewModel;
            FakeDockPanel first = AddPanel(manager, zone);
            FakeDockPanel second = AddPanel(manager, zone);
            FakeDockPanel third = AddPanel(manager, zone);
            zone.SelectedIndex = 2;

            DockDragOrigin origin = DockDragOrigin.ForPanel(first.DockingAdapter);
            FakeDockWindow tearoff = manager.DoTearoff(first, kTearoffBounds).Value;

            ReturnFromTearoff(manager, origin, tearoff);

            CollectionAssert.AreEqual(new[] { first.DockingAdapter, second.DockingAdapter, third.DockingAdapter }, zone.DockedContent);
            Assert.AreEqual(2, zone.SelectedIndex, "What was selected is selected again");
            Assert.IsTrue(tearoff.IsClosed, "The tearoff the drag made closes");
        }

        [TestMethod]
        public void DDO_PanelAloneInASplit_SplitsBackOnItsSide_AtTheOldSizes ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel root = manager.RegisterRoot().ViewModel;
            root.Configure(eDockOrientation.Horizontal);
            FakeDockZone rootUI = GiveUI(root, 1000, 600);
            DockZoneViewModel left = AddChild(manager, root, 300, 600);
            DockZoneViewModel right = AddChild(manager, root, 700, 600);
            FakeDockPanel leftPanel = AddPanel(manager, left);
            FakeDockPanel rightPanel = AddPanel(manager, right);

            DockDragOrigin origin = DockDragOrigin.ForPanel(leftPanel.DockingAdapter);
            FakeDockWindow tearoff = manager.DoTearoff(leftPanel, kTearoffBounds).Value;
            Assert.AreEqual(0, root.Children.Count, "Tearing off collapsed the split into the root");

            ReturnFromTearoff(manager, origin, tearoff);

            Assert.AreEqual(eDockOrientation.Horizontal, root.Orientation);
            Assert.AreEqual(2, root.Children.Count);
            Assert.AreSame(leftPanel.DockingAdapter, root.Children[0].DockedContent[0], "Back on the left");
            Assert.AreSame(rightPanel.DockingAdapter, root.Children[1].DockedContent[0]);
            CollectionAssert.AreEqual(new List<double> { 300, 700 }, rootUI.TargetSizeCalls[rootUI.TargetSizeCalls.Count - 1]);
            Assert.IsTrue(tearoff.IsClosed);
        }

        // ===[ A zone torn off whole ]===

        [TestMethod]
        public void DDO_ZoneFromASplitOfThree_GoesBackInItsPlace_AtTheOldSizes ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel root = manager.RegisterRoot().ViewModel;
            root.Configure(eDockOrientation.Vertical);
            FakeDockZone rootUI = GiveUI(root, 800, 900);
            DockZoneViewModel top = AddChild(manager, root, 800, 200);
            DockZoneViewModel middle = AddChild(manager, root, 800, 300);
            DockZoneViewModel bottom = AddChild(manager, root, 800, 400);
            AddPanel(manager, top);
            AddPanel(manager, middle);
            AddPanel(manager, bottom);

            DockDragOrigin origin = DockDragOrigin.ForZone(middle);
            FakeDockWindow tearoff = manager.DoGroupTearoff(middle, kTearoffBounds).Value;
            CollectionAssert.AreEqual(new[] { top, bottom }, root.Children);

            ReturnFromTearoff(manager, origin, tearoff);

            CollectionAssert.AreEqual(new[] { top, middle, bottom }, root.Children);
            CollectionAssert.AreEqual(new List<double> { 200, 300, 400 }, rootUI.TargetSizeCalls[rootUI.TargetSizeCalls.Count - 1]);
            Assert.IsTrue(tearoff.IsClosed);
        }

        [TestMethod]
        public void DDO_ZoneFromANestedSplitOfTwo_RebuildsTheNestedSplit ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel root = manager.RegisterRoot().ViewModel;
            root.Configure(eDockOrientation.Horizontal);
            GiveUI(root, 1000, 600);
            DockZoneViewModel side = AddChild(manager, root, 250, 600);
            DockZoneViewModel stack = AddChild(manager, root, 750, 600);
            stack.Configure(eDockOrientation.Vertical);
            DockZoneViewModel upper = AddChild(manager, stack, 750, 400);
            DockZoneViewModel lower = AddChild(manager, stack, 750, 200);
            AddPanel(manager, side);
            FakeDockPanel upperPanel = AddPanel(manager, upper);
            AddPanel(manager, lower);

            DockDragOrigin origin = DockDragOrigin.ForZone(lower);
            FakeDockWindow tearoff = manager.DoGroupTearoff(lower, kTearoffBounds).Value;
            CollectionAssert.AreEqual(new[] { side, upper }, root.Children, "The vertical split collapsed into the zone left in it");

            // Moving into the root dropped the zone's view; the toolkit builds it a new one there
            FakeDockZone upperUI = GiveUI(upper, 750, 600);

            ReturnFromTearoff(manager, origin, tearoff);

            Assert.AreEqual(2, root.Children.Count);
            DockZoneViewModel rebuilt = root.Children[1];
            Assert.AreEqual(eDockOrientation.Vertical, rebuilt.Orientation);
            Assert.AreSame(upperPanel.DockingAdapter, rebuilt.Children[0].DockedContent[0], "The zone that was above is above again");
            Assert.AreSame(lower, rebuilt.Children[1]);
            CollectionAssert.AreEqual(new List<double> { 400, 200 }, upperUI.TargetSizeCalls[upperUI.TargetSizeCalls.Count - 1]);
        }

        [TestMethod]
        public void DDO_RootZoneTornOffWhole_FillsItsSlotAgain ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel root = manager.RegisterRoot().ViewModel;
            FakeDockPanel first = AddPanel(manager, root);
            FakeDockPanel second = AddPanel(manager, root);
            root.SelectedIndex = 1;

            DockDragOrigin origin = DockDragOrigin.ForZone(root);
            FakeDockWindow tearoff = manager.DoGroupTearoff(root, kTearoffBounds).Value;
            Assert.AreEqual(eDockOrientation.Empty, root.Orientation, "The root keeps an empty slot");

            ReturnFromTearoff(manager, origin, tearoff);

            CollectionAssert.AreEqual(new[] { first.DockingAdapter, second.DockingAdapter }, root.DockedContent);
            Assert.AreEqual(1, root.SelectedIndex);
            Assert.IsTrue(tearoff.IsClosed);
        }

        [TestMethod]
        public void DDO_NoPlaceLeftToGoBackTo_SaysSo ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel root = manager.RegisterRoot().ViewModel;
            root.Configure(eDockOrientation.Horizontal);
            DockZoneViewModel left = AddChild(manager, root, 300, 600);
            DockZoneViewModel right = AddChild(manager, root, 700, 600);
            AddPanel(manager, left);
            AddPanel(manager, right);

            DockDragOrigin origin = DockDragOrigin.ForZone(left);
            manager.DoGroupTearoff(left, kTearoffBounds);
            manager.Dispose();

            Assert.IsFalse(origin.TryFindReturn(out _, out _), "The root the split collapsed into is gone with its manager");
        }

        // ===[ The session ]===

        [TestMethod]
        public void DDS_Cancel_EndsWithNothingToDrop ()
        {
            var host = new RecordingHost();
            var session = new DockDragSession<string, string, string>(host);
            session.Update("zone", "widget", null);

            session.Cancel();
            session.Cancel();

            Assert.IsTrue(session.IsEnded);
            Assert.IsTrue(session.IsCanceled);
            Assert.IsNull(session.EngagedWidget, "A canceled drag drops nothing");
            Assert.IsNull(session.DropTarget);
            CollectionAssert.AreEqual(new[] { "engage widget", "show zone", "dim", "undim", "disengage widget", "hide zone" }, host.Calls);
        }

        [TestMethod]
        public void DDS_End_IsNotACancel ()
        {
            var session = new DockDragSession<string, string, string>(new RecordingHost());
            session.Update("zone", "widget", null);

            session.End();
            session.Cancel();

            Assert.IsFalse(session.IsCanceled, "Cancel after End does nothing");
            Assert.AreEqual("widget", session.EngagedWidget);
        }

        // ===[ Helpers ]===

        // Drops the tearoff's root zone back where the origin says, the way a toolkit does on cancel
        private static void ReturnFromTearoff (TestDockingManager manager, DockDragOrigin origin, FakeDockWindow tearoff)
        {
            DockZoneViewModel tornOffRoot = manager.ZonesFor(tearoff)[0].ViewModel;
            Assert.IsTrue(origin.TryFindReturn(out DockZoneViewModel target, out eDockInsertionDirection direction));
            manager.Drop(target, tornOffRoot, direction, tearoff, null);
            origin.RestoreAfterReturn(tornOffRoot);
        }

        private static FakeDockPanel AddPanel (TestDockingManager manager, DockZoneViewModel zone)
        {
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            zone.AddDockedContent(panel);
            return panel;
        }

        private static DockZoneViewModel AddChild (TestDockingManager manager, DockZoneViewModel split, double width, double height)
        {
            var child = new DockZoneViewModel(manager);
            GiveUI(child, width, height);
            split.AddChild(child);
            return child;
        }

        private static FakeDockZone GiveUI (DockZoneViewModel zone, double width, double height)
        {
            var ui = new FakeDockZone(zone) { RenderSize = new DockZoneSize(width, height) };
            zone.UI = ui;
            return ui;
        }

        private sealed class RecordingHost : IDockDragSessionHost<string, string, string>
        {
            public List<string> Calls { get; } = new List<string>();

            public void ShowDropTarget (string zone, bool isTarget) => this.Calls.Add($"{(isTarget ? "show" : "hide")} {zone}");
            public void EngageWidget (string widget, bool isEngaged) => this.Calls.Add($"{(isEngaged ? "engage" : "disengage")} {widget}");
            public void DimDragSource (bool isDimmed) => this.Calls.Add(isDimmed ? "dim" : "undim");
            public void BeginHoverFlash (string window) => this.Calls.Add($"begin flash {window}");
            public void SetHoverFlashOpacity (string window, double opacity) => this.Calls.Add($"flash {window} {opacity.ToString(CultureInfo.InvariantCulture)}");
            public void EndHoverFlash (string window) => this.Calls.Add($"end flash {window}");
            public void RaiseHoverWindow (string window) => this.Calls.Add($"raise {window}");
        }
    }
}
