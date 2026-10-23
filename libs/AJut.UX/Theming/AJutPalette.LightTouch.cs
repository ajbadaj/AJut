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
                { "AJut_Brush_ControlBackground", Solid(0xFFCFE0F2) },
                { "AJut_Brush_PopOutControlBackground", Gradient(Stop(0.0, 0xFFFFFFFF), Stop(0.1, 0xFFDCE8F5), Stop(0.9, 0xFFB4CDE8), Stop(1.0, 0xFF5A7896)) },
                { "AJut_Brush_ControlBackgroundHover", Solid(0xFFBCD4EE) },
                { "AJut_Brush_ButtonBackgroundPressed", Solid(0xFF8CB8E6) },
                { "AJut_Brush_ControlBackgroundDisabled", Solid(0xFFD7D7D7) },
                { "AJut_Color_ButtonBorder", Solid(0x00000000) },
                { "AJut_Brush_ButtonBorder", Alias("AJut_Color_ButtonBorder") },
                { "AJut_Brush_ButtonBorderHover", Solid(0x00000000) },
                { "AJut_Brush_ButtonBorderPressed", Solid(0x00000000) },
            };
        }
    }
}
