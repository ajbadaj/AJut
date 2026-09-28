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

        // Nothing is mapped at the very bottom of the address space, so reading from here faults
        private static readonly IntPtr kUnmappedAddress = new IntPtr(0x10);

        public delegate int UnhandledExceptionFilterDelegate (IntPtr exceptionPointersPtr);

        [DllImport("kernel32.dll")]
        public static extern IntPtr SetUnhandledExceptionFilter (IntPtr topLevelExceptionFilter);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateThread (IntPtr threadAttributes, UIntPtr stackSize, IntPtr startAddress, IntPtr parameter, uint creationFlags, out uint threadId);

        /// <summary>
        /// Starts a native thread running strlen on an unmapped address, which access violates with no managed code on its
        /// stack. Starting a thread AT an unmapped address instead gets the process killed by Control Flow Guard, before any
        /// crash filter runs.
        /// </summary>
        public static void StartThreadThatAccessViolates ()
        {
            IntPtr strlen = NativeLibrary.GetExport(NativeLibrary.Load("msvcrt.dll"), "strlen");
            CreateThread(IntPtr.Zero, UIntPtr.Zero, strlen, kUnmappedAddress, 0, out _);
        }

        /// <summary>
        /// Calls a crash filter through its raw function pointer, which works whether the filter is native or was made from
        /// a managed delegate. Wrapping the pointer in a delegate instead throws for a managed one of any other type.
        /// </summary>
        public static unsafe int CallFilter (IntPtr filter, IntPtr exceptionPointersPtr)
            => ((delegate* unmanaged[Stdcall]<IntPtr, int>)filter)(exceptionPointersPtr);
    }
}
