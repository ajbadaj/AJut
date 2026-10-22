namespace AJut.UX.Theming
{
    using System;
    using System.Collections.Generic;
    using AJut;
    using AJut.UX.Helpers;
    using AppKit;

    // ===========[ ThemedColors ]===============================================
    // The AppKit side of AJut's "sane unthemed, AJut palette when themed" rule.
    //
    // WinUI3 does this with three resource layers: per-control literal defaults, an aliasing file
    // that points each control key at a palette key, and the palette itself (Light and Dark theme
    // dictionaries). AppKit has no resource system, so the layers are tables:
    //  1. Unthemed defaults: control key -> NSColor, usually a macOS system color, so a control
    //     with no theme loaded looks like any other Mac control. These are AppKit's own, see
    //     ThemedColorKeys.
    //  2. Themed: AJut.UX's palette tables (AJutPalette.Light / AJutPalette.Dark), the same keys
    //     and values the WinUI3 theme dictionaries carry. A table takes a control key directly
    //     and follows the shared aliases to the palette entry itself, so the alias layer is not
    //     repeated here. SetThemedAlias exists only to point a key somewhere else: a key only
    //     AppKit has, or a consumer restyling one.
    //
    // Get(key) hands back a dynamic NSColor. A dynamic NSColor (colorWithName:dynamicProvider:,
    // macOS 10.15+) is not a fixed value: AppKit calls its provider with the appearance it is
    // drawing under every time the color is used, so one NSColor follows light and dark on its
    // own, per view, including a view whose window overrides the app appearance. The provider
    // decides each time whether a theme is loaded and which palette table matches the
    // appearance, which means loading or unloading a theme needs nothing more than a redraw.
    //
    // The one place this does not happen by itself is a CALayer: layer.BackgroundColor takes a
    // CGColor, and asking a dynamic NSColor for its CGColor resolves it once, under whatever the
    // current drawing appearance is at that moment, and never again. So layer colors must be
    // pushed from inside NSView.UpdateLayer (AppKit sets the drawing appearance to the view's
    // effective appearance around that call), and the view must redisplay when its appearance
    // changes. ThemedView does both, which is why AJut's AppKit controls derive from it.
    public static class ThemedColors
    {
        private static readonly object g_lock = new object();
        private static readonly Dictionary<string, NSColor> g_unthemedDefaults = new Dictionary<string, NSColor>();
        private static readonly Dictionary<string, string> g_themedAliases = new Dictionary<string, string>();
        private static readonly Dictionary<string, NSColor> g_dynamicColors = new Dictionary<string, NSColor>();
        private static readonly Dictionary<string, NSColor> g_lightPaletteCache = new Dictionary<string, NSColor>();
        private static readonly Dictionary<string, NSColor> g_darkPaletteCache = new Dictionary<string, NSColor>();
        private static readonly Dictionary<string, NSGradient> g_lightGradientCache = new Dictionary<string, NSGradient>();
        private static readonly Dictionary<string, NSGradient> g_darkGradientCache = new Dictionary<string, NSGradient>();
        private static readonly HashSet<string> g_reportedMissingKeys = new HashSet<string>();
        private static bool g_isThemeLoaded;

        // ===========[ Setup/Construction/Teardown ]===========================
        static ThemedColors ()
        {
            ThemedColorKeys.RegisterAll();
        }

        // ===========[ Events ]================================================
        /// <summary>
        /// Raised after <see cref="IsThemeLoaded"/> changes and every window has been asked to redraw. Only
        /// needed for colors pushed somewhere AppKit does not redraw on its own; anything drawn through
        /// <see cref="Get"/> or a <see cref="ThemedView"/> is already handled.
        /// </summary>
        public static event EventHandler<EventArgs> ThemeLoadedChanged;

        // ===========[ Properties ]============================================
        /// <summary>
        /// True when the AJut palette is in effect. False means every control shows its unthemed default,
        /// which on AppKit is the matching macOS system color.
        /// </summary>
        public static bool IsThemeLoaded => g_isThemeLoaded;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Puts the AJut palette in effect and redraws every window</summary>
        public static void LoadTheme ()
        {
            SetThemeLoaded(true);
        }

        /// <summary>Goes back to the unthemed defaults (macOS system colors) and redraws every window</summary>
        public static void UnloadTheme ()
        {
            SetThemeLoaded(false);
        }

        /// <summary>
        /// Gets the dynamic color for a key. The key is either a control key (registered with
        /// <see cref="SetUnthemedDefault"/> and usually <see cref="SetThemedAlias"/>) or an AJut palette key
        /// directly. The same NSColor instance comes back for the same key every time, and it resolves
        /// against the theme state and the drawing appearance whenever it is used.
        /// </summary>
        public static NSColor Get (string key)
        {
            lock (g_lock)
            {
                if (!g_dynamicColors.TryGetValue(key, out NSColor color))
                {
                    color = new DynamicColorSource(key).Create();
                    g_dynamicColors.Add(key, color);
                }

                return color;
            }
        }

        /// <summary>
        /// Sets the color a key shows when no theme is loaded. AJut registers one for every key its
        /// controls use; call this to restyle a control in unthemed mode, or to add a key of your own.
        /// </summary>
        public static void SetUnthemedDefault (string key, NSColor color)
        {
            lock (g_lock)
            {
                g_unthemedDefaults[key] = color;
            }
        }

        /// <summary>
        /// Points a key at a different palette key for themed mode, ahead of the shared aliases AJut.UX's
        /// palette tables already follow. Use it for a key of your own, or to restyle one of AJut's. A key
        /// the palette tables do not know, with no alias here, falls back to its unthemed default. Pass
        /// null to remove an alias.
        /// </summary>
        public static void SetThemedAlias (string key, string paletteKey)
        {
            lock (g_lock)
            {
                if (paletteKey == null)
                {
                    g_themedAliases.Remove(key);
                }
                else
                {
                    g_themedAliases[key] = paletteKey;
                }

                // Gradients are cached by key rather than by palette key, so a moved alias drops them
                g_lightGradientCache.Remove(key);
                g_darkGradientCache.Remove(key);
            }
        }

        /// <summary>
        /// Gets the gradient a key resolves to under <paramref name="appearance"/>, when a theme is loaded and the
        /// key's palette entry is a gradient (a few AJut keys are, such as ToggleStrip's checked backgrounds).
        /// Returns null otherwise: unthemed, or a solid key, draws with <see cref="Get"/> instead. Palette gradients
        /// run top to bottom, so draw one at 90 degrees in a flipped view (which every AJut AppKit control is) to
        /// put its first stop at the top.
        /// </summary>
        public static NSGradient GetThemedGradient (string key, NSAppearance appearance)
        {
            lock (g_lock)
            {
                if (!g_isThemeLoaded)
                {
                    return null;
                }

                bool isDark = IsDark(appearance);
                Dictionary<string, NSGradient> cache = isDark ? g_darkGradientCache : g_lightGradientCache;
                if (cache.TryGetValue(key, out NSGradient gradient))
                {
                    return gradient;
                }

                string paletteKey = g_themedAliases.TryGetValue(key, out string alias) ? alias : key;
                if (AJutPalette.For(isDark).TryGetPaint(paletteKey, out AJutThemePaint paint) && paint.IsGradient)
                {
                    var colors = new NSColor[paint.GradientStops.Count];
                    var locations = new double[paint.GradientStops.Count];
                    for (int i = 0; i < colors.Length; ++i)
                    {
                        colors[i] = paint.GradientStops[i].Color.ToNSColor();
                        locations[i] = paint.GradientStops[i].Offset;
                    }

                    gradient = new NSGradient(colors, locations);
                }

                // A miss is cached too (as null), so a solid key is not looked up again on every draw
                cache[key] = gradient;
                return gradient;
            }
        }

        /// <summary>
        /// Is the given appearance a dark one. Covers darkAqua, vibrantDark and the high contrast dark
        /// variants, which all carry "Dark" in their name; Apple's bestMatchFromAppearancesWithNames is
        /// the formal check, and gives the same answer for the system appearances.
        /// </summary>
        public static bool IsDark (NSAppearance appearance)
        {
            string name = appearance?.Name;
            return name != null && name.Contains("Dark", StringComparison.Ordinal);
        }

        /// <summary>
        /// Asks every window's whole view tree, title bar included, to redisplay, so dynamic colors and
        /// <see cref="ThemedView"/> layer colors resolve again. Theme loads and unloads call this already.
        /// </summary>
        public static void RedrawAllWindows ()
        {
            // DangerousWindows is the .NET bindings' name for NSApplication's windows array
            foreach (NSWindow window in NSApplication.SharedApplication.DangerousWindows)
            {
                // The content view's superview is the window's frame view, which draws the title bar
                // and the window background
                NSView root = window.ContentView?.Superview ?? window.ContentView;
                if (root != null)
                {
                    MarkTreeForRedisplay(root);
                }
            }
        }

        // ===========[ Helper Methods ]========================================
        private static void SetThemeLoaded (bool isLoaded)
        {
            if (g_isThemeLoaded == isLoaded)
            {
                return;
            }

            g_isThemeLoaded = isLoaded;
            RedrawAllWindows();
            ThemeLoadedChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void MarkTreeForRedisplay (NSView view)
        {
            view.NeedsDisplay = true;
            foreach (NSView child in view.Subviews)
            {
                MarkTreeForRedisplay(child);
            }
        }

        private static NSColor Resolve (string key, NSAppearance appearance)
        {
            lock (g_lock)
            {
                // 1. Themed: alias first, then the key itself as a palette key
                if (g_isThemeLoaded)
                {
                    string paletteKey = g_themedAliases.TryGetValue(key, out string alias) ? alias : key;
                    if (TryGetPaletteColor(paletteKey, IsDark(appearance), out NSColor themed))
                    {
                        return themed;
                    }
                }

                // 2. Unthemed default. A system color returned here is itself dynamic, and resolves
                //    under the same drawing appearance this provider is being asked about.
                if (g_unthemedDefaults.TryGetValue(key, out NSColor unthemed))
                {
                    return unthemed;
                }

                // 3. Nothing registered. Say so once per key rather than on every draw.
                if (g_reportedMissingKeys.Add(key))
                {
                    Logger.LogError($"[ThemedColors] No unthemed default or palette entry for '{key}', drawing it clear");
                }

                return NSColor.Clear;
            }
        }

        private static bool TryGetPaletteColor (string paletteKey, bool isDark, out NSColor color)
        {
            Dictionary<string, NSColor> cache = isDark ? g_darkPaletteCache : g_lightPaletteCache;
            if (cache.TryGetValue(paletteKey, out color))
            {
                return true;
            }

            // A gradient-only key answers false here and falls back to the unthemed default; the
            // controls that want a gradient ask the table for its paint themselves
            AJutPalette.Table table = AJutPalette.For(isDark);
            if (!table.TryGetColor(paletteKey, out ColorArgb paletteColor))
            {
                color = null;
                return false;
            }

            color = paletteColor.ToNSColor();
            cache.Add(paletteKey, color);
            return true;
        }

        // ===========[ Subclasses/structs ]====================================
        // Holds the key for the provider through a named instance method rather than a lambda
        // capture. The NSColor keeps the delegate, and the ThemedColors cache keeps the NSColor for
        // the life of the app, so this is one small object per key, created once.
        private sealed class DynamicColorSource
        {
            private readonly string m_key;

            public DynamicColorSource (string key)
            {
                m_key = key;
            }

            public NSColor Create () => NSColor.GetColor(m_key, this.Provide);

            private NSColor Provide (NSAppearance appearance) => Resolve(m_key, appearance);
        }
    }
}
