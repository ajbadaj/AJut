namespace AJut.UX.Controls
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut.DateAndTime;
    using DPUtils = DPUtils<DateTimeEditIngressControl>;

    // ===========[ DateTimeEditIngressControl ]=================================
    // WPF date and time editor. At rest its text box shows the value, in DisplayFormat or the culture's own short
    // form. Typed text is read by TextResolver (DateTimeTextResolver when there is none), so "in two weeks", "friday"
    // and "+3d" are as good as a written date. The button opens a popup holding DateValueEditControl and
    // TimeValueEditControl, whichever of the two IncludeDate and IncludeTime ask for.
    //
    // Typing lifecycle:
    //   Text changes   -> read as typed, to show whether it is valid and, in the tooltip, what it comes to
    //   Enter or focus leaving -> valid text becomes EditDateTime and is shown formatted; invalid text stays to be fixed
    //   Escape         -> the text goes back to the value held
    //
    // Picker lifecycle (as ColorEditIngressControl's):
    //   ShowEditDisplay goes true  -> cache EditDateTime, sync the pickers to it
    //   A date or time is picked   -> EditDateTime changes right away
    //   ShowEditDisplay goes false -> UserEditComplete, if the value changed
    //
    // What text comes to, and what a picked date or time does to the value, is DateTimeEditRules in AJut.UX, the same
    // on every stack.
    [TemplatePart(Name = nameof(PART_TextBox), Type = typeof(TextBox))]
    [TemplatePart(Name = nameof(PART_DateEditor), Type = typeof(DateValueEditControl))]
    [TemplatePart(Name = nameof(PART_TimeEditor), Type = typeof(TimeValueEditControl))]
    public class DateTimeEditIngressControl : Control, IUserEditNotifier
    {
        /// <summary>Opens the pickers, or closes them when they are open</summary>
        public static readonly RoutedCommand TogglePickersCommand = new RoutedCommand(nameof(TogglePickersCommand), typeof(DateTimeEditIngressControl));

        private bool m_isSyncingPickers;

        // Whether the text box holds something typed that hasn't become the value yet. Text that only shows the value is
        //  never read back in: a format can drop part of a value (its seconds, say), and reading it back would lose them.
        private bool m_hasUncommittedText;

        // The text the control last put in the text box to show the value. A text change that leaves the box holding
        //  exactly that is the control's own, not an edit, however late the TextChanged for it comes.
        private string m_shownText = String.Empty;

        // The value held when the pickers opened, while they are open
        private DateTime? m_editCache;

        // ===========[ Setup/Construction/Teardown ]===========================
        static DateTimeEditIngressControl ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DateTimeEditIngressControl), new FrameworkPropertyMetadata(typeof(DateTimeEditIngressControl)));
        }

        public DateTimeEditIngressControl ()
        {
            this.CommandBindings.Add(new CommandBinding(TogglePickersCommand, this.OnTogglePickersExecuted));
            this.EvaluateText();
        }

        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            // Named handlers, so a template applied again takes the old parts' handlers off them
            if (this.PART_TextBox != null)
            {
                this.PART_TextBox.TextChanged -= this.TextBox_OnTextChanged;
                this.PART_TextBox.PreviewKeyDown -= this.TextBox_OnPreviewKeyDown;
                this.PART_TextBox.LostKeyboardFocus -= this.TextBox_OnLostKeyboardFocus;
            }

            if (this.PART_DateEditor != null)
            {
                this.PART_DateEditor.EditDateChanged -= this.DateEditor_OnEditDateChanged;
            }

            if (this.PART_TimeEditor != null)
            {
                this.PART_TimeEditor.EditTimeChanged -= this.TimeEditor_OnEditTimeChanged;
            }

            this.PART_TextBox = this.GetTemplateChild(nameof(PART_TextBox)) as TextBox;
            this.PART_DateEditor = this.GetTemplateChild(nameof(PART_DateEditor)) as DateValueEditControl;
            this.PART_TimeEditor = this.GetTemplateChild(nameof(PART_TimeEditor)) as TimeValueEditControl;

            if (this.PART_TextBox != null)
            {
                this.PART_TextBox.TextChanged += this.TextBox_OnTextChanged;
                this.PART_TextBox.PreviewKeyDown += this.TextBox_OnPreviewKeyDown;
                this.PART_TextBox.LostKeyboardFocus += this.TextBox_OnLostKeyboardFocus;
            }

            if (this.PART_DateEditor != null)
            {
                this.PART_DateEditor.EditDateChanged += this.DateEditor_OnEditDateChanged;
            }

            if (this.PART_TimeEditor != null)
            {
                this.PART_TimeEditor.EditTimeChanged += this.TimeEditor_OnEditTimeChanged;
            }

            this.ShowValueAsText();
            this.SyncPickersToValue();
        }

        // ===========[ Events ]================================================
        /// <summary>
        /// Raised once a person has changed the value: when typed text is taken, and when the pickers close on a
        /// different value than they opened on. Values set from code or a binding don't raise it.
        /// </summary>
        public event EventHandler<UserEditAppliedEventArgs> UserEditComplete;

        // ===========[ Dependency Properties ]=================================
        public static readonly DependencyProperty EditDateTimeProperty = DPUtils.RegisterFP(_ => _.EditDateTime, (d, e) => d.OnEditDateTimeChanged(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault);
        /// <summary>
        /// The date and time being edited, or null for none. A date only control keeps it at midnight when it sets it,
        /// and a time only control leaves its date as it was.
        /// </summary>
        public DateTime? EditDateTime
        {
            get => (DateTime?)this.GetValue(EditDateTimeProperty);
            set => this.SetValue(EditDateTimeProperty, value);
        }

        public static readonly DependencyProperty IncludeDateProperty = DPUtils.Register(_ => _.IncludeDate, true, (d, e) => d.OnWhatIsShownChanged());
        /// <summary>Whether the date is edited. With this and <see cref="IncludeTime"/> both off, both are edited.</summary>
        public bool IncludeDate
        {
            get => (bool)this.GetValue(IncludeDateProperty);
            set => this.SetValue(IncludeDateProperty, value);
        }

        public static readonly DependencyProperty IncludeTimeProperty = DPUtils.Register(_ => _.IncludeTime, false, (d, e) => d.OnWhatIsShownChanged());
        /// <summary>Whether the time of day is edited</summary>
        public bool IncludeTime
        {
            get => (bool)this.GetValue(IncludeTimeProperty);
            set => this.SetValue(IncludeTimeProperty, value);
        }

        public static readonly DependencyProperty DisplayFormatProperty = DPUtils.Register(_ => _.DisplayFormat, (d, e) => d.OnWhatIsShownChanged());
        /// <summary>
        /// The .NET date and time format the value is shown in ("yyyy-MM-dd", "ddd, MMM d"). With none, it is the
        /// culture's short date, short time, or both, by what is edited.
        /// A format .NET can't use (one half typed, say) shows the culture's form as well, until it is fixed.
        /// </summary>
        public string DisplayFormat
        {
            get => (string)this.GetValue(DisplayFormatProperty);
            set => this.SetValue(DisplayFormatProperty, value);
        }

        public static readonly DependencyProperty TimeFormatProperty = DPUtils.Register(_ => _.TimeFormat, (d, e) => d.OnWhatIsShownChanged());
        /// <summary>
        /// The clock a time is shown and picked on: the culture's own (the default), twelve hour with AM and PM, or twenty
        /// four hour with neither. It sets the shown text while there is no <see cref="DisplayFormat"/>, and the time
        /// picker either way.
        /// </summary>
        public eTimeFormat TimeFormat
        {
            get => (eTimeFormat)this.GetValue(TimeFormatProperty);
            set => this.SetValue(TimeFormatProperty, value);
        }

        public static readonly DependencyProperty MinuteStepCarriesIntoHourProperty = DPUtils.Register(_ => _.MinuteStepCarriesIntoHour, true);
        /// <summary>
        /// Whether stepping the time picker's minute past either end of the hour moves the hour, as a clock does, which it
        /// does unless set otherwise, or goes round inside the hour. See <see cref="TimeValueEditControl.MinuteStepCarriesIntoHour"/>.
        /// </summary>
        public bool MinuteStepCarriesIntoHour
        {
            get => (bool)this.GetValue(MinuteStepCarriesIntoHourProperty);
            set => this.SetValue(MinuteStepCarriesIntoHourProperty, value);
        }

        public static readonly DependencyProperty TextResolverProperty = DPUtils.Register(_ => _.TextResolver, (d, e) => d.OnWhatIsShownChanged());
        /// <summary>
        /// What reads typed text and writes the value as text. With none, <see cref="DateTimeTextResolver.Default"/>
        /// does both.
        /// </summary>
        public IDateTimeTextResolver TextResolver
        {
            get => (IDateTimeTextResolver)this.GetValue(TextResolverProperty);
            set => this.SetValue(TextResolverProperty, value);
        }

        public static readonly DependencyProperty TreatEmptyAsInvalidProperty = DPUtils.Register(_ => _.TreatEmptyAsInvalid, (d, e) => d.EvaluateText());
        /// <summary>When true, empty text is invalid rather than a way to set no value</summary>
        public bool TreatEmptyAsInvalid
        {
            get => (bool)this.GetValue(TreatEmptyAsInvalidProperty);
            set => this.SetValue(TreatEmptyAsInvalidProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly);
        /// <summary>
        /// Shows the value without letting it change: it takes no typing, and its text is in italics. The picker button
        /// still opens the pickers, marked read-only, to show the value, and they take no picks. The default style
        /// takes this from the inherited <see cref="AJut.UX.AttachedProperties.Edit.IsReadOnlyProperty"/>, so a read-only
        /// PropertyGrid, or anything else that sets Edit.IsReadOnly, makes it read-only too.
        /// </summary>
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }

        public static readonly DependencyProperty UnsetTextPromptProperty = DPUtils.Register(_ => _.UnsetTextPrompt);
        /// <summary>Placeholder text shown while there is no value and nothing typed</summary>
        public string UnsetTextPrompt
        {
            get => (string)this.GetValue(UnsetTextPromptProperty);
            set => this.SetValue(UnsetTextPromptProperty, value);
        }

        public static readonly DependencyProperty PickerButtonContentProperty = DPUtils.Register(_ => _.PickerButtonContent);
        /// <summary>
        /// What the picker button shows. With none, it is the calendar glyph, or the clock glyph for a time only control.
        /// </summary>
        public object PickerButtonContent
        {
            get => this.GetValue(PickerButtonContentProperty);
            set => this.SetValue(PickerButtonContentProperty, value);
        }

        public static readonly DependencyProperty ShowEditDisplayProperty = DPUtils.Register(_ => _.ShowEditDisplay, false, (d, e) => d.OnShowEditDisplayChanged(e.NewValue));
        /// <summary>Whether the pickers are open</summary>
        public bool ShowEditDisplay
        {
            get => (bool)this.GetValue(ShowEditDisplayProperty);
            set => this.SetValue(ShowEditDisplayProperty, value);
        }

        private static readonly DependencyPropertyKey IsTextValidPropertyKey = DPUtils.RegisterReadOnly(_ => _.IsTextValid, true);
        public static readonly DependencyProperty IsTextValidProperty = IsTextValidPropertyKey.DependencyProperty;
        /// <summary>Whether the text in the box can be read as a value</summary>
        public bool IsTextValid
        {
            get => (bool)this.GetValue(IsTextValidProperty);
            private set => this.SetValue(IsTextValidPropertyKey, value);
        }

        private static readonly DependencyPropertyKey InvalidTextReasonPropertyKey = DPUtils.RegisterReadOnly(_ => _.InvalidTextReason);
        public static readonly DependencyProperty InvalidTextReasonProperty = InvalidTextReasonPropertyKey.DependencyProperty;
        /// <summary>Why the text in the box can't be read, or null when it can</summary>
        public string InvalidTextReason
        {
            get => (string)this.GetValue(InvalidTextReasonProperty);
            private set => this.SetValue(InvalidTextReasonPropertyKey, value);
        }

        private static readonly DependencyPropertyKey CurrentToolTipPropertyKey = DPUtils.RegisterReadOnly(_ => _.CurrentToolTip);
        public static readonly DependencyProperty CurrentToolTipProperty = CurrentToolTipPropertyKey.DependencyProperty;
        /// <summary>The tooltip over the control: what typed text will come to, or why it can't be read</summary>
        public string CurrentToolTip
        {
            get => (string)this.GetValue(CurrentToolTipProperty);
            private set => this.SetValue(CurrentToolTipPropertyKey, value);
        }

        private static readonly DependencyPropertyKey ShowsDatePropertyKey = DPUtils.RegisterReadOnly(_ => _.ShowsDate, true);
        public static readonly DependencyProperty ShowsDateProperty = ShowsDatePropertyKey.DependencyProperty;
        /// <summary>Whether the date is edited, which it also is when <see cref="IncludeDate"/> and <see cref="IncludeTime"/> are both off</summary>
        public bool ShowsDate
        {
            get => (bool)this.GetValue(ShowsDateProperty);
            private set => this.SetValue(ShowsDatePropertyKey, value);
        }

        private static readonly DependencyPropertyKey ShowsTimePropertyKey = DPUtils.RegisterReadOnly(_ => _.ShowsTime, false);
        public static readonly DependencyProperty ShowsTimeProperty = ShowsTimePropertyKey.DependencyProperty;
        /// <summary>Whether the time of day is edited, which it also is when <see cref="IncludeDate"/> and <see cref="IncludeTime"/> are both off</summary>
        public bool ShowsTime
        {
            get => (bool)this.GetValue(ShowsTimeProperty);
            private set => this.SetValue(ShowsTimePropertyKey, value);
        }

        public static readonly DependencyProperty FocusedBorderBrushProperty = DPUtils.RegisterFP(_ => _.FocusedBorderBrush, null, null, CoerceUtils.CallbackForBrush);
        public Brush FocusedBorderBrush
        {
            get => (Brush)this.GetValue(FocusedBorderBrushProperty);
            set => this.SetValue(FocusedBorderBrushProperty, value);
        }

        public static readonly DependencyProperty InvalidTextBorderBrushProperty = DPUtils.RegisterFP(_ => _.InvalidTextBorderBrush, null, null, CoerceUtils.CallbackForBrush);
        public Brush InvalidTextBorderBrush
        {
            get => (Brush)this.GetValue(InvalidTextBorderBrushProperty);
            set => this.SetValue(InvalidTextBorderBrushProperty, value);
        }

        public static readonly DependencyProperty InvalidTextGlowBrushProperty = DPUtils.RegisterFP(_ => _.InvalidTextGlowBrush, null, null, CoerceUtils.CallbackForBrush);
        /// <summary>
        /// The glow drawn around the control while its text is invalid. Its own color rather than the invalid border's,
        /// since a border color dark enough to read on a dark surface is too dark to glow on it.
        /// </summary>
        public Brush InvalidTextGlowBrush
        {
            get => (Brush)this.GetValue(InvalidTextGlowBrushProperty);
            set => this.SetValue(InvalidTextGlowBrushProperty, value);
        }

        public static readonly DependencyProperty InvalidForegroundSymbolProperty = DPUtils.RegisterFP(_ => _.InvalidForegroundSymbol, null, null, CoerceUtils.CallbackForBrush);
        public Brush InvalidForegroundSymbol
        {
            get => (Brush)this.GetValue(InvalidForegroundSymbolProperty);
            set => this.SetValue(InvalidForegroundSymbolProperty, value);
        }

        public static readonly DependencyProperty ButtonBackgroundProperty = DPUtils.RegisterFP(_ => _.ButtonBackground, null, null, CoerceUtils.CallbackForBrush);
        public Brush ButtonBackground
        {
            get => (Brush)this.GetValue(ButtonBackgroundProperty);
            set => this.SetValue(ButtonBackgroundProperty, value);
        }

        public static readonly DependencyProperty ButtonForegroundProperty = DPUtils.RegisterFP(_ => _.ButtonForeground, null, null, CoerceUtils.CallbackForBrush);
        public Brush ButtonForeground
        {
            get => (Brush)this.GetValue(ButtonForegroundProperty);
            set => this.SetValue(ButtonForegroundProperty, value);
        }

        public static readonly DependencyProperty ButtonHoverBackgroundProperty = DPUtils.RegisterFP(_ => _.ButtonHoverBackground, null, null, CoerceUtils.CallbackForBrush);
        public Brush ButtonHoverBackground
        {
            get => (Brush)this.GetValue(ButtonHoverBackgroundProperty);
            set => this.SetValue(ButtonHoverBackgroundProperty, value);
        }

        public static readonly DependencyProperty ButtonHoverForegroundProperty = DPUtils.RegisterFP(_ => _.ButtonHoverForeground, null, null, CoerceUtils.CallbackForBrush);
        public Brush ButtonHoverForeground
        {
            get => (Brush)this.GetValue(ButtonHoverForegroundProperty);
            set => this.SetValue(ButtonHoverForegroundProperty, value);
        }

        public static readonly DependencyProperty ButtonPressedBackgroundProperty = DPUtils.RegisterFP(_ => _.ButtonPressedBackground, null, null, CoerceUtils.CallbackForBrush);
        public Brush ButtonPressedBackground
        {
            get => (Brush)this.GetValue(ButtonPressedBackgroundProperty);
            set => this.SetValue(ButtonPressedBackgroundProperty, value);
        }

        public static readonly DependencyProperty ButtonPressedForegroundProperty = DPUtils.RegisterFP(_ => _.ButtonPressedForeground, null, null, CoerceUtils.CallbackForBrush);
        public Brush ButtonPressedForeground
        {
            get => (Brush)this.GetValue(ButtonPressedForegroundProperty);
            set => this.SetValue(ButtonPressedForegroundProperty, value);
        }

        public static readonly DependencyProperty TextBoxBorderBrushProperty = DPUtils.RegisterFP(_ => _.TextBoxBorderBrush, null, null, CoerceUtils.CallbackForBrush);
        /// <summary>The text box's inset edge, drawn along its top and left</summary>
        public Brush TextBoxBorderBrush
        {
            get => (Brush)this.GetValue(TextBoxBorderBrushProperty);
            set => this.SetValue(TextBoxBorderBrushProperty, value);
        }

        public static readonly DependencyProperty PopupBackgroundProperty = DPUtils.RegisterFP(_ => _.PopupBackground, null, null, CoerceUtils.CallbackForBrush);
        /// <summary>Behind the pickers in the popup</summary>
        public Brush PopupBackground
        {
            get => (Brush)this.GetValue(PopupBackgroundProperty);
            set => this.SetValue(PopupBackgroundProperty, value);
        }

        public static readonly DependencyProperty PopupForegroundProperty = DPUtils.RegisterFP(_ => _.PopupForeground, null, null, CoerceUtils.CallbackForBrush);
        /// <summary>The pickers' text and glyphs</summary>
        public Brush PopupForeground
        {
            get => (Brush)this.GetValue(PopupForegroundProperty);
            set => this.SetValue(PopupForegroundProperty, value);
        }

        // ===========[ Properties ]============================================
        private TextBox PART_TextBox { get; set; }
        private DateValueEditControl PART_DateEditor { get; set; }
        private TimeValueEditControl PART_TimeEditor { get; set; }

        // ===========[ Helper Methods ]========================================
        // ------ Typed text
        /// <summary>
        /// Makes the typed text the value, as Enter and focus leaving the text box do. Returns whether there was typed
        /// text and it was taken: text that can't be read stays as typed, shown invalid, and the value is left alone.
        /// </summary>
        internal bool CommitText ()
        {
            if (!m_hasUncommittedText || this.IsReadOnly || (this.PART_TextBox == null))
            {
                return false;
            }

            DateTimeEditRules.Evaluation evaluation = DateTimeEditRules.Evaluate(this.PART_TextBox.Text, this.TextResolver, this.BuildContext(), this.TreatEmptyAsInvalid);
            if (!evaluation.IsTextValid)
            {
                return false;
            }

            DateTime? oldValue = this.EditDateTime;
            m_hasUncommittedText = false;
            this.EditDateTime = evaluation.Value;
            this.ShowValueAsText();
            if (oldValue != evaluation.Value)
            {
                this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(oldValue, evaluation.Value));
            }

            return true;
        }

        private void TextBox_OnTextChanged (object sender, TextChangedEventArgs e)
        {
            // Text matching what the control showed is the value, whether the control just wrote it or a person typed
            //  their way back to it
            m_hasUncommittedText = !String.Equals(this.PART_TextBox.Text, m_shownText, StringComparison.Ordinal);
            this.EvaluateText();
        }

        private void TextBox_OnPreviewKeyDown (object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.CommitText();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                this.ShowValueAsText();
                e.Handled = true;
            }
        }

        private void TextBox_OnLostKeyboardFocus (object sender, KeyboardFocusChangedEventArgs e)
        {
            this.CommitText();
        }

        // Reads the text box as typed, for the invalid look and the tooltip. Nothing here changes the value.
        private void EvaluateText ()
        {
            DateTimeTextContext context = this.BuildContext();
            if (!m_hasUncommittedText || (this.PART_TextBox == null))
            {
                // The box shows the value, which is always valid, unless there is none and none is not allowed
                DateTimeEditRules.Evaluation ofValue = DateTimeEditRules.Evaluate(null, this.TextResolver, context, this.TreatEmptyAsInvalid);
                bool isValueFine = (this.EditDateTime != null) || ofValue.IsTextValid;
                this.IsTextValid = isValueFine;
                this.InvalidTextReason = isValueFine ? null : ofValue.InvalidTextReason;
                this.CurrentToolTip = isValueFine ? null : ofValue.InvalidTextReason;
                return;
            }

            DateTimeEditRules.Evaluation evaluation = DateTimeEditRules.Evaluate(this.PART_TextBox.Text, this.TextResolver, context, this.TreatEmptyAsInvalid);
            this.IsTextValid = evaluation.IsTextValid;
            this.InvalidTextReason = evaluation.InvalidTextReason;
            this.CurrentToolTip = evaluation.IsTextValid
                ? DateTimeEditRules.Format(evaluation.Value, this.TextResolver, context)
                : evaluation.InvalidTextReason;
        }

        // Puts the value in the text box, formatted, in place of whatever was typed there
        private void ShowValueAsText ()
        {
            m_hasUncommittedText = false;
            m_shownText = DateTimeEditRules.Format(this.EditDateTime, this.TextResolver, this.BuildContext());
            if (this.PART_TextBox != null)
            {
                this.PART_TextBox.Text = m_shownText;
            }

            this.EvaluateText();
        }

        private DateTimeTextContext BuildContext ()
        {
            return new DateTimeTextContext(DateTime.Now, this.EditDateTime, this.IncludeDate, this.IncludeTime, this.DisplayFormat, CultureInfo.CurrentCulture, this.TimeFormat);
        }

        // ------ Pickers
        // Read-only too: the pickers open to show the value, and take no picks (both are read-only with the control)
        private void OnTogglePickersExecuted (object sender, ExecutedRoutedEventArgs e)
        {
            this.ShowEditDisplay = !this.ShowEditDisplay;
        }

        private void OnShowEditDisplayChanged (bool isOpen)
        {
            if (isOpen)
            {
                // Anything typed is settled first, so the pickers open on the value the text box shows
                this.CommitText();
                m_editCache = this.EditDateTime;
                this.SyncPickersToValue();
                return;
            }

            if (m_editCache != this.EditDateTime)
            {
                this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(m_editCache, this.EditDateTime));
            }

            m_editCache = null;
        }

        private void SyncPickersToValue ()
        {
            m_isSyncingPickers = true;
            try
            {
                if (this.PART_DateEditor != null)
                {
                    this.PART_DateEditor.EditDate = this.EditDateTime?.Date;
                    if (this.EditDateTime == null)
                    {
                        this.PART_DateEditor.DisplayMonth = DateTime.Today;
                    }
                }

                if (this.PART_TimeEditor != null)
                {
                    // With no value yet the time editor starts on this minute, which a picked date then takes
                    DateTime now = DateTime.Now;
                    this.PART_TimeEditor.EditTime = this.EditDateTime?.TimeOfDay ?? new TimeSpan(now.Hour, now.Minute, 0);
                }
            }
            finally
            {
                m_isSyncingPickers = false;
            }
        }

        private void DateEditor_OnEditDateChanged (object sender, EventArgs e)
        {
            if (m_isSyncingPickers || this.IsReadOnly || (this.PART_DateEditor.EditDate == null))
            {
                return;
            }

            // The first date picked, where a time is shown too, is at the time the time editor shows
            DateTime? current = this.EditDateTime;
            if ((current == null) && this.ShowsTime && (this.PART_TimeEditor != null))
            {
                current = DateTime.Today + this.PART_TimeEditor.EditTime;
            }

            this.EditDateTime = DateTimeEditRules.WithPickedDate(current, this.PART_DateEditor.EditDate.Value);

            // With only a date to pick, picking it is the whole edit
            if (!this.ShowsTime)
            {
                this.ShowEditDisplay = false;
            }
        }

        private void TimeEditor_OnEditTimeChanged (object sender, EventArgs e)
        {
            if (m_isSyncingPickers || this.IsReadOnly)
            {
                return;
            }

            this.EditDateTime = DateTimeEditRules.WithPickedTimeOfDay(this.EditDateTime, this.PART_TimeEditor.EditTime, DateTime.Now);
        }

        // ------ Property change handlers
        private void OnEditDateTimeChanged ()
        {
            // A new value replaces whatever was typed, wherever it came from, and the pickers follow it while open
            this.ShowValueAsText();
            if (this.ShowEditDisplay)
            {
                this.SyncPickersToValue();
            }
        }

        // IncludeDate, IncludeTime, DisplayFormat, TimeFormat and TextResolver all change what the value reads as. Typed text is
        //  left alone and read again under the new terms.
        private void OnWhatIsShownChanged ()
        {
            DateTimeTextContext context = this.BuildContext();
            this.ShowsDate = context.ShowsDate;
            this.ShowsTime = context.ShowsTime;
            if (m_hasUncommittedText)
            {
                this.EvaluateText();
            }
            else
            {
                this.ShowValueAsText();
            }
        }
    }
}
