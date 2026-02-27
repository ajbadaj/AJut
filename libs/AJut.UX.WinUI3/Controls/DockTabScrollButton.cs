namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml.Controls;

    // ===========[ DockTabScrollButton ]========================================
    // WinUI3-specific: no WPF equivalent.
    // Minimal Button subclass for the left/right scroll arrow buttons in
    // DockZone's tab navigation strip.  DefaultStyleKey points to the style in
    // DockTabScrollButton.xaml (merged via Generic.xaml), which provides a
    // transparent template with CommonStates VSM hover/pressed highlight.
    // ButtonBase drives CommonStates automatically — no pointer event handlers needed.
    public sealed class DockTabScrollButton : Button
    {
        public DockTabScrollButton()
        {
            this.DefaultStyleKey = typeof(DockTabScrollButton);
        }
    }
}
