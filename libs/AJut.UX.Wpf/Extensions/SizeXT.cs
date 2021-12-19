namespace AJut.UX
{
    using System.Windows;

    public static class SizeXT
    {
        public static bool HasZeroArea(this Size size)
        {
            return size.Width == 0.0 || size.Height == 0.0;
        }
    }
}
