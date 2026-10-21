namespace TheAJutShowRoom.UI.Pages
{
    using System.Windows.Controls;
    using AJut.UX;
    using TheAJutShowRoom.UI.Controls.StackNavDemo;

    public partial class StackNavOverviewPage : UserControl, IStackNavDisplayControl
    {
        private StackNavAdapter? m_adapter;

        public StackNavOverviewPage ()
        {
            // A second controller, separate from App.Pages, so the demo stack never touches the showroom's own stack
            this.DemoNavigator = new StackNavFlowController();
            this.DemoNavigator.GenerateAndPushDisplay<StackNavDemoPageA>();
            this.InitializeComponent();
        }

        public StackNavFlowController DemoNavigator { get; }

        public void Setup (StackNavAdapter adapter)
        {
            m_adapter = adapter;
            m_adapter.Title = "Stack Nav";
        }

        public string HostingXamlCode { get; } =
@"<ajut:StackNavDisplay Navigator=""{Binding ElementName=Self, Path=DemoNavigator, Mode=OneWay}""
                      FixedDrawerWidth=""180""/>";

        public string HostingCSharpCode { get; } =
@"public partial class StackNavOverviewPage : UserControl, IStackNavDisplayControl
{
    public StackNavOverviewPage ()
    {
        // One controller per stack. The showroom window has its own (App.Pages);
        // this page owns a second one for the demo.
        this.DemoNavigator = new StackNavFlowController();
        this.DemoNavigator.GenerateAndPushDisplay<StackNavDemoPageA>();
        this.InitializeComponent();
    }

    public StackNavFlowController DemoNavigator { get; }
}";

        public string PageACode { get; } =
@"public partial class StackNavDemoPageA : UserControl, IStackNavDisplayControl
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
        m_adapter.Title = ""Page A"";
        m_adapter.DrawerHeading = ""Page A drawer"";
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
        using (m_adapter.GenerateBusyWait(""Pretending to work...""))
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    private class PageAState
    {
        public int ClickCount { get; set; }
    }
}";

        public string PageBCode { get; } =
@"public partial class StackNavDemoPageB : UserControl, IStackNavDisplayControl
{
    private StackNavAdapter? m_adapter;

    public StackNavDemoPageB ()
    {
        this.InitializeComponent();
    }

    public static readonly DependencyProperty LastPopoverResultProperty = DPUtils.Register(_ => _.LastPopoverResult, ""(none yet)"");
    public string LastPopoverResult
    {
        get => (string)this.GetValue(LastPopoverResultProperty);
        set => this.SetValue(LastPopoverResultProperty, value);
    }

    public void Setup (StackNavAdapter adapter)
    {
        m_adapter = adapter;
        m_adapter.Title = ""Page B"";
    }

    private async void ShowPopover_OnClick (object sender, RoutedEventArgs e)
    {
        if (m_adapter == null)
        {
            return;
        }

        // A popover covers only this display, and the await gives back whichever option was picked
        Result<string> result = await m_adapter.ShowPopover(MessageBoxPopover.Generate(""Popovers sit over the current display and hand back a result."", ""Nice"", ""Ok""));
        this.LastPopoverResult = result ? result.Value : ""(canceled)"";
    }

    private async void PopBack_OnClick (object sender, RoutedEventArgs e)
    {
        if (m_adapter == null)
        {
            return;
        }

        await m_adapter.Navigator.PopDisplay();
    }
}";

        public string DrawerCode { get; } =
@"public partial class StackNavDemoDrawer : UserControl
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
}";
    }
}
