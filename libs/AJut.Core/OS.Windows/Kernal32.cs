namespace AJut.OS.Windows
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// Kernal32.dll based utilities
    /// </summary>
    public static partial class Kernal32
    {
        private static class DllImport
        {
            [DllImport("Kernel32.dll")]
            public static extern bool QueryPerformanceCounter (out long lpPerformanceCount);

            [DllImport("Kernel32.dll")]
            public static extern bool QueryPerformanceFrequency (out long lpFrequency);
        }
    }
}
