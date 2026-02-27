namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using DPUtils = AJut.UX.DPUtils<DockTabItem>;

    // ===========[ DockTabItem ]================================================
    // A tab header item for DockZone's tab strip. Manages VSM opacity/margin
    // transitions for Normal/PointerOver/Selected/SelectedPointerOver via IsSelected DP
    // and pointer event overrides.
    //
    // Visual styling (Background, BorderBrush) comes from XAML Style Setters with
    // TemplateBinding in the template — themes override without redefining the ControlTemplate.
    // BorderThickness is the only property pushed in OnApplyTemplate: it is a pre-template
    // local value (index-aware, set by DockZone code) that TemplateBinding cannot backfill.
    //
    // Template visual states (group: SelectionStates):
    //   Normal              - unselected, resting   (opacity 0.35, slight vertical inset)
    //   PointerOver         - unselected, hovered   (opacity 0.80, slightly raised)
    //   Selected            - selected              (opacity 1.00, -2px top margin bleeds into content)
    //   SelectedPointerOver - selected + hovered    (same as Selected — hover keeps the overlap)

    [TemplateVisualState(Name = "Normal", GroupName = "SelectionStates")]
    [TemplateVisualState(Name = "PointerOver", GroupName = "SelectionStates")]
    [TemplateVisualState(Name = "Selected", GroupName = "SelectionStates")]
    [TemplateVisualState(Name = "SelectedPointerOver", GroupName = "SelectionStates")]
    public sealed class DockTabItem : ContentControl
    {
        // ===========[ Fields ]================================================
        private bool m_isPointerOver;
        private Border m_rootBorder;

        // ===========[ Construction ]==========================================
        public DockTabItem()
        {
            this.DefaultStyleKey = typeof(DockTabItem);
            this.UseSystemFocusVisuals = false;
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            m_rootBorder = this.GetTemplateChild("Root") as Border;

            // BorderThickness is a local value set by DockZone code before template application
            // (per-tab index-aware left/right borders). WinUI3 TemplateBinding does not backfill
            // pre-template local values, so push manually here.
            // Background and BorderBrush come from Style Setters and are handled by
            // TemplateBinding in the XAML template — no code-push needed.
            if (m_rootBorder != null)
            {
                m_rootBorder.BorderThickness = this.BorderThickness;
            }

            this.UpdateVisualState();
        }

        // ===========[ Dependency Properties ]=================================

        public static readonly DependencyProperty IsSelectedProperty = DPUtils.Register(_ => _.IsSelected, (d, e) => d.UpdateVisualState());
        public bool IsSelected
        {
            get => (bool)this.GetValue(IsSelectedProperty);
            set => this.SetValue(IsSelectedProperty, value);
        }

        // ===========[ Pointer event overrides ]===============================

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);
            m_isPointerOver = true;
            this.UpdateVisualState();
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);
            m_isPointerOver = false;
            this.UpdateVisualState();
        }

        // ===========[ Visual state management ]===============================

        private void UpdateVisualState()
        {
            if (this.IsSelected)
            {
                VisualStateManager.GoToState(this, m_isPointerOver ? "SelectedPointerOver" : "Selected", false);
            }
            else
            {
                VisualStateManager.GoToState(this, m_isPointerOver ? "PointerOver" : "Normal", false);
            }
        }
    }
}
