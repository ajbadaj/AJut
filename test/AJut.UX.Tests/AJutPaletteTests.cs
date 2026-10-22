#nullable enable
namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Keeps AJutPalette and AJutThemeKeys in step with the WinUI3 theme files they copy. The XAML is the source of
    /// truth, so these read it as XML, resolve every key the way WinUI3 would (aliases followed, brush opacity applied),
    /// and check the C# tables give the same answer for every key, in both themes, with nothing missing or extra.
    /// A failure names each key that differs, so the fix is to bring that entry in AJut.UX\Theming up to date.
    /// </summary>
    [TestClass]
    public class AJutPaletteTests
    {
        private const string kAliasingFile = "AJutThemeMapAliasing.xaml";
        private const string kOverridesFile = "AJut.UX_Overrides.xaml";
        private const string kSystemAliasingFile = "SystemColorAliasing.xaml";
        private static readonly string kResourcesFolder = Path.Combine("libs", "AJut.UX.WinUI3", "Resources");
        private static readonly string[] kThemes = ["Light", "Dark"];
        private static readonly XNamespace kXamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly Regex kMarkupReference = new Regex(@"^\{(?:ThemeResource|StaticResource)\s+([A-Za-z0-9_]+)\s*\}$", RegexOptions.Compiled);

        // ===========[ Values ]===================================
        [TestMethod]
        public void Palette_EveryKey_ResolvesAsTheXamlDoes_Light () => AssertEveryKeyResolves("Light");

        [TestMethod]
        public void Palette_EveryKey_ResolvesAsTheXamlDoes_Dark () => AssertEveryKeyResolves("Dark");

        [TestMethod]
        public void Palette_Numbers_MatchTheXaml ()
        {
            var gaps = new List<string>();
            foreach (string theme in kThemes)
            {
                AJutPalette.Table table = AJutPalette.For(theme == "Dark");
                foreach (KeyValuePair<string, XamlEntry> pair in ReadPalette(theme).Where(p => p.Value.Number.HasValue))
                {
                    if (!table.TryGetDouble(pair.Key, out double actual) || (actual != pair.Value.Number!.Value))
                    {
                        gaps.Add($"{theme} {pair.Key}: the XAML says {pair.Value.Number}");
                    }
                }
            }

            AssertNoGaps(gaps, "Palette numbers that differ from the XAML");
        }

        // ===========[ Key sets ]===================================
        [TestMethod]
        public void Palette_Keys_AreExactlyTheXamlKeys ()
        {
            var gaps = new List<string>();
            foreach (string theme in kThemes)
            {
                HashSet<string> expected = ReadPalette(theme).Keys.ToHashSet();
                HashSet<string> actual = AJutPalette.For(theme == "Dark").Keys.ToHashSet();
                gaps.AddRange(expected.Except(actual).OrderBy(k => k).Select(k => $"{theme} is missing {k}"));
                gaps.AddRange(actual.Except(expected).OrderBy(k => k).Select(k => $"{theme} has {k}, which the XAML does not"));
            }

            AssertNoGaps(gaps, "Palette keys out of step with the XAML");
        }

        [TestMethod]
        public void ThemeKeys_All_AreExactlyTheXamlKeys ()
        {
            HashSet<string> expected = ReadKeyMap().Keys.ToHashSet();
            HashSet<string> actual = AJutThemeKeys.All.ToHashSet();
            var gaps = new List<string>();
            gaps.AddRange(expected.Except(actual).OrderBy(k => k).Select(k => $"AJutThemeKeys is missing {k}"));
            gaps.AddRange(actual.Except(expected).OrderBy(k => k).Select(k => $"AJutThemeKeys has {k}, which the XAML does not"));
            AssertNoGaps(gaps, "Control and stock keys out of step with the XAML");
        }

        [TestMethod]
        public void ThemeKeys_Aliases_NameTheXamlTargets ()
        {
            var gaps = new List<string>();
            foreach (KeyValuePair<string, XamlEntry> pair in ReadKeyMap())
            {
                AJutThemeKeys.TryGetAlias(pair.Key, out string? actual);
                if (actual != pair.Value.AliasKey)
                {
                    gaps.Add($"{pair.Key}: the XAML aliases {pair.Value.AliasKey ?? "nothing"}, the table {actual ?? "nothing"}");
                }
            }

            AssertNoGaps(gaps, "Aliases that differ from the XAML");
        }

        [TestMethod]
        public void ThemeKeys_EveryAJutKey_HasAConstant ()
        {
            HashSet<string> constants = typeof(AJutThemeKeys)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && (f.FieldType == typeof(string)))
                .Select(f => (string)f.GetRawConstantValue()!)
                .ToHashSet();

            HashSet<string> expected = ReadKeyMap().Keys.Where(k => k.StartsWith("AJut_", StringComparison.Ordinal)).ToHashSet();
            var gaps = new List<string>();
            gaps.AddRange(expected.Except(constants).OrderBy(k => k).Select(k => $"No constant for {k}"));
            gaps.AddRange(constants.Except(expected).OrderBy(k => k).Select(k => $"A constant for {k}, which the XAML does not define"));
            AssertNoGaps(gaps, "AJutThemeKeys constants out of step with the XAML");
        }

        // ===========[ Lookup behavior ]===================================
        [TestMethod]
        public void TryGetColor_FollowsAControlKeyToTheThemeValue ()
        {
            Assert.IsTrue(AJutPalette.Dark.TryGetColor(AJutThemeKeys.NumericEditor_FocusBorderBrush, out ColorArgb dark));
            Assert.IsTrue(AJutPalette.Dark.TryGetColor("AJut_Color_PrimaryHighlight", out ColorArgb darkHighlight));
            Assert.AreEqual(darkHighlight, dark);

            Assert.IsTrue(AJutPalette.Light.TryGetColor(AJutThemeKeys.NumericEditor_FocusBorderBrush, out ColorArgb light));
            Assert.IsTrue(AJutPalette.Light.TryGetColor("AJut_Color_PrimaryHighlight", out ColorArgb lightHighlight));
            Assert.AreEqual(lightHighlight, light);
        }

        [TestMethod]
        public void TryGetColor_AppliesBrushOpacity ()
        {
            // The XAML gives the drag connector half the highlight's opacity.
            Assert.IsTrue(AJutPalette.Dark.TryGetColor(AJutThemeKeys.FlatTree_DragParentConnector, out ColorArgb connector));
            Assert.IsTrue(AJutPalette.Dark.TryGetColor("AJut_Color_PrimaryHighlight", out ColorArgb highlight));
            Assert.AreEqual(highlight.WithAlpha((byte)Math.Round(highlight.A * 0.5)), connector);
        }

        [TestMethod]
        public void TryGetColor_IsFalseForGradientsUnknownKeysAndNumbers ()
        {
            Assert.IsFalse(AJutPalette.Dark.TryGetColor(AJutThemeKeys.ToggleStripButton_CheckedBackground, out _), "A gradient is not a solid color");
            Assert.IsTrue(AJutPalette.Dark.TryGetPaint(AJutThemeKeys.ToggleStripButton_CheckedBackground, out AJutThemePaint? gradient));
            Assert.IsTrue(gradient!.IsGradient);
            Assert.AreEqual(3, gradient.GradientStops.Count);

            Assert.IsFalse(AJutPalette.Dark.TryGetColor("AJut_NoSuchKey", out _));
            Assert.IsFalse(AJutPalette.Dark.TryGetColor("AJut_Double_StandardDisabledOpacity", out _));
        }

        [TestMethod]
        public void StockKeys_ResolveThroughTheTheme ()
        {
            Assert.IsTrue(AJutPalette.Dark.TryGetColor("ButtonBackground", out ColorArgb button));
            Assert.IsTrue(AJutPalette.Dark.TryGetColor("AJut_Brush_ControlBackground", out ColorArgb control));
            Assert.AreEqual(control, button);
        }

        // ===========[ Helper Methods ]===================================
        private static void AssertEveryKeyResolves (string theme)
        {
            AJutPalette.Table table = AJutPalette.For(theme == "Dark");
            Dictionary<string, XamlEntry> palette = ReadPalette(theme);
            Dictionary<string, XamlEntry> keyMap = ReadKeyMap();

            var gaps = new List<string>();
            foreach (string key in palette.Keys.Concat(keyMap.Keys).Where(k => !(palette.TryGetValue(k, out XamlEntry? e) && e.Number.HasValue)))
            {
                string expected = ResolveFromXaml(key, palette, keyMap);
                string actual = table.TryGetPaint(key, out AJutThemePaint? paint) ? Describe(paint) : "unresolved";
                if (expected != actual)
                {
                    gaps.Add($"{key}: the XAML gives {expected}, the table {actual}");
                }
            }

            AssertNoGaps(gaps, $"{theme} keys that resolve differently from the XAML");
        }

        // Resolves a key the way WinUI3 would, and describes the result the same way Describe does, so the two compare as text.
        private static string ResolveFromXaml (string key, Dictionary<string, XamlEntry> palette, Dictionary<string, XamlEntry> keyMap)
        {
            const int kMaxAliasDepth = 8;
            double opacity = 1.0;
            string current = key;
            for (int depth = 0; depth < kMaxAliasDepth; ++depth)
            {
                if (!palette.TryGetValue(current, out XamlEntry? entry) && !keyMap.TryGetValue(current, out entry))
                {
                    return "unresolved";
                }

                opacity *= entry.Opacity;
                if (entry.AliasKey != null)
                {
                    current = entry.AliasKey;
                    continue;
                }

                if (entry.Color.HasValue)
                {
                    return Hex(ApplyOpacity(entry.Color.Value, opacity));
                }

                if (entry.Stops != null)
                {
                    return DescribeGradient(entry.Stops.Select(s => new AJutGradientStop(s.Offset, ApplyOpacity(s.Color, opacity))));
                }

                return "unresolved";
            }

            return "unresolved";
        }

        private static ColorArgb ApplyOpacity (ColorArgb color, double opacity) => color.WithAlpha((byte)Math.Round(color.A * opacity));

        private static string Describe (AJutThemePaint paint)
        {
            return paint.IsGradient ? DescribeGradient(paint.GradientStops) : Hex(paint.Color);
        }

        private static string DescribeGradient (IEnumerable<AJutGradientStop> stops)
        {
            return "gradient " + string.Join(", ", stops.Select(s => $"{s.Offset.ToString(CultureInfo.InvariantCulture)} {Hex(s.Color)}"));
        }

        private static string Hex (ColorArgb color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

        private static void AssertNoGaps (List<string> gaps, string heading)
        {
            Assert.AreEqual(0, gaps.Count, heading + ":" + Environment.NewLine + string.Join(Environment.NewLine, gaps));
        }

        // ===========[ Reading the XAML ]===================================
        private static Dictionary<string, XamlEntry> ReadPalette (string theme) => ReadEntries($"{theme}ThemeColorsBase.xaml");

        private static Dictionary<string, XamlEntry> ReadKeyMap ()
        {
            var map = new Dictionary<string, XamlEntry>();
            foreach (string file in new[] { kAliasingFile, kOverridesFile, kSystemAliasingFile })
            {
                foreach (KeyValuePair<string, XamlEntry> pair in ReadEntries(file))
                {
                    Assert.IsTrue(map.TryAdd(pair.Key, pair.Value), $"{pair.Key} is defined in more than one aliasing file");
                }
            }

            return map;
        }

        private static Dictionary<string, XamlEntry> ReadEntries (string fileName)
        {
            XDocument doc = XDocument.Load(Path.Combine(ResourcesRoot(), fileName));
            var entries = new Dictionary<string, XamlEntry>();
            foreach (XElement element in doc.Root!.Elements())
            {
                string? key = element.Attribute(kXamlNamespace + "Key")?.Value;
                switch (element.Name.LocalName)
                {
                    case "Color":
                        AddEntry(entries, fileName, key!, XamlEntry.Solid(ParseColor(element.Value)));
                        break;

                    case "SolidColorBrush":
                        double opacity = ParseNumber(element.Attribute("Opacity")?.Value ?? "1");
                        string color = element.Attribute("Color")!.Value;
                        Match reference = kMarkupReference.Match(color);
                        AddEntry(entries, fileName, key!, reference.Success
                            ? XamlEntry.Alias(reference.Groups[1].Value, opacity)
                            : XamlEntry.Solid(ParseColor(color), opacity));
                        break;

                    case "StaticResource":
                        AddEntry(entries, fileName, key!, XamlEntry.Alias(element.Attribute("ResourceKey")!.Value, 1.0));
                        break;

                    case "LinearGradientBrush":
                        Assert.AreEqual("0,0", element.Attribute("StartPoint")?.Value, $"{key} must run top to bottom, the only direction the palette tables carry");
                        Assert.AreEqual("0,1", element.Attribute("EndPoint")?.Value, $"{key} must run top to bottom, the only direction the palette tables carry");
                        AJutGradientStop[] stops = element.Elements()
                            .Select(stop => new AJutGradientStop(ParseNumber(stop.Attribute("Offset")!.Value), ParseColor(stop.Attribute("Color")!.Value)))
                            .ToArray();
                        AddEntry(entries, fileName, key!, XamlEntry.Gradient(stops));
                        break;

                    case "Double":
                        AddEntry(entries, fileName, key!, XamlEntry.Double(ParseNumber(element.Value)));
                        break;

                    case "Style":
                        // A themed style's setters become keys named for the control and the property they set.
                        string target = element.Attribute("TargetType")!.Value.Split(':').Last();
                        foreach (XElement setter in element.Elements())
                        {
                            Match setterReference = kMarkupReference.Match(setter.Attribute("Value")!.Value);
                            Assert.IsTrue(setterReference.Success, $"A {target} setter in {fileName} does not reference a key");
                            AddEntry(entries, fileName, $"AJut_{target}_{setter.Attribute("Property")!.Value}", XamlEntry.Alias(setterReference.Groups[1].Value, 1.0));
                        }

                        break;

                    case "CornerRadius":
                        // Not a color; the palette tables carry colors and numbers only.
                        break;

                    default:
                        Assert.Fail($"{fileName} has a <{element.Name.LocalName}>, which the palette tables do not carry yet");
                        break;
                }
            }

            return entries;
        }

        // A key defined twice in one file is a mistake in the XAML; say which, rather than failing on the dictionary.
        private static void AddEntry (Dictionary<string, XamlEntry> entries, string fileName, string key, XamlEntry entry)
        {
            if (!entries.TryAdd(key, entry))
            {
                Assert.Fail($"{key} is defined more than once in {fileName}");
            }
        }

        private static ColorArgb ParseColor (string text)
        {
            text = text.Trim();
            if (text == "Transparent")
            {
                return new ColorArgb(0x00, 0xFF, 0xFF, 0xFF);
            }

            Assert.IsTrue(AJutColorHelper.TryGetColorFromHex(text, out byte[] argb), $"Could not read the color '{text}'");
            return new ColorArgb(argb[0], argb[1], argb[2], argb[3]);
        }

        private static double ParseNumber (string text) => double.Parse(text, CultureInfo.InvariantCulture);

        private static string ResourcesRoot ()
        {
            // The test runs from its build output somewhere under the repo, so walk up until the resources folder appears.
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, kResourcesFolder);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException($"Could not find {kResourcesFolder} above {AppContext.BaseDirectory}");
        }

        // ===========[ Subclasses/structs ]===================================
        // One key's definition as the XAML states it, read independently of AJut.UX's own table types.
        private sealed class XamlEntry
        {
            private XamlEntry (ColorArgb? color, AJutGradientStop[]? stops, string? aliasKey, double opacity, double? number)
            {
                this.Color = color;
                this.Stops = stops;
                this.AliasKey = aliasKey;
                this.Opacity = opacity;
                this.Number = number;
            }

            public ColorArgb? Color { get; }

            public AJutGradientStop[]? Stops { get; }

            public string? AliasKey { get; }

            public double Opacity { get; }

            public double? Number { get; }

            public static XamlEntry Solid (ColorArgb color, double opacity = 1.0) => new XamlEntry(color, null, null, opacity, null);

            public static XamlEntry Gradient (AJutGradientStop[] stops) => new XamlEntry(null, stops, null, 1.0, null);

            public static XamlEntry Alias (string key, double opacity) => new XamlEntry(null, null, key, opacity, null);

            public static XamlEntry Double (double value) => new XamlEntry(null, null, null, 1.0, value);
        }
    }
}
