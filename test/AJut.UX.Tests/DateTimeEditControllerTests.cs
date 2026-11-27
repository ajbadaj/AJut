namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// What the AppKit, UIKit and Android DateTimeEditIngressControls do through <see cref="DateTimeEditController"/>:
    /// taking typed text, leaving text that can't be read, keeping the rest of the value when a date or time is picked,
    /// and saying when a person changed it. What text comes to is tested with the rules themselves.
    /// </summary>
    [TestClass]
    public class DateTimeEditControllerTests
    {
        // ===========[ Typed text ]============================================
        [TestMethod]
        public void DateTimeEditController_TypedDateCommitted_BecomesTheValueAndSaysSo ()
        {
            var host = new TestHost();

            host.Type("2026-12-24");
            Assert.IsNull(host.Controller.Value, "Typing alone changes nothing");
            Assert.IsTrue(host.Controller.CommitText());

            Assert.AreEqual(new DateTime(2026, 12, 24), host.Controller.Value);
            Assert.AreEqual(1, host.UserEdits.Count);
            Assert.IsNull(host.UserEdits[0].OldValue);
            Assert.AreEqual(new DateTime(2026, 12, 24), host.UserEdits[0].NewValue);
            Assert.AreEqual(1, host.PersonChanges, "The host hears the value changed, and that a person changed it");
            Assert.IsFalse(host.Controller.HasUncommittedText);
        }

        [TestMethod]
        public void DateTimeEditController_TextThatIsNoDate_IsInvalidAndLeavesTheValue ()
        {
            var held = new DateTime(2026, 3, 4);
            var host = new TestHost(held);

            host.Type("soonish");

            Assert.IsFalse(host.Controller.IsTextValid);
            Assert.IsFalse(String.IsNullOrEmpty(host.Controller.InvalidTextReason), "The text says why it can't be read");
            Assert.AreEqual(host.Controller.InvalidTextReason, host.Controller.CurrentToolTip);
            Assert.IsFalse(host.Controller.CommitText(), "Text that can't be read is not taken");
            Assert.AreEqual(held, host.Controller.Value);
            Assert.AreEqual("soonish", host.Controller.Text, "And it stays as typed, to be fixed");
            Assert.AreEqual(0, host.UserEdits.Count);
        }

        [TestMethod]
        public void DateTimeEditController_TypedText_ToolTipSaysWhatItWillComeTo ()
        {
            var host = new TestHost();

            host.Type("2026-12-24");

            Assert.IsTrue(host.Controller.IsTextValid);
            Assert.AreEqual(new DateTime(2026, 12, 24).ToShortDateString(), host.Controller.CurrentToolTip);
        }

        [TestMethod]
        public void DateTimeEditController_TextThatOnlyShowsTheValue_IsNeverReadBackIn ()
        {
            // The short date and time shown drops the seconds, which the value keeps
            var held = new DateTime(2026, 3, 4, 10, 30, 45);
            var host = new TestHost();
            host.Controller.IncludeTime = true;
            host.Controller.Value = held;

            Assert.IsFalse(host.Controller.CommitText());
            Assert.AreEqual(held, host.Controller.Value);
        }

        [TestMethod]
        public void DateTimeEditController_TextShownByTheController_IsNotTakenAsTyping ()
        {
            // Android and UIKit raise their text changed event for a set from code too, which the host passes straight on
            var host = new TestHost { EchoesShownText = true };

            host.Controller.Value = new DateTime(2026, 3, 4);

            Assert.IsFalse(host.Controller.HasUncommittedText);
            Assert.IsNull(host.Controller.CurrentToolTip);
        }

        [TestMethod]
        public void DateTimeEditController_ValueSetFromCode_ReplacesWhatWasTypedAndIsNoUserEdit ()
        {
            var host = new TestHost();
            host.Type("soonish");

            host.Controller.Value = new DateTime(2026, 5, 6);

            Assert.AreEqual(new DateTime(2026, 5, 6).ToShortDateString(), host.ShownText);
            Assert.IsTrue(host.Controller.IsTextValid);
            Assert.IsFalse(host.Controller.HasUncommittedText);
            Assert.AreEqual(0, host.UserEdits.Count);
            Assert.AreEqual(1, host.CodeChanges);
            Assert.AreEqual(0, host.PersonChanges);
        }

        [TestMethod]
        public void DateTimeEditController_EscapeAfterTyping_PutsTheValueBack ()
        {
            var held = new DateTime(2026, 3, 4);
            var host = new TestHost(held);
            host.Type("soonish");

            host.Controller.ShowValueAsText();

            Assert.AreEqual(held.ToShortDateString(), host.ShownText);
            Assert.IsTrue(host.Controller.IsTextValid);
            Assert.IsFalse(host.Controller.CommitText(), "Nothing typed is left to take");
            Assert.AreEqual(held, host.Controller.Value);
        }

        [TestMethod]
        public void DateTimeEditController_EmptyTextCommitted_ClearsTheValue ()
        {
            var held = new DateTime(2026, 3, 4);
            var host = new TestHost(held);

            host.Type("");
            Assert.IsTrue(host.Controller.CommitText());

            Assert.IsNull(host.Controller.Value);
            Assert.AreEqual(1, host.UserEdits.Count);
            Assert.AreEqual(held, host.UserEdits[0].OldValue);
        }

        [TestMethod]
        public void DateTimeEditController_EmptyTreatedAsInvalid_ShowsNoValueAsInvalidAndKeepsTheValueOnEmptyText ()
        {
            var host = new TestHost();
            host.Controller.TreatEmptyAsInvalid = true;
            Assert.IsFalse(host.Controller.IsTextValid, "No value, when none is allowed, shows as invalid");

            host.Controller.Value = new DateTime(2026, 3, 4);
            Assert.IsTrue(host.Controller.IsTextValid);

            host.Type("");
            Assert.IsFalse(host.Controller.CommitText());
            Assert.AreEqual(new DateTime(2026, 3, 4), host.Controller.Value);
        }

        [TestMethod]
        public void DateTimeEditController_ReadOnly_TakesNoTypedTextAndNoPicks ()
        {
            var host = new TestHost();
            host.Controller.IncludeTime = true;
            host.Controller.IsReadOnly = true;

            host.Type("2026-12-24");

            Assert.IsFalse(host.Controller.CommitText());
            Assert.IsNull(host.Controller.Value);
            Assert.IsTrue(host.Controller.BeginPickerEdit(), "The pickers open to show the value");
            host.Controller.PickDate(new DateTime(2026, 8, 9));
            host.Controller.PickTimeOfDay(new TimeSpan(9, 30, 0));
            host.Controller.EndPickerEdit();
            Assert.IsNull(host.Controller.Value, "And take no picks");
        }

        [TestMethod]
        public void DateTimeEditController_TimeFormatChanged_ShowsTheValueOnThatClock ()
        {
            var host = new TestHost();
            host.Controller.IncludeDate = false;
            host.Controller.IncludeTime = true;
            host.Controller.Value = new DateTime(2026, 3, 4, 15, 5, 0);

            host.Controller.TimeFormat = eTimeFormat.TwentyFourHour;
            Assert.IsFalse(host.Controller.UsesTwelveHourClock);
            Assert.IsTrue(host.ShownText.Contains("15"), $"'{host.ShownText}' is on a twenty four hour clock");

            host.Controller.TimeFormat = eTimeFormat.TwelveHour;
            Assert.IsTrue(host.Controller.UsesTwelveHourClock);
            Assert.IsFalse(host.ShownText.Contains("15"), $"'{host.ShownText}' is on a twelve hour clock");
        }

        // ===========[ Pickers ]===============================================
        [TestMethod]
        public void DateTimeEditController_DatePickedWithATimeHeld_KeepsTheTime ()
        {
            var host = new TestHost();
            host.Controller.IncludeTime = true;
            host.Controller.Value = new DateTime(2026, 3, 4, 15, 45, 0);

            Assert.IsTrue(host.Controller.BeginPickerEdit());
            bool isEditDone = host.Controller.PickDate(new DateTime(2026, 8, 9));

            Assert.IsFalse(isEditDone, "A time is still there to pick, so the pickers stay open");
            Assert.AreEqual(new DateTime(2026, 8, 9, 15, 45, 0), host.Controller.Value);
            Assert.AreEqual(0, host.UserEdits.Count, "The edit completes when the pickers close");
        }

        [TestMethod]
        public void DateTimeEditController_DatePickedOnADateOnlyControl_EndsTheEdit ()
        {
            var host = new TestHost();

            host.Controller.BeginPickerEdit();
            bool isEditDone = host.Controller.PickDate(new DateTime(2026, 8, 9));

            Assert.IsTrue(isEditDone);
            Assert.AreEqual(new DateTime(2026, 8, 9), host.Controller.Value);
        }

        [TestMethod]
        public void DateTimeEditController_TimePickedOnATimeOnlyControl_KeepsTheDate ()
        {
            var host = new TestHost();
            host.Controller.IncludeDate = false;
            host.Controller.IncludeTime = true;
            host.Controller.Value = new DateTime(2026, 3, 4, 8, 0, 0);

            host.Controller.BeginPickerEdit();
            host.Controller.PickTimeOfDay(new TimeSpan(17, 15, 0));

            Assert.AreEqual(new DateTime(2026, 3, 4, 17, 15, 0), host.Controller.Value);
        }

        [TestMethod]
        public void DateTimeEditController_TimePickedWithNoValue_IsToday ()
        {
            var host = new TestHost();
            host.Controller.IncludeTime = true;
            var pickedTime = new TimeSpan(9, 0, 0);

            // Today is read on each side of the pick, so a run that crosses midnight takes either day
            DateTime todayBeforePick = DateTime.Today;
            host.Controller.BeginPickerEdit();
            host.Controller.PickTimeOfDay(pickedTime);
            DateTime todayAfterPick = DateTime.Today;

            DateTime? value = host.Controller.Value;
            Assert.IsTrue(
                (value == todayBeforePick + pickedTime) || (value == todayAfterPick + pickedTime),
                $"'{value}' is today at the time picked"
            );
        }

        [TestMethod]
        public void DateTimeEditController_PickersOpenedOverTypedText_TakeItFirst ()
        {
            var host = new TestHost();
            host.Type("2026-12-24");

            host.Controller.BeginPickerEdit();

            Assert.AreEqual(new DateTime(2026, 12, 24), host.Controller.Value, "The pickers open on the value the text box shows");
            Assert.AreEqual(1, host.UserEdits.Count, "Taking the typed text is its own edit");
        }

        [TestMethod]
        public void DateTimeEditController_PickersClosedOnAChangedValue_SaysSoOnce ()
        {
            var held = new DateTime(2026, 3, 4);
            var host = new TestHost(held);

            host.Controller.BeginPickerEdit();
            host.Controller.PickDate(new DateTime(2026, 4, 1));
            host.Controller.PickDate(new DateTime(2026, 4, 2));
            host.Controller.EndPickerEdit();
            host.Controller.EndPickerEdit();

            Assert.AreEqual(1, host.UserEdits.Count);
            Assert.AreEqual(held, host.UserEdits[0].OldValue);
            Assert.AreEqual(new DateTime(2026, 4, 2), host.UserEdits[0].NewValue);
            Assert.AreEqual(2, host.PersonChanges, "Each pick changes the value as it is made");
        }

        [TestMethod]
        public void DateTimeEditController_PickersClosedBackOnTheValueTheyOpenedOn_SayNothing ()
        {
            var held = new DateTime(2026, 3, 4);
            var host = new TestHost(held);

            host.Controller.BeginPickerEdit();
            host.Controller.PickDate(new DateTime(2026, 4, 1));
            host.Controller.PickDate(held);
            host.Controller.EndPickerEdit();

            Assert.AreEqual(0, host.UserEdits.Count);
        }

        // ===========[ Subclasses/structs ]====================================
        // Stands in for a toolkit's control: keeps what the controller shows it, and counts what it is told
        private sealed class TestHost : IDateTimeEditHost
        {
            public TestHost (DateTime? startValue = null)
            {
                this.Controller = new DateTimeEditController(this);
                this.Controller.Value = startValue;
                this.Controller.ShowValueAsText();
                this.CodeChanges = 0;
            }

            public DateTimeEditController Controller { get; }

            /// <summary>Whether ShowText raises a text edit the way a set from code does on Android and UIKit</summary>
            public bool EchoesShownText { get; set; }

            public string ShownText { get; private set; } = String.Empty;
            public int PersonChanges { get; private set; }
            public int CodeChanges { get; private set; }
            public List<UserEditAppliedEventArgs> UserEdits { get; } = new List<UserEditAppliedEventArgs>();

            public void Type (string text) => this.Controller.OnTextEdited(text);

            public void ShowText (string text)
            {
                this.ShownText = text;
                if (this.EchoesShownText)
                {
                    this.Controller.OnTextEdited(text);
                }
            }

            public void OnValueChanged (bool isFromPerson)
            {
                if (isFromPerson)
                {
                    ++this.PersonChanges;
                }
                else
                {
                    ++this.CodeChanges;
                }
            }

            public void ShowTextState ()
            {
            }

            public void RaiseUserEditComplete (DateTime? oldValue, DateTime? newValue)
            {
                this.UserEdits.Add(new UserEditAppliedEventArgs(oldValue, newValue));
            }
        }
    }
}
