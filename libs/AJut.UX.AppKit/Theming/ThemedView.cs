namespace AJut.UX.Theming
{
    using System.Collections.Generic;
    using AppKit;
    using CoreAnimation;
    using CoreGraphics;
    using Foundation;
    using ObjCRuntime;

    // ===========[ ThemedView ]=================================================
    // A layer-backed NSView whose layer colors come from ThemedColors keys and stay right when the
    // appearance or the theme changes.
    //
    // Layer colors are CGColors, and a CGColor taken from a dynamic NSColor is a snapshot (see
    // ThemedColors). So nothing here caches a CGColor: every color is pushed from UpdateLayer,
    // which AppKit calls with the drawing appearance set to this view's effective appearance, and
    // the view asks for UpdateLayer again whenever its effective appearance changes. A theme load
    // or unload marks every view for redisplay, which also lands here.
    //
    // Derived controls override ApplyLayerColors to push more than background and border, and
    // call base first.
    //
    // A background key can resolve to a gradient (the pop-out surface, AJut_Brush_PopOutControlBackground,
    // is one). A layer's background is one color, so a gradient goes in a CAGradientLayer kept as the
    // backing layer's first sublayer, under every subview's layer and under the border, which a layer
    // draws over its sublayers. It takes the layer's bounds, corners and corner mask after
    // ApplyLayerColors, so a subclass that rounds only some corners rounds the gradient the same way.
    // Its stops run top to bottom wherever the layer tree puts the origin: contentsAreFlipped says
    // whether the gradient layer is drawn y down, which decides the end its first stop goes at.
    // Implicit animations are off for it, or it would fade on a theme change and trail a resize.
    public class ThemedView : NSView
    {
        private string m_backgroundColorKey;
        private string m_borderColorKey;
        private double m_borderThickness;
        private double m_cornerRadius;
        private IReadOnlyList<AJutGradientStop> m_backgroundStops;
        private CAGradientLayer m_backgroundGradient;

        // ===========[ Setup/Construction/Teardown ]===========================
        public ThemedView ()
        {
            this.WantsLayer = true;
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native view whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls: the native view keeps the layer it has. None of its
        /// managed state survives a dispose, so a call that needs that state can still fail: dispose it only once AppKit
        /// is done with it.
        /// </summary>
        protected ThemedView (NativeHandle handle) : base(handle)
        {
        }

        // ===========[ Properties ]============================================
        /// <summary>Top-left origin, which is what every AJut layout assumes</summary>
        public override bool IsFlipped => true;

        /// <summary>Layer-backed views that set their layer up in UpdateLayer opt in here, or AppKit falls back to DrawRect</summary>
        public override bool WantsUpdateLayer => true;

        /// <summary>The <see cref="ThemedColors"/> key for the background, or null for no background</summary>
        public string BackgroundColorKey
        {
            get => m_backgroundColorKey;
            set
            {
                m_backgroundColorKey = value;
                this.NeedsDisplay = true;
            }
        }

        /// <summary>The <see cref="ThemedColors"/> key for the border, or null for no border</summary>
        public string BorderColorKey
        {
            get => m_borderColorKey;
            set
            {
                m_borderColorKey = value;
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
                this.NeedsDisplay = true;
            }
        }

        /// <summary>Corner radius in points, applied to both the background and the border</summary>
        public double CornerRadius
        {
            get => m_cornerRadius;
            set
            {
                m_cornerRadius = value;
                this.NeedsDisplay = true;
            }
        }

        // ===========[ Public Interface Methods ]==============================
        public override void UpdateLayer ()
        {
            CALayer layer = this.Layer;
            if (layer != null)
            {
                m_backgroundStops = (m_backgroundColorKey != null) ? ThemedColors.GetThemedGradientStops(m_backgroundColorKey, this.EffectiveAppearance) : null;
                this.ApplyLayerColors(layer);
                this.SyncBackgroundGradient(layer);
            }
        }

        public override void ViewDidChangeEffectiveAppearance ()
        {
            base.ViewDidChangeEffectiveAppearance();
            this.NeedsDisplay = true;
        }

        // The gradient keeps to the view's size between redraws, which a resize doesn't always ask for
        public override void SetFrameSize (CGSize newSize)
        {
            base.SetFrameSize(newSize);
            if ((m_backgroundGradient != null) && (this.Layer != null))
            {
                CATransaction.Begin();
                CATransaction.DisableActions = true;
                m_backgroundGradient.Frame = this.Layer.Bounds;
                CATransaction.Commit();
            }
        }

        // ===========[ Helper Methods ]========================================
        /// <summary>
        /// Pushes this view's colors into its layer. Only called from <see cref="UpdateLayer"/>, where the
        /// drawing appearance is this view's, so a CGColor taken here is the right one for it. A background
        /// key that resolves to a gradient leaves the layer's own background clear (the gradient layer under
        /// the subviews paints it).
        /// </summary>
        protected virtual void ApplyLayerColors (CALayer layer)
        {
            layer.BackgroundColor = ((m_backgroundColorKey != null) && (m_backgroundStops == null)) ? ThemedColors.Get(m_backgroundColorKey).CGColor : null;
            layer.BorderColor = m_borderColorKey != null ? ThemedColors.Get(m_borderColorKey).CGColor : null;
            layer.BorderWidth = m_borderColorKey != null ? (nfloat)m_borderThickness : (nfloat)0.0;
            layer.CornerRadius = (nfloat)m_cornerRadius;
        }

        // Adds, updates or removes the gradient layer for the background (see the class comment)
        private void SyncBackgroundGradient (CALayer layer)
        {
            if (m_backgroundStops == null)
            {
                if (m_backgroundGradient != null)
                {
                    m_backgroundGradient.RemoveFromSuperLayer();
                    m_backgroundGradient = null;
                }

                return;
            }

            CATransaction.Begin();
            CATransaction.DisableActions = true;
            if (m_backgroundGradient == null)
            {
                m_backgroundGradient = new CAGradientLayer();
                layer.InsertSublayer(m_backgroundGradient, 0);
            }

            var colors = new CGColor[m_backgroundStops.Count];
            var locations = new NSNumber[m_backgroundStops.Count];
            for (int i = 0; i < colors.Length; ++i)
            {
                colors[i] = m_backgroundStops[i].Color.ToNSColor().CGColor;
                locations[i] = NSNumber.FromDouble(m_backgroundStops[i].Offset);
            }

            m_backgroundGradient.Colors = colors;
            m_backgroundGradient.Locations = locations;
            m_backgroundGradient.Frame = layer.Bounds;
            m_backgroundGradient.CornerRadius = layer.CornerRadius;
            m_backgroundGradient.MaskedCorners = layer.MaskedCorners;
            m_backgroundGradient.MasksToBounds = true;

            // The first stop goes at the top as it is drawn: y = 0 is the top only where the gradient layer is drawn flipped
            bool isDrawnFlipped = m_backgroundGradient.ContentsAreFlipped;
            m_backgroundGradient.StartPoint = new CGPoint(0.5, isDrawnFlipped ? 0.0 : 1.0);
            m_backgroundGradient.EndPoint = new CGPoint(0.5, isDrawnFlipped ? 1.0 : 0.0);
            CATransaction.Commit();
        }
    }
}
