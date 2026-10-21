namespace TheAJutShowRoom.UI.Controls.UxUtils
{
    using System.Windows;
    using System.Windows.Controls;

    public partial class ConvertersExample : UserControl
    {
        public ConvertersExample ()
        {
            this.InitializeComponent();
        }

        public string BooleanToVisibilityCode { get; } =
@"<CheckBox x:Name=""ShowMessage"" Content=""Show the message"" IsChecked=""True""/>
<TextBlock Text=""Here is the message, shown while the box is checked.""
           Visibility=""{Binding ElementName=ShowMessage, Path=IsChecked, Converter={ajut_conv:BooleanToVisibilityConverter TrueValue=Visible, FalseValue=Collapsed}}""/>";

        public string BooleanToValueCode { get; } =
@"<CheckBox x:Name=""IsExisting"" Content=""Editing an existing record""/>
<Button Content=""{Binding ElementName=IsExisting, Path=IsChecked, Converter={ajut_conv:BooleanToValueConverter TrueValue='Apply changes', FalseValue='Add record'}}""/>";

        public string BooleanInverseCode { get; } =
@"<CheckBox x:Name=""IsLocked"" Content=""Lock the text box""/>
<TextBox Text=""Editable until locked""
         IsEnabled=""{Binding ElementName=IsLocked, Path=IsChecked, Converter={ajut_conv:BooleanInverseConverter}}""/>";

        public string NumericComparisonCode { get; } =
@"<ajut:NumericEditor x:Name=""ComparisonSource"" Value=""5""/>
<TextBlock Text=""The value is over 10.""
           Visibility=""{Binding ElementName=ComparisonSource, Path=Value, Converter={ajut_conv:NumericComparisonToVisibilityConverter Comp=GreaterThan, To=10}}""/>";

        public string ColorStringToBrushCode { get; } =
@"<TextBox x:Name=""BrushText"" Text=""linear-gradient(90deg, #A22, #00F)""/>
<Border Width=""200"" Height=""40""
        Background=""{Binding ElementName=BrushText, Path=Text, Converter={ajut_conv:ColorStringToBrushConverter}}""/>";

        public string NullnessCode { get; } =
@"<ComboBox x:Name=""NullnessSource"" .../>
<TextBlock Text=""Nothing is selected.""
           Visibility=""{Binding ElementName=NullnessSource, Path=SelectedItem, Converter={ajut_conv:NullnessToVisibilityConverter WhenNull=Visible, WhenNotNull=Collapsed}}""/>";

        public string FixedArithmeticCode { get; } =
@"<Slider x:Name=""FontSizeSource"" Minimum=""6"" Maximum=""20"" Value=""10""/>
<TextBlock Text=""Twice the slider, as a font size""
           FontSize=""{Binding ElementName=FontSizeSource, Path=Value, Converter={ajut_conv:FixedArithmeticConverter Operation=Multiply, FixedOperand=2}}""/>";

        public string ArithmeticMultiBindingCode { get; } =
@"<TextBlock>
    <TextBlock.Text>
        <ajut_conv:ArithmeticMultiBinding Operation=""Add"">
            <Binding ElementName=""AddendA"" Path=""Value""/>
            <Binding ElementName=""AddendB"" Path=""Value""/>
        </ajut_conv:ArithmeticMultiBinding>
    </TextBlock.Text>
</TextBlock>";

        public string ThicknessFromNumberCode { get; } =
@"<Slider x:Name=""IndentSource"" Minimum=""0"" Maximum=""60"" Value=""20""/>
<Border Background=""#4080FF"" Height=""20"" Width=""200""
        Margin=""{Binding ElementName=IndentSource, Path=Value, Converter={ajut_conv:ThicknessFromNumberConverter MultiplyMask='1,0.25,0,0.25'}}""/>";

        public string FriendlyTextCode { get; } =
@"<TextBox x:Name=""FriendlyTextSource"" Text=""UseSameAsOS""/>
<TextBlock Text=""{Binding ElementName=FriendlyTextSource, Path=Text, Converter={ajut_conv:FriendlyTextEnConverter}}""/>";

        private void ClearNullnessSelection_OnClick (object sender, RoutedEventArgs e)
        {
            this.NullnessSource.SelectedItem = null;
        }
    }
}
