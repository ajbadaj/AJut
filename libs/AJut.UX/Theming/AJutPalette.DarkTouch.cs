namespace AJut.UX.Theming
{
    using System.Collections.Generic;
    using static AJut.UX.Theming.AJutThemeEntry;

    public static partial class AJutPalette
    {
        // Mirrors AJut.UX.WinUI3's Resources\DarkTouchThemeColorsBase.xaml, in its order: only what the touch palette
        // changes from the dark one, laid over it. Change the XAML first; the palette test names every entry here that
        // no longer matches it.
        private static Dictionary<string, AJutThemeEntry> BuildDarkTouchOverrides ()
        {
            return new Dictionary<string, AJutThemeEntry>
            {
                { "AJut_Color_NormalText", Solid(0xFFDDDDDD) },
                { "AJut_Brush_NormalText", Alias("AJut_Color_NormalText") },
                { "AJut_Brush_ControlBackground", Solid(0xFF34343C) },
                { "AJut_Brush_PopOutControlBackground", Gradient(Stop(0.0, 0xFF76768A), Stop(0.05, 0xFF45454F), Stop(0.9, 0xFF34343C), Stop(1.0, 0xFF18181D)) },
                { "AJut_Brush_ControlBackgroundHover", Solid(0xFF3E3E48) },
                { "AJut_Brush_ButtonBackgroundPressed", Solid(0xFF0B4F80) },
                { "AJut_Brush_ControlBackgroundDisabled", Solid(0xFF2B2B2E) },
                { "AJut_Color_ButtonBorder", Solid(0x00000000) },
                { "AJut_Brush_ButtonBorder", Alias("AJut_Color_ButtonBorder") },
                { "AJut_Brush_ButtonBorderHover", Solid(0x00000000) },
                { "AJut_Brush_ButtonBorderPressed", Solid(0x00000000) },
            };
        }
    }
}
