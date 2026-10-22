namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;

    // ===========[ GeometryButton ]=============================================
    // A button that is a shape: path markup (the same string WinUI3's GeometryButton takes)
    // filled, and optionally traced, in colors that follow the button's state. The shape is
    // clipped to the button, so UniformToFill cuts off what overflows rather than drawing past it.
    //
    // The path comes from AJut.UX's PathMarkup, fitted to the button with PathFit, so a markup
    // string draws the same here as on the other stacks. The view is flipped, which makes path
    // data (y-down) draw the right way up with no transform. The hit area is the whole button,
    // not only the shape.
    //
    // Colors: the fill and trace keys alias, when themed, to the same Button* keys the WinUI3
    // GeometryButton's states use. Unthemed, the fill is a label color rather than a button
    // face color, so the shape reads as a glyph among stock controls.
    public class GeometryButton : ManagedButton
    {
        private PathMarkupData m_geometry;
        private string m_geometryMarkup;
        private ePathStretch m_geometryStretch = ePathStretch.UniformToFill;
        private double m_traceThickness;

        // ===========[ Properties ]============================================
        /// <summary>
        /// The shape, as path markup (for example "M0,0 L10,5 L0,10 Z"). Setting markup that does not parse
        /// clears the shape and logs the parse error.
        /// </summary>
        public string GeometryMarkup
        {
            get => m_geometryMarkup;
            set
            {
                m_geometryMarkup = value;
                if (String.IsNullOrWhiteSpace(value))
                {
                    this.Geometry = null;
                }
                else if (PathMarkup.TryParse(value, out PathMarkupData data, out string error))
                {
                    this.Geometry = data;
                }
                else
                {
                    Logger.LogError($"[GeometryButton] {error}");
                    this.Geometry = null;
                }
            }
        }

        /// <summary>The shape as parsed path data, for a shape built in code rather than from markup</summary>
        public PathMarkupData Geometry
        {
            get => m_geometry;
            set
            {
                m_geometry = value;
                this.InvalidateIntrinsicContentSize();
                this.NeedsDisplay = true;
            }
        }

        /// <summary>How the shape is fitted to the button, UniformToFill by default (the WinUI3 GeometryButton's default)</summary>
        public ePathStretch GeometryStretch
        {
            get => m_geometryStretch;
            set
            {
                m_geometryStretch = value;
                this.NeedsDisplay = true;
            }
        }

        /// <summary>Width of the outline traced around the shape in points; 0, the default, draws no outline</summary>
        public double TraceThickness
        {
            get => m_traceThickness;
            set
            {
                m_traceThickness = value;
                this.InvalidateIntrinsicContentSize();
                this.NeedsDisplay = true;
            }
        }

        /// <summary>With nothing constraining it, the button is the size of its shape (plus its outline)</summary>
        public override CGSize IntrinsicContentSize
        {
            get
            {
                if (m_geometry == null || m_geometry.Bounds.IsEmpty)
                {
                    return new CGSize(NSView.NoIntrinsicMetric, NSView.NoIntrinsicMetric);
                }

                return new CGSize(
                    (nfloat)(m_geometry.Bounds.Width + m_traceThickness),
                    (nfloat)(m_geometry.Bounds.Height + m_traceThickness)
                );
            }
        }

        // ===========[ Public Interface Methods ]==============================
        public override void DrawRect (CGRect dirtyRect)
        {
            if (m_geometry == null || m_geometry.Bounds.IsEmpty)
            {
                return;
            }

            // UniformToFill overfills on purpose and is cut to the button, as the XAML and UIKit buttons are. From
            // macOS 14 a view no longer clips its own drawing to its bounds by default, so the clip is set here.
            CGRect bounds = this.Bounds;
            NSBezierPath.ClipRect(bounds);
            PathFit fit = PathFit.Compute(m_geometry.Bounds, (double)bounds.Width, (double)bounds.Height, m_geometryStretch, m_traceThickness);
            NSBezierPath path = m_geometry.ToBezierPath(fit);

            eManagedButtonState state = this.CurrentState;
            ThemedColors.Get(FillKeyFor(state)).SetFill();
            path.Fill();

            if (m_traceThickness > 0.0)
            {
                path.LineWidth = (nfloat)m_traceThickness;
                ThemedColors.Get(TraceKeyFor(state)).SetStroke();
                path.Stroke();
            }
        }

        // ===========[ Helper Methods ]========================================
        private static string FillKeyFor (eManagedButtonState state)
        {
            switch (state)
            {
                case eManagedButtonState.PointerOver: return ThemedColorKeys.GeometryButton_FillPointerOver;
                case eManagedButtonState.Pressed: return ThemedColorKeys.GeometryButton_FillPressed;
                case eManagedButtonState.Disabled: return ThemedColorKeys.GeometryButton_FillDisabled;
                default: return ThemedColorKeys.GeometryButton_Fill;
            }
        }

        private static string TraceKeyFor (eManagedButtonState state)
        {
            switch (state)
            {
                case eManagedButtonState.PointerOver: return ThemedColorKeys.GeometryButton_TracePointerOver;
                case eManagedButtonState.Pressed: return ThemedColorKeys.GeometryButton_TracePressed;
                case eManagedButtonState.Disabled: return ThemedColorKeys.GeometryButton_TraceDisabled;
                default: return ThemedColorKeys.GeometryButton_Trace;
            }
        }
    }
}
