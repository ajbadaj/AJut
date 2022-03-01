namespace AJut.MathUtilities
{
    public static class Cap
    {
        public static dynamic Within (dynamic min, dynamic max, dynamic value) => System.Math.Min(max, System.Math.Max(min, value));
    }
}
