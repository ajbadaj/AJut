namespace AJut.UX.Theming
{
    using AJut.UX.Helpers;

    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// AutoBrushButton: the interaction palette every stack derives from one accent, with the pressed border pushed
        /// much further off the pressed fill. On touch a press is the only highlight there is, and the shared derivation's
        /// pressed state, a slightly dimmer fill with its border one small step off it, barely changes under a finger.
        /// </summary>
        public static class AutoBrushButton
        {
            /// <summary>How far the pressed border's lightness sits off the pressed fill's, lighter on a dark fill and darker on a light one.</summary>
            public const double PressedBorderLightnessShift = 0.45;

            // ===========[ Public Interface Methods ]===================================
            /// <summary>
            /// The palette for <paramref name="accent"/>: <see cref="InteractiveSurfaceColors.BuildFromAccent"/>'s, with
            /// the pressed border <see cref="PressedBorderLightnessShift"/> off the pressed fill.
            /// </summary>
            public static InteractiveSurfaceColors BuildFromAccent (ColorArgb accent)
            {
                InteractiveSurfaceColors shared = InteractiveSurfaceColors.BuildFromAccent(accent);
                ColorArgb pressedFill = shared.Pressed.Background;
                double shift = (AJutColorHelper.IsDark(pressedFill) ? 1.0 : -1.0) * PressedBorderLightnessShift;
                return shared with
                {
                    Pressed = shared.Pressed with { Border = AJutColorHelper.AdjustLightness(pressedFill, shift) },
                };
            }
        }
    }
}
