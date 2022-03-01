namespace AJut.UX
{
    using System.Windows;

    public static class RectXT
    {
        public static bool HasZeroArea (this Rect rect)
        {
            return rect.Size.HasZeroArea();
        }
    }
}
