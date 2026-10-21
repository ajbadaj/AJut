namespace AJutShowRoomWinUI.UI.ContentFlows
{
    using System;
    using AJut.UX.Docking;
    using AJutShowRoomWinUI.UI.Samples;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The docking framework demo: a DockingManager rooted to the main window, with registered panel types, a
    /// toolbar, a managed View menu, and layout save and load.
    /// </summary>
    public sealed partial class DockingFrameworkPage : Page
    {
        private DockingManager m_dockingManager;
        private bool m_dockZoneInitialized = false;

        // ===========[ Setup/Construction/Teardown ]=====================================

        public DockingFrameworkPage ()
        {
            this.InitializeComponent();
        }

        private void Page_OnLoaded (object sender, RoutedEventArgs e)
        {
            if (m_dockZoneInitialized)
            {
                return;
            }

            m_dockZoneInitialized = true;

            m_dockingManager = new DockingManager(App.Instance.MainWindow, "ShowRoomDock");
            m_dockingManager.FixedTearoffWindowTitle = "Show Room";
            m_dockingManager.RegisterMainWindowRootDockZones(this.TestDockZone);
            m_dockingManager.RegisterDisplayFactory<ShowRoomPanel>();
            m_dockingManager.RegisterDisplayFactory<SingleInstanceShowRoomPanel>(new DockPanelRegistrationRules {
                SingleInstanceOnly = true,
                GuaranteedOnStart = true,
                SpawnWidth = 200,
                SpawnHeight = 500,
            });
            m_dockingManager.RegisterDisplayFactory<HiddenInfraShowRoomPanel>(new DockPanelRegistrationRules {
                SingleInstanceOnly = true,
                IsHiddenFromToolbar = true,
                IsHiddenFromMenu = true,
            });

            m_dockingManager.UISyncVM.RegisterPanelDisplayOverride<SingleInstanceShowRoomPanel>("Single Inst", "Assets/PenguinExample.png");
            this.DockToolbar.DockingManager = m_dockingManager;
            m_dockingManager.ManageMenu(this.ViewMenu);

            var zone = m_dockingManager.FindFirstAvailableDockZone();
            if (zone != null)
            {
                var panelA = m_dockingManager.DockNewPanel<ShowRoomPanel>(zone);
                panelA.Label = "Panel A";
                panelA.DockingAdapter.TitleContent = "Panel A";

                var panelB = m_dockingManager.DockNewPanel<ShowRoomPanel>(zone);
                panelB.Label = "Panel B";
                panelB.DockingAdapter.TitleContent = "Panel B";

                // Hidden infra panel - always present in layout, never in menu/toolbar
                m_dockingManager.DockNewPanel<HiddenInfraShowRoomPanel>(zone);
            }
        }

        // The page is dropped when navigated away from and rebuilt on return, so the manager goes with it:
        // Dispose tears down the zone tree and unhooks the root window, and clearing the flag lets the
        // next Loaded build a fresh one.
        private void Page_OnUnloaded (object sender, RoutedEventArgs e)
        {
            if (!m_dockZoneInitialized)
            {
                return;
            }

            m_dockZoneInitialized = false;
            this.DockToolbar.DockingManager = null;
            m_dockingManager?.Dispose();
            m_dockingManager = null;
        }

        // ===========[ Properties ]======================================================

        /// <summary>The dock zone markup and the manager setup behind it, shown beside the demo.</summary>
        public string DemoCode { get; } = @"<ajut:DockPanelAddRemoveToolbar x:Name=""DockToolbar""/>
<ajut:DockZone x:Name=""TestDockZone""/>

// Code-behind, in Loaded; Dispose the manager in Unloaded
m_dockingManager = new DockingManager(hostWindow, ""ShowRoomDock"");
m_dockingManager.RegisterMainWindowRootDockZones(this.TestDockZone);
m_dockingManager.RegisterDisplayFactory<ShowRoomPanel>();
m_dockingManager.RegisterDisplayFactory<SingleInstanceShowRoomPanel>(new DockPanelRegistrationRules {
    SingleInstanceOnly = true,
    GuaranteedOnStart = true,
});
this.DockToolbar.DockingManager = m_dockingManager;
m_dockingManager.ManageMenu(this.ViewMenu);

var zone = m_dockingManager.FindFirstAvailableDockZone();
var panel = m_dockingManager.DockNewPanel<ShowRoomPanel>(zone);
panel.DockingAdapter.TitleContent = ""Panel A"";";

        // ===========[ Button handlers ]=================================================

        private void DockZone_OnSaveLayoutClicked(object sender, RoutedEventArgs e)
        {
            m_dockingManager?.SaveDockLayoutToPersistentStorage();
        }

        private void DockZone_OnLoadLayoutClicked(object sender, RoutedEventArgs e)
        {
            m_dockingManager?.ReloadDockLayoutFromPersistentStorage();
        }

        private void DockZone_OnSetAllValuesTestClicked(object sender, RoutedEventArgs e)
        {
            Random rng = new Random(DateTime.Now.Second);
            double newValue = rng.NextDouble();
            foreach (var display in m_dockingManager.EnumerateDisplays())
            {
                if (display is ShowRoomPanel panel)
                {
                    panel.Value = newValue;
                }
            }
        }
    }
}
