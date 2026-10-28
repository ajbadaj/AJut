namespace AJut.UX.Controls
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Content.Res;
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
    /// An edit is committed when the keyboard's Done action is pressed or the field loses focus. Out of bounds text is
    /// flagged and left for the user to fix, or snapped to the bound on commit, as <see cref="OutOfBoundsResponse"/> says.
    /// In a layout, ajutMinimum, ajutMaximum, ajutNudge, ajutDecimalPlacesAllowed, ajutOutOfBoundsResponse,
    /// ajutEnforceNumericType and ajutLabel set those.
    /// </remarks>
    [Register("ajut.ux.controls.NumericEditor")]
    public class NumericEditor : LinearLayout, INumericEditorHost
    {
        private const float kButtonWidthDp = 44f;
        private const float kCornerRadiusDp = 4f;
        private const float kBorderThicknessDp = 1f;
        private const int kRepeatDelayMs = 400;
        private const int kRepeatIntervalMs = 80;

        private static readonly ThemedColor kNormalBorder = ThemedColor.FromArgb(AJutThemeKeys.NumericEditor_NormalBorderBrush, 0xFF888888, 0xFF888888);
        private static readonly ThemedColor kFocusBorder = ThemedColor.FromArgb(AJutThemeKeys.NumericEditor_FocusBorderBrush, 0xFF0078D4, 0xFF4CA3E8);
        private static readonly ThemedColor kErrorBorder = ThemedColor.FromArgb(AJutThemeKeys.NumericEditor_ErrorBrush, 0xFFE0524D, 0xFFE0524D);
        private static readonly ThemedColor kButtonPressed = ThemedColor.FromArgb(AJutThemeKeys.NumericEditor_IncreasePointerOverBackground, 0x26000000, 0x33FFFFFF);
        private static readonly ThemedColor kForeground = ThemedColor.FromArgb("TextFillColorPrimaryBrush", 0xE4000000, 0xFFFFFFFF);
        private static readonly ThemedColor kFieldForeground = ThemedColor.FromArgb("TextControlForeground", 0xE4000000, 0xFFFFFFFF);
        private static readonly ThemedColor kFieldForegroundDisabled = ThemedColor.FromArgb("TextControlForegroundDisabled", 0x5C000000, 0x5DFFFFFF);

        private readonly NumericEditorController m_controller;
        private readonly GradientDrawable m_frame = new GradientDrawable();
        private TextView m_label = null!;
        private EditText m_field = null!;
        private TextView m_decrease = null!;
        private TextView m_increase = null!;
        private Handler? m_repeatHandler;
        private RepeatNudge? m_repeat;
        private object m_value = 0.0;

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        private Type? m_enforceNumericType;

        private double m_minimum = double.NegativeInfinity;
        private double m_maximum = double.PositiveInfinity;
        private double m_nudge = 1.0;
        private int m_decimalPlacesAllowed = -1;
        private eOutOfBoundsResponse m_outOfBoundsResponse = eOutOfBoundsResponse.ErrorAndToolTip;
        private string? m_errorMessage;
        private bool m_isReadOnly;
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

        /// <summary>The label shown in front of the field; null or empty for none.</summary>
        public string? Label
        {
            get => m_label.Text;
            set
            {
                m_label.Text = value;
                m_label.Visibility = string.IsNullOrEmpty(value) ? ViewStates.Gone : ViewStates.Visible;
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
            m_field.Error = errorMessage;
            this.PaintFrame();
        }

        // ===========[ Helper Methods ]===================================
        private void Initialize (Context context, IAttributeSet? attrs)
        {
            m_density = context.Resources?.DisplayMetrics?.Density ?? 1f;
            this.Orientation = Android.Widget.Orientation.Horizontal;
            this.SetGravity(GravityFlags.CenterVertical);

            // 1. Label, minus, field, plus
            m_label = new TextView(context) { Visibility = ViewStates.Gone };
            m_label.SetPadding(0, 0, (int)(8 * m_density), 0);
            m_decrease = this.MakeStepButton(context, "-", "Decrease");
            m_increase = this.MakeStepButton(context, "+", "Increase");
            m_field = new EditText(context)
            {
                Gravity = GravityFlags.Center,
                ImeOptions = ImeAction.Done,
                InputType = InputTypes.ClassNumber | InputTypes.NumberFlagSigned | InputTypes.NumberFlagDecimal,
                Background = null,
            };
            m_field.SetSingleLine(true);
            m_field.SetMinEms(3);

            // The number is text control text, as on WinUI3, and dims while read-only (the field is disabled then)
            int fieldText = ThemeColors.Resolve(context, kFieldForeground).ToAndroidColor().ToArgb();
            int fieldTextDisabled = ThemeColors.Resolve(context, kFieldForegroundDisabled).ToAndroidColor().ToArgb();
            m_field.SetTextColor(new ColorStateList(
                new[] { new[] { -Android.Resource.Attribute.StateEnabled }, Array.Empty<int>() },
                new[] { fieldTextDisabled, fieldText }
            ));

            // The minus, field and plus share one bordered frame; the label sits outside it.
            var frame = new LinearLayout(context) { Orientation = Android.Widget.Orientation.Horizontal, Background = m_frame };
            frame.SetGravity(GravityFlags.CenterVertical);
            m_frame.SetCornerRadius(kCornerRadiusDp * m_density);
            frame.AddView(m_decrease, new LayoutParams((int)(kButtonWidthDp * m_density), ViewGroup.LayoutParams.MatchParent));
            frame.AddView(m_field, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            frame.AddView(m_increase, new LayoutParams((int)(kButtonWidthDp * m_density), ViewGroup.LayoutParams.MatchParent));

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

        private TextView MakeStepButton (Context context, string glyph, string description)
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

            button.SetTextColor(ThemeColors.Resolve(context, kForeground).ToAndroidColor());
            var pressed = new GradientDrawable();
            pressed.SetColor(ThemeColors.Resolve(context, kButtonPressed).ToAndroidColor());
            var states = new StateListDrawable();
            states.AddState(new[] { Android.Resource.Attribute.StatePressed }, pressed);
            button.Background = states;
            return button;
        }

        private void PaintFrame ()
        {
            Context context = this.Context!;
            ThemedColor border = (m_errorMessage != null) ? kErrorBorder : (m_field.HasFocus ? kFocusBorder : kNormalBorder);
            m_frame.SetStroke((int)Math.Max(1f, kBorderThicknessDp * m_density), ThemeColors.Resolve(context, border).ToAndroidColor());
        }

        private void StepOnce (bool positive)
        {
            if (!m_isReadOnly)
            {
                m_controller.Nudge(positive, m_nudge);
            }
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

        // A tap steps once; holding steps again after a short delay, then repeatedly until the finger lifts.
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
                    this.StopRepeating();
                    this.StepOnce(positive);
                    m_repeatHandler ??= new Handler(Looper.MainLooper!);
                    m_repeat = new RepeatNudge(this, positive);
                    m_repeatHandler.PostDelayed(m_repeat, kRepeatDelayMs);
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

            public RepeatNudge (NumericEditor owner, bool positive)
            {
                m_owner = owner;
                m_positive = positive;
            }

            public void Run ()
            {
                if (!ReferenceEquals(m_owner.m_repeat, this))
                {
                    return;
                }

                m_owner.StepOnce(m_positive);
                m_owner.m_repeatHandler?.PostDelayed(this, kRepeatIntervalMs);
            }
        }
    }
}
