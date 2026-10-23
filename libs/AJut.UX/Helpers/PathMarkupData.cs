namespace AJut.UX.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Which areas of a path count as inside, where its outlines cross or nest. F0 in path markup is
    /// <see cref="EvenOdd"/>, and so is markup with no fill rule at all; F1 is <see cref="Nonzero"/>.
    /// </summary>
    public enum ePathFillRule
    {
        /// <summary>A point is inside when a ray from it crosses the outline an odd number of times, so a shape inside a shape is a hole</summary>
        EvenOdd,

        /// <summary>A point is inside when the outline winds around it a nonzero number of times, so a shape inside a shape drawn the same direction stays filled</summary>
        Nonzero,
    }

    /// <summary>
    /// The four commands every <see cref="PathMarkupData"/> is made of. Markup's other commands (H, V, S, Q, T, A)
    /// are lowered to these, so a toolkit only has to draw these four.
    /// </summary>
    public enum ePathCommand
    {
        /// <summary>Starts a new subpath at <see cref="PathCommand.End"/></summary>
        MoveTo,

        /// <summary>A straight line to <see cref="PathCommand.End"/></summary>
        LineTo,

        /// <summary>A cubic bezier through <see cref="PathCommand.Control1"/> and <see cref="PathCommand.Control2"/> to <see cref="PathCommand.End"/></summary>
        CubicTo,

        /// <summary>A straight line back to the subpath's start, closing it</summary>
        Close,
    }

    /// <summary>A point in path coordinates, which are y-down like XAML and SVG</summary>
    public readonly record struct PathPoint (double X, double Y);

    /// <summary>
    /// One drawing command, in absolute coordinates. <see cref="ePathCommand.MoveTo"/> and <see cref="ePathCommand.LineTo"/>
    /// use only <see cref="End"/>, <see cref="ePathCommand.CubicTo"/> uses all three points, and
    /// <see cref="ePathCommand.Close"/> uses none.
    /// </summary>
    public readonly record struct PathCommand (ePathCommand Kind, PathPoint Control1, PathPoint Control2, PathPoint End)
    {
        public static PathCommand MoveTo (PathPoint end) => new PathCommand(ePathCommand.MoveTo, default, default, end);
        public static PathCommand LineTo (PathPoint end) => new PathCommand(ePathCommand.LineTo, default, default, end);
        public static PathCommand CubicTo (PathPoint control1, PathPoint control2, PathPoint end) => new PathCommand(ePathCommand.CubicTo, control1, control2, end);
        public static PathCommand Close () => new PathCommand(ePathCommand.Close, default, default, default);
    }

    /// <summary>An axis-aligned rectangle in path coordinates</summary>
    public readonly record struct PathBounds (double X, double Y, double Width, double Height)
    {
        /// <summary>The bounds of a path with nothing drawn in it</summary>
        public static PathBounds Empty { get; } = new PathBounds(0.0, 0.0, -1.0, -1.0);

        /// <summary>True for <see cref="Empty"/>: nothing was drawn. A single point or a straight line has zero width or height and is not empty.</summary>
        public bool IsEmpty => this.Width < 0.0 || this.Height < 0.0;

        public double Right => this.X + this.Width;
        public double Bottom => this.Y + this.Height;
    }

    // ===========[ PathMarkupData ]=============================================
    // A path as a toolkit draws it: a fill rule, then absolute MoveTo, LineTo, CubicTo and Close
    // commands. PathMarkup builds these from markup; code that generates shapes can build one
    // directly from commands, and the same toolkit code draws both.
    public sealed class PathMarkupData
    {
        // ===========[ Setup/Construction/Teardown ]===========================
        public PathMarkupData (ePathFillRule fillRule, IReadOnlyList<PathCommand>? commands)
        {
            // Copied, so a caller that keeps changing its list cannot leave Commands out of step with Bounds
            this.FillRule = fillRule;
            this.Commands = commands != null && commands.Count > 0 ? commands.ToArray() : Array.Empty<PathCommand>();
            this.Bounds = ComputeBounds(this.Commands);
        }

        // ===========[ Properties ]============================================
        public ePathFillRule FillRule { get; }
        public IReadOnlyList<PathCommand> Commands { get; }

        /// <summary>
        /// The tight bounds of what the path draws: curve extrema, not control points, which is what a
        /// XAML Path scales by when it stretches. A MoveTo that nothing is drawn from is not counted.
        /// </summary>
        public PathBounds Bounds { get; }

        // ===========[ Helper Methods ]========================================
        private static PathBounds ComputeBounds (IReadOnlyList<PathCommand> commands)
        {
            var accumulator = new BoundsAccumulator();
            PathPoint current = default;
            PathPoint subpathStart = default;
            bool hasPendingMove = false;
            foreach (PathCommand command in commands)
            {
                switch (command.Kind)
                {
                    case ePathCommand.MoveTo:
                        current = command.End;
                        subpathStart = command.End;
                        hasPendingMove = true;
                        break;

                    case ePathCommand.LineTo:
                        CountPendingMove(ref accumulator, ref hasPendingMove, current);
                        accumulator.Include(command.End);
                        current = command.End;
                        break;

                    case ePathCommand.CubicTo:
                        CountPendingMove(ref accumulator, ref hasPendingMove, current);
                        accumulator.IncludeCubic(current, command.Control1, command.Control2, command.End);
                        current = command.End;
                        break;

                    case ePathCommand.Close:
                        CountPendingMove(ref accumulator, ref hasPendingMove, current);
                        current = subpathStart;
                        break;
                }
            }

            return accumulator.ToBounds();

            static void CountPendingMove (ref BoundsAccumulator accumulator, ref bool hasPendingMove, PathPoint current)
            {
                if (hasPendingMove)
                {
                    accumulator.Include(current);
                    hasPendingMove = false;
                }
            }
        }

        // ===========[ Subclasses/structs ]====================================
        private struct BoundsAccumulator
        {
            private bool m_hasAny;
            private double m_minX;
            private double m_minY;
            private double m_maxX;
            private double m_maxY;

            public void Include (PathPoint point)
            {
                if (!m_hasAny)
                {
                    m_hasAny = true;
                    m_minX = m_maxX = point.X;
                    m_minY = m_maxY = point.Y;
                    return;
                }

                m_minX = Math.Min(m_minX, point.X);
                m_minY = Math.Min(m_minY, point.Y);
                m_maxX = Math.Max(m_maxX, point.X);
                m_maxY = Math.Max(m_maxY, point.Y);
            }

            // A cubic's control points can sit well outside the curve, so the curve's own extrema are
            // found instead: on each axis the curve turns where its derivative is zero, and the
            // derivative of a cubic bezier is a quadratic, a*t^2 + b*t + c, with
            //   a = p3 - 3*p2 + 3*p1 - p0,  b = 2*(p2 - 2*p1 + p0),  c = p1 - p0
            // (the common factor of 3 dropped). Its roots inside (0, 1), plus the two end points,
            // are every place the curve can reach its min or max on that axis.
            public void IncludeCubic (PathPoint p0, PathPoint p1, PathPoint p2, PathPoint p3)
            {
                this.Include(p0);
                this.Include(p3);
                this.IncludeAxisExtrema(p0, p1, p2, p3, p0.X, p1.X, p2.X, p3.X);
                this.IncludeAxisExtrema(p0, p1, p2, p3, p0.Y, p1.Y, p2.Y, p3.Y);
            }

            public PathBounds ToBounds ()
            {
                return m_hasAny ? new PathBounds(m_minX, m_minY, m_maxX - m_minX, m_maxY - m_minY) : PathBounds.Empty;
            }

            private void IncludeAxisExtrema (PathPoint p0, PathPoint p1, PathPoint p2, PathPoint p3, double v0, double v1, double v2, double v3)
            {
                const double kEpsilon = 1e-12;

                double a = v3 - (3.0 * v2) + (3.0 * v1) - v0;
                double b = 2.0 * (v2 - (2.0 * v1) + v0);
                double c = v1 - v0;

                if (Math.Abs(a) < kEpsilon)
                {
                    // Derivative is linear (or constant): one root at most
                    if (Math.Abs(b) >= kEpsilon)
                    {
                        this.IncludeCubicAt(p0, p1, p2, p3, -c / b);
                    }

                    return;
                }

                double discriminant = (b * b) - (4.0 * a * c);
                if (discriminant < 0.0)
                {
                    return;
                }

                double root = Math.Sqrt(discriminant);
                this.IncludeCubicAt(p0, p1, p2, p3, (-b + root) / (2.0 * a));
                this.IncludeCubicAt(p0, p1, p2, p3, (-b - root) / (2.0 * a));
            }

            private void IncludeCubicAt (PathPoint p0, PathPoint p1, PathPoint p2, PathPoint p3, double t)
            {
                if (t <= 0.0 || t >= 1.0)
                {
                    return;
                }

                double mt = 1.0 - t;
                double w0 = mt * mt * mt;
                double w1 = 3.0 * mt * mt * t;
                double w2 = 3.0 * mt * t * t;
                double w3 = t * t * t;
                this.Include(new PathPoint(
                    (w0 * p0.X) + (w1 * p1.X) + (w2 * p2.X) + (w3 * p3.X),
                    (w0 * p0.Y) + (w1 * p1.Y) + (w2 * p2.Y) + (w3 * p3.Y)
                ));
            }
        }
    }
}
