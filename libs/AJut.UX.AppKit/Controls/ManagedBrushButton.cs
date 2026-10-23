namespace AJut.UX.Controls
{
    using System;
    using AppKit;
    using CoreGraphics;
    using Foundation;

    // ===========[ ManagedBrushButton ]=========================================
    // The base for buttons that pick their own colors per state, the AppKit counterpart of
    // WinUI3's _ManagedBrushButton: a rounded surface with a border and a centered title, drawn in
    // whatever GetColorsForState returns for the current state. BrushButton takes those colors
    // as properties; AutoBrushButton derives them from one accent color.
    public abstract class ManagedBrushButton : ManagedButton
    {
        private string m_title = String.Empty;
        private NSFont m_titleFont = NSFont.SystemFontOfSize(NSFont.SystemFontSize);
        private double m_cornerRadius = 4.0;
        private double m_borderThickness = 1.0;
        private NSEdgeInsets m_padding = new NSEdgeInsets(4, 10, 4, 10);

        // ===========[ Properties ]============================================
        /// <summary>The text shown centered on the button</summary>
        public string Title
        {
            get => m_title;
            set
            {
                m_title = value ?? String.Empty;
                this.InvalidateIntrinsicContentSize();
                this.NeedsDisplay = true;
            }
        }

        /// <summary>The title's font, the system font at its regular size by default</summary>
        public NSFont TitleFont
        {
            get => m_titleFont;
            set
            {
                m_titleFont = value ?? NSFont.SystemFontOfSize(NSFont.SystemFontSize);
                this.InvalidateIntrinsicContentSize();
                this.NeedsDisplay = true;
            }
        }

        /// <summary>Corner radius of the surface in points</summary>
        public double CornerRadius
        {
            get => m_cornerRadius;
            set
            {
                m_cornerRadius = value;
                this.NeedsDisplay = true;
            }
        }

        /// <summary>Border width in points, drawn inside the bounds</summary>
        public double BorderThickness
        {
            get => m_borderThickness;
            set
            {
                m_borderThickness = value;
                this.InvalidateIntrinsicContentSize();
                this.NeedsDisplay = true;
            }
        }

        /// <summary>Space between the border and the title</summary>
        public NSEdgeInsets Padding
        {
            get => m_padding;
            set
            {
                m_padding = value;
                this.InvalidateIntrinsicContentSize();
                this.NeedsDisplay = true;
            }
        }

        protected override string DefaultAccessibilityLabel => m_title;

        public override CGSize IntrinsicContentSize
        {
            get
            {
                CGSize titleSize = this.MakeTitle(NSColor.Black).Size;
                return new CGSize(
                    (nfloat)(Math.Ceiling((double)titleSize.Width) + (double)m_padding.Left + (double)m_padding.Right + (2.0 * m_borderThickness)),
                    (nfloat)(Math.Ceiling((double)titleSize.Height) + (double)m_padding.Top + (double)m_padding.Bottom + (2.0 * m_borderThickness))
                );
            }
        }

        // ===========[ Public Interface Methods ]==============================
        public override void DrawRect (CGRect dirtyRect)
        {
            SurfaceColors colors = this.GetColorsForState(this.CurrentState);

            // The border is stroked on the rect's edge, half in and half out, so the rect is inset
            // by half the thickness to keep all of it inside the bounds
            nfloat halfBorder = (nfloat)(m_borderThickness / 2.0);
            CGRect surfaceRect = this.Bounds.Inset(halfBorder, halfBorder);
            NSBezierPath surface = NSBezierPath.FromRoundedRect(surfaceRect, (nfloat)m_cornerRadius, (nfloat)m_cornerRadius);

            if (colors.Background != null)
            {
                colors.Background.SetFill();
                surface.Fill();
            }

            if (colors.Border != null && m_borderThickness > 0.0)
            {
                surface.LineWidth = (nfloat)m_borderThickness;
                colors.Border.SetStroke();
                surface.Stroke();
            }

            if (colors.Foreground != null && m_title.Length > 0)
            {
                NSAttributedString title = this.MakeTitle(colors.Foreground);
                CGSize size = title.Size;
                CGRect bounds = this.Bounds;
                title.DrawAtPoint(new CGPoint(
                    (bounds.Width - size.Width) / 2,
                    (bounds.Height - size.Height) / 2
                ));
            }
        }

        // ===========[ Helper Methods ]========================================
        /// <summary>The colors to draw the given state in. A null color is not drawn.</summary>
        protected abstract SurfaceColors GetColorsForState (eManagedButtonState state);

        private NSAttributedString MakeTitle (NSColor foreground)
        {
            return new NSAttributedString(m_title, new NSStringAttributes { Font = m_titleFont, ForegroundColor = foreground });
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>The three colors a managed brush button draws a state in; null for any of them draws nothing for that part</summary>
        public readonly record struct SurfaceColors (NSColor Background, NSColor Foreground, NSColor Border);
    }
}
