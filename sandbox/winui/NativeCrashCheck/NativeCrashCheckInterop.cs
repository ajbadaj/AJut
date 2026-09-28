namespace AJutShowRoomWinUI
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// The native calls the crash filter check needs: installing a top-level crash filter, and crashing with no managed code on
    /// the stack to catch it first.
    /// </summary>
    internal static class NativeCrashCheckInterop
    {
        public const int EXCEPTION_CONTINUE_SEARCH = 0;

        // Nothing is mapped at the very bottom of the address space, so starting a thread there faults immediately
        public static readonly IntPtr kUnmappedAddress = new IntPtr(0x10);

        public delegate int UnhandledExceptionFilterDelegate (IntPtr exceptionPointersPtr);

        [DllImport("kernel32.dll")]
        public static extern IntPtr SetUnhandledExceptionFilter (IntPtr topLevelExceptionFilter);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateThread (IntPtr threadAttributes, UIntPtr stackSize, IntPtr startAddress, IntPtr parameter, uint creationFlags, out uint threadId);
    }
}
