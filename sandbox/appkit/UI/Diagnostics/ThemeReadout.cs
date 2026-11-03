namespace AJutShowRoomAppKit.UI
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using AJut.UX;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using Foundation;

    // A plain text account of how a window's themed surfaces are painting, for pasting into a bug report: the theme
    // state, the OS and its accent, then each themed view's key, the color ThemedColors resolves for it under that view's
    // appearance, and the color its layer actually holds. Views that come out the same are counted once. Faded views are
    // listed with their opacity, which is how a dimmed tab label shows up.
    //
    // When the resolved color and the layer color disagree, the layer was given a color once and not again; when the
    // resolved color is a macOS system color rather than the palette's, the palette lookup missed.
    public static class ThemeReadout
    {
        // ===========[ Public Interface Methods ]==============================
        /// <summary>Builds the readout for <paramref name="window"/> and puts it on the clipboard as plain text</summary>
        public static void CopyFor (NSWindow window)
        {
            string text = Build(window);
            NSPasteboard pasteboard = NSPasteboard.GeneralPasteboard;
            pasteboard.ClearContents();
            pasteboard.SetStringForType(text, NSPasteboardType.String.GetConstant());
        }

        // ===========[ Helper Methods ]========================================
        private static string Build (NSWindow window)
        {
            var text = new StringBuilder();
            text.AppendLine("AJut AppKit theme readout");
            text.AppendLine($"macOS: {NSProcessInfo.ProcessInfo.OperatingSystemVersionString}");
            text.AppendLine($"Theme loaded: {ThemedColors.IsThemeLoaded}, touch variant: {ThemedColors.UsesTouchVariant}");
            text.AppendLine($"App appearance: {NSApplication.SharedApplication.Appearance?.Name ?? "(follows the OS)"}");
            if (window == null)
            {
                text.AppendLine("No window");
                return text.ToString();
            }

            NSAppearance appearance = window.EffectiveAppearance;
            text.AppendLine($"Window appearance: {appearance?.Name}, key window: {window.IsKeyWindow}");
            text.AppendLine($"Accent (ControlAccent): {Resolve(NSColor.ControlAccent, appearance)}");
            text.AppendLine($"Window background: {Resolve(window.BackgroundColor, appearance)}");
            text.AppendLine($"First responder: {window.FirstResponder?.GetType().Name ?? "(none)"}");
            text.AppendLine();
            text.AppendLine("view | key | resolved | layer | count");

            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            NSView root = window.ContentView?.Superview ?? window.ContentView;
            if (root != null)
            {
                Walk(root, counts, order);
            }

            foreach (string line in order)
            {
                text.AppendLine($"{line} | {counts[line]}");
            }

            return text.ToString();
        }

        private static void Walk (NSView view, Dictionary<string, int> counts, List<string> order)
        {
            string line = Describe(view);
            if (line != null)
            {
                if (counts.TryGetValue(line, out int count))
                {
                    counts[line] = count + 1;
                }
                else
                {
                    counts.Add(line, 1);
                    order.Add(line);
                }
            }

            foreach (NSView child in view.Subviews)
            {
                Walk(child, counts, order);
            }
        }

        // One line for a themed view with a background key, or a view drawn faded; null for anything else
        private static string Describe (NSView view)
        {
            string name = view.GetType().Name;
            string layerColor = DescribeLayer(view);
            if ((view is ThemedView themed) && (themed.BackgroundColorKey != null))
            {
                string resolved = Resolve(ThemedColors.Get(themed.BackgroundColorKey), view.EffectiveAppearance);
                return $"{name} | {themed.BackgroundColorKey} | {resolved} | {layerColor}";
            }

            if (view.AlphaValue < (nfloat)1.0)
            {
                string label = (view is NSTextField field) ? $" \"{field.StringValue}\"" : String.Empty;
                return $"{name}{label} | alpha {view.AlphaValue:0.##} | - | {layerColor}";
            }

            return null;
        }

        private static string DescribeLayer (NSView view)
        {
            CGColor color = view.Layer?.BackgroundColor;
            if (color == null)
            {
                return view.WantsLayer ? "clear" : "no layer";
            }

            NSColor ns = NSColor.FromCGColor(color);
            return ((ns != null) && ns.TryToColorArgb(out ColorArgb argb)) ? Hex(argb) : "?";
        }

        // A dynamic color resolves under the current drawing appearance, so it is read with the view's appearance current
        private static string Resolve (NSColor color, NSAppearance appearance)
        {
            if (color == null)
            {
                return "(none)";
            }

            string result = "?";
            if (appearance == null)
            {
                return color.TryToColorArgb(out ColorArgb plain) ? Hex(plain) : result;
            }

            appearance.PerformAsCurrentDrawingAppearance(ReadUnderAppearance);
            return result;

            void ReadUnderAppearance ()
            {
                result = color.TryToColorArgb(out ColorArgb argb) ? Hex(argb) : "?";
            }
        }

        private static string Hex (ColorArgb argb) => (argb.A == 0xFF) ? $"#{argb.R:X2}{argb.G:X2}{argb.B:X2}" : $"#{argb.A:X2}{argb.R:X2}{argb.G:X2}{argb.B:X2}";
    }
}
