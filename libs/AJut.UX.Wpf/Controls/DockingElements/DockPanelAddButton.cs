namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;

    public class DockPanelAddButton : Button
    {
        static DockPanelAddButton ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DockPanelAddButton), new FrameworkPropertyMetadata(typeof(DockPanelAddButton)));
        }
    }
}
