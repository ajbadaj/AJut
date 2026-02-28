namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using DPUtils = AJut.UX.DPUtils<ToggleStripButton>;

    // ===========[ ToggleStripButton ]===========================================
    // A single selectable button within a ToggleStrip. Manages CommonStates VSM
    // transitions via IsSelected DP and pointer event overrides. All background
    // visuals are XAML-driven through ThemeResource keys - no visual properties
    // are set in code.
    //
    // BorderThickness and CornerRadius are set by ToggleStrip code (index-aware,
    // structural assignments) before template application. They are pushed to
    // PART_Root manually in OnApplyTemplate because WinUI3 TemplateBinding does
    // not backfill pre-template local values.
    //
    // Template parts:
    //   PART_Root  - the root Border; carries BorderThickness/CornerRadius and
    //                owns the CommonStates VSM background transitions.
    //
    // Template visual states (group: CommonStates):
    //   Normal             - unselected, resting
    //   PointerOver        - unselected, hovered
    //   Pressed            - unselected, pressed
    //   Checked            - selected, resting
    //   CheckedPointerOver - selected + hovered
    //   CheckedPressed     - selected + pressed

    [TemplatePart(Name = nameof(PART_Root), Type = typeof(Border))]
    [TemplateVisualState(Name = "Normal",             GroupName = "CommonStates")]
    [TemplateVisualState(Name = "PointerOver",        GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Pressed",            GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Checked",            GroupName = "CommonStates")]
    [TemplateVisualState(Name = "CheckedPointerOver", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "CheckedPressed",     GroupName = "CommonStates")]
    public sealed class ToggleStripButton : ContentControl
    {
        // ===========[ Fields ]================================================
        private bool m_isPointerOver;
        private bool m_isPointerPressed;

        // ===========[ Construction ]==========================================
        public ToggleStripButton ()
        {
            this.DefaultStyleKey = typeof(ToggleStripButton);
            this.UseSystemFocusVisuals = false;
        }

        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            this.PART_Root = this.GetTemplateChild(nameof(PART_Root)) as Border;

            // BorderThickness and CornerRadius are set by ToggleStrip as local
            // values before template application. WinUI3 TemplateBinding does not
            // backfill pre-template local values, so push manually here.
            if (this.PART_Root != null)
            {
                this.PART_Root.BorderThickness = this.BorderThickness;
                this.PART_Root.CornerRadius = this.CornerRadius;
            }

            this.UpdateVisualState(false);
        }

        // ===========[ Dependency Properties ]=================================
        public static readonly DependencyProperty IsSelectedProperty = DPUtils.Register(_ => _.IsSelected, (d, e) => d.UpdateVisualState(true));
        public bool IsSelected
        {
            get => (bool)this.GetValue(IsSelectedProperty);
            set => this.SetValue(IsSelectedProperty, value);
        }

        // ===========[ Properties ]============================================
        private Border PART_Root { get; set; }

        // ===========[ Pointer event overrides ]===============================
        protected override void OnPointerEntered (PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);
            m_isPointerOver = true;
            this.UpdateVisualState(true);
        }

        protected override void OnPointerExited (PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);
            m_isPointerOver = false;
            m_isPointerPressed = false;
            this.UpdateVisualState(true);
        }

        protected override void OnPointerPressed (PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            m_isPointerPressed = true;
            this.UpdateVisualState(true);
        }

        protected override void OnPointerReleased (PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);
            m_isPointerPressed = false;
            this.UpdateVisualState(true);
        }

        protected override void OnTapped (TappedRoutedEventArgs e)
        {
            base.OnTapped(e);
            this.IsSelected = !this.IsSelected;
            e.Handled = true;
        }

        // ===========[ Private helpers ]=======================================
        private void UpdateVisualState (bool useTransitions)
        {
            string state;
            if (this.IsSelected)
            {
                state = m_isPointerPressed ? "CheckedPressed" : m_isPointerOver ? "CheckedPointerOver" : "Checked";
            }
            else
            {
                state = m_isPointerPressed ? "Pressed" : m_isPointerOver ? "PointerOver" : "Normal";
            }

            VisualStateManager.GoToState(this, state, useTransitions);
        }
    }
}
