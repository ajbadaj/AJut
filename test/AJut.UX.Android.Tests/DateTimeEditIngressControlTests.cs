namespace AJut.UX.Tests
{
    using System;
    using System.Threading.Tasks;
    using AJut;
    using AJut.UX;
    using AJut.UX.Controls;
    using Android.Views;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The Android DateTimeEditIngressControl through its text field and its picker dialog. What typed text comes to, and
    /// what a pick does to the value, are tested with the shared controller and rules; these are the control taking text
    /// from its field, showing what it can't take, and opening and closing its pickers.
    /// </summary>
    [TestClass]
    public class DateTimeEditIngressControlTests
    {
        private static readonly TimeSpan kDismissTimeout = TimeSpan.FromSeconds(10);

        [TestMethod]
        public void DateTimeEditIngressControl_TypedDateCommitted_BecomesTheValueAndSaysSo ()
        {
            var control = new DateTimeEditIngressControl(TestHost.RequiredContext);
            using IDisposable attached = TestHost.Attach(control);
            int changes = 0;
            UserEditAppliedEventArgs? edit = null;
            control.EditDateTimeChanged += _OnChanged;
            control.UserEditComplete += _OnUserEdit;

            control.TextField.Text = "2026-12-24";
            Assert.IsNull(control.EditDateTime, "Typing alone changes nothing");
            Assert.IsTrue(control.CommitText());
            control.EditDateTimeChanged -= _OnChanged;
            control.UserEditComplete -= _OnUserEdit;

            Assert.AreEqual(new DateTime(2026, 12, 24), control.EditDateTime);
            Assert.AreEqual(new DateTime(2026, 12, 24).ToShortDateString(), control.TextField.Text, "The field shows the value it took, formatted");
            Assert.AreEqual(1, changes);
            Assert.IsNotNull(edit);
            Assert.IsNull(edit.OldValue);

            void _OnChanged (object? sender, EventArgs<DateTime?> e) => ++changes;
            void _OnUserEdit (object? sender, UserEditAppliedEventArgs e) => edit = e;
        }

        [TestMethod]
        public void DateTimeEditIngressControl_TextThatIsNoDate_ShowsTheWarningAndLeavesTheValue ()
        {
            var held = new DateTime(2026, 3, 4);
            var control = new DateTimeEditIngressControl(TestHost.RequiredContext) { EditDateTime = held };
            using IDisposable attached = TestHost.Attach(control);

            control.TextField.Text = "soonish";

            Assert.IsFalse(control.IsTextValid);
            Assert.AreEqual("'soonish' isn't a date", control.InvalidTextReason);
            Assert.AreEqual(ViewStates.Visible, FindWarningGlyph(control).Visibility);
            Assert.IsFalse(control.CommitText(), "Text that can't be read is not taken");
            Assert.AreEqual(held, control.EditDateTime);
            Assert.AreEqual("soonish", control.TextField.Text, "And it stays as typed, to be fixed");
        }

        [TestMethod]
        public void DateTimeEditIngressControl_ValueSetFromCode_IsShownAndIsNoUserEdit ()
        {
            var control = new DateTimeEditIngressControl(TestHost.RequiredContext);
            using IDisposable attached = TestHost.Attach(control);
            int userEdits = 0;
            control.UserEditComplete += _OnUserEdit;

            control.EditDateTime = new DateTime(2026, 5, 6);
            control.UserEditComplete -= _OnUserEdit;

            Assert.AreEqual(new DateTime(2026, 5, 6).ToShortDateString(), control.TextField.Text);
            Assert.IsTrue(control.IsTextValid);
            Assert.IsFalse(control.CommitText(), "Text the control showed itself is never read back in");
            Assert.AreEqual(0, userEdits);

            void _OnUserEdit (object? sender, UserEditAppliedEventArgs e) => ++userEdits;
        }

        [TestMethod]
        public void DateTimeEditIngressControl_TwentyFourHour_ShowsTheTimeOnThatClock ()
        {
            var control = new DateTimeEditIngressControl(TestHost.RequiredContext) { IncludeDate = false, IncludeTime = true, TimeFormat = eTimeFormat.TwentyFourHour };

            control.EditDateTime = new DateTime(2026, 3, 4, 15, 5, 0);

            Assert.IsTrue(control.TextField.Text!.Contains("15"), $"'{control.TextField.Text}' is on a twenty four hour clock");
        }

        [TestMethod]
        public async Task DateTimeEditIngressControl_ReadOnly_OpensPickersAndTakesNoPicks ()
        {
            var held = new DateTime(2026, 3, 4);
            var control = new DateTimeEditIngressControl(TestHost.RequiredContext) { EditDateTime = held, IsReadOnly = true };
            int userEdits = 0;
            control.UserEditComplete += _OnUserEdit;

            control.OpenPickers();
            Assert.IsTrue(control.ShowEditDisplay, "Read-only, the pickers still open, to show the value");

            // A day tapped on the calendar (its months count from zero)
            const int kMarch = 2;
            const int kTappedDay = 20;
            Android.Widget.DatePicker calendar = control.OpenCalendar!;
            calendar.UpdateDate(held.Year, kMarch, kTappedDay);

            Assert.AreEqual(held, control.EditDateTime, "The pick is not taken");
            Assert.AreEqual(held.Day, calendar.DayOfMonth, "And the calendar is put back on the value");
            Assert.IsTrue(control.ShowEditDisplay, "A day tapped read-only doesn't close the pickers");
            control.ClosePickers();
            await WaitUntilClosedAsync(control);
            control.UserEditComplete -= _OnUserEdit;

            Assert.AreEqual(0, userEdits);

            void _OnUserEdit (object? sender, UserEditAppliedEventArgs e) => ++userEdits;
        }

        [TestMethod]
        public async Task DateTimeEditIngressControl_PickersClosedWithNothingPicked_SayNothing ()
        {
            var control = new DateTimeEditIngressControl(TestHost.RequiredContext) { IncludeTime = true, EditDateTime = new DateTime(2026, 3, 4, 8, 0, 0) };
            int userEdits = 0;
            control.UserEditComplete += _OnUserEdit;

            control.OpenPickers();
            Assert.IsTrue(control.ShowEditDisplay);
            control.ClosePickers();
            await WaitUntilClosedAsync(control);
            control.UserEditComplete -= _OnUserEdit;

            Assert.AreEqual(0, userEdits);
            Assert.AreEqual(new DateTime(2026, 3, 4, 8, 0, 0), control.EditDateTime, "Opening the pickers on a value doesn't change it");

            void _OnUserEdit (object? sender, UserEditAppliedEventArgs e) => ++userEdits;
        }

        // ===========[ Helper Methods ]===================================
        // The control's children are the field, the warning glyph and the picker button, in that order
        private static View FindWarningGlyph (DateTimeEditIngressControl control) => control.GetChildAt(1)!;

        // A dialog's dismiss listener runs on a later pass of the main looper, so let the looper run until it has
        private static async Task WaitUntilClosedAsync (DateTimeEditIngressControl control)
        {
            const int kPollMilliseconds = 10;
            DateTime deadline = DateTime.UtcNow + kDismissTimeout;
            while (control.ShowEditDisplay && (DateTime.UtcNow < deadline))
            {
                await Task.Delay(kPollMilliseconds);
            }

            Assert.IsFalse(control.ShowEditDisplay, $"The pickers did not close within {kDismissTimeout.TotalSeconds} s");
        }
    }
}
