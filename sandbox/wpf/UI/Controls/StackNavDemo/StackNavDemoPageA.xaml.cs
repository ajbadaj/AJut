namespace TheAJutShowRoom.UI.Controls.StackNavDemo
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<StackNavDemoPageA>;

    /// <summary>
    /// The first page of the nested StackNav demo. Shows title, drawer, busy wait, and state handoff across a push and pop.
    /// </summary>
    public partial class StackNavDemoPageA : UserControl, IStackNavDisplayControl
    {
        private StackNavAdapter? m_adapter;

        public StackNavDemoPageA ()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty ClickCountProperty = DPUtils.Register(_ => _.ClickCount);
        public int ClickCount
        {
            get => (int)this.GetValue(ClickCountProperty);
            set => this.SetValue(ClickCountProperty, value);
        }

        public void Setup (StackNavAdapter adapter)
        {
            m_adapter = adapter;
            m_adapter.Title = "Page A";
            m_adapter.DrawerHeading = "Page A drawer";
            m_adapter.DrawerDisplay = new StackNavDemoDrawer(adapter);
        }

        // The navigator throws the control away when it is covered and rebuilds it on pop, so anything
        // worth keeping goes out through GenerateState and comes back in through SetState.
        void IStackNavDisplayControl.SetState (object state)
        {
            if (state is PageAState pageState)
            {
                this.ClickCount = pageState.ClickCount;
            }
        }

        object IStackNavDisplayControl.GenerateState () => new PageAState { ClickCount = this.ClickCount };

        private void ClickMe_OnClick (object sender, RoutedEventArgs e)
        {
            ++this.ClickCount;
        }

        private void PushPageB_OnClick (object sender, RoutedEventArgs e)
        {
            m_adapter?.Navigator.GenerateAndPushDisplay<StackNavDemoPageB>();
        }

        private async void BusyWait_OnClick (object sender, RoutedEventArgs e)
        {
            if (m_adapter == null)
            {
                return;
            }

            // The busy wait overlay stays up for as long as the tracker lives
            using (m_adapter.GenerateBusyWait("Pretending to work..."))
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        private class PageAState
        {
            public int ClickCount { get; set; }
        }
    }
}
