namespace AJut.UX
{
    using System.Windows.Controls.Primitives;

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
