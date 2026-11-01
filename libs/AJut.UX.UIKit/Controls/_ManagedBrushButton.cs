namespace AJut.UX.Controls
{
    using CoreGraphics;
    using ObjCRuntime;
    using UIKit;

    /// <summary>
    /// Shared plumbing for the color driven buttons. It stays a plain UIButton, so UIControl still owns the
    /// highlighted / disabled tracking (and pointer hover on iPad), and instead of the standard button
    /// styles it paints its own background, foreground, and border for whichever state the button is in.
    /// Subclasses only have to answer "which colors for this state".
    ///
    /// Not meant to be used directly - use BrushButton or AutoBrushButton.
    /// </summary>
    public abstract class _ManagedBrushButton : UIButton
    {
        // Android's managed buttons' defaults, in dp there and points here
        private const float kDefaultCornerRadius = 4f;
        private const float kDefaultBorderThickness = 1f;

        // ===========[ Instance Fields ]===================================
        private nfloat m_cornerRadius = kDefaultCornerRadius;
        private nfloat m_borderThickness = kDefaultBorderThickness;

        // ===========[ Setup/Construction/Teardown ]===================================
        protected _ManagedBrushButton () : base(CGRect.Empty)
        {
            this.Configuration = CreateBaseConfiguration();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native button.</summary>
        protected _ManagedBrushButton (NativeHandle handle) : base(handle)
        {
        }

        // ===========[ Properties ]===================================
        /// <summary>The surface's corner radius, in points. Defaults to 4, as Android's does in dp.</summary>
        public nfloat CornerRadius
        {
            get => m_cornerRadius;
            set
            {
                m_cornerRadius = value;
                this.RefreshActiveStateColors();
            }
        }

        /// <summary>The border's thickness, in points. Defaults to 1, as Android's does in dp.</summary>
        public nfloat BorderThickness
        {
            get => m_borderThickness;
            set
            {
                m_borderThickness = value;
                this.RefreshActiveStateColors();
            }
        }

        /// <summary>The interaction state the button is painted for right now.</summary>
        public eManagedButtonState ActiveState
        {
            get
            {
                if (!this.Enabled)
                {
                    return eManagedButtonState.Disabled;
                }

                if (this.Highlighted)
                {
                    return eManagedButtonState.Pressed;
                }

                return this.Hovered ? eManagedButtonState.PointerOver : eManagedButtonState.Normal;
            }
        }

        // ===========[ Public Interface Methods ]===================================
        public override void UpdateConfiguration ()
        {
            // How this works, since UIButton offers three ways to color a button and two of them fight:
            //  - The legacy per state setters (SetTitleColor, BackgroundColor, layer border) are ignored or
            //    overridden once a UIButtonConfiguration is set, and a layer border takes a CGColor, which
            //    freezes a dynamic (light/dark) color at the moment it is converted.
            //  - A configurationUpdateHandler works, but it is a block the button holds that captures the
            //    button, which is a cycle between the native button and its managed peer.
            //  - Overriding updateConfiguration (iOS 15) is what Apple intends for subclasses. UIKit calls
            //    it whenever the state that matters to a configuration changes: highlighted, enabled,
            //    selected and hovered. That is the whole state machine this button needs.
            //
            // base.UpdateConfiguration is not called on purpose: it applies the system's own state
            // adjustments (dimming on highlight and when disabled), and this button owns those colors.
            // The colors go onto the configuration's background, which takes UIColor rather than CGColor,
            // so dynamic colors keep following light/dark with no trait change handling here.
            SurfaceColors colors = this.GetColorsForState(this.ActiveState);
            UIButtonConfiguration configuration = this.Configuration ?? CreateBaseConfiguration();

            UIBackgroundConfiguration background = configuration.Background;
            background.BackgroundColor = colors.Background ?? UIColor.Clear;
            background.StrokeColor = colors.Border ?? UIColor.Clear;
            background.CornerRadius = m_cornerRadius;
            background.StrokeWidth = m_borderThickness;
            configuration.Background = background;
            configuration.BaseForegroundColor = colors.Foreground;

            this.Configuration = configuration;
        }

        /// <summary>Subclasses answer which colors to paint for a given interaction state. Any may be null (no paint).</summary>
        protected abstract SurfaceColors GetColorsForState (eManagedButtonState state);

        /// <summary>Asks UIKit to repaint the current state. Call when a color source changes.</summary>
        protected void RefreshActiveStateColors ()
        {
            this.SetNeedsUpdateConfiguration();
        }

        // ===========[ Helper Methods ]===================================
        // The padding a bordered system button uses; the corner and border are the button's own properties, which
        //  UpdateConfiguration puts on
        private static UIButtonConfiguration CreateBaseConfiguration ()
        {
            const float kVerticalPadding = 7f;
            const float kHorizontalPadding = 12f;

            UIButtonConfiguration configuration = UIButtonConfiguration.PlainButtonConfiguration;
            configuration.ContentInsets = new NSDirectionalEdgeInsets(kVerticalPadding, kHorizontalPadding, kVerticalPadding, kHorizontalPadding);
            return configuration;
        }

        // ===========[ Subclasses/structs ]===================================
        /// <summary>The colors to paint for one interaction state. Any may be null (no paint).</summary>
        protected readonly record struct SurfaceColors (UIColor Background, UIColor Foreground, UIColor Border);
    }
}
