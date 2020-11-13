namespace AJut
{
    using System;

    public class EventArgs<T> : EventArgs
    {
        public new static readonly EventArgs<T> Empty = new EventArgs<T>(default);
        public EventArgs (T value)
        {
            this.Value = value;
        }

        public T Value { get; }
    }
}
