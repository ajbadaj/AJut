namespace AJut.UX.Helpers
{
    using System;
    using System.Collections.Generic;

    // ===========[ PathStrokeBounds ]===========================================
    // The bounds of a path's stroke, joined the way SVG joins one by default and the AJut toolkits draw one: miter joins,
    // beveled past a miter limit, and butt caps on open ends (https://www.w3.org/TR/svg-strokes/#StrokeShape).
    //
    // A stroke is its outline swept by a line across it, so its sides run alongside the outline half the stroke away. A
    // side reaches furthest on an axis where the outline runs straight along the other axis, so the sides are counted at
    // each segment's ends, and inside a curve wherever its tangent is axis aligned (the same turning points
    // PathMarkupData finds a curve's own extremes at), half the stroke out along the normal. A corner then adds its
    // join: a miter reaches out along the corner's outer bisector to half the stroke over sin(half the corner's angle),
    // unless that is more than the miter limit times half the stroke, when the corner is beveled straight across between
    // the two sides' ends, which are counted already. A butt cap ends square across the outline's end, so it adds
    // nothing past the sides' ends either. Round joins and caps are not handled, since nothing here draws them.
    //
    // That is exact for straight lines, and for curves whose bends are wider than half the stroke. A curve bent tighter
    // folds its inner side over itself, which can only paint less than is counted, never more.
    internal static class PathStrokeBounds
    {
        private const double kEpsilon = 1e-12;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// The bounds of <paramref name="path"/> stroked <paramref name="strokeWidth"/> wide with miter joins beveled past
        /// <paramref name="miterLimit"/> (a miter's length over the stroke's width) and butt caps. The stroke alone:
        /// <see cref="PathBounds.Empty"/> for a stroke of no width, or a path with nothing drawn.
        /// </summary>
        public static PathBounds Compute (PathMarkupData path, double strokeWidth, double miterLimit)
        {
            var walk = new StrokeWalk(strokeWidth / 2.0, miterLimit);
            if (walk.HalfWidth <= 0.0)
            {
                return PathBounds.Empty;
            }

            PathPoint current = default;
            PathPoint subpathStart = default;
            foreach (PathCommand command in path.Commands)
            {
                switch (command.Kind)
                {
                    case ePathCommand.MoveTo:
                        walk.StartSubpath();
                        current = command.End;
                        subpathStart = command.End;
                        break;

                    case ePathCommand.LineTo:
                        walk.AddLine(current, command.End);
                        current = command.End;
                        break;

                    case ePathCommand.CubicTo:
                        walk.AddCubic(current, command.Control1, command.Control2, command.End);
                        current = command.End;
                        break;

                    case ePathCommand.Close:
                        // The closing line, then the join where it meets the subpath's first segment, so a closed
                        //  subpath has no caps
                        walk.AddLine(current, subpathStart);
                        walk.CloseSubpath(subpathStart);
                        current = subpathStart;
                        break;
                }
            }

            return walk.ToBounds();
        }

        // ===========[ Subclasses/structs ]====================================
        // What the walk along one path has gathered: the extents so far, and the directions of the current subpath's
        //  first segment and of the last one, for the joins
        private sealed class StrokeWalk
        {
            private readonly double m_miterLimit;
            private bool m_hasAny;
            private double m_left;
            private double m_top;
            private double m_right;
            private double m_bottom;
            private bool m_hasLast;
            private PathPoint m_lastDirection;
            private bool m_hasFirst;
            private PathPoint m_firstDirection;

            public StrokeWalk (double halfWidth, double miterLimit)
            {
                this.HalfWidth = halfWidth;
                m_miterLimit = miterLimit;
            }

            public double HalfWidth { get; }

            public void StartSubpath ()
            {
                m_hasLast = false;
                m_hasFirst = false;
            }

            public void CloseSubpath (PathPoint subpathStart)
            {
                if (m_hasLast && m_hasFirst)
                {
                    this.AddJoin(subpathStart, m_lastDirection, m_firstDirection);
                }

                this.StartSubpath();
            }

            // A segment of no length draws nothing with butt caps, and has no direction to join by, so it is passed over
            public void AddLine (PathPoint start, PathPoint end)
            {
                if (TryGetDirection(start, end, out PathPoint direction))
                {
                    this.AddSegmentEnds(start, direction, end, direction);
                }
            }

            public void AddCubic (PathPoint p0, PathPoint p1, PathPoint p2, PathPoint p3)
            {
                // A control point on its end leaves the tangent there to the next point along
                bool hasStart = TryGetDirection(p0, p1, out PathPoint startDirection)
                    || TryGetDirection(p0, p2, out startDirection)
                    || TryGetDirection(p0, p3, out startDirection);
                bool hasEnd = TryGetDirection(p2, p3, out PathPoint endDirection)
                    || TryGetDirection(p1, p3, out endDirection)
                    || TryGetDirection(p0, p3, out endDirection);
                if (!hasStart || !hasEnd)
                {
                    return;
                }

                this.AddSegmentEnds(p0, startDirection, p3, endDirection);

                // Inside the curve, where its tangent is vertical its sides reach furthest across, and where it is
                //  horizontal, furthest up and down
                foreach (double t in AxisTurns(p0.X, p1.X, p2.X, p3.X))
                {
                    PathPoint point = CubicAt(p0, p1, p2, p3, t);
                    this.Include(point.X - this.HalfWidth, point.Y);
                    this.Include(point.X + this.HalfWidth, point.Y);
                }

                foreach (double t in AxisTurns(p0.Y, p1.Y, p2.Y, p3.Y))
                {
                    PathPoint point = CubicAt(p0, p1, p2, p3, t);
                    this.Include(point.X, point.Y - this.HalfWidth);
                    this.Include(point.X, point.Y + this.HalfWidth);
                }
            }

            public PathBounds ToBounds ()
            {
                return m_hasAny ? new PathBounds(m_left, m_top, m_right - m_left, m_bottom - m_top) : PathBounds.Empty;
            }

            // Both sides at each end, and the join with the segment before it, if any
            private void AddSegmentEnds (PathPoint start, PathPoint startDirection, PathPoint end, PathPoint endDirection)
            {
                if (m_hasLast)
                {
                    this.AddJoin(start, m_lastDirection, startDirection);
                }
                else if (!m_hasFirst)
                {
                    m_hasFirst = true;
                    m_firstDirection = startDirection;
                }

                this.AddSides(start, startDirection);
                this.AddSides(end, endDirection);
                m_hasLast = true;
                m_lastDirection = endDirection;
            }

            // The stroke's two side points at a point on the outline, half the stroke out along its normal
            private void AddSides (PathPoint point, PathPoint direction)
            {
                double normalX = -direction.Y * this.HalfWidth;
                double normalY = direction.X * this.HalfWidth;
                this.Include(point.X + normalX, point.Y + normalY);
                this.Include(point.X - normalX, point.Y - normalY);
            }

            // A corner arriving along incoming and leaving along outgoing (both of length 1). With incoming and outgoing
            //  at an angle of turn whose cosine is their dot product, the corner's own angle is what is left of a half
            //  turn, so sin(half the corner) = sqrt((1 + cos(turn)) / 2), and the miter's length over the stroke's width
            //  is one over that.
            private void AddJoin (PathPoint corner, PathPoint incoming, PathPoint outgoing)
            {
                double cosTurn = (incoming.X * outgoing.X) + (incoming.Y * outgoing.Y);
                double sinHalfCorner = Math.Sqrt(Math.Max(0.0, (1.0 + cosTurn) / 2.0));
                if ((cosTurn >= 1.0 - kEpsilon) || (sinHalfCorner <= kEpsilon))
                {
                    // Straight on draws no corner, and a full reversal's miter would be endless, so it is beveled
                    return;
                }

                double miterRatio = 1.0 / sinHalfCorner;
                if (miterRatio > m_miterLimit)
                {
                    return;
                }

                // The tip sits on the outer bisector, which points away from both directions' turn
                if (TryGetDirection(outgoing, incoming, out PathPoint outward))
                {
                    double reach = this.HalfWidth * miterRatio;
                    this.Include(corner.X + (outward.X * reach), corner.Y + (outward.Y * reach));
                }
            }

            private void Include (double x, double y)
            {
                if (!m_hasAny)
                {
                    m_hasAny = true;
                    m_left = m_right = x;
                    m_top = m_bottom = y;
                    return;
                }

                m_left = Math.Min(m_left, x);
                m_top = Math.Min(m_top, y);
                m_right = Math.Max(m_right, x);
                m_bottom = Math.Max(m_bottom, y);
            }

            // The direction from one point to another, of length 1, when they are apart
            private static bool TryGetDirection (PathPoint from, PathPoint to, out PathPoint direction)
            {
                double dx = to.X - from.X;
                double dy = to.Y - from.Y;
                double length = Math.Sqrt((dx * dx) + (dy * dy));
                if (length <= kEpsilon)
                {
                    direction = default;
                    return false;
                }

                direction = new PathPoint(dx / length, dy / length);
                return true;
            }

            // Where inside (0, 1) a cubic turns on one axis: the roots of its derivative, a quadratic a*t^2 + b*t + c with
            //  a = v3 - 3*v2 + 3*v1 - v0, b = 2*(v2 - 2*v1 + v0), c = v1 - v0 (the common factor of 3 dropped)
            private static IEnumerable<double> AxisTurns (double v0, double v1, double v2, double v3)
            {
                double a = v3 - (3.0 * v2) + (3.0 * v1) - v0;
                double b = 2.0 * (v2 - (2.0 * v1) + v0);
                double c = v1 - v0;
                if (Math.Abs(a) < kEpsilon)
                {
                    if (Math.Abs(b) >= kEpsilon)
                    {
                        double root = -c / b;
                        if ((root > 0.0) && (root < 1.0))
                        {
                            yield return root;
                        }
                    }

                    yield break;
                }

                double discriminant = (b * b) - (4.0 * a * c);
                if (discriminant < 0.0)
                {
                    yield break;
                }

                double rootOfDiscriminant = Math.Sqrt(discriminant);
                double first = (-b + rootOfDiscriminant) / (2.0 * a);
                double second = (-b - rootOfDiscriminant) / (2.0 * a);
                if ((first > 0.0) && (first < 1.0))
                {
                    yield return first;
                }

                if ((second > 0.0) && (second < 1.0))
                {
                    yield return second;
                }
            }

            private static PathPoint CubicAt (PathPoint p0, PathPoint p1, PathPoint p2, PathPoint p3, double t)
            {
                double mt = 1.0 - t;
                double w0 = mt * mt * mt;
                double w1 = 3.0 * mt * mt * t;
                double w2 = 3.0 * mt * t * t;
                double w3 = t * t * t;
                return new PathPoint(
                    (w0 * p0.X) + (w1 * p1.X) + (w2 * p2.X) + (w3 * p3.X),
                    (w0 * p0.Y) + (w1 * p1.Y) + (w2 * p2.Y) + (w3 * p3.Y)
                );
            }
        }
    }
}
