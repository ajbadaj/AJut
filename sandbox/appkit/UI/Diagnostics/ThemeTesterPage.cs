namespace AJutShowRoomAppKit.UI.Diagnostics
{
    using System;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using AppKit;
    using Foundation;

    /// <summary>
    /// Every control the AJut theme styles, in each state side by side and labeled, so a palette can be judged in one
    /// look: switch Dark, Light, System, the AJut theme and Touch in the theme bar and watch every state at once. The same
    /// sections as the Android and UIKit showrooms' Theme Tester.
    /// </summary>
    /// <remarks>
    /// A desktop has one pointer, so pointer over and pressed are drawn by subclasses that pin CurrentState (every AJut
    /// AppKit button draws from it). Disabled is the real thing. Focus is the page's one real focus. What AppKit can't
    /// pin is said where it would go, with a live sample to try by hand.
    /// </remarks>
    public sealed class ThemeTesterPage : ShowroomPage
    {
        private const string kNotchedShape = "M 0,0 20,20 40,0 150,0 150,100 100,90 0,100 Z";
        private static readonly string[] kStripItems = new[] { "One", "Two", "Three" };
        private static readonly string[] kManyItems = new[] { "January", "February", "March", "April", "May", "June", "July" };

        private readonly NumericEditor m_focusedEditor;

        // ===========[ Setup/Construction/Teardown ]===========================
        public ThemeTesterPage ()
            : base("Theme Tester", "Every control the AJut theme styles, in each of its states side by side. Use the theme bar to switch the look, the AJut theme and Touch, and judge every state at once. The \"Rest\" samples are live: move over and press them too.")
        {
            // 1. Buttons, every state pinned
            this.AddHeading("BrushButton");
            this.AddStates(
                new StateSample("Rest", new BrushButton { Title = "Button" }),
                new StateSample("Pointer over", new PinnedBrushButton(eManagedButtonState.PointerOver) { Title = "Button" }),
                new StateSample("Pressed", new PinnedBrushButton(eManagedButtonState.Pressed) { Title = "Button" }),
                new StateSample("Disabled", new BrushButton { Title = "Button", Enabled = false })
            );

            this.AddHeading("AutoBrushButton, no accent of its own (the AJut accent when themed)");
            this.AddStates(
                new StateSample("Rest", new AutoBrushButton { Title = "Button" }),
                new StateSample("Pointer over", new PinnedAutoBrushButton(eManagedButtonState.PointerOver) { Title = "Button" }),
                new StateSample("Pressed", new PinnedAutoBrushButton(eManagedButtonState.Pressed) { Title = "Button" }),
                new StateSample("Disabled", new AutoBrushButton { Title = "Button", Enabled = false })
            );

            this.AddHeading("GeometryButton, traced");
            this.AddStates(
                new StateSample("Rest", MakeShape(new GeometryButton())),
                new StateSample("Pointer over", MakeShape(new PinnedGeometryButton(eManagedButtonState.PointerOver))),
                new StateSample("Pressed", MakeShape(new PinnedGeometryButton(eManagedButtonState.Pressed))),
                new StateSample("Disabled", MakeShape(new GeometryButton { Enabled = false }))
            );

            this.AddHeading("Docking toolbar buttons");
            this.AddStates(
                new StateSample("Add, rest", new DockPanelAddButton { Title = "Add Panel" }),
                new StateSample("Add, pointer over", new PinnedDockPanelAddButton(eManagedButtonState.PointerOver) { Title = "Add Panel" }),
                new StateSample("Add, pressed", new PinnedDockPanelAddButton(eManagedButtonState.Pressed) { Title = "Add Panel" }),
                new StateSample("Toggle, off", new DockPanelShowHideToggleButton { Title = "Panel" }),
                new StateSample("Toggle, on", new DockPanelShowHideToggleButton { Title = "Panel", IsChecked = true }),
                new StateSample("Toggle, on, pointer over", new PinnedDockPanelToggleButton(eManagedButtonState.PointerOver) { Title = "Panel", IsChecked = true })
            );

            // 2. ToggleStrip: its items are drawn inside the strip, so only selection can be set from here
            this.AddHeading("ToggleStrip");
            this.AddStates(
                new StateSample("Rest, with Two picked", new ToggleStrip { ItemsSource = kStripItems, SelectedItem = kStripItems[1] }),
                new StateSample("OverflowPopup (the chevron)", MakeNarrowStrip(eToggleStripOverflow.OverflowPopup)),
                new StateSample("Scroll (the end dots)", MakeNarrowStrip(eToggleStripOverflow.Scroll))
            );
            this.AddText("A strip draws its own items, so their pointer over and pressed looks can't be pinned on AppKit: move over and press the items above. No stack has a disabled or focused look for a strip.");

            // 3. NumericEditor
            this.AddHeading("NumericEditor");
            m_focusedEditor = new NumericEditor { Value = 42 };
            var outOfBounds = new NumericEditor { Value = 10, Minimum = 0, Maximum = 10, OutOfBoundsResponse = eOutOfBoundsResponse.ErrorAndToolTip };
            ShowAsTyped(outOfBounds, "42");
            this.AddStates(
                new StateSample("Rest", MakeEditorSized(new NumericEditor { Value = 42 })),
                new StateSample("Focused", MakeEditorSized(m_focusedEditor)),
                new StateSample("Out of bounds (0 to 10)", MakeEditorSized(outOfBounds)),
                new StateSample("Read-only", MakeEditorSized(new NumericEditor { Value = 42, IsReadOnly = true }))
            );
            this.AddText("Focused holds while the page keeps its focus; click another sample's field to move it. The minus and plus are drawn inside the editor, so press and move over them by hand.");

            // 4. ColorEditIngressControl
            this.AddHeading("ColorEditIngressControl");
            this.AddStates(
                new StateSample("Rest (click: open)", new ColorEditIngressControl { EditColor = new ColorArgb(0xFF, 0x21, 0x96, 0xF3) }),
                new StateSample("Read-only", new ColorEditIngressControl { EditColor = new ColorArgb(0xFF, 0x21, 0x96, 0xF3), IsReadOnly = true })
            );
            this.AddText("While its Colors panel is open, the swatch's border takes the highlight.");

            // 5. The macOS controls AJut leaves alone, for comparison
            this.AddHeading("macOS's own controls, not themed by AJut (they follow the system appearance only)");
            this.AddStates(
                new StateSample("Check box, on", MakeCheckBox(isOn: true, isEnabled: true)),
                new StateSample("Check box, off", MakeCheckBox(isOn: false, isEnabled: true)),
                new StateSample("Check box, disabled", MakeCheckBox(isOn: true, isEnabled: false)),
                new StateSample("Text field", new NSTextField { StringValue = "Text" }),
                new StateSample("EnumComboBox (a pop-up button)", new EnumComboBox { SelectedItem = eAppThemeConfiguration.Dark })
            );

            this.AddCode(
@"// Every AJut AppKit button draws from CurrentState, so overriding it draws one state with no pointer
class PressedBrushButton : BrushButton
{
    public override eManagedButtonState CurrentState => eManagedButtonState.Pressed;
}"
            );
        }

        // ===========[ Events ]================================================
        // The page's one focus, given once the page is in the window that owns it
        public override void ViewDidMoveToWindow ()
        {
            base.ViewDidMoveToWindow();
            this.Window?.MakeFirstResponder(m_focusedEditor.TextField);
        }

        // ===========[ Helper Methods ]========================================
        // One heading's samples in a row, each over its label
        private void AddStates (params StateSample[] samples)
        {
            var row = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: 18.0);
            foreach (StateSample sample in samples)
            {
                var column = new FillStack(NSUserInterfaceLayoutOrientation.Vertical, spacing: 6.0);
                NSTextField caption = ShowroomStyle.Understated(sample.Label);
                column.Add(sample.View, alignment: eFillStackCrossAlignment.Start);
                column.Add(caption, alignment: eFillStackCrossAlignment.Start);

                // Start alignment only keeps the sample and its caption inside the column, and the row has no grower, so
                // nothing ties a column's width to what it holds: any column could take the row's spare width, and a
                // relayout (a theme switch) could hand it to a different one. A zero width at a low priority pulls each
                // column in to its content. It sits just under the caption's horizontal compression resistance, which
                // is low so the caption can wrap; above it, the hug would beat the caption and wrap it to its sample's width.
                NSLayoutConstraint hugContent = column.WidthAnchor.ConstraintEqualTo((nfloat)0);
                hugContent.Priority = caption.GetContentCompressionResistancePriority(NSLayoutConstraintOrientation.Horizontal) - 1f;
                hugContent.Active = true;
                row.Add(column);
            }

            this.AddDemo(row, eFillStackCrossAlignment.Start);
        }

        private static GeometryButton MakeShape (GeometryButton button)
        {
            const double kShapeWidth = 90.0;
            const double kShapeHeight = 60.0;

            button.GeometryMarkup = kNotchedShape;
            button.GeometryStretch = ePathStretch.Fill;
            button.TraceThickness = 1.0;
            button.ToolTip = "Notched shape";
            button.TranslatesAutoresizingMaskIntoConstraints = false;
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                button.WidthAnchor.ConstraintEqualTo((nfloat)kShapeWidth),
                button.HeightAnchor.ConstraintEqualTo((nfloat)kShapeHeight),
            });
            return button;
        }

        private static ToggleStrip MakeNarrowStrip (eToggleStripOverflow overflow)
        {
            const double kNarrowStripWidth = 150.0;

            var strip = new ToggleStrip { ItemsSource = kManyItems, SelectedItem = kManyItems[0], OverflowBehavior = overflow };
            strip.TranslatesAutoresizingMaskIntoConstraints = false;
            strip.WidthAnchor.ConstraintEqualTo((nfloat)kNarrowStripWidth).Active = true;
            return strip;
        }

        private static NumericEditor MakeEditorSized (NumericEditor editor)
        {
            const double kEditorWidth = 130.0;

            editor.TranslatesAutoresizingMaskIntoConstraints = false;
            editor.WidthAnchor.ConstraintEqualTo((nfloat)kEditorWidth).Active = true;
            return editor;
        }

        // Puts text in an editor the way typing does. The editor learns of typing from its field's text-did-change
        // notification, which its field's delegate receives; setting the text from code posts nothing, so this posts it.
        private static void ShowAsTyped (NumericEditor editor, string text)
        {
            editor.TextField.StringValue = text;
            NSNotificationCenter.DefaultCenter.PostNotificationName(NSControl.TextDidChangeNotification, editor.TextField);
        }

        private static NSButton MakeCheckBox (bool isOn, bool isEnabled)
        {
            var check = new NSButton { Title = "Check box", Enabled = isEnabled };
            check.SetButtonType(NSButtonType.Switch);
            check.State = isOn ? NSCellStateValue.On : NSCellStateValue.Off;
            return check;
        }

        // ===========[ Subclasses/structs ]====================================
        private sealed record StateSample (string Label, NSView View);

        // Each draws one pinned state; clicks still work, and nothing here is bound to them
        private sealed class PinnedBrushButton : BrushButton
        {
            private readonly eManagedButtonState m_state;
            public PinnedBrushButton (eManagedButtonState state) => m_state = state;
            public override eManagedButtonState CurrentState => m_state;
        }

        private sealed class PinnedAutoBrushButton : AutoBrushButton
        {
            private readonly eManagedButtonState m_state;
            public PinnedAutoBrushButton (eManagedButtonState state) => m_state = state;
            public override eManagedButtonState CurrentState => m_state;
        }

        private sealed class PinnedGeometryButton : GeometryButton
        {
            private readonly eManagedButtonState m_state;
            public PinnedGeometryButton (eManagedButtonState state) => m_state = state;
            public override eManagedButtonState CurrentState => m_state;
        }

        private sealed class PinnedDockPanelAddButton : DockPanelAddButton
        {
            private readonly eManagedButtonState m_state;
            public PinnedDockPanelAddButton (eManagedButtonState state) => m_state = state;
            public override eManagedButtonState CurrentState => m_state;
        }

        private sealed class PinnedDockPanelToggleButton : DockPanelShowHideToggleButton
        {
            private readonly eManagedButtonState m_state;
            public PinnedDockPanelToggleButton (eManagedButtonState state) => m_state = state;
            public override eManagedButtonState CurrentState => m_state;
        }
    }
}
