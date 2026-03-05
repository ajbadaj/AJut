namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls.Primitives;

    public class DockPanelShowHideToggleButton : ToggleButton
    {
        static DockPanelShowHideToggleButton ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DockPanelShowHideToggleButton), new FrameworkPropertyMetadata(typeof(DockPanelShowHideToggleButton)));
        }
    }
}
