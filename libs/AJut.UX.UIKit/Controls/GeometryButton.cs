namespace AJut.UX.Controls
{
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using CoreAnimation;
    using CoreGraphics;
    using ObjCRuntime;
    using UIKit;

    /// <summary>
    /// A button whose background is a shape rather than a rectangle: path markup (the XAML path mini-language) is
    /// filled, and optionally traced, behind the button's title and image. The shape is fitted to the button by
    /// <see cref="GeometryStretch"/>, and follows the pressed / disabled states (and pointer over on iPad) the way a
    /// bordered system button does.
    /// </summary>
    public class GeometryButton : UIButton
    {
        // ===========[ Instance Fields ]===================================
        private readonly CAShapeLayer m_shape = new CAShapeLayer();
        private PathMarkupData m_backgroundGeometry;
        private ePathStretch m_geometryStretch = ePathStretch.UniformToFill;
        private nfloat m_traceThickness;

        // ===========[ Setup/Construction/Teardown ]===================================
        public GeometryButton () : base(CGRect.Empty)
        {
            this.Setup();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native button.</summary>
        protected GeometryButton (NativeHandle handle) : base(handle)
        {
            this.Setup();
        }

        private void Setup ()
        {
            // Under UniformToFill the shape covers the button and overflows it, and the overflow is clipped, the same
            // as a XAML Path in a Grid.
            this.ClipsToBounds = true;

            // Index 0 alone does not keep the shape at the back: a configuration based button adds its content view
            // (the title and image) later, and inserts it at index 0 too, which put it under the shape and hid the
            // title. A negative zPosition draws the shape behind every sibling layer whatever order they arrive in.
            m_shape.ZPosition = -1;
            this.Layer.InsertSublayer(m_shape, 0);
            this.Configuration = UIButtonConfiguration.PlainButtonConfiguration;
        }

        // ===========[ Properties ]===================================
        /// <summary>The shape drawn behind the content, from <see cref="PathMarkup.Parse"/>. Null draws nothing.</summary>
        public PathMarkupData BackgroundGeometry
        {
            get => m_backgroundGeometry;
            set
            {
                m_backgroundGeometry = value;
                this.SetNeedsLayout();
            }
        }

        /// <summary>How the shape is fitted to the button. Defaults to UniformToFill, as WinUI3's does.</summary>
        public ePathStretch GeometryStretch
        {
            get => m_geometryStretch;
            set
            {
                m_geometryStretch = value;
                this.SetNeedsLayout();
            }
        }

        /// <summary>The width of the line traced around the shape. Zero (the default) traces nothing.</summary>
        public nfloat TraceThickness
        {
            get => m_traceThickness;
            set
            {
                m_traceThickness = value;
                this.SetNeedsLayout();
            }
        }

        // ===========[ Public Interface Methods ]===================================
        public override void UpdateConfiguration ()
        {
            // The foreground follows the state through the configuration, and the shape layer's colors are CGColors,
            // so they are re-resolved in LayoutSubviews, which this asks for. base.UpdateConfiguration is skipped on
            // purpose, as it is for the brush buttons: it would dim the content on top of the state colors here.
            UIButtonConfiguration configuration = this.Configuration ?? UIButtonConfiguration.PlainButtonConfiguration;
            configuration.BaseForegroundColor = this.GetStateColors().Foreground;
            this.Configuration = configuration;
            this.SetNeedsLayout();
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews();

            // A CAShapeLayer takes CGColors, which freeze a dynamic (light/dark) color, so they are resolved here:
            // UIKit calls LayoutSubviews again on an appearance change.
            StateColors colors = this.GetStateColors();
            CATransaction.Begin();
            CATransaction.DisableActions = true;
            m_shape.Frame = this.Bounds;
            m_shape.FillColor = colors.Fill.GetResolvedColor(this.TraitCollection).CGColor;
            m_shape.StrokeColor = colors.Stroke.GetResolvedColor(this.TraitCollection).CGColor;
            m_shape.LineWidth = m_traceThickness;
            m_shape.FillRule = m_backgroundGeometry?.FillRule == ePathFillRule.Nonzero ? CAShapeLayer.FillRuleNonZero : CAShapeLayer.FillRuleEvenOdd;
            m_shape.Path = this.BuildShapePath();
            CATransaction.Commit();
        }

        // ===========[ Helper Methods ]===================================
        private CGPath BuildShapePath ()
        {
            if (m_backgroundGeometry == null)
            {
                return null;
            }

            PathFit fit = PathFit.Compute(m_backgroundGeometry.Bounds, this.Bounds.Width, this.Bounds.Height, m_geometryStretch, m_traceThickness);
            return m_backgroundGeometry.ToBezierPath(fit).CGPath;
        }

        // MobileLook.GeometryButton's keys, which Android's GeometryButton draws too: the stock button keys for the fill
        // (the background) and the foreground, so it sits with an untouched BrushButton, themed or not, and its own trace
        // keys for the outline. The trace keys take the common borders, so the outline stays on touch, where the touch
        // palettes take button borders off; pressed, the outline goes to the bright highlight.
        private StateColors GetStateColors ()
        {
            if (!this.Enabled)
            {
                return _Colors(MobileLook.GeometryButton.DisabledFill, MobileLook.GeometryButton.DisabledTrace, MobileLook.GeometryButton.DisabledGlyph);
            }

            if (this.Highlighted)
            {
                return _Colors(MobileLook.GeometryButton.PressedFill, MobileLook.GeometryButton.PressedTrace, MobileLook.GeometryButton.PressedGlyph);
            }

            return this.Hovered
                ? _Colors(MobileLook.GeometryButton.PointerOverFill, MobileLook.GeometryButton.PointerOverTrace, MobileLook.GeometryButton.PointerOverGlyph)
                : _Colors(MobileLook.GeometryButton.Fill, MobileLook.GeometryButton.Trace, MobileLook.GeometryButton.Glyph);

            static StateColors _Colors (string fill, string stroke, string foreground)
            {
                return new StateColors(
                    ThemedColors.Get(fill) ?? UIColor.Clear,
                    ThemedColors.Get(stroke) ?? UIColor.Clear,
                    ThemedColors.Get(foreground) ?? UIColor.Label
                );
            }
        }

        // ===========[ Subclasses/structs ]===================================
        private readonly record struct StateColors (UIColor Fill, UIColor Stroke, UIColor Foreground);
    }
}
