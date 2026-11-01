namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Content.Res;
    using Android.Runtime;
    using Android.Util;
    using AndroidColor = Android.Graphics.Color;

    /// <summary>
    /// A button you color with a single accent. Hand it one PropagatedAccentColor and it derives a coherent,
    /// readable palette - a foreground that contrasts the accent, a visible border, and the pointer over, pressed
    /// and disabled variants - with no per state colors. The derivation is <see cref="InteractiveSurfaceColors.BuildFromAccent"/>'s,
    /// shared with the other AJut stacks, through <see cref="MobileLook.AutoBrushButton.BuildFromAccent"/>, which UIKit's
    /// AutoBrushButton takes too: it pushes the pressed border further off the fill, since a press is the only highlight
    /// touch has.
    /// </summary>
    /// <remarks>
    /// With no accent set it uses the AJut accent (SystemAccentColor) when the AJut theme is loaded, as WinUI3's does.
    /// Without the theme it uses the system accent (the wallpaper-derived accent on Android 12 and later), then the
    /// Android theme's colorAccent, so it shows something coherent before a consumer picks a color. An accent set in
    /// code, or by the ajutPropagatedAccentColor layout attribute, wins over both.
    /// </remarks>
    [Register("ajut.ux.controls.AutoBrushButton")]
    public class AutoBrushButton : _ManagedBrushButton
    {
        private const uint kFallbackAccent = 0xFF0D83D8;
        private const string kThemedAccentKey = "SystemAccentColor";

        private AndroidColor m_propagatedAccentColor;
        private bool m_isAccentSet;
        private InteractiveSurfaceColors m_palette;

        // ===========[ Setup/Construction/Teardown ]===================================
        public AutoBrushButton (Context context) : base(context)
        {
            this.Initialize(context, null);
        }

        public AutoBrushButton (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            this.Initialize(context, attrs);
        }

        public AutoBrushButton (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.Initialize(context, attrs);
        }

        protected AutoBrushButton (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        // ===========[ Properties ]===================================
        /// <summary>
        /// The one color every state's colors are derived from. Until it is set, the AJut accent with the AJut theme
        /// loaded, and the system accent without it.
        /// </summary>
        public AndroidColor PropagatedAccentColor
        {
            get => m_propagatedAccentColor;
            set
            {
                m_propagatedAccentColor = value;
                m_isAccentSet = true;
                this.RebuildPalette();
                this.RefreshActiveStateColors();
            }
        }

        // ===========[ Public Interface Methods ]===================================
        protected override SurfaceStateColors GetColorsForState (eManagedButtonState state)
        {
            return state switch
            {
                eManagedButtonState.PointerOver => m_palette.PointerOver,
                eManagedButtonState.Pressed => m_palette.Pressed,
                eManagedButtonState.Disabled => m_palette.Disabled,
                _ => m_palette.Normal,
            };
        }

        // The default accent depends on night mode (both the AJut accent and the system palette have a light and dark
        // tone), so an accent nobody set is looked up again.
        protected override void OnThemeMayHaveChanged ()
        {
            if (!m_isAccentSet)
            {
                m_propagatedAccentColor = _FindDefaultAccent(this.Context!);
                this.RebuildPalette();
            }
        }

        // ===========[ Helper Methods ]===================================
        private void Initialize (Context context, IAttributeSet? attrs)
        {
            m_propagatedAccentColor = _FindDefaultAccent(context);
            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutAutoBrushButton);
                if (values.HasValue(Resource.Styleable.AJutAutoBrushButton_ajutPropagatedAccentColor))
                {
                    m_propagatedAccentColor = new AndroidColor(values.GetColor(Resource.Styleable.AJutAutoBrushButton_ajutPropagatedAccentColor, 0));
                    m_isAccentSet = true;
                }

                values.Recycle();
            }

            this.RebuildPalette();
            this.CompleteInitialization();
        }

        private void RebuildPalette ()
        {
            m_palette = MobileLook.AutoBrushButton.BuildFromAccent(m_propagatedAccentColor.ToColorArgb());
        }

        private static AndroidColor _FindDefaultAccent (Context context)
        {
            if (ThemeColors.IsThemeLoaded
                && AJutPalette.For(ThemeColors.IsDark(context), ThemeColors.IsTouchFocused).TryGetColor(kThemedAccentKey, out ColorArgb themed))
            {
                return themed.ToAndroidColor();
            }

            return _FindSystemAccent(context);
        }

        private static AndroidColor _FindSystemAccent (Context context)
        {
            // Android 12 added the system palette generated from the wallpaper. Its accent1 500 tone is the
            // closest thing Android has to the Windows system accent color.
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                return new AndroidColor(context.Resources!.GetColor(Android.Resource.Color.SystemAccent1500, context.Theme));
            }

            var resolved = new TypedValue();
            if ((context.Theme?.ResolveAttribute(Android.Resource.Attribute.ColorAccent, resolved, true) ?? false)
                && (resolved.Type >= DataType.FirstColorInt)
                && (resolved.Type <= DataType.LastColorInt))
            {
                return new AndroidColor(resolved.Data);
            }

            return new AndroidColor(unchecked((int)kFallbackAccent));
        }
    }
}
