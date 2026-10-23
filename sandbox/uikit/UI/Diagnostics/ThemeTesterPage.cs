namespace AJutShowRoomUIKit.UI.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using CoreGraphics;
    using UIKit;

    /// <summary>
    /// Every control the AJut theme paints on UIKit, once per state, side by side, so a theme can be judged at a glance
    /// in each look the theme bar picks. States a control keeps as data (disabled, checked) are set as the page is built;
    /// pressed, focused, and out of bounds only hold on screen, so they are set as the page appears. A sample held in a
    /// state takes no touches, so a tap can't end it.
    /// </summary>
    public sealed class ThemeTesterPage : ShowroomPageController
    {
        private const string kRest = "Rest";
        private const string kHovered = "Hovered";
        private const string kPressed = "Pressed";
        private const string kDisabled = "Disabled";
        private const string kChecked = "Checked";
        private const string kCheckedPressed = "Checked, pressed";
        private const string kShape = "M 10,0 H 90 L 100,10 V 40 L 90,50 H 10 L 0,40 V 10 Z";

        private readonly List<UIControl> m_pressedSamples = new List<UIControl>();
        private readonly ToggleStrip m_uncheckedStrip = new ToggleStrip { AllowMultiSelect = true, AllowNoSelection = true };
        private readonly ToggleStrip m_checkedStrip = new ToggleStrip { AllowMultiSelect = true, AllowNoSelection = true };
        private readonly NumericEditor m_focusedEditor = new NumericEditor { Value = 5, Maximum = 10, LabelText = "Focused" };
        private readonly NumericEditor m_errorEditor = new NumericEditor { Value = 5, Maximum = 10, LabelText = "Error, over 10" };
        private readonly NumericEditor m_nudgePressedEditor = new NumericEditor { Value = 5, Maximum = 10, LabelText = "Plus held" };

        // ===========[ Setup/Construction/Teardown ]===================================
        public ThemeTesterPage () : base("Theme Tester")
        {
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear(animated);
            this.PinOnScreenStates();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear(animated);
            m_focusedEditor.TextField.ResignFirstResponder();
        }

        // ===========[ Public Interface Methods ]===================================
        protected override void BuildPage ()
        {
            this.AddText("Every control the AJut theme paints, in each state it can show. Pick the look with the bar at the top.");
            this.AddText("Pressed samples are held in that state for display, so they take no taps. Focused is the page's one real focus, and brings up no keyboard.");

            // 1. The buttons
            this.AddHeading("BrushButton");
            this.AddText("The standard button colors (ButtonBackground, ButtonForeground, and ButtonBorderBrush) in each state. Hovered is a sample that always reports a pointer over it, since only an iPad pointer hovers for real. UIKit shows no focus on it.");
            this.AddStateRow(
                _Titled(new BrushButton(), kRest),
                _Titled(new HoveredBrushButton(), kHovered),
                this.Pressed(_Titled(new BrushButton(), kPressed)),
                _Disabled(_Titled(new BrushButton(), kDisabled))
            );

            this.AddHeading("GeometryButton");
            this.AddText("The button colors fill the shape, and the GeometryButton trace keys (the common border) draw around it, so the trace stays on touch.");
            this.AddStateRow(
                _Shaped(_Titled(new GeometryButton(), kRest)),
                _Shaped(_Titled(new HoveredGeometryButton(), kHovered)),
                this.Pressed(_Shaped(_Titled(new GeometryButton(), kPressed))),
                _Disabled(_Shaped(_Titled(new GeometryButton(), kDisabled)))
            );

            this.AddHeading("AutoBrushButton");
            this.AddText("Every state derived from one accent. With none set, as here, that is the theme's accent (SystemAccentColor), or the view's tint with the AJut theme off. An accent set on the button wins over both.");
            this.AddStateRow(
                _Titled(new AutoBrushButton(), kRest),
                _Titled(new HoveredAutoBrushButton(), kHovered),
                this.Pressed(_Titled(new AutoBrushButton(), kPressed)),
                _Disabled(_Titled(new AutoBrushButton(), kDisabled))
            );

            // 2. The strips
            this.AddHeading("ToggleStrip");
            this.AddText("Each item is named for its state. The items are the ToggleStripButton keys over ToggleButtonBackground, and the outline and the lines between them are the common border. Hover only shows under an iPad pointer, so Hovered is left live for one. A strip has no disabled or focus look. EnumToggleStrip is this strip over an enum, and the theme bar at the top is one.");
            m_uncheckedStrip.ItemsSource = new[] { kRest, kHovered, kPressed };
            m_checkedStrip.ItemsSource = new[] { kChecked, kCheckedPressed };
            foreach (ToggleStripItem item in m_checkedStrip.Items)
            {
                item.IsSelected = true;
            }

            this.AddDemo(m_uncheckedStrip);
            this.AddDemo(m_checkedStrip);

            this.AddText("With more items than fit: OverflowPopup moves the rest into a menu on a chevron button that takes the rest of the strip, and Scroll scrolls, with a dot past each end it can still scroll toward.");
            this.AddDemo(_Months(eToggleStripOverflow.OverflowPopup));
            this.AddDemo(_Months(eToggleStripOverflow.Scroll));

            // 3. The editors
            this.AddHeading("NumericEditor");
            this.AddText("The frame is the NumericEditor border keys: normal, focused, and the error color out of bounds. The label is NumericEditor_Label, the number TextControlForeground (TextControlForegroundDisabled while read-only), and a held step button NumericEditor_NudgePressedBackground. Read-only buttons take the disabled button colors.");
            this.AddWide(_Sized(new NumericEditor { Value = 5, Maximum = 10, LabelText = kRest }));
            this.AddWide(_Sized(m_focusedEditor));
            this.AddWide(_Sized(m_errorEditor));
            this.AddWide(_Sized(new NumericEditor { Value = 5, Maximum = 10, LabelText = "Read-only", IsReadOnly = true }));
            this.AddWide(_Sized(m_nudgePressedEditor));

            // The focused sample is the page's one first responder, and an empty input view keeps the keyboard away
            m_focusedEditor.TextField.InputView = new UIView(CGRect.Empty);
            m_focusedEditor.TextField.InputAssistantItem.LeadingBarButtonGroups = Array.Empty<UIBarButtonItemGroup>();
            m_focusedEditor.TextField.InputAssistantItem.TrailingBarButtonGroups = Array.Empty<UIBarButtonItemGroup>();

            this.AddHeading("EnumComboBox");
            this.AddText("ComboBoxBackground and ComboBoxForeground. Pressed and disabled are UIKit's own dimming of those colors.");
            var pressedCombo = new EnumComboBox { SelectedItem = eSample.Second };
            m_pressedSamples.Add(pressedCombo.Button);
            pressedCombo.UserInteractionEnabled = false;
            var disabledCombo = new EnumComboBox { SelectedItem = eSample.Second };
            disabledCombo.Button.Enabled = false;
            this.AddStateRow(
                _Captioned(new EnumComboBox { SelectedItem = eSample.Second }, kRest),
                _Captioned(pressedCombo, kPressed),
                _Captioned(disabledCombo, kDisabled)
            );

            this.AddHeading("ColorEditIngressControl");
            this.AddText("Only its border is themed: ColorEditIngressControl_NormalBorderBrush, and HighlightBorderBrush while the picker is open. Pressed shows the edit overlay. It has no disabled or focus look.");
            this.AddStateRow(
                _Captioned(_Swatch(), kRest),
                _Captioned(this.Pressed(_Swatch()), kPressed),
                _Captioned(_Swatch(), "Tap: picker open")
            );

            // 4. What the AJut controls sit beside
            this.AddHeading("UIKit's own controls");
            this.AddText("Not themed by AJut: these follow the device's light or dark and the tint color, so they show what the AJut controls sit beside.");
            var disabledSwitch = new UISwitch { On = true, Enabled = false };
            this.AddStateRow(
                _Captioned(new UISwitch { On = false }, "Off"),
                _Captioned(new UISwitch { On = true }, "On"),
                _Captioned(disabledSwitch, kDisabled)
            );

            var disabledField = new UITextField { Text = kDisabled, BorderStyle = UITextBorderStyle.RoundedRect, Enabled = false };
            this.AddStateRow(
                new UITextField { Text = "Text", BorderStyle = UITextBorderStyle.RoundedRect },
                disabledField
            );

            var filled = new UIButton(CGRect.Empty) { Configuration = UIButtonConfiguration.FilledButtonConfiguration };
            filled.SetTitle("Filled", UIControlState.Normal);
            var tinted = new UIButton(CGRect.Empty) { Configuration = UIButtonConfiguration.TintedButtonConfiguration };
            tinted.SetTitle("Tinted", UIControlState.Normal);
            this.AddStateRow(filled, tinted, new UISegmentedControl("One", "Two") { SelectedSegment = 0 });

            // 5. How an app picks these
            this.AddHeading("Switching the theme in an app");
            this.AddText("Controls read their colors when they are made, so an app sets these at startup, before the first AJut control is made, or sets them and rebuilds its views, which is what the bar at the top does. Light and dark need neither: every color follows the appearance on its own.");
            this.AddCode(@"
// At startup, before the first AJut control is made
ThemedColors.IsThemeLoaded = true;

// On by default; off for the pointer palette the desktop stacks use
ThemedColors.IsTouchFocused = false;");

            static T _Titled<T> (T button, string title) where T : UIButton
            {
                button.SetTitle(title, UIControlState.Normal);
                return button;
            }

            static T _Disabled<T> (T control) where T : UIControl
            {
                control.Enabled = false;
                return control;
            }

            static GeometryButton _Shaped (GeometryButton button)
            {
                const float kTraceThickness = 2f;
                const float kHeight = 52f;

                button.BackgroundGeometry = PathMarkup.Parse(kShape);
                button.GeometryStretch = ePathStretch.Fill;
                button.TraceThickness = kTraceThickness;
                button.TranslatesAutoresizingMaskIntoConstraints = false;
                button.HeightAnchor.ConstraintEqualTo(kHeight).Active = true;
                return button;
            }

            static ToggleStrip _Months (eToggleStripOverflow overflow)
            {
                const float kWidth = 240f;

                var strip = new ToggleStrip
                {
                    ItemsSource = new[] { "January", "February", "March", "April", "May", "June" },
                    OverflowBehavior = overflow,
                    SelectedItem = "January",
                    TranslatesAutoresizingMaskIntoConstraints = false,
                };
                strip.WidthAnchor.ConstraintEqualTo(kWidth).Active = true;
                return strip;
            }

            static NumericEditor _Sized (NumericEditor editor)
            {
                const float kHeight = 36f;

                editor.TranslatesAutoresizingMaskIntoConstraints = false;
                editor.HeightAnchor.ConstraintEqualTo(kHeight).Active = true;
                return editor;
            }

            static ColorEditIngressControl _Swatch ()
            {
                const float kHeight = 36f;

                var swatch = new ColorEditIngressControl { EditColor = new ColorArgb(0xFF, 0x3A, 0x86, 0xC8) };
                swatch.TranslatesAutoresizingMaskIntoConstraints = false;
                swatch.HeightAnchor.ConstraintEqualTo(kHeight).Active = true;
                return swatch;
            }

            // A sample with no state text of its own, and a caption under it naming the state it shows
            static UIView _Captioned (UIView sample, string caption)
            {
                const float kSpacing = 4f;

                var label = new UILabel
                {
                    Text = caption,
                    Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Footnote),
                    TextColor = UIColor.SecondaryLabel,
                };
                return new UIStackView(new[] { sample, label })
                {
                    Axis = UILayoutConstraintAxis.Vertical,
                    Alignment = UIStackViewAlignment.Center,
                    Spacing = kSpacing,
                };
            }
        }

        // ===========[ Helper Methods ]===================================
        // A row of one control in its states, sharing the page's width evenly
        private void AddStateRow (params UIView[] samples)
        {
            const float kSpacing = 6f;

            this.AddWide(new UIStackView(samples)
            {
                Axis = UILayoutConstraintAxis.Horizontal,
                Alignment = UIStackViewAlignment.Center,
                Distribution = UIStackViewDistribution.FillEqually,
                Spacing = kSpacing,
            });
        }

        // Marks a sample to be held pressed once the page is on screen
        private T Pressed<T> (T control) where T : UIControl
        {
            control.UserInteractionEnabled = false;
            m_pressedSamples.Add(control);
            return control;
        }

        private void PinOnScreenStates ()
        {
            const string kIncreaseLabel = "Increase";

            // 1. Pressed: highlighted, with touches off so nothing ends it
            foreach (UIControl control in m_pressedSamples)
            {
                control.Highlighted = true;
            }

            // 2. Strip items are the strip's own buttons, found by the names each is given for the state it shows
            _PinStripItems(m_uncheckedStrip);
            _PinStripItems(m_checkedStrip);

            UIButton increase = _FindButton(m_nudgePressedEditor, kIncreaseLabel);
            if (increase != null)
            {
                increase.UserInteractionEnabled = false;
                increase.Highlighted = true;
            }

            // 3. Out of bounds is typed text, not a value set in code (that would be capped), and the editor only hears
            // typing while it is on screen
            const string kOutOfBoundsText = "50";
            m_errorEditor.TextField.Text = kOutOfBoundsText;
            m_errorEditor.TextField.SendActionForControlEvents(UIControlEvent.EditingChanged);

            // 4. Focus last, since it is the one real focus on the page
            m_focusedEditor.TextField.BecomeFirstResponder();

            static void _PinStripItems (UIView view)
            {
                if (view is UIButton button)
                {
                    string title = button.Configuration?.Title ?? button.CurrentTitle;
                    if ((title == kPressed) || (title == kCheckedPressed))
                    {
                        button.UserInteractionEnabled = false;
                        button.Highlighted = true;
                    }
                    else if ((title == kRest) || (title == kChecked))
                    {
                        button.UserInteractionEnabled = false;
                    }

                    return;
                }

                foreach (UIView child in view.Subviews)
                {
                    _PinStripItems(child);
                }
            }

            static UIButton _FindButton (UIView view, string accessibilityLabel)
            {
                if ((view is UIButton button) && (button.AccessibilityLabel == accessibilityLabel))
                {
                    return button;
                }

                foreach (UIView child in view.Subviews)
                {
                    UIButton found = _FindButton(child, accessibilityLabel);
                    if (found != null)
                    {
                        return found;
                    }
                }

                return null;
            }
        }

        // ===========[ Subclasses/structs ]===================================
        private enum eSample
        {
            First,
            Second,
            Third,
        }

        // Samples that always report a pointer over them, so the pointer over look holds for display. Only an iPad
        // pointer hovers for real, and nothing else can set it.
        private sealed class HoveredBrushButton : BrushButton
        {
            public override bool Hovered => true;
        }

        private sealed class HoveredAutoBrushButton : AutoBrushButton
        {
            public override bool Hovered => true;
        }

        private sealed class HoveredGeometryButton : GeometryButton
        {
            public override bool Hovered => true;
        }
    }
}
