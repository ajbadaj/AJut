namespace AJutShowRoomWinUI.UI.UxUtils
{
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// Live examples of the AJut.UX.Converters markup extensions. Nearly all of the state is in the XAML, bound
    /// element to element; the one page property is the status string the switch converter reads.
    /// </summary>
    public sealed partial class ConvertersPage : Page, INotifyPropertyChanged
    {
        private string m_status = "Ready";

        public ConvertersPage ()
        {
            this.InitializeComponent();
        }

        // ===========[ Events ]=======================================================
        public event PropertyChangedEventHandler? PropertyChanged;

        // ===========[ Properties ]===================================================
        public string[] StatusOptions { get; } = new[] { "Ready", "Busy", "Failed", "Unknown" };

        /// <summary>
        /// The string the switch converter reads. It starts set so the converter never sees a null key.
        /// </summary>
        public string Status
        {
            get => m_status;
            set
            {
                if (m_status == value || value == null)
                {
                    return;
                }

                m_status = value;
                this.RaisePropertyChanged();
            }
        }

        public string BooleanCode { get; } = @"<ToggleSwitch x:Name=""FlagSwitch"" IsOn=""True""/>

<!-- Visible for true, Collapsed for false, unless you say otherwise -->
<TextBlock Text=""Shown while the flag is true""
           Visibility=""{Binding ElementName=FlagSwitch, Path=IsOn, Converter={ajut_conv:BooleanToVisibilityConverter}}""/>
<TextBlock Text=""Shown while the flag is false""
           Visibility=""{Binding ElementName=FlagSwitch, Path=IsOn, Converter={ajut_conv:BooleanToVisibilityConverter TrueValue=Collapsed, FalseValue=Visible}}""/>

<!-- Either of two objects -->
<Button Content=""{Binding ElementName=FlagSwitch, Path=IsOn, Converter={ajut_conv:BooleanToValueConverter TrueValue='Apply changes', FalseValue='Add record'}}""/>

<!-- The plain not -->
<TextBox IsEnabled=""{Binding ElementName=FlagSwitch, Path=IsOn, Converter={ajut_conv:BooleanInverseConverter}}""/>

<!-- Either of two brushes; the same shape exists for Color, Double, Thickness and CornerRadius -->
<Border Background=""{Binding ElementName=FlagSwitch, Path=IsOn, Converter={ajut_conv:BooleanToBrushConverter TrueValue=SeaGreen, FalseValue=Gray}}""/>";

        public string ComparisonCode { get; } = @"<Slider x:Name=""AmountSlider"" Minimum=""0"" Maximum=""100"" Value=""35""/>

<TextBlock Text=""The amount is over 50.""
           Visibility=""{Binding ElementName=AmountSlider, Path=Value, Converter={ajut_conv:NumericComparisonToVisibilityConverter Comp=GreaterThan, To=50}}""/>
<TextBlock Text=""The amount is 50 or under.""
           Visibility=""{Binding ElementName=AmountSlider, Path=Value, Converter={ajut_conv:NumericComparisonToVisibilityConverter Comp=LessThanOrEqual, To=50}}""/>

<!-- The bool flavor, for IsEnabled and friends -->
<Button Content=""Only enabled at exactly 100""
        IsEnabled=""{Binding ElementName=AmountSlider, Path=Value, Converter={ajut_conv:NumericComparisonToBoolConverter Comp=Equal, To=100}}""/>";

        public string ColorStringCode { get; } = @"<TextBox x:Name=""BrushText"" Text=""linear-gradient(76deg, #A22, #0000FF)""/>

<!-- Hex, a named color, linear-gradient(...) or radial-gradient(...) all parse -->
<Border Height=""48""
        Background=""{Binding ElementName=BrushText, Path=Text, Converter={ajut_conv:ColorStringToBrushConverter}}""/>";

        public string StringSwitchCode { get; } = @"<Ellipse Width=""24"" Height=""24"">
    <Ellipse.Fill>
        <Binding ElementName=""Self"" Path=""Status"">
            <Binding.Converter>
                <ajut_conv:StringToBrushSwitchConverter>
                    <ajut_conv:StringToBrushSwitchConverter.Cases>
                        <ajut_conv:BrushDictionary>
                            <SolidColorBrush x:Key=""Ready"" Color=""SeaGreen""/>
                            <SolidColorBrush x:Key=""Busy"" Color=""Goldenrod""/>
                            <SolidColorBrush x:Key=""Failed"" Color=""Firebrick""/>
                        </ajut_conv:BrushDictionary>
                    </ajut_conv:StringToBrushSwitchConverter.Cases>
                </ajut_conv:StringToBrushSwitchConverter>
            </Binding.Converter>
        </Binding>
    </Ellipse.Fill>
</Ellipse>

<!-- A string with no case keeps the last match: pick Unknown after Failed and the jewel stays red -->";

        public string ThicknessCode { get; } = @"<Slider x:Name=""InsetSlider"" Minimum=""0"" Maximum=""60"" Value=""12""/>

<!-- The mask is multiplied by the number: 1,0,1,0 insets left and right only -->
<Border Height=""32""
        Margin=""{Binding ElementName=InsetSlider, Path=Value, Converter={ajut_conv:ThicknessFromNumberConverter MultiplyMask='1,0,1,0'}}""/>";

        // ===========[ Helpers ]======================================================
        private void RaisePropertyChanged ([CallerMemberName] string propertyName = "")
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
