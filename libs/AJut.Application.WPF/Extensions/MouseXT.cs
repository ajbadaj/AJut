namespace AJut.Application
{
    using System;
    using System.Windows;
    using System.Windows.Input;

    /// <summary>
    /// The core mouse buttons that have first party support in windows (Primary, Secondary, and Middle)
    /// </summary>
    public enum eCoreMouseButton 
    {
        /// <summary>
        /// The Primary mouse button - typically the left mouse button, but sometimes the right if swapped.
        /// </summary>
        Primary,

        /// <summary>
        /// The Middle mouse button
        /// </summary>
        Middle,

        /// <summary>
        /// The Secondary mouse button - typically the right mouse button, but sometimes the left if swapped.
        /// </summary>
        Secondary,
    }

    /// <summary>
    /// Extensions for input mouse related evaluations.
    /// </summary>
    public static class MouseXT
    {
        public static MouseButton PrimaryButton => SystemParameters.SwapButtons ? MouseButton.Right : MouseButton.Left;
        public static MouseButton SecondaryButton => SystemParameters.SwapButtons ? MouseButton.Left : MouseButton.Right;

        /// <summary>
        /// Gets the event's button state for the primary mouse button. Typically primary is left, but windows allows a swap for left handed people so it may sometimes be the right.
        /// </summary>
        public static MouseButtonState GetPrimaryButtonState (this MouseButtonEventArgs e)
        {
            return SystemParameters.SwapButtons ? e.RightButton : e.LeftButton;
        }

        /// <summary>
        /// Indicates if the event's <see cref="MouseButtonEventArgs.ChangedButton"/> is the primary mouse button. Typically primary is left, but windows allows a swap for left handed people so it may sometimes be the right.
        /// </summary>
        public static bool IsTargetPrimary (this MouseButtonEventArgs e)
        {
            return e.ChangedButton == (SystemParameters.SwapButtons ? MouseButton.Right : MouseButton.Left);
        }

        /// <summary>
        /// Indicates if the event's <see cref="MouseButtonEventArgs.ChangedButton"/> is the secondary mouse button. Typically secondary is the right button, but windows allows a swap for left handed people so it may sometimes be the left)
        /// </summary>
        public static bool IsTargetSecondary (this MouseButtonEventArgs e)
        {
            return e.ChangedButton == (SystemParameters.SwapButtons ? MouseButton.Left : MouseButton.Right);
        }

        /// <summary>
        /// Gets the button state for the primary mouse button of the given mouse (typically primary is left, but windows allows a swap for left handed people so it may sometimes be the right.
        /// </summary>
        public static MouseButtonState GetPrimaryButtonState (this MouseDevice mouse)
        {
            return SystemParameters.SwapButtons
                ? mouse.RightButton
                : mouse.LeftButton;
        }

        /// <summary>
        /// Gets the button state for the secondary mouse button of the given mouse. Typically secondary is right, but windows allows a swap for left handed people so it may sometimes be the left.
        /// </summary>
        public static MouseButtonState GetSecondaryButtonState (this MouseDevice mouse)
        {
            return SystemParameters.SwapButtons
                ? mouse.LeftButton
                : mouse.RightButton;
        }

        /// <summary>
        /// Gets the button state for the primary mouse button of the <see cref="Mouse.PrimaryDevice"/> mouse device. Typically primary is left, but windows allows a swap for left handed people so it may sometimes be the right.
        /// </summary>
        public static MouseButtonState GetPrimaryButtonState ()
        {
            return Mouse.PrimaryDevice.GetPrimaryButtonState();
        }

        /// <summary>
        /// Gets the button state for the secondary mouse button of the <see cref="Mouse.PrimaryDevice"/> mouse device. Typically secondary is right, but windows allows a swap for left handed people so it may sometimes be the left.
        /// </summary>
        public static MouseButtonState GetSecondaryButtonState ()
        {
            return Mouse.PrimaryDevice.GetSecondaryButtonState();
        }

        /// <summary>
        /// Gets the state of one of the core buttons (see <see cref="eCoreMouseButton"/>) for the given mouse device
        /// </summary>
        public static MouseButtonState GetButtonState(this MouseDevice mouse, eCoreMouseButton button)
        {
            switch (button)
            {
                case eCoreMouseButton.Primary:
                    return GetPrimaryButtonState();
                
                case eCoreMouseButton.Secondary:
                    return GetPrimaryButtonState();

                case eCoreMouseButton.Middle:
                    return mouse.MiddleButton;
            }

            throw new BadParametersException("MouseXT", new Parameter(nameof(button), "Was provided invalid enum", button.ToString()));
        }

        /// <summary>
        /// Gets the state of one of the core buttons (see <see cref="eCoreMouseButton"/>) for the <see cref="Mouse.PrimaryDevice"/> mouse device
        /// </summary>
        public static MouseButtonState GetButtonState (eCoreMouseButton button)
        {
            return Mouse.PrimaryDevice.GetButtonState(button);
        }
    }
}
