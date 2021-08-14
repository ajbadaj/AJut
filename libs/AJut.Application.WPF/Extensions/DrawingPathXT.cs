namespace AJut.Application
{
    using AJut.MathUtilities;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using System.Windows.Media;

    public static class DrawingPathXT
    {
        public static double CalculateFlattenedLength (this PathGeometry pathGeom)
        {
            pathGeom = pathGeom.GetFlattenedPathGeometry();
            double runningTotal = 0.0;
            Point startPoint = pathGeom.Figures.First().StartPoint;
            foreach (PathSegment segment in pathGeom.Figures.SelectMany(f => f.Segments))
            {
                Point endPoint;
                if (segment is ArcSegment arc)
                {
                    // TODO - fix math, should be elipses solver
                    endPoint = arc.Point;
                    runningTotal += PathMath.CalculateLinearBezierArcLength(startPoint.X, startPoint.Y, endPoint.X, endPoint.Y);
                }
                else if (segment is BezierSegment bezier)
                {
                    // TODO - fix path, should calculate control points Point1 & Point2
                    runningTotal += PathMath.CalculateLinearBezierArcLength(startPoint.X, startPoint.Y, bezier.Point3.X, bezier.Point3.Y);
                    endPoint = bezier.Point3;
                }
                else if (segment is LineSegment lineSegment)
                {
                    endPoint = lineSegment.Point;
                    runningTotal += (endPoint - startPoint).Length;
                    startPoint = endPoint;
                }
                else if (segment is PolyBezierSegment polyBezier)
                {
                    // TODO - fix
                    foreach (Point point in polyBezier.Points)
                    {
                        runningTotal += PathMath.CalculateLinearBezierArcLength(startPoint.X, startPoint.Y, point.X, point.Y);
                        startPoint = point;
                    }

                    endPoint = polyBezier.Points.Last();
                }
                else if (segment is PolyLineSegment polyLine)
                {
                    foreach (Point point in polyLine.Points)
                    {
                        runningTotal += (point - startPoint).Length;
                        startPoint = point;
                    }

                    endPoint = polyLine.Points.Last();
                }
                else if (segment is PolyQuadraticBezierSegment polyQuadBezier)
                {
                    // TODO - fix
                    foreach (Point point in polyQuadBezier.Points)
                    {
                        runningTotal += PathMath.CalculateLinearBezierArcLength(startPoint.X, startPoint.Y, point.X, point.Y);
                        startPoint = point;
                    }

                    endPoint = polyQuadBezier.Points.Last();
                }
                else if (segment is QuadraticBezierSegment quadBezier)
                {
                    // TODO - fix
                    endPoint = quadBezier.Point2;
                    runningTotal += PathMath.CalculateLinearBezierArcLength(startPoint.X, startPoint.Y, endPoint.X, endPoint.Y);
                }
                else
                {
                    endPoint = startPoint;
                }    

                startPoint = endPoint;
            }

            return runningTotal;
        }

        public static bool IsGeometryNegative (this PathGeometry pathGeom)
        {
            return pathGeom.Bounds.Left < 0.0 || pathGeom.Bounds.Top < 0.0;
        }

        public static void MovePointsPositive (this PathGeometry pathGeom)
        {
            if (!pathGeom.IsGeometryNegative())
            {
                return;
            }

            Vector offset = (Vector)pathGeom.Bounds.TopLeft;
            if (offset.X < 0.0)
            {
                offset.X = -offset.X;
            }

            if (offset.Y < 0.0)
            {
                offset.Y = -offset.Y;
            }

            foreach (PathSegment segment in pathGeom.Figures.SelectMany(f => f.Segments))
            {
                if (segment is LineSegment lineSegment)
                {
                    lineSegment.Point += offset;
                }
                else if (segment is PolyLineSegment)
                {
                    PointCollection pointCollection = (segment as PolyLineSegment).Points;
                    for(int index = 0; index < pointCollection.Count; ++index)
                    {
                        pointCollection[index] += offset;
                    }
                }
            }
        }
    }
}