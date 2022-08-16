namespace AJut.UX
{
    using System;

    /// <summary>
    /// An interface for a unified "user edit" notification has occurred 
    /// </summary>
    public interface IUserEditNotifier
    {
        /// <summary>
        /// An event that signifies a user edit has completed - this is slightly different than bound or otherwise modified target value changes in that this
        /// event signifies: an edit initiation, a single or even several changes, and a completion have all occurred - not just a change. Changes made outside user edit
        /// similarly do not notify via this event.
        /// </summary>
        public event EventHandler<UserEditAppliedEventArgs> UserEditComplete;
    }

    /// <summary>
    /// Event arg storage for user edit notification via <see cref="IUserEditNotifier.UserEditComplete"/>
    /// </summary>
    public class UserEditAppliedEventArgs
    {
        public UserEditAppliedEventArgs (object oldValue, object newValue)
        {
            this.OldValue = oldValue;
            this.NewValue = newValue;
        }

        /// <summary>
        /// The value previously held by the <see cref="IUserEditNotifier"/> before the user's modification
        /// </summary>
        public object OldValue { get; }

        /// <summary>
        /// The value currenly held by the <see cref="IUserEditNotifier"/>, the value after the user edit was complete.
        /// </summary>
        public object NewValue { get; }
    }
}
