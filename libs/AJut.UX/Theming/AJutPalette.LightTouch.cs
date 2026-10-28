namespace AJut.UX.Theming
{
    using System.Collections.Generic;
    using static AJut.UX.Theming.AJutThemeEntry;

    public static partial class AJutPalette
    {
        // Mirrors AJut.UX.WinUI3's Resources\LightTouchThemeColorsBase.xaml, in its order: only what the touch palette
        // changes from the light one, laid over it. Change the XAML first; the palette test names every entry here that
        // no longer matches it.
        private static Dictionary<string, AJutThemeEntry> BuildLightTouchOverrides ()
        {
            return new Dictionary<string, AJutThemeEntry>
            {
                { "AJut_Brush_ControlBackground", Solid(0xFFB8B8B8) },
                { "AJut_Brush_PopOutControlBackground", Gradient(Stop(0.0, 0xFFF0F0F0), Stop(0.1, 0xFFC8C8C8), Stop(0.9, 0xFFACACAC), Stop(1.0, 0xFF6A6A6A)) },
                { "AJut_Brush_ControlBackgroundHover", Solid(0xFFC9C9C9) },
                { "AJut_Brush_ButtonBackgroundPressed", Solid(0xFF8E8E8E) },
                { "AJut_Brush_ControlBackgroundDisabled", Solid(0xFFE3E3E3) },
                { "AJut_Color_ButtonBorder", Solid(0x00000000) },
                { "AJut_Brush_ButtonBorder", Alias("AJut_Color_ButtonBorder") },
                { "AJut_Brush_ButtonBorderHover", Solid(0x00000000) },
                { "AJut_Brush_ButtonBorderPressed", Solid(0x00000000) },
            };
        }
    }
}
