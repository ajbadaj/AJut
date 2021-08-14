namespace AJut.MathUtilities
{
    /// <summary>
    /// Use the linear interpolation formula to determine one of the following missing components:
    ///     start number, end number, value, and a percent between that value is between the start and end
    /// </summary>
    public static class Lerp
    {
        public static double Value(double start, double end, double percent)  => start + (percent * (end - start));
        public static double Percent(double start, double end, double value)  => (value - start) / (end - start);
        public static double Start (double end, double value, double percent) => end - ((end - value) / percent);
        public static double End (double start, double value, double percent) => ((value - start) / percent) + start;

        public static int Value (int start, int end, double percent) => (int)(start + (percent * (end - start)));
        public static double Percent (int start, int end, int value) => (double)(value - start) / (end - start);
        public static int Start (int end, int value, double percent) => (int)(end - ((end - value) / percent));
        public static int End (int start, int value, double percent) => (int)(((value - start) / percent) + start);

        public static byte Value (byte start, byte end, double percent) => (byte)Cap.Within(0, 255, (start + (percent * (end - start))));
        public static double Percent (byte start, byte end, byte value) => (double)(value - start) / (end - start);
        public static byte Start (byte end, byte value, double percent) => (byte)Cap.Within(0, 255, (end - ((end - value) / percent)));
        public static byte End (byte start, byte value, double percent) => (byte)Cap.Within(0, 255, (((value - start) / percent) + start));
    }
}
