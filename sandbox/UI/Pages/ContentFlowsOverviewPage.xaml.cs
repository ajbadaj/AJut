namespace TheAJutShowRoom.UI.Pages
{
    using System.Windows.Controls;
    using AJut.UX;

    public partial class ContentFlowsOverviewPage : UserControl, IStackNavDisplayControl
    {
        private StackNavAdapter? m_adapter;
        public ContentFlowsOverviewPage()
        {
            this.InitializeComponent();
        }

        public void Setup (StackNavAdapter adapter)
        {
            m_adapter = adapter;
            m_adapter.Title = "Content Flows";
        }

        private void StackNav_OnClick (object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }
    }
}
