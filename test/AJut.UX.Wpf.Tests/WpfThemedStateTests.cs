namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Documents;
    using System.Windows.Media;
    using System.Windows.Media.Effects;
    using AJut.UX.Controls;
    using AJut.UX.PropertyInteraction;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// With the AJut theme loaded, every state of every WPF control has to draw from the palette: no default-style literal,
    /// stock look or system color left showing. Each test builds controls under the theme as an app merges it, puts them in
    /// the state in question, and checks the brush that state resolves to. States a test can't put a control in (a hover,
    /// a press) are checked through the themed style's own trigger for them. WPF objects need an STA thread, so each test
    /// runs on one, and nothing is ever shown.
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

        // ===========[ Controls the theme left out ]===================================
        [TestMethod]
        public void WpfThemed_PathSelectionControl_EveryStateTakesThePalette ()
        {
            StaTestThread.Run(() =>
            {
                var path = new PathSelectionControl();
                StackPanel host = MakeThemedHost(path);
                var textBox = (TextBox)path.Template.FindName("PART_PathTextBox", path);

                // Read through the properties' fields, since the test below holds their typed accessors to account
                var gaps = new List<string>();
                ExpectPalette(gaps, host, "Fill", path.Background, "AJut_Brush_PopOutControlBackground");
                ExpectPalette(gaps, host, "Border", path.BorderBrush, "AJut_Brush_TextBoxBorder");
                ExpectPalette(gaps, host, "Path text box's inset edge", textBox.BorderBrush, "AJut_Brush_Shadow");
                ExpectPalette(gaps, host, "Border with keyboard focus", path.GetValue(PathSelectionControl.FocusedBorderBrushProperty), "AJut_Brush_PrimaryHighlight");
                ExpectPalette(gaps, host, "Border and badge for a bad path", path.GetValue(PathSelectionControl.InvalidPathBorderBrushProperty), "AJut_Brush_BadStuff");
                ExpectPalette(gaps, host, "Bad path badge glyph", path.GetValue(PathSelectionControl.InvalidForegroundSymbolProperty), "AJut_Brush_NormalText");
                ExpectPalette(gaps, host, "Buttons' fill", path.GetValue(PathSelectionControl.ButtonBackgroundProperty), "AJut_Brush_ControlBackground");
                ExpectPalette(gaps, host, "Buttons' glyph", path.GetValue(PathSelectionControl.ButtonForegroundProperty), "AJut_Brush_SymbolForeground");
                ExpectPalette(gaps, host, "Browse button's fill under the pointer", path.GetValue(PathSelectionControl.ButtonHoverBackgroundProperty), "AJut_Brush_ControlBackgroundHover");
                ExpectPalette(gaps, host, "Browse button's glyph under the pointer", path.GetValue(PathSelectionControl.ButtonHoverForegroundProperty), "AJut_Brush_SymbolForegroundHighlight");
                ExpectPalette(gaps, host, "Browse button's fill when pressed", path.GetValue(PathSelectionControl.ButtonPressedBackgroundProperty), "AJut_Brush_ButtonBackgroundPressed");
                ExpectPalette(gaps, host, "Browse button's glyph when pressed", path.GetValue(PathSelectionControl.ButtonPressedForegroundProperty), "AJut_Brush_SymbolForegroundPressed");
                AssertNoGaps(gaps, "PathSelectionControl brushes that are not the AJut theme's");
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

        [TestMethod]
        public void WpfThemed_PropertyGrid_ListElementRemoveButton_TakesTheThemedButtonLook ()
        {
            StaTestThread.Run(() =>
            {
                var grid = new PropertyGrid { SingleItemSource = new ListSource() };
                StackPanel host = MakeThemedHost(grid);

                Button remove = FindVisuals<Button>(grid).FirstOrDefault(button => (button.ToolTip as string) == "Remove element");
                Assert.IsNotNull(remove, "The grid built no row with a remove-element button");

                var gaps = new List<string>();
                ExpectPalette(gaps, host, "Remove-element button's fill", remove.Background, "AJut_Brush_ControlBackground");
                ExpectPalette(gaps, host, "Remove-element button's border", remove.BorderBrush, "AJut_Brush_ButtonBorder");
                AssertNoGaps(gaps, "A list element's remove button shows the stock button rather than the themed one");
            });
        }

        [TestMethod]
        public void WpfThemed_ComboBoxItem_HighlightedSelectedAndDisabled_TakeThePalette ()
        {
            StaTestThread.Run(() =>
            {
                StackPanel host = MakeThemedHost();
                var themed = host.TryFindResource(typeof(ComboBoxItem)) as Style;
                Assert.IsNotNull(themed, "The AJut theme has no ComboBoxItem style, so a drop-down's items show the stock look in every state");

                // The themed style is set explicitly since an implicit style only reaches the exact type, not this one
                var highlighted = new HighlightableComboBoxItem { Style = themed, Content = "Highlighted" };
                var selected = new HighlightableComboBoxItem { Style = themed, Content = "Selected", IsSelected = true };
                var selectedAndHighlighted = new HighlightableComboBoxItem { Style = themed, Content = "Both", IsSelected = true };
                var disabled = new HighlightableComboBoxItem { Style = themed, Content = "Disabled", IsEnabled = false };
                foreach (HighlightableComboBoxItem item in new[] { highlighted, selected, selectedAndHighlighted, disabled })
                {
                    host.Children.Add(item);
                }

                highlighted.SetHighlighted(true);
                selectedAndHighlighted.SetHighlighted(true);
                LayOut(host);

                var gaps = new List<string>();
                ExpectPalette(gaps, host, "Highlighted item's fill", highlighted.Background, "AJut_Brush_ListSelectionBackground");
                ExpectPalette(gaps, host, "Highlighted item's outline", highlighted.BorderBrush, "AJut_Brush_ListItemHover");
                ExpectPalette(gaps, host, "Selected item's fill", selected.Background, "AJut_Brush_ListSelection");
                ExpectPalette(gaps, host, "Selected item's text", selected.Foreground, "AJut_Brush_ImportantText");
                ExpectPalette(gaps, host, "Selected and highlighted item's fill", selectedAndHighlighted.Background, "AJut_Brush_ListItemHover");
                ExpectPalette(gaps, host, "Disabled item's text", disabled.Foreground, "AJut_Brush_UnderstatedText");
                AssertNoGaps(gaps, "ComboBoxItem brushes that are not the AJut theme's");
            });
        }

        [TestMethod]
        public void WpfThemed_ComboBox_DropDownShadow_TakesThePaletteShadow ()
        {
            StaTestThread.Run(() =>
            {
                var comboBox = new ComboBox();
                StackPanel host = MakeThemedHost(comboBox);
                var popup = (Popup)comboBox.Template.FindName("PART_Popup", comboBox);
                Border shadow = ((Grid)popup.Child).Children.OfType<Border>().First();

                var gaps = new List<string>();
                ExpectPalette(gaps, host, "Drop-down shadow", shadow.BorderBrush, "AJut_Brush_Shadow");
                AssertNoGaps(gaps, "The themed ComboBox's drop-down shadow is not the AJut theme's");
            });
        }

        [TestMethod]
        public void WpfThemed_Separator_TakesTheCommonBorder ()
        {
            StaTestThread.Run(() =>
            {
                // The one MessageBoxPopover draws between its message and its options is a plain Separator
                var separator = new Separator();
                StackPanel host = MakeThemedHost(separator);

                var gaps = new List<string>();
                ExpectPalette(gaps, host, "Separator line", separator.Background, "AJut_Brush_BorderCommon");
                AssertNoGaps(gaps, "A plain Separator shows the stock line with the AJut theme loaded");
            });
        }

        [TestMethod]
        public void WpfThemed_NullableEditor_NullOverlay_TakesThePaletteShadow ()
        {
            StaTestThread.Run(() =>
            {
                var editor = new NullableEditor();
                StackPanel host = MakeThemedHost(editor);
                var overlay = (Grid)editor.Template.FindName("PART_NullOverlay", editor);
                Border dimming = overlay.Children.OfType<Border>().First();

                var gaps = new List<string>();
                ExpectPalette(gaps, host, "Overlay dimming the editor while the value is null", dimming.Background, "AJut_Brush_Shadow");
                AssertNoGaps(gaps, "NullableEditor brushes that are not the AJut theme's");
            });
        }

        [TestMethod]
        public void WpfThemed_ColorValueEditControl_FrameAndSwatchBacking_TakeThePalette ()
        {
            StaTestThread.Run(() =>
            {
                var editor = new ColorValueEditControl();
                StackPanel host = MakeThemedHost(editor);

                var gaps = new List<string>();
                ExpectPalette(gaps, host, "Frame, hex row rule and swatch borders, used on its own", editor.BorderBrush, "AJut_Brush_BorderCommon");
                ExpectPalette(gaps, host, "Swatch backing behind a translucent color", editor.BackingFillBrush, "AJut_Brush_TextBoxBackground");
                ExpectGradientOf(gaps, host, "Swatch transparency lines", editor.TransparencyLinesBrush, "AJut_Color_Shadow");
                AssertNoGaps(gaps, "ColorValueEditControl brushes that are not the AJut theme's");
            });
        }

        [TestMethod]
        public void WpfThemed_ColorEditIngressControl_GlossBannerGlyphAndSwatchBacking_TakeThePalette ()
        {
            StaTestThread.Run(() =>
            {
                var editor = new ColorEditIngressControl();
                StackPanel host = MakeThemedHost(editor);
                var gloss = (Grid)editor.Template.FindName("FancyCover", editor);
                TextBlock bannerGlyph = ((Grid)editor.Template.FindName("EditOverlay", editor)).Children.OfType<TextBlock>().Single();

                var gaps = new List<string>();
                ExpectGradientOf(gaps, host, "Gloss over the swatch at rest", gloss.Background, "AJut_Color_Shadow", "AJut_Color_RimLighting");
                ExpectPalette(gaps, host, "Corner banner's edit glyph, on hover and while editing", bannerGlyph.Foreground, "AJut_Brush_SymbolForegroundHighlight");
                ExpectPalette(gaps, host, "Swatch backing behind a translucent color", editor.BackingFillBrush, "AJut_Brush_TextBoxBackground");
                ExpectGradientOf(gaps, host, "Swatch transparency lines", editor.TransparencyLinesBrush, "AJut_Color_Shadow");
                AssertNoGaps(gaps, "ColorEditIngressControl brushes that are not the AJut theme's");
            });
        }

        // ===========[ Focus visual, selection and caret ]===================================
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

        [TestMethod]
        public void WpfThemed_TextBoxAndPasswordBox_SelectionAndCaret_TakeThePalette ()
        {
            StaTestThread.Run(() =>
            {
                var textBox = new TextBox();
                var passwordBox = new PasswordBox();
                StackPanel host = MakeThemedHost(textBox, passwordBox);

                var gaps = new List<string>();
                ExpectPalette(gaps, host, "TextBox selected text highlight", textBox.SelectionBrush, "AJut_Brush_PrimaryHighlight");
                ExpectPalette(gaps, host, "TextBox caret", textBox.CaretBrush, "AJut_Brush_InteractiveText");
                ExpectPalette(gaps, host, "PasswordBox selected text highlight", passwordBox.SelectionBrush, "AJut_Brush_PrimaryHighlight");
                ExpectPalette(gaps, host, "PasswordBox caret", passwordBox.CaretBrush, "AJut_Brush_InteractiveText");
                AssertNoGaps(gaps, "Text selection and caret brushes that are not the AJut theme's");
            });
        }

        // ===========[ Smaller states ]===================================
        [TestMethod]
        public void WpfThemed_NumericEditor_NudgeFillsAndInsetEdges_TakeThePalette ()
        {
            StaTestThread.Run(() =>
            {
                var editor = new NumericEditor();
                StackPanel host = MakeThemedHost(editor);
                Border[] borders = FindVisuals<Border>(editor).ToArray();
                Border insetShadow = borders.FirstOrDefault(border => border.BorderThickness == new Thickness(1.4, 1.4, 0, 0));
                Border insetHighlight = borders.FirstOrDefault(border => border.BorderThickness == new Thickness(0, 0, 1, 1));
                Assert.IsNotNull(insetShadow, "The text area's inset shadow edge was not found");
                Assert.IsNotNull(insetHighlight, "The text area's inset highlight edge was not found");

                var gaps = new List<string>();
                ExpectPalette(gaps, host, "Nudge up fill under the pointer", editor.IncreaseHoverHighlight, "AJut_Brush_ListSelectionBackground");
                ExpectPalette(gaps, host, "Nudge down fill under the pointer", editor.DecreaseHoverHighlight, "AJut_Brush_ListSelectionBackground");
                ExpectPalette(gaps, host, "Nudge up fill when pressed", editor.IncreasePressedHighlight, "AJut_Brush_PrimaryHighlightDesat");
                ExpectPalette(gaps, host, "Nudge down fill when pressed", editor.DecreasePressedHighlight, "AJut_Brush_PrimaryHighlightDesat");
                ExpectPalette(gaps, host, "Text area's top and left inset edge", insetShadow.BorderBrush, "AJut_Brush_Shadow");
                ExpectPalette(gaps, host, "Text area's bottom and right inset edge", insetHighlight.BorderBrush, "AJut_Brush_Rimlighting");
                AssertNoGaps(gaps, "NumericEditor brushes that are not the AJut theme's");
            });
        }

        [TestMethod]
        public void WpfThemed_FlatTreeListControl_ExpanderHoverAndDragVisuals_TakeThePalette ()
        {
            StaTestThread.Run(() =>
            {
                var tree = new FlatTreeListControl();
                StackPanel host = MakeThemedHost(tree);
                var ghost = (Border)tree.Template.FindName("PART_DragGhost", tree);

                var gaps = new List<string>();
                ExpectPalette(gaps, host, "Expander fill under the pointer", tree.GlyphBackgroundHighlightBrush, "AJut_Brush_MouseInteractionHighlight");
                ExpectPalette(gaps, host, "Drag insertion line", tree.InsertionLineBrush, "AJut_Brush_PrimaryHighlight");
                ExpectPalette(gaps, host, "Drag parent connector", tree.ParentConnectorBrush, "AJut_Brush_PrimaryHighlightDesat");
                ExpectPalette(gaps, host, "Drop parent's expander highlight", tree.DragTargetHighlightBrush, "AJut_Brush_PrimaryHighlight");
                ExpectPalette(gaps, host, "Drag ghost's fill", ghost.Background, "AJut_Brush_PopupBackground");
                ExpectPalette(gaps, host, "Drag ghost's border", ghost.BorderBrush, "AJut_Brush_BorderCommon");
                ExpectPalette(gaps, host, "Drag ghost's label", ghost.GetValue(TextElement.ForegroundProperty), "AJut_Brush_NormalText");
                AssertNoGaps(gaps, "FlatTreeListControl brushes that are not the AJut theme's");
            });
        }

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

        [TestMethod]
        public void WpfThemed_DockWholeWindowDragStartControl_Hover_TakesThePalette ()
        {
            StaTestThread.Run(() =>
            {
                var dragStart = new DockWholeWindowDragStartControl();
                StackPanel host = MakeThemedHost(dragStart);

                var gaps = new List<string>();
                ExpectPalette(gaps, host, "Fill under the pointer", dragStart.BackgroundBrushHighlighted, "AJut_Brush_PrimaryHighlight");
                ExpectPalette(gaps, host, "Border under the pointer", dragStart.BorderBrushHighlighted, "AJut_Brush_Shadow");
                AssertNoGaps(gaps, "DockWholeWindowDragStartControl brushes that are not the AJut theme's");
            });
        }

        [TestMethod]
        public void WpfThemed_WindowChromeButtonStrip_HoverAndPressedBorders_TakeThePalette ()
        {
            StaTestThread.Run(() =>
            {
                var strip = new WindowChromeButtonStrip();
                StackPanel host = MakeThemedHost(strip);

                var gaps = new List<string>();
                ExpectPalette(gaps, host, "Button border under the pointer", strip.ButtonHoverBorder, "AJut_Brush_Shadow");
                ExpectPalette(gaps, host, "Button border when pressed", strip.ButtonPressedBorder, "AJut_Brush_Rimlighting");
                ExpectPalette(gaps, host, "Close button border under the pointer", strip.CloseButtonHoverBorder, "AJut_Brush_Rimlighting");
                ExpectPalette(gaps, host, "Close button glyph under the pointer", strip.CloseButtonHoverGlyphForeground, "AJut_Brush_ChromeButtonHoverForeground");
                AssertNoGaps(gaps, "WindowChromeButtonStrip brushes that are not the AJut theme's");
            });
        }

        [TestMethod]
        public void WpfThemed_StackNav_CoverAndPopoverHalo_TakeThePalette ()
        {
            StaTestThread.Run(() =>
            {
                var content = new StackNavActiveContentPresenter();
                var popover = new StackNavPopoverOverlay();
                StackPanel host = MakeThemedHost(content, popover);

                var gaps = new List<string>();
                ExpectPalette(gaps, host, "Cover over the content while busy or showing a popover", content.CoverBackgroundBrush, "AJut_Brush_OverlayBlockoutBackground");
                ExpectPalette(gaps, host, "Halo around a popover", popover.HighlightBorderBrush, "AJut_Brush_Shadow");
                AssertNoGaps(gaps, "StackNav brushes that are not the AJut theme's");
            });
        }

        [TestMethod]
        public void WpfThemed_DockDropInsertionDriverWidget_EngagedBorder_TakesThePalette ()
        {
            StaTestThread.Run(() =>
            {
                var widget = new DockDropInsertionDriverWidget();
                StackPanel host = MakeThemedHost(widget);

                var gaps = new List<string>();
                ExpectPalette(gaps, host, "Border while a drag is over it", widget.BorderHighlighted, "AJut_Brush_BorderCommon");
                AssertNoGaps(gaps, "DockDropInsertionDriverWidget brushes that are not the AJut theme's");
            });
        }

        // ===========[ Themed and unthemed ]===================================
        [TestMethod]
        public void WpfThemed_DockPanelButtonsToolbarAndSwitch_EveryStateTakesThePalette ()
        {
            StaTestThread.Run(() =>
            {
                var add = new DockPanelAddButton();
                var showHide = new DockPanelShowHideToggleButton { IsChecked = true };
                var toolbar = new DockPanelAddRemoveToolbar();
                var toggleSwitch = new ToggleSwitchButton();
                StackPanel host = MakeThemedHost(add, showHide, toolbar, toggleSwitch);
                var plusOverlay = (TextBlock)add.Template.FindName("PlusOverlay", add);

                var gaps = new List<string>();
                ExpectPalette(gaps, host, "Add button's fill", add.Background, "AJut_Brush_InnerPanelBackground");
                ExpectPalette(gaps, host, "Add button's text", add.Foreground, "AJut_Brush_NormalText");
                ExpectPalette(gaps, host, "Add button's border", add.BorderBrush, "AJut_Brush_BorderCommon");
                ExpectPalette(gaps, host, "Add button's plus badge", plusOverlay.Foreground, "AJut_Brush_PrimaryHighlightBright");
                var addStyle = host.TryFindResource(typeof(DockPanelAddButton)) as Style;
                ExpectThemedTrigger(gaps, addStyle, "Add button's fill under the pointer", Control.BackgroundProperty, "AJut_Brush_ControlBackgroundHover", new StateCondition(UIElement.IsMouseOverProperty, true));
                ExpectThemedTrigger(gaps, addStyle, "Add button's border under the pointer", Control.BorderBrushProperty, "AJut_Brush_BorderCommonHover", new StateCondition(UIElement.IsMouseOverProperty, true));
                ExpectThemedTrigger(gaps, addStyle, "Add button's fill when pressed", Control.BackgroundProperty, "AJut_Brush_ButtonBackgroundPressed", new StateCondition(ButtonBase.IsPressedProperty, true));
                ExpectThemedTrigger(gaps, addStyle, "Add button's border when pressed", Control.BorderBrushProperty, "AJut_Brush_BorderCommonPress", new StateCondition(ButtonBase.IsPressedProperty, true));

                ExpectPalette(gaps, host, "Shown panel button's fill", showHide.Background, "AJut_Brush_PrimaryHighlight");
                ExpectPalette(gaps, host, "Shown panel button's border", showHide.BorderBrush, "AJut_Brush_PrimaryHighlight");
                ExpectPalette(gaps, host, "Shown panel button's text", showHide.Foreground, "AJut_Brush_ImportantText");
                var showHideStyle = host.TryFindResource(typeof(DockPanelShowHideToggleButton)) as Style;
                ExpectThemedTrigger(gaps, showHideStyle, "Show/hide button's fill under the pointer", Control.BackgroundProperty, "AJut_Brush_ControlBackgroundHover", new StateCondition(UIElement.IsMouseOverProperty, true));
                ExpectThemedTrigger(gaps, showHideStyle, "Show/hide button's fill when pressed", Control.BackgroundProperty, "AJut_Brush_ButtonBackgroundPressed", new StateCondition(ButtonBase.IsPressedProperty, true));
                ExpectThemedTrigger(gaps, showHideStyle, "Shown panel button's fill under the pointer", Control.BackgroundProperty, "AJut_Brush_PrimaryHighlightBright", new StateCondition(ToggleButton.IsCheckedProperty, true), new StateCondition(UIElement.IsMouseOverProperty, true));

                ExpectPalette(gaps, host, "Toolbar's fill", toolbar.Background, "AJut_Brush_PanelBackground");
                ExpectPalette(gaps, host, "Toolbar's border", toolbar.BorderBrush, "AJut_Brush_BorderCommon");
                ExpectPalette(gaps, host, "Toggle switch's side content text", toggleSwitch.ContentForeground, "AJut_Brush_NormalText");
                AssertNoGaps(gaps, "Docking toolbar and toggle switch brushes that are not the AJut theme's");
            });
        }

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

        [TestMethod]
        public void WpfThemed_PopupMenuButton_MenuShadow_TakesThePaletteShadow ()
        {
            StaTestThread.Run(() =>
            {
                var menuButton = new PopupMenuButton();
                StackPanel host = MakeThemedHost(menuButton);
                var popup = (Popup)menuButton.Template.FindName("PART_Popup", menuButton);
                var shadow = (DropShadowEffect)((Grid)popup.Child).Children.OfType<Border>().First().Effect;

                Assert.AreEqual(host.TryFindResource("AJut_Color_Shadow"), shadow.Color, "The menu's shadow takes the palette's shadow color");
                Assert.AreEqual(host.TryFindResource("AJut_Double_StandardShadowOpacity"), shadow.Opacity, "The menu's shadow takes the palette's shadow opacity");
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

        private static ResourceDictionary LoadAJutResources (string path)
        {
            return (ResourceDictionary)Application.LoadComponent(new Uri($"/AJut.UX.Wpf;component/{path}", UriKind.Relative));
        }

        // A DynamicResource hands every user the dictionary's own instance, so a themed brush is the palette's brush itself
        private static void ExpectPalette (List<string> gaps, FrameworkElement host, string what, object actual, string key)
        {
            object expected = host.TryFindResource(key);
            if ((expected == null) || !ReferenceEquals(expected, actual))
            {
                gaps.Add($"{what} is {Describe(actual)}, not {key}");
            }
        }

        // A gradient built from palette colors: every stop that shows is one of them
        private static void ExpectGradientOf (List<string> gaps, FrameworkElement host, string what, Brush actual, params string[] colorKeys)
        {
            Color[] palette = colorKeys.Select(key => host.TryFindResource(key)).OfType<Color>().ToArray();
            if ((actual is not GradientBrush gradient)
                || (palette.Length != colorKeys.Length)
                || gradient.GradientStops.Any(stop => (stop.Color.A != 0) && !palette.Contains(stop.Color)))
            {
                gaps.Add($"{what} is {Describe(actual)}, not a gradient of {string.Join(" and ", colorKeys)}");
            }
        }

        private static void ExpectColor (List<string> missing, string what, Brush actual)
        {
            if (actual == null)
            {
                missing.Add(what);
            }
        }

        // What a themed style sets in a state no test can put a control in: the setter of the trigger for exactly that state
        private static void ExpectThemedTrigger (List<string> gaps, Style themed, string what, DependencyProperty property, string key, params StateCondition[] state)
        {
            if (themed == null)
            {
                gaps.Add($"{what}: the AJut theme has no style for it");
                return;
            }

            object value = ThemedTriggerSetterValue(themed, property, state);
            if (((value as DynamicResourceExtension)?.ResourceKey as string) != key)
            {
                gaps.Add($"{what} is {Describe(value)}, not {key}");
            }
        }

        private static object ThemedTriggerSetterValue (Style style, DependencyProperty property, StateCondition[] state)
        {
            for (Style current = style; current != null; current = current.BasedOn)
            {
                foreach (TriggerBase trigger in current.Triggers.Reverse())
                {
                    if (IsTriggerFor(trigger, state))
                    {
                        object value = TriggerSetters(trigger).OfType<Setter>().LastOrDefault(setter => (setter.Property == property) && (setter.TargetName == null))?.Value;
                        if (value != null)
                        {
                            return value;
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsTriggerFor (TriggerBase trigger, StateCondition[] state)
        {
            if (trigger is Trigger single)
            {
                return (state.Length == 1) && (single.Property == state[0].Property) && Equals(single.Value, state[0].Value);
            }

            if (trigger is MultiTrigger multi)
            {
                return (multi.Conditions.Count == state.Length)
                    && state.All(expected => multi.Conditions.Any(condition => (condition.Property == expected.Property) && Equals(condition.Value, expected.Value)));
            }

            return false;
        }

        private static SetterBaseCollection TriggerSetters (TriggerBase trigger)
        {
            return trigger switch
            {
                Trigger single => single.Setters,
                MultiTrigger multi => multi.Setters,
                _ => new SetterBaseCollection(),
            };
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

        private static void AssertNoGaps (List<string> gaps, string heading)
        {
            Assert.AreEqual(0, gaps.Count, $"{heading}:{Environment.NewLine}{string.Join(Environment.NewLine, gaps)}");
        }

        private static string Describe (object value)
        {
            return value switch
            {
                null => "nothing",
                SolidColorBrush solid => solid.Color.ToString(),
                GradientBrush gradient => "a gradient of " + string.Join(" ", gradient.GradientStops.Select(stop => stop.Color.ToString())),
                DynamicResourceExtension dynamicResource => $"{{DynamicResource {dynamicResource.ResourceKey}}}",
                _ => value.ToString(),
            };
        }

        private static string DescribeFocusVisual (Style style)
        {
            return (style == null) ? "no focus visual" : $"a focus visual not the theme's ({style.TargetType?.Name ?? "untyped"})";
        }

        // ===========[ Subclasses/structs ]===================================
        // IsHighlighted has a protected setter: this sets it the way pointer or keyboard movement in a drop-down would
        private sealed class HighlightableComboBoxItem : ComboBoxItem
        {
            public void SetHighlighted (bool isHighlighted) => this.IsHighlighted = isHighlighted;
        }

        // One condition of the state a trigger is for: the property and the value it has
        private sealed record StateCondition (DependencyProperty Property, object Value);

        private sealed class ListSource
        {
            [PGList]
            public List<string> Tags { get; set; } = new List<string> { "alpha", "beta" };
        }
    }
}
