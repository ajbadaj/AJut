namespace AJut.UX.WinUI3.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Reads the AJut.UX.WinUI3 palette files as XML and checks that every AJut_* key they reference is defined in each
    /// theme. WinUI only builds a resource entry on its first lookup, so a reference to a key that does not exist loads
    /// without complaint and only fails when something looks it up, which is why this is checked from the files rather
    /// than from a running app. The test project cannot create XAML objects (no UI thread), so XML is the only headless
    /// route to the palettes.
    /// </summary>
    [TestClass]
    public class WinUI3ThemeKeyReferenceTests
    {
        private const string kResourcesFolder = @"libs\AJut.UX.WinUI3\Resources";
        private const string kAliasingFile = "SystemColorAliasing.xaml";
        private const string kKeyPrefix = "AJut_";
        private static readonly string[] kThemes = ["Dark", "Light"];
        private static readonly Regex kMarkupReference = new Regex(@"\{(?:ThemeResource|StaticResource)\s+(AJut_[A-Za-z0-9_]+)\s*\}", RegexOptions.Compiled);
        private static readonly XNamespace kXamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

        // ===========[ Every referenced key exists ]===================================

        [TestMethod]
        public void ThemeKeys_EveryReferencedAJutKey_IsDefinedInDark ()
        {
            this.AssertEveryReferencedKeyIsDefined("Dark");
        }

        [TestMethod]
        public void ThemeKeys_EveryReferencedAJutKey_IsDefinedInLight ()
        {
            this.AssertEveryReferencedKeyIsDefined("Light");
        }

        // ===========[ Both palettes define the same keys ]===================================

        [TestMethod]
        public void ThemeKeys_EveryAJutKey_IsDefinedInBothPalettes ()
        {
            Dictionary<string, HashSet<string>> definedByTheme = kThemes.ToDictionary(theme => theme, theme => ReadDefinedKeys(PalettePath(theme)));
            List<string> gaps = new List<string>();
            foreach (string theme in kThemes)
            {
                foreach (string other in kThemes.Where(t => t != theme))
                {
                    foreach (string key in definedByTheme[theme].Except(definedByTheme[other]).OrderBy(k => k))
                    {
                        gaps.Add($"{key} is defined in {theme} but not in {other}");
                    }
                }
            }

            Assert.AreEqual(0, gaps.Count, "Palette keys missing from one theme:" + Environment.NewLine + string.Join(Environment.NewLine, gaps));
        }

        // ===========[ Helpers ]===================================

        private void AssertEveryReferencedKeyIsDefined (string theme)
        {
            HashSet<string> defined = ReadDefinedKeys(PalettePath(theme));
            HashSet<string> referenced = new HashSet<string>();
            referenced.UnionWith(ReadReferencedKeys(PalettePath(theme)));
            referenced.UnionWith(ReadReferencedKeys(Path.Combine(ResourcesRoot(), kAliasingFile)));

            List<string> gaps = referenced.Where(key => !defined.Contains(key)).OrderBy(k => k).ToList();
            Assert.AreEqual(0, gaps.Count, $"AJut keys referenced but never defined for {theme}:" + Environment.NewLine + string.Join(Environment.NewLine, gaps));
        }

        private static string PalettePath (string theme) => Path.Combine(ResourcesRoot(), $"{theme}ThemeColorsBase.xaml");

        private static string ResourcesRoot ()
        {
            // The test runs from its build output somewhere under the repo, so walk up until the resources folder appears.
            DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
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

        private static HashSet<string> ReadDefinedKeys (string path)
        {
            XDocument doc = XDocument.Load(path);
            return doc.Descendants()
                .Select(e => e.Attribute(kXamlNamespace + "Key")?.Value)
                .Where(key => key != null && key.StartsWith(kKeyPrefix, StringComparison.Ordinal))
                .ToHashSet();
        }

        private static HashSet<string> ReadReferencedKeys (string path)
        {
            XDocument doc = XDocument.Load(path);
            HashSet<string> referenced = new HashSet<string>();
            foreach (XElement element in doc.Descendants())
            {
                foreach (XAttribute attribute in element.Attributes())
                {
                    // A StaticResource element aliasing one key to another names the target with ResourceKey.
                    if (attribute.Name.LocalName == "ResourceKey" && attribute.Value.StartsWith(kKeyPrefix, StringComparison.Ordinal))
                    {
                        referenced.Add(attribute.Value);
                        continue;
                    }

                    foreach (Match match in kMarkupReference.Matches(attribute.Value))
                    {
                        referenced.Add(match.Groups[1].Value);
                    }
                }
            }

            return referenced;
        }
    }
}
