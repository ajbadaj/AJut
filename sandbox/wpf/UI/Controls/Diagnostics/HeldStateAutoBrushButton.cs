namespace TheAJutShowRoom.UI.Controls.Diagnostics
{
    using System.Windows;
    using AJut.UX.Controls;
    using DPUtils = AJut.UX.DPUtils<HeldStateAutoBrushButton>;

    /// <summary>
    /// An AutoBrushButton that paints one state with no pointer, so the Theme Tester can show pointer over and pressed
    /// beside rest and disabled. WPF picks implicit styles by exact type, so give it the themed AutoBrushButton style by
    /// key: Style="{DynamicResource {x:Type ajut:AutoBrushButton}}".
    /// </summary>
    public sealed class HeldStateAutoBrushButton : AutoBrushButton
    {
        // ===========[ Dependency Properties ]===================================
        public static readonly DependencyProperty HeldStateProperty = DPUtils.Register(_ => _.HeldState, eManagedButtonState.Normal, (d, e) => d.RefreshActiveStateBrushes());
        public eManagedButtonState HeldState
        {
            get => (eManagedButtonState)this.GetValue(HeldStateProperty);
            set => this.SetValue(HeldStateProperty, value);
        }

        // ===========[ Properties ]===================================
        protected override eManagedButtonState CurrentState => this.HeldState;
    }
}
