namespace AJut.Application.AttachedProperties
{
    using System;
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

        private static DependencyPropertyKey InspectorPropertyKey = APUtils.RegisterReadOnly(GetInspector, SetInspector);
        private static DependencyProperty InspectorProperty = InspectorPropertyKey.DependencyProperty;
        private static DragInspector GetInspector (DependencyObject obj) => (DragInspector)obj.GetValue(InspectorProperty);
        private static void SetInspector (DependencyObject obj, DragInspector value) => obj.SetValue(InspectorPropertyKey, value);

        private static void OnIsEnabledChanged (DependencyObject target, DependencyPropertyChangedEventArgs<bool> e)
        {
            if (target is UIElement uitarget)
            {
                if (e.NewValue)
                {
                    var inspector = new DragInspector(uitarget);
                    if (inspector.IsValid)
                    {
                        SetInspector(target, inspector);
                    }
                }
                else
                {
                    GetInspector(target)?.Dispose();
                    SetInspector(target, null);
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
                m_target.MouseDown += this.OnTargetMouseDown;
                m_target.MouseUp += this.OnTargetMouseUp;
                m_target.MouseMove += this.OnTargetMouseMove;

                m_target.TouchDown += this.OnTargetTouchDown;
                m_target.TouchUp += this.OnTargetTouchUp;
                m_target.TouchMove += this.OnTargetTouchMove;
            }

            public void Dispose ()
            {
                m_target.MouseDown -= this.OnTargetMouseDown;
                m_target.MouseUp -= this.OnTargetMouseUp;
                m_target.MouseMove -= this.OnTargetMouseMove;

                m_target.TouchDown -= this.OnTargetTouchDown;
                m_target.TouchUp -= this.OnTargetTouchUp;
                m_target.TouchMove -= this.OnTargetTouchMove;
                m_target = null;
                m_targetParent = null;
            }

            public bool IsValid => m_target != null && m_targetParent != null;

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
                    var localInitial = _GetLocalInitial();
                    if (DragDropElement.HorizontalDragInitiatedCommand.CanExecute(localInitial, m_target))
                    {
                        DragDropElement.HorizontalDragInitiatedCommand.Execute(localInitial, m_target);
                    }
                    else if (DragDropElement.DragInitiatedCommand.CanExecute(localInitial, m_target))
                    {
                        DragDropElement.DragInitiatedCommand.Execute(localInitial, m_target);
                    }

                    m_initialDownLocation = null;
                }
                else if (Math.Abs(offset.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var localInitial = _GetLocalInitial();
                    if (DragDropElement.VerticalDragInitiatedCommand.CanExecute(localInitial, m_target))
                    {
                        DragDropElement.VerticalDragInitiatedCommand.Execute(localInitial, m_target);
                    }
                    else if (DragDropElement.DragInitiatedCommand.CanExecute(localInitial, m_target))
                    {
                        DragDropElement.DragInitiatedCommand.Execute(localInitial, m_target);
                    }

                    m_initialDownLocation = null;
                }

                Point _GetLocalInitial ()
                {
                    return m_targetParent.TranslatePoint((Point)initial, m_target);
                }
            }

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
                    DragDropElement.HorizontalDragInitiatedCommand.Execute(null, m_target);
                }
                else if (offset.Y > SystemParameters.MinimumVerticalDragDistance)
                {
                    DragDropElement.VerticalDragInitiatedCommand.Execute(null, m_target);
                    m_initialDownLocation = null;
                }
            }

        }
    }
}
