namespace AJut.UX.Controls
{
    /// <summary>What an EditableTextBlock does with an edit when it stops editing without being told which</summary>
    public enum eEditTextCloseAction
    {
        /// <summary>The edit is thrown out, and the text goes back to what it was before editing started</summary>
        Cancel,

        /// <summary>The edit is kept</summary>
        Apply,
    }
}
