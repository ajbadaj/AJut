namespace AJut.Application
{
    using System;
    using System.Windows;
    using System.Windows.Interop;

    public static class WindowXT
    {
        public static IntPtr GetHwnd (this DependencyObject src)
        {
            try
            {
                Window w = Window.GetWindow(src);
                if (w == null)
                {
                    return IntPtr.Zero;
                }

                var wih = new WindowInteropHelper(w);
                return wih.Handle;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }
    }
}