namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using AJut;
    using AJut.DateAndTime;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using AppKit;

    /// <summary>The model the DateTimeEditIngressControl page binds its date only sample to</summary>
    public sealed class DateSampleModel : NotifyPropertyChanged
    {
        private DateTime? m_due;

        public DateTime? Due
        {
            get => m_due;
            set => this.SetAndRaiseIfChanged(ref m_due, value);
        }
    }

    public sealed class DateTimeEditIngressControlPage : ShowroomPage
    {
        private const double kControlWidth = 280.0;

        private readonly DateSampleModel m_model = new DateSampleModel();
        private readonly BindingSet m_bindings = new BindingSet();
        private readonly DateTimeEditIngressControl m_dateAndTime;
        private readonly NSTextField m_editReadout = ShowroomStyle.Understated("No edit yet");

        public DateTimeEditIngressControlPage ()
            : base("DateTimeEditIngressControl", "Shows a date, a time, or both, and takes typed text as well as picks from the Mac's calendar and time picker. Try: today, tomorrow, friday, next friday, in two weeks, +3d, -2w, 2026-12-24, friday 3pm, tomorrow at 15:30. Return takes what is typed, and Escape puts the value back. The rules are the ones every stack's DateTimeEditIngressControl follows, so the same text gets the same value on Windows.")
        {
            // 1. Date only, bound to a model
            DateTimeEditIngressControl dateOnly = MakeControl();
            dateOnly.UnsetTextPrompt = "No date";
            m_bindings.BindEditDateTime(dateOnly, m_model, nameof(DateSampleModel.Due), this.ReadDue, this.WriteDue);
            this.AddLabeledDemo("Date only", dateOnly);

            NSTextField modelReadout = ShowroomStyle.Understated(String.Empty);
            m_bindings.BindLabel(modelReadout, m_model, nameof(DateSampleModel.Due), this.ReadDueText);
            this.AddLabeledDemo("Model says", modelReadout);

            // 2. Time only
            DateTimeEditIngressControl timeOnly = MakeControl();
            timeOnly.IncludeDate = false;
            timeOnly.IncludeTime = true;
            timeOnly.UnsetTextPrompt = "No time";
            this.AddLabeledDemo("Time only", timeOnly);

            // 3. Date and time on each clock, whatever the culture
            DateTimeEditIngressControl twentyFourHour = MakeControl();
            twentyFourHour.IncludeTime = true;
            twentyFourHour.TimeFormat = eTimeFormat.TwentyFourHour;
            twentyFourHour.EditDateTime = DateTime.Now;
            this.AddLabeledDemo("Date and time on a twenty four hour clock, whatever the culture", twentyFourHour);

            DateTimeEditIngressControl twelveHour = MakeControl();
            twelveHour.IncludeTime = true;
            twelveHour.TimeFormat = eTimeFormat.TwelveHour;
            twelveHour.EditDateTime = DateTime.Now;
            this.AddLabeledDemo("Date and time on a twelve hour clock, whatever the culture", twelveHour);

            // 4. Date and time, in a format of its own, saying when a person changes it
            m_dateAndTime = MakeControl();
            m_dateAndTime.IncludeTime = true;
            m_dateAndTime.DisplayFormat = "ddd, MMM d yyyy 'at' h:mm tt";
            m_dateAndTime.UserEditComplete += this.OnDateAndTimeEdited;
            this.AddLabeledDemo("Date and time, shown in a format of its own", m_dateAndTime);
            this.AddLabeledDemo("Last edit", m_editReadout);

            // 5. A resolver of its own
            DateTimeEditIngressControl quarters = MakeControl();
            quarters.TextResolver = new QuarterResolver();
            this.AddLabeledDemo("A resolver of its own: q1 to q4 are quarters, and values show as one", quarters);

            // 6. Empty is invalid
            DateTimeEditIngressControl required = MakeControl();
            required.TreatEmptyAsInvalid = true;
            required.UnsetTextPrompt = "A date is needed";
            this.AddLabeledDemo("Empty is invalid", required);

            // 7. Read only
            DateTimeEditIngressControl readOnly = MakeControl();
            readOnly.IncludeTime = true;
            readOnly.EditDateTime = DateTime.Now;
            readOnly.IsReadOnly = true;
            this.AddLabeledDemo("Read only", readOnly);

            this.AddCode(
@"var due = new DateTimeEditIngressControl { UnsetTextPrompt = ""No date"" };
bindings.BindEditDateTime(
    due, model, nameof(Model.Due),
    () => model.Due, value => model.Due = value
);

var starts = new DateTimeEditIngressControl
{
    IncludeTime = true,
    DisplayFormat = ""ddd, MMM d yyyy 'at' h:mm tt"",
};

// A resolver of its own, an IDateTimeTextResolver, reads the text and writes the value
var quarter = new DateTimeEditIngressControl { TextResolver = new QuarterResolver() };"
            );
        }

        protected override void OnTeardown ()
        {
            m_dateAndTime.UserEditComplete -= this.OnDateAndTimeEdited;
            m_bindings.Dispose();
        }

        private void OnDateAndTimeEdited (object sender, AJut.UX.UserEditAppliedEventArgs e)
        {
            m_editReadout.StringValue = $"{e.OldValue ?? "(none)"} to {e.NewValue ?? "(none)"}";
        }

        private DateTime? ReadDue () => m_model.Due;
        private void WriteDue (DateTime? value) => m_model.Due = value;
        private string ReadDueText () => m_model.Due?.ToString("D") ?? "(none)";

        private static DateTimeEditIngressControl MakeControl ()
        {
            var control = new DateTimeEditIngressControl();
            control.TranslatesAutoresizingMaskIntoConstraints = false;
            control.WidthAnchor.ConstraintEqualTo((nfloat)kControlWidth).Active = true;
            return control;
        }

        /// <summary>
        /// Reads q1 to q4 (and "q1 2027") as the first day of that quarter, shows any value as its quarter, and leaves
        /// everything else typed to the default resolver
        /// </summary>
        private sealed class QuarterResolver : IDateTimeTextResolver
        {
            private const int kMonthsPerQuarter = 3;

            public DateTimeTextResolution Resolve (string text, DateTimeTextContext context)
            {
                // q1 to q4, alone for this year, or with a year after it, which is how a value shows ("Q4 2026"), so the
                //  shown text can be edited and read back
                string[] words = text.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if ((words.Length is 1 or 2) && (words[0] is "q1" or "q2" or "q3" or "q4"))
                {
                    int year = context.Now.Year;
                    if ((words.Length == 2)
                        && (!Int32.TryParse(words[1], out year) || (year < DateTime.MinValue.Year) || (year > DateTime.MaxValue.Year)))
                    {
                        return DateTimeTextResolution.Failed($"'{words[1]}' isn't a year");
                    }

                    int quarter = words[0][1] - '0';
                    return DateTimeTextResolution.Resolved(new DateTime(year, ((quarter - 1) * kMonthsPerQuarter) + 1, 1));
                }

                return DateTimeTextResolver.Default.Resolve(text, context);
            }

            public string Format (DateTime? value, DateTimeTextContext context)
            {
                return (value == null) ? String.Empty : $"Q{((value.Value.Month - 1) / kMonthsPerQuarter) + 1} {value.Value.Year}";
            }
        }
    }
}
