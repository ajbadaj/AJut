namespace AJut.UX.Event
{
    using System;

    public class UserEditEventArgs<T> : EventArgs
    {
        public UserEditEventArgs (T previousData, T newData)
        {
            this.PreviousData = previousData;
            this.NewData = newData;
        }

        public T PreviousData { get; }
        public T NewData { get; }
    }
}
