namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics;
    using Android.Runtime;
    using Android.Util;
    using Android.Views;
    using AndroidColor = Android.Graphics.Color;

    /// <summary>
    /// A button whose shape is a path: the background is a geometry, filled with the standard button colors for each
    /// state and traced in the GeometryButton trace colors, with the button's text drawn over it. The geometry is path
    /// markup, the same mini language a XAML Path's Data takes ("M 0,0 L 10,0 10,10 Z", F0/F1 fill rules, curves and
    /// arcs), so the same string draws the same shape on every AJut stack.
    /// </summary>
    /// <remarks>
    /// The path is fitted into the view by <see cref="GeometryStretch"/> (UniformToFill by default, as on WinUI3),
    /// inset by half the trace so the trace is not clipped. Geometry coordinates are dp, as they are device independent
    /// units on WinUI3: an unstretched path draws at that size, and a view sized wrap_content takes the geometry's size
    /// (or its text's, if that is bigger), the way an unsized GeometryButton does on WinUI3. Parsing and fitting are
    /// AJut.UX's <see cref="PathMarkup"/> and <see cref="PathFit"/>; this only builds the Android Path from the result.
    /// In a layout, ajutBackgroundGeometry, ajutTraceThickness and ajutGeometryStretch set the three.
    /// </remarks>
    [Register("ajut.ux.controls.GeometryButton")]
    public class GeometryButton : _ManagedBrushButton
    {
        // The stock button keys for the fill and text, so a GeometryButton fills like a standard button in each state,
        // the way WinUI3's GeometryButton does. The trace has keys of its own: it is part of the shape, and the touch
        // palettes take button borders off, which would leave a shape drawn as a trace alone with nothing to show. The
        // literals are the unthemed look, the same as BrushButton's.
        private static readonly ThemedColor kNormalFill = ThemedColor.FromArgb("ButtonBackground", 0xFFF3F3F3, 0xFF2D2D2D);
        private static readonly ThemedColor kNormalForeground = ThemedColor.FromArgb("ButtonForeground", 0xFF1A1A1A, 0xFFFFFFFF);
        private static readonly ThemedColor kNormalTrace = ThemedColor.FromArgb(AJutThemeKeys.GeometryButton_Trace, 0xFFC8C8C8, 0xFF3F3F3F);
        private static readonly ThemedColor kPointerOverFill = ThemedColor.FromArgb("ButtonBackgroundPointerOver", 0xFFE8E8E8, 0xFF363636);
        private static readonly ThemedColor kPointerOverForeground = ThemedColor.FromArgb("ButtonForegroundPointerOver", 0xFF1A1A1A, 0xFFFFFFFF);
        private static readonly ThemedColor kPointerOverTrace = ThemedColor.FromArgb(AJutThemeKeys.GeometryButton_TracePointerOver, 0xFFB4B4B4, 0xFF4A4A4A);
        private static readonly ThemedColor kPressedFill = ThemedColor.FromArgb("ButtonBackgroundPressed", 0xFFDADADA, 0xFF242424);
        private static readonly ThemedColor kPressedForeground = ThemedColor.FromArgb("ButtonForegroundPressed", 0xFF5C5C5C, 0xFFCFCFCF);
        private static readonly ThemedColor kPressedTrace = ThemedColor.FromArgb(AJutThemeKeys.GeometryButton_TracePressed, 0xFFC8C8C8, 0xFF3F3F3F);
        private static readonly ThemedColor kDisabledFill = ThemedColor.FromArgb("ButtonBackgroundDisabled", 0xFFF3F3F3, 0xFF2A2A2A);
        private static readonly ThemedColor kDisabledForeground = ThemedColor.FromArgb("ButtonForegroundDisabled", 0xFFA0A0A0, 0xFF6E6E6E);
        private static readonly ThemedColor kDisabledTrace = ThemedColor.FromArgb(AJutThemeKeys.GeometryButton_TraceDisabled, 0xFFE0E0E0, 0xFF333333);

        private readonly Paint m_fillPaint = new Paint(PaintFlags.AntiAlias);
        private readonly Paint m_tracePaint = new Paint(PaintFlags.AntiAlias);
        private readonly Path m_fittedPath = new Path();
        private string? m_backgroundGeometry;
        private PathMarkupData? m_geometry;
        private float m_traceThickness;
        private ePathStretch m_geometryStretch = ePathStretch.UniformToFill;

        // ===========[ Setup/Construction/Teardown ]===================================
        public GeometryButton (Context context) : base(context)
        {
            this.Initialize(context, null);
        }

        public GeometryButton (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            this.Initialize(context, attrs);
        }

        public GeometryButton (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.Initialize(context, attrs);
        }

        protected GeometryButton (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        // ===========[ Properties ]===================================
        /// <summary>
        /// The button's shape, as path markup. Null or empty draws no shape. Markup that does not parse draws no shape
        /// either, and logs why.
        /// </summary>
        public string? BackgroundGeometry
        {
            get => m_backgroundGeometry;
            set
            {
                m_backgroundGeometry = value;
                m_geometry = _Parse(value);
                this.RebuildFittedPath();
            }
        }

        /// <summary>The thickness of the trace around the shape, in pixels. Zero (the default) draws no trace.</summary>
        public float TraceThickness
        {
            get => m_traceThickness;
            set
            {
                m_traceThickness = Math.Max(0f, value);
                this.RebuildFittedPath();
            }
        }

        /// <summary>How the shape is fitted into the button. Defaults to UniformToFill.</summary>
        public ePathStretch GeometryStretch
        {
            get => m_geometryStretch;
            set
            {
                m_geometryStretch = value;
                this.RebuildFittedPath();
            }
        }

        // ===========[ Public Interface Methods ]===================================
        // The base paints its own rounded surface; here that surface stays clear and the path is the surface instead.
        protected override SurfaceStateColors GetColorsForState (eManagedButtonState state)
        {
            ColorArgb clear = new ColorArgb(0, 0, 0, 0);
            return new SurfaceStateColors(clear, ThemeColors.Resolve(this.Context!, _ForegroundFor(state)), clear);
        }

        // A side that is not an exact size grows to the geometry's natural size, within whatever bound the parent set
        protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
        {
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
            if ((m_geometry == null) || m_geometry.Bounds.IsEmpty)
            {
                return;
            }

            // Stretched, the path is scaled to the bounds' own size; unstretched, it sits at its coordinates, so it
            // reaches out to the bounds' far edge. The trace straddles the edge either way.
            float density = this.Resources!.DisplayMetrics!.Density;
            PathBounds bounds = m_geometry.Bounds;
            bool isStretched = m_geometryStretch != ePathStretch.None;
            double naturalWidth = ((isStretched ? bounds.Width : bounds.X + bounds.Width) * density) + m_traceThickness;
            double naturalHeight = ((isStretched ? bounds.Height : bounds.Y + bounds.Height) * density) + m_traceThickness;
            int width = _GrowToNatural(this.MeasuredWidth, widthMeasureSpec, naturalWidth);
            int height = _GrowToNatural(this.MeasuredHeight, heightMeasureSpec, naturalHeight);
            if ((width != this.MeasuredWidth) || (height != this.MeasuredHeight))
            {
                // Measured again at the grown size rather than only setting it, since the text lays itself out
                // during measure, and would otherwise stay centered in the smaller size
                base.OnMeasure(
                    MeasureSpec.MakeMeasureSpec(width, MeasureSpecMode.Exactly),
                    MeasureSpec.MakeMeasureSpec(height, MeasureSpecMode.Exactly)
                );
            }
        }

        protected override void OnSizeChanged (int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged(w, h, oldw, oldh);
            this.RebuildFittedPath();
        }

        protected override void OnDraw (Canvas canvas)
        {
            if (m_geometry != null)
            {
                Context context = this.Context!;
                m_fillPaint.Color = ThemeColors.Resolve(context, _FillFor(this.ActiveState)).ToAndroidColor();
                canvas.DrawPath(m_fittedPath, m_fillPaint);
                if (m_traceThickness > 0f)
                {
                    m_tracePaint.Color = ThemeColors.Resolve(context, _TraceFor(this.ActiveState)).ToAndroidColor();
                    canvas.DrawPath(m_fittedPath, m_tracePaint);
                }
            }

            // The text draws over the shape.
            base.OnDraw(canvas);
        }

        protected override void DrawableStateChanged ()
        {
            base.DrawableStateChanged();
            this.Invalidate();
        }

        // ===========[ Helper Methods ]===================================
        private void Initialize (Context context, IAttributeSet? attrs)
        {
            m_fillPaint.SetStyle(Android.Graphics.Paint.Style.Fill!);
            m_tracePaint.SetStyle(Android.Graphics.Paint.Style.Stroke!);
            m_tracePaint.StrokeJoin = Android.Graphics.Paint.Join.Miter;

            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutGeometryButton);
                m_backgroundGeometry = values.GetString(Resource.Styleable.AJutGeometryButton_ajutBackgroundGeometry);
                m_traceThickness = Math.Max(0f, values.GetDimension(Resource.Styleable.AJutGeometryButton_ajutTraceThickness, 0f));
                m_geometryStretch = (ePathStretch)values.GetInt(Resource.Styleable.AJutGeometryButton_ajutGeometryStretch, (int)ePathStretch.UniformToFill);
                values.Recycle();
            }

            m_geometry = _Parse(m_backgroundGeometry);
            this.CompleteInitialization();
            this.RebuildFittedPath();
        }

        // Fits the parsed geometry into the view once, here, so drawing only draws. Runs on a size, geometry, trace
        // or stretch change.
        private void RebuildFittedPath ()
        {
            m_fittedPath.Reset();
            m_tracePaint.StrokeWidth = m_traceThickness;
            if ((m_geometry == null) || (this.Width <= 0) || (this.Height <= 0))
            {
                this.Invalidate();
                return;
            }

            // Unstretched, geometry units are dp, so the path only scales by the screen's density
            float density = this.Resources!.DisplayMetrics!.Density;
            PathFit fit = m_geometryStretch == ePathStretch.None
                ? new PathFit(density, density, 0.0, 0.0)
                : PathFit.Compute(m_geometry.Bounds, this.Width, this.Height, m_geometryStretch, m_traceThickness);
            m_fittedPath.SetFillType(m_geometry.FillRule == ePathFillRule.Nonzero ? Path.FillType.Winding! : Path.FillType.EvenOdd!);
            foreach (PathCommand command in m_geometry.Commands)
            {
                switch (command.Kind)
                {
                    case ePathCommand.MoveTo:
                        PathPoint moveTo = fit.Apply(command.End);
                        m_fittedPath.MoveTo((float)moveTo.X, (float)moveTo.Y);
                        break;

                    case ePathCommand.LineTo:
                        PathPoint lineTo = fit.Apply(command.End);
                        m_fittedPath.LineTo((float)lineTo.X, (float)lineTo.Y);
                        break;

                    case ePathCommand.CubicTo:
                        PathPoint control1 = fit.Apply(command.Control1);
                        PathPoint control2 = fit.Apply(command.Control2);
                        PathPoint end = fit.Apply(command.End);
                        m_fittedPath.CubicTo((float)control1.X, (float)control1.Y, (float)control2.X, (float)control2.Y, (float)end.X, (float)end.Y);
                        break;

                    case ePathCommand.Close:
                        m_fittedPath.Close();
                        break;
                }
            }

            this.Invalidate();
        }

        private static int _GrowToNatural (int measured, int measureSpec, double natural)
        {
            MeasureSpecMode mode = MeasureSpec.GetMode(measureSpec);
            if (mode == MeasureSpecMode.Exactly)
            {
                return measured;
            }

            int grown = Math.Max(measured, (int)Math.Ceiling(natural));
            return (mode == MeasureSpecMode.AtMost) ? Math.Min(grown, MeasureSpec.GetSize(measureSpec)) : grown;
        }

        private static PathMarkupData? _Parse (string? markup)
        {
            if (string.IsNullOrWhiteSpace(markup))
            {
                return null;
            }

            if (PathMarkup.TryParse(markup, out PathMarkupData? data, out string? error))
            {
                return data;
            }

            Logger.LogError($"GeometryButton could not read its geometry '{markup}': {error}");
            return null;
        }

        private static ThemedColor _FillFor (eManagedButtonState state) => state switch
        {
            eManagedButtonState.PointerOver => kPointerOverFill,
            eManagedButtonState.Pressed => kPressedFill,
            eManagedButtonState.Disabled => kDisabledFill,
            _ => kNormalFill,
        };

        private static ThemedColor _ForegroundFor (eManagedButtonState state) => state switch
        {
            eManagedButtonState.PointerOver => kPointerOverForeground,
            eManagedButtonState.Pressed => kPressedForeground,
            eManagedButtonState.Disabled => kDisabledForeground,
            _ => kNormalForeground,
        };

        private static ThemedColor _TraceFor (eManagedButtonState state) => state switch
        {
            eManagedButtonState.PointerOver => kPointerOverTrace,
            eManagedButtonState.Pressed => kPressedTrace,
            eManagedButtonState.Disabled => kDisabledTrace,
            _ => kNormalTrace,
        };
    }
}
