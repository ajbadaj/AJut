namespace AJut.UX.Controls
{
    /// <summary>What starts editing on a mobile EditableTextBlock</summary>
    public enum eEditTextTouchInstigator
    {
        /// <summary>Only code, by setting IsEditing</summary>
        Manually,

        /// <summary>A tap on the text</summary>
        Tap,

        /// <summary>Two quick taps on the text</summary>
        DoubleTap,

        /// <summary>A press held on the text</summary>
        LongPress,
    }
}
