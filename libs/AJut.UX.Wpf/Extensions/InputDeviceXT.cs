namespace AJut.UX
{
    using System;
    using System.Windows;
    using System.Windows.Input;
    using AJut.Storage;

    public static class InputDeviceXT
    {
        /// <summary>
        /// Has the target <see cref="UIElement"/> capture this device in a way that will auto shut
        /// </summary>
        /// <param name="device">The device to capture</param>
        /// <param name="target">The UI element to perform the capture</param>
        /// <returns>The disposable tracker that can be disposed to release the capture, and will dispose itself when all the tricky criteria for ending capture are met</returns>
        public static Result<InputDeviceCaptureTracker> EngageSelfReleasingCaptureFor (this InputDevice device, UIElement target)
        {
            Window window = Window.GetWindow(target);
            if (window == null)
            {
                return Result<InputDeviceCaptureTracker>.Error("Cannot capture device for target, associated window could not be discerned");
            }

            if (device is MouseDevice mouse)
            {
                if (target.CaptureMouse())
                {
                    return Result<InputDeviceCaptureTracker>.Success(new InputDeviceCaptureTracker(window, target, mouse));
                }
            }

            if (device is TouchDevice touch)
            {
                if (target.CaptureTouch(touch))
                {
                    return Result<InputDeviceCaptureTracker>.Success(new InputDeviceCaptureTracker(window, target, touch));
                }
            }

            if (device is StylusDevice stylus)
            {
                if (target.CaptureStylus())
                {
                    return Result<InputDeviceCaptureTracker>.Success(new InputDeviceCaptureTracker(window, target, stylus));
                }
            }

            return Result<InputDeviceCaptureTracker>.Error("Unknown device");
        }
    }


    public class InputDeviceCaptureTracker : IDisposable
    {
        private Action m_onComplete;
        private InputDeviceCaptureTracker (Window window, UIElement target)
        {
            this.Window = window;
            this.Target = target;
            this.Window.Deactivated += this.HandleOnComplete;
        }

        public InputDeviceCaptureTracker (Window window, UIElement target, MouseDevice device) : this(window, target)
        {
            this.Device = device;
            target.MouseUp += this.HandleOnComplete;
        }

        public InputDeviceCaptureTracker (Window window, UIElement target, TouchDevice device) : this(window, target)
        {
            this.Device = device;
            target.TouchUp += this.HandleOnComplete;
        }
        public InputDeviceCaptureTracker (Window window, UIElement target, StylusDevice device) : this(window, target)
        {
            this.Device = device;
            target.StylusUp += this.HandleOnComplete;
        }

        public Window Window { get; private set; }
        public UIElement Target { get; private set; }
        public InputDevice Device { get; private set; }
        public bool IsCaptureActive { get; private set; } = true;

        public void RegisterActionWhenCaptureIsComplete (Action onComplete)
        {
            m_onComplete = onComplete;
        }

        public void Dispose ()
        {
            if (!this.IsCaptureActive)
            {
                return;
            }

            this.IsCaptureActive = false;
            this.Window.Deactivated -= this.HandleOnComplete;

            if (this.Device is MouseDevice)
            {
                this.Target.MouseUp -= this.HandleOnComplete;
                this.Target.ReleaseMouseCapture();
            }

            if (this.Device is TouchDevice touch)
            {
                this.Target.TouchUp -= this.HandleOnComplete;
                this.Target.ReleaseTouchCapture(touch);
            }

            if (this.Device is StylusDevice)
            {
                this.Target.StylusUp -= this.HandleOnComplete;
                this.Target.ReleaseStylusCapture();
            }

            Action onComplete = m_onComplete;
            m_onComplete = null;
            this.Device = null;
            this.Target = null;
            this.Window = null;

            onComplete?.Invoke();
        }

        private void HandleOnComplete (object sender, EventArgs e)
        {
            this.Dispose();
        }
    }
}
