namespace TheAJutShowRoom.UI.Controls.StackNavDemo
{
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX;

    /// <summary>
    /// The drawer page A hangs off its adapter in the nested StackNav demo.
    /// </summary>
    public partial class StackNavDemoDrawer : UserControl
    {
        private readonly StackNavAdapter m_adapter;

        public StackNavDemoDrawer (StackNavAdapter adapter)
        {
            m_adapter = adapter;
            this.InitializeComponent();
        }

        private void PushPageB_OnClick (object sender, RoutedEventArgs e)
        {
            m_adapter.Navigator.GenerateAndPushDisplay<StackNavDemoPageB>();
        }

        private void CloseDrawer_OnClick (object sender, RoutedEventArgs e)
        {
            m_adapter.Navigator.CloseDrawer();
        }
    }
}
