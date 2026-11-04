namespace AJutShowRoomWinUI.UI.Controls
{
    using System;
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

        /// <summary>The types the Integer, Float and Double rows keep their values in</summary>
        public Type IntegerType { get; } = typeof(int);
        public Type FloatType { get; } = typeof(float);
        public Type DoubleType { get; } = typeof(double);

        public string DemoCode { get; } = @"<!-- A literal arrives as text, so EnforceNumericType says what type to keep it in -->
<ajut:NumericEditor Value=""42"" EnforceNumericType=""{x:Bind IntegerType}""/>
<ajut:NumericEditor Value=""3.14"" EnforceNumericType=""{x:Bind FloatType}""/>
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
