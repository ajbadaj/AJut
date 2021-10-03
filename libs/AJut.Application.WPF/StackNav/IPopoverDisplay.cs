namespace AJut.Application.StackNav.Model
{
    using System;
    using AJut;
    using AJut.Storage;

    public interface IPopoverDisplayBase
    {
        void Cancel (string cancelReason = null);
    }

    public interface IPopoverDisplay : IPopoverDisplayBase
    {
        event EventHandler<EventArgs<Result>> ResultSet;
    }

    public interface IPopoverDisplay<T> : IPopoverDisplayBase
    {
        event EventHandler<EventArgs<Result<T>>> ResultSet;
    }
}
