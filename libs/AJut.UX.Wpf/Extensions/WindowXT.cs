namespace AJut.UX
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Interop;
    using AJut.Storage;

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

        public static async Task<bool> AsyncDragMove (this Window window, eCoreMouseButton dragMouseButton = eCoreMouseButton.Primary, Action onMove = null)
        {
            return await window.AsyncDragMove(CancellationToken.None, dragMouseButton, onMove).ConfigureAwait(false);
        }

        public static async Task<bool> AsyncDragMove (this Window window, CancellationToken cancellor, eCoreMouseButton dragMouseButton = eCoreMouseButton.Primary, Action onMove = null)
        {
            TaskCompletionSource taskCompletion = new TaskCompletionSource();

            if (MouseXT.GetButtonState(dragMouseButton) == MouseButtonState.Released)
            {
                return false;
            }

            Result<InputDeviceCaptureTracker> result = Mouse.PrimaryDevice.EngageSelfReleasingCaptureFor(window);
            if (!result)
            {
                Logger.LogError(result.GetErrorReport());
                return false;
            }
            result.Value.RegisterActionWhenCaptureIsComplete(() => taskCompletion.TrySetResult());

            Vector startOffset = (Vector)Mouse.PrimaryDevice.GetPosition(window);
            window.MouseMove += _OnMouseMove;

            try
            {
                cancellor.Register(() => taskCompletion.TrySetCanceled());
                await taskCompletion.Task;
            }
            finally
            {
                window.MouseMove -= _OnMouseMove;
                result.Value.Dispose();
            }

            return true;

            void _OnMouseMove (object _sender, MouseEventArgs _e)
            {
                // Unfortunately, if a new window pops up while dragging and takes focus, or the user iniates
                //  window's interactive screenshotting (windowskey + shift + s)
                if (Mouse.PrimaryDevice.Captured == null)
                {
                    taskCompletion.TrySetResult();
                    return;
                }

                var offset = (Vector)Mouse.PrimaryDevice.GetPosition(window) - startOffset;
                window.Left += offset.X;
                window.Top += offset.Y;
                onMove();
            }
        }

        public static async Task<bool> AsyncDragMoveWindow (this DependencyObject src, eCoreMouseButton dragMouseButton = eCoreMouseButton.Primary, Action onMove = null)
        {
            return await src.AsyncDragMoveWindow(CancellationToken.None, dragMouseButton, onMove).ConfigureAwait(false);
        }

        public static async Task<bool> AsyncDragMoveWindow (this DependencyObject src, CancellationToken cancellor, eCoreMouseButton dragMouseButton = eCoreMouseButton.Primary, Action onMove = null)
        {
            var window = Window.GetWindow(src);
            if (window != null)
            {
                return await window.AsyncDragMove(cancellor, dragMouseButton, onMove).ConfigureAwait(false);
            }

            return false;
        }
    }
}