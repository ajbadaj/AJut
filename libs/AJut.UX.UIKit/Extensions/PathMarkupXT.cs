namespace AJut.UX
{
    using AJut.UX.Helpers;
    using CoreGraphics;
    using UIKit;

    public static class PathMarkupXT
    {
        /// <summary>
        /// Builds a UIBezierPath from parsed path markup, with every point placed through <paramref name="fit"/>. The
        /// markup is already reduced to absolute move, line, cubic and close commands, and UIKit is y-down like XAML,
        /// so each command maps straight onto its UIBezierPath call.
        /// </summary>
        public static UIBezierPath ToBezierPath (this PathMarkupData data, PathFit fit)
        {
            var path = new UIBezierPath
            {
                UsesEvenOddFillRule = data.FillRule == ePathFillRule.EvenOdd,
            };

            foreach (PathCommand command in data.Commands)
            {
                switch (command.Kind)
                {
                    case ePathCommand.MoveTo:
                        path.MoveTo(_ToPoint(fit.Apply(command.End)));
                        break;

                    case ePathCommand.LineTo:
                        path.AddLineTo(_ToPoint(fit.Apply(command.End)));
                        break;

                    case ePathCommand.CubicTo:
                        path.AddCurveToPoint(
                            _ToPoint(fit.Apply(command.End)),
                            _ToPoint(fit.Apply(command.Control1)),
                            _ToPoint(fit.Apply(command.Control2))
                        );
                        break;

                    case ePathCommand.Close:
                        path.ClosePath();
                        break;
                }
            }

            return path;

            static CGPoint _ToPoint (PathPoint point) => new CGPoint(point.X, point.Y);
        }
    }
}
