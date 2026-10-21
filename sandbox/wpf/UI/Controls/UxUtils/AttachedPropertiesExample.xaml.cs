namespace TheAJutShowRoom.UI.Controls.UxUtils
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using DPUtils = AJut.UX.DPUtils<AttachedPropertiesExample>;

    public partial class AttachedPropertiesExample : UserControl
    {
        private int m_leftClicks;
        private int m_rightClicks;

        public AttachedPropertiesExample ()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty ClickReportProperty = DPUtils.Register(_ => _.ClickReport, "No clicks yet");
        public string ClickReport
        {
            get => (string)this.GetValue(ClickReportProperty);
            set => this.SetValue(ClickReportProperty, value);
        }

        public string CornerRadiusCode { get; } =
@"<Button Content=""Radius 16"" ajut_ap:BorderXTA.CornerRadius=""16""/>
<Button Content=""Top only"" ajut_ap:BorderXTA.CornerRadius=""12,12,0,0""/>
<ajut:ToggleStrip ajut_ap:BorderXTA.CornerRadius=""10"" .../>";

        public string ClickableCode { get; } =
@"<Border ajut_ap:ClickableXTA.IsTrackingClick=""True"" ajut_ap:ClickableXTA.Click=""LeftClickBorder_OnClick"">
    <Border.Style>
        <Style TargetType=""{x:Type Border}"">
            <Setter Property=""Background"" Value=""#3040A0FF""/>
            <Style.Triggers>
                <DataTrigger Binding=""{Binding RelativeSource={RelativeSource Self}, Path=(ajut_ap:ClickableXTA.IsPressed)}"" Value=""True"">
                    <Setter Property=""Background"" Value=""#A040A0FF""/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </Border.Style>
    <TextBlock Text=""Left click this border""/>
</Border>

<Border ajut_ap:ClickableXTA.IsTrackingClick=""True""
        ajut_ap:ClickableXTA.AcceptsMousePrimaryButton=""False""
        ajut_ap:ClickableXTA.AcceptsMouseSecondaryButton=""True""
        ajut_ap:ClickableXTA.Click=""RightClickBorder_OnClick"">
    <TextBlock Text=""Right click this one""/>
</Border>

// Code behind: the handler is a plain MouseButtonEventHandler
private void LeftClickBorder_OnClick (object sender, MouseButtonEventArgs e) { ... }";

        public string ScrollWatchCode { get; } =
@"<ScrollViewer x:Name=""WatchedScroller"" ajut_ap:ScrollXTA.IsScrollWatchEnabled=""True"">
    ...
</ScrollViewer>
<CheckBox Content=""Can scroll down"" IsChecked=""{Binding ElementName=WatchedScroller, Path=(ajut_ap:ScrollXTA.CanScrollDown), Mode=OneWay}""/>";

        public string HeaderedContentCode { get; } =
@"<GroupBox Header=""Big header on the right""
          ajut_ap:HeaderedContentControlXTA.HeaderFontSize=""22""
          ajut_ap:HeaderedContentControlXTA.HorizontalHeaderAlignment=""Right""
          ajut_ap:HeaderedContentControlXTA.HeaderPadding=""0,0,12,-8"">
    ...
</GroupBox>";

        public string WindowCode { get; } =
@"<Window WindowStyle=""None""
        ajutap:WindowXTA.FixMaximizeAsFullscreenIssue=""True""
        ajutap:WindowXTA.MaximizedRootElementMargin=""4,3""
        ajutap:WindowXTA.FullscreenRootElementMargin=""4,2"">
    <WindowChrome.WindowChrome>
        <WindowChrome CaptionHeight=""40"" ResizeBorderThickness=""5""/>
    </WindowChrome.WindowChrome>
    ...
</Window>";

        private void LeftClickBorder_OnClick (object sender, MouseButtonEventArgs e)
        {
            ++m_leftClicks;
            this.ClickReport = $"Left border clicked {m_leftClicks} times, right border clicked {m_rightClicks} times";
        }

        private void RightClickBorder_OnClick (object sender, MouseButtonEventArgs e)
        {
            ++m_rightClicks;
            this.ClickReport = $"Left border clicked {m_leftClicks} times, right border clicked {m_rightClicks} times";
        }
    }
}
