namespace AJutShowRoomWinUI
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.UX;
    using AJut.UX.Theming;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using DPUtils = AJut.UX.DPUtils<PaletteDoubleCheck>;

    /// <summary>
    /// A manual check that the two opacity doubles in the AJut WinUI3 palettes can be looked up, from code and through
    /// ThemeResource, and come back holding the loaded theme's values.
    /// </summary>
    public sealed partial class PaletteDoubleCheck : UserControl
    {
        private const string kShadowOpacityKey = "AJut_Double_StandardShadowOpacity";
        private const string kDisabledOpacityKey = "AJut_Double_StandardDisabledOpacity";
        private const double kOpacityMatchEpsilon = 0.001;

        public PaletteDoubleCheck ()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty OutputTextProperty = DPUtils.Register(_ => _.OutputText, string.Empty);
        public string OutputText
        {
            get => (string)this.GetValue(OutputTextProperty);
            set => this.SetValue(OutputTextProperty, value);
        }

        public static readonly DependencyProperty ProbeRowsProperty = DPUtils.Register(_ => _.ProbeRows);
        /// <summary>
        /// The rows the ThemeResource probe builds. Each one is a pair of borders taking their Opacity from the two keys.
        /// </summary>
        public IEnumerable? ProbeRows
        {
            get => (IEnumerable?)this.GetValue(ProbeRowsProperty);
            set => this.SetValue(ProbeRowsProperty, value);
        }

        private void OnLookUpFromCode_OnClick (object sender, RoutedEventArgs e)
        {
            var results = new List<string>();
            bool allPass = this.CheckLookupFromCode(kShadowOpacityKey, results);
            allPass &= this.CheckLookupFromCode(kDisabledOpacityKey, results);

            this.ShowResults(
                allPass
                    ? $"PASS - both keys looked up from code ({this.ActualTheme} theme)"
                    : $"FAIL - looking up the keys from code ({this.ActualTheme} theme)",
                results
            );
        }

        private void OnBuildRows_OnClick (object sender, RoutedEventArgs e)
        {
            const int kRowCount = 3;

            var results = new List<string>();
            try
            {
                // Clear first so a second run builds fresh rows rather than keeping the last run's
                this.ProbeRows = null;
                this.ProbeRows = Enumerable.Range(1, kRowCount).Select(index => $"Row {index}").ToList();
                this.BoundRows.UpdateLayout();
            }
            catch (Exception ex)
            {
                results.Add($"building the rows threw {ex.GetType().FullName}: {ex.Message}");
                this.ShowResults($"FAIL - building rows that bind the keys ({this.ActualTheme} theme)", results);
                return;
            }

            bool allPass = true;
            for (int index = 0; index < kRowCount; ++index)
            {
                StackPanel? row = this.BoundRows.ContainerFromIndex(index)?.GetFirstChildOf<StackPanel>();
                Border[] borders = row?.Children.OfType<Border>().ToArray() ?? Array.Empty<Border>();
                if (borders.Length != 2)
                {
                    results.Add($"row {index + 1}: MISMATCH | was not built");
                    allPass = false;
                    continue;
                }

                allPass &= this.CheckOpacity($"row {index + 1}, {kShadowOpacityKey}", kShadowOpacityKey, borders[0].Opacity, results);
                allPass &= this.CheckOpacity($"row {index + 1}, {kDisabledOpacityKey}", kDisabledOpacityKey, borders[1].Opacity, results);
            }

            this.ShowResults(
                allPass
                    ? $"PASS - every row bound both keys ({this.ActualTheme} theme)"
                    : $"FAIL - building rows that bind the keys ({this.ActualTheme} theme)",
                results
            );
        }

        private bool CheckLookupFromCode (string resourceKey, List<string> results)
        {
            try
            {
                if (!this.TryFindThemedResource(resourceKey, out object found))
                {
                    results.Add($"{resourceKey}: MISMATCH | not found in the loaded theme");
                    return false;
                }

                if (found is double opacity)
                {
                    return this.CheckOpacity(resourceKey, resourceKey, opacity, results);
                }

                results.Add($"{resourceKey}: MISMATCH | came back as {found.GetType().FullName} '{found}', want a double");
                return false;
            }
            catch (Exception ex)
            {
                results.Add($"{resourceKey}: MISMATCH | the lookup threw {ex.GetType().FullName}: {ex.Message}");
                return false;
            }
        }

        private bool CheckOpacity (string label, string resourceKey, double actual, List<string> results)
        {
            double expected = this.GetExpectedOpacity(resourceKey);
            bool ok = Math.Abs(actual - expected) < kOpacityMatchEpsilon;
            results.Add($"{label}: {(ok ? "ok" : "MISMATCH")} | {actual} (want {expected})");
            return ok;
        }

        // What each palette declares, so a lookup that lands on a default or on the other theme's value fails too
        private double GetExpectedOpacity (string resourceKey)
        {
            const double kDarkShadowOpacity = 0.6;
            const double kDarkDisabledOpacity = 0.4;
            const double kLightShadowOpacity = 0.3;
            const double kLightDisabledOpacity = 0.6;

            bool isShadow = resourceKey == kShadowOpacityKey;
            if (this.ActualTheme == ElementTheme.Light)
            {
                return isShadow ? kLightShadowOpacity : kLightDisabledOpacity;
            }

            return isShadow ? kDarkShadowOpacity : kDarkDisabledOpacity;
        }

        private void ShowResults (string verdict, List<string> results)
        {
            this.OutputText = verdict + "\n" + string.Join("\n", results.Select(line => "    " + line));
        }
    }
}
