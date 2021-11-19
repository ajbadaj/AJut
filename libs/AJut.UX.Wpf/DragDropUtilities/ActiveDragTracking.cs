namespace AJut.UX
{
    using System;
    using System.Windows;
    using System.Windows.Input;
    using AJut.Storage;

    public class ActiveDragTracking : IDisposable
    {
        private InputDeviceCaptureTracker m_captureTracker;
        public ActiveDragTracking (UIElement dragOwner, UIElement childFoundIn, InputDevice device, Point startPoint)
        {
            this.DragOwner = dragOwner;
            this.ChildFoundIn = childFoundIn;
            this.Device = device;
            this.StartPoint = startPoint;

            if (!(this.Device is MouseDevice || this.Device is TouchDevice || this.Device is StylusDevice))
            {
                throw new InvalidSetupException("DragStartInfo", $"Current drag support is limited to Mouse, Touch, and Stylus - but passed in device was not any of those, instead was '{this.Device?.GetType().Name ?? "-null-"}'");
            }
        }

        public event EventHandler<EventArgs<Point>> SignalDragMoved;
        public event EventHandler<EventArgs> SignalDragEnd;

        public bool IsValid => this.DragOwner != null && this.Device != null;
        public UIElement DragOwner { get; private set; }
        public UIElement ChildFoundIn { get; private set; }
        public object SenderContext => (this.ChildFoundIn as FrameworkElement)?.DataContext;
        public InputDevice Device { get; private set; }
        public Point StartPoint { get; }
        public bool IsEngaged => m_captureTracker != null;

        /// <summary>
        /// Capture the input device in question and sign up for device update events
        /// </summary>
        public bool Engage ()
        {
            if (this.IsEngaged)
            {
                return true;
            }

            Result<InputDeviceCaptureTracker> result = this.Device.EngageSelfReleasingCaptureFor(this.DragOwner);
            if (!result)
            {
                Logger.LogError(result.GetErrorReport());
                return false;
            }

            m_captureTracker = result.Value;
            m_captureTracker.RegisterActionWhenCaptureIsComplete(this.TriggerEnd);

            if (this.Device is MouseDevice mouse)
            {
                this.DragOwner.MouseMove -= this.OnDeviceUpdated;
                this.DragOwner.MouseMove += this.OnDeviceUpdated;
                return true;
            }

            if (this.Device is TouchDevice touch)
            {
                touch.Updated -= this.OnDeviceUpdated;
                touch.Updated += this.OnDeviceUpdated;
                return true;
            }

            if (this.Device is StylusDevice stylus)
            {
                this.DragOwner.StylusMove -= this.OnDeviceUpdated;
                this.DragOwner.StylusMove += this.OnDeviceUpdated;
                return true;
            }

            return false;
        }

        private void OnDeviceUpdated (object sender, EventArgs e)
        {
            if (this.Device is TouchDevice touch && touch.GetTouchPoint(this.DragOwner) is TouchPoint tp && tp.Action == TouchAction.Up)
            {
                this.TriggerEnd();
            }
            else
            {
                this.TriggerMoved();
            }
        }
        
        private void OnDragOwnerWindowDeactivated (object sender, EventArgs e)
        {
            this.TriggerEnd();
        }

        public void TriggerMoved ()
        {
            this.SignalDragMoved?.Invoke(this, new EventArgs<Point>(this.GetCurrentPointOnDragOwner()));
        }

        public void TriggerEnd ()
        {
            m_captureTracker?.Dispose();
            m_captureTracker = null;
            this.TriggerMoved();
            this.SignalDragEnd?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose ()
        {
            m_captureTracker?.Dispose();
            this.DragOwner.MouseMove -= this.OnDeviceUpdated;
            this.DragOwner.StylusMove -= this.OnDeviceUpdated;

            if (this.Device is TouchDevice touch)
            {
                touch.Updated -= this.OnDeviceUpdated;
            }

            this.Device = null;
            this.DragOwner = null;
            this.ChildFoundIn = null;

        }

        public Point GetCurrentPointOnDragOwner ()
        {
            return this.GetPointFor(this.DragOwner);
        }

        public Point GetPointFor (IInputElement element)
        {
            if (this.Device is MouseDevice mouse)
            {
                return mouse.GetPosition(element);
            }

            if (this.Device is TouchDevice touch)
            {
                return touch.GetTouchPoint(element).Position;
            }

            if (this.Device is StylusDevice stylus)
            {
                return stylus.GetPosition(element);
            }

            throw new ThisWillNeverHappenButICantReturnWithoutDoingSomethingException();
        }
    }
}
