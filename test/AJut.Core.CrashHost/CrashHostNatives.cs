namespace AJut.Core.CrashHost
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// The native calls the crash host needs: keeping Windows from popping crash dialogs, and crashing with no managed code
    /// on the stack.
    /// </summary>
    internal static class CrashHostNatives
    {
        private const uint SEM_FAILCRITICALERRORS = 0x0001;
        private const uint SEM_NOGPFAULTERRORBOX = 0x0002;
        private const uint SEM_NOOPENFILEERRORBOX = 0x8000;

        // Nothing is mapped at the very bottom of the address space, so starting a thread there faults immediately
        private static readonly IntPtr kUnmappedAddress = new IntPtr(0x10);

        [DllImport("kernel32.dll")]
        private static extern uint SetErrorMode (uint mode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateThread (IntPtr threadAttributes, UIntPtr stackSize, IntPtr startAddress, IntPtr parameter, uint creationFlags, out uint threadId);

        /// <summary>
        /// Keeps a crash from sitting on an error dialog nobody is there to close.
        /// </summary>
        public static void SuppressCrashDialogs ()
        {
            SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
        }

        /// <summary>
        /// Starts a native thread at an unmapped address, which access violates before a single instruction runs.
        /// </summary>
        public static void StartThreadThatAccessViolates ()
        {
            CreateThread(IntPtr.Zero, UIntPtr.Zero, kUnmappedAddress, IntPtr.Zero, 0, out _);
        }
    }
}
