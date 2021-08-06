namespace AJut.Application.AttachedProperties
{
    using System;
    using System.Linq;
    using System.Windows;
    using System.Windows.Input;

    public static class DragWatch
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(DragWatch));

        public static DependencyProperty IsEnabledProperty = APUtils.Register(GetIsEnabled, SetIsEnabled, OnIsEnabledChanged);
        public static bool GetIsEnabled (DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled (DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        public static DependencyProperty IssueForPrimaryMouseButtonProperty = APUtils.Register(GetIssueForPrimaryMouseButton, SetIssueForPrimaryMouseButton, true);
        public static bool GetIssueForPrimaryMouseButton (DependencyObject obj) => (bool)obj.GetValue(IssueForPrimaryMouseButtonProperty);
        public static void SetIssueForPrimaryMouseButton (DependencyObject obj, bool value) => obj.SetValue(IssueForPrimaryMouseButtonProperty, value);

        public static DependencyProperty IssueForSecondaryMouseButtonProperty = APUtils.Register(GetIssueForSecondaryMouseButton, SetIssueForSecondaryMouseButton, false);
        public static bool GetIssueForSecondaryMouseButton (DependencyObject obj) => (bool)obj.GetValue(IssueForSecondaryMouseButtonProperty);
        public static void SetIssueForSecondaryMouseButton (DependencyObject obj, bool value) => obj.SetValue(IssueForSecondaryMouseButtonProperty, value);

        public static DependencyProperty IssueForMiddleMouseButtonProperty = APUtils.Register(GetIssueForMiddleMouseButton, SetIssueForMiddleMouseButton);
        public static bool GetIssueForMiddleMouseButton (DependencyObject obj) => (bool)obj.GetValue(IssueForMiddleMouseButtonProperty);
        public static void SetIssueForMiddleMouseButton (DependencyObject obj, bool value) => obj.SetValue(IssueForMiddleMouseButtonProperty, value);

        public static DependencyProperty IssueForTouchProperty = APUtils.Register(GetIssueForTouch, SetIssueForTouch, true);
        public static bool GetIssueForTouch (DependencyObject obj) => (bool)obj.GetValue(IssueForTouchProperty);
        public static void SetIssueForTouch (DependencyObject obj, bool value) => obj.SetValue(IssueForTouchProperty, value);

        public static DependencyProperty IssueForStylusProperty = APUtils.Register(GetIssueForStylus, SetIssueForStylus);
        public static bool GetIssueForStylus (DependencyObject obj) => (bool)obj.GetValue(IssueForStylusProperty);
        public static void SetIssueForStylus (DependencyObject obj, bool value) => obj.SetValue(IssueForStylusProperty, value);

        private static DependencyPropertyKey InspectorPropertyKey = APUtils.RegisterReadOnly(GetInspector, SetInspector);
        private static DependencyProperty InspectorProperty = InspectorPropertyKey.DependencyProperty;
        private static DragInspector GetInspector (DependencyObject obj) => (DragInspector)obj.GetValue(InspectorProperty);
        private static void SetInspector (DependencyObject obj, DragInspector value) => obj.SetValue(InspectorPropertyKey, value);

        private static void OnIsEnabledChanged (DependencyObject target, DependencyPropertyChangedEventArgs<bool> e)
        {
            bool wasSetToEnabled = e.HasNewValue && e.NewValue;
            if (target is UIElement uitarget)
            {
                if (uitarget is FrameworkElement ironTarget && !ironTarget.IsLoaded)
                {
                    ironTarget.Loaded += _OnIronTargetLoaded;
                    return;
                }

                _SetupTarget(uitarget, wasSetToEnabled);
            }

            void _OnIronTargetLoaded (object _sender, RoutedEventArgs _e)
            {
                var ironTarget = (FrameworkElement)_sender;
                ironTarget.Loaded -= _OnIronTargetLoaded;

                _SetupTarget(ironTarget, wasSetToEnabled);
            }

            void _SetupTarget (UIElement _target, bool isEnabled)
            {
                if (isEnabled)
                {
                    var inspector = new DragInspector(_target);
                    if (inspector.IsValid)
                    {
                        SetInspector(_target, inspector);
                    }
                    else
                    {
                        inspector.Dispose();
                        SetInspector(_target, null);
                    }
                }
                else
                {
                    GetInspector(_target)?.Dispose();
                    SetInspector(_target, null);
                }
            }
        }

        private class DragInspector : IDisposable
        {
            private UIElement m_target;
            private UIElement m_targetParent;
            private Point? m_initialDownLocation;

            public DragInspector (UIElement uitarget)
            {
                m_target = uitarget;
                m_targetParent = m_target.GetVisualParent() as UIElement;

                if (this.IsValid)
                {
                    m_target.MouseDown += this.OnTargetMouseDown;
                    m_target.MouseUp += this.OnTargetMouseUp;
                    m_target.MouseMove += this.OnTargetMouseMove;

                    m_target.TouchDown += this.OnTargetTouchDown;
                    m_target.TouchUp += this.OnTargetTouchUp;
                    m_target.TouchMove += this.OnTargetTouchMove;

                    m_target.StylusDown += this.OnTargetStylusDown;
                    m_target.StylusUp += this.OnTargetStylusUp;
                    m_target.StylusMove += this.OnTargetStylusMove;
                }
            }

            public void Dispose ()
            {
                if (m_target != null)
                {
                    m_target.MouseDown -= this.OnTargetMouseDown;
                    m_target.MouseUp -= this.OnTargetMouseUp;
                    m_target.MouseMove -= this.OnTargetMouseMove;

                    m_target.TouchDown -= this.OnTargetTouchDown;
                    m_target.TouchUp -= this.OnTargetTouchUp;
                    m_target.TouchMove -= this.OnTargetTouchMove;

                    m_target.StylusDown -= this.OnTargetStylusDown;
                    m_target.StylusUp -= this.OnTargetStylusUp;
                    m_target.StylusMove -= this.OnTargetStylusMove;
                }

                m_target = null;
                m_targetParent = null;
            }

            public bool IsValid => m_target != null && m_targetParent != null;

            #region =========== Mouse Event Handlers ====================
            private void OnTargetMouseDown (object sender, MouseButtonEventArgs e)
            {
                if ((e.IsTargetPrimary() && !GetIssueForPrimaryMouseButton(m_target))
                    || (e.IsTargetSecondary() && !GetIssueForSecondaryMouseButton(m_target))
                    || (e.ChangedButton == MouseButton.Middle && !GetIssueForMiddleMouseButton(m_target))
                )
                {
                    return;
                }

                if (m_initialDownLocation == null)
                {
                    m_initialDownLocation = e.GetPosition(m_targetParent);
                }
            }

            private void OnTargetMouseUp (object sender, MouseButtonEventArgs e)
            {
                m_initialDownLocation = null;
            }

            private void OnTargetMouseMove (object sender, MouseEventArgs e)
            {
                if (m_initialDownLocation == null)
                {
                    return;
                }

                var initial = (Vector)m_initialDownLocation.Value;
                var current = (Vector)e.GetPosition(m_targetParent);

                var offset = current - initial;
                if (Math.Abs(offset.X) > SystemParameters.MinimumHorizontalDragDistance)
                {
                    if (ExecuteDragCommand(DragDropElement.HorizontalDragInitiatedCommand, e, (Point)current))
                    {
                        return;
                    }
                    else if (ExecuteDragCommand(DragDropElement.DragInitiatedCommand, e, (Point)current))
                    {
                        return;
                    }
                }
                
                if (Math.Abs(offset.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (!ExecuteDragCommand(DragDropElement.VerticalDragInitiatedCommand, e, (Point)current))
                    {
                        ExecuteDragCommand(DragDropElement.DragInitiatedCommand, e, (Point)current);
                    }
                }
            }

            #endregion // =========== Mouse Event Handlers ====================

            #region =========== Touch Event Handlers ====================
            private void OnTargetTouchDown (object sender, TouchEventArgs e)
            {
                if (!GetIssueForTouch(m_target))
                {
                    return;
                }

                if (m_initialDownLocation == null)
                {
                    m_initialDownLocation = e.GetTouchPoint(m_targetParent).Position;
                }
            }

            private void OnTargetTouchUp (object sender, TouchEventArgs e)
            {
                m_initialDownLocation = null;
            }

            private void OnTargetTouchMove (object sender, TouchEventArgs e)
            {
                var initial = (Vector)m_initialDownLocation.Value;
                var current = (Vector)e.GetTouchPoint(m_targetParent).Position;

                var offset = current - initial;
                if (offset.X > SystemParameters.MinimumHorizontalDragDistance)
                {
                    if (!ExecuteDragCommand(DragDropElement.HorizontalDragInitiatedCommand, e, (Point)current))
                    {
                        ExecuteDragCommand(DragDropElement.DragInitiatedCommand, e, (Point)current);
                    }
                }
                
                if (offset.Y > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (!ExecuteDragCommand(DragDropElement.VerticalDragInitiatedCommand, e, (Point)current))
                    {
                        ExecuteDragCommand(DragDropElement.DragInitiatedCommand, e, (Point)current);
                    }
                }
            }
            #endregion // =========== Touch Event Handlers ====================

            #region =========== Stylus Event Handlers ====================
            private void OnTargetStylusDown (object sender, StylusDownEventArgs e)
            {
                if (!GetIssueForStylus(m_target))
                {
                    return;
                }

                if (m_initialDownLocation == null)
                {
                    m_initialDownLocation = e.StylusDevice.GetPosition(m_targetParent);
                }
            }

            private void OnTargetStylusUp (object sender, StylusEventArgs e)
            {
                var initial = (Vector)m_initialDownLocation.Value;
                var current = (Vector)e.StylusDevice.GetPosition(m_targetParent);

                var offset = current - initial;
                if (offset.X > SystemParameters.MinimumHorizontalDragDistance)
                {
                    if (!ExecuteDragCommand(DragDropElement.HorizontalDragInitiatedCommand, e, (Point)current))
                    {
                        ExecuteDragCommand(DragDropElement.DragInitiatedCommand, e, (Point)current);
                    }
                }

                if (offset.Y > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (!ExecuteDragCommand(DragDropElement.VerticalDragInitiatedCommand, e, (Point)current))
                    {
                        ExecuteDragCommand(DragDropElement.DragInitiatedCommand, e, (Point)current);
                    }
                }
            }

            private void OnTargetStylusMove (object sender, StylusEventArgs e)
            {
                if (m_initialDownLocation == null)
                {
                    m_initialDownLocation = e.GetStylusPoints(m_targetParent).Last().ToPoint();
                }
            }
            #endregion // =========== Stylus Event Handlers ====================

            private bool ExecuteDragCommand (RoutedUICommand command, InputEventArgs e, Point localStartPoint)
            {
                UIElement sender = e.OriginalSource as UIElement;
                if (sender == null || sender == m_target)
                {
                    sender = m_target.GetFirstChildAtParentLocalPoint(localStartPoint) ?? sender ?? m_target;
                }

                var activeDragTracking = new ActiveDragTracking(m_target, sender, e.Device, localStartPoint);
                if (activeDragTracking.IsValid)
                {
                    if (command.CanExecute(activeDragTracking, m_target))
                    {
                        // ===================================================================================================
                        // NOTE: It is required we do this first, initial down location being null shortcircuits drag tracking
                        //  and if we don't do that first, the nature of command execution will be to allow more drag commands
                        //  to spawn.
                        m_initialDownLocation = null;
                        // ===================================================================================================

                        command.Execute(activeDragTracking, m_target);
                        return true;
                    }
                }

                return false;
            }

        }
    }
}
