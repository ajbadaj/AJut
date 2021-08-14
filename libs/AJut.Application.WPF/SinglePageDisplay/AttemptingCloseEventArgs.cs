namespace AJut.Application.SinglePageDisplay
{
    using System;

    public class AttemptingCloseEventArgs : EventArgs
    {
        public bool CanClose { get; set; } = true;
    }
}
