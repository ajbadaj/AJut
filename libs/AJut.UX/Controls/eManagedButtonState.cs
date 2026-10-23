namespace AJut.UX.Controls
{
    /// <summary>The interaction state a managed button (BrushButton, AutoBrushButton and the rest) draws</summary>
    public enum eManagedButtonState
    {
        /// <summary>Enabled, with no pointer over it and not pressed</summary>
        Normal,

        /// <summary>The pointer is over the button</summary>
        PointerOver,

        /// <summary>Held down</summary>
        Pressed,

        /// <summary>The button is not enabled, which wins over every other state</summary>
        Disabled,
    }
}
