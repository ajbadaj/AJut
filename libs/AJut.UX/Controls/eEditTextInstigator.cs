namespace AJut.UX.Controls
{
    /// <summary>What starts editing on a desktop EditableTextBlock (WPF and AppKit), where a pointer can hover</summary>
    public enum eEditTextInstigator
    {
        /// <summary>Only code, by setting IsEditing</summary>
        Manually,

        /// <summary>A double click on the text</summary>
        DoubleClick,

        /// <summary>The pointer coming over the text; it ends again when the pointer leaves, unless the edit is being typed into</summary>
        MouseOver,
    }
}
