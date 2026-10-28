namespace AJutShowRoomAppKit.UI.ContentFlows
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using AJut.TypeManagement;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using AJut.UX.Docking;
    using AJut.UX.PropertyInteraction;
    using AJut.UX.Theming;
    using AppKit;

    /// <summary>
    /// The docking framework demo, the same as the WinUI3 showroom's: a DockingManager rooted to the showroom window, with
    /// registered panel types, a toolbar, and layout save and load. Panels drag out into tearoff windows by their header
    /// or tab, dock back by dropping on the arrows, and Escape during a drag puts things back.
    /// </summary>
    public sealed class DockingFrameworkPage : ShowroomPage
    {
        private const double kDockAreaHeight = 420.0;

        private readonly BindingSet m_bindings = new BindingSet();
        private readonly DockPanelToolbarView m_toolbar = new DockPanelToolbarView();
        private readonly ThemedView m_dockArea = new ThemedView();
        private DockingManager m_dockingManager;

        // ===========[ Setup/Construction/Teardown ]===========================
        public DockingFrameworkPage ()
            : base("Docking Framework", "Panels docked in zones you can split, tab, tear off into their own windows and dock back by dragging. Drag a panel's header or tab out to tear it off, drop it on an arrow to split a zone or on the middle to add it to the tabs, and press Escape mid-drag to put it back. Save Layout and Load Layout write and read the same file format WinUI3 uses.")
        {
            var buttons = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: 8.0);
            buttons.Add(this.MakeButton("Save Layout", this.SaveLayout));
            buttons.Add(this.MakeButton("Load Layout", this.LoadLayout));
            buttons.Add(this.MakeButton("Reset Layout", this.ResetLayout));
            buttons.Add(this.MakeButton("Set All Values (test)", this.SetAllValues));
            this.AddDemo(buttons, eFillStackCrossAlignment.Start);

            this.AddDemo(m_toolbar, eFillStackCrossAlignment.Start);

            m_dockArea.TranslatesAutoresizingMaskIntoConstraints = false;
            m_dockArea.HeightAnchor.ConstraintEqualTo((nfloat)kDockAreaHeight).Active = true;
            this.AddDemo(m_dockArea);

            this.AddCode(
@"// A layout load finds each panel type, and its state type, by its [TypeId]
TypeIdRegistrar.RegisterTypeId<ShowRoomPanel>(TypeIdRegistrar.GetTypeIdFor<ShowRoomPanel>());

var docking = new DockingManager(hostWindow, ""ShowRoomDock"");
docking.FixedTearoffWindowTitle = ""Show Room"";
docking.RegisterDisplayFactory<ShowRoomPanel>();
docking.RegisterDisplayFactory<SingleInstanceShowRoomPanel>(new DockPanelRegistrationRules {
    SingleInstanceOnly = true,
    GuaranteedOnStart = true,
});

DockZoneView rootZone = docking.CreateRootDockZone();
dockArea.AddSubview(rootZone);
toolbar.DockingManager = docking;

var panel = docking.DockNewPanel<ShowRoomPanel>(docking.FindFirstAvailableDockZone());
panel.Label = ""Panel A"";

// When the page goes away
toolbar.DockingManager = null;
docking.Dispose();"
            );

            // A layout names each panel, and each panel's state, by its [TypeId], and a load finds the type by looking
            //  that id up in the TypeIdRegistrar, which only knows the ids it is given. A save reads the attribute itself,
            //  so without these a layout saves fine and then loads with no panels in it. The WinUI3 and WPF showrooms
            //  register their whole assembly at startup; one type at a time is the form trimming keeps.
            RegisterTypeIdOf<ShowRoomPanel>();
            RegisterTypeIdOf<SingleInstanceShowRoomPanel>();
            RegisterTypeIdOf<HiddenInfraShowRoomPanel>();
            RegisterTypeIdOf<ShowRoomPanelState>();
        }

        // The manager is rooted to a window, so it is built when the page first lands in one
        public override void ViewDidMoveToWindow ()
        {
            base.ViewDidMoveToWindow();
            if ((this.Window != null) && (m_dockingManager == null))
            {
                this.BuildDocking(this.Window);
            }
        }

        // Dispose tears down the zone tree, closes the tearoffs and unhooks the root window, and the
        // toolbar lets go of the manager first, so nothing of the page outlives it
        protected override void OnTeardown ()
        {
            m_bindings.Dispose();
            m_toolbar.DockingManager = null;
            m_dockingManager?.Dispose();
            m_dockingManager = null;
        }

        // ===========[ Helper Methods ]========================================
        private void BuildDocking (NSWindow hostWindow)
        {
            m_dockingManager = new DockingManager(hostWindow, "ShowRoomDock")
            {
                FixedTearoffWindowTitle = "Show Room",
            };

            m_dockingManager.RegisterDisplayFactory<ShowRoomPanel>();
            m_dockingManager.RegisterDisplayFactory<SingleInstanceShowRoomPanel>(new DockPanelRegistrationRules
            {
                SingleInstanceOnly = true,
                GuaranteedOnStart = true,
                SpawnWidth = 200,
                SpawnHeight = 500,
            });
            m_dockingManager.RegisterDisplayFactory<HiddenInfraShowRoomPanel>(new DockPanelRegistrationRules
            {
                SingleInstanceOnly = true,
                IsHiddenFromToolbar = true,
                IsHiddenFromMenu = true,
            });
            m_dockingManager.UISyncVM.RegisterPanelDisplayOverride<SingleInstanceShowRoomPanel>("Single Inst");

            DockZoneView rootZone = m_dockingManager.CreateRootDockZone();
            rootZone.TranslatesAutoresizingMaskIntoConstraints = false;
            m_dockArea.AddSubview(rootZone);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                rootZone.TopAnchor.ConstraintEqualTo(m_dockArea.TopAnchor),
                rootZone.BottomAnchor.ConstraintEqualTo(m_dockArea.BottomAnchor),
                rootZone.LeadingAnchor.ConstraintEqualTo(m_dockArea.LeadingAnchor),
                rootZone.TrailingAnchor.ConstraintEqualTo(m_dockArea.TrailingAnchor),
            });

            m_toolbar.DockingManager = m_dockingManager;

            DockZoneViewModel zone = m_dockingManager.FindFirstAvailableDockZone();
            if (zone != null)
            {
                m_dockingManager.DockNewPanel<ShowRoomPanel>(zone).Label = "Panel A";
                m_dockingManager.DockNewPanel<ShowRoomPanel>(zone).Label = "Panel B";

                // Always in the layout, never in the toolbar or menu
                m_dockingManager.DockNewPanel<HiddenInfraShowRoomPanel>(zone);
            }
        }

        private NSButton MakeButton (string title, Action action)
        {
            var button = new NSButton { Title = title, BezelStyle = NSBezelStyle.Rounded };
            m_bindings.BindCommand(button, new ActionCommand(action));
            return button;
        }

        private void SaveLayout ()
        {
            m_dockingManager?.SaveDockLayoutToPersistentStorage();
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Every panel type a layout here can name is registered with RegisterDisplayFactory<T> in BuildDocking, which is what the warning asks for.")]
        private void LoadLayout ()
        {
            m_dockingManager?.ReloadDockLayoutFromPersistentStorage();
        }

        // Back to the panels the page starts with. The manager is let go the way leaving the page lets it go (its tearoffs
        // close and its zone tree is torn down), and a new one is built in its place. The saved layout is left as it is.
        private void ResetLayout ()
        {
            if (this.Window == null)
            {
                return;
            }

            m_toolbar.DockingManager = null;
            m_dockingManager?.Dispose();
            m_dockingManager = null;
            foreach (NSView view in m_dockArea.Subviews)
            {
                view.RemoveFromSuperview();
            }

            this.BuildDocking(this.Window);
        }

        private static void RegisterTypeIdOf<T> () => TypeIdRegistrar.RegisterTypeId<T>(TypeIdRegistrar.GetTypeIdFor<T>());

        private void SetAllValues ()
        {
            if (m_dockingManager == null)
            {
                return;
            }

            double newValue = new Random().NextDouble();
            foreach (IDockableDisplayElement display in m_dockingManager.EnumerateDisplays())
            {
                if (display is ShowRoomPanel panel)
                {
                    panel.Value = newValue;
                }
            }
        }
    }
}
