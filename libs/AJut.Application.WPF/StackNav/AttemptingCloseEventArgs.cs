namespace AJut.Application.StackNav.Model
{
    using System;

    public class AttemptingCloseEventArgs : EventArgs
    {
        public bool CanClose { get; set; } = true;
    }
}
