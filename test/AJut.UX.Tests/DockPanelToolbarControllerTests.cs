namespace AJut.UX.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>A toolbar view as the controller sees it: the buttons it was last handed, and how often it was asked to refresh</summary>
    public class FakeToolbarHost : IDockPanelToolbarHost
    {
        public IReadOnlyList<DockPanelToolbarController.Button> Shown { get; private set; } = new List<DockPanelToolbarController.Button>();
        public int ShowCount { get; private set; }
        public int RefreshCount { get; private set; }

        public void ShowButtons (IReadOnlyList<DockPanelToolbarController.Button> buttons)
        {
            ++this.ShowCount;
            this.Shown = buttons;
        }

        public void RefreshButtonStates () => ++this.RefreshCount;
    }

    public sealed record ToolbarFixture (FakeToolbarHost Host, DockPanelToolbarController Controller);

    [TestClass]
    public class DockPanelToolbarControllerTests
    {
        [TestMethod]
        public void Toolbar_ButtonPerVisibleType_ToggleForSingleInstance_AddOtherwise ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            manager.RegisterDisplayFactory<FakeDockPanel>(singleInstanceOnly: true);
            manager.RegisterDisplayFactory<OtherFakeDockPanel>();
            manager.UISyncVM.RegisterPanelDisplayOverride<OtherFakeDockPanel>("Others");
            var (host, _) = MakeToolbar(manager);

            Assert.AreEqual(2, host.Shown.Count);
            Assert.IsTrue(host.Shown[0].IsToggle);
            Assert.AreEqual("Fake Dock Panel", host.Shown[0].Label, "A type's display name starts as its friendly type name");
            Assert.IsFalse(host.Shown[1].IsToggle);
            Assert.AreEqual("Others", host.Shown[1].Label);
        }

        [TestMethod]
        public void Toolbar_TypeHiddenFromTheToolbar_GetsNoButton ()
        {
            var manager = new TestDockingManager();
            manager.RegisterDisplayFactory<FakeDockPanel>();
            manager.RegisterDisplayFactory<OtherFakeDockPanel>();
            manager.UISyncVM.FindEntry(typeof(OtherFakeDockPanel)).IsHiddenFromToolbar = true;

            var (host, _) = MakeToolbar(manager);

            CollectionAssert.AreEqual(new[] { typeof(FakeDockPanel) }, host.Shown.Select(b => b.Entry.PanelType).ToList());
        }

        [TestMethod]
        public void Toolbar_ATypeRegisteredLater_RebuildsTheButtons ()
        {
            var manager = new TestDockingManager();
            manager.RegisterDisplayFactory<FakeDockPanel>();
            var (host, _) = MakeToolbar(manager);

            manager.RegisterDisplayFactory<OtherFakeDockPanel>();

            Assert.AreEqual(2, host.Shown.Count);
        }

        [TestMethod]
        public void Toolbar_APanelShowing_RefreshesTheCheckedState ()
        {
            var manager = new TestDockingManager();
            manager.RegisterRoot();
            manager.RegisterDisplayFactory<FakeDockPanel>(singleInstanceOnly: true);
            var (host, _) = MakeToolbar(manager);
            int refreshesBefore = host.RefreshCount;

            manager.AddPanel(typeof(FakeDockPanel));

            Assert.IsTrue(host.RefreshCount > refreshesBefore);
            Assert.IsTrue(host.Shown[0].IsChecked);
        }

        [TestMethod]
        public void Toolbar_HidingAType_RebuildsWithoutIt ()
        {
            var manager = new TestDockingManager();
            manager.RegisterDisplayFactory<FakeDockPanel>();
            manager.RegisterDisplayFactory<OtherFakeDockPanel>();
            var (host, _) = MakeToolbar(manager);

            manager.UISyncVM.HidePanelDisplayActions<OtherFakeDockPanel>();

            Assert.AreEqual(1, host.Shown.Count);
        }

        [TestMethod]
        public void Toolbar_Activating_TogglesOrAdds ()
        {
            var manager = new TestDockingManager();
            FakeDockZone root = manager.RegisterRoot();
            manager.RegisterDisplayFactory<FakeDockPanel>(singleInstanceOnly: true);
            manager.RegisterDisplayFactory<OtherFakeDockPanel>();
            var (host, controller) = MakeToolbar(manager);

            controller.OnButtonActivated(host.Shown[0]);
            controller.OnButtonActivated(host.Shown[1]);
            controller.OnButtonActivated(host.Shown[1]);
            Assert.AreEqual(3, root.ViewModel.DockedContent.Count, "The toggle shows its panel, and each add adds one");

            controller.OnButtonActivated(host.Shown[0]);
            Assert.AreEqual(2, root.ViewModel.DockedContent.Count, "The toggle hides its panel again");
        }

        [TestMethod]
        public void Toolbar_AnotherManager_LetsGoOfTheFirst ()
        {
            var first = new TestDockingManager();
            first.RegisterDisplayFactory<FakeDockPanel>();
            var (host, controller) = MakeToolbar(first);

            controller.DockingManager = new TestDockingManager();
            int showsBefore = host.ShowCount;
            first.RegisterDisplayFactory<OtherFakeDockPanel>();

            Assert.AreEqual(showsBefore, host.ShowCount, "The first manager's changes no longer reach the toolbar");
            Assert.AreEqual(0, host.Shown.Count);
        }

        [TestMethod]
        public void Toolbar_Detached_LetsGoOfTheManagersSync ()
        {
            var manager = new TestDockingManager();
            manager.RegisterDisplayFactory<FakeDockPanel>();
            var toolbar = MakeToolbar(manager);

            toolbar.Controller.OnDetached();
            int showsBefore = toolbar.Host.ShowCount;
            manager.RegisterDisplayFactory<OtherFakeDockPanel>();

            Assert.AreEqual(showsBefore, toolbar.Host.ShowCount, "A toolbar out of its window must not leave the manager holding it");
        }

        [TestMethod]
        public void Toolbar_Reattached_CatchesUpAndFollowsOnce ()
        {
            var manager = new TestDockingManager();
            manager.RegisterDisplayFactory<FakeDockPanel>();
            var toolbar = MakeToolbar(manager);

            toolbar.Controller.OnDetached();
            manager.RegisterDisplayFactory<OtherFakeDockPanel>();
            toolbar.Controller.OnAttached();
            Assert.AreEqual(2, toolbar.Host.Shown.Count, "Coming back shows the types registered while it was away");

            int showsBefore = toolbar.Host.ShowCount;
            manager.UISyncVM.HidePanelDisplayActions<OtherFakeDockPanel>();
            Assert.AreEqual(showsBefore + 1, toolbar.Host.ShowCount, "Followed once, not twice");
        }

        private static ToolbarFixture MakeToolbar (TestDockingManager manager)
        {
            var host = new FakeToolbarHost();
            var controller = new DockPanelToolbarController(host) { DockingManager = manager };
            return new ToolbarFixture(host, controller);
        }
    }
}
