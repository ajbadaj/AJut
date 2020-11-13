namespace AJut.Math
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using System.Text;
    using SysMath = System.Math;

    public static class PathMath
    {
        public static double CalculateLinearBezierArcLength(double startX, double startY, double endX, double endY)
        {
            return SysMath.Sqrt(Pow2(endX - startX) + Pow2(endY - startY));
        }

        //public static double CalculateElipsesArcLength (Point start, Point end, Size radius)
        //{
        //    //return ((SysMath.Sqrt(0.5 * ((len * len) + (wid * wid)))) * (PI * 2)) / 2;
        //    //return ((SysMath.Sqrt(0.5 * (len^2 + wid^2))) * (PI * 2)) / 2;
        //    return (SysMath.Sqrt(0.5 * (Pow2(radius.Height) + Pow2(radius.Width))) * (SysMath.PI * 2)) / 2;
        //}

        private static double Pow2 (double value) => value * value;
    }
}
