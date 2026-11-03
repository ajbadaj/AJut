namespace AJut.UX.Theming
{
    using AJut.UX.Helpers;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics.Drawables;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// Gives Android's own widgets the AJut palette at runtime: every CheckBox, RadioButton, Switch and Spinner under a
    /// view. It is the part of the AJut look a style can't carry. Theme.AJut (in this package's resources) gives them the
    /// accent and follows light and dark by itself, since a light or dark switch recreates the activity; what Android
    /// only knows as the app runs, the AJut theme on or off (<see cref="ThemeColors.IsThemeLoaded"/>) and its touch
    /// palettes (<see cref="ThemeColors.UsesTouchVariant"/>), only code can follow. This also covers an app whose
    /// activities keep a theme of their own.
    /// </summary>
    /// <remarks>
    /// <see cref="AppThemeManager"/> runs it on each activity it gives a theme to, as the activity starts. Views added
    /// after that (a list's rows, a dialog's content) are an app's to run it on. With no AJut theme loaded it leaves every
    /// widget as it is. AJut's own controls paint themselves, so it never goes inside one: an EnumComboBox's Spinner keeps
    /// EnumComboBox's look. The keys are <see cref="MobileLook.StockControls"/>'s, the stock keys WinUI3 gives the same
    /// parts. A Spinner's open list takes the AJut fill; its items' text is the item layout's, which follows the
    /// activity's theme.
    /// </remarks>
    public static class StockControlTint
    {
        // ===========[ Public Interface Methods ]===================================
        /// <summary>Tints every stock CheckBox, RadioButton, Switch and Spinner under <paramref name="root"/>, itself included</summary>
        public static void Apply (View root)
        {
            Context? context = root.Context;
            if (!ThemeColors.IsThemeLoaded || (context == null))
            {
                return;
            }

            Walk(root, new Tints(context));
        }

        // ===========[ Helper Methods ]===================================
        private static void Walk (View view, Tints tints)
        {
            // An AJut control paints itself, children and all
            if (view.GetType().Assembly == typeof(StockControlTint).Assembly)
            {
                return;
            }

            switch (view)
            {
                case Switch toggle:
                    toggle.ThumbTintList = tints.SwitchThumb;
                    toggle.TrackTintList = tints.SwitchTrack;
                    break;

                case CheckBox check:
                    check.ButtonTintList = tints.CheckBox;
                    break;

                case RadioButton radio:
                    radio.ButtonTintList = tints.RadioButton;
                    break;

                case Spinner spinner:
                    spinner.BackgroundTintList = tints.SpinnerArrow;
                    spinner.SetPopupBackgroundDrawable(new ColorDrawable(tints.SpinnerPopupBackground));
                    break;
            }

            if (view is ViewGroup group)
            {
                for (int index = 0; index < group.ChildCount; ++index)
                {
                    View? child = group.GetChildAt(index);
                    if (child != null)
                    {
                        Walk(child, tints);
                    }
                }
            }
        }

        // ===========[ Subclasses/structs ]===================================
        // Every tint a walk hands out, made once per walk for the context's light or dark. Each list goes from the most
        // particular state to the least, since a view takes the first that matches.
        private sealed class Tints
        {
            private static readonly int[] kDisabledChecked = new[] { -Android.Resource.Attribute.StateEnabled, Android.Resource.Attribute.StateChecked };
            private static readonly int[] kDisabled = new[] { -Android.Resource.Attribute.StateEnabled };
            private static readonly int[] kPressed = new[] { Android.Resource.Attribute.StatePressed };
            private static readonly int[] kChecked = new[] { Android.Resource.Attribute.StateChecked };
            private static readonly int[] kAny = System.Array.Empty<int>();

            private readonly Context m_context;

            public Tints (Context context)
            {
                m_context = context;
                this.SwitchThumb = this.MakeChecked(MobileLook.StockControls.SwitchThumbOnDisabled, MobileLook.StockControls.SwitchThumbOffDisabled, null, MobileLook.StockControls.SwitchThumbOn, MobileLook.StockControls.SwitchThumbOff);
                this.SwitchTrack = this.MakeChecked(MobileLook.StockControls.SwitchTrackOnDisabled, MobileLook.StockControls.SwitchTrackOffDisabled, null, MobileLook.StockControls.SwitchTrackOn, MobileLook.StockControls.SwitchTrackOff);
                this.CheckBox = this.MakeChecked(MobileLook.StockControls.CheckBoxCheckedDisabled, MobileLook.StockControls.CheckBoxUncheckedDisabled, MobileLook.StockControls.CheckBoxPressed, MobileLook.StockControls.CheckBoxChecked, MobileLook.StockControls.CheckBoxUnchecked);
                this.RadioButton = this.MakeChecked(MobileLook.StockControls.RadioButtonCheckedDisabled, MobileLook.StockControls.RadioButtonUncheckedDisabled, MobileLook.StockControls.RadioButtonPressed, MobileLook.StockControls.RadioButtonChecked, MobileLook.StockControls.RadioButtonUnchecked);
                this.SpinnerArrow = ColorStateList.ValueOf(this.Resolve(MobileLook.StockControls.SpinnerArrow));
                this.SpinnerPopupBackground = this.Resolve(MobileLook.StockControls.SpinnerPopupBackground);
            }

            public ColorStateList SwitchThumb { get; }

            public ColorStateList SwitchTrack { get; }

            public ColorStateList CheckBox { get; }

            public ColorStateList RadioButton { get; }

            public ColorStateList SpinnerArrow { get; }

            public Android.Graphics.Color SpinnerPopupBackground { get; }

            // Disabled and checked, disabled, pressed (when the part has a pressed look), checked, then anything else
            private ColorStateList MakeChecked (string disabledChecked, string disabled, string? pressed, string isChecked, string normal)
            {
                if (pressed == null)
                {
                    return new ColorStateList(
                        new[] { kDisabledChecked, kDisabled, kChecked, kAny },
                        new[] { this.Resolve(disabledChecked).ToArgb(), this.Resolve(disabled).ToArgb(), this.Resolve(isChecked).ToArgb(), this.Resolve(normal).ToArgb() }
                    );
                }

                return new ColorStateList(
                    new[] { kDisabledChecked, kDisabled, kPressed, kChecked, kAny },
                    new[] { this.Resolve(disabledChecked).ToArgb(), this.Resolve(disabled).ToArgb(), this.Resolve(pressed).ToArgb(), this.Resolve(isChecked).ToArgb(), this.Resolve(normal).ToArgb() }
                );
            }

            // Every key here is a solid color in all four palettes (MobileLookTests checks it), and this only runs with the
            // theme loaded, so the literal never shows; it is there because ThemedColor takes one
            private Android.Graphics.Color Resolve (string key)
            {
                const uint kNeverShown = 0xFF808080;
                return ThemeColors.Resolve(m_context, ThemedColor.FromArgb(key, kNeverShown, kNeverShown)).ToAndroidColor();
            }
        }
    }
}
