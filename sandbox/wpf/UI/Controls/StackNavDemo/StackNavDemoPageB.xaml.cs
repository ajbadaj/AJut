namespace TheAJutShowRoom.UI.Controls.StackNavDemo
{
    using System.Threading.Tasks;
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
            this.LastPopoverResult = DescribeResult(result);
        }

        private async void ShowTwoPopovers_OnClick (object sender, RoutedEventArgs e)
        {
            if (m_adapter == null)
            {
                return;
            }

            // The second is asked for while the first is showing, so it waits its turn and shows once the first is answered
            Task<Result<string>> first = m_adapter.ShowPopover(MessageBoxPopover.Generate("The first of two popovers. The second one shows once this one is answered.", "Nice", "Ok"));
            Task<Result<string>> second = m_adapter.ShowPopover(MessageBoxPopover.Generate("The second popover, which waited for the first.", "Nice", "Ok"));
            Result<string> firstResult = await first;
            Result<string> secondResult = await second;
            this.LastPopoverResult = $"{DescribeResult(firstResult)}, then {DescribeResult(secondResult)}";
        }

        private async void PopBack_OnClick (object sender, RoutedEventArgs e)
        {
            if (m_adapter == null)
            {
                return;
            }

            await m_adapter.Navigator.PopDisplay();
        }

        private static string DescribeResult (Result<string> result) => result ? result.Value : "(canceled)";
    }
}
