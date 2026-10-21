namespace AJutShowRoomWinUI.UI.Controls
{
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The GridSplitter demo: a vertical split between two panes, and a horizontal split inside the right one.
    /// </summary>
    public sealed partial class GridSplitterPage : Page
    {
        public GridSplitterPage ()
        {
            this.InitializeComponent();
        }

        public string DemoCode { get; } = @"<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition />
        <ColumnDefinition />
    </Grid.ColumnDefinitions>
    <Border Margin=""0,0,3,0"">
        <TextBlock TextWrapping=""WrapWholeWords"" Text=""Left pane""/>
    </Border>

    <!-- Sits in column 0, hugging its right edge, and splits columns 0 and 1 -->
    <ajut:GridSplitter HorizontalAlignment=""Right"" VerticalAlignment=""Stretch"" Margin=""1""/>

    <Grid Grid.Column=""1"" Margin=""3,0"">
        <Grid.RowDefinitions>
            <RowDefinition />
            <RowDefinition />
        </Grid.RowDefinitions>
        <TextBlock Text=""Right pane, top""/>

        <!-- Sits in row 0, hugging its bottom edge, and splits rows 0 and 1 -->
        <ajut:GridSplitter Thickness=""5"" HorizontalAlignment=""Stretch"" VerticalAlignment=""Bottom""/>

        <TextBlock Grid.Row=""1"" Text=""Right pane, bottom""/>
    </Grid>
</Grid>";
    }
}
