namespace TheAJutShowRoom.UI.Pages
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX;
    using AJut.UX.Docking;
    using TheAJutShowRoom.UI.PageDrawers;
    using DPUtils = AJut.UX.DPUtils<DockingFrameworkOverviewPage>;

    public partial class DockingFrameworkOverviewPage : UserControl, IStackNavDisplayControl, IDisposable
    {
        public DockingFrameworkOverviewPage ()
        {
            this.InitializeComponent();

            this.Docking = new DockingManager(App.Current.MainWindow, "ajut-showroom-docking-overview", autoSaveMethod: this.DockSaveMethod);
            this.Docking.RegisterRootDockZones(this.RootZone);

            this.Docking.RegisterDisplayFactory<DockExampleControls.ColorController>();
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
            this.Docking.CloseAll(force:true);
        }

        public static readonly DependencyProperty DockSaveMethodProperty = DPUtils.Register(_ => _.DockSaveMethod, eDockingAutoSaveMethod.AutoSaveOnAllChanges);
        public eDockingAutoSaveMethod DockSaveMethod
        {
            get => (eDockingAutoSaveMethod)this.GetValue(DockSaveMethodProperty);
            set => this.SetValue(DockSaveMethodProperty, value);
        }

        public DockingManager Docking { get; }
        public StackNavAdapter? PageNav { get; private set; }
    }
}
