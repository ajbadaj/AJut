namespace AJut.UX.Theming
{
    using System;
    using CoreGraphics;
    using Foundation;
    using UIKit;

    /// <summary>
    /// Gives iOS's own controls the AJut palette: the AJut accent as each window's tint, which every tint-driven stock
    /// control takes (sliders, progress bars, text carets and selection), the AJut on color for every UISwitch, and,
    /// through <see cref="Apply(UIView)"/>, AJut's button and text box colors for the plain buttons and text fields in a
    /// view. The colors are ThemedColors' dynamic colors, so they follow light and dark with nothing to refresh.
    /// </summary>
    /// <remarks>
    /// <see cref="AppThemeManager"/> runs <see cref="Apply()"/> as it starts and gives every window it themes the accent;
    /// an app runs it again after turning the AJut theme or its touch palettes on or off, then rebuilds its views, as
    /// AJut's own UIKit controls need. A switch takes its appearance as it enters a window. A switch's thumb and its off
    /// track keep the system's look, since UIKit has no public way to color an off track. With no AJut theme loaded the
    /// switch goes back to the system's color, and a window's tint is left as the app set it.
    /// <para>
    /// Buttons and text fields are an app's to pass in, as on Android: <see cref="Apply(UIView)"/> on the views it builds.
    /// UIAppearance can't carry these: a button's colors per state live on its configuration, which appearance doesn't
    /// reach, and an appearance on UIButton would reach AJut's own buttons, which are UIButtons too.
    /// </para>
    /// </remarks>
    public static class StockControlTint
    {
        // The padding a bordered system button uses, as AJut's own buttons have it
        private const float kButtonVerticalPadding = 7f;
        private const float kButtonHorizontalPadding = 12f;

        // ===========[ Properties ]===================================
        /// <summary>The accent stock controls take with the AJut theme loaded; null without it, which leaves the system's.</summary>
        public static UIColor Accent => ThemedColors.Get(MobileLook.StockControls.Accent);

        // ===========[ Public Interface Methods ]===================================
        /// <summary>Gives every UISwitch made from now on the AJut on color, or the system's with no theme loaded</summary>
        public static void Apply ()
        {
            UISwitch.Appearance.OnTintColor = ThemedColors.Get(MobileLook.StockControls.SwitchTrackOn);
        }

        /// <summary>
        /// Gives every plain system button (a UIButton of type System with no configuration of its own) and every rounded
        /// text field under <paramref name="root"/>, itself included, the AJut colors. With no AJut theme loaded it leaves
        /// them as they are. A button or field given a configuration or a border style of its own is left alone, as is
        /// anything inside an AJut control or inside any other control, whose insides are its own.
        /// </summary>
        public static void Apply (UIView root)
        {
            if (ThemedColors.IsThemeLoaded && (root != null))
            {
                Walk(root);
            }
        }

        /// <summary>
        /// Gives a button an AJut control makes for itself (a PropertyGrid button row) the face <see cref="Apply(UIView)"/>
        /// gives an app's plain button, with the AJut theme loaded. Apply never reaches inside an AJut control, so the
        /// control asks. Its title, from its configuration or set for its normal state, carries over.
        /// </summary>
        internal static void ApplyToOwnButton (UIButton button)
        {
            if (ThemedColors.IsThemeLoaded)
            {
                RestyleButton(button);
            }
        }

        /// <summary>The text field counterpart of <see cref="ApplyToOwnButton"/> (a PropertyGrid string editor)</summary>
        internal static void ApplyToOwnTextField (UITextField field)
        {
            if (ThemedColors.IsThemeLoaded)
            {
                RestyleTextField(field);
            }
        }

        /// <summary>Gives <paramref name="window"/> the AJut accent as its tint, with the AJut theme loaded</summary>
        public static void ApplyTo (UIWindow window)
        {
            UIColor accent = Accent;
            if (accent != null)
            {
                window.TintColor = accent;
            }
        }

        // ===========[ Helper Methods ]===================================
        private static void Walk (UIView view)
        {
            // An AJut control paints itself, children and all
            if (view.GetType().Assembly == typeof(StockControlTint).Assembly)
            {
                return;
            }

            // Exact types only: a subclass is somebody's own control
            if (view.GetType() == typeof(UIButton))
            {
                var button = (UIButton)view;
                if ((button.ButtonType == UIButtonType.System) && (button.Configuration == null))
                {
                    RestyleButton(button);
                }
            }
            else if (view.GetType() == typeof(UITextField))
            {
                var field = (UITextField)view;
                if (field.BorderStyle == UITextBorderStyle.RoundedRect)
                {
                    RestyleTextField(field);
                }
            }

            if (view is UIControl)
            {
                return;
            }

            foreach (UIView child in view.Subviews)
            {
                Walk(child);
            }
        }

        // A system button is tinted text with no face, which reads as a link beside AJut's buttons. It takes a plain
        // configuration with AJut's button face, the way AJut's own buttons are drawn, and a static update handler that
        // paints the face for each state. A handler is a block the button holds; a static one captures nothing, so it
        // holds nothing that leads back to the button. The title set before this, on the button's own configuration or
        // for its normal state, carries over onto the new configuration.
        private static void RestyleButton (UIButton button)
        {
            UIButtonConfiguration configuration = UIButtonConfiguration.PlainButtonConfiguration;
            configuration.ContentInsets = new NSDirectionalEdgeInsets(kButtonVerticalPadding, kButtonHorizontalPadding, kButtonVerticalPadding, kButtonHorizontalPadding);
            configuration.Title = button.Configuration?.Title ?? button.CurrentTitle;
            button.Configuration = configuration;
            button.ConfigurationUpdateHandler = UpdateButtonFace;
            button.SetNeedsUpdateConfiguration();
        }

        private static void UpdateButtonFace (UIButton button)
        {
            UIButtonConfiguration configuration = button.Configuration;
            if (configuration == null)
            {
                return;
            }

            string backgroundKey = MobileLook.StockControls.ButtonBackground;
            string foregroundKey = MobileLook.StockControls.ButtonForeground;
            string borderKey = MobileLook.StockControls.ButtonBorder;
            if (!button.Enabled)
            {
                backgroundKey = MobileLook.StockControls.ButtonBackgroundDisabled;
                foregroundKey = MobileLook.StockControls.ButtonForegroundDisabled;
                borderKey = MobileLook.StockControls.ButtonBorderDisabled;
            }
            else if (button.Highlighted)
            {
                backgroundKey = MobileLook.StockControls.ButtonBackgroundPressed;
                foregroundKey = MobileLook.StockControls.ButtonForegroundPressed;
                borderKey = MobileLook.StockControls.ButtonBorderPressed;
            }
            else if (button.Hovered)
            {
                backgroundKey = MobileLook.StockControls.ButtonBackgroundPointerOver;
                foregroundKey = MobileLook.StockControls.ButtonForegroundPointerOver;
                borderKey = MobileLook.StockControls.ButtonBorderPointerOver;
            }

            // The face's colors are UIColors, so dynamic ones keep following light and dark
            UIBackgroundConfiguration face = configuration.Background;
            face.BackgroundColor = ThemedColors.Get(backgroundKey);
            face.StrokeColor = ThemedColors.Get(borderKey);
            face.StrokeWidth = MobileLook.StockControls.ButtonBorderThickness;
            face.CornerRadius = MobileLook.StockControls.ButtonCornerRadius;
            configuration.Background = face;
            configuration.BaseForegroundColor = ThemedColors.Get(foregroundKey);
            button.Configuration = configuration;
        }

        // The normal AJut text box, as WPF's TextBox style draws it: the text box fill in an outline that takes the
        // highlight while the field is being edited, and the bright highlight under an iPad pointer, which wins over
        // editing. The rounded style draws a fill and outline of its own (its fill is black in dark, a hole over AJut's
        // panels), so the field goes borderless and the fill and outline are drawn on its layer, its sides padded so the
        // text clears the corners and a least height keeping it at the rounded field's height. A field it has made
        // borderless isn't rounded any more, so running Apply again over it does nothing twice.
        //
        // The state is followed through static handlers, on the field's own editing events, a hover recognizer, and a
        // light and dark change: a static handler has no target, so the field holding it holds nothing that leads back
        // anywhere, and there is nothing to let go of when the field goes. The caret and the selection both take the
        // window's tint on iOS, so the caret stays the accent rather than WPF's text color.
        private static void RestyleTextField (UITextField field)
        {
            field.BorderStyle = UITextBorderStyle.None;
            field.BackgroundColor = ThemedColors.Get(MobileLook.StockControls.TextFieldBackground);
            field.Layer.CornerRadius = MobileLook.StockControls.TextFieldCornerRadius;
            field.Layer.BorderWidth = MobileLook.StockControls.TextFieldBorderThickness;

            field.LeftView = MakeTextFieldPadding();
            field.LeftViewMode = UITextFieldViewMode.Always;
            if (field.ClearButtonMode == UITextFieldViewMode.Never)
            {
                field.RightView = MakeTextFieldPadding();
                field.RightViewMode = UITextFieldViewMode.Always;
            }

            NSLayoutConstraint minimumHeight = field.HeightAnchor.ConstraintGreaterThanOrEqualTo(MobileLook.StockControls.TextFieldMinHeight);
            minimumHeight.Active = true;

            if (!string.IsNullOrEmpty(field.Placeholder))
            {
                field.AttributedPlaceholder = new NSAttributedString(
                    field.Placeholder,
                    foregroundColor: ThemedColors.Get(MobileLook.StockControls.TextFieldPlaceholder)
                );
            }

            field.EditingDidBegin -= TextField_OnEditingStateChanged;
            field.EditingDidBegin += TextField_OnEditingStateChanged;
            field.EditingDidEnd -= TextField_OnEditingStateChanged;
            field.EditingDidEnd += TextField_OnEditingStateChanged;
            field.AddGestureRecognizer(new UIHoverGestureRecognizer(OnTextFieldHover));
            if (OperatingSystem.IsIOSVersionAtLeast(17))
            {
                field.RegisterForTraitChanges(new[] { typeof(UITraitUserInterfaceStyle) }, OnTextFieldAppearanceChanged);
            }

            RefreshTextField(field, isPointerOver: false);
        }

        private static UIView MakeTextFieldPadding () => new UIView(new CGRect(0, 0, MobileLook.StockControls.TextFieldHorizontalPadding, 0));

        private static void TextField_OnEditingStateChanged (object sender, EventArgs e)
        {
            if (sender is UITextField field)
            {
                RefreshTextField(field, isPointerOver: false);
            }
        }

        private static void OnTextFieldHover (UIHoverGestureRecognizer recognizer)
        {
            if (recognizer.View is UITextField field)
            {
                bool isPointerOver = (recognizer.State == UIGestureRecognizerState.Began) || (recognizer.State == UIGestureRecognizerState.Changed);
                RefreshTextField(field, isPointerOver);
            }
        }

        // The outline is a CGColor, which freezes a dynamic color, so it is resolved again when the appearance changes
        private static void OnTextFieldAppearanceChanged (IUITraitEnvironment environment, UITraitCollection previous)
        {
            if (environment is UITextField field)
            {
                RefreshTextField(field, isPointerOver: false);
            }
        }

        // The pointer wins over editing for the outline, as the later of WPF's two triggers. A pointer state isn't kept
        // anywhere, so an edit that begins or ends under the pointer shows the editing outline until the pointer moves.
        private static void RefreshTextField (UITextField field, bool isPointerOver)
        {
            string borderKey = isPointerOver
                ? MobileLook.StockControls.TextFieldBorderPointerOver
                : (field.IsEditing ? MobileLook.StockControls.TextFieldBorderFocused : MobileLook.StockControls.TextFieldBorder);
            string textKey = !field.Enabled
                ? MobileLook.StockControls.TextFieldForegroundDisabled
                : (field.IsEditing ? MobileLook.StockControls.TextFieldForegroundFocused : MobileLook.StockControls.TextFieldForeground);

            field.Layer.BorderColor = ThemedColors.Get(borderKey)?.GetResolvedColor(field.TraitCollection).CGColor;
            field.TextColor = ThemedColors.Get(textKey);
        }
    }
}
