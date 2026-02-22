namespace AJut.UX.Controls
{
    using System;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using Windows.UI;
    using DPUtils = AJut.UX.DPUtils<ColorEditIngressControl>;

    // ===========[ ColorEditIngressControl ]====================================
    // WinUI3 color editor for PropertyGrid DataTemplates.  Shows a solid-color
    // swatch; tap/click opens a Flyout containing the native WinUI3 ColorPicker.
    //
    // Edit lifecycle (mirrors WPF ColorEditIngressControl):
    //   Tap swatch    → cache EditColor, sync ColorPicker, ShowAt swatch
    //   ColorChanged  → update EditColor in real time (live preview)
    //   Flyout closed → fire UserEditComplete if color actually changed
    //
    // IsReadOnly blocks the flyout from opening.
    //
    // No shared view model is needed — WinUI3's ColorPicker handles all
    // color-space math natively, so the WPF TextBox-based ColorValueEditControl
    // has no equivalent here.
    //
    // Template part:
    //   PART_Swatch  — Border whose Background is kept in sync with EditColor

    [TemplatePart(Name = nameof(PART_Swatch), Type = typeof(Border))]
    public class ColorEditIngressControl : Control, IUserEditNotifier
    {
        // ===========[ Instance fields ]==========================================
        private Border PART_Swatch;
        private Flyout m_flyout;
        private ColorPicker m_colorPicker;
        private Color? m_editCache;

        // ===========[ Construction ]=============================================
        public ColorEditIngressControl ()
        {
            this.DefaultStyleKey = typeof(ColorEditIngressControl);
        }

        // ===========[ IUserEditNotifier ]========================================
        /// <inheritdoc/>
        public event EventHandler<UserEditAppliedEventArgs> UserEditComplete;

        // ===========[ Dependency Properties ]====================================

        // EditColor: the color being edited.  Bind TwoWay to PropertyEditTarget.EditValue.
        public static readonly DependencyProperty EditColorProperty = DPUtils.Register(_ => _.EditColor, (d, e) => d.OnEditColorChanged(e.NewValue));
        public Color EditColor
        {
            get => (Color)this.GetValue(EditColorProperty);
            set => this.SetValue(EditColorProperty, value);
        }

        // IsReadOnly: prevents the flyout from opening.
        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly);
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }

        // ===========[ Template application ]====================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_Swatch != null)
            {
                this.PART_Swatch.Tapped -= this.Swatch_OnTapped;
            }

            this.PART_Swatch = (Border)this.GetTemplateChild(nameof(PART_Swatch));
            if (this.PART_Swatch == null)
            {
                return;
            }

            this.PART_Swatch.Background = new SolidColorBrush(this.EditColor);
            this.PART_Swatch.Tapped += this.Swatch_OnTapped;

            // Build flyout + ColorPicker once; reuse across opens.
            // Created in code rather than XAML to avoid GetTemplateChild limitations
            // on items inside attached-property flyouts in WinUI3 ControlTemplates.
            if (m_flyout == null)
            {
                m_colorPicker = new ColorPicker
                {
                    IsAlphaEnabled = true,
                    IsMoreButtonVisible = false,
                };
                m_colorPicker.ColorChanged += this.ColorPicker_OnColorChanged;

                m_flyout = new Flyout
                {
                    Content = m_colorPicker,
                    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                };
                m_flyout.Closed += this.Flyout_OnClosed;
            }
        }

        // ===========[ Event handlers ]==========================================
        private void Swatch_OnTapped (object sender, TappedRoutedEventArgs e)
        {
            if (this.IsReadOnly || m_flyout == null)
            {
                return;
            }

            m_editCache = this.EditColor;
            m_colorPicker.Color = this.EditColor;
            m_flyout.ShowAt(this.PART_Swatch);
        }

        private void ColorPicker_OnColorChanged (ColorPicker sender, ColorChangedEventArgs e)
        {
            this.EditColor = e.NewColor;
        }

        private void Flyout_OnClosed (object sender, object e)
        {
            if (m_editCache.HasValue && m_editCache.Value != this.EditColor)
            {
                this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(m_editCache.Value, this.EditColor));
            }

            m_editCache = null;
        }

        // ===========[ Property change handlers ]================================
        private void OnEditColorChanged (Color newColor)
        {
            if (this.PART_Swatch != null)
            {
                // Replace brush rather than mutate it — SolidColorBrush.Color
                // is animatable so mutation can interfere with the ColorPicker.
                this.PART_Swatch.Background = new SolidColorBrush(newColor);
            }
        }
    }
}
