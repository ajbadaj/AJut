namespace TheAJutShowRoom.UI.Pages
{
    using System.Windows.Controls;
    using AJut.UX;

    public partial class UxUtilsOverviewPage : UserControl, IStackNavDisplayControl
    {
        private StackNavAdapter? m_adapter;

        public UxUtilsOverviewPage ()
        {
            this.InitializeComponent();
        }

        public void Setup (StackNavAdapter adapter)
        {
            m_adapter = adapter;
            m_adapter.Title = "UX Utilities";
        }
    }
}
