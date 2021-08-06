namespace AJut.Application.SinglePageApp
{
    using System;

    public class AttemptingCloseEventArgs : EventArgs
    {
        public bool CanClose { get; set; } = true;
    }
}
