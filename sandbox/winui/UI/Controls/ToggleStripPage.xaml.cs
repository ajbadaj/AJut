namespace AJutShowRoomWinUI.UI.Controls
{
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The stock ToggleStrip demos: single, multi, allow-none, uniform sizing, the two overflow behaviors, a
    /// custom corner radius, and the same again laid out vertically.
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
            this.ToggleStripDisabled.ItemsSource = new[] { "Selected", "Not selected", "Neither" };
            this.ToggleStripDisabledScroll.ItemsSource = new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven" };

            // Vertical demo items: the uniform one mixes line counts so the even heights show
            this.ToggleStripVertical.ItemsSource = new[] { "Top", "Middle", "Bottom" };
            this.ToggleStripVerticalUniform.ItemsSource = new[] { "One line", "Two\nlines", "Three" };
            this.ToggleStripVerticalClip.ItemsSource = new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven" };
            this.ToggleStripVerticalOverflow.ItemsSource = new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven" };
            this.ToggleStripVerticalScroll.ItemsSource = new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven" };
            this.ToggleStripVerticalSwitched.ItemsSource = new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven" };
        }

        public string DemoCode { get; } = @"<ajut:ToggleStrip x:Name=""ToggleStripSingle""/>
<ajut:ToggleStrip x:Name=""ToggleStripMulti"" AllowMultiSelect=""True""/>
<ajut:ToggleStrip x:Name=""ToggleStripFreeform"" AllowNoSelection=""True""/>
<ajut:ToggleStrip x:Name=""ToggleStripUniform"" EnsureUniformSize=""True""/>
<ajut:ToggleStrip x:Name=""ToggleStripOverflow"" Width=""220"" OverflowBehavior=""OverflowPopup""/>
<ajut:ToggleStrip x:Name=""ToggleStripScroll"" Width=""220"" OverflowBehavior=""Scroll""/>
<ajut:ToggleStrip x:Name=""ToggleStripCustom"" CornerRadius=""15""/>

<!-- Vertical: overflow and scrolling work along the height, so the constrained strips get a Height -->
<ajut:ToggleStrip x:Name=""ToggleStripVertical"" Orientation=""Vertical""/>
<ajut:ToggleStrip x:Name=""ToggleStripVerticalUniform"" Orientation=""Vertical"" EnsureUniformSize=""True""/>
<ajut:ToggleStrip x:Name=""ToggleStripVerticalClip"" Orientation=""Vertical"" Height=""120""/>
<ajut:ToggleStrip x:Name=""ToggleStripVerticalOverflow"" Orientation=""Vertical"" Height=""120"" OverflowBehavior=""OverflowPopup""/>
<ajut:ToggleStrip x:Name=""ToggleStripVerticalScroll"" Orientation=""Vertical"" Height=""120"" OverflowBehavior=""Scroll""/>

// Code-behind: the strips take any IEnumerable as ItemsSource
this.ToggleStripSingle.ItemsSource = new[] { ""Option A"", ""Option B"", ""Option C"" };
this.ToggleStripMulti.ItemsSource = new[] { ""Bold"", ""Italic"", ""Underline"", ""Strikethrough"" };
this.ToggleStripVerticalUniform.ItemsSource = new[] { ""One line"", ""Two\nlines"", ""Three"" };";
    }
}
