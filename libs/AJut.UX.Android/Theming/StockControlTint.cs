namespace AJut.UX.Theming
{
    using AJut.UX.Helpers;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics.Drawables;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// Gives Android's own widgets the AJut palette at runtime: every CheckBox, RadioButton, Switch, Spinner, plain Button
    /// and EditText under a view. It is the part of the AJut look a style can't carry. Theme.AJut (in this package's
    /// resources) gives them the accent and follows light and dark by itself, since a light or dark switch recreates the
    /// activity; what Android only knows as the app runs, the AJut theme on or off (<see cref="ThemeColors.IsThemeLoaded"/>)
    /// and its touch palettes (<see cref="ThemeColors.UsesTouchVariant"/>), only code can follow. This also covers an app
    /// whose activities keep a theme of their own.
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
        /// <summary>Tints every stock CheckBox, RadioButton, Switch, Spinner, plain Button and EditText under <paramref name="root"/>, itself included</summary>
        public static void Apply (View root)
        {
            Context? context = root.Context;
            if (!ThemeColors.IsThemeLoaded || (context == null))
            {
                return;
            }

            Walk(root, new Tints(context));
        }

        /// <summary>
        /// Gives a plain button an AJut control makes for itself (a PropertyGrid button row) the face <see cref="Apply"/>
        /// gives an app's, with the AJut theme loaded. Apply never reaches inside an AJut control, so the control asks.
        /// </summary>
        internal static void ApplyToOwnButton (Button button)
        {
            if (ThemeColors.IsThemeLoaded && (button.Context != null))
            {
                new Tints(button.Context).RestyleButton(button);
            }
        }

        /// <summary>The text field counterpart of <see cref="ApplyToOwnButton"/> (a PropertyGrid string editor)</summary>
        internal static void ApplyToOwnTextField (EditText field)
        {
            if (ThemeColors.IsThemeLoaded && (field.Context != null))
            {
                new Tints(field.Context).RestyleTextField(field);
            }
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

                case EditText field:
                    tints.RestyleTextField(field);
                    break;

                // Any other check-style button (a ToggleButton) keeps its own look; only a plain button is a button face
                case CompoundButton:
                    break;

                case Button button:
                    tints.RestyleButton(button);
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
            private static readonly int[] kHovered = new[] { Android.Resource.Attribute.StateHovered };
            private static readonly int[] kFocused = new[] { Android.Resource.Attribute.StateFocused };
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
                this.ButtonText = new ColorStateList(
                    new[] { kDisabled, kPressed, kHovered, kAny },
                    new[]
                    {
                        this.Resolve(MobileLook.StockControls.ButtonForegroundDisabled).ToArgb(),
                        this.Resolve(MobileLook.StockControls.ButtonForegroundPressed).ToArgb(),
                        this.Resolve(MobileLook.StockControls.ButtonForegroundPointerOver).ToArgb(),
                        this.Resolve(MobileLook.StockControls.ButtonForeground).ToArgb(),
                    }
                );
                this.TextFieldText = new ColorStateList(
                    new[] { kDisabled, kFocused, kAny },
                    new[]
                    {
                        this.Resolve(MobileLook.StockControls.TextFieldForegroundDisabled).ToArgb(),
                        this.Resolve(MobileLook.StockControls.TextFieldForegroundFocused).ToArgb(),
                        this.Resolve(MobileLook.StockControls.TextFieldForeground).ToArgb(),
                    }
                );
            }

            public ColorStateList SwitchThumb { get; }

            public ColorStateList SwitchTrack { get; }

            public ColorStateList CheckBox { get; }

            public ColorStateList RadioButton { get; }

            public ColorStateList SpinnerArrow { get; }

            public Android.Graphics.Color SpinnerPopupBackground { get; }

            public ColorStateList ButtonText { get; }

            public ColorStateList TextFieldText { get; }

            /// <summary>
            /// Gives a text field the normal AJut text box, as WPF's TextBox style draws it: the text box fill in an
            /// outline that takes the highlight while the field has focus, and the bright highlight under a mouse or
            /// stylus, which wins over focus. The caret takes the text color where Android lets it be set apart from the
            /// selection, which stays Theme.AJut's.
            /// </summary>
            /// <remarks>
            /// The stock background is an underline, and its padding puts nearly all the room under the text, where the
            /// line was, so in an outlined box the text sat against the top of the outline. The field keeps the stock
            /// padding's total, so it keeps its height, but shares it evenly above and below the text, with at least the
            /// outline and <see cref="MobileLook.StockControls.TextFieldVerticalPadding"/> on each side.
            /// </remarks>
            public void RestyleTextField (EditText field)
            {
                var faces = new StateListDrawable();
                faces.AddState(kDisabled, this.MakeTextFieldFace(MobileLook.StockControls.TextFieldBorder));
                faces.AddState(kHovered, this.MakeTextFieldFace(MobileLook.StockControls.TextFieldBorderPointerOver));
                faces.AddState(kFocused, this.MakeTextFieldFace(MobileLook.StockControls.TextFieldBorderFocused));
                faces.AddState(kAny, this.MakeTextFieldFace(MobileLook.StockControls.TextFieldBorder));

                int least = (int)System.Math.Round((MobileLook.StockControls.TextFieldBorderThickness + MobileLook.StockControls.TextFieldVerticalPadding) * this.Density);
                int vertical = System.Math.Max(least, (field.PaddingTop + field.PaddingBottom) / 2);
                int left = field.PaddingLeft;
                int right = field.PaddingRight;
                field.Background = faces;
                field.SetPadding(left, vertical, right, vertical);
                field.SetTextColor(this.TextFieldText);
                field.SetHintTextColor(this.Resolve(MobileLook.StockControls.TextFieldPlaceholder));

                if (System.OperatingSystem.IsAndroidVersionAtLeast(29))
                {
                    var caret = new GradientDrawable();
                    caret.SetColor(this.Resolve(MobileLook.StockControls.TextFieldCaret));
                    caret.SetSize((int)System.Math.Round(MobileLook.StockControls.TextFieldCaretWidth * this.Density), 0);
                    field.TextCursorDrawable = caret;
                }
            }

            /// <summary>
            /// Gives a plain button an AJut button face for each state, as BrushButton paints one: the stock background is
            /// the platform's gray raised surface, which reads as a panel next to AJut's buttons rather than as a button.
            /// Like BrushButton, it shows a press with color rather than the stock lift, and keeps the padding the stock
            /// background carried. A drawable holds its state, so each button gets its own.
            /// </summary>
            public void RestyleButton (Button button)
            {
                var faces = new StateListDrawable();
                faces.AddState(kDisabled, this.MakeButtonFace(MobileLook.StockControls.ButtonBackgroundDisabled, MobileLook.StockControls.ButtonBorderDisabled));
                faces.AddState(kPressed, this.MakeButtonFace(MobileLook.StockControls.ButtonBackgroundPressed, MobileLook.StockControls.ButtonBorderPressed));
                faces.AddState(kHovered, this.MakeButtonFace(MobileLook.StockControls.ButtonBackgroundPointerOver, MobileLook.StockControls.ButtonBorderPointerOver));
                faces.AddState(kAny, this.MakeButtonFace(MobileLook.StockControls.ButtonBackground, MobileLook.StockControls.ButtonBorder));

                int left = button.PaddingLeft;
                int top = button.PaddingTop;
                int right = button.PaddingRight;
                int bottom = button.PaddingBottom;
                button.Background = faces;
                button.SetPadding(left, top, right, bottom);
                button.StateListAnimator = null;
                button.SetTextColor(this.ButtonText);

                // The stock background carried the platform's focus look, so the AJut one takes its place, as on BrushButton
                FocusVisual.Apply(button);
            }

            private float Density => m_context.Resources?.DisplayMetrics?.Density ?? 1f;

            private GradientDrawable MakeButtonFace (string backgroundKey, string borderKey)
            {
                return this.MakeFace(backgroundKey, borderKey, MobileLook.StockControls.ButtonCornerRadius, MobileLook.StockControls.ButtonBorderThickness);
            }

            private GradientDrawable MakeTextFieldFace (string borderKey)
            {
                return this.MakeFace(MobileLook.StockControls.TextFieldBackground, borderKey, MobileLook.StockControls.TextFieldCornerRadius, MobileLook.StockControls.TextFieldBorderThickness);
            }

            // A rounded, outlined fill, the sizes in dp
            private GradientDrawable MakeFace (string backgroundKey, string borderKey, float cornerRadius, float borderThickness)
            {
                float density = this.Density;
                var face = new GradientDrawable();
                face.SetCornerRadius(cornerRadius * density);
                face.SetColor(this.Resolve(backgroundKey));
                face.SetStroke((int)System.Math.Round(borderThickness * density), this.Resolve(borderKey));
                return face;
            }

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
