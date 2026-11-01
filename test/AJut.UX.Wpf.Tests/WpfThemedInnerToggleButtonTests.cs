namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Media;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Controls whose templates are built around a ToggleButton (a ComboBox's main button, an Expander's header, a
    /// ToggleStrip's items) under the AJut theme, which has an implicit ToggleButton style. That style reaches a ToggleButton
    /// inside another control's template too, and a style's triggers outrank the template's, so unless the control keeps it
    /// out, its checked and hover text colors replace the control's own. WPF objects need an STA thread, so each test runs on
    /// one, and nothing is ever shown.
    /// </summary>
    [TestClass]
    public class WpfThemedInnerToggleButtonTests
    {
        private static readonly Size kMeasureSize = new Size(600, 600);

        [TestMethod]
        public void WpfToggleStrip_Themed_SelectedItemText_IsTheStripsForegroundPressed ()
        {
            StaTestThread.Run(() =>
            {
                var strip = new ToggleStrip { ItemsSource = new[] { "A", "B" } };
                MakeThemedHost(strip);
                ToggleButton selected = ItemButtons(strip).First(button => button.IsChecked == true);

                Assert.AreSame(strip.ForegroundPressed, selected.Foreground, $"The selected item's text is {Describe(selected.Foreground)}, not the strip's ForegroundPressed ({Describe(strip.ForegroundPressed)})");
            });
        }

        [TestMethod]
        public void WpfToggleStrip_Themed_HoveredItemText_IsNotSetByAnyStyleTrigger ()
        {
            StaTestThread.Run(() =>
            {
                var strip = new ToggleStrip { ItemsSource = new[] { "A", "B" } };
                MakeThemedHost(strip);
                ToggleButton item = ItemButtons(strip).First();

                // The strip shows ForegroundHover through its item template's hover trigger, which any style trigger setting the
                // item's text color outranks
                Assert.IsFalse(StyleTriggersSetForeground(item.Style), "An item's style has triggers that set its text color, which hide the strip's ForegroundHover and ForegroundPressed");
            });
        }

        [TestMethod]
        public void WpfComboBox_Themed_MainButtonText_IsTheComboBoxsAndNoStyleTriggerCanChangeIt ()
        {
            StaTestThread.Run(() =>
            {
                var comboBox = new ComboBox { ItemsSource = new[] { "One", "Two" }, SelectedIndex = 0 };
                MakeThemedHost(comboBox);
                var mainButton = (ToggleButton)comboBox.Template.FindName("MainButton", comboBox);

                // Opening the drop-down checks the main button, and the theme's checked ToggleButton text is too light to read on
                // a light theme's ComboBox; the test can't open the drop-down without showing it, so it checks where the text
                // color comes from instead
                Assert.AreSame(comboBox.Foreground, mainButton.Foreground, $"The ComboBox's text is {Describe(mainButton.Foreground)}, not the ComboBox's Foreground ({Describe(comboBox.Foreground)})");
                Assert.AreEqual(
                    BaseValueSource.ParentTemplate,
                    DependencyPropertyHelper.GetValueSource(mainButton, Control.ForegroundProperty).BaseValueSource,
                    "The main button's text color comes from a style, whose checked and hover triggers then change it"
                );
            });
        }

        [TestMethod]
        public void WpfExpander_Themed_ExpandedHeaderText_IsTheExpandersForeground ()
        {
            StaTestThread.Run(() =>
            {
                var expander = new Expander { Header = "Header", Content = "Content", IsExpanded = true };
                MakeThemedHost(expander);
                var header = (ToggleButton)expander.Template.FindName("PART_Expander", expander);

                Assert.AreSame(expander.Foreground, header.Foreground, $"An expanded Expander's header text is {Describe(header.Foreground)}, not the Expander's Foreground ({Describe(expander.Foreground)})");
            });
        }

        // ===========[ Helpers ]===================================
        // The AJut theme as an app merges it, in its light palette, where the theme's checked ToggleButton text is unreadable
        private static StackPanel MakeThemedHost (params FrameworkElement[] children)
        {
            var host = new StackPanel();
            host.Resources.MergedDictionaries.Add(LoadAJutResources("Resources/LightThemeColorsBase.xaml"));
            host.Resources.MergedDictionaries.Add(LoadAJutResources("Resources/ThemedControlStylesBase.xaml"));
            foreach (FrameworkElement child in children)
            {
                host.Children.Add(child);
            }

            host.Measure(kMeasureSize);
            host.Arrange(new Rect(kMeasureSize));
            host.UpdateLayout();
            return host;
        }

        private static ResourceDictionary LoadAJutResources (string path)
        {
            return (ResourceDictionary)Application.LoadComponent(new Uri($"/AJut.UX.Wpf;component/{path}", UriKind.Relative));
        }

        // The strip's overflow chevron is a ToggleButton too; the items are the ones holding a ToggleItem
        private static ToggleButton[] ItemButtons (ToggleStrip strip)
        {
            return FindVisuals<ToggleButton>(strip).Where(button => button.DataContext is ToggleStrip.ToggleItem).ToArray();
        }

        private static bool StyleTriggersSetForeground (Style style)
        {
            for (Style current = style; current != null; current = current.BasedOn)
            {
                foreach (TriggerBase trigger in current.Triggers)
                {
                    SetterBaseCollection setters = trigger switch
                    {
                        Trigger single => single.Setters,
                        MultiTrigger multi => multi.Setters,
                        DataTrigger data => data.Setters,
                        MultiDataTrigger multiData => multiData.Setters,
                        _ => new SetterBaseCollection(),
                    };

                    if (setters.OfType<Setter>().Any(setter => (setter.Property == Control.ForegroundProperty) && (setter.TargetName == null)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static IEnumerable<T> FindVisuals<T> (DependencyObject parent) where T : DependencyObject
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); ++index)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (T descendant in FindVisuals<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private static string Describe (Brush brush) => (brush as SolidColorBrush)?.Color.ToString() ?? brush?.ToString() ?? "nothing";
    }
}
