namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml.Controls;

    // ===========[ DockCloseButton ]===========================================
    // WinUI3-specific: no WPF equivalent.
    // Minimal Button subclass for DockZone close buttons (panel header and
    // tab strip). DefaultStyleKey points to the style in DockCloseButton.xaml
    // (merged into Generic.xaml), which provides a ControlTemplate with
    // CommonStates VSM - Normal / PointerOver / Pressed.
    //
    // ButtonBase drives CommonStates automatically, so no pointer event
    // handlers are needed in code-behind.  Hover and pressed backgrounds use
    // AJut_DockCloseButton_* ThemeResource keys defined in
    // AJutCustomControlStylingDefaults.xaml; AJutThemeMapAliasing.xaml
    // redirects them to AJut palette brushes when the theme is loaded.
    //
    // DockZone sets per-instance Padding / FontSize / VerticalAlignment;
    // Content ("✕"), BorderThickness, and the VSM template come from the style.
    public sealed class DockCloseButton : Button
    {
        public DockCloseButton ()
        {
            this.DefaultStyleKey = typeof(DockCloseButton);
        }
    }
}
