namespace AJut.UX.Controls
{
    // ===========[ eToggleStripOverflow ]=======================================
    // How a ToggleStrip responds when its items do not all fit in the available width.

    public enum eToggleStripOverflow
    {
        /// <summary>Items are clipped to the available width (the original behavior).</summary>
        Clip,

        /// <summary>The strip becomes horizontally scrollable so every item stays reachable.</summary>
        Scroll,

        /// <summary>
        /// Items that do not fit move into an overflow popup opened by a chevron button at the
        /// trailing edge. Selected items are kept visible in the strip where possible.
        /// </summary>
        OverflowPopup,
    }
}
