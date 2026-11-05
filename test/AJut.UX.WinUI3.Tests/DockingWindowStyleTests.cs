namespace AJut.UX.WinUI3.Tests
{
    using System;
    using System.Runtime.InteropServices;
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The window style calls a docking drag makes on its windows (layering the window it drags, and the one it flashes),
    /// in whatever bitness this process is. 32-bit user32 names those calls differently, so run this as x86 as well as
    /// x64: dotnet test with -a x86 -p:PlatformTarget=x86.
    /// </summary>
    [TestClass]
    public class DockingWindowStyleTests
    {
        [TestMethod]
        public void DockingWindowStyle_LayeredStyleSetOnAWindow_ReadsBack ()
        {
            const int kTestWindowSize = 10;
            nint hwnd = TestWindowFuncs.CreateWindowEx(0, "STATIC", String.Empty, TestWindowFuncs.kWsPopup, 0, 0, kTestWindowSize, kTestWindowSize, 0, 0, 0, 0);
            Assert.AreNotEqual((nint)0, hwnd, "The hidden test window was not made");
            try
            {
                nint style = DockingManager.User32WindowFuncs.GetWindowLongPtr(hwnd, DockingManager.User32WindowFuncs.kGwlExStyle);
                DockingManager.User32WindowFuncs.SetWindowLongPtr(hwnd, DockingManager.User32WindowFuncs.kGwlExStyle, style | DockingManager.User32WindowFuncs.kWsExLayered);
                nint layeredStyle = DockingManager.User32WindowFuncs.GetWindowLongPtr(hwnd, DockingManager.User32WindowFuncs.kGwlExStyle);

                Assert.AreEqual((nint)DockingManager.User32WindowFuncs.kWsExLayered, layeredStyle & DockingManager.User32WindowFuncs.kWsExLayered, $"The layered style set on a window does not read back, in a {(Environment.Is64BitProcess ? 64 : 32)}-bit process");
            }
            finally
            {
                TestWindowFuncs.DestroyWindow(hwnd);
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // Makes and destroys the plain, never shown window the test sets styles on
        private static class TestWindowFuncs
        {
            internal const uint kWsPopup = 0x80000000;

            [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)]
            internal static extern nint CreateWindowEx (uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool DestroyWindow (nint hWnd);
        }
    }
}
