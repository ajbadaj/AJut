#nullable enable

namespace AJut.UX.Tests
{
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DockZoneSelectionTests
    {
        [TestMethod]
        public void DZS_RootZoneTornOffWhole_KeepsItsSelectedTab ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel root = manager.RegisterRoot().ViewModel;
            AddPanel(manager, root);
            AddPanel(manager, root);
            FakeDockPanel third = AddPanel(manager, root);
            root.SelectedIndex = 2;

            FakeDockWindow tearoff = manager.DoGroupTearoff(root, new DockWindowBounds(50, 50, 300, 200)).Value;

            DockZoneViewModel tearoffRoot = manager.ZonesFor(tearoff)[0].ViewModel;
            Assert.AreSame(third.DockingAdapter, tearoffRoot.DockedContent[tearoffRoot.SelectedIndex], "The tearoff shows the tab that was selected");
        }

        [TestMethod]
        public void DZS_DropOnADirectionArrow_KeepsTheTargetsSelectedTab ()
        {
            var manager = new TestDockingManager();
            DockZoneViewModel root = manager.RegisterRoot().ViewModel;
            AddPanel(manager, root);
            FakeDockPanel second = AddPanel(manager, root);
            AddPanel(manager, root);
            root.SelectedIndex = 1;

            var dropped = new DockZoneViewModel(manager);
            AddPanel(manager, dropped);
            root.DropAddSiblingIntoDock(dropped, eDockInsertionDirection.Right);

            DockZoneViewModel kept = root.Children[0];
            Assert.AreSame(second.DockingAdapter, kept.DockedContent[kept.SelectedIndex], "The zone split off from the target shows the tab that was selected");
        }

        private static FakeDockPanel AddPanel (TestDockingManager manager, DockZoneViewModel zone)
        {
            var panel = manager.BuildNewDisplayElement<FakeDockPanel>();
            zone.AddDockedContent(panel);
            return panel;
        }
    }
}
