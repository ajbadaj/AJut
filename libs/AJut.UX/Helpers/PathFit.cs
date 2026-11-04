namespace AJut.UX.Helpers
{
    using System;

    /// <summary>How a path is fitted into the space it is drawn in, the same four modes as XAML's Stretch</summary>
    public enum ePathStretch
    {
        /// <summary>Drawn at its own coordinates, unscaled and unmoved</summary>
        None,

        /// <summary>Scaled on each axis separately so its bounds exactly fill the space, distorting it if the aspect ratios differ</summary>
        Fill,

        /// <summary>Scaled evenly to the largest size that fits inside the space, and centered on the axis with room left over</summary>
        Uniform,

        /// <summary>Scaled evenly to the smallest size that covers the space, and centered so the overflow (which the view clips) is split evenly</summary>
        UniformToFill,
    }

    // ===========[ PathFit ]====================================================
    // The scale and offset that fit a path's bounds into a target size for a given stretch mode,
    // as one transform: drawn = (point * Scale) + Offset on each axis. A toolkit applies it to the
    // path once (an affine transform, a Matrix) rather than redoing the math.
    //
    // Rules, and where they come from:
    //  - Fill, Uniform and UniformToFill fit the path's BOUNDS, not its coordinate origin, so a
    //    path drawn around (100, 100) fills the space the same as one drawn around (0, 0). That is
    //    what a XAML Path does.
    //  - A stroke is drawn half outside the outline, so the target is inset by half the stroke
    //    thickness on every side first, the way a XAML Path insets, so the stroke is not clipped.
    //  - Uniform and UniformToFill center the path on the axis that does not fit exactly. A XAML
    //    Path itself sits top-left and leaves centering to its alignment; every AJut use (a glyph
    //    in a button) wants it centered, so centering is done here.
    //  - A bounds with zero width or height (a straight line, a single point) cannot be scaled on
    //    that axis: that axis keeps a scale of 1 under Fill, Uniform and UniformToFill use the other
    //    axis's scale, and the flat axis is centered. Empty bounds get the identity.
    public readonly record struct PathFit (double ScaleX, double ScaleY, double OffsetX, double OffsetY)
    {
        /// <summary>No scaling and no movement</summary>
        public static PathFit Identity { get; } = new PathFit(1.0, 1.0, 0.0, 0.0);

        /// <summary>
        /// Works out the fit of a path with the given bounds into a target of the given size. Pass the
        /// stroke thickness when the path is stroked, so the stroke stays inside the target.
        /// </summary>
        public static PathFit Compute (PathBounds bounds, double targetWidth, double targetHeight, ePathStretch stretch, double strokeThickness = 0.0)
        {
            if (stretch == ePathStretch.None || bounds.IsEmpty)
            {
                return Identity;
            }

            double inset = Math.Max(0.0, strokeThickness) / 2.0;
            double availableWidth = Math.Max(0.0, targetWidth - (2.0 * inset));
            double availableHeight = Math.Max(0.0, targetHeight - (2.0 * inset));

            // 1. The scale each axis would need on its own, or none for a flat axis
            double? fitX = bounds.Width > 0.0 ? availableWidth / bounds.Width : null;
            double? fitY = bounds.Height > 0.0 ? availableHeight / bounds.Height : null;

            // 2. The scale for the mode
            double scaleX;
            double scaleY;
            switch (stretch)
            {
                case ePathStretch.Fill:
                    scaleX = fitX ?? 1.0;
                    scaleY = fitY ?? 1.0;
                    break;

                case ePathStretch.Uniform:
                    scaleX = scaleY = PickEven(fitX, fitY, Math.Min);
                    break;

                default:
                    scaleX = scaleY = PickEven(fitX, fitY, Math.Max);
                    break;
            }

            // 3. Center the scaled bounds in the available space. Under Fill on a scalable axis the
            //    leftover is zero, so this lands the bounds exactly on the inset edge.
            double offsetX = inset + ((availableWidth - (bounds.Width * scaleX)) / 2.0) - (bounds.X * scaleX);
            double offsetY = inset + ((availableHeight - (bounds.Height * scaleY)) / 2.0) - (bounds.Y * scaleY);
            return new PathFit(scaleX, scaleY, offsetX, offsetY);

            static double PickEven (double? a, double? b, Func<double, double, double> pick)
            {
                if (a.HasValue && b.HasValue)
                {
                    return pick(a.Value, b.Value);
                }

                return a ?? b ?? 1.0;
            }
        }

        /// <summary>Where a path point lands once fitted</summary>
        public PathPoint Apply (PathPoint point) => new PathPoint((point.X * this.ScaleX) + this.OffsetX, (point.Y * this.ScaleY) + this.OffsetY);
    }
}
