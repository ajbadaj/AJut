namespace TheAJutShowRoom.UI.Pages
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Docking;
    using TheAJutShowRoom.UI.PageDrawers;
    using DPUtils = AJut.UX.DPUtils<DockingFrameworkOverviewPage>;

    public partial class DockingFrameworkOverviewPage : UserControl, IStackNavDisplayControl, IDisposable
    {
        public static RoutedUICommand ShowPopupCommand = new RoutedUICommand("Show Popup", nameof(ShowPopupCommand), typeof(DockingFrameworkOverviewPage));

        public DockingFrameworkOverviewPage ()
        {
            this.InitializeComponent();

            this.Docking = new DockingManager(App.Current.MainWindow, "ajut-showroom-docking-overview", autoSaveMethod: this.DockSaveMethod);
            this.Docking.AddCommandSource(this);
            this.Docking.AddCommandSource(Window.GetWindow(this));
            this.Docking.RegisterRootDockZones(this.RootZone);

            this.Docking.RegisterDisplayFactory(()=>new DockExampleControls.ColorController(this));
            this.Docking.RegisterDisplayFactory(new() { SingleInstanceOnly = true }, () => new DockExampleControls.CommandSender());
            this.DockToolbar.DockingManager = this.Docking;
            this.Docking.ManageMenu(this.PanelsMenu);
        }

        public void Setup (StackNavAdapter adapter)
        {
            this.PageNav = adapter;
            this.PageNav.Title = "Docking Framework";
            this.PageNav.DrawerDisplay = new DockingOverviewDrawer(this.Docking);
            this.PageNav.Closing += this.OnClosing;
        }

        private void OnClosing (object? sender, StackNavAttemptingDisplayCloseEventArgs e)
        {
            if (!this.Docking.CloseAll())
            {
                e.CanClose = false;
            }
        }

        public void Dispose ()
        {
            // DockingManager.Dispose calls CloseAll(force:true) internally and then tears
            // down its own subs (root window, window manager, menu, sizing). Use it instead
            // of the bare CloseAll - leaving subs in place is what causes the editor page
            // and the manager to stick around past navigation.
            this.Docking.Dispose();
        }

        // ===========[ Leak Probe ]==================================================
        // Spot-check buttons. Each one builds a fresh manager (or this whole page) against
        // the live root window, disposes it, drops the strong ref, runs GC, and reports
        // whether the WeakReference is dead. PASS = collected. FAIL = pinned by a stray sub.

        private void LeakProbe_OnDockingManagerClicked (object sender, RoutedEventArgs e)
        {
            this.LeakProbeDockingManagerStatus.Text = "running...";
            bool collected = LeakProbe_BuildAndDisposeDockingManager(App.Current.MainWindow);
            this.LeakProbeDockingManagerStatus.Text = collected
                ? "PASS - DockingManager was collected after Dispose"
                : "FAIL - DockingManager survived Dispose + GC (something is still pinning it)";
        }

        private void LeakProbe_OnWindowManagerClicked (object sender, RoutedEventArgs e)
        {
            this.LeakProbeWindowManagerStatus.Text = "running...";
            bool collected = LeakProbe_BuildAndDisposeWindowManager(App.Current.MainWindow);
            this.LeakProbeWindowManagerStatus.Text = collected
                ? "PASS - WindowManager was collected after Dispose"
                : "FAIL - WindowManager survived Dispose + GC (something is still pinning it)";
        }

        private void LeakProbe_OnCycleClicked (object sender, RoutedEventArgs e)
        {
            this.LeakProbeCycleStatus.Text = "running...";
            int passes = 0;
            for (int i = 0; i < 5; ++i)
            {
                if (LeakProbe_BuildAndDisposeDockingManager(App.Current.MainWindow))
                {
                    ++passes;
                }
            }

            this.LeakProbeCycleStatus.Text = passes == 5
                ? "PASS - all 5 cycles collected cleanly"
                : $"FAIL - only {passes}/5 cycles collected";
        }

        // Static so the local `manager` variable is the only place a strong ref lives.
        private static bool LeakProbe_BuildAndDisposeDockingManager (Window root)
        {
            WeakReference weak = LeakProbe_BuildAndDisposeDockingManager_Inner(root);
            return LeakProbe_ConfirmCollected(weak);
        }

        private static WeakReference LeakProbe_BuildAndDisposeDockingManager_Inner (Window root)
        {
            var manager = new DockingManager(root, "leak-probe-" + Guid.NewGuid().ToString("N"));
            var weak = new WeakReference(manager);
            manager.Dispose();
            return weak;
        }

        private static bool LeakProbe_BuildAndDisposeWindowManager (Window root)
        {
            WeakReference weak = LeakProbe_BuildAndDisposeWindowManager_Inner(root);
            return LeakProbe_ConfirmCollected(weak);
        }

        private static WeakReference LeakProbe_BuildAndDisposeWindowManager_Inner (Window root)
        {
            var manager = new WindowManager(root);
            var weak = new WeakReference(manager);
            manager.Dispose();
            return weak;
        }

        private static bool LeakProbe_ConfirmCollected (WeakReference weak)
        {
            for (int i = 0; i < 2; ++i)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            return !weak.IsAlive;
        }

        public static readonly DependencyProperty DockSaveMethodProperty = DPUtils.Register(_ => _.DockSaveMethod, eDockingAutoSaveMethod.AutoSaveOnAllChanges);
        public eDockingAutoSaveMethod DockSaveMethod
        {
            get => (eDockingAutoSaveMethod)this.GetValue(DockSaveMethodProperty);
            set => this.SetValue(DockSaveMethodProperty, value);
        }

        public DockingManager Docking { get; }
        public StackNavAdapter? PageNav { get; private set; }

        private async void ShowPopup_OnExecuted (object sender, ExecutedRoutedEventArgs e)
        {
            if (this.PageNav != null)
            {
                await this.PageNav.ShowPopover(MessageBoxPopover.Generate("This popup was triggered by the command from the button in the 'Command Route Example' docked element"));
            }
        }
    }
}
