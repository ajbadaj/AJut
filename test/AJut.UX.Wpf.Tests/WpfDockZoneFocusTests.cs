namespace AJut.UX.Tests
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Input;
    using AJut.UX.Controls;
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// How a WPF dock panel comes to hold focus. Its header shows it is the panel being typed into while keyboard focus is
    /// within it, so a press anywhere in the panel has to put focus there. Nothing is shown, so focus lands as logical focus
    /// in the window's focus scope, which is where keyboard focus goes once the window is active.
    /// </summary>
    [TestClass]
    public class WpfDockZoneFocusTests
    {
        [TestMethod]
        public void WpfDockZone_PressInsideAPanel_GivesThatPanelFocus ()
        {
            StaTestThread.Run(() =>
            {
                var window = new Window();
                var zone = new DockZone();
                window.Content = zone;
                DockingManager manager = MakeManager(window);
                manager.RegisterDisplayFactory<FakeDockPanel>();
                manager.RegisterRootDockZones(zone);
                manager.DockNewPanel<FakeDockPanel>(zone.ViewModel);

                zone.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent });

                Assert.AreSame(zone, FocusManager.GetFocusedElement(window), "A press inside a dock panel leaves focus where it was, so the panel never shows it is the one being typed into");
                manager.Dispose();
            });
        }

        // ===========[ Helpers ]===============================================
        private static DockingManager MakeManager (Window window)
        {
            string layoutPath = Path.Combine(Path.GetTempPath(), "ajut-dock-tests", Guid.NewGuid().ToString("N") + ".layout");
            return new DockingManager(window, "wpf-dock-focus-tests", layoutPath);
        }
    }
}
