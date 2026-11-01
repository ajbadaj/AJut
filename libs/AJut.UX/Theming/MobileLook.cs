namespace AJut.UX.Theming
{
    /// <summary>
    /// How the AJut controls the mobile toolkits draw in code (AJut.UX.Android and AJut.UX.UIKit) look: for each control,
    /// the theme key each part takes in each state, its sizes, and any fixed shading. Both toolkits draw from here, so a
    /// look changes in one place and the two cannot drift apart. AJut.UX.AppKit takes its disabled ToggleStrip's keys from
    /// here as well, so that strip matches theirs. The keys are the ones WinUI3's templates and visual
    /// states take for the same parts, resolved through <see cref="AJutPalette"/> with the AJut theme loaded; with no
    /// theme each toolkit gives a key its own platform default. Sizes are in device independent units: dp on Android,
    /// points on iOS.
    /// </summary>
    /// <remarks>
    /// A key marked as a gradient resolves to gradient stops (TryGetPaint), never to one color (TryGetColor fails for it),
    /// so a toolkit paints it as a gradient.
    ///
    /// Each control's look is a nested class in a file of its own (MobileLook.NumericEditor.cs and so on), so adding a
    /// control's look never touches another's.
    /// </remarks>
    public static partial class MobileLook
    {
    }
}
