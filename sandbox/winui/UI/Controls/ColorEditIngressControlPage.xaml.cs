namespace AJutShowRoomWinUI.UI.Controls
{
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The ColorEditIngressControl demo: fixed swatches and one bound to a text box through a converter.
    /// </summary>
    public sealed partial class ColorEditIngressControlPage : Page
    {
        public ColorEditIngressControlPage ()
        {
            this.InitializeComponent();
        }

        public string DemoCode { get; } = @"<ajut:ColorEditIngressControl Width=""60"" Height=""40"" EditColor=""#FF0000""/>
<ajut:ColorEditIngressControl Width=""60"" Height=""40"" EditColor=""#0000CC""/>

<!-- Two-way bound to a text box through the color string converter -->
<ajut:ColorEditIngressControl Width=""60"" Height=""40""
    EditColor=""{Binding ElementName=ColorText, Path=Text, Mode=TwoWay, Converter={ajut_conv:ColorStringToColorConverter}}""/>
<TextBox x:Name=""ColorText"" Text=""#C9DB733F""/>";
    }
}
