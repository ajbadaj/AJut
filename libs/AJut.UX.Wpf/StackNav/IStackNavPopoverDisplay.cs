namespace AJut.UX
{
    using System;
    using AJut;
    using AJut.Storage;

    /// <summary>
    /// The common or base interface for popovers, only used for internal tracking. Please use <see cref="IStackNavPopoverDisplay"/> or <see cref="IStackNavPopoverDisplay{T}"/> depending on if you want typed results or not
    /// </summary>
    public interface IStackNavPopoverDisplayBase
    {
        /// <summary>
        /// Close and cancel the popup
        /// </summary>
        /// <param name="cancelReason">The reason for the cancellation, this can be left null if you have no specific user facing information to pass about why it was cancelled</param>
        void Cancel (string cancelReason = null);
    }

    /// <summary>
    /// A popover display - popovers are controls displayed centered over <see cref="IStackNavDisplayControl"/> displays. The <see cref="ResultSet"/> should be triggered when 
    /// the popover is ready to be closed and an option has been selected, or the popover's reason to be displayed has been cancelled or otherwise closed without selection.
    /// </summary>
    public interface IStackNavPopoverDisplay : IStackNavPopoverDisplayBase
    {
        event EventHandler<EventArgs<Result>> ResultSet;
    }

    /// <summary>
    /// A popover display - popovers are controls displayed centered over <see cref="IStackNavDisplayControl"/> displays. The <see cref="ResultSet"/> should be triggered when 
    /// the popover is ready to be closed and a <see cref="T"/> has been selected, or the popover's reason to be displayed has been cancelled or otherwise closed without selection.
    /// </summary>
    public interface IStackNavPopoverDisplay<T> : IStackNavPopoverDisplayBase
    {
        event EventHandler<EventArgs<Result<T>>> ResultSet;
    }
}
