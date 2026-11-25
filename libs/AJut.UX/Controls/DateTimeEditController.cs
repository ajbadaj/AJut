namespace AJut.UX.Controls
{
    using System;
    using System.Globalization;

    /// <summary>
    /// What a toolkit's DateTimeEditIngressControl gives <see cref="DateTimeEditController"/>: the things the controller
    /// writes back to it. Each toolkit implements it once, on its control.
    /// </summary>
    public interface IDateTimeEditHost
    {
        /// <summary>
        /// Puts <paramref name="text"/> in the control's text field, in place of whatever was typed there. The controller
        /// ignores the text edit this causes (Android and UIKit raise one for a set from code), so the host can route its
        /// text changed event straight to <see cref="DateTimeEditController.OnTextEdited"/>.
        /// </summary>
        void ShowText (string text);

        /// <summary>
        /// The value changed. <paramref name="isFromPerson"/> says whether a person changed it, by typing or with a picker,
        /// or code did, so a host that tells the two apart (UIKit sends ValueChanged only for a person's change) can.
        /// </summary>
        void OnValueChanged (bool isFromPerson);

        /// <summary>
        /// Whether the text is valid, why not, or the tooltip changed: show <see cref="DateTimeEditController.IsTextValid"/>,
        /// <see cref="DateTimeEditController.InvalidTextReason"/> and <see cref="DateTimeEditController.CurrentToolTip"/>
        /// </summary>
        void ShowTextState ();

        /// <summary>A person finished changing the value, from <paramref name="oldValue"/> to <paramref name="newValue"/>: raise UserEditComplete</summary>
        void RaiseUserEditComplete (DateTime? oldValue, DateTime? newValue);
    }

    // ===========[ DateTimeEditController ]=====================================
    // The part of a DateTimeEditIngressControl that is the same on every toolkit drawn in code (AppKit, UIKit and
    // Android): it holds the value and the settings, takes the host's events (text typed, Enter, Escape, focus leaving,
    // the pickers opening, a date or time picked, the pickers closing), and writes the results back through
    // IDateTimeEditHost. What text comes to is DateTimeEditRules, which WinUI3's and WPF's controls call too, so a phrase
    // gets the same value on every stack. The host keeps only what is its toolkit's: the views, the look, focus, and the
    // platform's own pickers.
    //
    // Typing lifecycle, as on WinUI3 and WPF:
    //   Text changes           -> read as typed, to show whether it is valid and, in the tooltip, what it comes to
    //   Enter or focus leaving -> valid text becomes the value and is shown formatted; invalid text stays to be fixed
    //   Escape                 -> the text goes back to the value held
    //
    // Picker lifecycle:
    //   Pickers open           -> anything typed is settled, and the value is cached
    //   A date or time picked  -> the value changes right away; with only a date to pick, picking it ends the edit
    //   Pickers close          -> UserEditComplete, if the value changed
    //
    // Text that only shows the value is never read back in: a format can drop part of a value (its seconds, say), and
    // reading it back would lose them. So only text a person typed is ever committed.
    /// <summary>
    /// The value, settings and editing rules of a DateTimeEditIngressControl, toolkit free, so the AppKit, UIKit and
    /// Android controls behave the same and the behavior is tested once
    /// </summary>
    public sealed class DateTimeEditController
    {
        private readonly IDateTimeEditHost m_host;
        private DateTime? m_value;
        private bool m_includeDate = true;
        private bool m_includeTime;
        private string? m_displayFormat;
        private eTimeFormat m_timeFormat;
        private IDateTimeTextResolver? m_textResolver;
        private bool m_treatEmptyAsInvalid;
        private string m_text = String.Empty;
        private bool m_hasUncommittedText;
        private bool m_isPushingText;
        private DateTimeEditRules.Evaluation m_evaluation = new DateTimeEditRules.Evaluation(IsTextValid: true, Value: null, InvalidTextReason: null);
        private string? m_currentToolTip;
        private DateTime? m_valueAtPickerOpen;
        private bool m_isPickerEditActive;

        // ===========[ Setup/Construction/Teardown ]===================================
        /// <summary>
        /// Makes the controller for <paramref name="host"/>. Nothing is written to the host until <see cref="ShowValueAsText"/>
        /// runs or a setting changes, so a host can make this before its views, as long as its host methods let a call that
        /// comes before the views pass.
        /// </summary>
        public DateTimeEditController (IDateTimeEditHost host)
        {
            m_host = host;
        }

        // ===========[ Properties ]===================================
        /// <summary>
        /// The date and time being edited, or null for none. Setting it is a change from code: it replaces whatever was
        /// typed, and raises no user edit.
        /// </summary>
        public DateTime? Value
        {
            get => m_value;
            set
            {
                if (this.ApplyValue(value, isFromPerson: false))
                {
                    this.ShowValueAsText();
                }
            }
        }

        /// <summary>Whether the date is edited. With this and <see cref="IncludeTime"/> both off, both are edited.</summary>
        public bool IncludeDate
        {
            get => m_includeDate;
            set
            {
                m_includeDate = value;
                this.OnWhatIsShownChanged();
            }
        }

        /// <summary>Whether the time of day is edited</summary>
        public bool IncludeTime
        {
            get => m_includeTime;
            set
            {
                m_includeTime = value;
                this.OnWhatIsShownChanged();
            }
        }

        /// <summary>The .NET format the value is shown in, or null for the culture's short date, short time, or both</summary>
        public string? DisplayFormat
        {
            get => m_displayFormat;
            set
            {
                m_displayFormat = value;
                this.OnWhatIsShownChanged();
            }
        }

        /// <summary>
        /// The clock a time is shown and picked on: the culture's own (the default), twelve hour, or twenty four hour. It
        /// sets the shown text while there is no <see cref="DisplayFormat"/>, and the host's time picker either way.
        /// </summary>
        public eTimeFormat TimeFormat
        {
            get => m_timeFormat;
            set
            {
                m_timeFormat = value;
                this.OnWhatIsShownChanged();
            }
        }

        /// <summary>Whether the time picker shows a twelve hour clock with AM and PM, by <see cref="TimeFormat"/> and the culture</summary>
        public bool UsesTwelveHourClock => TimeOfDayRules.UsesTwelveHourClock(m_timeFormat, CultureInfo.CurrentCulture);

        /// <summary>What reads typed text and writes the value as text, or null for <see cref="DateTimeTextResolver.Default"/></summary>
        public IDateTimeTextResolver? TextResolver
        {
            get => m_textResolver;
            set
            {
                m_textResolver = value;
                this.OnWhatIsShownChanged();
            }
        }

        /// <summary>When true, empty text is invalid rather than a way to set no value</summary>
        public bool TreatEmptyAsInvalid
        {
            get => m_treatEmptyAsInvalid;
            set
            {
                m_treatEmptyAsInvalid = value;
                this.EvaluateText();
            }
        }

        /// <summary>When true, typed text is never taken and the pickers don't open</summary>
        public bool IsReadOnly { get; set; }

        /// <summary>The text the field holds, as typed or as the value is shown</summary>
        public string Text => m_text;

        /// <summary>Whether the field holds something typed that hasn't become the value yet</summary>
        public bool HasUncommittedText => m_hasUncommittedText;

        /// <summary>Whether the text in the field can be read as a value</summary>
        public bool IsTextValid => m_evaluation.IsTextValid;

        /// <summary>Why the text in the field can't be read, or null when it can</summary>
        public string? InvalidTextReason => m_evaluation.InvalidTextReason;

        /// <summary>What typed text will come to, or why it can't be read; null while the field shows the value</summary>
        public string? CurrentToolTip => m_currentToolTip;

        /// <summary>Whether the date is part of what is edited, so whether a calendar is offered</summary>
        public bool ShowsDate => this.BuildContext().ShowsDate;

        /// <summary>Whether the time of day is part of what is edited, so whether a time picker is offered</summary>
        public bool ShowsTime => this.BuildContext().ShowsTime;

        /// <summary>True between <see cref="BeginPickerEdit"/> and <see cref="EndPickerEdit"/></summary>
        public bool IsPickerEditActive => m_isPickerEditActive;

        // ===========[ Public Interface Methods ]===================================
        /// <summary>What the edit is read against right now: the moment, the value, what is edited, the format and culture</summary>
        public DateTimeTextContext BuildContext ()
        {
            return new DateTimeTextContext(DateTime.Now, m_value, m_includeDate, m_includeTime, m_displayFormat, CultureInfo.CurrentCulture, m_timeFormat);
        }

        /// <summary>The field's text changed. Text the controller put there itself is ignored.</summary>
        public void OnTextEdited (string? text)
        {
            if (m_isPushingText)
            {
                return;
            }

            m_text = text ?? String.Empty;
            m_hasUncommittedText = true;
            this.EvaluateText();
        }

        /// <summary>
        /// Makes typed text the value, when it reads as one: for Enter, and for focus leaving the field. Text that doesn't
        /// read as a value stays as typed, shown invalid, to be fixed. Returns whether typed text was taken.
        /// </summary>
        public bool CommitText ()
        {
            if (!m_hasUncommittedText || this.IsReadOnly)
            {
                return false;
            }

            DateTimeEditRules.Evaluation evaluation = DateTimeEditRules.Evaluate(m_text, m_textResolver, this.BuildContext(), m_treatEmptyAsInvalid);
            if (!evaluation.IsTextValid)
            {
                return false;
            }

            DateTime? oldValue = m_value;
            m_hasUncommittedText = false;
            bool isChanged = this.ApplyValue(evaluation.Value, isFromPerson: true);
            this.ShowValueAsText();
            if (isChanged)
            {
                m_host.RaiseUserEditComplete(oldValue, evaluation.Value);
            }

            return true;
        }

        /// <summary>Puts the value back in the field, formatted, in place of whatever was typed there: for Escape</summary>
        public void ShowValueAsText ()
        {
            m_hasUncommittedText = false;
            m_text = DateTimeEditRules.Format(m_value, m_textResolver, this.BuildContext());
            m_isPushingText = true;
            try
            {
                m_host.ShowText(m_text);
            }
            finally
            {
                m_isPushingText = false;
            }

            this.EvaluateText();
        }

        /// <summary>
        /// The pickers are about to open. Anything typed is settled first, so they open on the value the field shows.
        /// Read-only, they open too, to show the value: the host marks them read-only, and every pick is ignored. Returns
        /// whether the pickers open, which they always do.
        /// </summary>
        public bool BeginPickerEdit ()
        {
            this.CommitText();
            m_valueAtPickerOpen = m_value;
            m_isPickerEditActive = true;
            return true;
        }

        /// <summary>
        /// A date was picked in the calendar: the value becomes that date, at the time of day it held. Returns true when
        /// picking the date is the whole edit (the control edits no time of day), so the host closes its pickers.
        /// </summary>
        public bool PickDate (DateTime pickedDate)
        {
            if (this.IsReadOnly)
            {
                return false;
            }

            if (this.ApplyValue(DateTimeEditRules.WithPickedDate(m_value, pickedDate), isFromPerson: true))
            {
                this.ShowValueAsText();
            }

            return !this.ShowsTime;
        }

        /// <summary>A time of day was picked: the value becomes that time, on the date it held (today when it held none)</summary>
        public void PickTimeOfDay (TimeSpan pickedTimeOfDay)
        {
            if (this.IsReadOnly)
            {
                return;
            }

            if (this.ApplyValue(DateTimeEditRules.WithPickedTimeOfDay(m_value, pickedTimeOfDay, DateTime.Now), isFromPerson: true))
            {
                this.ShowValueAsText();
            }
        }

        /// <summary>The pickers closed, however they closed: raises the user edit if the value changed while they were open</summary>
        public void EndPickerEdit ()
        {
            if (!m_isPickerEditActive)
            {
                return;
            }

            m_isPickerEditActive = false;
            DateTime? valueAtOpen = m_valueAtPickerOpen;
            m_valueAtPickerOpen = null;
            if (valueAtOpen != m_value)
            {
                m_host.RaiseUserEditComplete(valueAtOpen, m_value);
            }
        }

        // ===========[ Helper Methods ]===================================
        private bool ApplyValue (DateTime? value, bool isFromPerson)
        {
            if (m_value == value)
            {
                return false;
            }

            m_value = value;
            m_host.OnValueChanged(isFromPerson);
            return true;
        }

        // Reads the field as typed, for the invalid look and the tooltip. Nothing here changes the value.
        private void EvaluateText ()
        {
            DateTimeTextContext context = this.BuildContext();
            if (m_hasUncommittedText)
            {
                m_evaluation = DateTimeEditRules.Evaluate(m_text, m_textResolver, context, m_treatEmptyAsInvalid);
                m_currentToolTip = m_evaluation.IsTextValid
                    ? DateTimeEditRules.Format(m_evaluation.Value, m_textResolver, context)
                    : m_evaluation.InvalidTextReason;
            }
            else
            {
                // The field shows the value, which is always valid, unless there is none and none is not allowed
                DateTimeEditRules.Evaluation ofValue = DateTimeEditRules.Evaluate(null, m_textResolver, context, m_treatEmptyAsInvalid);
                bool isValueFine = (m_value != null) || ofValue.IsTextValid;
                m_evaluation = isValueFine
                    ? new DateTimeEditRules.Evaluation(IsTextValid: true, Value: m_value, InvalidTextReason: null)
                    : ofValue;
                m_currentToolTip = isValueFine ? null : ofValue.InvalidTextReason;
            }

            m_host.ShowTextState();
        }

        // IncludeDate, IncludeTime, DisplayFormat and TextResolver all change what the value reads as. Typed text is left
        //  alone and read again under the new terms.
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
    }
}
