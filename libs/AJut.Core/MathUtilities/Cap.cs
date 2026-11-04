namespace AJut.MathUtilities
{
    using System.Numerics;

    public static class Cap
    {
        /// <summary>
        /// Caps <paramref name="value"/> to no less than <paramref name="min"/> and no more than <paramref name="max"/>.
        /// Unlike Math.Clamp it does not throw when min is above max: max wins.
        /// </summary>
        public static T Within<T> (T min, T max, T value) where T : INumber<T> => T.Min(max, T.Max(min, value));
    }
}
