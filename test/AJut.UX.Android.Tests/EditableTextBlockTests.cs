namespace AJut.UX.Tests
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Views;
    using Android.Views.InputMethods;
    using Android.Widget;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The Android EditableTextBlock on a device. Its edit rules are AJut.UX's EditableTextState, tested headless; these
    /// cover what only the view can get wrong: which gesture starts an edit, the label and field trading places, the
    /// field's text reaching Text, and every way an edit ends. It listens to its children only while attached, so every
    /// test that taps or types attaches it first.
    /// </summary>
    [TestClass]
    public class EditableTextBlockTests
    {
        // ===[ Starting an edit ]===

        [TestMethod]
        public void EditableTextBlock_Tap_StartsAnEdit_InTheField ()
        {
            var block = new EditableTextBlock(TestHost.RequiredContext) { Text = "Kestrel" };
            using (TestHost.Attach(block))
            {
                LabelOf(block).PerformClick();

                Assert.IsTrue(block.IsEditing);
                Assert.AreEqual(ViewStates.Visible, FieldOf(block).Visibility);
                Assert.AreEqual(ViewStates.Invisible, LabelOf(block).Visibility, "Invisible, so the control keeps the label's size");
                Assert.AreEqual("Kestrel", FieldOf(block).Text);
            }
        }

        [TestMethod]
        public void EditableTextBlock_LongPressInstigator_IgnoresATap_AndTakesALongPress ()
        {
            var block = new EditableTextBlock(TestHost.RequiredContext) { Text = "Kestrel", EditTextInstigatorAction = eEditTextTouchInstigator.LongPress };
            using (TestHost.Attach(block))
            {
                LabelOf(block).PerformClick();
                Assert.IsFalse(block.IsEditing);

                LabelOf(block).PerformLongClick();
                Assert.IsTrue(block.IsEditing);
            }
        }

        [TestMethod]
        public void EditableTextBlock_Manually_TakesNoGesture_ButStartsFromCode ()
        {
            var block = new EditableTextBlock(TestHost.RequiredContext) { Text = "Kestrel", EditTextInstigatorAction = eEditTextTouchInstigator.Manually };
            using (TestHost.Attach(block))
            {
                Assert.IsFalse(LabelOf(block).Clickable, "A label that starts nothing leaves its touches to what it sits in");
                LabelOf(block).PerformClick();
                Assert.IsFalse(block.IsEditing);

                block.IsEditing = true;
                Assert.IsTrue(block.IsEditing);
            }
        }

        [TestMethod]
        public void EditableTextBlock_ReadOnly_NeverStartsAnEdit ()
        {
            var block = new EditableTextBlock(TestHost.RequiredContext) { Text = "Kestrel", IsReadOnly = true };
            using (TestHost.Attach(block))
            {
                LabelOf(block).PerformClick();
                block.IsEditing = true;

                Assert.IsFalse(block.IsEditing);
            }
        }

        [TestMethod]
        public void EditableTextBlock_Disabled_NeverStartsAnEdit_AndDisablesItsChildren ()
        {
            var block = new EditableTextBlock(TestHost.RequiredContext) { Text = "Kestrel" };
            block.Enabled = false;
            using (TestHost.Attach(block))
            {
                LabelOf(block).PerformClick();
                block.IsEditing = true;

                Assert.IsFalse(block.IsEditing);
                Assert.IsFalse(LabelOf(block).Enabled);
                Assert.IsFalse(FieldOf(block).Enabled);
            }
        }

        [TestMethod]
        public void EditableTextBlock_SelectAll_SelectsTheText ()
        {
            var block = new EditableTextBlock(TestHost.RequiredContext) { Text = "Kestrel" };
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;

                Assert.AreEqual(0, FieldOf(block).SelectionStart);
                Assert.AreEqual(7, FieldOf(block).SelectionEnd);
            }
        }

        [TestMethod]
        public void EditableTextBlock_CursorAtEnd_PutsTheCaretAfterTheText ()
        {
            var block = new EditableTextBlock(TestHost.RequiredContext) { Text = "Kestrel", EditTextInitializationBehavior = eEditTextInitializationBehavior.CursorAtEnd };
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;

                Assert.AreEqual(7, FieldOf(block).SelectionStart);
                Assert.AreEqual(7, FieldOf(block).SelectionEnd);
            }
        }

        [TestMethod]
        public void EditableTextBlock_StartedEmpty_ShowsTheMessageAsTheHint ()
        {
            var block = new EditableTextBlock(TestHost.RequiredContext) { NoTextSetMessage = "Name it" };
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;

                Assert.AreEqual("Name it", FieldOf(block).Hint);
            }
        }

        // ===[ Typing ]===

        [TestMethod]
        public void EditableTextBlock_Typing_ReachesText_AsItGoes ()
        {
            var block = new EditableTextBlock(TestHost.RequiredContext) { Text = "Kestrel" };
            var changes = new List<string?>();
            block.TextChanged += (sender, e) => changes.Add(e.Value);
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;
                FieldOf(block).Text = "Merlin";

                Assert.AreEqual("Merlin", block.Text);
                CollectionAssert.AreEqual(new[] { "Merlin" }, changes);
            }
        }

        // ===[ Ending an edit ]===

        [TestMethod]
        public void EditableTextBlock_Done_KeepsTheEdit ()
        {
            var block = new EditableTextBlock(TestHost.RequiredContext) { Text = "Kestrel" };
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;
                FieldOf(block).Text = "Merlin";
                FieldOf(block).OnEditorAction(ImeAction.Done);

                Assert.IsFalse(block.IsEditing);
                Assert.AreEqual("Merlin", block.Text);
                Assert.AreEqual(ViewStates.Visible, LabelOf(block).Visibility);
                Assert.AreEqual(ViewStates.Gone, FieldOf(block).Visibility);
                Assert.AreEqual("Merlin", LabelOf(block).Text);
            }
        }

        [TestMethod]
        public void EditableTextBlock_Escape_PutsBackTheText ()
        {
            var block = new EditableTextBlock(TestHost.RequiredContext) { Text = "Kestrel" };
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;
                FieldOf(block).Text = "Merlin";
                FieldOf(block).DispatchKeyEvent(new KeyEvent(KeyEventActions.Down, Keycode.Escape));

                Assert.IsFalse(block.IsEditing);
                Assert.AreEqual("Kestrel", block.Text);
                Assert.AreEqual("Merlin", block.EditText, "The canceled edit is kept for the next one");
            }
        }

        [TestMethod]
        public void EditableTextBlock_CanceledEdit_IsPickedUpByTheNextEdit ()
        {
            var block = new EditableTextBlock(TestHost.RequiredContext) { Text = "Kestrel" };
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;
                FieldOf(block).Text = "Merl";
                FieldOf(block).DispatchKeyEvent(new KeyEvent(KeyEventActions.Down, Keycode.Escape));

                LabelOf(block).PerformClick();

                Assert.AreEqual("Merl", FieldOf(block).Text);
                Assert.AreEqual("Merl", block.Text);
            }
        }

        [TestMethod]
        public void EditableTextBlock_LeavingTheWindow_EndsTheEdit_TheLostFocusWay ()
        {
            var block = new EditableTextBlock(TestHost.RequiredContext) { Text = "Kestrel", LostFocusEditClose = eEditTextCloseAction.Cancel };
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;
                FieldOf(block).Text = "Merlin";
            }

            Assert.IsFalse(block.IsEditing);
            Assert.AreEqual("Kestrel", block.Text);
        }

        [TestMethod]
        public void EditableTextBlock_DisabledDuringAnEdit_EndsIt_TheLostFocusWay ()
        {
            var block = new EditableTextBlock(TestHost.RequiredContext) { Text = "Kestrel" };
            using (TestHost.Attach(block))
            {
                block.IsEditing = true;
                FieldOf(block).Text = "Merlin";

                block.Enabled = false;

                Assert.IsFalse(block.IsEditing);
                Assert.AreEqual("Merlin", block.Text, "Apply is the default for an edit that ends that way");
            }
        }

        [TestMethod]
        public void EditableTextBlock_Empty_LabelShowsTheMessage ()
        {
            var block = new EditableTextBlock(TestHost.RequiredContext) { NoTextSetMessage = "Name it" };

            Assert.AreEqual("Name it", LabelOf(block).Text);
            Assert.IsTrue(block.IsEmpty);
        }

        // ===[ Binding ]===

        [TestMethod]
        public void EditableTextBlock_BindText_PushesBothWays ()
        {
            var model = new Model { Name = "Kestrel" };
            var block = new EditableTextBlock(TestHost.RequiredContext);
            using var bindings = new BindingSet();
            using (TestHost.Attach(block))
            {
                bindings.BindText(block, model, nameof(Model.Name), () => model.Name, value => model.Name = value);
                Assert.AreEqual("Kestrel", block.Text);

                model.Name = "Hobby";
                Assert.AreEqual("Hobby", block.Text);

                block.IsEditing = true;
                FieldOf(block).Text = "Merlin";
                Assert.AreEqual("Merlin", model.Name);
            }
        }

        // ===[ Layout ]===

        [TestMethod]
        public void EditableTextBlock_FromALayout_TakesItsAttributes ()
        {
            View root = LayoutInflater.From(TestHost.RequiredContext)!.Inflate(Resource.Layout.editable_text_block_test, null)!;
            var block = root.FindViewById<EditableTextBlock>(Resource.Id.editable_text_block)!;

            Assert.AreEqual("Kestrel", block.Text);
            Assert.AreEqual("Name it", block.NoTextSetMessage);
            Assert.AreEqual(eEditTextTouchInstigator.DoubleTap, block.EditTextInstigatorAction);
            Assert.AreEqual(eEditTextInitializationBehavior.CursorAtEnd, block.EditTextInitializationBehavior);
            Assert.AreEqual(eEditTextCloseAction.Cancel, block.LostFocusEditClose);
            Assert.IsTrue(block.IsReadOnly);
            Assert.IsFalse(block.Enabled);
        }

        // ===[ Helpers ]===

        private static TextView LabelOf (EditableTextBlock block) => (TextView)block.GetChildAt(0)!;

        private static EditText FieldOf (EditableTextBlock block) => (EditText)block.GetChildAt(1)!;

        private sealed class Model : INotifyPropertyChanged
        {
            private string? m_name;

            public event PropertyChangedEventHandler? PropertyChanged;

            public string? Name
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
