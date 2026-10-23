namespace AJut.MathUtilities
{
    /// <summary>
    /// Use the linear interpolation formula to determine one of the following missing components:
    ///     start number, end number, value, and a percent between that value is between the start and end
    /// </summary>
    /// <remarks>
    /// Works in double. Integer and float arguments widen to double on the way in, and the result is a double, which is
    /// what these returned before, when they took dynamic: a percent is a double, so the arithmetic already came out
    /// as double.
    /// </remarks>
    public static class Lerp
    {
        public static double Value (double start, double end, double percent) => start + (percent * (end - start));
        public static double Percent (double start, double end, double value) => (value - start) / (end - start);
        public static double Start (double end, double value, double percent) => end - ((end - value) / percent);
        public static double End (double start, double value, double percent) => ((value - start) / percent) + start;
    }
}
