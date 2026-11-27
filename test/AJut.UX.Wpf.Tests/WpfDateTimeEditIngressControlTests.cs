namespace AJut.UX.Tests
{
    using System;
    using System.Globalization;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.DateAndTime;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The WPF DateTimeEditIngressControl through its text box and its pickers, and the calendar and time editors it
    /// opens. What typed text comes to is tested with the shared rules; these are the control taking it, keeping the
    /// rest of its value, and saying when a person changed it. WPF objects need an STA thread, so each test runs on one,
    /// and nothing is ever shown.
    /// </summary>
    [TestClass]
    public class WpfDateTimeEditIngressControlTests
    {
        private static readonly Size kMeasureSize = new Size(400, 400);

        // ===========[ Typed text ]============================================
        [TestMethod]
        public void WpfDateTimeEditIngressControl_TypedDateCommitted_BecomesTheValueAndSaysSo ()
        {
            StaTestThread.Run(() =>
            {
                var control = new DateTimeEditIngressControl();
                TextBox textBox = HostAndGetTextBox(control);
                var edits = new List<UserEditAppliedEventArgs>();
                control.UserEditComplete += _OnUserEdit;

                textBox.Text = "2026-12-24";
                Assert.IsNull(control.EditDateTime, "Typing alone changes nothing");
                Assert.IsTrue(control.CommitText());
                control.UserEditComplete -= _OnUserEdit;

                Assert.AreEqual(new DateTime(2026, 12, 24), control.EditDateTime);
                Assert.AreEqual(1, edits.Count);
                Assert.IsNull(edits[0].OldValue);
                Assert.AreEqual(new DateTime(2026, 12, 24), edits[0].NewValue);

                void _OnUserEdit (object sender, UserEditAppliedEventArgs e) => edits.Add(e);
            });
        }

        [TestMethod]
        public void WpfDateTimeEditIngressControl_TextThatIsNoDate_IsInvalidAndLeavesTheValue ()
        {
            StaTestThread.Run(() =>
            {
                var held = new DateTime(2026, 3, 4);
                var control = new DateTimeEditIngressControl { EditDateTime = held };
                TextBox textBox = HostAndGetTextBox(control);

                textBox.Text = "soonish";

                Assert.IsFalse(control.IsTextValid);
                Assert.IsFalse(control.CommitText(), "Text that can't be read is not taken");
                Assert.AreEqual(held, control.EditDateTime);
                Assert.AreEqual("soonish", textBox.Text, "And it stays as typed, to be fixed");
            });
        }

        [TestMethod]
        public void WpfDateTimeEditIngressControl_TextThatOnlyShowsTheValue_IsNeverReadBackIn ()
        {
            StaTestThread.Run(() =>
            {
                // The short date and time shown drops the seconds, which the value keeps
                var held = new DateTime(2026, 3, 4, 10, 30, 45);
                var control = new DateTimeEditIngressControl { IncludeTime = true, EditDateTime = held };
                HostAndGetTextBox(control);

                Assert.IsFalse(control.CommitText());
                Assert.AreEqual(held, control.EditDateTime);
            });
        }

        [TestMethod]
        public void WpfDateTimeEditIngressControl_TypedBackToTheShownText_IsTheValueEvenWhereTheResolverCantReadIt ()
        {
            StaTestThread.Run(() =>
            {
                // This resolver reads "soon" alone, and shows every value as "Soon": text it can't read back
                var control = new DateTimeEditIngressControl { TextResolver = new SoonResolver() };
                TextBox textBox = HostAndGetTextBox(control);
                textBox.Text = "soon";
                Assert.IsTrue(control.CommitText());
                Assert.AreEqual(SoonResolver.kSoon, control.EditDateTime);
                Assert.AreEqual("Soon", textBox.Text);

                textBox.Text = "Soo";
                Assert.IsFalse(control.IsTextValid, "Typing away from the shown text is an edit");
                textBox.Text = "Soon";

                Assert.IsTrue(control.IsTextValid, "Typing back to the shown text is the value again");
                Assert.IsFalse(control.CommitText(), "Which leaves nothing to take");
                Assert.AreEqual(SoonResolver.kSoon, control.EditDateTime);
            });
        }

        [TestMethod]
        public void WpfDateTimeEditIngressControl_ValueSetFromOutside_ReplacesWhatWasTyped ()
        {
            StaTestThread.Run(() =>
            {
                var control = new DateTimeEditIngressControl { DisplayFormat = "yyyy-MM-dd" };
                TextBox textBox = HostAndGetTextBox(control);
                textBox.Text = "soonish";

                control.EditDateTime = new DateTime(2026, 3, 4);

                Assert.AreEqual("2026-03-04", textBox.Text);
                Assert.IsTrue(control.IsTextValid);
            });
        }

        [TestMethod]
        public void WpfDateTimeEditIngressControl_ReadOnly_TakesNoTypedText ()
        {
            StaTestThread.Run(() =>
            {
                var held = new DateTime(2026, 3, 4);
                var control = new DateTimeEditIngressControl { EditDateTime = held, IsReadOnly = true };
                TextBox textBox = HostAndGetTextBox(control);

                textBox.Text = "2026-12-24";

                Assert.IsFalse(control.CommitText());
                Assert.AreEqual(held, control.EditDateTime);
            });
        }

        // ===========[ Pickers ]===============================================
        [TestMethod]
        public void WpfDateTimeEditIngressControl_DatePickedWithATimeHeld_KeepsTheTime ()
        {
            StaTestThread.Run(() =>
            {
                var control = new DateTimeEditIngressControl { IncludeTime = true, EditDateTime = new DateTime(2026, 3, 4, 17, 45, 0) };
                HostAndGetTextBox(control);
                var edits = new List<UserEditAppliedEventArgs>();
                control.UserEditComplete += _OnUserEdit;
                var dateEditor = (DateValueEditControl)control.Template.FindName("PART_DateEditor", control);

                control.ShowEditDisplay = true;
                dateEditor.EditDate = new DateTime(2026, 3, 9);

                Assert.AreEqual(new DateTime(2026, 3, 9, 17, 45, 0), control.EditDateTime);
                Assert.IsTrue(control.ShowEditDisplay, "With a time to pick as well, the pickers stay open");
                Assert.AreEqual(0, edits.Count, "The edit is not over until they close");

                control.ShowEditDisplay = false;
                control.UserEditComplete -= _OnUserEdit;
                Assert.AreEqual(1, edits.Count);
                Assert.AreEqual(new DateTime(2026, 3, 4, 17, 45, 0), edits[0].OldValue);
                Assert.AreEqual(new DateTime(2026, 3, 9, 17, 45, 0), edits[0].NewValue);

                void _OnUserEdit (object sender, UserEditAppliedEventArgs e) => edits.Add(e);
            });
        }

        [TestMethod]
        public void WpfDateTimeEditIngressControl_DatePickedOnADateOnlyControl_ClosesThePickers ()
        {
            StaTestThread.Run(() =>
            {
                var control = new DateTimeEditIngressControl();
                HostAndGetTextBox(control);
                var dateEditor = (DateValueEditControl)control.Template.FindName("PART_DateEditor", control);

                control.ShowEditDisplay = true;
                dateEditor.EditDate = new DateTime(2026, 3, 9);

                Assert.AreEqual(new DateTime(2026, 3, 9), control.EditDateTime);
                Assert.IsFalse(control.ShowEditDisplay);
            });
        }

        [TestMethod]
        public void WpfDateTimeEditIngressControl_TimeSteppedOnATimeOnlyControl_KeepsTheDate ()
        {
            StaTestThread.Run(() =>
            {
                var control = new DateTimeEditIngressControl { IncludeDate = false, IncludeTime = true, EditDateTime = new DateTime(2026, 3, 4, 23, 15, 0) };
                HostAndGetTextBox(control);
                var timeEditor = (TimeValueEditControl)control.Template.FindName("PART_TimeEditor", control);

                control.ShowEditDisplay = true;
                TimeValueEditControl.IncreaseHourCommand.Execute(null, timeEditor);

                Assert.AreEqual(new DateTime(2026, 3, 4, 0, 15, 0), control.EditDateTime, "An hour past eleven at night goes round to midnight on the same date");
            });
        }

        [TestMethod]
        public void WpfDateTimeEditIngressControl_PickersClosedWithNothingPicked_SaysNothing ()
        {
            StaTestThread.Run(() =>
            {
                var control = new DateTimeEditIngressControl { EditDateTime = new DateTime(2026, 3, 4) };
                HostAndGetTextBox(control);
                int edits = 0;
                control.UserEditComplete += _OnUserEdit;

                control.ShowEditDisplay = true;
                control.ShowEditDisplay = false;
                control.UserEditComplete -= _OnUserEdit;

                Assert.AreEqual(0, edits);

                void _OnUserEdit (object sender, UserEditAppliedEventArgs e) => ++edits;
            });
        }

        // ===========[ DateValueEditControl ]==================================
        [TestMethod]
        public void WpfDateValueEditControl_DatePicked_ShowsItsMonthWithThatDayMarked ()
        {
            StaTestThread.Run(() =>
            {
                var calendar = new DateValueEditControl();

                calendar.EditDate = new DateTime(2027, 2, 14);

                Assert.AreEqual(new DateTime(2027, 2, 1), calendar.DisplayMonth);
                Assert.AreEqual(CalendarMonthGrid.kDaysShown, calendar.Days.Count);
                DateValueEditControl.Day picked = calendar.Days.Single(day => day.IsPicked);
                Assert.AreEqual(new DateTime(2027, 2, 14), picked.Date);
                Assert.IsTrue(picked.IsInDisplayMonth);
            });
        }

        [TestMethod]
        public void WpfDateValueEditControl_SteppingTheMonths_LeavesThePickedDateAlone ()
        {
            StaTestThread.Run(() =>
            {
                var calendar = new DateValueEditControl { EditDate = new DateTime(2026, 12, 14) };

                DateValueEditControl.ShowNextMonthCommand.Execute(null, calendar);

                Assert.AreEqual(new DateTime(2027, 1, 1), calendar.DisplayMonth);
                Assert.AreEqual(new DateTime(2026, 12, 14), calendar.EditDate);
            });
        }

        [TestMethod]
        public void WpfDateValueEditControl_ReadOnly_PicksNothing ()
        {
            StaTestThread.Run(() =>
            {
                var calendar = new DateValueEditControl { EditDate = new DateTime(2026, 12, 14), IsReadOnly = true };
                DateValueEditControl.Day another = calendar.Days.First(day => !day.IsPicked);

                Assert.IsFalse(DateValueEditControl.PickDayCommand.CanExecute(another, calendar));
            });
        }

        [TestMethod]
        public void WpfDateValueEditControl_MonthFromTheMenu_ShowsItInTheYearShownAndPicksNothing ()
        {
            StaTestThread.Run(() =>
            {
                var picked = new DateTime(2026, 12, 14);
                var calendar = new DateValueEditControl { EditDate = picked };

                DateValueEditControl.ShowMonthCommand.Execute(3, calendar);

                Assert.AreEqual(new DateTime(2026, 3, 1), calendar.DisplayMonth);
                Assert.AreEqual(picked, calendar.EditDate);
            });
        }

        [TestMethod]
        public void WpfDateValueEditControl_YearSet_ShowsTheSameMonthInIt ()
        {
            StaTestThread.Run(() =>
            {
                var calendar = new DateValueEditControl { EditDate = new DateTime(2026, 12, 14) };
                Assert.AreEqual(2026, calendar.DisplayYear, "The year follows the month shown");

                calendar.DisplayYear = 1999;

                Assert.AreEqual(new DateTime(1999, 12, 1), calendar.DisplayMonth);
            });
        }

        [TestMethod]
        public void WpfDateValueEditControl_YearPickerChecked_ShowsTheYearChosenAndCloses ()
        {
            StaTestThread.Run(() =>
            {
                var calendar = new DateValueEditControl { EditDate = new DateTime(2026, 12, 14) };
                calendar.IsYearPickerOpen = true;
                Assert.AreEqual(2026, calendar.YearPickerYear, "The year picker starts on the year shown");

                calendar.YearPickerYear = 2031;
                Assert.AreEqual(new DateTime(2026, 12, 1), calendar.DisplayMonth, "Choosing a year alone moves nothing");
                DateValueEditControl.ApplyYearPickCommand.Execute(null, calendar);

                Assert.AreEqual(new DateTime(2031, 12, 1), calendar.DisplayMonth);
                Assert.IsFalse(calendar.IsYearPickerOpen);
            });
        }

        [TestMethod]
        public void WpfDateValueEditControl_YearDoubleClickedInTheList_ShowsItAndCloses ()
        {
            StaTestThread.Run(() =>
            {
                var calendar = new DateValueEditControl { EditDate = new DateTime(2026, 12, 14) };
                calendar.IsYearPickerOpen = true;

                calendar.PickYearFromList(1999);

                Assert.AreEqual(new DateTime(1999, 12, 1), calendar.DisplayMonth, "As picking it and pressing the check does");
                Assert.IsFalse(calendar.IsYearPickerOpen);
            });
        }

        [TestMethod]
        public void WpfTimeValueEditControl_MinuteSteppedPastTheHour_CarriesUnlessToldNotTo ()
        {
            StaTestThread.Run(() =>
            {
                var timeEditor = new TimeValueEditControl { EditTime = new TimeSpan(10, 59, 0) };
                TimeValueEditControl.IncreaseMinuteCommand.Execute(null, timeEditor);
                Assert.AreEqual(new TimeSpan(11, 0, 0), timeEditor.EditTime, "A minute step carries into the hour by default");

                timeEditor.MinuteStepCarriesIntoHour = false;
                TimeValueEditControl.DecreaseMinuteCommand.Execute(null, timeEditor);
                Assert.AreEqual(new TimeSpan(11, 59, 0), timeEditor.EditTime, "Told not to, it goes round inside the hour");
            });
        }

        [TestMethod]
        public void WpfTimeValueEditControl_TimeFormatChosen_SetsTheClock ()
        {
            StaTestThread.Run(() =>
            {
                var timeEditor = new TimeValueEditControl { EditTime = new TimeSpan(15, 20, 0) };

                timeEditor.TimeFormat = eTimeFormat.TwentyFourHour;
                Assert.IsFalse(timeEditor.UsesTwelveHourClock, "No half of the day on a twenty four hour clock");
                Assert.AreEqual("15", timeEditor.HourText);

                timeEditor.TimeFormat = eTimeFormat.TwelveHour;
                Assert.IsTrue(timeEditor.UsesTwelveHourClock);
                Assert.AreEqual("3", timeEditor.HourText);
            });
        }

        [TestMethod]
        public void WpfDateTimeEditIngressControl_TwentyFourHour_ShowsNoAmOrPm ()
        {
            StaTestThread.Run(() =>
            {
                var control = new DateTimeEditIngressControl { IncludeTime = true, TimeFormat = eTimeFormat.TwentyFourHour, EditDateTime = new DateTime(2026, 11, 25, 15, 5, 0) };
                TextBox textBox = HostAndGetTextBox(control);

                Assert.IsTrue(textBox.Text.Contains("15"), $"'{textBox.Text}' is on a twenty four hour clock");
                string afternoon = CultureInfo.CurrentCulture.DateTimeFormat.PMDesignator;
                Assert.IsTrue(String.IsNullOrEmpty(afternoon) || !textBox.Text.Contains(afternoon), $"'{textBox.Text}' shows no AM or PM");
            });
        }

        [TestMethod]
        public void WpfDateValueEditControl_YearPickerLeftWithX_LeavesTheYearShown ()
        {
            StaTestThread.Run(() =>
            {
                var calendar = new DateValueEditControl { EditDate = new DateTime(2026, 12, 14) };
                calendar.IsYearPickerOpen = true;
                calendar.YearPickerYear = 2031;

                DateValueEditControl.CancelYearPickCommand.Execute(null, calendar);

                Assert.AreEqual(new DateTime(2026, 12, 1), calendar.DisplayMonth);
                Assert.IsFalse(calendar.IsYearPickerOpen);
            });
        }

        [TestMethod]
        public void WpfDateValueEditControl_YearPastWhatThereIs_ComesBackToTheLastThatShows ()
        {
            StaTestThread.Run(() =>
            {
                var calendar = new DateValueEditControl { DisplayMonth = new DateTime(2026, 12, 1) };

                calendar.DisplayYear = 10000;

                Assert.AreEqual(new DateTime(9999, 11, 1), calendar.DisplayMonth);
                Assert.AreEqual(9999, calendar.DisplayYear);
            });
        }

        [TestMethod]
        public void WpfTimeValueEditControl_HourTypedOnATwelveHourClock_StaysInTheAfternoon ()
        {
            StaTestThread.Run(() =>
            {
                var timeEditor = new TimeValueEditControl { TimeFormat = eTimeFormat.TwelveHour, EditTime = new TimeSpan(15, 20, 0) };
                TextBox hour = HostAndGetPart(timeEditor, "PART_HourTextBox");

                Type(hour, "4");
                timeEditor.CommitHourText();

                Assert.AreEqual(new TimeSpan(16, 20, 0), timeEditor.EditTime);
                Assert.AreEqual("4", hour.Text);
            });
        }

        [TestMethod]
        public void WpfTimeValueEditControl_TypedTextThatIsNoMinute_GoesBackToTheMinuteHeld ()
        {
            StaTestThread.Run(() =>
            {
                var timeEditor = new TimeValueEditControl { EditTime = new TimeSpan(15, 20, 0) };
                TextBox minute = HostAndGetPart(timeEditor, "PART_MinuteTextBox");

                Type(minute, "75");
                timeEditor.CommitMinuteText();

                Assert.AreEqual(new TimeSpan(15, 20, 0), timeEditor.EditTime);
                Assert.AreEqual("20", minute.Text, "The text is put back, though the minute never changed");
            });
        }

        [TestMethod]
        public void WpfTimeValueEditControl_ReadOnly_TakesNoTypedHour ()
        {
            StaTestThread.Run(() =>
            {
                var timeEditor = new TimeValueEditControl { EditTime = new TimeSpan(15, 20, 0), IsReadOnly = true };
                TextBox hour = HostAndGetPart(timeEditor, "PART_HourTextBox");

                Type(hour, "4");
                timeEditor.CommitHourText();

                Assert.AreEqual(new TimeSpan(15, 20, 0), timeEditor.EditTime);
            });
        }

        // ===========[ Helper Methods ]========================================
        private static TextBox HostAndGetPart (Control control, string partName)
        {
            var host = new StackPanel();
            host.Children.Add(control);
            host.Measure(kMeasureSize);
            return (TextBox)control.Template.FindName(partName, control);
        }

        // Typing sets a text box's current value, which keeps its binding. Setting Text from code sets a local value,
        //  which replaces a OneWay binding, and then nothing can push the held value back into the box.
        private static void Type (TextBox box, string text) => box.SetCurrentValue(TextBox.TextProperty, text);

        // A measure applies the control's template, which is where its text box and pickers come from
        private static TextBox HostAndGetTextBox (DateTimeEditIngressControl control)
        {
            var host = new StackPanel();
            host.Children.Add(control);
            host.Measure(kMeasureSize);
            return (TextBox)control.Template.FindName("PART_TextBox", control);
        }

        // ===========[ Subclasses/structs ]====================================
        // Reads "soon" as one fixed day, and shows any value as "Soon", which it can't read: a product's own shorthand
        //  whose shown form doesn't read back
        private class SoonResolver : IDateTimeTextResolver
        {
            public static readonly DateTime kSoon = new DateTime(2026, 12, 1);

            public DateTimeTextResolution Resolve (string text, DateTimeTextContext context)
            {
                return (text == "soon") ? DateTimeTextResolution.Resolved(kSoon) : DateTimeTextResolution.Failed("Not soon");
            }

            public string Format (DateTime? value, DateTimeTextContext context) => (value == null) ? String.Empty : "Soon";
        }
    }
}
