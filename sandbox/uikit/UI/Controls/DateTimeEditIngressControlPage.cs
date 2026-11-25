namespace AJutShowRoomUIKit.UI.Controls
{
    using System;
    using AJut.UX;
    using AJut.UX.Controls;
    using UIKit;

    public sealed class DateTimeEditIngressControlPage : ShowroomPageController
    {
        private readonly DateTimeEditIngressControl m_dateOnly = new DateTimeEditIngressControl { UnsetTextPrompt = "No date" };
        private readonly DateTimeEditIngressControl m_timeOnly = new DateTimeEditIngressControl { IncludeDate = false, IncludeTime = true, UnsetTextPrompt = "No time" };
        private readonly DateTimeEditIngressControl m_dateAndTime = new DateTimeEditIngressControl { IncludeTime = true, DisplayFormat = "ddd, MMM d yyyy 'at' h:mm tt" };
        private readonly DateTimeEditIngressControl m_required = new DateTimeEditIngressControl { TreatEmptyAsInvalid = true, UnsetTextPrompt = "A date is needed" };
        private readonly DateTimeEditIngressControl m_readOnly = new DateTimeEditIngressControl { IncludeTime = true, EditDateTime = DateTime.Now, IsReadOnly = true };
        private readonly DateTimeEditIngressControl m_twentyFourHour = new DateTimeEditIngressControl { IncludeTime = true, EditDateTime = DateTime.Now, TimeFormat = eTimeFormat.TwentyFourHour };
        private readonly DateTimeEditIngressControl m_twelveHour = new DateTimeEditIngressControl { IncludeTime = true, EditDateTime = DateTime.Now, TimeFormat = eTimeFormat.TwelveHour };
        private readonly UILabel m_liveValue = new UILabel { Lines = 0 };
        private readonly UILabel m_lastEdit = new UILabel { Lines = 0, Text = "No edit completed yet" };

        public DateTimeEditIngressControlPage () : base("DateTimeEditIngressControl")
        {
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);
            m_dateOnly.ValueChanged -= this.DateOnly_OnValueChanged;
            m_dateOnly.ValueChanged += this.DateOnly_OnValueChanged;
            m_dateAndTime.UserEditComplete -= this.DateAndTime_OnUserEditComplete;
            m_dateAndTime.UserEditComplete += this.DateAndTime_OnUserEditComplete;
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            m_dateOnly.ValueChanged -= this.DateOnly_OnValueChanged;
            m_dateAndTime.UserEditComplete -= this.DateAndTime_OnUserEditComplete;
        }

        protected override void BuildPage ()
        {
            this.AddHeading("DateTimeEditIngressControl");
            this.AddText("Shows a date, a time, or both, and takes typed text as well as picks from UIKit's calendar and time picker: a popover on iPad, a sheet on iPhone. Try: today, tomorrow, friday, next friday, in two weeks, +3d, -2w, 2026-12-24, friday 3pm, tomorrow at 15:30. Return takes what is typed, and Escape on a keyboard puts the value back.");

            this.AddText("Date only");
            this.AddDemo(m_dateOnly);
            this.AddDemo(m_liveValue);

            this.AddText("Time only");
            this.AddDemo(m_timeOnly);

            this.AddText("Date and time on a twenty four hour clock, whatever the culture");
            this.AddDemo(m_twentyFourHour);

            this.AddText("Date and time on a twelve hour clock, whatever the culture");
            this.AddDemo(m_twelveHour);

            this.AddText("Date and time, shown in a format of its own");
            this.AddDemo(m_dateAndTime);
            this.AddDemo(m_lastEdit);

            this.AddText("Empty is invalid");
            this.AddDemo(m_required);

            this.AddText("Read only");
            this.AddDemo(m_readOnly);

            this.AddCode(@"
var due = new DateTimeEditIngressControl { UnsetTextPrompt = ""No date"" };
due.ValueChanged += this.Due_OnValueChanged;           // a person changed it: typed text taken, or a pick
due.UserEditComplete += this.Due_OnUserEditComplete;   // once per edit: typed text taken, or the pickers closed

var alarm = new DateTimeEditIngressControl
{
    IncludeTime = true,
    TimeFormat = eTimeFormat.TwentyFourHour,   // or TwelveHour; CurrentCulture, the default, is the culture's own clock
};

var starts = new DateTimeEditIngressControl
{
    IncludeTime = true,
    DisplayFormat = ""ddd, MMM d yyyy 'at' h:mm tt"",
};");

            this.ShowLiveValue();
        }

        private void DateOnly_OnValueChanged (object sender, EventArgs e) => this.ShowLiveValue();

        private void DateAndTime_OnUserEditComplete (object sender, UserEditAppliedEventArgs e)
        {
            m_lastEdit.Text = $"Last edit: {e.OldValue ?? "(none)"} to {e.NewValue ?? "(none)"}";
        }

        private void ShowLiveValue ()
        {
            m_liveValue.Text = $"EditDateTime: {m_dateOnly.EditDateTime?.ToString("D") ?? "(none)"}";
        }
    }
}
