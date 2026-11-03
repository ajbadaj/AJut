namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.Helpers;
    using AppKit;
    using CoreGraphics;
    using ObjCRuntime;

    /// <summary>
    /// Draws an svg document read by AJut.UX's <see cref="SvgParsing"/>, the same read WPF's Svg control and the phones'
    /// Svg views draw from, so a file looks the same on each. Unstretched (the default, as WPF's Svg is) the drawing sits at
    /// its own coordinates in points; any other <see cref="Stretch"/> fits what it draws into the view, strokes and all, the
    /// way a Viewbox around WPF's Svg does. What it draws is cut to the view. It draws the document's own colors, so there
    /// is nothing to theme. Load a file with
    /// <c>SvgParsing.Parse(File.OpenRead(NSBundle.MainBundle.PathForResource("name", "svg")))</c>.
    /// </summary>
    public class Svg : NSView
    {
        private readonly List<DrawnPath> m_paths = new List<DrawnPath>();
        private SvgDocument m_source;
        private ePathStretch m_stretch = ePathStretch.None;

        // ===========[ Setup/Construction/Teardown ]===================================
        public Svg ()
        {
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native view whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls, and draws nothing until it is given a Source. None of
        /// its managed state survives a dispose, so a call that needs that state can still fail: dispose it only once
        /// AppKit is done with it.
        /// </summary>
        protected Svg (NativeHandle handle) : base(handle)
        {
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                this.ClearPaths();
            }

            base.Dispose(disposing);
        }

        // ===========[ Properties ]===================================
        /// <summary>Top-left origin, which is what svg coordinates and every AJut layout assume</summary>
        public override bool IsFlipped => true;

        /// <summary>The document drawn, from <see cref="SvgParsing.Parse(System.IO.Stream, double, double)"/>. Null draws nothing.</summary>
        public SvgDocument Source
        {
            get => m_source;
            set
            {
                m_source = value;
                this.RebuildPaths();
                this.InvalidateIntrinsicContentSize();
                this.NeedsDisplay = true;
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
                this.NeedsDisplay = true;
            }
        }

        /// <summary>
        /// The drawing's own size in points, strokes included: from the origin out to its far edges when unstretched, as
        /// WPF's Svg measures, or just what it paints when it is stretched to fit
        /// </summary>
        public override CGSize IntrinsicContentSize
        {
            get
            {
                if ((m_source == null) || m_source.PaintedBounds.IsEmpty)
                {
                    return new CGSize(NSView.NoIntrinsicMetric, NSView.NoIntrinsicMetric);
                }

                PathBounds bounds = m_source.PaintedBounds;
                return m_stretch == ePathStretch.None
                    ? new CGSize((nfloat)Math.Max(0.0, bounds.Right), (nfloat)Math.Max(0.0, bounds.Bottom))
                    : new CGSize((nfloat)bounds.Width, (nfloat)bounds.Height);
            }
        }

        // ===========[ Public Interface Methods ]===================================
        public override void DrawRect (CGRect dirtyRect)
        {
            if ((m_source == null) || (m_paths.Count == 0))
            {
                return;
            }

            CGContext context = NSGraphicsContext.CurrentContext?.CGContext;
            if (context == null)
            {
                return;
            }

            // UniformToFill overfills on purpose, and an unstretched drawing can reach past the view, so what it draws is
            //  cut to the view, as a UIKit view's drawing is. From macOS 14 a view no longer clips its own drawing to its
            //  bounds by default, so the clip is set here. The whole drawing is then placed once, through the context's
            //  transform, so a stroke scales with the shapes it outlines, and it is fitted by its painted bounds, strokes
            //  joined at the miter limit they are measured with, so no stroke reaches past the view.
            CGRect bounds = this.Bounds;
            context.SaveState();
            context.ClipToRect(bounds);
            context.SetMiterLimit((nfloat)SvgDocument.kStrokeMiterLimit);
            if (m_stretch != ePathStretch.None)
            {
                PathFit fit = PathFit.Compute(
                    m_source.PaintedBounds,
                    (double)bounds.Width,
                    (double)bounds.Height,
                    m_stretch
                );
                context.TranslateCTM((nfloat)fit.OffsetX, (nfloat)fit.OffsetY);
                context.ScaleCTM((nfloat)fit.ScaleX, (nfloat)fit.ScaleY);
            }

            foreach (DrawnPath drawn in m_paths)
            {
                DrawNode(context, drawn);
            }

            context.RestoreState();
        }

        // ===========[ Helper Methods ]===================================
        // Each drawn node's path, built once per document, in the document's own coordinates
        private void RebuildPaths ()
        {
            this.ClearPaths();
            if (m_source == null)
            {
                return;
            }

            foreach (SvgNode node in m_source.DrawnNodes)
            {
                if (node.Path != null)
                {
                    m_paths.Add(new DrawnPath(node, node.Path.ToCGPath()));
                }
            }
        }

        private void ClearPaths ()
        {
            foreach (DrawnPath drawn in m_paths)
            {
                drawn.Path.Dispose();
            }

            m_paths.Clear();
        }

        // An element's opacity applies to it as a whole, fill and stroke together, so a translucent one is drawn as a
        //  transparency layer at that alpha, as WPF composites a Path's Opacity
        private static void DrawNode (CGContext context, DrawnPath drawn)
        {
            SvgNode node = drawn.Node;
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
                _Paint(context, drawn.Path, node.Fill, node.Path.Bounds, isEvenOdd, strokeWidth: null);
            }

            // A stroke of no width draws nothing, as WPF's does; CoreGraphics would draw a hairline
            if ((node.Stroke != null) && (node.StrokeWidth > 0.0))
            {
                _Paint(context, drawn.Path, node.Stroke, node.Path.Bounds, isEvenOdd, strokeWidth: node.StrokeWidth);
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
                    CGColor color = paint.Color.ToNSColor().CGColor;
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

                // The stops are sRGB, as every AJut color is (see ColorXT), so they are blended in sRGB
                var colors = new CGColor[gradient.Stops.Length];
                var locations = new nfloat[gradient.Stops.Length];
                for (int index = 0; index < colors.Length; ++index)
                {
                    byte[] argb = gradient.Stops[index].ARGB;
                    colors[index] = new ColorArgb(argb[0], argb[1], argb[2], argb[3]).ToNSColor().CGColor;
                    locations[index] = gradient.Stops[index].Offset;
                }

                using CGColorSpace colorSpace = CGColorSpace.CreateSrgb();
                using var shading = new CGGradient(colorSpace, colors, locations);
                const CGGradientDrawingOptions kSpread = CGGradientDrawingOptions.DrawsBeforeStartLocation | CGGradientDrawingOptions.DrawsAfterEndLocation;
                if (gradient.Type == eBrushGradientType.Linear)
                {
                    gradient.LinearParams.CalculateStartEnd(
                        out double startX,
                        out double startY,
                        out double endX,
                        out double endY
                    );
                    context.DrawLinearGradient(
                        shading,
                        new CGPoint(bounds.X + (startX * bounds.Width), bounds.Y + (startY * bounds.Height)),
                        new CGPoint(bounds.X + (endX * bounds.Width), bounds.Y + (endY * bounds.Height)),
                        kSpread
                    );
                }
                else
                {
                    // A radial gradient is a circle stretched to its ellipse through the context's transform, so neither
                    //  radius may be zero
                    const double kSmallestRadius = 0.001;
                    RadialGradientParams radial = gradient.RadialParams;
                    double centerX = bounds.X + (radial.CenterX * bounds.Width);
                    double centerY = bounds.Y + (radial.CenterY * bounds.Height);
                    double radiusX = Math.Max(kSmallestRadius, radial.RadiusX * bounds.Width);
                    double radiusY = Math.Max(kSmallestRadius, radial.RadiusY * bounds.Height);
                    context.TranslateCTM((nfloat)centerX, (nfloat)centerY);
                    context.ScaleCTM(1, (nfloat)(radiusY / radiusX));
                    context.DrawRadialGradient(shading, CGPoint.Empty, 0, CGPoint.Empty, (nfloat)radiusX, kSpread);
                }

                context.RestoreState();
            }
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed record DrawnPath (SvgNode Node, CGPath Path);
    }
}
