namespace AJut.UX.Controls
{
    using AJut.UX;
    using AJut.UX.Helpers;
    using Microsoft.UI.Xaml;
    using Windows.UI;
    using DPUtils = AJut.UX.DPUtils<AutoBrushButton>;

    /// <summary>
    /// A button you color with a single accent. Hand it one PropagatedAccentColor and it derives a
    /// coherent, readable palette - a foreground that contrasts the accent, a visible border, and the
    /// pointer over / pressed / disabled variants - with no per state brushes and no ResourceDictionary
    /// override. The derivation lives in <see cref="InteractiveSurfaceColors.BuildFromAccent"/>.
    /// </summary>
    public class AutoBrushButton : _ManagedBrushButton
    {
        private InteractiveSurfaceColors m_palette;

        public AutoBrushButton ()
        {
            this.DefaultStyleKey = typeof(AutoBrushButton);
            this.RebuildPalette();
        }

        // ===========[ Dependency Properties ]===================================
        public static readonly DependencyProperty PropagatedAccentColorProperty = DPUtils.Register(_ => _.PropagatedAccentColor, (d, e) => d.OnPropagatedAccentColorChanged());
        public Color PropagatedAccentColor { get => (Color)this.GetValue(PropagatedAccentColorProperty); set => this.SetValue(PropagatedAccentColorProperty, value); }

        // ===========[ Public Interface Methods ]===================================
        protected override SurfaceBrushes GetBrushesForState (eManagedButtonState state)
        {
            SurfaceStateColors colors = state switch
            {
                eManagedButtonState.PointerOver => m_palette.PointerOver,
                eManagedButtonState.Pressed => m_palette.Pressed,
                eManagedButtonState.Disabled => m_palette.Disabled,
                _ => m_palette.Normal,
            };

            return new SurfaceBrushes(colors.Background.ToBrush(), colors.Foreground.ToBrush(), colors.Border.ToBrush());
        }

        // ===========[ Events ]===================================
        private void OnPropagatedAccentColorChanged ()
        {
            this.RebuildPalette();
            this.RefreshActiveStateBrushes();
        }

        // ===========[ Helper Methods ]===================================
        private void RebuildPalette ()
        {
            m_palette = InteractiveSurfaceColors.BuildFromAccent(this.PropagatedAccentColor.ToColorArgb());
        }
    }
}
