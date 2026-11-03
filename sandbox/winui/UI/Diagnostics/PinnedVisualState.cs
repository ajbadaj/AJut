namespace AJutShowRoomWinUI.UI.Diagnostics
{
    using AJut.UX;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// Holds a control in one visual state, so the Theme Tester can show a state that takes the pointer (PointerOver,
    /// Pressed) or focus beside the others. The control stops taking the pointer and keyboard focus, so nothing moves it
    /// out of the state, and the state is put back each time it loads or its theme changes.
    /// </summary>
    /// <remarks>
    /// It works for any control whose template draws the state from VisualStateManager, which is every AJut WinUI3
    /// button (the managed brush buttons follow their CommonStates group). A state of another group, such as a focus or
    /// error state, is held the same way. A control that draws its parts itself (a ToggleStrip's items) can't be held.
    /// </remarks>
    public static class PinnedVisualState
    {
        // ===========[ Dependency Properties ]=====================================
        public static readonly DependencyProperty StateProperty = StaticAPUtils.Register(typeof(PinnedVisualState), GetState, SetState, OnStateChanged);
        public static string GetState (DependencyObject obj) => (string)obj.GetValue(StateProperty);
        public static void SetState (DependencyObject obj, string value) => obj.SetValue(StateProperty, value);

        private static void OnStateChanged (DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not Control control)
            {
                return;
            }

            control.Loaded -= OnControlLoaded;
            control.Unloaded -= OnControlUnloaded;
            control.ActualThemeChanged -= OnControlActualThemeChanged;
            if ((e.NewValue is string state) && (state.Length > 0))
            {
                control.IsHitTestVisible = false;
                control.IsTabStop = false;
                control.Loaded += OnControlLoaded;
                control.Unloaded += OnControlUnloaded;
                Pin(control);
            }
        }

        // ===========[ Events ]===================================================
        private static void OnControlLoaded (object sender, RoutedEventArgs e)
        {
            var control = (Control)sender;
            control.ActualThemeChanged -= OnControlActualThemeChanged;
            control.ActualThemeChanged += OnControlActualThemeChanged;
            Pin(control);
        }

        private static void OnControlUnloaded (object sender, RoutedEventArgs e)
        {
            ((Control)sender).ActualThemeChanged -= OnControlActualThemeChanged;
        }

        private static void OnControlActualThemeChanged (FrameworkElement sender, object args)
        {
            Pin((Control)sender);
        }

        // ===========[ Helper Methods ]============================================
        private static void Pin (Control control)
        {
            string state = GetState(control);
            if (state?.Length > 0)
            {
                VisualStateManager.GoToState(control, state, false);
            }
        }
    }
}
