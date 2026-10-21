namespace AJutShowRoomWinUI.UI.Controls
{
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The NumericEditor demos: integer, float, double, clamped, stepped, and custom label content.
    /// </summary>
    public sealed partial class NumericEditorPage : Page
    {
        public NumericEditorPage ()
        {
            this.InitializeComponent();
        }

        public string DemoCode { get; } = @"<ajut:NumericEditor Value=""42""/>
<ajut:NumericEditor Value=""3.14""/>
<ajut:NumericEditor Value=""50"" Minimum=""0"" Maximum=""100""/>
<ajut:NumericEditor Value=""1.5"" Nudge=""0.5""/>

<!-- Anything can sit in front of the value -->
<ajut:NumericEditor Value=""1.5"" Nudge=""0.5"">
    <ajut:NumericEditor.LabelContent>
        <TextBlock Text=""X"" Foreground=""Red"" FontWeight=""Bold"" Margin=""5,0"" VerticalAlignment=""Center"" FontSize=""20""/>
    </ajut:NumericEditor.LabelContent>
</ajut:NumericEditor>";
    }
}
