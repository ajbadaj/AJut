namespace AJut.UX.Controls
{
    /// <summary>
    /// Which edges of one button in a ToggleStrip draw a separator, and which of its corners take the strip's corner
    /// radius, so the buttons read as one control: a separator between each pair of neighbors, and the strip's rounding
    /// only on its two outer ends. The same on every stack, and for either orientation: a horizontal strip's buttons
    /// meet left to right, a vertical strip's top to bottom.
    /// </summary>
    /// <param name="SeparatorLeft">Draw a separator on the left edge</param>
    /// <param name="SeparatorTop">Draw a separator on the top edge</param>
    /// <param name="SeparatorRight">Draw a separator on the right edge</param>
    /// <param name="SeparatorBottom">Draw a separator on the bottom edge</param>
    /// <param name="RoundTopLeft">Round the top left corner with the strip's radius</param>
    /// <param name="RoundTopRight">Round the top right corner with the strip's radius</param>
    /// <param name="RoundBottomRight">Round the bottom right corner with the strip's radius</param>
    /// <param name="RoundBottomLeft">Round the bottom left corner with the strip's radius</param>
    public readonly record struct ToggleStripItemEdges (
        bool SeparatorLeft, bool SeparatorTop, bool SeparatorRight, bool SeparatorBottom,
        bool RoundTopLeft, bool RoundTopRight, bool RoundBottomRight, bool RoundBottomLeft
    )
    {
        /// <summary>
        /// The edges of the button at <paramref name="index"/> among <paramref name="count"/> buttons showing in the strip.
        /// </summary>
        /// <param name="index">The button's place among the buttons showing, in strip order</param>
        /// <param name="count">How many buttons are showing</param>
        /// <param name="isVertical">True for a strip laid out top to bottom, false for left to right</param>
        /// <param name="trailingSeparator">
        /// True when the strip goes on past its last button (into an overflow button): the last button then draws a
        /// separator on its trailing edge and stays square there, since the strip's own border rounds that end.
        /// </param>
        public static ToggleStripItemEdges For (int index, int count, bool isVertical, bool trailingSeparator)
        {
            bool isFirst = index == 0;
            bool isLast = index == count - 1;

            // Along the strip: a separator leads every button but the first, and trails the last one when the strip
            // continues past it. Its leading end rounds on the first button, its trailing end on the last.
            bool separatorLeading = !isFirst;
            bool separatorTrailing = isLast && trailingSeparator;
            bool roundLeading = isFirst;
            bool roundTrailing = isLast && !trailingSeparator;

            // Leading is left (horizontal) or top (vertical); trailing is right or bottom. A corner sits on one end
            // of each axis, so it rounds when its end of the strip does.
            return isVertical
                ? new ToggleStripItemEdges(
                    SeparatorLeft: false, SeparatorTop: separatorLeading, SeparatorRight: false, SeparatorBottom: separatorTrailing,
                    RoundTopLeft: roundLeading, RoundTopRight: roundLeading, RoundBottomRight: roundTrailing, RoundBottomLeft: roundTrailing
                )
                : new ToggleStripItemEdges(
                    SeparatorLeft: separatorLeading, SeparatorTop: false, SeparatorRight: separatorTrailing, SeparatorBottom: false,
                    RoundTopLeft: roundLeading, RoundTopRight: roundTrailing, RoundBottomRight: roundTrailing, RoundBottomLeft: roundLeading
                );
        }
    }
}
