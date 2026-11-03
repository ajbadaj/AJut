namespace AJut.UX.Controls
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics;
    using Android.Graphics.Drawables;
    using Android.OS;
    using Android.Runtime;
    using Android.Text;
    using Android.Util;
    using Android.Views;
    using Android.Views.InputMethods;
    using Android.Widget;

    /// <summary>
    /// A number field with step buttons: a minus button, the text field, and a plus button, with an optional label in
    /// front. It keeps the value's own numeric type (an int stays an int), caps it to <see cref="Minimum"/> and
    /// <see cref="Maximum"/>, and steps it by <see cref="Nudge"/> per tap. Holding a step button repeats it. The editing
    /// rules are AJut.UX's <see cref="NumericEditorController"/>, the same on every AJut toolkit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An edit is committed when the keyboard's Done action is pressed or the field loses focus. Out of bounds text is
    /// flagged and left for the user to fix, or snapped to the bound on commit, as <see cref="OutOfBoundsResponse"/> says.
    /// In a layout, ajutMinimum, ajutMaximum, ajutNudge, ajutBigNudge, ajutSmallNudge, ajutDecimalPlacesAllowed,
    /// ajutOutOfBoundsResponse, ajutEnforceNumericType and ajutLabel set those.
    /// </para>
    /// <para>
    /// With a hardware keyboard (a Chromebook, a tablet's keyboard), holding Shift steps by <see cref="SmallNudge"/> and
    /// holding Ctrl or Meta steps by <see cref="BigNudge"/>, on the step buttons and on the Up and Down arrow keys alike,
    /// as on UIKit and WinUI3.
    /// </para>
    /// <para>
    /// The number's alignment is the field's own: <see cref="Field"/>'s Gravity, centered by default, where UIKit's editor
    /// has a TextAlignment. This editor is a LinearLayout, which already has View's TextAlignment (android:textAlignment),
    /// and taking that over for the number would give it a meaning it has on no other layout.
    /// </para>
    /// </remarks>
    [Register("ajut.ux.controls.NumericEditor")]
    public class NumericEditor : LinearLayout, INumericEditorHost
    {
        // The step sizes Shift (small) and Ctrl or Meta (big) take, until set in code or in a layout
        private const double kDefaultBigNudge = 5.0;
        private const double kDefaultSmallNudge = 0.5;

        // The look is MobileLook.NumericEditor's, which UIKit's NumericEditor draws too: the keys and sizes come from there,
        // and only the colors each key falls back to with no theme loaded are Android's own. Unthemed, the frame is the
        // stock control fill, as the ToggleStrip's is, and the well a plain text box color.
        private static readonly ThemedGradient kSurface = new ThemedGradient(
            MobileLook.NumericEditor.FrameSurface,
            new[] { ThemedGradient.Stop(0.0, 0xFFFDFDFD) },
            new[] { ThemedGradient.Stop(0.0, 0xFF2B2B2B) }
        );
        private static readonly ThemedColor kWellBackground = ThemedColor.FromArgb(MobileLook.NumericEditor.WellSurface, 0xFFFFFFFF, 0xFF1E1E1E);
        private static readonly ThemedColor kNormalBorder = ThemedColor.FromArgb(MobileLook.NumericEditor.FrameBorder, 0xFF888888, 0xFF888888);
        private static readonly ThemedColor kFocusBorder = ThemedColor.FromArgb(MobileLook.NumericEditor.FrameBorderFocused, 0xFF0078D4, 0xFF4CA3E8);
        private static readonly ThemedColor kPointerOverBorder = ThemedColor.FromArgb(MobileLook.NumericEditor.FrameBorderPointerOver, 0xFF429CE3, 0xFF7AB8EA);
        private static readonly ThemedColor kErrorBorder = ThemedColor.FromArgb(MobileLook.NumericEditor.FrameBorderError, 0xFFE0524D, 0xFFE0524D);
        private static readonly ThemedColor kButtonPointerOver = ThemedColor.FromArgb(MobileLook.NumericEditor.StepPointerOverFill, 0x26000000, 0x33FFFFFF);
        private static readonly ThemedColor kButtonPressed = ThemedColor.FromArgb(MobileLook.NumericEditor.StepPressedFill, 0x26000000, 0x33FFFFFF);
        private static readonly ThemedColor kForeground = ThemedColor.FromArgb(MobileLook.NumericEditor.StepGlyph, 0xE4000000, 0xFFFFFFFF);
        private static readonly ThemedColor kFieldForeground = ThemedColor.FromArgb(MobileLook.NumericEditor.Number, 0xE4000000, 0xFFFFFFFF);
        private static readonly ThemedColor kLabelForeground = ThemedColor.FromArgb(MobileLook.NumericEditor.Label, 0xE4000000, 0xFFFFFFFF);
        private static readonly ThemedColor kReadOnlyForeground = ThemedColor.FromArgb(MobileLook.NumericEditor.ReadOnlyText, 0x5C000000, 0x5DFFFFFF);

        private readonly NumericEditorController m_controller;
        private GradientDrawable m_frame = null!;
        private View m_frameRow = null!;
        private TextView m_label = null!;
        private EditText m_field = null!;
        private TextView m_decrease = null!;
        private TextView m_increase = null!;
        private View m_warningGlyph = null!;
        private View? m_labelView;
        private Handler? m_repeatHandler;
        private RepeatNudge? m_repeat;
        private object m_value = 0.0;

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        private Type? m_enforceNumericType;

        private double m_minimum = double.NegativeInfinity;
        private double m_maximum = double.PositiveInfinity;
        private double m_nudge = 1.0;
        private double m_bigNudge = kDefaultBigNudge;
        private double m_smallNudge = kDefaultSmallNudge;
        private int m_decimalPlacesAllowed = -1;
        private eOutOfBoundsResponse m_outOfBoundsResponse = eOutOfBoundsResponse.ErrorAndToolTip;
        private string? m_errorMessage;
        private bool m_isReadOnly;
        private bool m_isPointerOverFrame;
        private float m_density = 1f;

        // ===========[ Setup/Construction/Teardown ]===================================
        public NumericEditor (Context context) : base(context)
        {
            m_controller = new NumericEditorController(this);
            this.Initialize(context, null);
        }

        public NumericEditor (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            m_controller = new NumericEditorController(this);
            this.Initialize(context, attrs);
        }

        public NumericEditor (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            m_controller = new NumericEditorController(this);
            this.Initialize(context, attrs);
        }

        protected NumericEditor (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
            m_controller = new NumericEditorController(this);
        }

        protected override void OnAttachedToWindow ()
        {
            base.OnAttachedToWindow();
            m_field.TextChanged -= this.OnFieldTextChanged;
            m_field.TextChanged += this.OnFieldTextChanged;
            m_field.EditorAction -= this.OnFieldEditorAction;
            m_field.EditorAction += this.OnFieldEditorAction;
            m_field.FocusChange -= this.OnFieldFocusChange;
            m_field.FocusChange += this.OnFieldFocusChange;
            m_decrease.Touch -= this.OnStepButtonTouch;
            m_decrease.Touch += this.OnStepButtonTouch;
            m_increase.Touch -= this.OnStepButtonTouch;
            m_increase.Touch += this.OnStepButtonTouch;
        }

        protected override void OnDetachedFromWindow ()
        {
            this.StopRepeating();
            if (m_isPointerOverFrame)
            {
                m_isPointerOverFrame = false;
                this.PaintFrame();
            }

            m_field.TextChanged -= this.OnFieldTextChanged;
            m_field.EditorAction -= this.OnFieldEditorAction;
            m_field.FocusChange -= this.OnFieldFocusChange;
            m_decrease.Touch -= this.OnStepButtonTouch;
            m_increase.Touch -= this.OnStepButtonTouch;
            base.OnDetachedFromWindow();
        }

        // ===========[ Events ]===================================
        /// <summary>Raised when <see cref="Value"/> changes, from an edit, a step or code. Bind two-way through this.</summary>
        public event EventHandler<EventArgs<object>>? ValueChanged;

        // ===========[ Properties ]===================================
        /// <summary>The value, in its own numeric type: setting an int keeps edits and steps as ints.</summary>
        public object Value
        {
            get => m_value;
            set
            {
                if (Equals(m_value, value))
                {
                    return;
                }

                // The controller can write back a different value (text parsed to a number, or a number kept in the
                //  enforced type), and that write back raises ValueChanged itself. Only raise it here when the value
                //  given is the one kept, so the last change reported is always the value the editor holds.
                m_value = value;
                m_controller.OnValueChanged(value);
                if (Equals(m_value, value))
                {
                    this.ValueChanged?.Invoke(this, new EventArgs<object>(value));
                }
            }
        }

        /// <summary>
        /// The numeric type <see cref="Value"/> is kept in however it arrives, as text or as a number of another type.
        /// Null (the default) keeps each value's own type, and text parses as a float. Setting it after a value converts
        /// that value, so the order the two are set in does not matter.
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        public Type? EnforceNumericType
        {
            get => m_enforceNumericType;
            set
            {
                m_enforceNumericType = value;
                m_controller.OnEnforcedTypeChanged(value);
            }
        }

        /// <summary>The lowest value allowed; negative infinity (the default) for none.</summary>
        public double Minimum
        {
            get => m_minimum;
            set
            {
                m_minimum = value;
                m_controller.RefreshErrorState();
            }
        }

        /// <summary>The highest value allowed; positive infinity (the default) for none.</summary>
        public double Maximum
        {
            get => m_maximum;
            set
            {
                m_maximum = value;
                m_controller.RefreshErrorState();
            }
        }

        /// <summary>How far one tap of a step button moves the value. Defaults to 1.</summary>
        public double Nudge
        {
            get => m_nudge;
            set => m_nudge = value;
        }

        /// <summary>The step while Ctrl or Meta is held on a hardware keyboard. Defaults to 5.</summary>
        public double BigNudge
        {
            get => m_bigNudge;
            set => m_bigNudge = value;
        }

        /// <summary>The step while Shift is held on a hardware keyboard. Defaults to 0.5.</summary>
        public double SmallNudge
        {
            get => m_smallNudge;
            set => m_smallNudge = value;
        }

        /// <summary>How many decimal places the value may have; -1 (the default) for any.</summary>
        public int DecimalPlacesAllowed
        {
            get => m_decimalPlacesAllowed;
            set => m_decimalPlacesAllowed = value;
        }

        /// <summary>What happens to out of bounds text: flag it and leave it (the default), or snap it to the bound on commit.</summary>
        public eOutOfBoundsResponse OutOfBoundsResponse
        {
            get => m_outOfBoundsResponse;
            set
            {
                m_outOfBoundsResponse = value;
                m_controller.RefreshErrorState();
            }
        }

        /// <summary>The out of bounds message being shown, or null when the text is fine.</summary>
        public string? ErrorMessage => m_errorMessage;

        /// <summary>The label shown in front of the field; null or empty for none. Hidden while <see cref="LabelView"/> is set.</summary>
        public string? Label
        {
            get => m_label.Text;
            set
            {
                m_label.Text = value;
                this.UpdateLabelVisibility();
            }
        }

        /// <summary>
        /// A view shown in front of the frame in place of the text label, the way WinUI3's LabelContent replaces its "#"
        /// glyph. Unless it brings LinearLayout layout parameters of its own, it is sized to its content and set off from
        /// the frame as the text label is. Null brings the text label back.
        /// </summary>
        public View? LabelView
        {
            get => m_labelView;
            set
            {
                if (m_labelView != null)
                {
                    this.RemoveView(m_labelView);
                }

                m_labelView = value;
                if (value != null)
                {
                    LayoutParams layout = value.LayoutParameters as LayoutParams
                        ?? new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { MarginEnd = (int)(MobileLook.NumericEditor.LabelSpacing * m_density) };
                    this.AddView(value, 0, layout);
                }

                this.UpdateLabelVisibility();
            }
        }

        /// <summary>Stops edits and steps; the value still shows.</summary>
        public bool IsReadOnly
        {
            get => m_isReadOnly;
            set
            {
                m_isReadOnly = value;
                m_field.Enabled = !value;
                m_decrease.Enabled = !value;
                m_increase.Enabled = !value;
            }
        }

        /// <summary>The text field, for styling it the way the app styles its other fields.</summary>
        public EditText Field => m_field;

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// Up and Down step the value from a hardware keyboard, by the tier the held modifiers pick. A key reaches the field
        /// through its parents, so the editor sees it first and takes it, before a single line EditText moves focus on it.
        /// The key's repeats while held step again, as holding a step button does.
        /// </summary>
        public override bool DispatchKeyEvent (KeyEvent? e)
        {
            if ((e != null)
                && !m_isReadOnly
                && ((e.KeyCode == Keycode.DpadUp) || (e.KeyCode == Keycode.DpadDown)))
            {
                if (e.Action == KeyEventActions.Down)
                {
                    this.StepOnce(e.KeyCode == Keycode.DpadUp, this.PickNudge(e.MetaState));
                }

                return true;
            }

            return base.DispatchKeyEvent(e);
        }

        // A mouse or stylus over the frame brightens its border. The editor is handed every hover event inside its bounds,
        //  entering, moving and leaving, before its parts are, so the frame is told apart from the label by where the
        //  pointer is. A hover listener on the frame itself would not do: a layout that passes a hover on to a child that
        //  takes it (a step button, the field) gets an exit of its own, so the border would drop while over a step button.
        protected override bool DispatchHoverEvent (MotionEvent? e)
        {
            if (e != null)
            {
                float x = e.GetX();
                float y = e.GetY();
                bool isOverFrame = (e.ActionMasked != MotionEventActions.HoverExit)
                    && (x >= m_frameRow.Left) && (x < m_frameRow.Right)
                    && (y >= m_frameRow.Top) && (y < m_frameRow.Bottom);
                if (isOverFrame != m_isPointerOverFrame)
                {
                    m_isPointerOverFrame = isOverFrame;
                    this.PaintFrame();
                }
            }

            return base.DispatchHoverEvent(e);
        }

        // ===========[ INumericEditorSettings Implementations ]===================================
        // The settings interface takes null for an unconstrained bound, though its signature does not say so.
        object INumericEditorSettings.Minimum => NumericEditorController.ToMinimumBound(m_minimum)!;

        object INumericEditorSettings.Maximum => NumericEditorController.ToMaximumBound(m_maximum)!;

        // ===========[ INumericEditorHost Implementations ]===================================
        // The caret stays where it was, clamped to the new text, so a reformat while typing does not move it.
        void INumericEditorHost.ShowText (string? text)
        {
            text ??= string.Empty;
            if (m_field.Text == text)
            {
                return;
            }

            int caret = Math.Min(Math.Max(m_field.SelectionEnd, 0), text.Length);
            m_field.Text = text;
            m_field.SetSelection(caret);
        }

        void INumericEditorHost.ShowValue (object value)
        {
            if (Equals(m_value, value))
            {
                return;
            }

            m_value = value;
            this.ValueChanged?.Invoke(this, new EventArgs<object>(value));
        }

        void INumericEditorHost.ShowError (string? errorMessage)
        {
            m_errorMessage = errorMessage;
            m_warningGlyph.Visibility = (errorMessage != null) ? ViewStates.Visible : ViewStates.Gone;
            m_warningGlyph.TooltipText = errorMessage;
            m_warningGlyph.ContentDescription = errorMessage;
            this.PaintFrame();
        }

        // ===========[ Helper Methods ]===================================
        private void Initialize (Context context, IAttributeSet? attrs)
        {
            m_density = context.Resources?.DisplayMetrics?.Density ?? 1f;
            this.Orientation = Android.Widget.Orientation.Horizontal;
            this.SetGravity(GravityFlags.CenterVertical);

            const float kWarningGlyphSizeDp = 16f;
            const float kWarningGlyphSpacingDp = 4f;

            // 1. Label, minus, field, warning glyph, plus
            m_label = new TextView(context) { Visibility = ViewStates.Gone };
            m_label.SetPadding(0, 0, (int)(MobileLook.NumericEditor.LabelSpacing * m_density), 0);
            m_label.SetTextColor(ThemeColors.Resolve(context, kLabelForeground).ToAndroidColor());
            m_decrease = this.MakeStepButton(context, "-", "Decrease", isLeading: true);
            m_increase = this.MakeStepButton(context, "+", "Increase", isLeading: false);
            m_field = new EditText(context)
            {
                Gravity = GravityFlags.Center,
                ImeOptions = ImeAction.Done,
                InputType = InputTypes.ClassNumber | InputTypes.NumberFlagSigned | InputTypes.NumberFlagDecimal,
                Background = new FieldWell(ThemeColors.Resolve(context, kWellBackground).ToAndroidColor(), m_density),
            };
            m_field.SetSingleLine(true);
            m_field.SetMinEms(3);

            // The number is text control text, as on WinUI3, and dims while read-only (the field is disabled then)
            m_field.SetTextColor(MakeReadOnlyDimmedText(context, kFieldForeground));

            // The out of bounds warning is AJut's own glyph beside the number, in the error color, as UIKit's and AppKit's
            //  are, rather than EditText's error icon and popup, which take only the platform's theme. Its tooltip (a long
            //  press, or a pointer resting on it) and its description carry the message.
            m_warningGlyph = new View(context)
            {
                Visibility = ViewStates.Gone,
                Background = new WarningGlyphDrawable(ThemeColors.Resolve(context, kErrorBorder).ToAndroidColor()),
            };

            // The minus, field, warning glyph and plus share one bordered pop-out frame; the label sits outside it. The buttons have no
            //  fill of their own, so they read as part of the frame, and the frame shows above and below the field's well.
            //  The frame is padded by its border so a pressed button fills inside the outline rather than over it.
            int border = (int)Math.Max(1f, MobileLook.NumericEditor.BorderThickness * m_density);
            int wellInset = (int)(MobileLook.NumericEditor.WellInset * m_density);
            int buttonWidth = (int)(MobileLook.NumericEditor.StepButtonWidth * m_density);
            m_frame = ThemeColors.ToDrawable(ThemeColors.Resolve(context, kSurface));
            m_frame.SetCornerRadius(MobileLook.NumericEditor.CornerRadius * m_density);
            var frame = new LinearLayout(context) { Orientation = Android.Widget.Orientation.Horizontal, Background = m_frame };
            frame.SetGravity(GravityFlags.CenterVertical);
            frame.SetPadding(border, border, border, border);
            frame.AddView(m_decrease, new LayoutParams(buttonWidth, ViewGroup.LayoutParams.MatchParent));
            frame.AddView(m_field, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f) { TopMargin = wellInset, BottomMargin = wellInset });
            int glyphSize = (int)(kWarningGlyphSizeDp * m_density);
            int glyphSpacing = (int)(kWarningGlyphSpacingDp * m_density);
            frame.AddView(m_warningGlyph, new LayoutParams(glyphSize, glyphSize) { LeftMargin = glyphSpacing, RightMargin = glyphSpacing });
            frame.AddView(m_increase, new LayoutParams(buttonWidth, ViewGroup.LayoutParams.MatchParent));
            m_frameRow = frame;

            this.AddView(m_label, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
            this.AddView(frame, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            // 2. Layout attributes
            int enforcedTypeIndex = -1;
            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutNumericEditor);
                m_minimum = values.GetFloat(Resource.Styleable.AJutNumericEditor_ajutMinimum, float.NegativeInfinity);
                m_maximum = values.GetFloat(Resource.Styleable.AJutNumericEditor_ajutMaximum, float.PositiveInfinity);
                m_nudge = values.GetFloat(Resource.Styleable.AJutNumericEditor_ajutNudge, 1f);
                m_bigNudge = values.GetFloat(Resource.Styleable.AJutNumericEditor_ajutBigNudge, (float)kDefaultBigNudge);
                m_smallNudge = values.GetFloat(Resource.Styleable.AJutNumericEditor_ajutSmallNudge, (float)kDefaultSmallNudge);
                m_decimalPlacesAllowed = values.GetInt(Resource.Styleable.AJutNumericEditor_ajutDecimalPlacesAllowed, -1);
                m_outOfBoundsResponse = (eOutOfBoundsResponse)values.GetInt(Resource.Styleable.AJutNumericEditor_ajutOutOfBoundsResponse, (int)eOutOfBoundsResponse.ErrorAndToolTip);
                this.Label = values.GetString(Resource.Styleable.AJutNumericEditor_ajutLabel);
                enforcedTypeIndex = values.GetInt(Resource.Styleable.AJutNumericEditor_ajutEnforceNumericType, -1);
                values.Recycle();
            }

            this.PaintFrame();
            m_controller.OnValueChanged(m_value);

            // Set after the starting value, so the controller converts that value to the type and writes it back
            Type? enforcedType = ToEnforceableNumericType(enforcedTypeIndex);
            if (enforcedType != null)
            {
                this.EnforceNumericType = enforcedType;
            }
        }

        // The type each ajutEnforceNumericType value in attrs.xml stands for, or null when the attribute is not set. A
        //  fixed set rather than a type name keeps the attribute working under trimming and AOT, where looking a type up
        //  by name may find nothing, and returning each typeof directly lets the trim analyzer see the annotation is met
        //  (an array of types hides it).
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        private static Type? ToEnforceableNumericType (int attributeValue) => attributeValue switch
        {
            0 => typeof(byte),
            1 => typeof(sbyte),
            2 => typeof(short),
            3 => typeof(ushort),
            4 => typeof(int),
            5 => typeof(uint),
            6 => typeof(long),
            7 => typeof(ulong),
            8 => typeof(float),
            9 => typeof(double),
            10 => typeof(decimal),
            _ => null,
        };

        // A step button is the frame's own surface at rest, takes a wash of the bright highlight under a mouse or stylus, and
        //  fills with it when pressed. Each fill is rounded on the frame's outer side, just inside the outline's rounded
        //  corners, so it does not poke past them. A clickable view is marked hovered by the framework while a pointer is
        //  over it and it is enabled, so a read-only editor's disabled buttons show no hover.
        private TextView MakeStepButton (Context context, string glyph, string description, bool isLeading)
        {
            var button = new TextView(context)
            {
                Text = glyph,
                Gravity = GravityFlags.Center,
                ContentDescription = description,
                Clickable = true,
                Focusable = false,
                TextSize = 20,
            };

            // Its glyph dims while read-only (the button is disabled then), as the number does
            button.SetTextColor(MakeReadOnlyDimmedText(context, kForeground));
            float radius = Math.Max(0f, (MobileLook.NumericEditor.CornerRadius - MobileLook.NumericEditor.BorderThickness) * m_density);
            float[] radii = isLeading
                ? new[] { radius, radius, 0f, 0f, 0f, 0f, radius, radius }
                : new[] { 0f, 0f, radius, radius, radius, radius, 0f, 0f };

            ColorArgb pointerOver = ThemeColors.Resolve(context, kButtonPointerOver);
            pointerOver = pointerOver.WithAlpha((byte)(pointerOver.A * MobileLook.NumericEditor.StepPointerOverOpacity));

            // Pressed is listed first: a state list draws the first entry the button's state matches, and a pressed button
            //  under a stylus is hovered too
            var states = new StateListDrawable();
            states.AddState(new[] { Android.Resource.Attribute.StatePressed }, MakeFill(ThemeColors.Resolve(context, kButtonPressed)));
            states.AddState(new[] { Android.Resource.Attribute.StateHovered }, MakeFill(pointerOver));
            button.Background = states;
            return button;

            GradientDrawable MakeFill (ColorArgb color)
            {
                var fill = new GradientDrawable();
                fill.SetColor(color.ToAndroidColor());
                fill.SetCornerRadii(radii);
                return fill;
            }
        }

        private void PaintFrame ()
        {
            Context context = this.Context!;
            ThemedColor border = MobileLook.NumericEditor.FrameBorderFor(m_errorMessage != null, m_isPointerOverFrame, m_field.HasFocus) switch
            {
                MobileLook.NumericEditor.FrameBorderError => kErrorBorder,
                MobileLook.NumericEditor.FrameBorderPointerOver => kPointerOverBorder,
                MobileLook.NumericEditor.FrameBorderFocused => kFocusBorder,
                _ => kNormalBorder,
            };
            m_frame.SetStroke((int)Math.Max(1f, MobileLook.NumericEditor.BorderThickness * m_density), ThemeColors.Resolve(context, border).ToAndroidColor());
        }

        // A text color for a view that is disabled while the editor is read-only: the given color, and the read-only one
        //  while disabled
        private static ColorStateList MakeReadOnlyDimmedText (Context context, ThemedColor normal)
        {
            return new ColorStateList(
                new[] { new[] { -Android.Resource.Attribute.StateEnabled }, Array.Empty<int>() },
                new[] { ThemeColors.Resolve(context, kReadOnlyForeground).ToAndroidColor().ToArgb(), ThemeColors.Resolve(context, normal).ToAndroidColor().ToArgb() }
            );
        }

        private void StepOnce (bool positive, double amount)
        {
            if (!m_isReadOnly)
            {
                m_controller.Nudge(positive, amount);
            }
        }

        // The tier the held modifiers pick: Ctrl or Meta (a Mac keyboard's Command) for the big step, Shift for the small one
        private double PickNudge (MetaKeyStates modifiers)
        {
            return NumericEditorController.PickNudgeAmount(
                (modifiers & (MetaKeyStates.CtrlOn | MetaKeyStates.MetaOn)) != 0,
                (modifiers & MetaKeyStates.ShiftOn) != 0,
                m_nudge,
                m_bigNudge,
                m_smallNudge
            );
        }

        // The text label shows only with text to show, and never while a LabelView stands in for it
        private void UpdateLabelVisibility ()
        {
            bool isShown = (m_labelView == null) && !string.IsNullOrEmpty(m_label.Text);
            m_label.Visibility = isShown ? ViewStates.Visible : ViewStates.Gone;
        }

        private void StopRepeating ()
        {
            if ((m_repeatHandler != null) && (m_repeat != null))
            {
                m_repeatHandler.RemoveCallbacks(m_repeat);
            }

            m_repeat = null;
        }

        private void OnFieldTextChanged (object? sender, TextChangedEventArgs e) => m_controller.OnTextEdited(m_field.Text);

        private void OnFieldEditorAction (object? sender, TextView.EditorActionEventArgs e)
        {
            e.Handled = false;
            if (e.ActionId == ImeAction.Done)
            {
                m_controller.Commit();
            }
        }

        private void OnFieldFocusChange (object? sender, View.FocusChangeEventArgs e)
        {
            if (!e.HasFocus)
            {
                m_controller.Commit();
            }

            this.PaintFrame();
        }

        // A tap steps once; holding steps again after a short delay, then repeatedly until the finger lifts. The step is the
        // tier the keyboard modifiers held at the press pick, for the whole hold.
        private void OnStepButtonTouch (object? sender, View.TouchEventArgs e)
        {
            e.Handled = false;
            if (m_isReadOnly || (e.Event == null))
            {
                return;
            }

            bool positive = ReferenceEquals(sender, m_increase);
            switch (e.Event.ActionMasked)
            {
                case MotionEventActions.Down:
                    double amount = this.PickNudge(e.Event.MetaState);
                    this.StopRepeating();
                    this.StepOnce(positive, amount);
                    m_repeatHandler ??= new Handler(Looper.MainLooper!);
                    m_repeat = new RepeatNudge(this, positive, amount);
                    m_repeatHandler.PostDelayed(m_repeat, (long)MobileLook.NumericEditor.RepeatDelay.TotalMilliseconds);
                    break;

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    this.StopRepeating();
                    break;
            }
        }

        // ===========[ Subclasses/structs ]===================================
        // One held step button's repeat: steps, then schedules itself again until the editor stops it.
        private sealed class RepeatNudge : Java.Lang.Object, Java.Lang.IRunnable
        {
            private readonly NumericEditor m_owner;
            private readonly bool m_positive;
            private readonly double m_amount;

            public RepeatNudge (NumericEditor owner, bool positive, double amount)
            {
                m_owner = owner;
                m_positive = positive;
                m_amount = amount;
            }

            public void Run ()
            {
                if (!ReferenceEquals(m_owner.m_repeat, this))
                {
                    return;
                }

                m_owner.StepOnce(m_positive, m_amount);
                m_owner.m_repeatHandler?.PostDelayed(this, (long)MobileLook.NumericEditor.RepeatInterval.TotalMilliseconds);
            }
        }

        // The field's background: the text box color, sunk into the frame by the edge lighting WinUI3's NumericEditor
        //  draws with two nested borders, a shadow along the top and left and a light line along the bottom and right.
        //  The lighting is the same literal shading over any theme, as it is there.
        private sealed class FieldWell : Drawable
        {
            private static readonly Color kShadow = MobileLook.NumericEditor.WellShadow.ToAndroidColor();
            private static readonly Color kLight = MobileLook.NumericEditor.WellLight.ToAndroidColor();

            private readonly Paint m_paint = new Paint();
            private readonly Color m_fill;
            private readonly float m_shadowThickness;
            private readonly float m_lightThickness;
            private int m_alpha = 255;

            public FieldWell (Color fill, float density)
            {
                m_fill = fill;
                m_shadowThickness = Math.Max(1f, MobileLook.NumericEditor.WellShadowThickness * density);
                m_lightThickness = Math.Max(1f, MobileLook.NumericEditor.WellLightThickness * density);
            }

            public override int Opacity => (int)Format.Translucent;

            public override void Draw (Canvas canvas)
            {
                Rect bounds = this.Bounds;
                float left = bounds.Left;
                float top = bounds.Top;
                float right = bounds.Right;
                float bottom = bounds.Bottom;

                // The light line runs the whole right side and the whole bottom, and the shadow the rest of the top and
                //  left, the way the outer border (bottom and right) holds the inner one (top and left) on WinUI3
                this.Fill(canvas, kLight, right - m_lightThickness, top, right, bottom);
                this.Fill(canvas, kLight, left, bottom - m_lightThickness, right - m_lightThickness, bottom);
                this.Fill(canvas, kShadow, left, top, right - m_lightThickness, top + m_shadowThickness);
                this.Fill(canvas, kShadow, left, top + m_shadowThickness, left + m_shadowThickness, bottom - m_lightThickness);
                this.Fill(canvas, m_fill, left + m_shadowThickness, top + m_shadowThickness, right - m_lightThickness, bottom - m_lightThickness);
            }

            public override void SetAlpha (int alpha)
            {
                m_alpha = alpha;
                this.InvalidateSelf();
            }

            public override void SetColorFilter (ColorFilter? colorFilter)
            {
                m_paint.SetColorFilter(colorFilter);
                this.InvalidateSelf();
            }

            private void Fill (Canvas canvas, Color color, float left, float top, float right, float bottom)
            {
                m_paint.Color = new Color(color.R, color.G, color.B, (byte)(color.A * m_alpha / 255));
                canvas.DrawRect(left, top, right, bottom, m_paint);
            }
        }

        // The out of bounds warning glyph: a triangle in the error color with its exclamation mark cut out of it, the way
        //  UIKit's filled warning triangle reads, so whatever is behind the glyph shows through the mark.
        private sealed class WarningGlyphDrawable : Drawable
        {
            // Each a fraction of the glyph's height (or width, for the mark's thickness and the corner rounding)
            private const float kApex = 0.08f;
            private const float kBase = 0.94f;
            private const float kMarkTop = 0.38f;
            private const float kMarkBottom = 0.64f;
            private const float kDotCenter = 0.80f;
            private const float kMarkThickness = 0.12f;
            private const float kCornerRounding = 0.12f;

            // The dot's radius, a fraction of the mark's thickness, so it reads a little heavier than the bar
            private const float kDotRadius = 0.6f;

            private readonly Paint m_fill = new Paint(PaintFlags.AntiAlias);
            private readonly Paint m_cut = new Paint(PaintFlags.AntiAlias);
            private readonly Android.Graphics.Path m_triangle = new Android.Graphics.Path();
            private RectF m_bounds = new RectF();

            public WarningGlyphDrawable (Color color)
            {
                m_fill.Color = color;
                m_cut.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.Clear!));
                m_cut.StrokeCap = Paint.Cap.Round;
            }

            public override int Opacity => (int)Format.Translucent;

            public override void Draw (Canvas canvas)
            {
                float height = m_bounds.Height();
                float centerX = m_bounds.CenterX();

                // The mark clears what is under it, so the glyph is drawn on a layer of its own; drawn straight onto the
                //  canvas, the cut would clear whatever was painted behind the glyph as well
                int saved = canvas.SaveLayer(m_bounds, null);
                canvas.DrawPath(m_triangle, m_fill);
                canvas.DrawLine(centerX, m_bounds.Top + (height * kMarkTop), centerX, m_bounds.Top + (height * kMarkBottom), m_cut);
                canvas.DrawCircle(centerX, m_bounds.Top + (height * kDotCenter), m_cut.StrokeWidth * kDotRadius, m_cut);
                canvas.RestoreToCount(saved);
            }

            // The shape only changes with the size, so it is made here rather than on every draw
            protected override void OnBoundsChange (Rect bounds)
            {
                base.OnBoundsChange(bounds);
                m_bounds = new RectF(bounds);
                float width = m_bounds.Width();
                float height = m_bounds.Height();
                m_triangle.Reset();
                m_triangle.MoveTo(m_bounds.CenterX(), m_bounds.Top + (height * kApex));
                m_triangle.LineTo(m_bounds.Right, m_bounds.Top + (height * kBase));
                m_triangle.LineTo(m_bounds.Left, m_bounds.Top + (height * kBase));
                m_triangle.Close();
                m_fill.SetPathEffect(new CornerPathEffect(width * kCornerRounding));
                m_cut.StrokeWidth = width * kMarkThickness;
            }

            public override void SetAlpha (int alpha)
            {
                m_fill.Alpha = alpha;
                this.InvalidateSelf();
            }

            public override void SetColorFilter (ColorFilter? colorFilter)
            {
                m_fill.SetColorFilter(colorFilter);
                this.InvalidateSelf();
            }
        }
    }
}
