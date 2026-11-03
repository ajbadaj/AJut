#nullable enable

namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Keeps the colors of AJut.UX.Android's Theme.AJut style in step with the shared palette. The style is XML that
    /// Android reads, so its colors are copies of palette values; this reads both halves as files (the light half under
    /// values, the dark half under values-night) and fails on any copy that is not the palette's value for its key.
    /// The copies are of the plain palettes, since the touch variant is off by default (ThemeColors.UsesTouchVariant), as
    /// on every stack; StockControlTint follows a switch to touch as the app runs.
    /// </summary>
    [TestClass]
    public class AndroidAJutThemeTests
    {
        private static readonly string kAndroidResourcesFolder = Path.Combine("libs", "AJut.UX.Android", "Resources");
        private const string kThemeFileName = "ajut_theme.xml";

        // Every color the style takes, the palette key it copies, and the opacity it is laid down at
        private static readonly MirroredColor[] kMirrored =
        [
            new MirroredColor("ajut_accent", MobileLook.StockControls.Accent, 1f),
            new MirroredColor("ajut_control_normal", MobileLook.StockControls.CheckBoxUnchecked, 1f),
            new MirroredColor("ajut_text_selection", MobileLook.StockControls.TextSelection, MobileLook.StockControls.TextSelectionOpacity),
        ];

        [TestMethod]
        public void AndroidAJutTheme_EveryColor_IsThePalettesValue ()
        {
            var gaps = new List<string>();
            foreach (ThemeHalf half in new[] { new ThemeHalf("values", AJutPalette.Light), new ThemeHalf("values-night", AJutPalette.Dark) })
            {
                string file = Path.Combine(ResourcesRoot(), half.Folder, kThemeFileName);
                Dictionary<string, string> colors = XDocument.Load(file).Root!
                    .Elements("color")
                    .ToDictionary(element => (string)element.Attribute("name")!, element => element.Value.Trim());

                foreach (string unknown in colors.Keys.Where(name => kMirrored.All(mirrored => mirrored.Name != name)))
                {
                    gaps.Add($"{half.Folder}/{kThemeFileName} {unknown}: a color this test does not know the palette key of");
                }

                foreach (MirroredColor mirrored in kMirrored)
                {
                    if (!half.Palette.TryGetColor(mirrored.Key, out ColorArgb paletteColor))
                    {
                        gaps.Add($"{half.Folder} {mirrored.Name} ({mirrored.Key}): the palette has no solid color for the key");
                        continue;
                    }

                    var expected = new ColorArgb((byte)(paletteColor.A * mirrored.Opacity), paletteColor.R, paletteColor.G, paletteColor.B);
                    string expectedText = $"#{expected.A:X2}{expected.R:X2}{expected.G:X2}{expected.B:X2}";
                    if (!colors.TryGetValue(mirrored.Name, out string? actual))
                    {
                        gaps.Add($"{half.Folder} {mirrored.Name} ({mirrored.Key}): missing from the XML, the palette gives {expectedText}");
                    }
                    else if (!string.Equals(actual, expectedText, StringComparison.OrdinalIgnoreCase))
                    {
                        gaps.Add($"{half.Folder} {mirrored.Name} ({mirrored.Key}): the XML gives {actual}, the palette {expectedText}");
                    }
                }
            }

            Assert.AreEqual(0, gaps.Count, "Theme.AJut colors out of step with the shared palette:" + Environment.NewLine + string.Join(Environment.NewLine, gaps));
        }

        // ===========[ Helper Methods ]===================================
        // The test runs from its build output somewhere under the repo, so walk up until the resources folder appears
        private static string ResourcesRoot ()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, kAndroidResourcesFolder);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException($"Could not find {kAndroidResourcesFolder} above {AppContext.BaseDirectory}");
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed record MirroredColor (string Name, string Key, float Opacity);

        private sealed record ThemeHalf (string Folder, AJutPalette.Table Palette);
    }
}
