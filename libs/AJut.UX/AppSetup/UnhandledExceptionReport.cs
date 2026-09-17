namespace AJut.UX
{
    using System;

    /// <summary>
    /// Handles an unhandled exception that made it all the way up to the application. Return true if you handled it and
    /// the app should try to carry on, false to let it go the way it was going.
    /// </summary>
    /// <remarks>
    /// This is the shared replacement for the per-stack ExceptionProcessor delegates, which have the same name in the
    /// same namespace with different signatures and so cannot both be satisfied by one handler. Anything targeting both
    /// WPF and WinUI3 wants this one.
    /// </remarks>
    public delegate bool ApplicationExceptionProcessor (UnhandledExceptionReport report);

    /// <summary>
    /// What came through an unhandled exception hook. Not every hook reports an <see cref="System.Exception"/>, and not
    /// every hook knows whether the process is going down, so both of those are asked rather than assumed.
    /// </summary>
    /// <param name="ExceptionObject">Whatever was thrown, which is usually but not always an <see cref="System.Exception"/></param>
    /// <param name="IsTerminating">True or false when the hook knows whether the process is on its way down, null when it cannot say</param>
    public readonly record struct UnhandledExceptionReport (object ExceptionObject, bool? IsTerminating)
    {
        /// <summary>
        /// The exception that came through, or null in the uncommon case where what was thrown was not an exception at all
        /// </summary>
        public Exception? Exception => this.ExceptionObject as Exception;
    }
}
