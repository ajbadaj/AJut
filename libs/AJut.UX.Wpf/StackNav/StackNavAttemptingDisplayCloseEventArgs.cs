namespace AJut.UX
{
    using System;

    public class StackNavAttemptingDisplayCloseEventArgs : EventArgs
    {
        public bool CanClose { get; set; } = true;
    }
}
