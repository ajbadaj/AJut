namespace TheAJutShowRoom.UI.Pages
{
    using System.Windows.Controls;
    using AJut.UX;

    public partial class ControlsOverviewPage : UserControl, IStackNavDisplayControl
    {
        private StackNavAdapter? m_adapter;
        public ControlsOverviewPage()
        {
            this.InitializeComponent();
        }

        public void Setup (StackNavAdapter adapter)
        {
            m_adapter = adapter;
            m_adapter.Title = "ajut.ux.controls";
        }

    }
}
