namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using AJut.UX.Helpers;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics;
    using Android.Runtime;
    using Android.Util;
    using Android.Views;
    using AndroidPath = Android.Graphics.Path;

    /// <summary>
    /// Draws an svg document read by AJut.UX's <see cref="SvgParsing"/>, the same read WPF's Svg control draws from, so a
    /// file looks the same on both. Unstretched (the default, as WPF's Svg is) the drawing sits at its own coordinates
    /// in dp; any other <see cref="Stretch"/> fits what it draws into the view, strokes and all, the way a Viewbox around
    /// WPF's Svg does. It draws the document's own colors, so there is nothing to theme. In a layout, ajutSvgSource names
    /// a raw resource holding the file and ajutSvgStretch sets the stretch.
    /// </summary>
    [Register("ajut.ux.controls.Svg")]
    public class Svg : View
    {
        private readonly Paint m_fillPaint = new Paint(PaintFlags.AntiAlias);
        private readonly Paint m_strokePaint = new Paint(PaintFlags.AntiAlias);
        private readonly List<DrawnPath> m_paths = new List<DrawnPath>();
        private SvgDocument? m_source;
        private ePathStretch m_stretch = ePathStretch.None;

        // ===========[ Setup/Construction/Teardown ]===================================
        public Svg (Context context) : this(context, null)
        {
        }

        public Svg (Context context, IAttributeSet? attrs) : this(context, attrs, 0)
        {
        }

        public Svg (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            m_fillPaint.SetStyle(Paint.Style.Fill);
            m_strokePaint.SetStyle(Paint.Style.Stroke);

            // Joined at the miter limit the painted bounds measure by, which is Paint's own default too, said here so the
            //  two cannot drift
            m_strokePaint.StrokeMiter = (float)SvgDocument.kStrokeMiterLimit;
            if (attrs == null)
            {
                return;
            }

            using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutSvg);
            m_stretch = (ePathStretch)values.GetInt(Resource.Styleable.AJutSvg_ajutSvgStretch, (int)ePathStretch.None);
            int sourceId = values.GetResourceId(Resource.Styleable.AJutSvg_ajutSvgSource, 0);
            values.Recycle();
            if ((sourceId != 0) && (context.Resources?.OpenRawResource(sourceId) is Stream stream))
            {
                using (stream)
                {
                    this.Source = SvgParsing.Parse(stream);
                }
            }
        }

        protected Svg (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        // ===========[ Properties ]===================================
        /// <summary>The document drawn, from <see cref="SvgParsing.Parse(Stream, double, double)"/>. Null draws nothing.</summary>
        public SvgDocument? Source
        {
            get => m_source;
            set
            {
                m_source = value;
                this.RebuildPaths();
                this.RequestLayout();
                this.Invalidate();
            }
        }

        /// <summary>How the drawing is fitted to the view. None, the default, draws it at its own coordinates, in dp.</summary>
        public ePathStretch Stretch
        {
            get => m_stretch;
            set
            {
                m_stretch = value;
                this.RequestLayout();
                this.Invalidate();
            }
        }

        // ===========[ Public Interface Methods ]===================================
        // Wrapped to its content the view is the drawing's own size in dp, strokes included: from the origin out to its
        //  far edges when unstretched, as WPF's Svg measures, or just what it paints when it is stretched to fit
        protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
        {
            float density = this.Resources?.DisplayMetrics?.Density ?? 1f;
            double naturalWidth = 0.0;
            double naturalHeight = 0.0;
            if ((m_source != null) && !m_source.PaintedBounds.IsEmpty)
            {
                PathBounds bounds = m_source.PaintedBounds;
                bool isStretched = m_stretch != ePathStretch.None;
                naturalWidth = (isStretched ? bounds.Width : Math.Max(0.0, bounds.Right)) * density;
                naturalHeight = (isStretched ? bounds.Height : Math.Max(0.0, bounds.Bottom)) * density;
            }

            this.SetMeasuredDimension(
                ResolveSize((int)Math.Ceiling(naturalWidth) + this.PaddingLeft + this.PaddingRight, widthMeasureSpec),
                ResolveSize((int)Math.Ceiling(naturalHeight) + this.PaddingTop + this.PaddingBottom, heightMeasureSpec)
            );
        }

        protected override void OnDraw (Canvas canvas)
        {
            base.OnDraw(canvas);
            if ((m_source == null) || (m_paths.Count == 0))
            {
                return;
            }

            // The whole drawing is placed once, through the canvas, so a stroke scales with the shapes it outlines, and it
            //  is fitted by its painted bounds, so no stroke reaches past the view
            int saved = canvas.Save();
            canvas.Translate(this.PaddingLeft, this.PaddingTop);
            if (m_stretch == ePathStretch.None)
            {
                float density = this.Resources?.DisplayMetrics?.Density ?? 1f;
                canvas.Scale(density, density);
            }
            else
            {
                double width = this.Width - this.PaddingLeft - this.PaddingRight;
                double height = this.Height - this.PaddingTop - this.PaddingBottom;
                PathFit fit = PathFit.Compute(m_source.PaintedBounds, width, height, m_stretch);
                canvas.Translate((float)fit.OffsetX, (float)fit.OffsetY);
                canvas.Scale((float)fit.ScaleX, (float)fit.ScaleY);
            }

            foreach (DrawnPath drawn in m_paths)
            {
                this.DrawPath(canvas, drawn);
            }

            canvas.RestoreToCount(saved);
        }

        // ===========[ Helper Methods ]===================================
        // Each drawn node's path, built once per document, in the document's own coordinates
        private void RebuildPaths ()
        {
            foreach (DrawnPath drawn in m_paths)
            {
                drawn.Path.Dispose();
            }

            m_paths.Clear();
            if (m_source == null)
            {
                return;
            }

            foreach (SvgNode node in m_source.DrawnNodes)
            {
                if (node.Path != null)
                {
                    m_paths.Add(new DrawnPath(node, node.Path.ToAndroidPath(PathFit.Identity)));
                }
            }
        }

        // An element's opacity applies to it as a whole, fill and stroke together, so a translucent one is drawn into a
        //  layer at that alpha, as WPF composites a Path's Opacity
        private void DrawPath (Canvas canvas, DrawnPath drawn)
        {
            SvgNode node = drawn.Node;
            bool isTranslucent = node.Opacity < 1.0;
            int saved = isTranslucent ? canvas.SaveLayerAlpha(null, (int)Math.Round(Math.Clamp(node.Opacity, 0.0, 1.0) * 255)) : -1;

            if ((node.Fill != null) && _Prepare(m_fillPaint, node.Fill, node.Path!.Bounds))
            {
                canvas.DrawPath(drawn.Path, m_fillPaint);
            }

            // A stroke of no width draws nothing, as WPF's does; Android would draw a hairline
            if ((node.Stroke != null) && (node.StrokeWidth > 0.0) && _Prepare(m_strokePaint, node.Stroke, node.Path!.Bounds))
            {
                m_strokePaint.StrokeWidth = (float)node.StrokeWidth;
                canvas.DrawPath(drawn.Path, m_strokePaint);
            }

            if (isTranslucent)
            {
                canvas.RestoreToCount(saved);
            }

            // A gradient spans the path's own bounds, as a XAML gradient brush does by default
            static bool _Prepare (Paint paint, SvgPaint svgPaint, PathBounds bounds)
            {
                if (svgPaint.Kind == eSvgPaintKind.Color)
                {
                    paint.SetShader(null);
                    paint.Color = svgPaint.Color.ToAndroidColor();
                    return true;
                }

                GradientBuilder gradient = svgPaint.Gradient;
                if ((gradient.Stops == null) || (gradient.Stops.Length == 0) || bounds.IsEmpty)
                {
                    return false;
                }

                var colors = new int[gradient.Stops.Length];
                var offsets = new float[gradient.Stops.Length];
                for (int index = 0; index < colors.Length; ++index)
                {
                    byte[] argb = gradient.Stops[index].ARGB;
                    colors[index] = Color.Argb(argb[0], argb[1], argb[2], argb[3]).ToArgb();
                    offsets[index] = gradient.Stops[index].Offset;
                }

                if (gradient.Type == eBrushGradientType.Linear)
                {
                    gradient.LinearParams.CalculateStartEnd(out double startX, out double startY, out double endX, out double endY);
                    paint.SetShader(new LinearGradient(
                        (float)(bounds.X + (startX * bounds.Width)), (float)(bounds.Y + (startY * bounds.Height)),
                        (float)(bounds.X + (endX * bounds.Width)), (float)(bounds.Y + (endY * bounds.Height)),
                        colors, offsets, Shader.TileMode.Clamp!
                    ));
                    return true;
                }

                // A radial gradient is a circle stretched to its ellipse by the shader's matrix
                RadialGradientParams radial = gradient.RadialParams;
                float centerX = (float)(bounds.X + (radial.CenterX * bounds.Width));
                float centerY = (float)(bounds.Y + (radial.CenterY * bounds.Height));
                float radiusX = Math.Max(0.001f, (float)(radial.RadiusX * bounds.Width));
                float radiusY = Math.Max(0.001f, (float)(radial.RadiusY * bounds.Height));
                var shader = new RadialGradient(centerX, centerY, radiusX, colors, offsets, Shader.TileMode.Clamp!);
                var stretchToEllipse = new Matrix();
                stretchToEllipse.SetScale(1f, radiusY / radiusX, centerX, centerY);
                shader.SetLocalMatrix(stretchToEllipse);
                paint.SetShader(shader);
                return true;
            }
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed record DrawnPath (SvgNode Node, AndroidPath Path);
    }
}
