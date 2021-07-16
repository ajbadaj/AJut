namespace AJut.Application
{
    using System.Windows;
    using System.Windows.Input;
    public static class MouseXT
    {
        public static MouseButtonState PrimaryButton (this MouseButtonEventArgs e)
        {
            return SystemParameters.SwapButtons ? e.RightButton : e.LeftButton;
        }

        public static bool IsTargetPrimary (this MouseButtonEventArgs e)
        {
            return e.ChangedButton == (SystemParameters.SwapButtons ? MouseButton.Right : MouseButton.Left);
        }

        public static bool IsTargetSecondary (this MouseButtonEventArgs e)
        {
            return e.ChangedButton == (SystemParameters.SwapButtons ? MouseButton.Left : MouseButton.Right);
        }

        public static MouseButtonState GetPrimaryButtonState ()
        {
            return SystemParameters.SwapButtons
                ? Mouse.PrimaryDevice.RightButton
                : Mouse.PrimaryDevice.LeftButton;
        }
    }
}
