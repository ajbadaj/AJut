namespace AJut.Application.AttachedProperties
{
    using System;
    using System.Windows;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;

    public static class PopupXTA
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(PopupXTA));

        public static DependencyProperty StaysOpenUnlessClickAwayProperty = APUtils.Register(GetStaysOpenUnlessClickAway, SetStaysOpenUnlessClickAway, OnSetupClickAwayInspectorAttachment);
        public static bool GetStaysOpenUnlessClickAway (DependencyObject obj) => (bool)obj.GetValue(StaysOpenUnlessClickAwayProperty);
        public static void SetStaysOpenUnlessClickAway (DependencyObject obj, bool value) => obj.SetValue(StaysOpenUnlessClickAwayProperty, value);
        private static void OnSetupClickAwayInspectorAttachment (DependencyObject target, DependencyPropertyChangedEventArgs<bool> e)
        {
            if (e.NewValue)
            {
                SetAttachedClickAwayInspector(target, new ClickAwayWatcher((Popup)target));
            }
            else
            {
                var inspector = GetAttachedClickAwayInspector(target);
                if (inspector != null)
                {
                    inspector.Dispose();
                    SetAttachedClickAwayInspector(target, null);
                }
            }
        }

        private static DependencyPropertyKey AttachedClickAwayInspectorPropertyKey = APUtils.RegisterReadOnly(GetAttachedClickAwayInspector, SetAttachedClickAwayInspector);
        public static DependencyProperty AttachedClickAwayInspectorProperty = AttachedClickAwayInspectorPropertyKey.DependencyProperty;
        private static ClickAwayWatcher GetAttachedClickAwayInspector (DependencyObject obj) => (ClickAwayWatcher)obj.GetValue(AttachedClickAwayInspectorProperty);
        private static void SetAttachedClickAwayInspector (DependencyObject obj, ClickAwayWatcher value) => obj.SetValue(AttachedClickAwayInspectorPropertyKey, value);


        private class ClickAwayWatcher : IDisposable
        {
            private Popup m_target;
            private UIElement m_mouseUpWatcher;
            private bool m_previousStaysOpenStatus;

            public ClickAwayWatcher (Popup target)
            {
                m_target = target;

                m_previousStaysOpenStatus = m_target.StaysOpen;
                m_target.SetCurrentValue(Popup.StaysOpenProperty, true);

                m_target.Opened += this.OnTargetOpened;
                m_target.Closed += this.OnTargetClosed;

                if (m_target.IsOpen)
                {
                    this.OnTargetOpened(m_target, EventArgs.Empty);
                }
            }

            public void Dispose ()
            {
                if (m_mouseUpWatcher != null)
                {
                    m_mouseUpWatcher.PreviewMouseUp -= this.OnTargetMouseUp;
                }

                m_mouseUpWatcher = null;
                m_target.SetCurrentValue(Popup.StaysOpenProperty, m_previousStaysOpenStatus);

                m_target.Closed -= this.OnTargetClosed;
                m_target.Opened -= this.OnTargetOpened;
                m_target = null;
            }


            private void OnTargetOpened (object sender, EventArgs e)
            {
                m_mouseUpWatcher = Window.GetWindow(m_target);
                m_mouseUpWatcher.PreviewMouseUp -= this.OnTargetMouseUp;
                m_mouseUpWatcher.PreviewMouseUp += this.OnTargetMouseUp;
            }

            private void OnTargetMouseUp (object sender, MouseButtonEventArgs e)
            {
                if (m_mouseUpWatcher != null && e.IsTargetPrimary() && !m_target.Child.IsLocalPointInBounds(e.GetPosition(m_target.Child)))
                {
                    m_target.SetCurrentValue(Popup.IsOpenProperty, false);

                    // Since we're handling this, we need to release the mouse capture so anything like a button or something that might
                    //  have started to capture the mouse interaction already is stopped
                    Mouse.Captured?.ReleaseMouseCapture();
                    e.Handled = true;
                }
            }

            private void OnTargetClosed (object sender, EventArgs e)
            {
                m_mouseUpWatcher.ReleaseMouseCapture();
                m_mouseUpWatcher.PreviewMouseUp -= this.OnTargetMouseUp;
                m_mouseUpWatcher = null;
            }

        }
    }
}
