namespace AJut.UX.Controls
{
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using AppKit;

    // ===========[ AutoBrushButton ]============================================
    // A button colored from one accent color: hover, pressed and disabled are derived from it by
    // AJut.UX's InteractiveSurfaceColors, the same derivation WinUI3's AutoBrushButton uses, and
    // the text color is picked to stay readable on it.
    //
    // The accent defaults to the AJut accent with the theme loaded, as WinUI3's does, and to the
    // system accent color without it, which the user picks in System Settings. Either way it is a
    // dynamic color (it changes with light and dark, with the theme, and when the user changes the
    // system accent). So the accent is resolved to an actual color each time the button draws,
    // under the drawing appearance, and the derived palette is rebuilt only when that resolved
    // color differs from the last one.
    public class AutoBrushButton : ManagedBrushButton
    {
        private NSColor m_propagatedAccentColor = ThemedColors.Get(ThemedColorKeys.AutoBrushButton_Accent);
        private ColorArgb m_paletteAccent;
        private bool m_hasPalette;
        private InteractiveSurfaceColors m_palette;

        // ===========[ Properties ]============================================
        /// <summary>The color every state is derived from. By default, and when set to null, the AJut accent with the theme loaded and the system accent color without it.</summary>
        public NSColor PropagatedAccentColor
        {
            get => m_propagatedAccentColor;
            set
            {
                m_propagatedAccentColor = value ?? ThemedColors.Get(ThemedColorKeys.AutoBrushButton_Accent);
                this.NeedsDisplay = true;
            }
        }

        // ===========[ Helper Methods ]========================================
        protected override SurfaceColors GetColorsForState (eManagedButtonState state)
        {
            this.EnsurePalette();
            SurfaceStateColors colors;
            switch (state)
            {
                case eManagedButtonState.PointerOver: colors = m_palette.PointerOver; break;
                case eManagedButtonState.Pressed: colors = m_palette.Pressed; break;
                case eManagedButtonState.Disabled: colors = m_palette.Disabled; break;
                default: colors = m_palette.Normal; break;
            }

            return new SurfaceColors(colors.Background.ToNSColor(), colors.Foreground.ToNSColor(), colors.Border.ToNSColor());
        }

        // Only called while drawing, so a dynamic accent resolves to what this view shows now
        private void EnsurePalette ()
        {
            // A color with no sRGB form (a pattern, say) falls back to a mid gray
            const byte kOpaque = 0xFF;
            const byte kFallbackGray = 0x80;

            if (!m_propagatedAccentColor.TryToColorArgb(out ColorArgb accent))
            {
                accent = new ColorArgb(kOpaque, kFallbackGray, kFallbackGray, kFallbackGray);
            }

            if (!m_hasPalette || accent != m_paletteAccent)
            {
                m_palette = InteractiveSurfaceColors.BuildFromAccent(accent);
                m_paletteAccent = accent;
                m_hasPalette = true;
            }
        }
    }
}
