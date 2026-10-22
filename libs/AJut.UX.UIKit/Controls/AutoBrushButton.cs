namespace AJut.UX.Controls
{
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using ObjCRuntime;
    using UIKit;

    /// <summary>
    /// A button you color with a single accent. Hand it one PropagatedAccentColor and it derives a
    /// coherent, readable palette - a foreground that contrasts the accent, a visible border, and the
    /// pointer over / pressed / disabled variants - with no per state colors. The derivation lives in
    /// <see cref="InteractiveSurfaceColors.BuildFromAccent"/>.
    /// </summary>
    public class AutoBrushButton : _ManagedBrushButton
    {
        // ===========[ Instance Fields ]===================================
        private UIColor m_propagatedAccentColor;
        private SurfaceColors m_normal;
        private SurfaceColors m_pointerOver;
        private SurfaceColors m_pressed;
        private SurfaceColors m_disabled;

        // ===========[ Setup/Construction/Teardown ]===================================
        public AutoBrushButton ()
        {
            this.RebuildPalette();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native button.</summary>
        protected AutoBrushButton (NativeHandle handle) : base(handle)
        {
            this.RebuildPalette();
        }

        // ===========[ Properties ]===================================
        /// <summary>
        /// The accent the whole palette is derived from. A dynamic color is derived twice, once for light and
        /// once for dark, so the button follows an appearance switch. Null (the default) uses the AJut accent
        /// (SystemAccentColor) when the theme is loaded, and otherwise the view's TintColor, the way UIKit's own buttons
        /// pick up the app's accent.
        /// </summary>
        public UIColor PropagatedAccentColor
        {
            get => m_propagatedAccentColor;
            set
            {
                m_propagatedAccentColor = value;
                this.RebuildPalette();
                this.RefreshActiveStateColors();
            }
        }

        // ===========[ Public Interface Methods ]===================================
        public override void TintColorDidChange ()
        {
            base.TintColorDidChange();
            if (m_propagatedAccentColor == null)
            {
                this.RebuildPalette();
                this.RefreshActiveStateColors();
            }
        }

        protected override SurfaceColors GetColorsForState (eManagedButtonState state)
        {
            return state switch
            {
                eManagedButtonState.PointerOver => m_pointerOver,
                eManagedButtonState.Pressed => m_pressed,
                eManagedButtonState.Disabled => m_disabled,
                _ => m_normal,
            };
        }

        // ===========[ Helper Methods ]===================================
        private void RebuildPalette ()
        {
            UIColor accent = m_propagatedAccentColor
                ?? ThemedColors.Get(ThemedColorKeys.SystemAccentColor)
                ?? this.TintColor
                ?? UIColor.SystemBlue;

            // The derivation is byte math on one fixed color, so a dynamic accent is resolved for each
            // appearance and derived separately, then the pairs are joined back into dynamic colors.
            InteractiveSurfaceColors light = InteractiveSurfaceColors.BuildFromAccent(accent.ToColorArgb(UIUserInterfaceStyle.Light));
            InteractiveSurfaceColors dark = InteractiveSurfaceColors.BuildFromAccent(accent.ToColorArgb(UIUserInterfaceStyle.Dark));

            m_normal = _Join(light.Normal, dark.Normal);
            m_pointerOver = _Join(light.PointerOver, dark.PointerOver);
            m_pressed = _Join(light.Pressed, dark.Pressed);
            m_disabled = _Join(light.Disabled, dark.Disabled);

            static SurfaceColors _Join (SurfaceStateColors lightState, SurfaceStateColors darkState)
            {
                return new SurfaceColors(
                    UIColorXT.FromLightDark(lightState.Background.ToUIColor(), darkState.Background.ToUIColor()),
                    UIColorXT.FromLightDark(lightState.Foreground.ToUIColor(), darkState.Foreground.ToUIColor()),
                    UIColorXT.FromLightDark(lightState.Border.ToUIColor(), darkState.Border.ToUIColor())
                );
            }
        }
    }
}
