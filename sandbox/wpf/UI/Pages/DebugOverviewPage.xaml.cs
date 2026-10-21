namespace TheAJutShowRoom.UI.Pages
{
    using System.Windows.Controls;
    using AJut.UX;

    public partial class DebugOverviewPage : UserControl, IStackNavDisplayControl
    {
        private StackNavAdapter? m_adapter;

        public DebugOverviewPage ()
        {
            this.InitializeComponent();
        }

        public void Setup (StackNavAdapter adapter)
        {
            m_adapter = adapter;
            m_adapter.Title = "Debug";
        }
    }
}
