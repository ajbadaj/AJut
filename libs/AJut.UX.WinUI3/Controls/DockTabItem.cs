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
    // Visual styling (Background, BorderBrush) comes from the XAML Style Setters so
    // themes can override them without redefining the ControlTemplate.
    // BorderThickness is index-aware (computed per-tab position by DockZone code) and
    // pushed to PART_Root in OnApplyTemplate since WinUI3 TemplateBinding does not
    // backfill local values set before template application.
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
        private Border m_innerBorder;

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
            m_innerBorder = this.GetTemplateChild("InnerBorder") as Border;

            // Push visual DPs to template parts. BorderThickness is a local value set by
            // DockZone code before the template is applied; WinUI3 TemplateBinding does not
            // backfill local values so we push manually here. Background and BorderBrush come
            // from Style Setters (read correctly by code here as the effective value).
            if (m_rootBorder != null)
            {
                m_rootBorder.BorderThickness = this.BorderThickness;
                m_rootBorder.BorderBrush = this.BorderBrush;
            }
            if (m_innerBorder != null)
            {
                m_innerBorder.Background = this.Background;
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
