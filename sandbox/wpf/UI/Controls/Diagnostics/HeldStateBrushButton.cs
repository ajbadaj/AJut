namespace TheAJutShowRoom.UI.Controls.Diagnostics
{
    using System.Windows;
    using AJut.UX.Controls;
    using DPUtils = AJut.UX.DPUtils<HeldStateBrushButton>;

    /// <summary>
    /// A BrushButton that paints one state with no pointer, so the Theme Tester can show pointer over and pressed beside
    /// rest and disabled. WPF picks implicit styles by exact type, so give it the themed BrushButton style by key:
    /// Style="{DynamicResource {x:Type ajut:BrushButton}}".
    /// </summary>
    public sealed class HeldStateBrushButton : BrushButton
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
