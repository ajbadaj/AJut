namespace AJut.UX
{
    using AJut.UX.Helpers;
    using AndroidPath = Android.Graphics.Path;

    public static class PathMarkupXT
    {
        /// <summary>
        /// Builds an Android Path from parsed path markup, with every point placed through <paramref name="fit"/>. The
        /// markup is already reduced to absolute move, line, cubic and close commands, and Android is y-down like XAML, so
        /// each command maps straight onto its Path call. UIKit's PathMarkupXT.ToBezierPath is the same on that toolkit.
        /// </summary>
        public static AndroidPath ToAndroidPath (this PathMarkupData data, PathFit fit)
        {
            var path = new AndroidPath();
            path.SetFillType(data.FillRule == ePathFillRule.Nonzero ? AndroidPath.FillType.Winding! : AndroidPath.FillType.EvenOdd!);
            foreach (PathCommand command in data.Commands)
            {
                switch (command.Kind)
                {
                    case ePathCommand.MoveTo:
                        PathPoint moveTo = fit.Apply(command.End);
                        path.MoveTo((float)moveTo.X, (float)moveTo.Y);
                        break;

                    case ePathCommand.LineTo:
                        PathPoint lineTo = fit.Apply(command.End);
                        path.LineTo((float)lineTo.X, (float)lineTo.Y);
                        break;

                    case ePathCommand.CubicTo:
                        PathPoint control1 = fit.Apply(command.Control1);
                        PathPoint control2 = fit.Apply(command.Control2);
                        PathPoint end = fit.Apply(command.End);
                        path.CubicTo((float)control1.X, (float)control1.Y, (float)control2.X, (float)control2.Y, (float)end.X, (float)end.Y);
                        break;

                    case ePathCommand.Close:
                        path.Close();
                        break;
                }
            }

            return path;
        }
    }
}
