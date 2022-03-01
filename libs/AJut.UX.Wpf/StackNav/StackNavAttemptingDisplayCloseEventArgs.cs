namespace AJut.UX
{
    using System;

    /// <summary>
    /// Event args used to indicate if a display can be closed
    /// </summary>
    public class StackNavAttemptingDisplayCloseEventArgs : EventArgs
    {
        public bool CanClose { get; set; } = true;
    }
}
