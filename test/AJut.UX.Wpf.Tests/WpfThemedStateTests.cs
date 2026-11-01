namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Media;
    using System.Windows.Media.Effects;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// WPF controls with the AJut theme loaded and with none. Every control a user can focus takes the AJut focus visual,
    /// and the list of those accounts for every AJut control. With no theme loaded, the docking buttons, toolbar, and toggle
    /// switch still draw. PathSelectionControl glows when its path is invalid and its brush properties read back what they
    /// hold, and FlatTreeListControl gives a drag target's expander back to its style when the highlight clears. WPF objects
    /// need an STA thread, so each test runs on one, and nothing is ever shown.
    /// </summary>
    [TestClass]
    public class WpfThemedStateTests
    {
        private static readonly Size kMeasureSize = new Size(600, 600);

        // Every AJut control a user can tab to or click into, so every one that can show a keyboard focus visual
        private static readonly Type[] kFocusableAJutControls =
        [
            typeof(AutoBrushButton), typeof(BrushButton), typeof(BumpStack), typeof(BusyWait), typeof(ColorEditIngressControl),
            typeof(ColorValueEditControl), typeof(DockDropInsertionDriverWidget), typeof(DockPanelAddButton),
            typeof(DockPanelAddRemoveToolbar), typeof(DockPanelShowHideToggleButton), typeof(DockWholeWindowDragStartControl),
            typeof(DockZone), typeof(DockZoneSelectedHeaderControl), typeof(DockZoneTabHeadersControl), typeof(EditableTextBlock),
            typeof(EnumComboBox), typeof(EnumToggleStrip), typeof(FlatTreeListControl), typeof(MessageBoxPopover),
            typeof(NullableEditor), typeof(NumericEditor), typeof(PathSelectionControl), typeof(PopupMenuButton),
            typeof(PropertyGrid), typeof(PropertyGridListEditor), typeof(StackNavActiveContentPresenter),
            typeof(StackNavActiveDrawerPresenter), typeof(StackNavActiveHeaderPresenter), typeof(StackNavBusyWaitOverlay),
            typeof(StackNavDisplay), typeof(StackNavPopoverOverlay), typeof(Svg), typeof(ToggleStrip), typeof(ToggleSwitchButton),
            typeof(UIElementRepeater), typeof(WindowChromeButtonStrip),
        ];

        // The stock controls the AJut theme styles that can take keyboard focus. TextBox and PasswordBox are left out: they
        // show focus with their caret and border, and stock WPF gives them no focus visual either.
        private static readonly Type[] kFocusableStockControls =
        [
            typeof(Button), typeof(CheckBox), typeof(ComboBox), typeof(ComboBoxItem), typeof(Expander), typeof(ListBox),
            typeof(ListBoxItem), typeof(Slider), typeof(TabControl), typeof(TabItem), typeof(ToggleButton),
        ];

        // AJut controls a user never focuses: a window has no focus visual of its own, and the sibling split spanner has no
        // look at all (no default style, nothing uses it)
        private static readonly Type[] kAJutControlsNeverFocused = [typeof(DefaultDockTearoffWindow), typeof(DockDropSiblingSplitSpannerWidget)];

        // ===========[ PathSelectionControl ]===================================
        [TestMethod]
        public void WpfPathSelectionControl_InvalidPath_GlowsInTheInvalidBorderColor ()
        {
            StaTestThread.Run(() =>
            {
                var path = new PathSelectionControl { TreatEmptyPathAsInvalid = true, InvalidPathBorderBrush = Brushes.OrangeRed };
                MakeHost(path);
                Assert.IsFalse(path.IsPathValid, "An empty path is invalid here");

                // The glow is a border of its own behind the content, so the text box is never drawn through an effect
                var root = (Border)path.Template.FindName("PathSelectorBorderRoot", path);
                var glowBorder = (Border)path.Template.FindName("PathSelectorGlow", path);
                Assert.IsNull(root.Effect, "The content is not drawn through an effect");
                Assert.AreEqual(Visibility.Visible, glowBorder.Visibility, "An invalid path draws a glow around the control");
                var glow = glowBorder.Effect as DropShadowEffect;
                Assert.IsNotNull(glow, "The glow is a drop shadow with no offset");
                Assert.AreEqual(Colors.OrangeRed, glow.Color, "The glow is the invalid border's color");

                path.TreatEmptyPathAsInvalid = false;
                Assert.IsTrue(path.IsPathValid, "Allowed, an empty path is valid");
                Assert.AreEqual(Visibility.Collapsed, glowBorder.Visibility, "And the glow goes once the path is valid");
            });
        }

        [TestMethod]
        public void WpfPathSelectionControl_BrushProperties_ReadBackTheWpfBrushTheyHold ()
        {
            StaTestThread.Run(() =>
            {
                string[] gdiTyped = typeof(PathSelectionControl).GetProperties()
                    .Where(property => property.PropertyType.FullName == "System.Drawing.Brush")
                    .Select(property => property.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
                Assert.AreEqual(0, gdiTyped.Length, "PathSelectionControl brush properties typed as System.Drawing's Brush, which throw when read from code: " + string.Join(", ", gdiTyped));

                var path = new PathSelectionControl();
                path.SetValue(PathSelectionControl.FocusedBorderBrushProperty, Brushes.Red);
                object read = path.FocusedBorderBrush;
                Assert.AreSame(Brushes.Red, read, "FocusedBorderBrush reads back the brush it holds");
            });
        }

        // ===========[ Focus visual ]===================================
        [TestMethod]
        public void WpfThemed_EveryFocusableControl_TakesTheAJutFocusVisual ()
        {
            StaTestThread.Run(() =>
            {
                FrameworkElement[] controls = kFocusableAJutControls.Concat(kFocusableStockControls).Select(MakeControl).ToArray();
                StackPanel host = MakeThemedHost(controls);

                var gaps = new List<string>();
                var focusVisual = host.TryFindResource("AJut_Style_FocusVisual") as Style;
                if (focusVisual == null)
                {
                    gaps.Add("The AJut theme defines no AJut_Style_FocusVisual");
                }

                foreach (FrameworkElement control in controls)
                {
                    if ((focusVisual == null) || !ReferenceEquals(focusVisual, control.FocusVisualStyle))
                    {
                        gaps.Add($"{control.GetType().Name} shows {DescribeFocusVisual(control.FocusVisualStyle)} under keyboard focus");
                    }
                }

                AssertNoGaps(gaps, "Controls whose keyboard focus visual is not the AJut theme's");
            });
        }

        [TestMethod]
        public void WpfThemed_FocusVisualList_CoversEveryAJutControl ()
        {
            // A new control has to be held to the focus visual test above, or named here as one nobody focuses
            Type[] accountedFor = kFocusableAJutControls.Concat(kAJutControlsNeverFocused).ToArray();
            string[] missing = typeof(PathSelectionControl).Assembly.GetExportedTypes()
                .Where(type => typeof(Control).IsAssignableFrom(type) && !type.IsAbstract && !accountedFor.Contains(type))
                .Select(type => type.FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.AreEqual(0, missing.Length, "AJut controls the focus visual test does not account for: " + string.Join(", ", missing));
        }

        // ===========[ FlatTreeListControl drag highlight ]===================================
        [TestMethod]
        public void WpfFlatTreeListControl_DragTargetHighlightCleared_TheExpanderGoesBackToItsStyledForeground ()
        {
            StaTestThread.Run(() =>
            {
                var tree = new FlatTreeListControl { DragTargetHighlightBrush = Brushes.Orange };
                var expanderStyle = new Style(typeof(ToggleButton));
                expanderStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Gray));
                var expander = new ToggleButton { Style = expanderStyle };
                MakeHost(tree, expander);

                tree.HighlightDragTargetExpander_Wpf(expander);
                Assert.AreSame(Brushes.Orange, expander.Foreground, "During a drag the drop parent's expander takes the highlight");

                tree.ClearParentHighlight_Wpf();
                Assert.AreEqual(
                    BaseValueSource.Style,
                    DependencyPropertyHelper.GetValueSource(expander, Control.ForegroundProperty).BaseValueSource,
                    "After a drag the expander keeps a local copy of its old brush, so its hover highlight and later theme switches no longer reach it"
                );
            });
        }

        // ===========[ ListBox ]===================================
        [TestMethod]
        public void WpfThemed_ListBoxItem_ShowsItsOwnContent_AndABoundItemShowsItsData ()
        {
            StaTestThread.Run(() =>
            {
                var written = new ListBoxItem { Content = "Item One" };
                var writtenList = new ListBox();
                writtenList.Items.Add(written);

                var boundList = new ListBox { ItemsSource = new[] { "Bound One" } };
                MakeThemedHost(writtenList, boundList);
                var bound = (ListBoxItem)boundList.ItemContainerGenerator.ContainerFromIndex(0);

                Assert.IsTrue(PresentedContents(written).Contains("Item One"), $"A ListBoxItem written in the list shows {DescribeContents(written)}, not its own Content");
                Assert.IsTrue(PresentedContents(bound).Contains("Bound One"), $"A bound list's item shows {DescribeContents(bound)}, not its data");
            });
        }

        // ===========[ Themed and unthemed ]===================================
        [TestMethod]
        public void WpfUnthemed_DockPanelButtonsToolbarAndSwitch_DrawWithTheirOwnColors ()
        {
            StaTestThread.Run(() =>
            {
                var add = new DockPanelAddButton();
                var showHide = new DockPanelShowHideToggleButton();
                var toolbar = new DockPanelAddRemoveToolbar();
                var toggleSwitch = new ToggleSwitchButton();
                MakeHost(add, showHide, toolbar, toggleSwitch);
                var plusOverlay = (TextBlock)add.Template.FindName("PlusOverlay", add);

                // With no theme loaded there is no palette, so each of these is the default style's own color or nothing
                var missing = new List<string>();
                ExpectColor(missing, "Add button's fill", add.Background);
                ExpectColor(missing, "Add button's text", add.Foreground);
                ExpectColor(missing, "Add button's border", add.BorderBrush);
                ExpectColor(missing, "Add button's plus badge", plusOverlay.Foreground);
                ExpectColor(missing, "Show/hide button's fill", showHide.Background);
                ExpectColor(missing, "Show/hide button's text", showHide.Foreground);
                ExpectColor(missing, "Show/hide button's border", showHide.BorderBrush);
                ExpectColor(missing, "Toolbar's fill", toolbar.Background);
                ExpectColor(missing, "Toolbar's border", toolbar.BorderBrush);
                ExpectColor(missing, "Toggle switch's side content text", toggleSwitch.ContentForeground);
                AssertNoGaps(missing, "With no theme loaded these draw nothing, since their default styles take palette keys");
            });
        }

        // ===========[ Helpers ]===================================
        // The AJut theme as an app merges it: the dark palette, then the themed control styles over it
        private static StackPanel MakeThemedHost (params FrameworkElement[] children)
        {
            var host = new StackPanel();
            host.Resources.MergedDictionaries.Add(LoadAJutResources("Resources/DarkThemeColorsBase.xaml"));
            host.Resources.MergedDictionaries.Add(LoadAJutResources("Resources/ThemedControlStylesBase.xaml"));
            AddAndLayOut(host, children);
            return host;
        }

        private static StackPanel MakeHost (params FrameworkElement[] children)
        {
            var host = new StackPanel();
            AddAndLayOut(host, children);
            return host;
        }

        // A measure and arrange applies the templates; UpdateLayout runs whatever layout they queued
        private static void AddAndLayOut (StackPanel host, FrameworkElement[] children)
        {
            foreach (FrameworkElement child in children)
            {
                host.Children.Add(child);
            }

            LayOut(host);
        }

        private static void LayOut (FrameworkElement host)
        {
            host.Measure(kMeasureSize);
            host.Arrange(new Rect(kMeasureSize));
            host.UpdateLayout();
        }

        // MessageBoxPopover is made through its factory; everything else has a parameterless constructor
        private static FrameworkElement MakeControl (Type type)
        {
            if (type == typeof(MessageBoxPopover))
            {
                return MessageBoxPopover.Generate("Message", "OK");
            }

            return (FrameworkElement)Activator.CreateInstance(type);
        }

        // What every content presenter under an element is showing
        private static List<object> PresentedContents (DependencyObject root)
        {
            var contents = new List<object>();
            var toVisit = new Stack<DependencyObject>();
            toVisit.Push(root);
            while (toVisit.Count > 0)
            {
                DependencyObject next = toVisit.Pop();
                if (next is ContentPresenter presenter)
                {
                    contents.Add(presenter.Content);
                }

                for (int index = 0; index < VisualTreeHelper.GetChildrenCount(next); ++index)
                {
                    toVisit.Push(VisualTreeHelper.GetChild(next, index));
                }
            }

            return contents;
        }

        private static string DescribeContents (DependencyObject root)
        {
            return "[" + string.Join(", ", PresentedContents(root).Select(content => content?.ToString() ?? "nothing")) + "]";
        }

        private static ResourceDictionary LoadAJutResources (string path)
        {
            return (ResourceDictionary)Application.LoadComponent(new Uri($"/AJut.UX.Wpf;component/{path}", UriKind.Relative));
        }

        private static void ExpectColor (List<string> missing, string what, Brush actual)
        {
            if (actual == null)
            {
                missing.Add(what);
            }
        }

        private static void AssertNoGaps (List<string> gaps, string heading)
        {
            Assert.AreEqual(0, gaps.Count, $"{heading}:{Environment.NewLine}{string.Join(Environment.NewLine, gaps)}");
        }

        private static string DescribeFocusVisual (Style style)
        {
            return (style == null) ? "no focus visual" : $"a focus visual not the theme's ({style.TargetType?.Name ?? "untyped"})";
        }
    }
}
