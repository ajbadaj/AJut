namespace AJutShowRoomWinUI.UI.Controls
{
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The stock ToggleStrip demos: single, multi, allow-none, uniform sizing, the two overflow behaviors, and a
    /// custom corner radius.
    /// </summary>
    public sealed partial class ToggleStripPage : Page
    {
        public ToggleStripPage ()
        {
            this.InitializeComponent();

            // ToggleStrip demo items
            this.ToggleStripSingle.ItemsSource = new[] { "Option A", "Option B", "Option C" };
            this.ToggleStripMulti.ItemsSource = new[] { "Bold", "Italic", "Underline", "Strikethrough" };
            this.ToggleStripFreeform.ItemsSource = new[] { "First", "Second", "Third" };
            this.ToggleStripCustom.ItemsSource = new[] { "Red", "Green", "Blue" };
            this.ToggleStripUniform.ItemsSource = new[] { "S", "Medium", "Extra Large" };
            this.ToggleStripOverflow.ItemsSource = new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven" };
            this.ToggleStripScroll.ItemsSource = new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven" };
        }

        public string DemoCode { get; } = @"<ajut:ToggleStrip x:Name=""ToggleStripSingle""/>
<ajut:ToggleStrip x:Name=""ToggleStripMulti"" AllowMultiSelect=""True""/>
<ajut:ToggleStrip x:Name=""ToggleStripFreeform"" AllowNoSelection=""True""/>
<ajut:ToggleStrip x:Name=""ToggleStripUniform"" EnsureUniformSize=""True""/>
<ajut:ToggleStrip x:Name=""ToggleStripOverflow"" Width=""220"" OverflowBehavior=""OverflowPopup""/>
<ajut:ToggleStrip x:Name=""ToggleStripScroll"" Width=""220"" OverflowBehavior=""Scroll""/>
<ajut:ToggleStrip x:Name=""ToggleStripCustom"" CornerRadius=""15""/>

// Code-behind: the strips take any IEnumerable as ItemsSource
this.ToggleStripSingle.ItemsSource = new[] { ""Option A"", ""Option B"", ""Option C"" };
this.ToggleStripMulti.ItemsSource = new[] { ""Bold"", ""Italic"", ""Underline"", ""Strikethrough"" };";
    }
}
