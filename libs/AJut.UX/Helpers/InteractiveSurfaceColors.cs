namespace AJut.UX.Helpers
{
    /// <summary>The three brushable colors for one interaction state of a surface (button, chip, etc).</summary>
    public readonly record struct SurfaceStateColors (ColorArgb Background, ColorArgb Foreground, ColorArgb Border);

    /// <summary>
    /// A full set of interaction state colors (normal / pointer over / pressed / disabled) derived from a
    /// single accent color. Hand over one color and get a coherent, readable palette back: the background
    /// brightens on hover and dims on press, the foreground holds one readable polarity across those states
    /// (no white/black flip on small shifts), and the border sits a step off each state's background.
    /// </summary>
    public readonly record struct InteractiveSurfaceColors (
        SurfaceStateColors Normal,
        SurfaceStateColors PointerOver,
        SurfaceStateColors Pressed,
        SurfaceStateColors Disabled
    )
    {
        // ------ How far each state shifts off the accent. Tuned by eye; these are the knobs to turn.
        private const double kPointerOverLightnessShift = 0.06; // hover brightens
        private const double kPressedLightnessShift = 0.10;     // pressed dims
        private const double kBorderLightnessShift = 0.18;
        private const double kPressedSaturationScale = 0.92;
        private const double kDisabledSaturationScale = 0.25;
        // Hold the accent's foreground polarity across hover / pressed so a small background shift across the
        // light/dark crossover does not flip the text color. Sized to cover the lightness shifts above.
        private const double kForegroundHysteresis = 0.12;
        private const byte kDisabledBackgroundAlpha = 0x40;
        private const byte kDisabledForegroundAlpha = 0x80;
        private const byte kDisabledBorderAlpha = 0x40;

        /// <summary>Derives a coherent interaction palette from a single accent (background) color.</summary>
        public static InteractiveSurfaceColors BuildFromAccent (ColorArgb accent)
        {
            // Polarity is decided once from the accent and then held (with hysteresis) across the states, so
            // the background can brighten / dim on interaction without the foreground flipping black<->white.
            bool preferLight = AJutColorHelper.IsDark(accent);

            var normal = _BuildState(accent, preferLight);
            var pointerOver = _BuildState(AJutColorHelper.Lighten(accent, kPointerOverLightnessShift), preferLight);

            ColorArgb pressedBackground = AJutColorHelper.ScaleSaturation(
                AJutColorHelper.Darken(accent, kPressedLightnessShift),
                kPressedSaturationScale
            );
            var pressed = _BuildState(pressedBackground, preferLight);

            ColorArgb disabledBackground = AJutColorHelper.ScaleSaturation(accent, kDisabledSaturationScale).WithAlpha(kDisabledBackgroundAlpha);
            var disabled = new SurfaceStateColors(
                disabledBackground,
                normal.Foreground.WithAlpha(kDisabledForegroundAlpha),
                AJutColorHelper.ScaleSaturation(normal.Border, kDisabledSaturationScale).WithAlpha(kDisabledBorderAlpha)
            );

            return new InteractiveSurfaceColors(normal, pointerOver, pressed, disabled);

            // Foreground from this state's background (holding polarity); border stepped off it for a visible edge.
            static SurfaceStateColors _BuildState (ColorArgb background, bool preferLight)
            {
                double borderDelta = (AJutColorHelper.IsDark(background) ? 1.0 : -1.0) * kBorderLightnessShift;
                return new SurfaceStateColors(
                    background,
                    AJutColorHelper.PickReadableForeground(background, preferLight, kForegroundHysteresis),
                    AJutColorHelper.AdjustLightness(background, borderDelta)
                );
            }
        }
    }
}
