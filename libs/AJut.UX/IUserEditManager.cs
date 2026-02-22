namespace AJut.UX
{
    using System;

    // ===========[ IUserEditNotifier ]==========================================
    // Shared interface for controls that support the "user edit" lifecycle:
    //   open → zero or more value changes → close.
    // UserEditComplete fires exactly once per closed edit, regardless of how
    // many intermediate changes occurred, and only when the user initiated the
    // edit (programmatic changes do not fire it).
    //
    // Moved from AJut.UX.Wpf so WinUI3 controls can implement the same contract
    // without a WPF dependency.

    /// <summary>
    /// Contract for controls that track a distinct "user edit" lifecycle,
    /// separate from raw value-change notifications.
    /// </summary>
    public interface IUserEditNotifier
    {
        /// <summary>
        /// Fires once after the user completes an edit session (e.g. closes the
        /// popup, presses Enter, or focus leaves the field).  Not fired for
        /// programmatic value changes.
        /// </summary>
        public event EventHandler<UserEditAppliedEventArgs> UserEditComplete;
    }

    /// <summary>
    /// Event arguments for <see cref="IUserEditNotifier.UserEditComplete"/>.
    /// </summary>
    public class UserEditAppliedEventArgs
    {
        public UserEditAppliedEventArgs (object oldValue, object newValue)
        {
            this.OldValue = oldValue;
            this.NewValue = newValue;
        }

        /// <summary>Value held before the user's edit began.</summary>
        public object OldValue { get; }

        /// <summary>Value held after the user's edit completed.</summary>
        public object NewValue { get; }
    }
}
