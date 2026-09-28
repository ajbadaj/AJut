namespace AJutShowRoomWinUI
{
    using System;
    using System.Runtime.InteropServices;
    using AJut;

    /// <summary>
    /// Stands in for a host app's own native crash filter (the kind a crash reporter installs to write a dump), installed
    /// before AJut's setup runs. On a native crash it logs a line of its own, so the log shows whether AJut's filter chained to
    /// it, and which of the two ran first.
    /// </summary>
    internal static class StandInHostCrashFilter
    {
        public const string kLogTag = "[HOST-FILTER]";

        private static NativeCrashCheckInterop.UnhandledExceptionFilterDelegate? g_handler;
        private static IntPtr g_previousFilter;

        /// <summary>
        /// Installs the stand-in filter. Call before <see cref="AJut.UX.ApplicationUtilities.RunOnetimeSetup"/>, the way a host
        /// with its own crash reporter would.
        /// </summary>
        public static void Install ()
        {
            g_handler = OnNativeCrash;
            g_previousFilter = NativeCrashCheckInterop.SetUnhandledExceptionFilter(Marshal.GetFunctionPointerForDelegate(g_handler));
        }

        private static int OnNativeCrash (IntPtr exceptionPointersPtr)
        {
            Logger.LogError($"{kLogTag} The stand-in host crash filter ran");
            return g_previousFilter == IntPtr.Zero
                ? NativeCrashCheckInterop.EXCEPTION_CONTINUE_SEARCH
                : NativeCrashCheckInterop.CallFilter(g_previousFilter, exceptionPointersPtr);
        }
    }
}
