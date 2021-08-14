namespace AJut.Application
{
    using System;
    using System.Windows;
    using System.Windows.Input;

    public class ActiveDragTracking : IDisposable
    {
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
        public UIElement DragOwner { get; }
        public UIElement ChildFoundIn { get; }
        public object SenderContext => (this.ChildFoundIn as FrameworkElement)?.DataContext;
        public InputDevice Device { get; }
        public Point StartPoint { get; }

        /// <summary>
        /// Capture the input device in question and sign up for device update events
        /// </summary>
        public bool Engage ()
        {
            if (this.Device is MouseDevice mouse)
            {
                this.DragOwner.MouseMove -= this.OnDeviceUpdated;
                this.DragOwner.MouseUp -= this.OnDeviceSignalingDragCompleted;

                if (!mouse.Capture(this.DragOwner))
                {
                    return false;
                }

                this.DragOwner.MouseMove += this.OnDeviceUpdated;
                this.DragOwner.MouseUp += this.OnDeviceSignalingDragCompleted;
                return true;
            }

            if (this.Device is TouchDevice touch)
            {
                touch.Updated -= this.OnDeviceUpdated;
                if (!touch.Capture(this.DragOwner))
                {
                    return false;
                }

                touch.Updated += this.OnDeviceUpdated;
                return true;
            }

            if (this.Device is StylusDevice stylus)
            {
                this.DragOwner.StylusMove -= this.OnDeviceUpdated;
                this.DragOwner.StylusUp -= this.OnDeviceSignalingDragCompleted;

                if (!stylus.Capture(this.DragOwner))
                {
                    return false;
                }

                this.DragOwner.StylusMove += this.OnDeviceUpdated;
                this.DragOwner.StylusUp += this.OnDeviceSignalingDragCompleted;

                return true;
            }

            throw new ThisWillNeverHappenButICantReturnWithoutDoingSomethingException();
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
        
        private void OnDeviceSignalingDragCompleted (object sender, EventArgs e)
        {
            this.TriggerEnd();
        }

        public void TriggerMoved ()
        {
            this.SignalDragMoved?.Invoke(this, new EventArgs<Point>(this.GetCurrentPoint()));
        }

        public void TriggerEnd ()
        {
            this.TriggerMoved();
            this.SignalDragEnd?.Invoke(this, EventArgs.Empty);
        }


        public void Dispose ()
        {
            this.DragOwner.MouseMove -= this.OnDeviceUpdated;
            this.DragOwner.MouseUp -= this.OnDeviceSignalingDragCompleted;
            this.DragOwner.StylusMove -= this.OnDeviceUpdated;
            this.DragOwner.StylusUp -= this.OnDeviceSignalingDragCompleted;

            if (this.Device is MouseDevice)
            {
                this.DragOwner.ReleaseMouseCapture();
            }

            if (this.Device is TouchDevice touch)
            {
                touch.Updated -= this.OnDeviceUpdated;
                this.DragOwner.ReleaseTouchCapture(touch);
            }

            if (this.Device is StylusDevice)
            {
                this.DragOwner.ReleaseStylusCapture();
            }
        }

        public Point GetCurrentPoint ()
        {
            if (this.Device is MouseDevice mouse)
            {
                return mouse.GetPosition(this.DragOwner);
            }

            if (this.Device is TouchDevice touch)
            {
                return touch.GetTouchPoint(this.DragOwner).Position;
            }

            if (this.Device is StylusDevice stylus)
            {
                return stylus.GetPosition(this.DragOwner);
            }

            throw new ThisWillNeverHappenButICantReturnWithoutDoingSomethingException();
        }
    }
}
