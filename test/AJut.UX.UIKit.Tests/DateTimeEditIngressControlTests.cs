namespace AJut.UX.Tests
{
    using System;
    using System.Linq;
    using AJut.DateAndTime;
    using AJut.UX.Controls;
    using Foundation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using UIKit;

    /// <summary>
    /// The UIKit DateTimeEditIngressControl through its text field. What typed text comes to, and what a pick does to the
    /// value, are tested with the shared controller and rules; these cover what only the view can get wrong: the field's
    /// typing reaching the value, ValueChanged for a person's change only, and the warning glyph.
    /// </summary>
    [TestClass]
    public class DateTimeEditIngressControlTests
    {
        [TestMethod]
        public void DateTimeEditIngressControl_TypedDateCommitted_BecomesTheValueAndSendsValueChanged ()
        {
            var control = new DateTimeEditIngressControl();
            int valueChanges = 0;
            UserEditAppliedEventArgs edit = null;
            control.AddTarget(_OnValueChanged, UIControlEvent.ValueChanged);
            control.UserEditComplete += _OnUserEdit;
            using (TestHost.Attach(control))
            {
                Type(control, "2026-12-24");
                Assert.IsNull(control.EditDateTime, "Typing alone changes nothing");
                Assert.IsTrue(control.CommitText());
            }

            control.RemoveTarget(_OnValueChanged, UIControlEvent.ValueChanged);
            control.UserEditComplete -= _OnUserEdit;
            Assert.AreEqual(new DateTime(2026, 12, 24), control.EditDateTime);
            Assert.AreEqual(new DateTime(2026, 12, 24).ToShortDateString(), control.TextField.Text, "The field shows the value it took, formatted");
            Assert.AreEqual(1, valueChanges);
            Assert.IsNotNull(edit);
            Assert.IsNull(edit.OldValue);

            void _OnValueChanged (object sender, EventArgs e) => ++valueChanges;
            void _OnUserEdit (object sender, UserEditAppliedEventArgs e) => edit = e;
        }

        [TestMethod]
        public void DateTimeEditIngressControl_TextThatIsNoDate_ShowsTheWarningAndLeavesTheValue ()
        {
            var held = new DateTime(2026, 3, 4);
            var control = new DateTimeEditIngressControl { EditDateTime = held };
            using (TestHost.Attach(control))
            {
                Type(control, "soonish");

                Assert.IsFalse(control.IsTextValid);
                Assert.AreEqual("'soonish' isn't a date", control.InvalidTextReason);
                Assert.IsFalse(WarningGlyphOf(control).Hidden);
                Assert.IsFalse(control.CommitText(), "Text that can't be read is not taken");
                Assert.AreEqual(held, control.EditDateTime);
                Assert.AreEqual("soonish", control.TextField.Text, "And it stays as typed, to be fixed");
            }
        }

        [TestMethod]
        public void DateTimeEditIngressControl_ValueFromCode_IsShownAndSendsNoValueChanged ()
        {
            var control = new DateTimeEditIngressControl();
            int valueChanges = 0;
            control.AddTarget(_OnValueChanged, UIControlEvent.ValueChanged);

            control.EditDateTime = new DateTime(2026, 5, 6);
            control.RemoveTarget(_OnValueChanged, UIControlEvent.ValueChanged);

            Assert.AreEqual(new DateTime(2026, 5, 6).ToShortDateString(), control.TextField.Text);
            Assert.IsFalse(control.CommitText(), "Text the control showed itself is never read back in");
            Assert.AreEqual(0, valueChanges);

            void _OnValueChanged (object sender, EventArgs e) => ++valueChanges;
        }

        [TestMethod]
        public void DateTimeEditIngressControl_TwentyFourHour_ShowsTheTimeOnThatClock ()
        {
            var control = new DateTimeEditIngressControl { IncludeDate = false, IncludeTime = true, TimeFormat = eTimeFormat.TwentyFourHour };

            control.EditDateTime = new DateTime(2026, 3, 4, 15, 5, 0);

            Assert.IsTrue(control.TextField.Text.Contains("15"), $"'{control.TextField.Text}' is on a twenty four hour clock");
        }

        [TestMethod]
        public void DateTimeEditIngressControl_ReadOnly_OpensPickersAndTakesNoPicks ()
        {
            var held = new DateTime(2026, 3, 4);
            var control = new DateTimeEditIngressControl { EditDateTime = held, IsReadOnly = true };
            int valueChanges = 0;
            control.AddTarget(_OnValueChanged, UIControlEvent.ValueChanged);
            using (TestHost.Attach(control))
            {
                Assert.IsTrue(control.AccessibilityActivate());
                Assert.IsTrue(control.ShowEditDisplay, "Read-only, the pickers still open, to show the value");

                // A date tapped on the calendar
                UIDatePicker calendar = TestHost.Descendants<UIDatePicker>(PresentedPickersOf(control).View).First(picker => picker.Mode == UIDatePickerMode.Date);
                calendar.Date = (NSDate)DateTime.SpecifyKind(new DateTime(2026, 3, 20, 12, 0, 0), DateTimeKind.Local);
                calendar.SendActionForControlEvents(UIControlEvent.ValueChanged);

                Assert.AreEqual(held, control.EditDateTime, "The pick is not taken");
                Assert.AreEqual(held, ((DateTime)calendar.Date).ToLocalTime().Date, "And the calendar is put back on the value");
                control.ClosePickers();
            }

            control.RemoveTarget(_OnValueChanged, UIControlEvent.ValueChanged);
            Assert.AreEqual(0, valueChanges);

            void _OnValueChanged (object sender, EventArgs e) => ++valueChanges;
        }

        // ===========[ Helper Methods ]===================================
        // Text set from code raises no EditingChanged, so a keystroke's is sent along with it
        private static void Type (DateTimeEditIngressControl control, string text)
        {
            control.TextField.Text = text;
            control.TextField.SendActionForControlEvents(UIControlEvent.EditingChanged);
        }

        // The pickers are presented from the top of the window's stack, so they are its last presented controller
        private static UIViewController PresentedPickersOf (DateTimeEditIngressControl control)
        {
            UIViewController presented = control.Window.RootViewController;
            while (presented.PresentedViewController != null)
            {
                presented = presented.PresentedViewController;
            }

            return presented;
        }

        // The control's row holds the field's well, the warning glyph and the picker button, in that order
        private static UIImageView WarningGlyphOf (DateTimeEditIngressControl control)
        {
            UIStackView row = FindRow(control);
            Assert.IsNotNull(row, "The control has its row");
            return (UIImageView)row.ArrangedSubviews[1];
        }

        private static UIStackView FindRow (UIView root)
        {
            foreach (UIView child in root.Subviews)
            {
                UIStackView row = (child as UIStackView) ?? FindRow(child);
                if (row != null)
                {
                    return row;
                }
            }

            return null;
        }
    }
}
