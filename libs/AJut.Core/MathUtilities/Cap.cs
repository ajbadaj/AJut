namespace AJut.MathUtilities
{
    public static class Cap
    {
        public static float Within (float min, float max, float value) => System.Math.Min(max, System.Math.Max(min, value));
        public static double Within (double min, double max, double value) => System.Math.Min(max, System.Math.Max(min, value));
        public static decimal Within (decimal min, decimal max, decimal value) => System.Math.Min(max, System.Math.Max(min, value));

        public static short Within (short min, short max, short value) => System.Math.Min(max, System.Math.Max(min, value));
        public static int Within (int min, int max, int value) => System.Math.Min(max, System.Math.Max(min, value));
        public static long Within (long min, long max, long value) => System.Math.Min(max, System.Math.Max(min, value));
        public static byte Within (byte min, byte max, byte value) => System.Math.Min(max, System.Math.Max(min, value));
    }
}
