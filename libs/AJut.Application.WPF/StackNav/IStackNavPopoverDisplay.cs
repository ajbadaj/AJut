namespace AJut.Application
{
    using System;
    using AJut;
    using AJut.Storage;

    public interface IStackNavPopoverDisplayBase
    {
        void Cancel (string cancelReason = null);
    }

    public interface IStackNavPopoverDisplay : IStackNavPopoverDisplayBase
    {
        event EventHandler<EventArgs<Result>> ResultSet;
    }

    public interface IStackNavPopoverDisplay<T> : IStackNavPopoverDisplayBase
    {
        event EventHandler<EventArgs<Result<T>>> ResultSet;
    }
}
