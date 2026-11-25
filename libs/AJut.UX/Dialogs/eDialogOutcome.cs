namespace AJut.UX
{
    /// <summary>
    /// How a dialog shown through a dialog service ended. Each dialog ends once: being hidden for nav back is not an
    /// ending, so its caller hears only the answer it is finally given, or why it left without one.
    /// </summary>
    public enum eDialogOutcome
    {
        /// <summary>The person chose the dialog's primary option</summary>
        Primary,

        /// <summary>The person chose the dialog's secondary option</summary>
        Secondary,

        /// <summary>
        /// Closed with no choice made: its close button, Escape, or app code closing it directly
        /// </summary>
        None,

        /// <summary>The service popped back past it, to the dialog beneath it</summary>
        PoppedBack,

        /// <summary>Another dialog, asked for with <see cref="eDialogOverlapResponse.Replace"/>, took its place</summary>
        Replaced,

        /// <summary>
        /// Never shown: it asked for <see cref="eDialogOverlapResponse.SkipIfBusy"/> while another dialog was showing, or
        /// showing it failed (which is logged)
        /// </summary>
        NotShown,

        /// <summary>Its window closed before it ended</summary>
        WindowClosed,
    }
}
