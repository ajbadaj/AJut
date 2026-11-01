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
    /// and check the C# tables give the same answer for every key, in every theme and touch variant, with nothing
    /// missing or extra. A failure names each key that differs, so the fix is to bring that entry in AJut.UX\Theming up
    /// to date. WPF's touch overlays are held to the same touch values.
    /// </summary>
    [TestClass]
    public class AJutPaletteTests
    {
        private const string kAliasingFile = "AJutThemeMapAliasing.xaml";
        private const string kOverridesFile = "AJut.UX_Overrides.xaml";
        private const string kSystemAliasingFile = "SystemColorAliasing.xaml";
        private const string kUnthemedDefaultsFile = "AJutCustomControlStylingDefaults.xaml";
        private static readonly string kResourcesFolder = Path.Combine("libs", "AJut.UX.WinUI3", "Resources");
        private static readonly string kWinUI3ControlsFolder = Path.Combine("libs", "AJut.UX.WinUI3", "Controls");
        private static readonly string kWpfResourcesFolder = Path.Combine("libs", "AJut.UX.Wpf", "Resources");
        private static readonly string[] kBaseThemes = ["Light", "Dark"];
        private static readonly string[] kThemes = ["Light", "Dark", "LightTouch", "DarkTouch"];
        private static readonly XNamespace kXamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly Regex kMarkupReference = new Regex(@"^\{(?:ThemeResource|StaticResource|DynamicResource)\s+([A-Za-z0-9_]+)\s*\}$", RegexOptions.Compiled);
        private static readonly Regex kAnyMarkupReference = new Regex(@"\{(?:ThemeResource|StaticResource)\s+([A-Za-z0-9_]+)\s*\}", RegexOptions.Compiled);
        private static readonly Regex kWpfAJutKeyReference = new Regex(@"\{(?:DynamicResource|StaticResource)\s+(AJut_[A-Za-z0-9_]+)\s*\}", RegexOptions.Compiled);

        // Every stock key an AJut control can show with the AJut theme loaded, by the controls that read it. On WinUI3
        // that is the keys AJut's own templates take, and the keys the stock templates of the WinUI controls hosted in
        // them take (the Windows App SDK's generic.xaml), for every state AJut's use of them reaches. AppKit, UIKit and
        // Android read the same keys by name. Left out, because AJut never shows them: a ToggleButton's indeterminate
        // states (no AJut toggle is three state), a ComboBox's placeholder, description and editable states, disabled
        // ComboBox items (an enum's values never are), the text box description, and the ColorPicker sliders' header
        // and disabled looks (they have no header and are never disabled). The stock ListViewItem is left out too, since
        // FlatTreeListControl templates its own rows, and so are the ColorPicker's checkerboard and More button text,
        // which it takes from legacy system colors that many other stock templates share.
        private static readonly string[] kStockKeysAJutControlsRead =
        [
            // Button: WinUI3's GeometryButton and brush button templates, and the stock Button in NullableEditor,
            // PathSelectionControl, the PropertyGrid row, list editor and [PGButton] rows, and ToggleStrip's overflow
            // chevron; the brush and geometry buttons on AppKit, UIKit and Android, and AppKit's NumericEditor spinner
            "ButtonBackground", "ButtonBackgroundPointerOver", "ButtonBackgroundPressed", "ButtonBackgroundDisabled",
            "ButtonForeground", "ButtonForegroundPointerOver", "ButtonForegroundPressed", "ButtonForegroundDisabled",
            "ButtonBorderBrush", "ButtonBorderBrushPointerOver", "ButtonBorderBrushPressed", "ButtonBorderBrushDisabled",

            // AutoBrushButton's default accent, on every stack
            "SystemAccentColor",

            // WinUI3's ToggleStripButton border, Android's ToggleStrip and NumericEditor buttons, and the defaults
            // WinUI3's ToggleStrip, EnumToggleStrip, PopupMenuButton, BumpStack and FlatTreeListControl start from
            "ControlFillColorDefaultBrush", "ControlStrokeColorDefaultBrush", "TextFillColorPrimaryBrush", "TextFillColorSecondaryBrush",

            // ToggleButton: PopupMenuButton's stock ToggleButton, ColorPicker's More button, and UIKit's ToggleStrip
            "ToggleButtonBackground", "ToggleButtonBackgroundPointerOver", "ToggleButtonBackgroundPressed", "ToggleButtonBackgroundDisabled",
            "ToggleButtonBackgroundChecked", "ToggleButtonBackgroundCheckedPointerOver", "ToggleButtonBackgroundCheckedPressed", "ToggleButtonBackgroundCheckedDisabled",
            "ToggleButtonForeground", "ToggleButtonForegroundPointerOver", "ToggleButtonForegroundPressed", "ToggleButtonForegroundDisabled",
            "ToggleButtonForegroundChecked", "ToggleButtonForegroundCheckedPointerOver", "ToggleButtonForegroundCheckedPressed", "ToggleButtonForegroundCheckedDisabled",
            "ToggleButtonBorderBrush", "ToggleButtonBorderBrushPointerOver", "ToggleButtonBorderBrushPressed", "ToggleButtonBorderBrushDisabled",
            "ToggleButtonBorderBrushChecked", "ToggleButtonBorderBrushCheckedPointerOver", "ToggleButtonBorderBrushCheckedPressed", "ToggleButtonBorderBrushCheckedDisabled",

            // RepeatButton: the outline WinUI3's BumpStack bump buttons start from; they template their own states
            "RepeatButtonBorderBrush",

            // Text boxes: the ones NumericEditor and PathSelectionControl template on WinUI3, the stock TextBox in the
            // ColorPicker that ColorEditIngressControl opens, and UIKit's NumericEditor field
            "TextControlForeground", "TextControlForegroundPointerOver", "TextControlForegroundFocused", "TextControlForegroundDisabled",
            "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "TextControlBackgroundDisabled",
            "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused", "TextControlBorderBrushDisabled",
            "TextControlPlaceholderForeground", "TextControlPlaceholderForegroundPointerOver", "TextControlPlaceholderForegroundFocused",
            "TextControlHeaderForeground", "TextControlSelectionHighlightColor",
            "TextControlButtonBackground", "TextControlButtonBackgroundPointerOver", "TextControlButtonBackgroundPressed",
            "TextControlButtonForeground", "TextControlButtonForegroundPointerOver", "TextControlButtonForegroundPressed",
            "TextControlButtonBorderBrush", "TextControlButtonBorderBrushPointerOver", "TextControlButtonBorderBrushPressed",

            // ComboBox: EnumComboBox on WinUI3 and UIKit, and the ColorPicker's representation picker
            "ComboBoxBackground", "ComboBoxBackgroundPointerOver", "ComboBoxBackgroundPressed", "ComboBoxBackgroundDisabled",
            "ComboBoxBackgroundFocused", "ComboBoxBackgroundBorderBrushFocused",
            "ComboBoxForeground", "ComboBoxForegroundPointerOver", "ComboBoxForegroundPressed", "ComboBoxForegroundDisabled",
            "ComboBoxForegroundFocused", "ComboBoxForegroundFocusedPressed",
            "ComboBoxBorderBrush", "ComboBoxBorderBrushPointerOver", "ComboBoxBorderBrushPressed", "ComboBoxBorderBrushDisabled",
            "ComboBoxDropDownGlyphForeground", "ComboBoxDropDownGlyphForegroundFocused", "ComboBoxDropDownGlyphForegroundFocusedPressed", "ComboBoxDropDownGlyphForegroundDisabled",
            "ComboBoxDropDownBackground", "ComboBoxDropDownBorderBrush", "ComboBoxDropDownForeground",
            "ComboBoxItemBackground", "ComboBoxItemBackgroundPointerOver", "ComboBoxItemBackgroundPressed",
            "ComboBoxItemBackgroundSelected", "ComboBoxItemBackgroundSelectedUnfocused", "ComboBoxItemBackgroundSelectedPointerOver", "ComboBoxItemBackgroundSelectedPressed",
            "ComboBoxItemForeground", "ComboBoxItemForegroundPointerOver", "ComboBoxItemForegroundPressed",
            "ComboBoxItemForegroundSelected", "ComboBoxItemForegroundSelectedUnfocused", "ComboBoxItemForegroundSelectedPointerOver", "ComboBoxItemForegroundSelectedPressed",
            "ComboBoxItemBorderBrushPointerOver", "ComboBoxItemBorderBrushPressed",
            "ComboBoxItemBorderBrushSelected", "ComboBoxItemBorderBrushSelectedUnfocused", "ComboBoxItemBorderBrushSelectedPointerOver", "ComboBoxItemBorderBrushSelectedPressed",

            // Flyouts: ToggleStrip's overflow and ColorEditIngressControl's color flyout
            "FlyoutPresenterBackground", "FlyoutBorderThemeBrush",

            // The ColorPicker's sliders, in ColorEditIngressControl's color flyout
            "ColorPickerSliderThumbBackground", "ColorPickerSliderThumbBackgroundPointerOver", "ColorPickerSliderThumbBackgroundPressed",
            "SliderThumbBorderBrush", "SliderOuterThumbBackground",

            // Menus: the DockZone, docking and PropertyGrid row menus, and whatever items a PopupMenuButton is given
            "MenuFlyoutPresenterBackground", "MenuFlyoutPresenterBorderBrush", "MenuFlyoutSeparatorBackground",
            "MenuFlyoutItemBackground", "MenuFlyoutItemBackgroundBrush", "MenuFlyoutItemBackgroundPointerOver", "MenuFlyoutItemBackgroundPressed", "MenuFlyoutItemBackgroundDisabled",
            "MenuFlyoutItemForeground", "MenuFlyoutItemForegroundPointerOver", "MenuFlyoutItemForegroundPressed", "MenuFlyoutItemForegroundDisabled",
            "MenuFlyoutItemKeyboardAcceleratorTextForeground", "MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver",
            "MenuFlyoutItemKeyboardAcceleratorTextForegroundPressed", "MenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled",
            "MenuFlyoutSubItemBackground", "MenuFlyoutSubItemBackgroundBrush", "MenuFlyoutSubItemBackgroundPointerOver", "MenuFlyoutSubItemBackgroundPressed",
            "MenuFlyoutSubItemBackgroundSubMenuOpened", "MenuFlyoutSubItemBackgroundDisabled",
            "MenuFlyoutSubItemForeground", "MenuFlyoutSubItemForegroundPointerOver", "MenuFlyoutSubItemForegroundPressed",
            "MenuFlyoutSubItemForegroundSubMenuOpened", "MenuFlyoutSubItemForegroundDisabled",
            "MenuFlyoutSubItemChevron", "MenuFlyoutSubItemChevronPointerOver", "MenuFlyoutSubItemChevronPressed",
            "MenuFlyoutSubItemChevronSubMenuOpened", "MenuFlyoutSubItemChevronDisabled",
            "ToggleMenuFlyoutItemKeyboardAcceleratorTextForeground", "ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver",
            "ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPressed", "ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled",
            "ToggleMenuFlyoutItemCheckGlyphForeground", "ToggleMenuFlyoutItemCheckGlyphForegroundPointerOver",
            "ToggleMenuFlyoutItemCheckGlyphForegroundPressed", "ToggleMenuFlyoutItemCheckGlyphForegroundDisabled",

            // Tool tips on PathSelectionControl's buttons and the PropertyGrid's row and list buttons
            "ToolTipBackgroundBrush", "ToolTipForegroundBrush", "ToolTipBorderBrush",

            // Scroll bars: FlatTreeListControl's list, which the PropertyGrid shows its rows in
            "ScrollViewerScrollBarSeparatorBackground",
            "ScrollBarBackground", "ScrollBarBackgroundPointerOver", "ScrollBarBackgroundDisabled", "ScrollBarForeground",
            "ScrollBarBorderBrush", "ScrollBarBorderBrushPointerOver", "ScrollBarBorderBrushDisabled",
            "ScrollBarButtonBackground", "ScrollBarButtonBorderBrush",
            "ScrollBarButtonArrowForeground", "ScrollBarButtonArrowForegroundPointerOver", "ScrollBarButtonArrowForegroundPressed", "ScrollBarButtonArrowForegroundDisabled",
            "ScrollBarTrackFill", "ScrollBarTrackFillPointerOver", "ScrollBarTrackFillDisabled",
            "ScrollBarTrackStroke", "ScrollBarTrackStrokePointerOver", "ScrollBarTrackStrokeDisabled",
            "ScrollBarThumbBackground", "ScrollBarThumbBorderBrush", "ScrollBarThumbFillDisabled",
            "ScrollBarPanningThumbBackground", "ScrollBarPanningThumbBackgroundDisabled",

            // Caption buttons: TitleBarCaptionButton (ThemedWindowRootControl's full screen buttons) and the tearoff
            // window's close button
            "WindowCaptionBackground", "WindowCaptionForeground", "WindowCaptionButtonBackgroundPointerOver",
            "WindowCaptionButtonBackgroundPressed", "WindowCaptionButtonStrokePressed", "TitleBarCaptionButtonInactiveForegroundColor",
        ];

        // Every stock key the stock controls an app uses beside AJut's read, for the families no AJut control hosts, in every
        // state: the Windows App SDK's generic.xaml templates, and for TabView the template AJut replaces it with
        // (Resources\Microsoft.UI.Xaml_Overrides.xaml). Nothing of AJut's reads these, so they are a list of their own
        // rather than part of the read list above; with the AJut theme loaded, a stock control in any of these states
        // shows the AJut palette rather than half of WinUI's look.
        private static readonly string[] kStockKeysOfStockFamilies =
        [
            // CheckBox: unchecked, checked and indeterminate, each at rest, pointer over, pressed and disabled
            "CheckBoxForegroundUnchecked", "CheckBoxBackgroundUnchecked", "CheckBoxBorderBrushUnchecked",
            "CheckBoxCheckBackgroundStrokeUnchecked", "CheckBoxCheckBackgroundFillUnchecked", "CheckBoxCheckGlyphForegroundUnchecked",
            "CheckBoxForegroundUncheckedPointerOver", "CheckBoxBackgroundUncheckedPointerOver", "CheckBoxBorderBrushUncheckedPointerOver",
            "CheckBoxCheckBackgroundStrokeUncheckedPointerOver", "CheckBoxCheckBackgroundFillUncheckedPointerOver", "CheckBoxCheckGlyphForegroundUncheckedPointerOver",
            "CheckBoxForegroundUncheckedPressed", "CheckBoxBackgroundUncheckedPressed", "CheckBoxBorderBrushUncheckedPressed",
            "CheckBoxCheckBackgroundStrokeUncheckedPressed", "CheckBoxCheckBackgroundFillUncheckedPressed", "CheckBoxCheckGlyphForegroundUncheckedPressed",
            "CheckBoxForegroundUncheckedDisabled", "CheckBoxBackgroundUncheckedDisabled", "CheckBoxBorderBrushUncheckedDisabled",
            "CheckBoxCheckBackgroundStrokeUncheckedDisabled", "CheckBoxCheckBackgroundFillUncheckedDisabled", "CheckBoxCheckGlyphForegroundUncheckedDisabled",
            "CheckBoxForegroundChecked", "CheckBoxBackgroundChecked", "CheckBoxBorderBrushChecked",
            "CheckBoxCheckBackgroundStrokeChecked", "CheckBoxCheckBackgroundFillChecked", "CheckBoxCheckGlyphForegroundChecked",
            "CheckBoxForegroundCheckedPointerOver", "CheckBoxBackgroundCheckedPointerOver", "CheckBoxBorderBrushCheckedPointerOver",
            "CheckBoxCheckBackgroundStrokeCheckedPointerOver", "CheckBoxCheckBackgroundFillCheckedPointerOver", "CheckBoxCheckGlyphForegroundCheckedPointerOver",
            "CheckBoxForegroundCheckedPressed", "CheckBoxBackgroundCheckedPressed", "CheckBoxBorderBrushCheckedPressed",
            "CheckBoxCheckBackgroundStrokeCheckedPressed", "CheckBoxCheckBackgroundFillCheckedPressed", "CheckBoxCheckGlyphForegroundCheckedPressed",
            "CheckBoxForegroundCheckedDisabled", "CheckBoxBackgroundCheckedDisabled", "CheckBoxBorderBrushCheckedDisabled",
            "CheckBoxCheckBackgroundStrokeCheckedDisabled", "CheckBoxCheckBackgroundFillCheckedDisabled", "CheckBoxCheckGlyphForegroundCheckedDisabled",
            "CheckBoxForegroundIndeterminate", "CheckBoxBackgroundIndeterminate", "CheckBoxBorderBrushIndeterminate",
            "CheckBoxCheckBackgroundStrokeIndeterminate", "CheckBoxCheckBackgroundFillIndeterminate", "CheckBoxCheckGlyphForegroundIndeterminate",
            "CheckBoxForegroundIndeterminatePointerOver", "CheckBoxBackgroundIndeterminatePointerOver", "CheckBoxBorderBrushIndeterminatePointerOver",
            "CheckBoxCheckBackgroundStrokeIndeterminatePointerOver", "CheckBoxCheckBackgroundFillIndeterminatePointerOver", "CheckBoxCheckGlyphForegroundIndeterminatePointerOver",
            "CheckBoxForegroundIndeterminatePressed", "CheckBoxBackgroundIndeterminatePressed", "CheckBoxBorderBrushIndeterminatePressed",
            "CheckBoxCheckBackgroundStrokeIndeterminatePressed", "CheckBoxCheckBackgroundFillIndeterminatePressed", "CheckBoxCheckGlyphForegroundIndeterminatePressed",
            "CheckBoxForegroundIndeterminateDisabled", "CheckBoxBackgroundIndeterminateDisabled", "CheckBoxBorderBrushIndeterminateDisabled",
            "CheckBoxCheckBackgroundStrokeIndeterminateDisabled", "CheckBoxCheckBackgroundFillIndeterminateDisabled", "CheckBoxCheckGlyphForegroundIndeterminateDisabled",

            // RadioButton: at rest, pointer over, pressed and disabled, and the checked glyph's outline
            "RadioButtonForeground", "RadioButtonBackground", "RadioButtonBorderBrush", "RadioButtonOuterEllipseStroke", "RadioButtonOuterEllipseFill",
            "RadioButtonOuterEllipseCheckedStroke", "RadioButtonOuterEllipseCheckedFill", "RadioButtonCheckGlyphFill", "RadioButtonCheckGlyphStroke",
            "RadioButtonForegroundPointerOver", "RadioButtonBackgroundPointerOver", "RadioButtonBorderBrushPointerOver", "RadioButtonOuterEllipseStrokePointerOver",
            "RadioButtonOuterEllipseFillPointerOver", "RadioButtonOuterEllipseCheckedStrokePointerOver", "RadioButtonOuterEllipseCheckedFillPointerOver",
            "RadioButtonCheckGlyphFillPointerOver", "RadioButtonCheckGlyphStrokePointerOver",
            "RadioButtonForegroundPressed", "RadioButtonBackgroundPressed", "RadioButtonBorderBrushPressed", "RadioButtonOuterEllipseStrokePressed",
            "RadioButtonOuterEllipseFillPressed", "RadioButtonOuterEllipseCheckedStrokePressed", "RadioButtonOuterEllipseCheckedFillPressed",
            "RadioButtonCheckGlyphFillPressed", "RadioButtonCheckGlyphStrokePressed",
            "RadioButtonForegroundDisabled", "RadioButtonBackgroundDisabled", "RadioButtonBorderBrushDisabled", "RadioButtonOuterEllipseStrokeDisabled",
            "RadioButtonOuterEllipseFillDisabled", "RadioButtonOuterEllipseCheckedStrokeDisabled", "RadioButtonOuterEllipseCheckedFillDisabled",
            "RadioButtonCheckGlyphFillDisabled", "RadioButtonCheckGlyphStrokeDisabled", "RadioButtonCheckGlyphStrokeChecked",

            // ToggleSwitch: off and on, at rest, pointer over, pressed and disabled, and its header and content text
            "ToggleSwitchHeaderForeground", "ToggleSwitchContentForeground", "ToggleSwitchContainerBackground",
            "ToggleSwitchStrokeOff", "ToggleSwitchFillOff", "ToggleSwitchKnobFillOff", "ToggleSwitchStrokeOn", "ToggleSwitchFillOn", "ToggleSwitchKnobFillOn", "ToggleSwitchKnobStrokeOn",
            "ToggleSwitchContainerBackgroundPointerOver", "ToggleSwitchStrokeOffPointerOver", "ToggleSwitchFillOffPointerOver", "ToggleSwitchKnobFillOffPointerOver",
            "ToggleSwitchStrokeOnPointerOver", "ToggleSwitchFillOnPointerOver", "ToggleSwitchKnobFillOnPointerOver",
            "ToggleSwitchContainerBackgroundPressed", "ToggleSwitchStrokeOffPressed", "ToggleSwitchFillOffPressed", "ToggleSwitchKnobFillOffPressed",
            "ToggleSwitchStrokeOnPressed", "ToggleSwitchFillOnPressed", "ToggleSwitchKnobFillOnPressed",
            "ToggleSwitchHeaderForegroundDisabled", "ToggleSwitchContentForegroundDisabled", "ToggleSwitchContainerBackgroundDisabled",
            "ToggleSwitchStrokeOffDisabled", "ToggleSwitchFillOffDisabled", "ToggleSwitchKnobFillOffDisabled",
            "ToggleSwitchStrokeOnDisabled", "ToggleSwitchFillOnDisabled", "ToggleSwitchKnobFillOnDisabled",

            // Expander: the content, and the header's text, border and chevron at rest, pointer over, pressed and disabled
            "ExpanderContentBackground", "ExpanderContentBorderBrush", "ExpanderHeaderBackground",
            "ExpanderHeaderForeground", "ExpanderHeaderBorderBrush", "ExpanderChevronForeground", "ExpanderChevronBackground", "ExpanderChevronBorderBrush",
            "ExpanderHeaderForegroundPointerOver", "ExpanderHeaderBorderPointerOverBrush",
            "ExpanderChevronPointerOverForeground", "ExpanderChevronPointerOverBackground", "ExpanderChevronBorderPointerOverBrush",
            "ExpanderHeaderForegroundPressed", "ExpanderHeaderBorderPressedBrush",
            "ExpanderChevronPressedForeground", "ExpanderChevronPressedBackground", "ExpanderChevronBorderPressedBrush",
            "ExpanderHeaderDisabledForeground", "ExpanderHeaderDisabledBorderBrush",

            // DropDownButton's chevron; the button itself takes the Button keys
            "DropDownButtonForegroundSecondary", "DropDownButtonForegroundSecondaryPointerOver", "DropDownButtonForegroundSecondaryPressed",

            // MenuBar and its items; the menus they open take the MenuFlyout keys
            "MenuBarBackground", "MenuBarItemForeground", "MenuBarItemBackground", "MenuBarItemBorderBrush",
            "MenuBarItemBackgroundPointerOver", "MenuBarItemBorderBrushPointerOver", "MenuBarItemBackgroundPressed", "MenuBarItemBorderBrushPressed",
            "MenuBarItemBackgroundSelected", "MenuBarItemBorderBrushSelected",

            // RepeatButton: at rest, pointer over, pressed and disabled, for an app's own and NumberBox's spin buttons. Its
            // outline at rest is in the read list, as the one WinUI3's BumpStack bump buttons start from.
            "RepeatButtonBackground", "RepeatButtonBackgroundPointerOver", "RepeatButtonBackgroundPressed", "RepeatButtonBackgroundDisabled",
            "RepeatButtonForeground", "RepeatButtonForegroundPointerOver", "RepeatButtonForegroundPressed", "RepeatButtonForegroundDisabled",
            "RepeatButtonBorderBrushPointerOver", "RepeatButtonBorderBrushPressed", "RepeatButtonBorderBrushDisabled",

            // NumberBox, beyond the text box and repeat button keys it shares: its disabled header and placeholder, its
            // description, and its spin button popup
            "TextControlHeaderForegroundDisabled", "TextControlPlaceholderForegroundDisabled", "SystemControlDescriptionTextForegroundBrush",
            "NumberBoxPopupBackground", "NumberBoxPopupBorderBrush", "NumberBoxPopupIndicatorForeground",

            // NavigationView: the selected item, at rest, under the pointer and pressed
            "NavigationViewItemBackgroundSelected", "NavigationViewItemBackgroundSelectedPointerOver", "NavigationViewItemBackgroundSelectedPressed",

            // TabView, as AJut's own template draws it: the strip, its add and scroll buttons, and each tab with its close button
            "TabViewBorderBrush", "TabViewItemSeparator", "TabViewSelectedItemBorderBrush",
            "TabViewButtonBackground", "TabViewButtonForeground", "TabViewButtonBorderBrush",
            "TabViewButtonBackgroundPointerOver", "TabViewButtonForegroundPointerOver", "TabViewButtonBorderBrushPointerOver",
            "TabViewButtonBackgroundPressed", "TabViewButtonForegroundPressed", "TabViewButtonBorderBrushPressed",
            "TabViewButtonBackgroundDisabled", "TabViewButtonForegroundDisabled", "TabViewButtonBorderBrushDisabled",
            "TabViewScrollButtonBackground", "TabViewScrollButtonForeground", "TabViewScrollButtonBorderBrush",
            "TabViewScrollButtonBackgroundPointerOver", "TabViewScrollButtonForegroundPointerOver", "TabViewScrollButtonBorderBrushPointerOver",
            "TabViewScrollButtonBackgroundPressed", "TabViewScrollButtonForegroundPressed", "TabViewScrollButtonBorderBrushPressed",
            "TabViewScrollButtonBackgroundDisabled", "TabViewScrollButtonForegroundDisabled", "TabViewScrollButtonBorderBrushDisabled",
            "TabViewItemBorderBrush", "TabViewItemHeaderDragBackground",
            "TabViewItemHeaderBackground", "TabViewItemHeaderBackgroundPointerOver", "TabViewItemHeaderBackgroundPressed",
            "TabViewItemHeaderBackgroundSelected", "TabViewItemHeaderBackgroundDisabled",
            "TabViewItemHeaderForeground", "TabViewItemHeaderForegroundPointerOver", "TabViewItemHeaderForegroundPressed",
            "TabViewItemHeaderForegroundSelected", "TabViewItemHeaderForegroundDisabled",
            "TabViewItemIconForeground", "TabViewItemIconForegroundPointerOver", "TabViewItemIconForegroundPressed", "TabViewItemIconForegroundSelected",
            "TabViewItemHeaderCloseButtonBackground", "TabViewItemHeaderCloseButtonForeground", "TabViewItemHeaderCloseButtonBorderBrush",
            "TabViewItemHeaderCloseButtonBackgroundPointerOver", "TabViewItemHeaderCloseButtonForegroundPointerOver", "TabViewItemHeaderCloseButtonBorderBrushPointerOver",
            "TabViewItemHeaderCloseButtonBackgroundPressed", "TabViewItemHeaderCloseButtonForegroundPressed", "TabViewItemHeaderCloseButtonBorderBrushPressed",
            "TabViewItemHeaderCloseButtonBorderBrushDisabled",
            "TabViewItemHeaderPointerOverCloseButtonBackground", "TabViewItemHeaderPointerOverCloseButtonForeground",
            "TabViewItemHeaderPressedCloseButtonBackground", "TabViewItemHeaderPressedCloseButtonForeground",
            "TabViewItemHeaderSelectedCloseButtonBackground", "TabViewItemHeaderSelectedCloseButtonForeground",
            "TabViewItemHeaderDisabledCloseButtonBackground", "TabViewItemHeaderDisabledCloseButtonForeground",
        ];

        // Stock keys the Windows App SDK's templates read where only a Color will do: ToggleSwitch animates its track and
        // container with color key frames, and generic.xaml builds brushes from the accent colors. A brush mapped to one
        // of these fails when WinUI applies it, so the AJut theme has to map each to a Color.
        private static readonly string[] kStockKeysReadAsColors =
        [
            "ToggleSwitchContainerBackgroundPointerOver", "ToggleSwitchContainerBackgroundPressed", "ToggleSwitchContainerBackgroundDisabled",
            "ToggleSwitchFillOffPointerOver", "ToggleSwitchFillOffPressed", "ToggleSwitchFillOffDisabled",
            "ToggleSwitchStrokeOffPointerOver", "ToggleSwitchStrokeOffPressed", "ToggleSwitchStrokeOffDisabled",
            "SystemAccentColor", "SystemAccentColorDark1", "SystemAccentColorLight1", "SystemAccentColorLight2",
        ];

        // Stock keys the AJut theme maps that the Windows App SDK 1.8's generic.xaml reads outside the families and
        // controls the lists above cover: the title bar, the window caption, and a few fills and accents shared across
        // its templates
        private static readonly string[] kStockKeysReadElsewhereByWinUI =
        [
            "AccentFillColorDefaultBrush", "ControlFillColorInputActiveBrush", "ControlFillColorSecondaryBrush", "SubtleFillColorSecondaryBrush",
            "DefaultTextForegroundThemeBrush", "ExpanderBorderBrush", "TabViewBackground",
            "TitleBarForegroundBrush", "TitleBarDeactivatedForegroundBrush",
            "WindowCaptionButtonBackground", "WindowCaptionButtonStrokePointerOver", "WindowCaptionForegroundDisabled",
        ];

        // Stock keys the AJut theme maps on purpose although nothing in the Windows App SDK 1.8 reads them: the window
        // caption keys (WinUI has read them in other versions, and an app's own title bar may), TextBoxForegroundThemeBrush
        // (the legacy text box foreground name), and the general system color names an app may read itself
        private static readonly string[] kStockKeysMappedThoughUnread =
        [
            "WindowCaptionBackgroundDisabled", "WindowCaptionButtonStroke",
            "CloseButtonBackgroundPointerOver", "CloseButtonStrokePointerOver", "CloseButtonBackgroundPressed", "CloseButtonStrokePressed",
            "TitleBarCaptionButtonForegroundColor", "TitleBarCaptionButtonHoverForegroundColor", "TitleBarCaptionButtonPressedForegroundColor",
            "TextBoxForegroundThemeBrush",
            "SystemAccentColor2", "SystemAccentColor2Dark1", "SystemAccentColor2Light1", "SystemAccentColor2Light2",
            "SystemListAccentLowColor", "SystemListAccentMediumColor", "SystemListAccentHighColor",
            "SystemListLowBrush", "SystemListMediumBrush", "SystemListAccentMediumBrush", "SystemChromeBlackHighBrush",
        ];

        // Resources AJut's TabView template (Resources\Microsoft.UI.Xaml_Overrides.xaml) takes from WinUI that are not colors
        private static readonly string[] kWinUI3TabViewKeysThatAreNotColors =
        [
            "ListViewItemDragThemeOpacity", "ListViewItemReorderHintThemeOffset", "ListViewItemReorderTargetThemeOpacity", "ListViewItemReorderThemeOpacity",
            "OverlayCornerRadius", "SymbolThemeFontFamily", "TopCornerRadiusFilterConverter",
            "TabViewButtonBorderThickness", "TabViewHeaderPadding", "TabViewItemAddButtonContainerPadding", "TabViewItemAddButtonFontSize",
            "TabViewItemAddButtonHeight", "TabViewItemAddButtonWidth", "TabViewItemBorderThickness", "TabViewItemHeaderCloseButtonBorderThickness",
            "TabViewItemHeaderCloseButtonHeight", "TabViewItemHeaderCloseButtonWidth", "TabViewItemHeaderCloseFontSize", "TabViewItemHeaderCloseMargin",
            "TabViewItemHeaderFontSize", "TabViewItemHeaderIconMargin", "TabViewItemHeaderIconSize", "TabViewItemHeaderPadding",
            "TabViewItemLeftScrollButtonContainerPadding", "TabViewItemMinHeight", "TabViewItemRightScrollButtonContainerPadding",
            "TabViewItemScrollButonFontSize", "TabViewItemScrollButtonHeight", "TabViewItemScrollButtonWidth", "TabViewItemSeparatorMargin",
            "TabViewSelectedItemBorderThickness", "TabViewSelectedItemHeaderMargin", "TabViewSelectedItemHeaderPadding",
        ];

        // Resources AJut's WinUI3 control templates take from outside AJut that are not colors
        private static readonly string[] kWinUI3TemplateKeysThatAreNotColors =
        [
            "ButtonBorderThemeThickness", "ButtonPadding", "ContentControlThemeFontFamily", "ControlContentThemeFontSize",
            "ControlCornerRadius", "TextBoxTopHeaderMargin", "UseSystemFocusVisuals",
        ];

        // Colors in AJut's WinUI3 templates that are never shown: the text box description in NumericEditor and
        // PathSelectionControl, which neither sets
        private static readonly string[] kWinUI3TemplateKeysNeverShown = ["SystemControlDescriptionTextForegroundBrush"];

        // Colors AJut's WinUI3 templates take only with no theme loaded: with the AJut theme loaded, the themed style
        // named here sets the property to an AJut key instead
        private static readonly UnthemedOnlyKey[] kWinUI3TemplateKeysOnlyUnthemed =
        [
            new UnthemedOnlyKey("ApplicationPageBackgroundThemeBrush", "AllControlThemeOverrides.xaml", "DockTearoffContainerPanel", "Background"),
            new UnthemedOnlyKey("SolidBackgroundFillColorBaseBrush", "AllControlThemeOverrides.xaml", "BumpStack", "ButtonBackground"),
            new UnthemedOnlyKey("LayerOnMicaBaseAltFillColorTransparentBrush", "AJut.UX_Overrides.xaml", "ThemedWindowRootControl", "ActiveTitleBarBackground"),
            new UnthemedOnlyKey("LayerOnMicaBaseAltFillColorTransparentBrush", "AJut.UX_Overrides.xaml", "ThemedWindowRootControl", "InactiveTitlebarBackground"),
            new UnthemedOnlyKey("ControlFillColorDisabledBrush", "AllControlThemeOverrides.xaml", "ToggleStrip", "DisabledBackground"),
            new UnthemedOnlyKey("ControlFillColorDisabledBrush", "AllControlThemeOverrides.xaml", "EnumToggleStrip", "DisabledBackground"),
            new UnthemedOnlyKey("TextFillColorDisabledBrush", "AllControlThemeOverrides.xaml", "ToggleStrip", "DisabledForeground"),
            new UnthemedOnlyKey("TextFillColorDisabledBrush", "AllControlThemeOverrides.xaml", "EnumToggleStrip", "DisabledForeground"),
        ];

        // Windows' high contrast system colors, which the high contrast unthemed defaults take the way WinUI's own high
        // contrast brushes do. With the AJut theme loaded, AJutThemeMapAliasing.xaml defines the AJut keys they fill.
        private static readonly string[] kWinUI3UnthemedHighContrastKeys =
        [
            "SystemColorButtonTextColor", "SystemColorGrayTextColor", "SystemColorHighlightColor", "SystemColorHighlightTextColor",
        ];

        // ===========[ Values ]===================================
        [TestMethod]
        public void Palette_EveryKey_ResolvesAsTheXamlDoes_Light () => AssertEveryKeyResolves("Light");

        [TestMethod]
        public void Palette_EveryKey_ResolvesAsTheXamlDoes_Dark () => AssertEveryKeyResolves("Dark");

        [TestMethod]
        public void Palette_EveryKey_ResolvesAsTheXamlDoes_LightTouch () => AssertEveryKeyResolves("LightTouch");

        [TestMethod]
        public void Palette_EveryKey_ResolvesAsTheXamlDoes_DarkTouch () => AssertEveryKeyResolves("DarkTouch");

        [TestMethod]
        public void Palette_Numbers_MatchTheXaml ()
        {
            var gaps = new List<string>();
            foreach (string theme in kThemes)
            {
                AJutPalette.Table table = TableFor(theme);
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
                HashSet<string> actual = TableFor(theme).Keys.ToHashSet();
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
        public void TryGetColor_IsFalseForGradientsUnknownKeysAndNumbers ()
        {
            AJutPalette.Table table = AJutPalette.Dark;
            string[] gradientKeys = table.Keys.Where(key => table.TryGetPaint(key, out AJutThemePaint? paint) && paint.IsGradient).ToArray();
            Assert.IsTrue(gradientKeys.Length > 0, "TryGetPaint gives a gradient for some of the palette's keys");
            foreach (string key in gradientKeys)
            {
                Assert.IsFalse(table.TryGetColor(key, out _), $"{key} is a gradient, not a solid color");
            }

            Assert.IsFalse(table.TryGetColor("AJut_NoSuchKey", out _));
            Assert.IsFalse(table.TryGetColor("AJut_Double_StandardDisabledOpacity", out _));
        }

        // ===========[ What AJut controls read ]===================================
        [TestMethod]
        public void ThemeKeys_EveryKey_ResolvesToAnAJutValueInEveryTheme ()
        {
            // A key the theme maps has to land on an AJut value. One that aliases a key the palette does not have (a
            // stock brush) shows the stock look with the theme loaded, the same as leaving it unmapped.
            var gaps = new List<string>();
            foreach (string key in AJutThemeKeys.All.OrderBy(k => k, StringComparer.Ordinal))
            {
                string[] missingIn = kThemes.Where(theme => !TableFor(theme).TryGetPaint(key, out _)).ToArray();
                if (missingIn.Length > 0)
                {
                    string target = AJutThemeKeys.TryGetAlias(key, out string? alias) ? $"aliases {alias}" : "has no value";
                    gaps.Add($"{key} {target}, unresolved in {string.Join(", ", missingIn)}");
                }
            }

            AssertNoGaps(gaps, "Mapped keys that do not resolve to an AJut value");
        }

        [TestMethod]
        public void StockKeys_EveryOneAnAJutControlReads_ResolvesInEveryTheme ()
        {
            var gaps = new List<string>();
            foreach (string key in kStockKeysAJutControlsRead)
            {
                string[] missingIn = kThemes.Where(theme => !TableFor(theme).TryGetPaint(key, out _)).ToArray();
                if (missingIn.Length > 0)
                {
                    gaps.Add($"{key}, unresolved in {string.Join(", ", missingIn)}");
                }
            }

            AssertNoGaps(gaps, "Stock keys AJut controls read that the AJut theme leaves unmapped");
        }

        [TestMethod]
        public void StockKeys_ReadList_HasNoRepeats ()
        {
            string[] repeated = kStockKeysAJutControlsRead.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            Assert.AreEqual(0, repeated.Length, "Listed more than once: " + string.Join(", ", repeated));
        }

        [TestMethod]
        public void StockKeys_AJutWinUI3TemplatesTake_AreAccountedFor ()
        {
            // Every key AJut.UX.WinUI3's control templates take from outside AJut has to be on the read list, so the
            // test above holds it to the theme, or be named here as not a color, never shown, or replaced by a themed
            // style. A stock brush added to a template without a theme mapping fails here.
            string controlsFolder = ResourcesRoot(Path.Combine("libs", "AJut.UX.WinUI3", "Controls"));
            string[] templateFiles = Directory.GetFiles(controlsFolder, "*.xaml");
            HashSet<string> definedInControls = templateFiles.SelectMany(DefinedKeys).ToHashSet();

            var accountedFor = new HashSet<string>(kStockKeysAJutControlsRead);
            accountedFor.UnionWith(kWinUI3TemplateKeysThatAreNotColors);
            accountedFor.UnionWith(kWinUI3TemplateKeysNeverShown);
            accountedFor.UnionWith(kWinUI3TemplateKeysOnlyUnthemed.Select(k => k.Key));
            accountedFor.UnionWith(kWinUI3UnthemedHighContrastKeys);

            var gaps = new List<string>();
            foreach (string file in templateFiles)
            {
                foreach (Match reference in kAnyMarkupReference.Matches(File.ReadAllText(file)))
                {
                    string key = reference.Groups[1].Value;
                    if (!key.StartsWith("AJut_", StringComparison.Ordinal) && !definedInControls.Contains(key) && !accountedFor.Contains(key))
                    {
                        gaps.Add($"{Path.GetFileName(file)} takes {key}");
                    }
                }
            }

            AssertNoGaps(gaps.Distinct().ToList(), "Keys AJut's WinUI3 templates take that the theme does not account for");
        }

        [TestMethod]
        public void StockKeys_OnlyUnthemed_AreReplacedByTheThemedStyles ()
        {
            var gaps = new List<string>();
            foreach (UnthemedOnlyKey unthemed in kWinUI3TemplateKeysOnlyUnthemed)
            {
                XDocument doc = XDocument.Load(Path.Combine(ResourcesRoot(kResourcesFolder), unthemed.ThemedStyleFile));
                string? value = doc.Root!.Elements()
                    .Where(e => e.Name.LocalName == "Style" && e.Attribute("TargetType")!.Value.Split(':').Last() == unthemed.Control)
                    .SelectMany(style => style.Elements())
                    .Where(setter => setter.Attribute("Property")?.Value == unthemed.Property)
                    .Select(setter => setter.Attribute("Value")?.Value)
                    .FirstOrDefault();

                Match reference = kMarkupReference.Match(value ?? string.Empty);
                if (!reference.Success || !reference.Groups[1].Value.StartsWith("AJut_", StringComparison.Ordinal))
                {
                    gaps.Add($"{unthemed.ThemedStyleFile} does not set {unthemed.Control}.{unthemed.Property} to an AJut key, so {unthemed.Key} shows with the theme loaded");
                }
            }

            AssertNoGaps(gaps, "Unthemed defaults the themed styles do not replace");
        }

        // ===========[ Stock families nothing of AJut's reads ]===================================
        [TestMethod]
        public void StockKeys_OfStockFamilies_ResolveInEveryTheme ()
        {
            var gaps = new List<string>();
            foreach (string key in kStockKeysOfStockFamilies)
            {
                string[] missingIn = kThemes.Where(theme => !TableFor(theme).TryGetPaint(key, out _)).ToArray();
                if (missingIn.Length > 0)
                {
                    gaps.Add($"{key}, unresolved in {string.Join(", ", missingIn)}");
                }
            }

            AssertNoGaps(gaps, "Stock keys of stock control families that the AJut theme leaves unmapped");
        }

        [TestMethod]
        public void StockKeys_FamilyList_HasNoRepeats_AndNothingFromTheReadList ()
        {
            string[] repeated = kStockKeysOfStockFamilies.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            Assert.AreEqual(0, repeated.Length, "Listed more than once: " + string.Join(", ", repeated));

            string[] onBoth = kStockKeysOfStockFamilies.Intersect(kStockKeysAJutControlsRead).ToArray();
            Assert.AreEqual(0, onBoth.Length, "On the read list as well, where they belong alone: " + string.Join(", ", onBoth));
        }

        [TestMethod]
        public void StockKeys_EveryOneMapped_IsReadOrKeptOnPurpose ()
        {
            // A mapping nothing reads does nothing, and reads as though the theme covers a state it doesn't. Each stock key
            // the AJut theme maps is read by an AJut control, by a stock control family, by generic.xaml elsewhere, or
            // is on the short list of keys kept on purpose.
            var accountedFor = new HashSet<string>(kStockKeysAJutControlsRead
                .Concat(kStockKeysOfStockFamilies)
                .Concat(kStockKeysReadAsColors)
                .Concat(kWinUI3TemplateKeysThatAreNotColors)
                .Concat(kStockKeysReadElsewhereByWinUI)
                .Concat(kStockKeysMappedThoughUnread)
            );

            List<string> gaps = TopLevelEntries(Path.Combine(ResourcesRoot(kResourcesFolder), kSystemAliasingFile)).Keys
                .Where(key => !accountedFor.Contains(key))
                .OrderBy(key => key)
                .Select(key => $"{key}, which nothing in the Windows App SDK 1.8 or AJut reads")
                .ToList();

            AssertNoGaps(gaps, "Stock keys the AJut theme maps that nothing reads");
        }

        [TestMethod]
        public void StockKeys_ReadAsColors_MapToColors ()
        {
            // Follows each key's StaticResource chain through the stock aliasing and the palette it is merged with, in
            // the light and the dark theme, and asks that it end on a <Color>
            const int kMaxAliasDepth = 8;
            var gaps = new List<string>();
            Dictionary<string, XElement> aliasing = TopLevelEntries(Path.Combine(ResourcesRoot(kResourcesFolder), kSystemAliasingFile));
            foreach (string theme in kBaseThemes)
            {
                Dictionary<string, XElement> palette = TopLevelEntries(Path.Combine(ResourcesRoot(kResourcesFolder), $"{theme}ThemeColorsBase.xaml"));
                foreach (string key in kStockKeysReadAsColors)
                {
                    string current = key;
                    string ends = "unmapped";
                    for (int depth = 0; depth < kMaxAliasDepth; ++depth)
                    {
                        if (!aliasing.TryGetValue(current, out XElement? entry) && !palette.TryGetValue(current, out entry))
                        {
                            break;
                        }

                        ends = $"a <{entry.Name.LocalName}>";
                        if (entry.Name.LocalName != "StaticResource")
                        {
                            break;
                        }

                        current = entry.Attribute("ResourceKey")!.Value;
                    }

                    if (ends != "a <Color>")
                    {
                        gaps.Add($"{key} ends on {ends} in {theme}");
                    }
                }
            }

            AssertNoGaps(gaps, "Stock keys WinUI reads as colors that the AJut theme does not map to a Color");
        }

        [TestMethod]
        public void StockKeys_AJutTabViewTemplateTakes_AreAccountedFor ()
        {
            // Every key AJut's TabView template takes from WinUI has to be on one of the lists, so the tests above hold it
            // to the theme, or be named as not a color. A stock brush added to the template without a theme mapping
            // fails here.
            string overrides = Path.Combine(ResourcesRoot(kResourcesFolder), "Microsoft.UI.Xaml_Overrides.xaml");
            HashSet<string> defined = DefinedKeys(overrides);
            var accountedFor = new HashSet<string>(kStockKeysAJutControlsRead);
            accountedFor.UnionWith(kStockKeysOfStockFamilies);
            accountedFor.UnionWith(kWinUI3TemplateKeysThatAreNotColors);
            accountedFor.UnionWith(kWinUI3TabViewKeysThatAreNotColors);

            string[] gaps = kAnyMarkupReference.Matches(File.ReadAllText(overrides))
                .Select(reference => reference.Groups[1].Value)
                .Where(key => !key.StartsWith("AJut_", StringComparison.Ordinal) && !defined.Contains(key) && !accountedFor.Contains(key))
                .Distinct()
                .OrderBy(k => k)
                .ToArray();

            Assert.AreEqual(0, gaps.Length, "Keys AJut's TabView template takes that the theme does not account for: " + string.Join(", ", gaps));
        }

        // ===========[ Touch palettes ]===================================
        [TestMethod]
        public void For_IsTouch_PicksTheTouchTable ()
        {
            Assert.AreSame(AJutPalette.LightTouch, AJutPalette.For(isDark: false, isTouch: true));
            Assert.AreSame(AJutPalette.DarkTouch, AJutPalette.For(isDark: true, isTouch: true));
            Assert.AreSame(AJutPalette.Light, AJutPalette.For(isDark: false, isTouch: false));
            Assert.AreSame(AJutPalette.Dark, AJutPalette.For(isDark: true, isTouch: false));
        }

        [TestMethod]
        public void TouchPalettes_MergeTheirBase_AndOnlyRestateKeysItDefines ()
        {
            var gaps = new List<string>();
            foreach (string theme in kBaseThemes)
            {
                List<PaletteLayer> layers = ReadPaletteLayers(theme + "Touch");
                Assert.AreEqual(2, layers.Count, $"{theme}TouchThemeColorsBase.xaml merges one palette");
                Assert.AreEqual($"{theme}ThemeColorsBase.xaml", layers[1].FileName, $"{theme}TouchThemeColorsBase.xaml merges the {theme.ToLowerInvariant()} palette");
                gaps.AddRange(layers[0].Entries.Keys.Except(layers[1].Entries.Keys).OrderBy(k => k).Select(k => $"{theme} touch adds {k}, which the {theme.ToLowerInvariant()} palette does not define"));
            }

            AssertNoGaps(gaps, "Touch palette keys that are not their base palette's");
        }

        /// <summary>
        /// A GeometryButton draws a glyph that is only a stroke with its trace, so a trace that goes clear makes the glyph
        /// vanish, with nothing to say so. Touch takes button borders off, which is why the trace has keys of its own.
        /// </summary>
        [TestMethod]
        public void GeometryButtonTraces_AreVisibleInEveryTheme ()
        {
            string[] traceKeys =
            [
                AJutThemeKeys.GeometryButton_Trace, AJutThemeKeys.GeometryButton_TracePointerOver,
                AJutThemeKeys.GeometryButton_TracePressed, AJutThemeKeys.GeometryButton_TraceDisabled,
            ];

            var gaps = new List<string>();
            foreach (string theme in kThemes)
            {
                foreach (string key in traceKeys)
                {
                    if (!TableFor(theme).TryGetColor(key, out ColorArgb trace))
                    {
                        gaps.Add($"{theme} {key}: not one color");
                    }
                    else if (trace.A == 0)
                    {
                        gaps.Add($"{theme} {key}: clear");
                    }
                }
            }

            AssertNoGaps(gaps, "GeometryButton traces that do not show");
        }

        [TestMethod]
        public void TouchThemeColors_GivesEachThemeItsTouchPaletteAndTheStockAliasing ()
        {
            // What AppThemeManager.IsTouchFocused adds on WinUI3: each theme dictionary carries the same pair an app's
            // own does (its palette, then the stock key aliasing), with the touch palette in place of the base one
            XDocument doc = XDocument.Load(Path.Combine(ResourcesRoot(kResourcesFolder), "TouchThemeColors.xaml"));
            XNamespace presentation = doc.Root!.Name.Namespace;
            XElement[] themes = doc.Root.Element(presentation + "ResourceDictionary.ThemeDictionaries")!.Elements().ToArray();
            CollectionAssert.AreEquivalent(kBaseThemes, themes.Select(t => t.Attribute(kXamlNamespace + "Key")!.Value).ToArray(), "A light and a dark theme dictionary");
            foreach (XElement theme in themes)
            {
                string name = theme.Attribute(kXamlNamespace + "Key")!.Value;
                string[] merged = theme.Element(presentation + "ResourceDictionary.MergedDictionaries")!.Elements()
                    .Select(d => Path.GetFileName(d.Attribute("Source")!.Value))
                    .ToArray();
                CollectionAssert.AreEqual(new[] { $"{name}TouchThemeColorsBase.xaml", kSystemAliasingFile }, merged, $"The {name.ToLowerInvariant()} theme dictionary");
            }
        }

        // ===========[ WinUI3 unthemed defaults ]===================================
        [TestMethod]
        public void UnthemedDefaults_NeutralFile_TakesNoOutsideResource ()
        {
            // A StaticResource to a WinUI theme resource resolves once, in whatever theme is current when the dictionary
            // loads, so an unthemed default that aliases one keeps that theme's look after the app switches between
            // light and dark. The theme-neutral defaults take only literals and their own keys; a default that follows
            // WinUI's theme colors belongs in the per-theme defaults.
            string neutralPath = Path.Combine(ResourcesRoot(kWinUI3ControlsFolder), kUnthemedDefaultsFile);
            HashSet<string> own = DefinedKeys(neutralPath);
            XDocument neutral = XDocument.Load(neutralPath);
            IEnumerable<string> elementReferences = neutral.Descendants()
                .Where(element => element.Name.LocalName == "StaticResource")
                .Select(element => element.Attribute("ResourceKey")!.Value);
            IEnumerable<string> markupReferences = neutral.Descendants()
                .SelectMany(element => element.Attributes())
                .SelectMany(attribute => kAnyMarkupReference.Matches(attribute.Value).Select(reference => reference.Groups[1].Value));

            string[] outside = elementReferences.Concat(markupReferences).Where(key => !own.Contains(key)).Distinct().OrderBy(k => k).ToArray();
            Assert.AreEqual(0, outside.Length, $"{kUnthemedDefaultsFile} takes resources from outside it, which do not follow a theme switch: " + string.Join(", ", outside));
        }

        [TestMethod]
        public void UnthemedDefaults_HaveADictionaryPerTheme ()
        {
            // The defaults file gives each theme WinUI defines (Default is dark) that theme's own defaults through its own
            // theme dictionaries, so they come with the one file wherever it is merged: Generic.xaml, and App.Resources in
            // an app with no AJut theme, where GoToState's {ThemeResource} lookups go. Generic.xaml merges only that file.
            XDocument generic = XDocument.Load(Path.Combine(ResourcesRoot(Path.Combine("libs", "AJut.UX.WinUI3", "Themes")), "Generic.xaml"));
            XNamespace presentation = generic.Root!.Name.Namespace;
            string[] genericMerges = generic.Root.Element(presentation + "ResourceDictionary.ThemeDictionaries")!.Elements()
                .SelectMany(theme => theme.Element(presentation + "ResourceDictionary.MergedDictionaries")!.Elements())
                .Select(d => Path.GetFileName(d.Attribute("Source")!.Value))
                .ToArray();
            CollectionAssert.AreEqual(new[] { kUnthemedDefaultsFile }, genericMerges, "Generic.xaml's theme dictionaries merge the defaults file alone");

            XDocument defaults = XDocument.Load(Path.Combine(ResourcesRoot(kWinUI3ControlsFolder), kUnthemedDefaultsFile));
            XElement[] themes = defaults.Root!.Element(presentation + "ResourceDictionary.ThemeDictionaries")!.Elements().ToArray();
            CollectionAssert.AreEquivalent(new[] { "Default", "Light", "HighContrast" }, themes.Select(t => t.Attribute(kXamlNamespace + "Key")!.Value).ToArray(), $"{kUnthemedDefaultsFile}'s theme dictionaries");

            var themeKeySets = new Dictionary<string, HashSet<string>>();
            foreach (XElement theme in themes)
            {
                string name = theme.Attribute(kXamlNamespace + "Key")!.Value;
                string themeFile = $"AJutCustomControlStylingDefaults_{(name == "Default" ? "Dark" : name)}.xaml";
                string[] merged = theme.Element(presentation + "ResourceDictionary.MergedDictionaries")!.Elements()
                    .Select(d => Path.GetFileName(d.Attribute("Source")!.Value))
                    .ToArray();
                CollectionAssert.AreEqual(new[] { themeFile }, merged, $"The {name} theme dictionary");
                themeKeySets[name] = DefinedKeys(Path.Combine(ResourcesRoot(kWinUI3ControlsFolder), themeFile));
            }

            HashSet<string> neutral = UnthemedDefaultsOwnKeys();
            HashSet<string> dark = themeKeySets["Default"];
            var gaps = new List<string>();
            foreach (KeyValuePair<string, HashSet<string>> theme in themeKeySets)
            {
                gaps.AddRange(dark.Except(theme.Value).OrderBy(k => k).Select(k => $"{theme.Key} is missing {k}"));
                gaps.AddRange(theme.Value.Except(dark).OrderBy(k => k).Select(k => $"{theme.Key} has {k}, which the dark defaults do not"));
                gaps.AddRange(theme.Value.Intersect(neutral).OrderBy(k => k).Select(k => $"{theme.Key} has {k}, which the theme-neutral defaults also define"));
            }

            AssertNoGaps(gaps, "Per-theme unthemed defaults out of step");
        }

        [TestMethod]
        public void UnthemedDefaults_HaveEveryAJutKeyAVisualStateReads ()
        {
            // A visual state reached through GoToState looks its {ThemeResource} keys up in App.Resources only, never in
            // Generic.xaml, so an app with no AJut theme, which merges the defaults file into App.Resources, needs every
            // AJut key a template's visual states read to come from that file: its own entries, or ones every theme
            // dictionary it carries defines. A key a template defines itself is found there.
            string controls = ResourcesRoot(kWinUI3ControlsFolder);
            HashSet<string> reachable = UnthemedDefaultsOwnKeys();

            // What the defaults file's theme dictionaries actually merge, and only what every one of them defines
            XDocument defaults = XDocument.Load(Path.Combine(controls, kUnthemedDefaultsFile));
            XNamespace presentation = defaults.Root!.Name.Namespace;
            HashSet<string>[] perTheme = (defaults.Root.Element(presentation + "ResourceDictionary.ThemeDictionaries")?.Elements() ?? [])
                .Select(theme => theme.Descendants()
                    .Select(d => d.Attribute("Source")?.Value)
                    .OfType<string>()
                    .SelectMany(source => DefinedKeys(Path.Combine(controls, Path.GetFileName(source))))
                    .ToHashSet())
                .ToArray();
            if (perTheme.Length > 0)
            {
                var inEveryTheme = new HashSet<string>(perTheme[0]);
                foreach (HashSet<string> keys in perTheme.Skip(1))
                {
                    inEveryTheme.IntersectWith(keys);
                }

                reachable.UnionWith(inEveryTheme);
            }

            var gaps = new List<string>();
            foreach (string file in Directory.GetFiles(controls, "*.xaml").Where(f => !Path.GetFileName(f).StartsWith("AJutCustomControlStylingDefaults", StringComparison.Ordinal)))
            {
                XDocument template = XDocument.Load(file);
                HashSet<string> local = DefinedKeys(file);
                IEnumerable<string> read = template.Descendants()
                    .Where(element => element.Name.LocalName == "VisualState")
                    .SelectMany(state => state.Descendants().SelectMany(element => element.Attributes()))
                    .SelectMany(attribute => kAnyMarkupReference.Matches(attribute.Value).Select(reference => reference.Groups[1].Value))
                    .Where(key => key.StartsWith("AJut_", StringComparison.Ordinal) && !local.Contains(key) && !reachable.Contains(key));
                gaps.AddRange(read.Distinct().OrderBy(k => k).Select(key => $"{Path.GetFileName(file)} reads {key} in a visual state"));
            }

            AssertNoGaps(gaps, $"AJut keys visual states read that {kUnthemedDefaultsFile} does not provide");
        }

        // ===========[ WPF palettes ]===================================
        /// <summary>
        /// WPF's palettes are written by hand beside WinUI3's, and the shared palette mirrors WinUI3's, so nothing but this
        /// keeps the two stacks' colors the same. Every key WPF defines that the shared palette also has must give the
        /// shared palette's value, unless it is listed here as a difference kept on purpose. A key only WPF defines is its
        /// own and is not compared.
        /// </summary>
        [TestMethod]
        public void WpfPalettes_EveryKeyTheSharedPaletteHas_GivesItsValue ()
        {
            WpfPaletteDifference[] kept =
            [
                new WpfPaletteDifference("Light", "AJut_Brush_TitleBarBackground", "WPF's light title bar stays its blue, where WinUI3's is gray"),
            ];

            var gaps = new List<string>();
            foreach (string theme in kBaseThemes)
            {
                AJutPalette.Table table = TableFor(theme);
                HashSet<string> sharedKeys = table.Keys.ToHashSet();
                Dictionary<string, XamlEntry> wpf = ReadEntries(Path.Combine(ResourcesRoot(kWpfResourcesFolder), $"{theme}ThemeColorsBase.xaml"), out _);
                var layers = new List<PaletteLayer> { new PaletteLayer("wpf", wpf) };
                KeyValuePair<string, XamlEntry>[] compared = wpf.Where(p => sharedKeys.Contains(p.Key)).OrderBy(p => p.Key).ToArray();
                Assert.IsTrue(compared.Length > 0, $"{theme}: WPF's palette and the shared palette have keys in common to compare");
                foreach (KeyValuePair<string, XamlEntry> pair in compared)
                {
                    WpfPaletteDifference? difference = kept.FirstOrDefault(d => (d.Theme == theme) && (d.Key == pair.Key));
                    if (difference != null)
                    {
                        // A difference kept on purpose has to still be one, or the list says something that is not so
                        string sharedValue = table.TryGetPaint(pair.Key, out AJutThemePaint? sharedPaint) ? Describe(sharedPaint) : "unresolved";
                        if (sharedValue == ResolveFromXaml(pair.Key, layers, new Dictionary<string, XamlEntry>()))
                        {
                            gaps.Add($"{theme} {pair.Key}: listed as kept different ({difference.Why}), but WPF now gives the shared value");
                        }

                        continue;
                    }

                    if (pair.Value.Number.HasValue)
                    {
                        if (!table.TryGetDouble(pair.Key, out double number) || (number != pair.Value.Number.Value))
                        {
                            gaps.Add($"{theme} {pair.Key}: WPF gives {pair.Value.Number.Value.ToString(CultureInfo.InvariantCulture)}, the shared palette {(table.TryGetDouble(pair.Key, out double shared) ? shared.ToString(CultureInfo.InvariantCulture) : "no number")}");
                        }

                        continue;
                    }

                    string expected = table.TryGetPaint(pair.Key, out AJutThemePaint? paint) ? Describe(paint) : "unresolved";
                    string actual = ResolveFromXaml(pair.Key, layers, new Dictionary<string, XamlEntry>());
                    if (expected != actual)
                    {
                        gaps.Add($"{theme} {pair.Key}: WPF gives {actual}, the shared palette {expected}");
                    }
                }
            }

            AssertNoGaps(gaps, "WPF palette values that differ from the shared palette");
        }

        // ===========[ WPF touch overlays ]===================================
        [TestMethod]
        public void WpfTouchOverlays_RestateWhatTheTouchPalettesChange ()
        {
            var gaps = new List<string>();
            foreach (string theme in kBaseThemes)
            {
                HashSet<string> wpfKeys = ReadEntries(Path.Combine(ResourcesRoot(kWpfResourcesFolder), $"{theme}ThemeColorsBase.xaml"), out _).Keys.ToHashSet();
                HashSet<string> expected = ReadPaletteLayers(theme + "Touch")[0].Entries.Keys.Where(wpfKeys.Contains).ToHashSet();
                HashSet<string> actual = ReadWpfTouchOverlay(theme).Keys.ToHashSet();
                gaps.AddRange(expected.Except(actual).OrderBy(k => k).Select(k => $"WPF's {theme.ToLowerInvariant()} touch overlay is missing {k}"));
                gaps.AddRange(actual.Except(expected).OrderBy(k => k).Select(k => $"WPF's {theme.ToLowerInvariant()} touch overlay has {k}, which the touch palette does not change or WPF does not define"));
            }

            AssertNoGaps(gaps, "WPF touch overlay keys out of step with the touch palettes");
        }

        [TestMethod]
        public void WpfTouchOverlays_GiveTheTouchPalettesValues ()
        {
            var gaps = new List<string>();
            foreach (string theme in kBaseThemes)
            {
                AJutPalette.Table table = TableFor(theme + "Touch");
                var layers = new List<PaletteLayer>
                {
                    new PaletteLayer("overlay", ReadWpfTouchOverlay(theme)),
                    new PaletteLayer("wpf", ReadEntries(Path.Combine(ResourcesRoot(kWpfResourcesFolder), $"{theme}ThemeColorsBase.xaml"), out _)),
                };

                foreach (string key in layers[0].Entries.Keys)
                {
                    string expected = table.TryGetPaint(key, out AJutThemePaint? paint) ? Describe(paint) : "unresolved";
                    string actual = ResolveFromXaml(key, layers, new Dictionary<string, XamlEntry>());
                    if (expected != actual)
                    {
                        gaps.Add($"{theme} {key}: WPF's touch overlay gives {actual}, the touch palette {expected}");
                    }
                }
            }

            AssertNoGaps(gaps, "WPF touch overlay values that differ from the touch palettes");
        }

        [TestMethod]
        public void WpfThemedStyles_EveryAJutKeyTheyTake_IsDefined ()
        {
            // A DynamicResource naming a key that nothing defines resolves to nothing, so whatever it sets draws
            // nothing: a border that vanishes, a fill that goes clear. Every AJut key the themed styles take has to be
            // in both WPF palettes, or be one the themed styles file defines itself.
            string folder = ResourcesRoot(kWpfResourcesFolder);
            string themedStyles = Path.Combine(folder, "ThemedControlStylesBase.xaml");
            HashSet<string> definedInThemedStyles = DefinedKeys(themedStyles);
            var palettes = new Dictionary<string, HashSet<string>>
            {
                ["LightThemeColorsBase.xaml"] = DefinedKeys(Path.Combine(folder, "LightThemeColorsBase.xaml")),
                ["DarkThemeColorsBase.xaml"] = DefinedKeys(Path.Combine(folder, "DarkThemeColorsBase.xaml")),
            };

            var gaps = new List<string>();
            IEnumerable<string> taken = kWpfAJutKeyReference.Matches(File.ReadAllText(themedStyles))
                .Select(reference => reference.Groups[1].Value)
                .Distinct()
                .OrderBy(k => k, StringComparer.Ordinal);

            foreach (string key in taken)
            {
                if (definedInThemedStyles.Contains(key))
                {
                    continue;
                }

                string[] missingFrom = palettes.Where(p => !p.Value.Contains(key)).Select(p => p.Key).ToArray();
                if (missingFrom.Length > 0)
                {
                    gaps.Add($"{key}, not in {string.Join(" or ", missingFrom)}");
                }
            }

            AssertNoGaps(gaps, "AJut keys WPF's themed styles take that nothing defines");
        }

        [TestMethod]
        public void WpfDefaultStyles_TakeNoPaletteKey ()
        {
            // A control's default style is how it looks with no theme loaded, so a palette key there has nothing to
            // resolve it, and whatever it sets draws nothing. Palette brushes go in the themed styles
            // (ThemedControlStylesBase.xaml), layered over the default style's own literal colors.
            string wpfRoot = ResourcesRoot(Path.Combine("libs", "AJut.UX.Wpf"));
            HashSet<string> paletteKeys = DefinedKeys(Path.Combine(wpfRoot, "Resources", "DarkThemeColorsBase.xaml"));
            paletteKeys.UnionWith(DefinedKeys(Path.Combine(wpfRoot, "Resources", "LightThemeColorsBase.xaml")));

            IEnumerable<string> defaultStyleFiles = Directory.GetFiles(Path.Combine(wpfRoot, "Controls"), "*.xaml", SearchOption.AllDirectories)
                .Append(Path.Combine(wpfRoot, "Themes", "Generic.xaml"))
                .OrderBy(file => file, StringComparer.Ordinal);

            var gaps = new List<string>();
            foreach (string file in defaultStyleFiles)
            {
                IEnumerable<string> taken = XDocument.Load(file).Descendants()
                    .SelectMany(element => element.Attributes())
                    .SelectMany(attribute => kWpfAJutKeyReference.Matches(attribute.Value))
                    .Select(reference => reference.Groups[1].Value)
                    .Where(paletteKeys.Contains)
                    .Distinct();
                gaps.AddRange(taken.Select(key => $"{Path.GetFileName(file)} takes {key}"));
            }

            AssertNoGaps(gaps, "Palette keys WPF default styles take, with nothing to resolve them when no theme is loaded");
        }

        // ===========[ Helper Methods ]===================================
        private static AJutPalette.Table TableFor (string theme)
        {
            switch (theme)
            {
                case "Light": return AJutPalette.Light;
                case "Dark": return AJutPalette.Dark;
                case "LightTouch": return AJutPalette.LightTouch;
                case "DarkTouch": return AJutPalette.DarkTouch;
                default: throw new ArgumentException($"No palette named {theme}", nameof(theme));
            }
        }

        private static void AssertEveryKeyResolves (string theme)
        {
            AJutPalette.Table table = TableFor(theme);
            List<PaletteLayer> layers = ReadPaletteLayers(theme);
            Dictionary<string, XamlEntry> palette = Flatten(layers);
            Dictionary<string, XamlEntry> keyMap = ReadKeyMap();

            var gaps = new List<string>();
            foreach (string key in palette.Keys.Concat(keyMap.Keys).Where(k => !(palette.TryGetValue(k, out XamlEntry? e) && e.Number.HasValue)))
            {
                string expected = ResolveFromXaml(key, layers, keyMap);
                string actual = table.TryGetPaint(key, out AJutThemePaint? paint) ? Describe(paint) : "unresolved";
                if (expected != actual)
                {
                    gaps.Add($"{key}: the XAML gives {expected}, the table {actual}");
                }
            }

            AssertNoGaps(gaps, $"{theme} keys that resolve differently from the XAML");
        }

        // Resolves a key the way WinUI3 would, and describes the result the same way Describe does, so the two compare as text.
        //
        // A palette can be layers: a touch palette's own entries over the base palette it merges in. A lookup starts at
        // the outermost layer, but an entry found in a merged layer resolves its own references from that layer on,
        // the way a brush in a merged file looks its color up in that file. That is the stricter reading of WinUI's
        // lookup, so a touch file that changes a color has to restate the brushes that take it, and the C# tables have
        // to say the same. A key map entry is theme wide, so its references start from the outermost layer again.
        private static string ResolveFromXaml (string key, List<PaletteLayer> layers, Dictionary<string, XamlEntry> keyMap)
        {
            const int kMaxAliasDepth = 8;
            double opacity = 1.0;
            string current = key;
            int firstLayer = 0;
            for (int depth = 0; depth < kMaxAliasDepth; ++depth)
            {
                XamlEntry? entry = null;
                int foundLayer = -1;
                for (int layer = firstLayer; layer < layers.Count; ++layer)
                {
                    if (layers[layer].Entries.TryGetValue(current, out entry))
                    {
                        foundLayer = layer;
                        break;
                    }
                }

                if (entry == null)
                {
                    if (!keyMap.TryGetValue(current, out entry))
                    {
                        return "unresolved";
                    }

                    foundLayer = 0;
                }

                firstLayer = foundLayer;
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
                    // A stop naming a palette color takes it from the theme, starting at the outermost palette the way a
                    // key map entry does
                    var stops = new List<AJutGradientStop>();
                    foreach (XamlStop stop in entry.Stops)
                    {
                        ColorArgb color;
                        if (stop.ColorKey != null)
                        {
                            string stopColor = ResolveFromXaml(stop.ColorKey, layers, keyMap);
                            if (!stopColor.StartsWith('#'))
                            {
                                return "unresolved";
                            }

                            color = ParseColor(stopColor);
                        }
                        else
                        {
                            color = stop.Color!.Value;
                        }

                        stops.Add(new AJutGradientStop(stop.Offset, ApplyOpacity(color, opacity)));
                    }

                    return DescribeGradient(stops);
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
        // The palette as a lookup from outside its file sees it: each key from the outermost layer that has it
        private static Dictionary<string, XamlEntry> ReadPalette (string theme) => Flatten(ReadPaletteLayers(theme));

        // A palette file's own entries, then those of the palette it merges in, if any (a touch palette merges its base)
        private static List<PaletteLayer> ReadPaletteLayers (string theme)
        {
            var layers = new List<PaletteLayer>();
            string? fileName = $"{theme}ThemeColorsBase.xaml";
            while (fileName != null)
            {
                Dictionary<string, XamlEntry> entries = ReadEntries(Path.Combine(ResourcesRoot(kResourcesFolder), fileName), out string? mergedFileName);
                layers.Add(new PaletteLayer(fileName, entries));
                fileName = mergedFileName;
            }

            return layers;
        }

        private static Dictionary<string, XamlEntry> Flatten (List<PaletteLayer> layers)
        {
            var flat = new Dictionary<string, XamlEntry>();
            foreach (PaletteLayer layer in layers)
            {
                foreach (KeyValuePair<string, XamlEntry> pair in layer.Entries)
                {
                    flat.TryAdd(pair.Key, pair.Value);
                }
            }

            return flat;
        }

        // The WinUI3 defaults file's own entries, not the theme dictionaries it carries
        private static HashSet<string> UnthemedDefaultsOwnKeys ()
        {
            XDocument defaults = XDocument.Load(Path.Combine(ResourcesRoot(kWinUI3ControlsFolder), kUnthemedDefaultsFile));
            return defaults.Root!.Elements()
                .Select(element => element.Attribute(kXamlNamespace + "Key")?.Value)
                .OfType<string>()
                .ToHashSet();
        }

        // A XAML file's top-level entries by key, as elements, so their kind (Color, SolidColorBrush, StaticResource) shows
        private static Dictionary<string, XElement> TopLevelEntries (string path)
        {
            var entries = new Dictionary<string, XElement>();
            foreach (XElement element in XDocument.Load(path).Root!.Elements())
            {
                string? key = element.Attribute(kXamlNamespace + "Key")?.Value;
                if (key != null)
                {
                    entries[key] = element;
                }
            }

            return entries;
        }

        // Every x:Key a XAML file defines, at any depth
        private static HashSet<string> DefinedKeys (string path)
        {
            return XDocument.Load(path).Descendants()
                .Select(element => element.Attribute(kXamlNamespace + "Key")?.Value)
                .OfType<string>()
                .ToHashSet();
        }

        private static Dictionary<string, XamlEntry> ReadWpfTouchOverlay (string theme)
        {
            Dictionary<string, XamlEntry> entries = ReadEntries(Path.Combine(ResourcesRoot(kWpfResourcesFolder), $"{theme}TouchThemeColorsOverlay.xaml"), out string? mergedFileName);
            Assert.IsNull(mergedFileName, $"WPF's {theme.ToLowerInvariant()} touch overlay is laid over the app's theme colors, so it merges nothing itself");
            return entries;
        }

        private static Dictionary<string, XamlEntry> ReadKeyMap ()
        {
            var map = new Dictionary<string, XamlEntry>();
            foreach (string file in new[] { kAliasingFile, kOverridesFile, kSystemAliasingFile })
            {
                Dictionary<string, XamlEntry> entries = ReadEntries(Path.Combine(ResourcesRoot(kResourcesFolder), file), out string? mergedFileName);
                Assert.IsNull(mergedFileName, $"{file} is an aliasing file, which merges nothing");
                foreach (KeyValuePair<string, XamlEntry> pair in entries)
                {
                    Assert.IsTrue(map.TryAdd(pair.Key, pair.Value), $"{pair.Key} is defined in more than one aliasing file");
                }
            }

            return map;
        }

        // A file's own entries, and the file name of the one palette it merges in, when it does
        private static Dictionary<string, XamlEntry> ReadEntries (string path, out string? mergedFileName)
        {
            string fileName = Path.GetFileName(path);
            XDocument doc = XDocument.Load(path);
            var entries = new Dictionary<string, XamlEntry>();
            mergedFileName = null;
            foreach (XElement element in doc.Root!.Elements())
            {
                string? key = element.Attribute(kXamlNamespace + "Key")?.Value;
                switch (element.Name.LocalName)
                {
                    case "ResourceDictionary.MergedDictionaries":
                        XElement[] merged = element.Elements().ToArray();
                        Assert.AreEqual(1, merged.Length, $"{fileName} merges one palette at most, which is all the palette tables carry");
                        mergedFileName = Path.GetFileName(merged[0].Attribute("Source")!.Value);
                        break;

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
                        XamlStop[] stops = element.Elements().Select(ReadStop).ToArray();
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

        // A stop's color is its own, or a {ThemeResource} reference to a palette color that each theme gives its own value
        private static XamlStop ReadStop (XElement stop)
        {
            double offset = ParseNumber(stop.Attribute("Offset")!.Value);
            string color = stop.Attribute("Color")!.Value;
            Match reference = kMarkupReference.Match(color);
            return reference.Success
                ? new XamlStop(offset, null, reference.Groups[1].Value)
                : new XamlStop(offset, ParseColor(color), null);
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

        private static string ResourcesRoot (string folder)
        {
            // The test runs from its build output somewhere under the repo, so walk up until the resources folder appears.
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, folder);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException($"Could not find {folder} above {AppContext.BaseDirectory}");
        }

        // ===========[ Subclasses/structs ]===================================
        // A color a WinUI3 template takes with no theme loaded, and the themed style that sets the property it fills
        private sealed record UnthemedOnlyKey (string Key, string ThemedStyleFile, string Control, string Property);

        // A key WPF's palette gives a value of its own on purpose, in one theme, and why
        private sealed record WpfPaletteDifference (string Theme, string Key, string Why);

        // One palette file's own entries, outermost first in a list of layers
        private sealed class PaletteLayer
        {
            public PaletteLayer (string fileName, Dictionary<string, XamlEntry> entries)
            {
                this.FileName = fileName;
                this.Entries = entries;
            }

            public string FileName { get; }

            public Dictionary<string, XamlEntry> Entries { get; }
        }

        // One gradient stop as the XAML states it: its own color, or the palette color it names
        private sealed record XamlStop (double Offset, ColorArgb? Color, string? ColorKey);

        // One key's definition as the XAML states it, read independently of AJut.UX's own table types.
        private sealed class XamlEntry
        {
            private XamlEntry (ColorArgb? color, XamlStop[]? stops, string? aliasKey, double opacity, double? number)
            {
                this.Color = color;
                this.Stops = stops;
                this.AliasKey = aliasKey;
                this.Opacity = opacity;
                this.Number = number;
            }

            public ColorArgb? Color { get; }

            public XamlStop[]? Stops { get; }

            public string? AliasKey { get; }

            public double Opacity { get; }

            public double? Number { get; }

            public static XamlEntry Solid (ColorArgb color, double opacity = 1.0) => new XamlEntry(color, null, null, opacity, null);

            public static XamlEntry Gradient (XamlStop[] stops) => new XamlEntry(null, stops, null, 1.0, null);

            public static XamlEntry Alias (string key, double opacity) => new XamlEntry(null, null, key, opacity, null);

            public static XamlEntry Double (double value) => new XamlEntry(null, null, null, 1.0, value);
        }
    }
}
