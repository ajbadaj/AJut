namespace AJut.UX.Theming
{
    using AJut.UX.Helpers;
    using Android.Content;
    using Android.Graphics.Drawables;
    using Android.Views;

    /// <summary>
    /// The keyboard focus look of the controls AJut draws itself on Android, by MobileLook.FocusVisual: a thin dotted
    /// rectangle in the bright primary just inside the control, the same as AJut's WPF focus visual. It is the view's
    /// foreground, shown only in the focused state, so it draws over the control's own surface and needs no layout space.
    /// In touch mode a tap doesn't move focus to a button, so the rectangle shows only once a key has.
    /// </summary>
    /// <remarks>
    /// Android 8 draws a default focus highlight of its own over a focused view with no focused state in its background or
    /// foreground. This foreground has one, and the default is turned off as well, so the two never stack.
    /// </remarks>
    public static class FocusVisual
    {
        // Unthemed, the blue Android's own GeometryButton press takes, light and dark
        private static readonly ThemedColor kStroke = ThemedColor.FromArgb(MobileLook.FocusVisual.Stroke, 0xFF0078D4, 0xFF4CA3E8);

        /// <summary>
        /// Gives <paramref name="view"/> the focus rectangle, in the colors its context resolves now. Call it again when the
        /// theme or night mode changes, as the control repaints its own colors.
        /// </summary>
        public static void Apply (View view)
        {
            Context context = view.Context!;
            float density = context.Resources?.DisplayMetrics?.Density ?? 1f;

            var rectangle = new GradientDrawable();
            rectangle.SetShape(ShapeType.Rectangle);
            rectangle.SetColor(Android.Graphics.Color.Transparent);
            rectangle.SetStroke(
                (int)System.Math.Max(1f, MobileLook.FocusVisual.StrokeThickness * density),
                ThemeColors.Resolve(context, kStroke).ToAndroidColor(),
                MobileLook.FocusVisual.DashLength * density,
                MobileLook.FocusVisual.DashGap * density
            );

            int inset = (int)System.Math.Round(MobileLook.FocusVisual.Inset * density);
            var focused = new StateListDrawable();
            focused.AddState(new[] { Android.Resource.Attribute.StateFocused }, new InsetDrawable(rectangle, inset));

            view.DefaultFocusHighlightEnabled = false;
            view.Foreground = focused;
        }
    }
}
