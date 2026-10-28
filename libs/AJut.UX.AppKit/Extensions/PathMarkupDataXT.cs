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

        private static CGPoint ToCGPoint (PathPoint point) => new CGPoint((nfloat)point.X, (nfloat)point.Y);
    }
}
