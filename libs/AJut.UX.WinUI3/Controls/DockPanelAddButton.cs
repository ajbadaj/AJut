namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    public sealed class DockPanelAddButton : Button
    {
        public DockPanelAddButton ()
        {
            this.DefaultStyleKey = typeof(DockPanelAddButton);
        }

        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
            VisualStateManager.GoToState(this, "Normal", false);
        }
    }
}
