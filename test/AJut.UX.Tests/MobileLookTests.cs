#nullable enable
namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using AJut.UX.Theming;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Keeps <see cref="MobileLook"/>, the look Android and UIKit both draw their controls from, pointed at keys the AJut
    /// theme has. A key that does not resolve leaves a part of a control on its platform default with the theme loaded,
    /// which no device test catches unless it happens to check that one part.
    /// </summary>
    [TestClass]
    public class MobileLookTests
    {
        private static readonly ThemeCase[] kThemes =
        [
            new ThemeCase("Light", false, false), new ThemeCase("Dark", true, false), new ThemeCase("LightTouch", false, true), new ThemeCase("DarkTouch", true, true),
        ];

        // The keys a toolkit paints as a gradient; every other key in the look is one color
        private static readonly string[] kGradientKeys =
        [
            MobileLook.NumericEditor.FrameSurface, MobileLook.ToggleStrip.StripSurface, MobileLook.ToggleStrip.ItemSelectedFill,
            MobileLook.EnumComboBox.Surface,
        ];

        [TestMethod]
        public void MobileLook_EveryKey_ResolvesInEveryTheme ()
        {
            LookKey[] keys = AllKeys().ToArray();
            Assert.IsTrue(keys.Length > 0, "The look's keys are found: each control's keys are public string constants of its nested class");

            var gaps = new List<string>();
            foreach (LookKey look in keys)
            {
                string[] missingIn = kThemes.Where(theme => !AJutPalette.For(theme.IsDark, theme.IsTouch).TryGetPaint(look.Key, out _)).Select(theme => theme.Name).ToArray();
                if (missingIn.Length > 0)
                {
                    gaps.Add($"{look.Control}.{look.Part} ({look.Key}) does not resolve in {string.Join(", ", missingIn)}");
                }
            }

            Assert.AreEqual(0, gaps.Count, "Mobile look keys the AJut theme does not have:" + Environment.NewLine + string.Join(Environment.NewLine, gaps));
        }

        /// <summary>
        /// A toolkit asking for one of these as one color gets nothing back, which is how a surface ends up on the platform
        /// default.
        /// </summary>
        [TestMethod]
        public void MobileLook_KeysMarkedAsGradients_AreGradientsInEveryTheme ()
        {
            foreach (ThemeCase theme in kThemes)
            {
                AJutPalette.Table palette = AJutPalette.For(theme.IsDark, theme.IsTouch);
                foreach (string key in kGradientKeys)
                {
                    Assert.IsTrue(palette.TryGetPaint(key, out AJutThemePaint? paint) && paint.IsGradient, $"{key} is a gradient in {theme.Name}");
                    Assert.IsFalse(palette.TryGetColor(key, out _), $"{key} is not one color in {theme.Name}");
                }
            }
        }

        /// <summary>
        /// The other half of the check above. A toolkit paints every key not marked as a gradient with one color, so a
        /// gradient there draws as nothing, or as one of its stops.
        /// </summary>
        [TestMethod]
        public void MobileLook_KeysNotMarkedAsGradients_AreOneColorInEveryTheme ()
        {
            LookKey[] keys = AllKeys().Where(look => !kGradientKeys.Contains(look.Key)).ToArray();
            Assert.IsTrue(keys.Length > 0, "The look's keys are found: each control's keys are public string constants of its nested class");

            var gaps = new List<string>();
            foreach (LookKey look in keys)
            {
                string[] notSolidIn = kThemes.Where(theme => !AJutPalette.For(theme.IsDark, theme.IsTouch).TryGetColor(look.Key, out _)).Select(theme => theme.Name).ToArray();
                if (notSolidIn.Length > 0)
                {
                    gaps.Add($"{look.Control}.{look.Part} ({look.Key}) is not one color in {string.Join(", ", notSolidIn)}");
                }
            }

            Assert.AreEqual(0, gaps.Count, "Mobile look keys not marked as gradients that are not one color:" + Environment.NewLine + string.Join(Environment.NewLine, gaps));
        }

        // ===========[ Helper Methods ]===================================
        // Every key in every control's look: the public string constants of each nested class
        private static IEnumerable<LookKey> AllKeys ()
        {
            foreach (Type control in typeof(MobileLook).GetNestedTypes(BindingFlags.Public))
            {
                foreach (FieldInfo field in control.GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral && (field.FieldType == typeof(string))))
                {
                    yield return new LookKey(control.Name, field.Name, (string)field.GetRawConstantValue()!);
                }
            }
        }

        // ===========[ Subclasses ]===================================
        private sealed record ThemeCase (string Name, bool IsDark, bool IsTouch);

        // One key in a control's look: the control's nested class, the constant's name, and the theme key it holds
        private sealed record LookKey (string Control, string Part, string Key);
    }
}
