namespace AJut.UX.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The menu WPF's DockingManager keeps for its panels (ManageMenu). WPF objects need an STA thread, so each test runs
    /// on one, and nothing is ever shown.
    /// </summary>
    [TestClass]
    public class WpfDockingManagerMenuTests
    {
        [TestMethod]
        public void WpfDockMenu_ItemsARebuildDrops_NoLongerReachTheManager ()
        {
            StaTestThread.Run(() =>
            {
                int built = 0;
                DockingManager manager = MakeManager();
                manager.RegisterDisplayFactory<FakeDockPanel>(() =>
                {
                    ++built;
                    return new FakeDockPanel();
                });

                var menu = new MenuItem();
                manager.ManageMenu(menu);
                MenuItem dropped = (MenuItem)menu.Items[0];

                // A new panel type rebuilds the menu
                manager.RegisterDisplayFactory<OtherFakeDockPanel>();
                Assert.IsFalse(menu.Items.Contains(dropped));

                dropped.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.AreEqual(0, built, "A menu item the rebuild dropped still adds panels");

                // The item that replaced it does
                MenuItem current = menu.Items.OfType<MenuItem>().Single(i => (Type)i.Tag == typeof(FakeDockPanel));
                current.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.AreEqual(1, built);

                manager.Dispose();
            });
        }

        [TestMethod]
        public void WpfDockMenu_Dispose_LetsGoOfTheManagerWhileItsMenuLives ()
        {
            StaTestThread.Run(() =>
            {
                var menu = new MenuItem();
                WeakReference manager = ManageThenDispose(menu);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                Assert.IsFalse(manager.IsAlive, "The managed menu's items keep a disposed manager alive");
                GC.KeepAlive(menu);
            });
        }

        // ===========[ Helpers ]===============================================
        // Builds a manager that manages menu, rebuilds the menu once, and disposes the manager, handing back only a weak
        // reference to it. Not inlined, so no local of this method outlives the call.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference ManageThenDispose (MenuItem menu)
        {
            DockingManager manager = MakeManager();
            manager.RegisterDisplayFactory<FakeDockPanel>();
            manager.ManageMenu(menu);
            manager.RegisterDisplayFactory<OtherFakeDockPanel>();
            manager.Dispose();
            return new WeakReference(manager);
        }

        private static DockingManager MakeManager ()
        {
            string layoutPath = Path.Combine(Path.GetTempPath(), "ajut-dock-tests", Guid.NewGuid().ToString("N") + ".layout");
            return new DockingManager(new Window(), "wpf-dock-menu-tests", layoutPath);
        }
    }
}
