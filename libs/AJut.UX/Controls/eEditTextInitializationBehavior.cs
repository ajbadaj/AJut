namespace AJut.UX.Controls
{
    /// <summary>Where the caret goes when an EditableTextBlock starts editing</summary>
    public enum eEditTextInitializationBehavior
    {
        /// <summary>All of the text is selected, so typing replaces it</summary>
        SelectAll,

        /// <summary>Nothing is selected, and the caret sits after the last character</summary>
        CursorAtEnd,
    }
}
