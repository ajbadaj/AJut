namespace AJut.UX.Controls
{
    /// <summary>
    /// Determines how a numeric editor responds when the entered text falls outside the allowed
    /// minimum/maximum bounds. In both cases the bound value is kept clamped to the nearest bound;
    /// this only governs the editor's response to the out of bounds text itself.
    /// </summary>
    public enum eOutOfBoundsResponse
    {
        /// <summary>
        /// Leave the out of bounds text in place and flag it - error border, warning glyph, and a
        /// tooltip explaining the violation - until the user corrects it.
        /// </summary>
        ErrorAndToolTip,

        /// <summary>
        /// Snap the text to the nearest bound when the edit is committed (lost focus or Enter) so the
        /// displayed text always matches the real clamped value. No error is shown.
        /// </summary>
        FixOnCommit,
    }
}
