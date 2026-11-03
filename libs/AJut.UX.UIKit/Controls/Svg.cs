namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Helpers;
    using CoreGraphics;
    using ObjCRuntime;
    using UIKit;

    /// <summary>
    /// Draws an svg document read by AJut.UX's <see cref="SvgParsing"/>, the same read WPF's Svg control draws from, so a
    /// file looks the same on both. Unstretched (the default, as WPF's Svg is) the drawing sits at its own coordinates in
    /// points; any other <see cref="Stretch"/> fits what it draws into the view, strokes and all, the way a Viewbox around
    /// WPF's Svg does. It draws the document's own colors, so there is nothing to theme. Load a file with
    /// <c>SvgParsing.Parse(File.OpenRead(NSBundle.MainBundle.PathForResource("name", "svg")))</c>.
    /// </summary>
    public class Svg : UIView
    {
        private SvgDocument m_source;
        private ePathStretch m_stretch = ePathStretch.None;

        // ===========[ Setup/Construction/Teardown ]===================================
        public Svg () : base(CGRect.Empty)
        {
            this.Setup();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native view.</summary>
        protected Svg (NativeHandle handle) : base(handle)
        {
            this.Setup();
        }

        private void Setup ()
        {
            // Drawn in Draw, clear around the drawing, and drawn again at any new size rather than scaled from the old one
            this.Opaque = false;
            this.BackgroundColor = UIColor.Clear;
            this.ContentMode = UIViewContentMode.Redraw;
        }

        // ===========[ Properties ]===================================
        /// <summary>The document drawn, from <see cref="SvgParsing.Parse(System.IO.Stream, double, double)"/>. Null draws nothing.</summary>
        public SvgDocument Source
        {
            get => m_source;
            set
            {
                m_source = value;
                this.InvalidateIntrinsicContentSize();
                this.SetNeedsDisplay();
            }
        }

        /// <summary>How the drawing is fitted to the view. None, the default, draws it at its own coordinates, in points.</summary>
        public ePathStretch Stretch
        {
            get => m_stretch;
            set
            {
                m_stretch = value;
                this.InvalidateIntrinsicContentSize();
                this.SetNeedsDisplay();
            }
        }

        // The drawing's own size in points, strokes included: from the origin out to its far edges when unstretched, as
        //  WPF's Svg measures, or just what it paints when it is stretched to fit
        public override CGSize IntrinsicContentSize
        {
            get
            {
                if ((m_source == null) || m_source.PaintedBounds.IsEmpty)
                {
                    return new CGSize(UIView.NoIntrinsicMetric, UIView.NoIntrinsicMetric);
                }

                PathBounds bounds = m_source.PaintedBounds;
                return m_stretch == ePathStretch.None
                    ? new CGSize(Math.Max(0.0, bounds.Right), Math.Max(0.0, bounds.Bottom))
                    : new CGSize(bounds.Width, bounds.Height);
            }
        }

        // ===========[ Public Interface Methods ]===================================
        public override CGSize SizeThatFits (CGSize size)
        {
            CGSize natural = this.IntrinsicContentSize;
            return natural.Width == UIView.NoIntrinsicMetric ? CGSize.Empty : natural;
        }

        public override void Draw (CGRect rect)
        {
            if ((m_source == null) || m_source.PaintedBounds.IsEmpty)
            {
                return;
            }

            CGContext context = UIGraphics.GetCurrentContext();
            if (context == null)
            {
                return;
            }

            // The whole drawing is placed once, through the context's transform, so a stroke scales with the shapes it
            //  outlines, and it is fitted by its painted bounds, strokes joined at the miter limit they are measured with
            //  (CoreGraphics' own default is 10), so no stroke reaches past the view
            context.SaveState();
            context.SetMiterLimit((nfloat)SvgDocument.kStrokeMiterLimit);
            if (m_stretch != ePathStretch.None)
            {
                PathFit fit = PathFit.Compute(
                    m_source.PaintedBounds,
                    this.Bounds.Width,
                    this.Bounds.Height,
                    m_stretch
                );
                context.TranslateCTM((nfloat)fit.OffsetX, (nfloat)fit.OffsetY);
                context.ScaleCTM((nfloat)fit.ScaleX, (nfloat)fit.ScaleY);
            }

            foreach (SvgNode node in m_source.DrawnNodes)
            {
                if (node.Path != null)
                {
                    DrawNode(context, node);
                }
            }

            context.RestoreState();
        }

        // ===========[ Helper Methods ]===================================
        // An element's opacity applies to it as a whole, fill and stroke together, so a translucent one is drawn as a
        //  transparency layer at that alpha, as WPF composites a Path's Opacity
        private static void DrawNode (CGContext context, SvgNode node)
        {
            using UIBezierPath bezier = node.Path.ToBezierPath(PathFit.Identity);
            CGPath path = bezier.CGPath;
            bool isEvenOdd = node.Path.FillRule == ePathFillRule.EvenOdd;
            bool isTranslucent = node.Opacity < 1.0;
            if (isTranslucent)
            {
                context.SaveState();
                context.SetAlpha((nfloat)Math.Clamp(node.Opacity, 0.0, 1.0));
                context.BeginTransparencyLayer();
            }

            if (node.Fill != null)
            {
                _Paint(context, path, node.Fill, node.Path.Bounds, isEvenOdd, strokeWidth: null);
            }

            // A stroke of no width draws nothing, as WPF's does
            if ((node.Stroke != null) && (node.StrokeWidth > 0.0))
            {
                _Paint(context, path, node.Stroke, node.Path.Bounds, isEvenOdd, strokeWidth: node.StrokeWidth);
            }

            if (isTranslucent)
            {
                context.EndTransparencyLayer();
                context.RestoreState();
            }

            // A color fills or strokes the path. A gradient is drawn clipped to the path, or to the outline of its stroke,
            //  and spans the path's own bounds, as a XAML gradient brush does by default.
            static void _Paint (CGContext context, CGPath path, SvgPaint paint, PathBounds bounds, bool isEvenOdd, double? strokeWidth)
            {
                context.SaveState();
                context.AddPath(path);
                if (strokeWidth is double width)
                {
                    context.SetLineWidth((nfloat)width);
                }

                if (paint.Kind == eSvgPaintKind.Color)
                {
                    CGColor color = paint.Color.ToUIColor().CGColor;
                    if (strokeWidth.HasValue)
                    {
                        context.SetStrokeColor(color);
                        context.StrokePath();
                    }
                    else
                    {
                        context.SetFillColor(color);
                        context.DrawPath(isEvenOdd ? CGPathDrawingMode.EOFill : CGPathDrawingMode.Fill);
                    }

                    context.RestoreState();
                    return;
                }

                GradientBuilder gradient = paint.Gradient;
                if ((gradient.Stops == null) || (gradient.Stops.Length == 0) || bounds.IsEmpty)
                {
                    context.RestoreState();
                    return;
                }

                if (strokeWidth.HasValue)
                {
                    context.ReplacePathWithStrokedPath();
                    context.Clip();
                }
                else if (isEvenOdd)
                {
                    context.EOClip();
                }
                else
                {
                    context.Clip();
                }

                var colors = new CGColor[gradient.Stops.Length];
                var locations = new nfloat[gradient.Stops.Length];
                for (int index = 0; index < colors.Length; ++index)
                {
                    byte[] argb = gradient.Stops[index].ARGB;
                    colors[index] = new ColorArgb(argb[0], argb[1], argb[2], argb[3]).ToUIColor().CGColor;
                    locations[index] = gradient.Stops[index].Offset;
                }

                using CGColorSpace colorSpace = CGColorSpace.CreateDeviceRGB();
                using var shading = new CGGradient(colorSpace, colors, locations);
                const CGGradientDrawingOptions kSpread = CGGradientDrawingOptions.DrawsBeforeStartLocation | CGGradientDrawingOptions.DrawsAfterEndLocation;
                if (gradient.Type == eBrushGradientType.Linear)
                {
                    gradient.LinearParams.CalculateStartEnd(out double startX, out double startY, out double endX, out double endY);
                    context.DrawLinearGradient(
                        shading,
                        new CGPoint(bounds.X + (startX * bounds.Width), bounds.Y + (startY * bounds.Height)),
                        new CGPoint(bounds.X + (endX * bounds.Width), bounds.Y + (endY * bounds.Height)),
                        kSpread
                    );
                }
                else
                {
                    // A radial gradient is a circle stretched to its ellipse through the context's transform
                    RadialGradientParams radial = gradient.RadialParams;
                    double centerX = bounds.X + (radial.CenterX * bounds.Width);
                    double centerY = bounds.Y + (radial.CenterY * bounds.Height);
                    double radiusX = Math.Max(0.001, radial.RadiusX * bounds.Width);
                    double radiusY = Math.Max(0.001, radial.RadiusY * bounds.Height);
                    context.TranslateCTM((nfloat)centerX, (nfloat)centerY);
                    context.ScaleCTM(1, (nfloat)(radiusY / radiusX));
                    context.DrawRadialGradient(shading, CGPoint.Empty, 0, CGPoint.Empty, (nfloat)radiusX, kSpread);
                }

                context.RestoreState();
            }
        }
    }
}
