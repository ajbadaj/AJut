namespace AJutShowRoomAppKit.UI
{
    using AJut.UX.Theming;
    using AppKit;

    // The showroom's own color keys and text styles. The keys go through ThemedColors like any
    // AJut control's, so the showroom chrome itself shows the themed and unthemed modes too.
    public static class ShowroomStyle
    {
        public const string PageBackground = "Showroom_PageBackground";
        public const string SidebarBackground = "Showroom_SidebarBackground";
        public const string CodeBackground = "Showroom_CodeBackground";
        public const string Divider = "Showroom_Divider";
        public const string HeaderText = "Showroom_HeaderText";
        public const string BodyText = "Showroom_BodyText";
        public const string UnderstatedText = "Showroom_UnderstatedText";

        private static bool g_isRegistered;

        // ===========[ Public Interface Methods ]==============================
        public static void EnsureRegistered ()
        {
            if (g_isRegistered)
            {
                return;
            }

            g_isRegistered = true;
            Register(PageBackground, NSColor.WindowBackground, "AJut_Brush_RootPanelBackground");
            Register(SidebarBackground, NSColor.UnderPageBackground, "AJut_Brush_PanelBackground");
            Register(CodeBackground, NSColor.TextBackground, "AJut_Brush_InnerPanelBackground");
            Register(Divider, NSColor.Separator, "AJut_Brush_BorderCommon");
            Register(HeaderText, NSColor.Label, "AJut_Brush_ImportantText");
            Register(BodyText, NSColor.Label, "AJut_Brush_NormalText");
            Register(UnderstatedText, NSColor.SecondaryLabel, "AJut_Brush_UnderstatedText");
        }

        public static NSTextField Title (string text) => MakeLabel(text, NSFont.BoldSystemFontOfSize((nfloat)20), HeaderText, wraps: false);
        public static NSTextField Heading (string text) => MakeLabel(text, NSFont.BoldSystemFontOfSize((nfloat)14), HeaderText, wraps: false);
        public static NSTextField Body (string text) => MakeLabel(text, NSFont.SystemFontOfSize((nfloat)13), BodyText, wraps: true);
        public static NSTextField Understated (string text) => MakeLabel(text, NSFont.SystemFontOfSize((nfloat)12), UnderstatedText, wraps: true);

        /// <summary>A one point line in the divider color, for separating columns</summary>
        public static ThemedView VerticalDivider ()
        {
            var divider = new ThemedView { BackgroundColorKey = Divider };
            divider.TranslatesAutoresizingMaskIntoConstraints = false;
            divider.WidthAnchor.ConstraintEqualTo((nfloat)1).Active = true;
            return divider;
        }

        /// <summary>A one point line in the divider color, for separating rows</summary>
        public static ThemedView HorizontalDivider ()
        {
            var divider = new ThemedView { BackgroundColorKey = Divider };
            divider.TranslatesAutoresizingMaskIntoConstraints = false;
            divider.HeightAnchor.ConstraintEqualTo((nfloat)1).Active = true;
            return divider;
        }

        // ===========[ Helper Methods ]========================================
        private static void Register (string key, NSColor unthemedDefault, string paletteKey)
        {
            ThemedColors.SetUnthemedDefault(key, unthemedDefault);
            ThemedColors.SetThemedAlias(key, paletteKey);
        }

        private static NSTextField MakeLabel (string text, NSFont font, string colorKey, bool wraps)
        {
            EnsureRegistered();
            NSTextField label = wraps ? NSTextField.CreateWrappingLabel(text) : NSTextField.CreateLabel(text);
            label.Font = font;
            label.TextColor = ThemedColors.Get(colorKey);
            if (wraps)
            {
                // A wrapping label in Auto Layout needs to give way horizontally, or it insists on
                // its single-line width and pushes its container wider
                label.SetContentCompressionResistancePriority(250, NSLayoutConstraintOrientation.Horizontal);
            }

            return label;
        }
    }
}
