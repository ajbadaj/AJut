namespace AJut.MathUtilities
{
    using System;

    public static class MathExtensions
    {
        /// <summary>
        /// Evaluates if a <see cref="double"/> is approximately equal to a target, by evaluating if it's within an epsilon distance away of the target. This helps to take into account floating point accuracy errors.
        /// </summary>
        /// <param name="epsilon">How off the value can be at maximum (default = <see cref="double.Epsilon"/>)</param>
        public static bool IsApproximatelyEqualTo (this double Self, double target, double epsilon = double.Epsilon)
        {
            return Math.Abs(Self - target) <= epsilon;
        }

        /// <summary>
        /// Evaluates if a <see cref="float"/> is approximately equal to a target, by evaluating if it's within an epsilon distance away of the target. This helps to take into account floating point accuracy errors.
        /// </summary>
        /// <param name="epsilon">How off the value can be at maximum (default = <see cref="float.Epsilon"/>)</param>
        public static bool IsApproximatelyEqualTo (this float Self, float target, float epsilon = float.Epsilon)
        {
            return Math.Abs(Self - target) <= epsilon;
        }
    }
}
