namespace AJut.UX.Theming
{
    using System;

    /// <summary>
    /// Something that knows whether the user is working by touch rather than with a mouse, and says when that may have
    /// changed. On Windows that is <see cref="WindowsTouchPreferenceSource"/>. Each UI package's TouchPreferenceWatcher
    /// follows one on a window's UI thread.
    /// </summary>
    public interface ITouchPreferenceSource
    {
        /// <summary>
        /// Raised when the preference may have changed, on whatever thread noticed it. It does not say which way; read
        /// <see cref="IsTouchPreferred"/> again to find out.
        /// </summary>
        event EventHandler? PreferenceMayHaveChanged;

        /// <summary>True when the UI should be touch focused right now</summary>
        bool IsTouchPreferred { get; }
    }
}
