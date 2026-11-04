namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using UIKit;

    /// <summary>
    /// The UIKit EditableTextBlock. Its edit rules are AJut.UX's EditableTextState, tested headless; these cover what only
    /// the view can get wrong: the label and field trading places, the field's text reaching Text, ValueChanged for edits
    /// only, and every way an edit ends. A gesture can't be driven from a test, so an edit starts the way VoiceOver starts
    /// one, through AccessibilityActivate, which runs the same path a tap does.
    /// </summary>
    [TestClass]
    public class EditableTextBlockTests
    {
        // ===[ Starting an edit ]===

        [TestMethod]
        public void EditableTextBlock_Activate_StartsAnEdit_InTheField ()
        {
            var block = new EditableTextBlock { Text = "Kestrel" };
            using (TestHost.Attach(block))
            {
                Assert.IsTrue(block.AccessibilityActivate());

                Assert.IsTrue(block.IsEditing);
                Assert.IsFalse(FieldOf(block).Hidden);
                Assert.IsTrue(LabelOf(block).Hidden);
                Assert.AreEqual("Kestrel", FieldOf(block).Text);
                Assert.IsTrue(FieldOf(block).IsFirstResponder, "The keyboard comes up with the edit");
            }
        }

        [TestMethod]
        public void EditableTextBlock_DuringAnEdit_VoiceOverReachesTheField ()
        {
            var block = new EditableTextBlock { Text = "Kestrel" };
            using (TestHost.Attach(block))
            {
                Assert.IsTrue(block.IsAccessibilityElement, "Out of an edit the control is one element, read as its text");

                block.IsEditing = true;
                Assert.IsFalse(block.IsAccessibilityElement, "An element hides what is under it, the field included");

                block.IsEditing = false;
                Assert.IsTrue(block.IsAccessibilityElement);
            }
        }

        [TestMethod]
        public void EditableTextBlock_Manually_IgnoresActivate_ButStartsFromCode ()
        {
            var block = new EditableTextBlock { Text = "Kestrel", EditTextInstigatorAction = eEditTextTouchInstigator.Manually };
            using (TestHost.Attach(block))
            {
                Assert.IsFalse(block.AccessibilityActivate());
                Assert.IsFalse(block.IsEditing);

                block.IsEditing = true;
                Assert.IsTrue(block.IsEditing);
            }
        }

        [TestMethod]
        public void EditableTextBlock_ReadOnly_NeverStartsAnEdit ()
        {
            var block = new EditableTextBlock { Text = "Kestrel", IsReadOnly = true };
            using (TestHost.Attach(block))
            {
                block.AccessibilityActivate();
                block.IsEditing = true;

                Assert.IsFalse(block.IsEditing);
            }
        }

        [TestMethod]
        public void EditableTextBlock_Disabled_NeverStartsAnEdit_AndDisablesTheField ()
        {
            var block = new EditableTextBlock { Text = "Kestrel", Enabled = false };
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;

                Assert.IsFalse(block.IsEditing);
                Assert.IsFalse(FieldOf(block).Enabled);
            }
        }

        [TestMethod]
        public void EditableTextBlock_StartedEmpty_ShowsTheMessageAsThePlaceholder ()
        {
            var block = new EditableTextBlock { NoTextSetMessage = "Name it" };
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;

                Assert.AreEqual("Name it", FieldOf(block).AttributedPlaceholder?.Value);
            }
        }

        // ===[ Typing ]===

        [TestMethod]
        public void EditableTextBlock_Typing_ReachesText_AndSendsValueChanged ()
        {
            var block = new EditableTextBlock { Text = "Kestrel" };
            int valueChanges = 0;
            block.AddTarget((sender, e) => ++valueChanges, UIControlEvent.ValueChanged);
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;
                Type(block, "Merlin");

                Assert.AreEqual("Merlin", block.Text);
                Assert.AreEqual(1, valueChanges);
            }
        }

        [TestMethod]
        public void EditableTextBlock_TextFromCode_SendsNoValueChanged ()
        {
            var block = new EditableTextBlock { Text = "Kestrel" };
            int valueChanges = 0;
            var textChanges = new List<string>();
            block.AddTarget((sender, e) => ++valueChanges, UIControlEvent.ValueChanged);
            block.TextChanged += (sender, e) => textChanges.Add(e.Value);

            block.Text = "Hobby";

            Assert.AreEqual(0, valueChanges, "A UIControl sends its edit event for the user's changes only");
            CollectionAssert.AreEqual(new[] { "Hobby" }, textChanges);
        }

        // ===[ Ending an edit ]===

        [TestMethod]
        public void EditableTextBlock_Return_KeepsTheEdit ()
        {
            var block = new EditableTextBlock { Text = "Kestrel" };
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;
                Type(block, "Merlin");
                FieldOf(block).ShouldReturn(FieldOf(block));

                Assert.IsFalse(block.IsEditing);
                Assert.AreEqual("Merlin", block.Text);
                Assert.IsTrue(FieldOf(block).Hidden);
                Assert.IsFalse(LabelOf(block).Hidden);
                Assert.AreEqual("Merlin", LabelOf(block).Text);
            }
        }

        [TestMethod]
        public void EditableTextBlock_GivingUpFocus_EndsTheEdit_TheLostFocusWay ()
        {
            var block = new EditableTextBlock { Text = "Kestrel", LostFocusEditClose = eEditTextCloseAction.Cancel };
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;
                Type(block, "Merlin");
                FieldOf(block).ResignFirstResponder();

                Assert.IsFalse(block.IsEditing);
                Assert.AreEqual("Kestrel", block.Text);
                Assert.AreEqual("Merlin", block.EditText, "The canceled edit is kept for the next one");
            }
        }

        [TestMethod]
        public void EditableTextBlock_LeavingTheWindow_EndsTheEdit_TheLostFocusWay ()
        {
            var block = new EditableTextBlock { Text = "Kestrel" };
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;
                Type(block, "Merlin");
            }

            Assert.IsFalse(block.IsEditing);
            Assert.AreEqual("Merlin", block.Text, "Apply is the default for an edit that ends that way");
        }

        [TestMethod]
        public void EditableTextBlock_CanceledEdit_IsPickedUpByTheNextEdit ()
        {
            var block = new EditableTextBlock { Text = "Kestrel", LostFocusEditClose = eEditTextCloseAction.Cancel };
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;
                Type(block, "Merl");
                FieldOf(block).ResignFirstResponder();

                block.AccessibilityActivate();

                Assert.AreEqual("Merl", FieldOf(block).Text);
                Assert.AreEqual("Merl", block.Text);
            }
        }

        [TestMethod]
        public void EditableTextBlock_Empty_LabelShowsTheMessage ()
        {
            var block = new EditableTextBlock { NoTextSetMessage = "Name it" };

            Assert.AreEqual("Name it", LabelOf(block).AttributedText?.Value);
            Assert.IsTrue(block.IsEmpty);
        }

        // ===[ Binding ]===

        [TestMethod]
        public void EditableTextBlock_BindText_PushesBothWays ()
        {
            var model = new Model { Name = "Kestrel" };
            var block = new EditableTextBlock();
            using var bindings = new BindingSet();
            using (TestHost.Attach(block))
            {
                bindings.BindText(block, model, nameof(Model.Name), () => model.Name, value => model.Name = value);
                Assert.AreEqual("Kestrel", block.Text);

                model.Name = "Hobby";
                Assert.AreEqual("Hobby", block.Text);

                block.IsEditing = true;
                Type(block, "Merlin");
                Assert.AreEqual("Merlin", model.Name);
            }
        }

        // ===[ Helpers ]===

        private static UILabel LabelOf (EditableTextBlock block) => block.Subviews.OfType<UILabel>().Single();

        private static UITextField FieldOf (EditableTextBlock block) => block.Subviews.OfType<UITextField>().Single();

        // A keystroke as UIKit reports one: the field's text changes, then EditingChanged
        private static void Type (EditableTextBlock block, string text)
        {
            UITextField field = FieldOf(block);
            field.Text = text;
            field.SendActionForControlEvents(UIControlEvent.EditingChanged);
        }

        private sealed class Model : INotifyPropertyChanged
        {
            private string m_name;

            public event PropertyChangedEventHandler PropertyChanged;

            public string Name
            {
                get => m_name;
                set
                {
                    m_name = value;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Name)));
                }
            }
        }
    }
}
