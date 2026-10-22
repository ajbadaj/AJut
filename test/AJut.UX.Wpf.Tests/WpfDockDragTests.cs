namespace AJut.UX.Tests
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using AJut.UX.Controls;
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// When WPF's docking headers start a drag: a tab dragged up or down out of its strip tears its panel off, and a zone's
    /// header dragged tears the zone off. Each asks its drag command's CanExecute on a header control that is never shown.
    /// </summary>
    [TestClass]
    public class WpfDockDragTests
    {
        [TestMethod]
        public void WpfDockDrag_TabOfAPanelThatCanTearOff_StartsATearoff ()
        {
            StaTestThread.Run(() =>
            {
                Assert.IsTrue(CanTearOffTab(canTearoff: true));
            });
        }

        [TestMethod]
        public void WpfDockDrag_TabOfAPanelThatCantTearOff_DoesNotStartATearoff ()
        {
            StaTestThread.Run(() =>
            {
                Assert.IsFalse(CanTearOffTab(canTearoff: false), "A tab drag tears off a panel that can't tear off");
            });
        }

        [TestMethod]
        public void WpfDockDrag_HeaderOfAZoneThatCanTearOff_StartsATearoff ()
        {
            StaTestThread.Run(() =>
            {
                Assert.IsTrue(CanTearOffHeader(canTearoff: true));
            });
        }

        [TestMethod]
        public void WpfDockDrag_HeaderOfAZoneThatCantTearOff_DoesNotStartATearoff ()
        {
            StaTestThread.Run(() =>
            {
                Assert.IsFalse(CanTearOffHeader(canTearoff: false), "A header drag tears off a zone holding a panel that can't tear off");
            });
        }

        // ===========[ Helpers ]===============================================
        // Whether a vertical drag on a tab for a panel that can (or can't) tear off would start
        private static bool CanTearOffTab (bool canTearoff)
        {
            var manager = new TestDockingManager();
            var adapter = new DockingContentAdapterModel(manager) { CanTearoff = canTearoff };
            var tabs = new DockZoneTabHeadersControl();
            var tab = new Border { DataContext = new DockZoneTabHeadersControl.HeaderItem(adapter) };

            var tracking = new ActiveDragTracking(tabs, tab, Mouse.PrimaryDevice, new Point());
            return DragDropElement.VerticalDragInitiatedCommand.CanExecute(tracking, tabs);
        }

        // Whether a drag on the header of a root window zone holding a panel that can (or can't) tear off would start
        private static bool CanTearOffHeader (bool canTearoff)
        {
            var manager = new TestDockingManager();
            var zone = new DockZoneViewModel(manager);
            var adapter = new DockingContentAdapterModel(manager) { CanTearoff = canTearoff };
            zone.AddDockedContent(adapter);
            var header = new DockZoneSelectedHeaderControl { Target = adapter };

            var tracking = new ActiveDragTracking(header, header, Mouse.PrimaryDevice, new Point());
            return DragDropElement.DragInitiatedCommand.CanExecute(tracking, header);
        }
    }
}
