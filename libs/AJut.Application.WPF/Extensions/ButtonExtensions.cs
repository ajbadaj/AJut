namespace AJut.Application
{
#if WINDOWS_UWP
    using Windows.UI.Xaml.Controls.Primitives;
#else
    using System.Windows.Controls.Primitives;
#endif

    public static class ButtonXT
    {
        public static bool IsCheckedSafe(this ToggleButton button)
        {
            if (button == null)
            {
                return false;
            }

            if (button.IsChecked == null)
            {
                return false;
            }

            return (bool)button.IsChecked;
        }
    }
}
