namespace AJutShowRoomWinUI.UI.Diagnostics
{
    using System;
    using System.Linq;
    using AJutShowRoomWinUI.UI.Samples;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// Two ToggleStrip repros: setting SelectedItem from code with a boxed enum, and SelectionChanged on a
    /// standalone deselect. Both report as a status line under the strip.
    /// </summary>
    public sealed partial class ToggleStripReprosPage : Page
    {
        private int m_selectionChangedReproCount;

        public ToggleStripReprosPage ()
        {
            this.InitializeComponent();

            // ToggleStrip enum bug repro
            this.ToggleStripEnumBugRepro.ItemsSource = Enum.GetValues<eEditorMode>();

            // ToggleStrip SelectionChanged deselect repros (multi-select + single-select-with-none, shared counter)
            this.ToggleStripSelectionChangedRepro.ItemsSource = new[] { "One", "Two", "Three", "Four" };
            this.ToggleStripSingleSelectNoneRepro.ItemsSource = new[] { "One", "Two", "Three" };
        }

        // ===========[ ToggleStrip enum bug repro ]================================
        public eEditorMode SelectedEditorMode { get; set; } = eEditorMode.Text;

        private void EnumBugRepro_OnSetNumber (object sender, RoutedEventArgs e)
        {
            this.SelectedEditorMode = eEditorMode.Number;
            this.ToggleStripEnumBugRepro.SelectedItem = eEditorMode.Number;
            this.EnumBugReproStatus.Text = $"Set to Number. SelectedItem = {this.ToggleStripEnumBugRepro.SelectedItem}";
        }

        private void EnumBugRepro_OnSetColor (object sender, RoutedEventArgs e)
        {
            this.SelectedEditorMode = eEditorMode.Color;
            this.ToggleStripEnumBugRepro.SelectedItem = eEditorMode.Color;
            this.EnumBugReproStatus.Text = $"Set to Color. SelectedItem = {this.ToggleStripEnumBugRepro.SelectedItem}";
        }

        private void EnumBugRepro_OnSetText (object sender, RoutedEventArgs e)
        {
            this.SelectedEditorMode = eEditorMode.Text;
            this.ToggleStripEnumBugRepro.SelectedItem = eEditorMode.Text;
            this.EnumBugReproStatus.Text = $"Set to Text. SelectedItem = {this.ToggleStripEnumBugRepro.SelectedItem}";
        }

        private void ToggleStripSelectionChangedRepro_OnSelectionChanged (object sender, AJut.UX.Controls.ToggleStrip.ToggleStripSelectionChangedEventArgs e)
        {
            ++m_selectionChangedReproCount;
            string current = string.Join(", ", e.CurrentSelection.OfType<object>());
            this.ToggleStripSelectionChangedStatus.Text = $"SelectionChanged fired {m_selectionChangedReproCount} times - current: [{current}]";
        }
    }
}
