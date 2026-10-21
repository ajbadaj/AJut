namespace TheAJutShowRoom.UI.Controls.StackNavDemo
{
    using System.Windows;
    using System.Windows.Controls;
    using AJut.Storage;
    using AJut.UX;
    using AJut.UX.Controls;
    using DPUtils = AJut.UX.DPUtils<StackNavDemoPageB>;

    /// <summary>
    /// The second page of the nested StackNav demo. Shows a popover and a programmatic pop.
    /// </summary>
    public partial class StackNavDemoPageB : UserControl, IStackNavDisplayControl
    {
        private StackNavAdapter? m_adapter;

        public StackNavDemoPageB ()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty LastPopoverResultProperty = DPUtils.Register(_ => _.LastPopoverResult, "(none yet)");
        public string LastPopoverResult
        {
            get => (string)this.GetValue(LastPopoverResultProperty);
            set => this.SetValue(LastPopoverResultProperty, value);
        }

        public void Setup (StackNavAdapter adapter)
        {
            m_adapter = adapter;
            m_adapter.Title = "Page B";
        }

        private async void ShowPopover_OnClick (object sender, RoutedEventArgs e)
        {
            if (m_adapter == null)
            {
                return;
            }

            // A popover covers only this display, and the await gives back whichever option was picked
            Result<string> result = await m_adapter.ShowPopover(MessageBoxPopover.Generate("Popovers sit over the current display and hand back a result.", "Nice", "Ok"));
            this.LastPopoverResult = result ? result.Value : "(canceled)";
        }

        private async void PopBack_OnClick (object sender, RoutedEventArgs e)
        {
            if (m_adapter == null)
            {
                return;
            }

            await m_adapter.Navigator.PopDisplay();
        }
    }
}
