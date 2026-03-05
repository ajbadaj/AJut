namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls.Primitives;

    public sealed class DockPanelShowHideToggleButton : ToggleButton
    {
        public DockPanelShowHideToggleButton ()
        {
            this.DefaultStyleKey = typeof(DockPanelShowHideToggleButton);
        }

        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
            string state = (this.IsChecked == true) ? "Checked" : "Normal";
            VisualStateManager.GoToState(this, state, false);
        }
    }
}
