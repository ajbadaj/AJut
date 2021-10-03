namespace AJut.Application.StackNav
{
    using System;

    public class AttemptingCloseEventArgs : EventArgs
    {
        public bool CanClose { get; set; } = true;
    }
}
