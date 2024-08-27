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
            this.Docking.RegisterDisplayFactory(() => new DockExampleControls.CommandSender());
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

        private async void ShowPopup_OnExecuted (object sender, ExecutedRoutedEventArgs e)
        {
            if (this.PageNav != null)
            {
                await this.PageNav.ShowPopover(MessageBoxPopover.Generate("This popup was triggered by the command from the button in the 'Command Route Example' docked element"));
            }
        }
    }
}
