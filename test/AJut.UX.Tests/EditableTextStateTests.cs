#nullable enable

namespace AJut.UX.Tests
{
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EditableTextStateTests
    {
        // ===[ Starting an edit ]===

        [TestMethod]
        public void ETS_Begin_StartsFromTheText_AndRemembersIt ()
        {
            var state = new EditableTextState { Text = "Kestrel" };

            Assert.IsTrue(state.TryBeginEdit(isReadOnly: false));

            Assert.IsTrue(state.IsEditing);
            Assert.AreEqual("Kestrel", state.Text);
            Assert.AreEqual("Kestrel", state.PreviousText);
            Assert.IsFalse(state.StartedEditingEmpty);
        }

        [TestMethod]
        public void ETS_Begin_FromNoText_SaysItStartedEmpty ()
        {
            var state = new EditableTextState();

            state.TryBeginEdit(isReadOnly: false);

            Assert.IsTrue(state.StartedEditingEmpty);
        }

        [TestMethod]
        public void ETS_Begin_WhileReadOnly_IsRefused ()
        {
            var state = new EditableTextState { Text = "Kestrel" };

            Assert.IsFalse(state.TryBeginEdit(isReadOnly: true));

            Assert.IsFalse(state.IsEditing);
            Assert.IsNull(state.PreviousText);
        }

        [TestMethod]
        public void ETS_Begin_WhileEditing_ChangesNothing ()
        {
            var state = new EditableTextState { Text = "Kestrel" };
            state.TryBeginEdit(isReadOnly: false);
            state.Text = "Kes";

            Assert.IsFalse(state.TryBeginEdit(isReadOnly: false));

            Assert.AreEqual("Kestrel", state.PreviousText, "The text from before the edit is still the one Cancel puts back");
        }

        // ===[ Keeping an edit ]===

        [TestMethod]
        public void ETS_Commit_KeepsTheTypedText_AndForgetsTheOld ()
        {
            var state = new EditableTextState { Text = "Kestrel" };
            state.TryBeginEdit(isReadOnly: false);
            state.Text = "Merlin";

            Assert.IsTrue(state.Commit());

            Assert.IsFalse(state.IsEditing);
            Assert.AreEqual("Merlin", state.Text);
            Assert.IsNull(state.PreviousText);
            Assert.IsNull(state.EditText);
        }

        [TestMethod]
        public void ETS_Commit_WhenNotEditing_DoesNothing ()
        {
            var state = new EditableTextState { Text = "Kestrel" };

            Assert.IsFalse(state.Commit());

            Assert.AreEqual("Kestrel", state.Text);
        }

        // ===[ Canceling an edit ]===

        [TestMethod]
        public void ETS_Cancel_PutsBackTheTextFromBefore ()
        {
            var state = new EditableTextState { Text = "Kestrel" };
            state.TryBeginEdit(isReadOnly: false);
            state.Text = "Merlin";

            Assert.IsTrue(state.Cancel());

            Assert.IsFalse(state.IsEditing);
            Assert.AreEqual("Kestrel", state.Text);
        }

        [TestMethod]
        public void ETS_Cancel_KeepsTheTypedText_ForTheNextEdit ()
        {
            var state = new EditableTextState { Text = "Kestrel" };
            state.TryBeginEdit(isReadOnly: false);
            state.Text = "Merl";
            state.Cancel();

            state.TryBeginEdit(isReadOnly: false);

            Assert.AreEqual("Merl", state.Text, "The next edit picks up the one backed out of");
            Assert.AreEqual("Kestrel", state.PreviousText, "And canceling that one puts back the text from before both");
        }

        [TestMethod]
        public void ETS_CommitAfterAPickedUpEdit_ClearsIt_SoTheNextEditStartsFresh ()
        {
            var state = new EditableTextState { Text = "Kestrel" };
            state.TryBeginEdit(isReadOnly: false);
            state.Text = "Merl";
            state.Cancel();
            state.TryBeginEdit(isReadOnly: false);
            state.Text = "Merlin";
            state.Commit();

            state.TryBeginEdit(isReadOnly: false);

            Assert.AreEqual("Merlin", state.Text);
        }

        [TestMethod]
        public void ETS_CanceledEdit_IsPickedUp_EvenAfterTheTextChangedFromCode ()
        {
            var state = new EditableTextState { Text = "Kestrel" };
            state.TryBeginEdit(isReadOnly: false);
            state.Text = "Merl";
            state.Cancel();
            state.Text = "Hobby";

            state.TryBeginEdit(isReadOnly: false);

            Assert.AreEqual("Merl", state.Text);
            Assert.AreEqual("Hobby", state.PreviousText);
        }

        [TestMethod]
        public void ETS_Cancel_WhenNotEditing_DoesNothing ()
        {
            var state = new EditableTextState { Text = "Kestrel" };

            Assert.IsFalse(state.Cancel());

            Assert.AreEqual("Kestrel", state.Text);
            Assert.IsNull(state.EditText);
        }

        // ===[ Closing ]===

        [TestMethod]
        public void ETS_CloseApply_KeepsTheEdit ()
        {
            var state = new EditableTextState { Text = "Kestrel" };
            state.TryBeginEdit(isReadOnly: false);
            state.Text = "Merlin";

            state.Close(eEditTextCloseAction.Apply);

            Assert.AreEqual("Merlin", state.Text);
            Assert.IsNull(state.EditText);
        }

        [TestMethod]
        public void ETS_CloseCancel_PutsBackTheText ()
        {
            var state = new EditableTextState { Text = "Kestrel" };
            state.TryBeginEdit(isReadOnly: false);
            state.Text = "Merlin";

            state.Close(eEditTextCloseAction.Cancel);

            Assert.AreEqual("Kestrel", state.Text);
            Assert.AreEqual("Merlin", state.EditText);
        }

        // ===[ The starting selection ]===

        [TestMethod]
        public void ETS_StartingSelection_SelectAll_CoversTheText ()
        {
            var state = new EditableTextState { Text = "Kestrel" };

            Assert.AreEqual(new EditableTextState.Selection(0, 7), state.GetStartingSelection(eEditTextInitializationBehavior.SelectAll));
        }

        [TestMethod]
        public void ETS_StartingSelection_CursorAtEnd_IsACaretAfterTheText ()
        {
            var state = new EditableTextState { Text = "Kestrel" };

            Assert.AreEqual(new EditableTextState.Selection(7, 7), state.GetStartingSelection(eEditTextInitializationBehavior.CursorAtEnd));
        }

        [TestMethod]
        public void ETS_StartingSelection_NoText_IsACaretAtTheStart ()
        {
            var state = new EditableTextState();

            Assert.AreEqual(new EditableTextState.Selection(0, 0), state.GetStartingSelection(eEditTextInitializationBehavior.SelectAll));
        }
    }
}
