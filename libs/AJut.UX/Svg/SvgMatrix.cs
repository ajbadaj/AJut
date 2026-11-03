namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.Helpers;

    // ===========[ SvgMatrix ]==================================================
    // A 2D affine transform, laid out the way XAML's Matrix is and applied the same way, to a point on
    // the left: (x, y) goes to (x*M11 + y*M21 + OffsetX, x*M12 + y*M22 + OffsetY). That is also SVG's
    // matrix(a b c d e f) in order, a = M11, b = M12, c = M21, d = M22, e = OffsetX, f = OffsetY, and
    // CoreGraphics' CGAffineTransform field for field. Android's Matrix lays the same numbers out
    // transposed (MSCALE_X = M11, MSKEW_X = M21, MTRANS_X = OffsetX on its first row).
    //
    // Then reads in application order: a.Then(b) applies a, then b. SVG's transform lists read the other
    // way, last entry first, which SvgParsing deals with when it reads one.
    public readonly record struct SvgMatrix (double M11, double M12, double M21, double M22, double OffsetX, double OffsetY)
    {
        // ===========[ Properties ]============================================
        /// <summary>No transform at all</summary>
        public static SvgMatrix Identity { get; } = new SvgMatrix(1.0, 0.0, 0.0, 1.0, 0.0, 0.0);

        public bool IsIdentity => this == Identity;

        /// <summary>
        /// How much the transform scales a length, on average: the square root of how much it scales an area. Exact for
        /// an even scale, and the in-between width for an uneven one, which no single width can match.
        /// </summary>
        public double AverageScale => Math.Sqrt(Math.Abs((this.M11 * this.M22) - (this.M12 * this.M21)));

        // ===========[ Public Interface Methods ]==============================
        public static SvgMatrix Translate (double x, double y) => new SvgMatrix(1.0, 0.0, 0.0, 1.0, x, y);
        public static SvgMatrix Scale (double x, double y) => new SvgMatrix(x, 0.0, 0.0, y, 0.0, 0.0);

        /// <summary>A turn by <paramref name="degrees"/> about the origin. Coordinates are y-down, so a positive turn is clockwise on screen.</summary>
        public static SvgMatrix Rotate (double degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new SvgMatrix(cos, sin, -sin, cos, 0.0, 0.0);
        }

        /// <summary>A turn by <paramref name="degrees"/> about (<paramref name="centerX"/>, <paramref name="centerY"/>)</summary>
        public static SvgMatrix Rotate (double degrees, double centerX, double centerY)
        {
            return Translate(-centerX, -centerY).Then(Rotate(degrees)).Then(Translate(centerX, centerY));
        }

        /// <summary>Slants x by <paramref name="degreesX"/> as y grows, and y by <paramref name="degreesY"/> as x grows</summary>
        public static SvgMatrix Skew (double degreesX, double degreesY)
        {
            return new SvgMatrix(1.0, Math.Tan(degreesY * Math.PI / 180.0), Math.Tan(degreesX * Math.PI / 180.0), 1.0, 0.0, 0.0);
        }

        /// <summary>This transform, then <paramref name="next"/></summary>
        public SvgMatrix Then (SvgMatrix next)
        {
            return new SvgMatrix(
                (this.M11 * next.M11) + (this.M12 * next.M21),
                (this.M11 * next.M12) + (this.M12 * next.M22),
                (this.M21 * next.M11) + (this.M22 * next.M21),
                (this.M21 * next.M12) + (this.M22 * next.M22),
                (this.OffsetX * next.M11) + (this.OffsetY * next.M21) + next.OffsetX,
                (this.OffsetX * next.M12) + (this.OffsetY * next.M22) + next.OffsetY
            );
        }

        /// <summary>Where <paramref name="point"/> lands</summary>
        public PathPoint Apply (PathPoint point)
        {
            return new PathPoint(
                (point.X * this.M11) + (point.Y * this.M21) + this.OffsetX,
                (point.X * this.M12) + (point.Y * this.M22) + this.OffsetY
            );
        }

        /// <summary>
        /// The path with every point moved by this transform. An affine transform carries a cubic bezier to the cubic
        /// through its moved points exactly, so moving the points is the whole job.
        /// </summary>
        public PathMarkupData Apply (PathMarkupData path)
        {
            if (this.IsIdentity)
            {
                return path;
            }

            var moved = new List<PathCommand>(path.Commands.Count);
            foreach (PathCommand command in path.Commands)
            {
                moved.Add(new PathCommand(command.Kind, this.Apply(command.Control1), this.Apply(command.Control2), this.Apply(command.End)));
            }

            return new PathMarkupData(path.FillRule, moved);
        }
    }
}
