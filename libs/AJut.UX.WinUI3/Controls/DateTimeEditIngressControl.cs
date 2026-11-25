namespace AJut.UX.Controls
{
    using System;
    using System.Globalization;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Input;
    using DPUtils = AJut.UX.DPUtils<DateTimeEditIngressControl>;

    // ===========[ DateTimeEditIngressControl ]=================================
    // WinUI3 date and time editor. At rest its text box shows the value, in DisplayFormat or the culture's own short
    // form. Typed text is read by TextResolver (DateTimeTextResolver when there is none), so "in two weeks", "friday"
    // and "+3d" are as good as a written date. The button opens a Flyout holding WinUI3's own CalendarView and
    // TimePicker, whichever of the two IncludeDate and IncludeTime ask for.
    //
    // Typing lifecycle:
    //   Text changes   -> read as typed, to show whether it is valid and, in the tooltip, what it comes to
    //   Enter or focus leaving -> valid text becomes EditDateTime and is shown formatted; invalid text stays to be fixed
    //   Escape         -> the text goes back to the value held
    //
    // Picker lifecycle (as ColorEditIngressControl's):
    //   Button         -> cache EditDateTime, sync the pickers to it, show the flyout
    //   A date or time is picked -> EditDateTime changes right away
    //   Flyout closed  -> UserEditComplete, if the value changed
    //
    // What text comes to, and what a picked date or time does to the value, is DateTimeEditRules in AJut.UX, the same
    // on every stack.
    //
    // Template parts:
    //   PART_Root         - outer Border (BorderBrush driven by the BorderStates VSM group)
    //   PART_TextBox      - the text box the value is shown and typed in
    //   PART_PickerButton - opens the flyout
    //
    // VSM groups:
    //   BorderStates        - Normal / Focused / TextInvalid
    //   TextBoxFocusStates  - TextBoxUnfocused / TextBoxFocused
    //   WarningDisplayStates - WarningHidden / WarningShown
    //   PickerGlyphStates   - CalendarGlyph / ClockGlyph / CustomPickerContent
    //   ReadOnlyStates      - Editable / ReadOnly

    [TemplatePart(Name = nameof(PART_Root), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_TextBox), Type = typeof(TextBox))]
    [TemplatePart(Name = nameof(PART_PickerButton), Type = typeof(ButtonBase))]
    public class DateTimeEditIngressControl : Control, IUserEditNotifier
    {
        // What CalendarView is handed and hands back is a moment, which it places on a day by its own time zone. A date
        //  goes in at the middle of its day, so no zone or daylight saving shift can move it onto the day before or after.
        private const int kMiddayHour = 12;

        private Flyout m_flyout;
        private CalendarView m_calendarView;
        private TimePicker m_timePicker;
        private TextBlock m_readOnlyNote;

        private bool m_isSyncingPickers;
        private bool m_isFocused;

        // Whether the text box holds something typed that hasn't become the value yet. Text that only shows the value is
        //  never read back in: a format can drop part of a value (its seconds, say), and reading it back would lose them.
        private bool m_hasUncommittedText;

        // The text the control last put in the text box to show the value. A text change that leaves the box holding
        //  exactly that is the control's own, not an edit. A flag raised around the write can't tell: WinUI3 raises
        //  TextChanged after the write has returned, once the flag is down again, so the shown value was read back as
        //  typed text, and a resolver that can't read its own format back ("Q4 2026") called it invalid.
        private string m_shownText = String.Empty;

        // The value held when the flyout opened, while it is open
        private DateTime? m_editCache;
        private bool m_isPickerEditActive;

        // ===========[ Setup/Construction/Teardown ]===========================
        public DateTimeEditIngressControl ()
        {
            this.DefaultStyleKey = typeof(DateTimeEditIngressControl);
            this.GotFocus += this.OnGotFocus;
            this.LostFocus += this.OnLostFocus;
        }

        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            // Unhook the previous template's children
            if (this.PART_TextBox != null)
            {
                this.PART_TextBox.TextChanged -= this.TextBox_OnTextChanged;
                this.PART_TextBox.KeyDown -= this.TextBox_OnKeyDown;
                this.PART_TextBox.LostFocus -= this.TextBox_OnLostFocus;
            }

            if (this.PART_PickerButton != null)
            {
                this.PART_PickerButton.Click -= this.PickerButton_OnClick;
            }

            this.PART_Root = (Border)this.GetTemplateChild(nameof(PART_Root));
            this.PART_TextBox = (TextBox)this.GetTemplateChild(nameof(PART_TextBox));
            this.PART_PickerButton = (ButtonBase)this.GetTemplateChild(nameof(PART_PickerButton));

            if (this.PART_TextBox != null)
            {
                this.PART_TextBox.TextChanged += this.TextBox_OnTextChanged;
                this.PART_TextBox.KeyDown += this.TextBox_OnKeyDown;
                this.PART_TextBox.LostFocus += this.TextBox_OnLostFocus;
            }

            if (this.PART_PickerButton != null)
            {
                this.PART_PickerButton.Click += this.PickerButton_OnClick;
            }

            // The flyout and its pickers are built once, in code, and reused across opens, as ColorEditIngressControl's
            //  are: GetTemplateChild can't reach what sits inside a flyout attached in a WinUI3 ControlTemplate
            if (m_flyout == null)
            {
                m_calendarView = new CalendarView { SelectionMode = CalendarViewSelectionMode.Single };
                m_calendarView.SelectedDatesChanged += this.CalendarView_OnSelectedDatesChanged;

                m_timePicker = new TimePicker { HorizontalAlignment = HorizontalAlignment.Stretch };
                m_timePicker.SelectedTimeChanged += this.TimePicker_OnSelectedTimeChanged;

                // Shown over the pickers while the control is read-only, when they open only to show the value
                const double kReadOnlyNoteOpacity = 0.7;
                m_readOnlyNote = new TextBlock
                {
                    Text = "Read-only",
                    FontStyle = Windows.UI.Text.FontStyle.Italic,
                    Opacity = kReadOnlyNoteOpacity,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4),
                };

                var pickers = new StackPanel();
                pickers.Children.Add(m_readOnlyNote);
                pickers.Children.Add(m_calendarView);
                pickers.Children.Add(m_timePicker);

                m_flyout = new Flyout
                {
                    Content = pickers,
                    Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
                };
                m_flyout.Closed += this.Flyout_OnClosed;
            }

            this.ShowValueAsText();
            this.UpdateVisualState();
        }

        // ===========[ Events ]================================================
        /// <summary>
        /// Raised once a person has changed the value: when typed text is taken, and when the picker flyout closes on a
        /// different value than it opened on. Values set from code or a binding don't raise it.
        /// </summary>
        public event EventHandler<UserEditAppliedEventArgs> UserEditComplete;

        // ===========[ Dependency Properties ]=================================
        public static readonly DependencyProperty EditDateTimeProperty = DPUtils.Register(_ => _.EditDateTime, (d, e) => d.OnEditDateTimeChanged());
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

        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly, (d, e) => d.UpdateVisualState());
        /// <summary>
        /// Shows the value without letting it change: it takes no typing, and its text is in italics. The picker button
        /// still opens the pickers, marked read-only, to show the value, and they take no picks.
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

        public static readonly DependencyProperty PickerButtonContentProperty = DPUtils.Register(_ => _.PickerButtonContent, (d, e) => d.UpdateVisualState());
        /// <summary>
        /// What the picker button shows. With none, it is the calendar glyph, or the clock glyph for a time only control.
        /// </summary>
        public object PickerButtonContent
        {
            get => this.GetValue(PickerButtonContentProperty);
            set => this.SetValue(PickerButtonContentProperty, value);
        }

        public static readonly DependencyProperty IsTextValidProperty = DPUtils.Register(_ => _.IsTextValid, true);
        /// <summary>Whether the text in the box can be read as a value. Set by the control.</summary>
        public bool IsTextValid
        {
            get => (bool)this.GetValue(IsTextValidProperty);
            private set => this.SetValue(IsTextValidProperty, value);
        }

        public static readonly DependencyProperty InvalidTextReasonProperty = DPUtils.Register(_ => _.InvalidTextReason);
        /// <summary>Why the text in the box can't be read, or null when it can. Set by the control.</summary>
        public string InvalidTextReason
        {
            get => (string)this.GetValue(InvalidTextReasonProperty);
            private set => this.SetValue(InvalidTextReasonProperty, value);
        }

        public static readonly DependencyProperty CurrentToolTipProperty = DPUtils.Register(_ => _.CurrentToolTip);
        /// <summary>
        /// The tooltip over the control: what typed text will come to, or why it can't be read. Set by the control.
        /// </summary>
        public string CurrentToolTip
        {
            get => (string)this.GetValue(CurrentToolTipProperty);
            private set => this.SetValue(CurrentToolTipProperty, value);
        }

        public static readonly DependencyProperty ShowEditDisplayProperty = DPUtils.Register(_ => _.ShowEditDisplay);
        /// <summary>True while the picker flyout is open. Set by the control.</summary>
        public bool ShowEditDisplay
        {
            get => (bool)this.GetValue(ShowEditDisplayProperty);
            private set => this.SetValue(ShowEditDisplayProperty, value);
        }

        // ===========[ Properties ]============================================
        private Border PART_Root { get; set; }
        private TextBox PART_TextBox { get; set; }
        private ButtonBase PART_PickerButton { get; set; }

        // ===========[ Helper Methods ]========================================
        // ------ Typed text
        private void TextBox_OnTextChanged (object sender, TextChangedEventArgs e)
        {
            // Text matching what the control showed is the value, whether the control just wrote it or a person typed
            //  their way back to it
            m_hasUncommittedText = !String.Equals(this.PART_TextBox.Text, m_shownText, StringComparison.Ordinal);
            this.EvaluateText();
        }

        private void TextBox_OnKeyDown (object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                this.CommitText();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                this.ShowValueAsText();
                e.Handled = true;
            }
        }

        private void TextBox_OnLostFocus (object sender, RoutedEventArgs e)
        {
            this.CommitText();
        }

        // Makes typed text the value, when it reads as one. Text that doesn't stays as typed, shown invalid, to be fixed.
        private void CommitText ()
        {
            if (!m_hasUncommittedText || this.IsReadOnly || (this.PART_TextBox == null))
            {
                return;
            }

            DateTimeEditRules.Evaluation evaluation = DateTimeEditRules.Evaluate(this.PART_TextBox.Text, this.TextResolver, this.BuildContext(), this.TreatEmptyAsInvalid);
            if (!evaluation.IsTextValid)
            {
                return;
            }

            DateTime? oldValue = this.EditDateTime;
            m_hasUncommittedText = false;
            this.EditDateTime = evaluation.Value;
            this.ShowValueAsText();
            if (oldValue != evaluation.Value)
            {
                this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(oldValue, evaluation.Value));
            }
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
            }
            else
            {
                DateTimeEditRules.Evaluation evaluation = DateTimeEditRules.Evaluate(this.PART_TextBox.Text, this.TextResolver, context, this.TreatEmptyAsInvalid);
                this.IsTextValid = evaluation.IsTextValid;
                this.InvalidTextReason = evaluation.InvalidTextReason;
                this.CurrentToolTip = evaluation.IsTextValid
                    ? DateTimeEditRules.Format(evaluation.Value, this.TextResolver, context)
                    : evaluation.InvalidTextReason;
            }

            this.UpdateVisualState();
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
        private void PickerButton_OnClick (object sender, RoutedEventArgs e)
        {
            if ((m_flyout == null) || (this.PART_Root == null))
            {
                return;
            }

            // Anything typed is settled first, so the pickers open on the value the text box shows
            this.CommitText();

            // Read-only, the pickers open to show the value. The time picker is simply off. CalendarView has no read-only
            //  mode that still pages through the months: no selection drops the mark on the held date, and off greys it
            //  and stops the paging. So it stays on, and a date clicked is put straight back (CalendarView_OnSelectedDatesChanged).
            DateTimeTextContext context = this.BuildContext();
            m_readOnlyNote.Visibility = this.IsReadOnly ? Visibility.Visible : Visibility.Collapsed;
            m_timePicker.IsEnabled = !this.IsReadOnly;

            // The time picker takes the clock the text is shown on, decided the same way, rather than the system's own
            m_timePicker.ClockIdentifier = TimeOfDayRules.UsesTwelveHourClock(this.TimeFormat, CultureInfo.CurrentCulture)
                ? Windows.Globalization.ClockIdentifiers.TwelveHour
                : Windows.Globalization.ClockIdentifiers.TwentyFourHour;
            m_calendarView.Visibility = context.ShowsDate ? Visibility.Visible : Visibility.Collapsed;
            m_timePicker.Visibility = context.ShowsTime ? Visibility.Visible : Visibility.Collapsed;
            this.SyncPickersToValue();

            m_editCache = this.EditDateTime;
            m_isPickerEditActive = true;
            this.ShowEditDisplay = true;
            m_flyout.ShowAt(this.PART_Root);
        }

        private void SyncPickersToValue ()
        {
            m_isSyncingPickers = true;
            try
            {
                DateTime shown = this.EditDateTime ?? DateTime.Now;
                var middayOfShown = new DateTimeOffset(shown.Date.AddHours(kMiddayHour));

                m_calendarView.SelectedDates.Clear();
                if (this.EditDateTime != null)
                {
                    m_calendarView.SelectedDates.Add(middayOfShown);
                }

                m_calendarView.SetDisplayDate(middayOfShown);
                m_timePicker.SelectedTime = this.EditDateTime?.TimeOfDay;
            }
            finally
            {
                m_isSyncingPickers = false;
            }
        }

        private void CalendarView_OnSelectedDatesChanged (CalendarView sender, CalendarViewSelectedDatesChangedEventArgs e)
        {
            if (m_isSyncingPickers)
            {
                return;
            }

            // Read-only, whatever was clicked, the held date goes back
            if (this.IsReadOnly)
            {
                this.SyncPickersToValue();
                return;
            }

            if (e.AddedDates.Count == 0)
            {
                return;
            }

            this.EditDateTime = DateTimeEditRules.WithPickedDate(this.EditDateTime, e.AddedDates[0].LocalDateTime);

            // With only a date to pick, picking it is the whole edit
            if (!this.BuildContext().ShowsTime)
            {
                m_flyout.Hide();
            }
        }

        private void TimePicker_OnSelectedTimeChanged (TimePicker sender, TimePickerSelectedValueChangedEventArgs e)
        {
            if (m_isSyncingPickers || this.IsReadOnly || (e.NewTime == null))
            {
                return;
            }

            this.EditDateTime = DateTimeEditRules.WithPickedTimeOfDay(this.EditDateTime, e.NewTime.Value, DateTime.Now);
        }

        private void Flyout_OnClosed (object sender, object e)
        {
            this.ShowEditDisplay = false;
            if (m_isPickerEditActive && (m_editCache != this.EditDateTime))
            {
                this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(m_editCache, this.EditDateTime));
            }

            m_isPickerEditActive = false;
            m_editCache = null;
        }

        // ------ Property change handlers
        private void OnEditDateTimeChanged ()
        {
            // A new value replaces whatever was typed, wherever it came from
            this.ShowValueAsText();
        }

        // IncludeDate, IncludeTime, DisplayFormat, TimeFormat and TextResolver all change what the value reads as. Typed text is
        //  left alone and read again under the new terms.
        private void OnWhatIsShownChanged ()
        {
            if (m_hasUncommittedText)
            {
                this.EvaluateText();
            }
            else
            {
                this.ShowValueAsText();
            }
        }

        // ------ Visual states
        private void OnGotFocus (object sender, RoutedEventArgs e)
        {
            m_isFocused = true;
            this.UpdateVisualState();
        }

        private void OnLostFocus (object sender, RoutedEventArgs e)
        {
            m_isFocused = false;
            this.UpdateVisualState();
        }

        private void UpdateVisualState ()
        {
            // Invalid text takes the border over the focused look
            string borderState = !this.IsTextValid ? "TextInvalid" : (m_isFocused ? "Focused" : "Normal");
            VisualStateManager.GoToState(this, borderState, false);

            // The text box keeps its focused look while focus is inside it, valid or not
            VisualStateManager.GoToState(this, m_isFocused ? "TextBoxFocused" : "TextBoxUnfocused", false);
            VisualStateManager.GoToState(this, this.IsTextValid ? "WarningHidden" : "WarningShown", false);

            string glyphState = "CustomPickerContent";
            if (this.PickerButtonContent == null)
            {
                glyphState = this.BuildContext().ShowsDate ? "CalendarGlyph" : "ClockGlyph";
            }

            VisualStateManager.GoToState(this, glyphState, false);
            VisualStateManager.GoToState(this, this.IsReadOnly ? "ReadOnly" : "Editable", false);
        }
    }
}
