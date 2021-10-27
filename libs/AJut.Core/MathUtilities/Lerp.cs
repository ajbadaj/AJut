namespace AJut.MathUtilities
{
    /// <summary>
    /// Use the linear interpolation formula to determine one of the following missing components:
    ///     start number, end number, value, and a percent between that value is between the start and end
    /// </summary>
    public static class Lerp
    {
        public static dynamic Value (dynamic start, dynamic end, double percent)  => start + (percent * (end - start));
        public static double Percent(dynamic start, dynamic end, dynamic value)  => (double)(value - start) / (double)(end - start);
        public static dynamic Start (dynamic end, dynamic value, double percent) => end - ((end - value) / percent);
        public static dynamic End (dynamic start, dynamic value, double percent) => ((value - start) / percent) + start;
    }
}
