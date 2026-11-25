namespace AJut.UX
{
    /// <summary>
    /// What a dialog service does with a dialog it is asked to show while another dialog is showing. When nothing is showing,
    /// every response shows the dialog at once.
    /// </summary>
    public enum eDialogOverlapResponse
    {
        /// <summary>
        /// Wait until there is no dialog at all, the whole nav back stack included, then show. Queued dialogs show in
        /// the order they were asked for.
        /// </summary>
        Queue,

        /// <summary>
        /// Hide the showing dialog and show this one on top of it. The hidden dialog is kept as it is (the same instance,
        /// with whatever was typed into it) and comes back when this one closes or is popped. Its caller's wait carries on
        /// through the hide, and ends with the answer it is finally given.
        /// </summary>
        HideForNavBack,

        /// <summary>
        /// Close the showing dialog for good, ending it as <see cref="eDialogOutcome.Replaced"/>, and take its place. Any
        /// dialog hidden beneath it stays, and comes back when this one closes.
        /// </summary>
        Replace,

        /// <summary>
        /// Show nothing and end at once as <see cref="eDialogOutcome.NotShown"/>, for a dialog that does not matter enough
        /// to wait or to interrupt.
        /// </summary>
        SkipIfBusy,
    }
}
