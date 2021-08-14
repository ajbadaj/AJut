namespace AJut.Application.AttachedProperties
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;

    public class ClickableXTA
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(ClickableXTA));
        private static readonly AEUtilsRegistrationHelper AEUtils = new AEUtilsRegistrationHelper(typeof(ClickableXTA));

        public static RoutedEvent ClickEvent = AEUtils.Register<MouseButtonEventHandler>(AddClickHandler, RemoveClickHandler);
        public static void AddClickHandler (DependencyObject obj, MouseButtonEventHandler handler)
        {
            if (obj is UIElement ui)
            {
                ui.AddHandler(ClickEvent, handler);
            }
        }
        public static void RemoveClickHandler (DependencyObject obj, MouseButtonEventHandler handler)
        {
            if (obj is UIElement ui)
            {
                ui.RemoveHandler(ClickEvent, handler);
            }
        }

        public delegate void TouchEventHandler (object sender, TouchEventArgs e);
        public static RoutedEvent TapEvent = AEUtils.Register<TouchEventHandler>(AddTapHandler, RemoveTapHandler);
        public static void AddTapHandler (DependencyObject obj, TouchEventHandler handler)
        {
            if (obj is UIElement ui)
            {
                ui.AddHandler(TapEvent, handler);
            }
        }
        public static void RemoveTapHandler (DependencyObject obj, TouchEventHandler handler)
        {
            if (obj is UIElement ui)
            {
                ui.RemoveHandler(TapEvent, handler);
            }
        }

        public static DependencyProperty IsTrackingClickProperty = APUtils.Register(GetIsTrackingClick, SetIsTrackingClick, OnIsTrackingClickChanged);
        public static bool GetIsTrackingClick (DependencyObject obj) => (bool)obj.GetValue(IsTrackingClickProperty);
        public static void SetIsTrackingClick (DependencyObject obj, bool value) => obj.SetValue(IsTrackingClickProperty, value);
        private static void OnIsTrackingClickChanged (DependencyObject sender, DependencyPropertyChangedEventArgs<bool> e)
        {
            if (e.HasNewValue && e.NewValue)
            {
                if (sender is UIElement ui)
                {
                    SetupClickTrackerFor(ui);
                }
                else // Only UIElement can track click
                {
                    SetIsTrackingClick(sender, false);
                }
            }
            else if (!GetIsTrackingTap(sender))
            {
                CleanupTracker(sender);
            }
        }

        public static DependencyProperty AcceptsMousePrimaryButtonProperty = APUtils.Register(GetAcceptsMousePrimaryButton, SetAcceptsMousePrimaryButton, true);
        public static bool GetAcceptsMousePrimaryButton (DependencyObject obj) => (bool)obj.GetValue(AcceptsMousePrimaryButtonProperty);
        public static void SetAcceptsMousePrimaryButton (DependencyObject obj, bool value) => obj.SetValue(AcceptsMousePrimaryButtonProperty, value);

        public static DependencyProperty AcceptsMouseSecondaryButtonProperty = APUtils.Register(GetAcceptsMouseSecondaryButton, SetAcceptsMouseSecondaryButton);
        public static bool GetAcceptsMouseSecondaryButton (DependencyObject obj) => (bool)obj.GetValue(AcceptsMouseSecondaryButtonProperty);
        public static void SetAcceptsMouseSecondaryButton (DependencyObject obj, bool value) => obj.SetValue(AcceptsMouseSecondaryButtonProperty, value);

        public static DependencyProperty AcceptsMouseMiddleButtonProperty = APUtils.Register(GetAcceptsMouseMiddleButton, SetAcceptsMouseMiddleButton);
        public static bool GetAcceptsMouseMiddleButton (DependencyObject obj) => (bool)obj.GetValue(AcceptsMouseMiddleButtonProperty);
        public static void SetAcceptsMouseMiddleButton (DependencyObject obj, bool value) => obj.SetValue(AcceptsMouseMiddleButtonProperty, value);

        public static DependencyProperty AcceptsMouseXB1Property = APUtils.Register(GetAcceptsMouseXB1, SetAcceptsMouseXB1);
        public static bool GetAcceptsMouseXB1 (DependencyObject obj) => (bool)obj.GetValue(AcceptsMouseXB1Property);
        public static void SetAcceptsMouseXB1 (DependencyObject obj, bool value) => obj.SetValue(AcceptsMouseXB1Property, value);

        public static DependencyProperty AcceptsMousXB2Property = APUtils.Register(GetAcceptsMousXB2, SetAcceptsMousXB2);
        public static bool GetAcceptsMousXB2 (DependencyObject obj) => (bool)obj.GetValue(AcceptsMousXB2Property);
        public static void SetAcceptsMousXB2 (DependencyObject obj, bool value) => obj.SetValue(AcceptsMousXB2Property, value);

        private static IEnumerable<MouseButton> GetMouseButtonsAccepted (DependencyObject source)
        {
            if (GetAcceptsMousePrimaryButton(source)) yield return MouseXT.PrimaryButton;
            if (GetAcceptsMouseSecondaryButton(source)) yield return MouseXT.SecondaryButton;
            if (GetAcceptsMouseMiddleButton(source)) yield return MouseButton.Middle;
            if (GetAcceptsMouseXB1(source)) yield return MouseButton.XButton1;
            if (GetAcceptsMousXB2(source)) yield return MouseButton.XButton2;
        }

        public static DependencyProperty IsTrackingTapProperty = APUtils.Register(GetIsTrackingTap, SetIsTrackingTap, OnIsTrackingTapChanged);
        public static bool GetIsTrackingTap (DependencyObject obj) => (bool)obj.GetValue(IsTrackingTapProperty);
        public static void SetIsTrackingTap (DependencyObject obj, bool value) => obj.SetValue(IsTrackingTapProperty, value);
        private static void OnIsTrackingTapChanged (DependencyObject sender, DependencyPropertyChangedEventArgs<bool> e)
        {
            if (e.HasNewValue && e.NewValue)
            {
                if (sender is UIElement ui)
                {
                    SetupTapTrackerFor(ui);
                }
                else // Only UIElement can track click
                {
                    SetIsTrackingTap(sender, false);
                }
            }
            else if (!GetIsTrackingTap(sender))
            {
                CleanupTracker(sender);
            }
        }

        private static DependencyPropertyKey IsPressedPropertyKey = APUtils.RegisterReadOnly(GetIsPressed, SetIsPressed);
        public static DependencyProperty IsPressedProperty = IsPressedPropertyKey.DependencyProperty;
        public static bool GetIsPressed (DependencyObject obj) => (bool)obj.GetValue(IsPressedProperty);
        private static void SetIsPressed (DependencyObject obj, bool value) => obj.SetValue(IsPressedPropertyKey, value);

        private static DependencyPropertyKey TrackerPropertyKey = APUtils.RegisterReadOnly(GetTracker, SetTracker);
        private static DependencyProperty TrackerProperty = TrackerPropertyKey.DependencyProperty;
        private static ClickTracker GetTracker (DependencyObject obj) => (ClickTracker)obj.GetValue(TrackerProperty);
        private static void SetTracker (DependencyObject obj,  ClickTracker value) => obj.SetValue(TrackerPropertyKey, value);

        private static void SetupClickTrackerFor (UIElement target)
        {
            GetOrCreateTracker(target).IsTrackingMouseClick = true;
        }

        private static void SetupTapTrackerFor (UIElement target)
        {
            GetOrCreateTracker(target).IsTrackingTap = true;
        }

        private static ClickTracker GetOrCreateTracker (UIElement target)
        {
            ClickTracker tracker = GetTracker(target);
            if (tracker == null)
            {
                tracker = new ClickTracker(target);
                SetTracker(target, tracker);
            }

            return tracker;
        }

        private static void CleanupTracker (DependencyObject obj)
        {
            ClickTracker tracker = GetTracker(obj);
            if (tracker != null)
            {
                tracker.Dispose();
                SetTracker(obj, null);
            }
        }

        private class ClickTracker : IDisposable
        {
            private UIElement m_target;
            private bool m_isTrackingMouseClick;
            private bool m_isTrackingTap;
            public ClickTracker (UIElement element)
            {
                m_target = element;
            }

            public bool IsTrackingMouseClick
            {
                get => m_isTrackingMouseClick;
                set
                {
                    if (m_isTrackingMouseClick != value)
                    {
                        m_isTrackingMouseClick = value;

                        m_target.MouseDown -= this.Target_OnMouseDown;
                        m_target.MouseUp -= this.Target_OnMouseUp;
                        if (m_isTrackingMouseClick)
                        {
                            m_target.MouseDown += this.Target_OnMouseDown;
                            m_target.MouseUp += this.Target_OnMouseUp;
                        }
                    }
                }
            }

            public bool IsTrackingTap
            {
                get => m_isTrackingTap;
                set
                {
                    if (m_isTrackingTap != value)
                    {
                        m_isTrackingTap = value;

                        m_target.TouchDown -= this.Target_OnTouchDown;
                        m_target.TouchUp -= this.Target_OnTouchUp;
                        if (m_isTrackingMouseClick)
                        {
                            m_target.TouchDown += this.Target_OnTouchDown;
                            m_target.TouchUp += this.Target_OnTouchUp;
                        }
                    }
                }
            }

            public void Dispose ()
            {
                SetIsPressed(m_target, false);
                m_target.ReleaseMouseCapture();
                m_target.MouseDown -= this.Target_OnMouseDown;
                m_target.MouseUp -= this.Target_OnMouseUp;
                m_target.TouchDown -= this.Target_OnTouchDown;
                m_target.TouchUp -= this.Target_OnTouchUp;
                m_target = null;
            }

            private void Target_OnMouseDown (object sender, MouseButtonEventArgs e)
            {
                if (GetMouseButtonsAccepted(m_target).Contains(e.ChangedButton) && m_target.CaptureMouse())
                {
                    SetIsPressed(m_target, true);
                }
            }

            private void Target_OnTouchDown (object sender, TouchEventArgs e)
            {
                if (m_target.CaptureTouch(e.TouchDevice))
                {
                    SetIsPressed(m_target, true);
                }
            }


            private void Target_OnMouseUp (object sender, MouseButtonEventArgs e)
            {
                if (GetIsPressed(m_target))
                {
                    e.Handled = true;
                    m_target.ReleaseMouseCapture();
                    SetIsPressed(m_target, false);

                    if (VisualTreeHelper.HitTest(m_target, e.GetPosition(m_target)) != null)
                    {
                        var mouseEventArgs = new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, e.ChangedButton);
                        mouseEventArgs.RoutedEvent = ClickEvent;
                        m_target.RaiseEvent(mouseEventArgs);
                    }
                }
            }

            private void Target_OnTouchUp (object sender, TouchEventArgs e)
            {
                if (GetIsPressed(m_target) && m_target.ReleaseTouchCapture(e.TouchDevice))
                {
                    SetIsPressed(m_target, false);
                    e.Handled = true;

                    Point? touchPoint = e.GetTouchPoint(m_target)?.Position;
                    if (touchPoint != null && VisualTreeHelper.HitTest(m_target, touchPoint.Value) != null)
                    {
                        var touchEventArgs = new TouchEventArgs(e.TouchDevice, e.Timestamp);
                        touchEventArgs.RoutedEvent = TapEvent;
                        m_target.RaiseEvent(touchEventArgs);
                    }
                }
            }

        }
    }
}
