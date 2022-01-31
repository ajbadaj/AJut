namespace AJutShowRoom.DockTest
{
    using System.Windows;
    using System.Windows.Controls;
    using AJut.TypeManagement;
    using AJut.UX.Docking;
    using DPUtils = AJut.UX.DPUtils<DockTestTwo>;

    [TypeId("AJutShowRoom.DockTest.DockTestTwo")]
    public partial class DockTestTwo : UserControl, IDockableDisplayElement
    {
        public DockTestTwo ()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty SaveDataProperty = DPUtils.Register(_ => _.SaveData);
        public double SaveData
        {
            get => (double)this.GetValue(SaveDataProperty);
            set => this.SetValue(SaveDataProperty, value);
        }

        public DockingContentAdapterModel DockingAdapter { get; private set; }
        void IDockableDisplayElement.Setup (DockingContentAdapterModel adapter)
        {
            this.DockingAdapter = adapter;
            adapter.TitleContent = "Two Has A Long Name";
            adapter.TooltipContent = "A two control";
            adapter.CanClose += this.OnCanClose;
            adapter.Closed += this.OnClosed;
        }

        private void OnCanClose (object sender, IsReadyToCloseEventArgs e)
        {
            var result = MessageBox.Show("Can close?", "Can Close?", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No)
            {
                e.IsReadyToClose = false;
            }
        }

        object IDockableDisplayElement.GenerateState () => this.SaveData;// new Data { Value = this.SaveData };
        void IDockableDisplayElement.ApplyState (object state)
        {
            if (state is double d)
            {
                this.SaveData = d;
            }
        }

        private void OnClosed (object sender, ClosedEventArgs e)
        {
            if (!e.IsForForcedClose)
            {
                MessageBox.Show("Closed a DockTestTwo panel");
            }
        }
    }
}
