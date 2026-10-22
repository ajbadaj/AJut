namespace AJut.UX.Theming
{
    using AppKit;
    using CoreAnimation;

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
    public class ThemedView : NSView
    {
        private string m_backgroundColorKey;
        private string m_borderColorKey;
        private double m_borderThickness;
        private double m_cornerRadius;

        // ===========[ Setup/Construction/Teardown ]===========================
        public ThemedView ()
        {
            this.WantsLayer = true;
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
                this.ApplyLayerColors(layer);
            }
        }

        public override void ViewDidChangeEffectiveAppearance ()
        {
            base.ViewDidChangeEffectiveAppearance();
            this.NeedsDisplay = true;
        }

        // ===========[ Helper Methods ]========================================
        /// <summary>
        /// Pushes this view's colors into its layer. Only called from <see cref="UpdateLayer"/>, where the
        /// drawing appearance is this view's, so a CGColor taken here is the right one for it.
        /// </summary>
        protected virtual void ApplyLayerColors (CALayer layer)
        {
            layer.BackgroundColor = m_backgroundColorKey != null ? ThemedColors.Get(m_backgroundColorKey).CGColor : null;
            layer.BorderColor = m_borderColorKey != null ? ThemedColors.Get(m_borderColorKey).CGColor : null;
            layer.BorderWidth = m_borderColorKey != null ? (nfloat)m_borderThickness : (nfloat)0.0;
            layer.CornerRadius = (nfloat)m_cornerRadius;
        }
    }
}
