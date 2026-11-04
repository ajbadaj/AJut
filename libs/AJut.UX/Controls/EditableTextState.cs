namespace AJut.UX.Controls
{
    // ===========[ EditableTextState ]==========================================
    // The edit rules of the mobile EditableTextBlocks, shared by Android and UIKit so the two behave
    // the same, and the same as WPF's EditableTextBlock: editing starts from the text as it is, the
    // text follows every keystroke, Apply keeps it, and Cancel puts back what was there before.
    //
    // A canceled edit is not thrown away. The next edit starts from it, so a user who backed out of
    // an edit by mistake picks up where they were; only a kept edit clears it. That is WPF's rule, and
    // it holds even when the text was changed from code in between.
    //
    // A read only EditableTextBlock never starts editing here. WPF's opens a read only text box so its
    // text can be selected; a phone selects text in a label it can long press instead, so there is
    // nothing for a read only edit to give.
    public sealed class EditableTextState
    {
        // ===========[ Properties ]============================================
        /// <summary>The text: the one shown, and while editing, the one being typed</summary>
        public string? Text { get; set; }

        /// <summary>Whether an edit is going on</summary>
        public bool IsEditing { get; private set; }

        /// <summary>Whether <see cref="Text"/> is null or empty</summary>
        public bool IsEmpty => string.IsNullOrEmpty(this.Text);

        /// <summary>The text as it was when the edit started, which Cancel puts back</summary>
        public string? PreviousText { get; private set; }

        /// <summary>The text of the last canceled edit, which the next edit starts from; null after an edit is kept</summary>
        public string? EditText { get; private set; }

        /// <summary>Whether the text was empty when the edit started</summary>
        public bool StartedEditingEmpty { get; private set; }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Starts an edit, from the last canceled edit's text if there is one. Refused, returning false, while already
        /// editing or when <paramref name="isReadOnly"/>.
        /// </summary>
        public bool TryBeginEdit (bool isReadOnly)
        {
            if (this.IsEditing || isReadOnly)
            {
                return false;
            }

            this.PreviousText = this.Text;
            if (this.EditText != null)
            {
                this.Text = this.EditText;
            }

            this.StartedEditingEmpty = this.IsEmpty;
            this.IsEditing = true;
            return true;
        }

        /// <summary>Ends the edit keeping the text as typed. Returns false when not editing.</summary>
        public bool Commit ()
        {
            if (!this.IsEditing)
            {
                return false;
            }

            this.IsEditing = false;
            this.EditText = null;
            this.PreviousText = null;
            return true;
        }

        /// <summary>
        /// Ends the edit putting back the text from before it, and keeps the typed text for the next edit. Returns false
        /// when not editing.
        /// </summary>
        public bool Cancel ()
        {
            if (!this.IsEditing)
            {
                return false;
            }

            this.EditText = this.Text;
            this.Text = this.PreviousText;
            this.IsEditing = false;
            return true;
        }

        /// <summary>Ends the edit the way <paramref name="action"/> says: Apply commits, Cancel cancels</summary>
        public bool Close (eEditTextCloseAction action) => (action == eEditTextCloseAction.Cancel) ? this.Cancel() : this.Commit();

        /// <summary>Where the selection starts when editing begins, for <paramref name="behavior"/></summary>
        public Selection GetStartingSelection (eEditTextInitializationBehavior behavior)
        {
            int length = this.Text?.Length ?? 0;
            return (behavior == eEditTextInitializationBehavior.SelectAll) ? new Selection(0, length) : new Selection(length, length);
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>A selection in the text, from <paramref name="Start"/> up to <paramref name="End"/>; equal for a caret with nothing selected</summary>
        public readonly record struct Selection (int Start, int End);
    }
}
