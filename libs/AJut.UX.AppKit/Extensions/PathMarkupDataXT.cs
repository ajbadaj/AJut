namespace AJut.UX
{
    using AJut.UX.Helpers;
    using AppKit;
    using CoreGraphics;

    /// <summary>Drawing AJut.UX path data with AppKit</summary>
    public static class PathMarkupDataXT
    {
        /// <summary>
        /// Builds an NSBezierPath from path data, with every point run through <paramref name="fit"/> (identity when
        /// not given). Path data is y-down, so draw the result in a flipped view (IsFlipped true), as every AJut
        /// AppKit control is; in an unflipped view it comes out upside down.
        /// </summary>
        public static NSBezierPath ToBezierPath (this PathMarkupData data, PathFit? fit = null)
        {
            PathFit transform = fit ?? PathFit.Identity;
            var path = new NSBezierPath
            {
                WindingRule = data.FillRule == ePathFillRule.Nonzero ? NSWindingRule.NonZero : NSWindingRule.EvenOdd,
            };

            foreach (PathCommand command in data.Commands)
            {
                switch (command.Kind)
                {
                    case ePathCommand.MoveTo:
                        path.MoveTo(ToCGPoint(transform.Apply(command.End)));
                        break;

                    case ePathCommand.LineTo:
                        path.LineTo(ToCGPoint(transform.Apply(command.End)));
                        break;

                    case ePathCommand.CubicTo:
                        path.CurveTo(
                            ToCGPoint(transform.Apply(command.End)),
                            ToCGPoint(transform.Apply(command.Control1)),
                            ToCGPoint(transform.Apply(command.Control2))
                        );
                        break;

                    case ePathCommand.Close:
                        path.ClosePath();
                        break;
                }
            }

            return path;
        }

        /// <summary>
        /// Builds a CGPath from path data, with every point run through <paramref name="fit"/> (identity when not given),
        /// for drawing straight into a CGContext. A CGPath carries no fill rule, so fill it with the one the data has
        /// (<see cref="PathMarkupData.FillRule"/>: EOFill for even odd). Path data is y-down, so draw it in a flipped
        /// view, the same as <see cref="ToBezierPath"/>.
        /// </summary>
        public static CGPath ToCGPath (this PathMarkupData data, PathFit? fit = null)
        {
            PathFit transform = fit ?? PathFit.Identity;
            var path = new CGPath();
            foreach (PathCommand command in data.Commands)
            {
                switch (command.Kind)
                {
                    case ePathCommand.MoveTo:
                        path.MoveToPoint(ToCGPoint(transform.Apply(command.End)));
                        break;

                    case ePathCommand.LineTo:
                        path.AddLineToPoint(ToCGPoint(transform.Apply(command.End)));
                        break;

                    case ePathCommand.CubicTo:
                        path.AddCurveToPoint(
                            ToCGPoint(transform.Apply(command.Control1)),
                            ToCGPoint(transform.Apply(command.Control2)),
                            ToCGPoint(transform.Apply(command.End))
                        );
                        break;

                    case ePathCommand.Close:
                        path.CloseSubpath();
                        break;
                }
            }

            return path;
        }

        private static CGPoint ToCGPoint (PathPoint point) => new CGPoint((nfloat)point.X, (nfloat)point.Y);
    }
}
