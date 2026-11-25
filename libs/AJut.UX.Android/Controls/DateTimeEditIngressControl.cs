namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Android.App;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics;
    using Android.Graphics.Drawables;
    using Android.Runtime;
    using Android.Text;
    using Android.Util;
    using Android.Views;
    using Android.Views.InputMethods;
    using Android.Widget;
    using AndroidColor = Android.Graphics.Color;

    /// <summary>
    /// A date and time field with a picker button. At rest it shows the value, in <see cref="DisplayFormat"/> or the
    /// culture's own short form; it takes free-form typed text ("tomorrow", "in two weeks", "friday 3pm", a written date),
    /// and the button opens Android's own date and time pickers in a dialog.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Typed text is read as it is typed, for the invalid look and the tooltip, and taken when the keyboard's Done action
    /// is pressed or the field loses focus. Text that can't be read stays, shown with the invalid border and the warning
    /// glyph, whose tooltip (a long press, or a pointer resting on it) says why. Escape on a hardware keyboard puts the
    /// value back.
    /// </para>
    /// <para>
    /// The button settles anything typed, then opens a dialog with the calendar, the time picker, or both, as
    /// <see cref="IncludeDate"/> and <see cref="IncludeTime"/> ask. A pick changes the value right away; with only a date
    /// to pick, picking a day closes the dialog. UserEditComplete fires when the dialog closes, if the value changed.
    /// Read-only, the field takes no typing, and the dialog still opens, marked read-only, to show the value and take no
    /// picks.
    /// </para>
    /// <para>
    /// What typed text comes to and when an edit is complete are AJut.UX's <see cref="DateTimeEditController"/>, which the
    /// AppKit and UIKit controls run too, over the rules WinUI3's and WPF's controls call, so the same text gets the same
    /// value on every stack. In a layout, ajutIncludeDate, ajutIncludeTime, ajutDisplayFormat, ajutTimeFormat, ajutUnsetTextPrompt,
    /// ajutTreatEmptyAsInvalid and ajutIsReadOnly set those.
    /// </para>
    /// </remarks>
    [Register("ajut.ux.controls.DateTimeEditIngressControl")]
    public class DateTimeEditIngressControl : LinearLayout, IUserEditNotifier, IDateTimeEditHost
    {
        // The look is MobileLook.DateTimeEditIngressControl's, which UIKit's control draws too: the keys and sizes come
        // from there, and only the colors each key falls back to with no theme loaded are Android's own
        private static readonly ThemedGradient kSurface = new ThemedGradient(
            MobileLook.DateTimeEditIngressControl.FrameSurface,
            new[] { ThemedGradient.Stop(0.0, 0xFFFDFDFD) },
            new[] { ThemedGradient.Stop(0.0, 0xFF2B2B2B) }
        );
        private static readonly ThemedColor kWell = ThemedColor.FromArgb(MobileLook.DateTimeEditIngressControl.WellSurface, 0xFFFFFFFF, 0xFF1E1E1E);
        private static readonly ThemedColor kNormalBorder = ThemedColor.FromArgb(MobileLook.DateTimeEditIngressControl.FrameBorder, 0xFF888888, 0xFF888888);
        private static readonly ThemedColor kFocusedBorder = ThemedColor.FromArgb(MobileLook.DateTimeEditIngressControl.FrameBorderFocused, 0xFF0078D4, 0xFF4CA3E8);
        private static readonly ThemedColor kInvalidBorder = ThemedColor.FromArgb(MobileLook.DateTimeEditIngressControl.FrameBorderInvalid, 0xFFE0524D, 0xFFE0524D);
        private static readonly ThemedColor kText = ThemedColor.FromArgb(MobileLook.DateTimeEditIngressControl.Text, 0xE4000000, 0xFFFFFFFF);
        private static readonly ThemedColor kPlaceholderText = ThemedColor.FromArgb(MobileLook.DateTimeEditIngressControl.PlaceholderText, 0x9E000000, 0xC5FFFFFF);
        private static readonly ThemedColor kWarningGlyph = ThemedColor.FromArgb(MobileLook.DateTimeEditIngressControl.WarningGlyph, 0xFFE0524D, 0xFFE0524D);
        private static readonly ThemedColor kButtonGlyph = ThemedColor.FromArgb(MobileLook.DateTimeEditIngressControl.ButtonGlyph, 0xE4000000, 0xFFFFFFFF);
        private static readonly ThemedColor kButtonPointerOver = ThemedColor.FromArgb(MobileLook.DateTimeEditIngressControl.ButtonPointerOverFill, 0xFF429CE3, 0xFF429CE3);
        private static readonly ThemedColor kButtonPressed = ThemedColor.FromArgb(MobileLook.DateTimeEditIngressControl.ButtonPressedFill, 0xFF0078D4, 0xFF0078D4);
        private static readonly ThemedColor kButtonHighlightGlyph = ThemedColor.FromArgb(MobileLook.DateTimeEditIngressControl.ButtonHighlightGlyph, 0xFFFFFFFF, 0xFFFFFFFF);

        private readonly DateTimeEditController m_controller;
        private GradientDrawable m_frame = null!;
        private EditText m_field = null!;
        private View m_warningGlyph = null!;
        private ImageView m_pickerButton = null!;
        private PickerGlyphDrawable m_defaultGlyph = null!;
        private Drawable? m_pickerButtonContent;
        private PickerDialog? m_openPickers;
        private float m_density = 1f;

        // ===========[ Setup/Construction/Teardown ]===================================
        public DateTimeEditIngressControl (Context context) : base(context)
        {
            m_controller = new DateTimeEditController(this);
            this.Initialize(context, null);
        }

        public DateTimeEditIngressControl (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            m_controller = new DateTimeEditController(this);
            this.Initialize(context, attrs);
        }

        public DateTimeEditIngressControl (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            m_controller = new DateTimeEditController(this);
            this.Initialize(context, attrs);
        }

        protected DateTimeEditIngressControl (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
            m_controller = new DateTimeEditController(this);
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
            m_pickerButton.Click -= this.OnPickerButtonClick;
            m_pickerButton.Click += this.OnPickerButtonClick;
        }

        // A dialog left open when its view goes away leaks the window, so a picker edit ends with the view
        protected override void OnDetachedFromWindow ()
        {
            this.ClosePickers();
            m_field.TextChanged -= this.OnFieldTextChanged;
            m_field.EditorAction -= this.OnFieldEditorAction;
            m_field.FocusChange -= this.OnFieldFocusChange;
            m_pickerButton.Click -= this.OnPickerButtonClick;
            base.OnDetachedFromWindow();
        }

        // ===========[ Events ]===================================
        /// <summary>Raised whenever <see cref="EditDateTime"/> changes: from typing, a picker, or code. Bind two-way through this.</summary>
        public event EventHandler<EventArgs<DateTime?>>? EditDateTimeChanged;

        /// <summary>
        /// Raised once a person has changed the value: when typed text is taken, and when the picker dialog closes on a
        /// different value than it opened on. Values set from code don't raise it. Old and new values are DateTime?.
        /// </summary>
        public event EventHandler<UserEditAppliedEventArgs>? UserEditComplete;

        // ===========[ Properties ]===================================
        /// <summary>
        /// The date and time being edited, or null for none. A date only control keeps it at midnight when it sets it, and
        /// a time only control leaves its date as it was.
        /// </summary>
        public DateTime? EditDateTime
        {
            get => m_controller.Value;
            set => m_controller.Value = value;
        }

        /// <summary>Whether the date is edited. With this and <see cref="IncludeTime"/> both off, both are edited.</summary>
        public bool IncludeDate
        {
            get => m_controller.IncludeDate;
            set
            {
                m_controller.IncludeDate = value;
                this.OnWhatIsEditedChanged();
            }
        }

        /// <summary>Whether the time of day is edited</summary>
        public bool IncludeTime
        {
            get => m_controller.IncludeTime;
            set
            {
                m_controller.IncludeTime = value;
                this.OnWhatIsEditedChanged();
            }
        }

        /// <summary>
        /// The .NET date and time format the value is shown in ("yyyy-MM-dd", "ddd, MMM d"). With none, it is the culture's
        /// short date, short time, or both, by what is edited.
        /// A format .NET can't use (one half typed, say) shows the culture's form as well, until it is fixed.
        /// </summary>
        public string? DisplayFormat
        {
            get => m_controller.DisplayFormat;
            set => m_controller.DisplayFormat = value;
        }

        /// <summary>
        /// The clock a time is shown and picked on: the culture's own (the default), twelve hour with AM and PM, or twenty
        /// four hour with neither. It sets the shown text while there is no <see cref="DisplayFormat"/>, and the time
        /// picker either way.
        /// </summary>
        public eTimeFormat TimeFormat
        {
            get => m_controller.TimeFormat;
            set => m_controller.TimeFormat = value;
        }

        /// <summary>What reads typed text and writes the value as text. With none, <see cref="DateTimeTextResolver.Default"/> does both.</summary>
        public IDateTimeTextResolver? TextResolver
        {
            get => m_controller.TextResolver;
            set => m_controller.TextResolver = value;
        }

        /// <summary>When true, empty text is invalid rather than a way to set no value</summary>
        public bool TreatEmptyAsInvalid
        {
            get => m_controller.TreatEmptyAsInvalid;
            set => m_controller.TreatEmptyAsInvalid = value;
        }

        /// <summary>
        /// Shows the value without letting it change: the field takes no typing, and its text is in italics. The picker
        /// button still opens the pickers, marked read-only, to show the value, and they take no picks.
        /// </summary>
        public bool IsReadOnly
        {
            get => m_controller.IsReadOnly;
            set
            {
                // Read-only looks as editable does but for the text, in italics and a touch faded
                const float kReadOnlyTextAlpha = 0.85f;
                m_controller.IsReadOnly = value;
                m_field.Enabled = !value;
                Typeface current = m_field.Typeface ?? Typeface.Default!;
                TypefaceStyle style = value ? (current.Style | TypefaceStyle.Italic) : (current.Style & ~TypefaceStyle.Italic);
                m_field.SetTypeface(Typeface.Create(current, style), style);
                m_field.Alpha = value ? kReadOnlyTextAlpha : 1f;
                m_openPickers?.ShowReadOnly(value);
            }
        }

        /// <summary>The prompt the field shows while there is no value and nothing typed</summary>
        public string? UnsetTextPrompt
        {
            get => m_field.Hint;
            set
            {
                m_field.Hint = value;
                this.ApplyDescriptions();
            }
        }

        /// <summary>
        /// The drawable the picker button shows, as it is given. With none, it is AJut's calendar glyph, or its clock glyph
        /// for a time only control, in the button's glyph colors.
        /// </summary>
        public Drawable? PickerButtonContent
        {
            get => m_pickerButtonContent;
            set
            {
                m_pickerButtonContent = value;
                m_pickerButton.SetImageDrawable(value ?? m_defaultGlyph);
            }
        }

        /// <summary>Whether the text in the field can be read as a value</summary>
        public bool IsTextValid => m_controller.IsTextValid;

        /// <summary>Why the text in the field can't be read, or null when it can</summary>
        public string? InvalidTextReason => m_controller.InvalidTextReason;

        /// <summary>The control's tooltip: what typed text will come to, or why it can't be read</summary>
        public string? CurrentToolTip => m_controller.CurrentToolTip;

        /// <summary>True while the picker dialog is open</summary>
        public bool ShowEditDisplay => m_openPickers != null;

        /// <summary>The text field, for styling it the way the app styles its other fields</summary>
        public EditText TextField => m_field;

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// Opens the pickers on the value, unless they are open already. Anything typed is settled first, so they open on
        /// the value the field shows. Read-only, they open marked read-only, to show it.
        /// </summary>
        public void OpenPickers ()
        {
            if ((m_openPickers != null) || !m_controller.BeginPickerEdit())
            {
                return;
            }

            m_openPickers = new PickerDialog(this, m_controller.ShowsDate, m_controller.ShowsTime, m_controller.IsReadOnly);
            m_pickerButton.Activated = true;
            m_openPickers.Show();
        }

        /// <summary>Closes the pickers if they are open, which completes the edit</summary>
        public void ClosePickers () => m_openPickers?.Close();

        /// <summary>Escape, from a hardware keyboard, puts the value back in place of what was typed</summary>
        public override bool DispatchKeyEvent (KeyEvent? e)
        {
            if ((e != null) && (e.KeyCode == Keycode.Escape) && m_field.HasFocus && m_controller.HasUncommittedText)
            {
                if (e.Action == KeyEventActions.Up)
                {
                    m_controller.ShowValueAsText();
                }

                return true;
            }

            return base.DispatchKeyEvent(e);
        }

        /// <summary>
        /// Makes typed text the value, when it reads as one, as Done and focus leaving the field do. Returns whether typed
        /// text was taken.
        /// </summary>
        internal bool CommitText () => m_controller.CommitText();

        /// <summary>The open dialog's calendar, or null while the pickers are closed or there is no date to pick</summary>
        internal DatePicker? OpenCalendar => m_openPickers?.Calendar;

        // ===========[ IDateTimeEditHost Implementations ]===================================
        void IDateTimeEditHost.ShowText (string text)
        {
            if ((m_field == null) || (m_field.Text == text))
            {
                return;
            }

            m_field.Text = text;
            if (m_field.HasFocus)
            {
                m_field.SetSelection(text.Length);
            }
        }

        void IDateTimeEditHost.OnValueChanged (bool isFromPerson)
        {
            m_openPickers?.ShowValue(m_controller.Value);
            this.EditDateTimeChanged?.Invoke(this, new EventArgs<DateTime?>(m_controller.Value));
        }

        void IDateTimeEditHost.ShowTextState ()
        {
            if (m_field == null)
            {
                return;
            }

            bool isInvalid = !m_controller.IsTextValid;
            m_warningGlyph.Visibility = isInvalid ? ViewStates.Visible : ViewStates.Gone;
            m_warningGlyph.TooltipText = m_controller.InvalidTextReason;
            m_warningGlyph.ContentDescription = m_controller.InvalidTextReason;
            this.TooltipText = m_controller.CurrentToolTip;
            this.PaintFrame();
        }

        void IDateTimeEditHost.RaiseUserEditComplete (DateTime? oldValue, DateTime? newValue)
        {
            this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(oldValue, newValue));
        }

        // ===========[ Helper Methods ]===================================
        private void Initialize (Context context, IAttributeSet? attrs)
        {
            m_density = context.Resources?.DisplayMetrics?.Density ?? 1f;
            this.Orientation = Android.Widget.Orientation.Horizontal;
            this.SetGravity(GravityFlags.CenterVertical);
            this.SetMinimumHeight((int)(MobileLook.DateTimeEditIngressControl.MinTouchHeight * m_density));

            int border = (int)Math.Max(1f, MobileLook.DateTimeEditIngressControl.BorderThickness * m_density);
            int wellInset = (int)(MobileLook.DateTimeEditIngressControl.WellInset * m_density);
            int textPadding = (int)(MobileLook.DateTimeEditIngressControl.WellTextPadding * m_density);
            float cornerRadius = MobileLook.DateTimeEditIngressControl.CornerRadius * m_density;

            // 1. The frame is this layout's own background: rounded, outlined, on the pop-out surface, padded by its border
            //    so the picker button's fill sits inside the outline
            m_frame = ThemeColors.ToDrawable(ThemeColors.Resolve(context, kSurface));
            m_frame.SetCornerRadius(cornerRadius);
            this.Background = m_frame;
            this.SetPadding(border, border, border, border);

            // 2. The field, in its well
            var well = new GradientDrawable();
            well.SetColor(ThemeColors.Resolve(context, kWell).ToAndroidColor());
            well.SetCornerRadius(Math.Max(0f, cornerRadius - border));
            m_field = new EditText(context)
            {
                Background = well,
                ImeOptions = ImeAction.Done,
                InputType = InputTypes.ClassText | InputTypes.TextFlagNoSuggestions,
            };
            m_field.SetSingleLine(true);
            m_field.SetPadding(textPadding, textPadding, textPadding, textPadding);
            m_field.SetTextColor(ThemeColors.Resolve(context, kText).ToAndroidColor());
            m_field.SetHintTextColor(ThemeColors.Resolve(context, kPlaceholderText).ToAndroidColor());
            this.AddView(m_field, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f) { LeftMargin = wellInset, TopMargin = wellInset, BottomMargin = wellInset });

            // 3. The warning glyph, shown while the text can't be read; its tooltip and description carry the reason
            int glyphSize = (int)(MobileLook.DateTimeEditIngressControl.WarningGlyphSize * m_density);
            int glyphSpacing = (int)(MobileLook.DateTimeEditIngressControl.WarningGlyphSpacing * m_density);
            m_warningGlyph = new View(context)
            {
                Visibility = ViewStates.Gone,
                Background = new WarningGlyphDrawable(ThemeColors.Resolve(context, kWarningGlyph).ToAndroidColor()),
            };
            this.AddView(m_warningGlyph, new LayoutParams(glyphSize, glyphSize) { LeftMargin = glyphSpacing, RightMargin = glyphSpacing });

            // 4. The picker button at the trailing end. The glyph lights up over the highlight fills.
            m_defaultGlyph = new PickerGlyphDrawable(
                new ColorStateList(
                    new[]
                    {
                        new[] { Android.Resource.Attribute.StatePressed },
                        new[] { Android.Resource.Attribute.StateActivated },
                        new[] { Android.Resource.Attribute.StateHovered },
                        Array.Empty<int>(),
                    },
                    new[]
                    {
                        ThemeColors.Resolve(context, kButtonHighlightGlyph).ToAndroidColor().ToArgb(),
                        ThemeColors.Resolve(context, kButtonHighlightGlyph).ToAndroidColor().ToArgb(),
                        ThemeColors.Resolve(context, kButtonHighlightGlyph).ToAndroidColor().ToArgb(),
                        ThemeColors.Resolve(context, kButtonGlyph).ToAndroidColor().ToArgb(),
                    }
                )
            );

            int glyphPadding = (int)(((MobileLook.DateTimeEditIngressControl.PickerButtonWidth - MobileLook.DateTimeEditIngressControl.PickerGlyphSize) / 2f) * m_density);
            m_pickerButton = new ImageView(context)
            {
                Clickable = true,
                Focusable = true,
                Background = this.MakePickerButtonFill(context, Math.Max(0f, cornerRadius - border)),
            };
            m_pickerButton.SetScaleType(ImageView.ScaleType.FitCenter);
            m_pickerButton.SetPadding(glyphPadding, 0, glyphPadding, 0);
            m_pickerButton.SetImageDrawable(m_defaultGlyph);
            FocusVisual.Apply(m_pickerButton);
            this.AddView(m_pickerButton, new LayoutParams((int)(MobileLook.DateTimeEditIngressControl.PickerButtonWidth * m_density), ViewGroup.LayoutParams.MatchParent));

            // 5. Layout attributes
            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutDateTimeEditIngressControl);
                m_controller.IncludeDate = values.GetBoolean(Resource.Styleable.AJutDateTimeEditIngressControl_ajutIncludeDate, true);
                m_controller.IncludeTime = values.GetBoolean(Resource.Styleable.AJutDateTimeEditIngressControl_ajutIncludeTime, false);
                m_controller.DisplayFormat = values.GetString(Resource.Styleable.AJutDateTimeEditIngressControl_ajutDisplayFormat);
                m_controller.TimeFormat = (eTimeFormat)values.GetInt(Resource.Styleable.AJutDateTimeEditIngressControl_ajutTimeFormat, (int)eTimeFormat.CurrentCulture);
                m_controller.TreatEmptyAsInvalid = values.GetBoolean(Resource.Styleable.AJutDateTimeEditIngressControl_ajutTreatEmptyAsInvalid, false);
                m_field.Hint = values.GetString(Resource.Styleable.AJutDateTimeEditIngressControl_ajutUnsetTextPrompt);
                bool isReadOnly = values.GetBoolean(Resource.Styleable.AJutDateTimeEditIngressControl_ajutIsReadOnly, false);
                values.Recycle();
                this.IsReadOnly = isReadOnly;
            }

            this.OnWhatIsEditedChanged();
            m_controller.ShowValueAsText();
        }

        // The picker button is the frame's own surface at rest, and fills with the highlight under a mouse or stylus, while
        //  pressed, and while its pickers are open (activated). Each fill is rounded on the frame's outer side, just inside
        //  the outline's rounded corners. Pressed is listed first: a state list draws the first entry the button's state
        //  matches, and a pressed button under a stylus is hovered too.
        private StateListDrawable MakePickerButtonFill (Context context, float innerRadius)
        {
            float[] radii = new[] { 0f, 0f, innerRadius, innerRadius, innerRadius, innerRadius, 0f, 0f };
            var states = new StateListDrawable();
            states.AddState(new[] { Android.Resource.Attribute.StatePressed }, MakeFill(ThemeColors.Resolve(context, kButtonPressed)));
            states.AddState(new[] { Android.Resource.Attribute.StateActivated }, MakeFill(ThemeColors.Resolve(context, kButtonPressed)));
            states.AddState(new[] { Android.Resource.Attribute.StateHovered }, MakeFill(ThemeColors.Resolve(context, kButtonPointerOver)));
            return states;

            GradientDrawable MakeFill (ColorArgb color)
            {
                var fill = new GradientDrawable();
                fill.SetColor(color.ToAndroidColor());
                fill.SetCornerRadii(radii);
                return fill;
            }
        }

        // Text that can't be read takes the invalid border, then focus the focused one
        private void PaintFrame ()
        {
            // Called from the setters, which can run before the frame is built
            if ((m_frame == null) || (m_field == null))
            {
                return;
            }

            Context context = this.Context!;
            ThemedColor border = MobileLook.DateTimeEditIngressControl.FrameBorderFor(!m_controller.IsTextValid, m_field.HasFocus) switch
            {
                MobileLook.DateTimeEditIngressControl.FrameBorderInvalid => kInvalidBorder,
                MobileLook.DateTimeEditIngressControl.FrameBorderFocused => kFocusedBorder,
                _ => kNormalBorder,
            };

            m_frame.SetStroke((int)Math.Max(1f, MobileLook.DateTimeEditIngressControl.BorderThickness * m_density), ThemeColors.Resolve(context, border).ToAndroidColor());
        }

        private void OnWhatIsEditedChanged ()
        {
            // Called from the setters, which can run before the button is built
            if (m_defaultGlyph == null)
            {
                return;
            }

            m_defaultGlyph.IsClock = !m_controller.ShowsDate;
            this.ApplyDescriptions();
        }

        private void ApplyDescriptions ()
        {
            if (m_field == null)
            {
                return;
            }

            bool showsDate = m_controller.ShowsDate;
            bool showsTime = m_controller.ShowsTime;
            string whatIsEdited = (showsDate && showsTime) ? "Date and time" : (showsDate ? "Date" : "Time");
            m_field.ContentDescription = String.IsNullOrEmpty(m_field.Hint) ? whatIsEdited : m_field.Hint;
            m_pickerButton.ContentDescription = showsDate ? "Pick a date" : "Pick a time";
        }

        private void OnFieldTextChanged (object? sender, TextChangedEventArgs e) => m_controller.OnTextEdited(m_field.Text);

        private void OnFieldEditorAction (object? sender, TextView.EditorActionEventArgs e)
        {
            e.Handled = false;
            if (e.ActionId == ImeAction.Done)
            {
                m_controller.CommitText();
            }
        }

        private void OnFieldFocusChange (object? sender, View.FocusChangeEventArgs e)
        {
            if (!e.HasFocus)
            {
                m_controller.CommitText();
            }

            this.PaintFrame();
        }

        private void OnPickerButtonClick (object? sender, EventArgs e) => this.OpenPickers();

        // Called by the open pickers once their dialog has gone, however it went
        private void OnPickersClosed ()
        {
            m_openPickers = null;
            m_pickerButton.Activated = false;
            m_controller.EndPickerEdit();
        }

        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// One open picker edit: the dialog, the calendar and the time picker, with every subscription between them and the
        /// control. It is made when the pickers open and dropped when they close, so nothing outlives an edit.
        /// </summary>
        private sealed class PickerDialog
        {
            private readonly DateTimeEditIngressControl m_owner;
            private readonly TextView m_readOnlyNote;
            private readonly DatePicker? m_calendar;
            private readonly TimePicker? m_timePicker;
            private readonly DateListener? m_dateListener;
            private readonly AlertDialog m_dialog;
            private bool m_isSyncing;
            private bool m_isClosed;

            public PickerDialog (DateTimeEditIngressControl owner, bool showsDate, bool showsTime, bool isReadOnly)
            {
                const float kReadOnlyNoteAlpha = 0.7f;
                m_owner = owner;
                Context context = owner.Context!;
                float density = owner.m_density;
                DateTime shown = owner.m_controller.Value ?? DateTime.Now;

                // 1. The calendar, the time picker, or both, in a scroller: both together are taller than a phone held
                //    sideways. A date picker reports its changes through the listener Init gives it. Over them, the note
                //    shown while the control is read-only, when they open only to show the value.
                var content = new LinearLayout(context) { Orientation = Android.Widget.Orientation.Vertical };
                content.SetGravity(GravityFlags.CenterHorizontal);
                m_readOnlyNote = new TextView(context) { Text = "Read-only", Alpha = kReadOnlyNoteAlpha };
                m_readOnlyNote.SetTypeface(m_readOnlyNote.Typeface, TypefaceStyle.Italic);
                content.AddView(m_readOnlyNote);
                if (showsDate)
                {
                    m_dateListener = new DateListener(this);
                    m_calendar = new DatePicker(context);
                    m_calendar.Init(shown.Year, shown.Month - 1, shown.Day, m_dateListener);
                    content.AddView(m_calendar);
                }

                if (showsTime)
                {
                    m_timePicker = new TimePicker(context);
                    // The clock the text is shown on, decided the same way, rather than the system's own 24-hour setting
                    m_timePicker.SetIs24HourView(Java.Lang.Boolean.ValueOf(!owner.m_controller.UsesTwelveHourClock));
                    this.ShowTimeOfDay(shown);
                    var layout = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
                    if (showsDate)
                    {
                        layout.TopMargin = (int)(MobileLook.DateTimeEditIngressControl.PickerSpacing * density);
                    }

                    content.AddView(m_timePicker, layout);
                    m_timePicker.TimeChanged += this.OnTimeChanged;
                }

                this.ShowReadOnly(isReadOnly);

                var scroller = new ScrollView(context);
                scroller.AddView(content);

                // 2. The dialog. However it goes away (Done, back, a tap outside, the control closing it) the dismiss
                //    handler ends the edit.
                m_dialog = new AlertDialog.Builder(context)
                    .SetView(scroller)!
                    .SetPositiveButton("Done", this.OnDoneClicked)!
                    .Create()!;
                m_dialog.DismissEvent += this.OnDialogDismissed;
            }

            public DatePicker? Calendar => m_calendar;

            public void Show () => m_dialog.Show();

            public void Close ()
            {
                if (!m_isClosed)
                {
                    m_dialog.Dismiss();
                }
            }

            /// <summary>
            /// Marks the pickers read-only, or not. The time picker is simply off. A calendar that is off stops paging
            /// through the months, so it stays on, and a day tapped is put straight back (OnDateChanged).
            /// </summary>
            public void ShowReadOnly (bool isReadOnly)
            {
                m_readOnlyNote.Visibility = isReadOnly ? ViewStates.Visible : ViewStates.Gone;
                if (m_timePicker != null)
                {
                    m_timePicker.Enabled = !isReadOnly;
                }
            }

            /// <summary>Brings the pickers to <paramref name="value"/>, as a pick or the control changed it, without reporting back</summary>
            public void ShowValue (DateTime? value)
            {
                if (value == null)
                {
                    return;
                }

                m_isSyncing = true;
                try
                {
                    DateTime shown = value.Value;
                    if ((m_calendar != null)
                        && ((m_calendar.Year != shown.Year) || (m_calendar.Month != shown.Month - 1) || (m_calendar.DayOfMonth != shown.Day)))
                    {
                        m_calendar.UpdateDate(shown.Year, shown.Month - 1, shown.Day);
                    }

                    this.ShowTimeOfDay(shown);
                }
                finally
                {
                    m_isSyncing = false;
                }
            }

            private void ShowTimeOfDay (DateTime shown)
            {
                if ((m_timePicker != null) && ((m_timePicker.Hour != shown.Hour) || (m_timePicker.Minute != shown.Minute)))
                {
                    m_timePicker.Hour = shown.Hour;
                    m_timePicker.Minute = shown.Minute;
                }
            }

            // A day picked with only a date to pick is the whole edit, so the dialog closes. Android's calendar also
            //  reports a year picked from its year list, as the same month and day in that year: that moves the value, but
            //  leaves the dialog open for the day to be picked, or the year list would close it before a day could be.
            private void OnDateChanged (int year, int monthOfYear, int dayOfMonth)
            {
                if (m_isSyncing || m_isClosed)
                {
                    return;
                }

                if (m_owner.m_controller.IsReadOnly)
                {
                    this.ShowValue(m_owner.m_controller.Value ?? DateTime.Now);
                    return;
                }

                DateTime? before = m_owner.m_controller.Value;
                var picked = new DateTime(year, monthOfYear + 1, dayOfMonth);
                bool isYearOnly = (before != null) && (before.Value.Month == picked.Month) && (before.Value.Day == picked.Day);
                if (m_owner.m_controller.PickDate(picked) && !isYearOnly)
                {
                    this.Close();
                }
            }

            private void OnTimeChanged (object? sender, TimePicker.TimeChangedEventArgs e)
            {
                if (!m_isSyncing && !m_isClosed)
                {
                    m_owner.m_controller.PickTimeOfDay(new TimeSpan(e.HourOfDay, e.Minute, 0));
                }
            }

            private void OnDoneClicked (object? sender, DialogClickEventArgs e) => this.Close();

            private void OnDialogDismissed (object? sender, EventArgs e)
            {
                m_isClosed = true;
                if (m_timePicker != null)
                {
                    m_timePicker.TimeChanged -= this.OnTimeChanged;
                }

                m_dateListener?.Release();
                m_dialog.DismissEvent -= this.OnDialogDismissed;
                m_owner.OnPickersClosed();
            }

            // The date picker's listener. Java holds it for as long as the picker does, so it lets go of the dialog once the
            //  edit is over, and a picker that outlives its dialog leads back to nothing.
            private sealed class DateListener : Java.Lang.Object, DatePicker.IOnDateChangedListener
            {
                private PickerDialog? m_pickers;

                public DateListener (PickerDialog pickers)
                {
                    m_pickers = pickers;
                }

                public void OnDateChanged (DatePicker? view, int year, int monthOfYear, int dayOfMonth)
                {
                    m_pickers?.OnDateChanged(year, monthOfYear, dayOfMonth);
                }

                public void Release () => m_pickers = null;
            }
        }

        /// <summary>
        /// AJut's own picker glyph, drawn in strokes so it needs no resources: a calendar page (a box, a band across its top,
        /// and two rings above it), or a clock face with its hands at ten past ten. Its color follows the button's state.
        /// </summary>
        private sealed class PickerGlyphDrawable : Drawable
        {
            // Each a fraction of the glyph's size
            private const float kStroke = 0.09f;
            private const float kPageTop = 0.2f;
            private const float kBandBottom = 0.4f;
            private const float kRingTop = 0.06f;
            private const float kRingInset = 0.28f;
            private const float kCornerRadius = 0.1f;
            private const float kMinuteHandLength = 0.3f;
            private const float kHourHandLength = 0.2f;

            // Ten past ten puts each hand 60 degrees off the vertical, one to either side: these are its sine and cosine
            private const float kHandAcross = 0.866f;
            private const float kHandUp = 0.5f;

            private readonly Paint m_paint = new Paint(PaintFlags.AntiAlias);
            private readonly ColorStateList m_colors;
            private bool m_isClock;

            public PickerGlyphDrawable (ColorStateList colors)
            {
                m_colors = colors;
                m_paint.SetStyle(Paint.Style.Stroke!);
                m_paint.StrokeCap = Paint.Cap.Round;
                m_paint.StrokeJoin = Paint.Join.Round;
                m_paint.Color = new AndroidColor(colors.DefaultColor);
            }

            public bool IsClock
            {
                get => m_isClock;
                set
                {
                    m_isClock = value;
                    this.InvalidateSelf();
                }
            }

            public override bool IsStateful => true;

            public override int Opacity => (int)Format.Translucent;

            public override void Draw (Canvas canvas)
            {
                Rect bounds = this.Bounds;
                float size = Math.Min(bounds.Width(), bounds.Height());
                float left = bounds.CenterX() - (size / 2f);
                float top = bounds.CenterY() - (size / 2f);
                m_paint.StrokeWidth = Math.Max(1f, size * kStroke);
                float inset = m_paint.StrokeWidth / 2f;

                if (m_isClock)
                {
                    float centerX = left + (size / 2f);
                    float centerY = top + (size / 2f);
                    canvas.DrawCircle(centerX, centerY, (size / 2f) - inset, m_paint);
                    canvas.DrawLine(centerX, centerY, centerX + (size * kMinuteHandLength * kHandAcross), centerY - (size * kMinuteHandLength * kHandUp), m_paint);
                    canvas.DrawLine(centerX, centerY, centerX - (size * kHourHandLength * kHandAcross), centerY - (size * kHourHandLength * kHandUp), m_paint);
                    return;
                }

                var page = new RectF(left + inset, top + (size * kPageTop), left + size - inset, top + size - inset);
                canvas.DrawRoundRect(page, size * kCornerRadius, size * kCornerRadius, m_paint);
                canvas.DrawLine(page.Left, top + (size * kBandBottom), page.Right, top + (size * kBandBottom), m_paint);
                canvas.DrawLine(left + (size * kRingInset), top + (size * kRingTop), left + (size * kRingInset), top + (size * kPageTop) + inset, m_paint);
                canvas.DrawLine(left + size - (size * kRingInset), top + (size * kRingTop), left + size - (size * kRingInset), top + (size * kPageTop) + inset, m_paint);
            }

            public override void SetAlpha (int alpha)
            {
                m_paint.Alpha = alpha;
                this.InvalidateSelf();
            }

            public override void SetColorFilter (ColorFilter? colorFilter)
            {
                m_paint.SetColorFilter(colorFilter);
                this.InvalidateSelf();
            }

            protected override bool OnStateChange (int[]? state)
            {
                int color = m_colors.GetColorForState(state, new AndroidColor(m_colors.DefaultColor));
                if (m_paint.Color.ToArgb() == color)
                {
                    return false;
                }

                m_paint.Color = new AndroidColor(color);
                this.InvalidateSelf();
                return true;
            }
        }
    }
}
