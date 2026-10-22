namespace AJut.UX.Controls
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using Foundation;
    using ObjCRuntime;

    // ===========[ NumericEditor ]==============================================
    // A number field with nudge buttons: the AppKit view over AJut.UX's NumericEditorController,
    // which owns every rule (keeping the value's numeric type, parsing, clamping to Minimum and
    // Maximum, decimal places, nudging, and what to do with out-of-range text). This class only
    // shows the text, the error and the buttons, and passes the user's edits in.
    //
    // Editing, as on the other stacks:
    //  - Every keystroke updates Value (clamped), while out-of-range text stays on screen.
    //  - Return, or leaving the field, commits: FixOnCommit snaps the text to the clamped value,
    //    ErrorAndToolTip keeps the text and shows the error.
    //  - Up and Down arrows nudge, and so do the buttons, which repeat while held. Command or
    //    Control nudges by BigNudge, Shift by SmallNudge (the WinUI3 editor's Ctrl and Shift).
    //
    // The text field's edits are read through one NSTextFieldDelegate rather than the field's C#
    // events, because the arrow keys only reach a text field through its delegate
    // (doCommandBySelector), and the C# events are a delegate under the hood: using both would
    // have one replace the other.
    public class NumericEditor : ThemedView, INumericEditorHost
    {
        private const double kNudgeButtonWidth = 22.0;
        private const double kNudgeButtonSpacing = 1.0;
        private const double kFieldHeight = 24.0;

        private readonly NumericEditorController m_controller;
        private readonly EditorTextField m_field;
        private readonly FieldDelegate m_fieldDelegate;
        private readonly NSTextField m_label;
        private readonly NudgeButton m_increaseButton;
        private readonly NudgeButton m_decreaseButton;
        private object m_value;

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        private Type m_enforceNumericType;

        private double m_minimum = Double.NegativeInfinity;
        private double m_maximum = Double.PositiveInfinity;
        private int m_decimalPlacesAllowed = -1;
        private eOutOfBoundsResponse m_outOfBoundsResponse = eOutOfBoundsResponse.ErrorAndToolTip;
        private string m_errorMessage;
        private bool m_isFocused;
        private bool m_isReadOnly;

        // ===========[ Setup/Construction/Teardown ]===========================
        public NumericEditor ()
        {
            this.BorderThickness = 1.0;
            this.CornerRadius = 3.0;
            this.BackgroundColorKey = ThemedColorKeys.NumericEditor_TextBoxBackground;
            this.BorderColorKey = ThemedColorKeys.NumericEditor_NormalBorder;

            // 1. Label, the "#" until one is given
            m_label = NSTextField.CreateLabel("#");
            m_label.TextColor = ThemedColors.Get(ThemedColorKeys.NumericEditor_Label);

            // 2. The field itself, borderless inside the editor's own border
            m_field = new EditorTextField(this)
            {
                Bordered = false,
                DrawsBackground = false,
                FocusRingType = NSFocusRingType.None,
                TextColor = ThemedColors.Get(ThemedColorKeys.NumericEditor_Text),
            };
            m_field.Cell.Scrollable = true;
            m_field.Cell.SetSendsActionOnEndEditing(false);
            m_fieldDelegate = new FieldDelegate(this);
            m_field.Delegate = m_fieldDelegate;

            // 3. Nudge buttons, minus then plus side by side at the trailing edge, as the UIKit editor has them.
            //    Up and down chevrons stacked in a column read as a combo box's arrows.
            m_decreaseButton = new NudgeButton(isPositive: false);
            m_increaseButton = new NudgeButton(isPositive: true);
            m_increaseButton.Activated += this.OnIncreaseActivated;
            m_decreaseButton.Activated += this.OnDecreaseActivated;

            // 4. Layout: label, field filling, then the two buttons in a row
            var buttonRow = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: kNudgeButtonSpacing);
            buttonRow.Add(m_decreaseButton, grows: true);
            buttonRow.Add(m_increaseButton, grows: true);
            buttonRow.TranslatesAutoresizingMaskIntoConstraints = false;
            buttonRow.WidthAnchor.ConstraintEqualTo((nfloat)(kNudgeButtonWidth * 2.0 + kNudgeButtonSpacing)).Active = true;

            var row = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: 4.0)
            {
                Padding = new NSEdgeInsets(1, 6, 1, 1),
            };
            row.Add(m_label, alignment: eFillStackCrossAlignment.Center);
            row.Add(m_field, grows: true, alignment: eFillStackCrossAlignment.Center);
            row.Add(buttonRow);
            row.TranslatesAutoresizingMaskIntoConstraints = false;
            this.AddSubview(row);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                row.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                row.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                row.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                row.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                row.HeightAnchor.ConstraintGreaterThanOrEqualTo((nfloat)kFieldHeight),
            });

            m_controller = new NumericEditorController(this);
            m_controller.OnValueChanged(null);
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                m_increaseButton.Activated -= this.OnIncreaseActivated;
                m_decreaseButton.Activated -= this.OnDecreaseActivated;
                m_field.Delegate = null;
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        /// <summary>Raised when <see cref="Value"/> changes, from typing, nudging or code</summary>
        public event EventHandler<EventArgs> ValueChanged;

        private void OnIncreaseActivated (object sender, EventArgs e) => this.NudgeFromInput(true);
        private void OnDecreaseActivated (object sender, EventArgs e) => this.NudgeFromInput(false);

        // ===========[ Properties ]============================================
        /// <summary>
        /// The number being edited. Its numeric type is kept: give it an int and every edit stays an int.
        /// Null shows 0 (as a double). Typing updates it on every keystroke, clamped to the bounds.
        /// </summary>
        public object Value
        {
            get => m_value;
            set
            {
                if (Equals(m_value, value))
                {
                    return;
                }

                m_value = value;
                m_controller.OnValueChanged(value);

                // A value that changed type on the way in (text, or a number of another type than the
                // enforced one) was already written back through ShowValue, which raised ValueChanged.
                // Raise here only when the value given is still the one kept, so a change raises once.
                if (Equals(m_value, value))
                {
                    this.ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// The numeric type every value is kept in, however it arrives (text, or a number of another type); null, the
        /// default, keeps whatever type each value has. A value already set is converted to it, so the order the two
        /// are set in does not matter.
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        public Type EnforceNumericType
        {
            get => m_enforceNumericType;
            set
            {
                if (m_enforceNumericType != value)
                {
                    m_enforceNumericType = value;
                    m_controller.OnEnforcedTypeChanged(value);
                }
            }
        }

        /// <summary>The lowest value allowed; negative infinity, the default, means no lower bound</summary>
        public double Minimum
        {
            get => m_minimum;
            set
            {
                m_minimum = value;
                m_controller.OnValueChanged(m_value);
            }
        }

        /// <summary>The highest value allowed; positive infinity, the default, means no upper bound</summary>
        public double Maximum
        {
            get => m_maximum;
            set
            {
                m_maximum = value;
                m_controller.OnValueChanged(m_value);
            }
        }

        /// <summary>How many decimal places typed text may keep; -1, the default, means no limit</summary>
        public int DecimalPlacesAllowed
        {
            get => m_decimalPlacesAllowed;
            set
            {
                m_decimalPlacesAllowed = value;
                m_controller.OnValueChanged(m_value);
            }
        }

        /// <summary>What happens to out-of-range text on commit: shown as an error (the default), or snapped to the bound</summary>
        public eOutOfBoundsResponse OutOfBoundsResponse
        {
            get => m_outOfBoundsResponse;
            set
            {
                m_outOfBoundsResponse = value;
                m_controller.RefreshErrorState();
            }
        }

        /// <summary>The step for a plain nudge</summary>
        public double Nudge { get; set; } = 1.0;

        /// <summary>The step for a nudge with Command or Control held</summary>
        public double BigNudge { get; set; } = 5.0;

        /// <summary>The step for a nudge with Shift held. On an integer value it rounds away from zero, so it is never a step of 0.</summary>
        public double SmallNudge { get; set; } = 0.5;

        /// <summary>Shows the value without letting it be typed or nudged; the text can still be selected and copied</summary>
        public bool IsReadOnly
        {
            get => m_isReadOnly;
            set
            {
                m_isReadOnly = value;
                m_field.Editable = !value;
                m_increaseButton.Enabled = !value;
                m_decreaseButton.Enabled = !value;
            }
        }

        /// <summary>The out-of-range message showing now, or null</summary>
        public string ErrorMessage => m_errorMessage;

        /// <summary>Text shown before the field; "#" when not set</summary>
        public string Label
        {
            get => m_label.StringValue == "#" ? null : m_label.StringValue;
            set => m_label.StringValue = String.IsNullOrEmpty(value) ? "#" : value;
        }

        /// <summary>How the text sits in the field</summary>
        public NSTextAlignment TextAlignment
        {
            get => m_field.Alignment;
            set => m_field.Alignment = value;
        }

        /// <summary>The text field inside, for anything not passed through here (font, for one)</summary>
        public NSTextField TextField => m_field;

        // ===========[ INumericEditorHost Implementations ]====================
        object INumericEditorSettings.Minimum => NumericEditorController.ToMinimumBound(m_minimum);
        object INumericEditorSettings.Maximum => NumericEditorController.ToMaximumBound(m_maximum);

        void INumericEditorHost.ShowText (string text)
        {
            // Writing the text a field already shows loses the caret, so only write a difference
            text = text ?? String.Empty;
            if (m_field.StringValue == text)
            {
                return;
            }

            // While the field is being edited its text lives in the window's field editor, and the
            // field's StringValue is only a copy. Write through the editor, so the edit goes on, and
            // put the caret back where it was, clamped to the new length (a nudge reformats the text
            // under the caret). Not editing, the field's own value is the text.
            NSText editor = m_field.CurrentEditor;
            if (editor == null)
            {
                m_field.StringValue = text;
                return;
            }

            nint caret = Math.Min(editor.SelectedRange.Location, (nint)text.Length);
            editor.Value = text;
            editor.SelectedRange = new NSRange(caret, 0);
        }

        void INumericEditorHost.ShowValue (object value)
        {
            if (Equals(m_value, value))
            {
                return;
            }

            m_value = value;
            this.ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        void INumericEditorHost.ShowError (string errorMessage)
        {
            m_errorMessage = errorMessage;
            this.ToolTip = errorMessage;
            this.RefreshBorder();
        }

        // ===========[ Helper Methods ]========================================
        // The buttons read the modifiers held now, while they repeat; the arrow keys pass their key event's own
        private void NudgeFromInput (bool isPositive) => this.NudgeFromInput(isPositive, NSEvent.CurrentModifierFlags);

        private void NudgeFromInput (bool isPositive, NSEventModifierMask modifiers)
        {
            // The arrow keys still reach a read-only field that has been clicked into to select its text
            if (m_isReadOnly)
            {
                return;
            }

            bool isBig = modifiers.HasFlag(NSEventModifierMask.CommandKeyMask) || modifiers.HasFlag(NSEventModifierMask.ControlKeyMask);
            bool isSmall = modifiers.HasFlag(NSEventModifierMask.ShiftKeyMask);
            m_controller.Nudge(isPositive, NumericEditorController.PickNudgeAmount(isBig, isSmall, this.Nudge, this.BigNudge, this.SmallNudge));
        }

        private void SetFocused (bool isFocused)
        {
            if (m_isFocused != isFocused)
            {
                m_isFocused = isFocused;
                this.RefreshBorder();
            }
        }

        private void RefreshBorder ()
        {
            if (m_errorMessage != null)
            {
                this.BorderColorKey = ThemedColorKeys.NumericEditor_ErrorBorder;
            }
            else
            {
                this.BorderColorKey = m_isFocused ? ThemedColorKeys.NumericEditor_FocusBorder : ThemedColorKeys.NumericEditor_NormalBorder;
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // A nudge button: the button background for its state, with a minus or plus stroked in the button foreground,
        // the same colors a plain BrushButton uses. It steps on press and repeats while held.
        private sealed class NudgeButton : ManagedButton
        {
            private readonly bool m_isPositive;

            public NudgeButton (bool isPositive)
            {
                m_isPositive = isPositive;
                this.IsRepeating = true;
                this.ToolTip = isPositive ? "Increase" : "Decrease";
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                const double kGlyphHalfLength = 4.0;
                const double kGlyphLineWidth = 1.5;

                eManagedButtonState state = this.CurrentState;
                ThemedColors.Get(BackgroundKeyFor(state)).SetFill();
                NSBezierPath.FillRect(this.Bounds);

                double centerX = (double)this.Bounds.Width / 2.0;
                double centerY = (double)this.Bounds.Height / 2.0;
                var glyph = new NSBezierPath
                {
                    LineWidth = (nfloat)kGlyphLineWidth,
                    LineCapStyle = NSLineCapStyle.Round,
                };
                glyph.MoveTo(new CGPoint(centerX - kGlyphHalfLength, centerY));
                glyph.LineTo(new CGPoint(centerX + kGlyphHalfLength, centerY));
                if (m_isPositive)
                {
                    glyph.MoveTo(new CGPoint(centerX, centerY - kGlyphHalfLength));
                    glyph.LineTo(new CGPoint(centerX, centerY + kGlyphHalfLength));
                }

                ThemedColors.Get(ForegroundKeyFor(state)).SetStroke();
                glyph.Stroke();
            }

            private static string BackgroundKeyFor (eManagedButtonState state)
            {
                switch (state)
                {
                    case eManagedButtonState.PointerOver: return ThemedColorKeys.BrushButton_PointerOverBackground;
                    case eManagedButtonState.Pressed: return ThemedColorKeys.BrushButton_PressedBackground;
                    case eManagedButtonState.Disabled: return ThemedColorKeys.BrushButton_DisabledBackground;
                    default: return ThemedColorKeys.BrushButton_NormalBackground;
                }
            }

            private static string ForegroundKeyFor (eManagedButtonState state)
            {
                switch (state)
                {
                    case eManagedButtonState.PointerOver: return ThemedColorKeys.BrushButton_PointerOverForeground;
                    case eManagedButtonState.Pressed: return ThemedColorKeys.BrushButton_PressedForeground;
                    case eManagedButtonState.Disabled: return ThemedColorKeys.BrushButton_DisabledForeground;
                    default: return ThemedColorKeys.BrushButton_NormalForeground;
                }
            }
        }

        // Knows when it takes focus. Losing focus shows up as the end of editing, in FieldDelegate.
        private sealed class EditorTextField : NSTextField
        {
            private readonly NumericEditor m_owner;

            public EditorTextField (NumericEditor owner)
            {
                m_owner = owner;
            }

            public override bool BecomeFirstResponder ()
            {
                bool became = base.BecomeFirstResponder();
                if (became)
                {
                    m_owner.SetFocused(true);
                }

                return became;
            }
        }

        // The field's one delegate: keystrokes, the end of editing, and the arrow and Return keys
        private sealed class FieldDelegate : NSTextFieldDelegate
        {
            // Virtual key codes (kVK_UpArrow, kVK_DownArrow), the same on every keyboard layout
            private const ushort kUpArrowKeyCode = 126;
            private const ushort kDownArrowKeyCode = 125;

            private readonly NumericEditor m_owner;

            public FieldDelegate (NumericEditor owner)
            {
                m_owner = owner;
            }

            public override void Changed (NSNotification notification)
            {
                m_owner.m_controller.OnTextEdited(m_owner.m_field.StringValue);
            }

            public override void EditingEnded (NSNotification notification)
            {
                m_owner.m_controller.Commit();
                m_owner.SetFocused(false);
            }

            public override bool DoCommandBySelector (NSControl control, NSTextView textView, Selector commandSelector)
            {
                // The field editor only sends moveUp: and moveDown: for a bare arrow. Once a modifier is held the
                // arrow arrives as a different selector, picked by the text system's key bindings: Shift+Up is
                // moveUpAndModifySelection:, Command+Up is moveToBeginningOfDocument:, Control+Up is scrollPageUp:,
                // and a user's own DefaultKeyBinding.dict can change any of them. So the arrow is read from the key
                // event instead, which holds for every modifier combination and every binding set. Selectors that
                // do not come from an arrow key (Control+P is moveUp: in the emacs bindings) still fall through to
                // the switch below. The modifiers come from the same event rather than NSEvent.CurrentModifierFlags,
                // which polls the keyboard now instead of when the key was hit.
                if (TryReadArrowKey(out bool isUpArrow, out NSEventModifierMask modifiers))
                {
                    m_owner.NudgeFromInput(isUpArrow, modifiers);
                    return true;
                }

                switch (commandSelector.Name)
                {
                    case "moveUp:":
                        m_owner.NudgeFromInput(true);
                        return true;

                    case "moveDown:":
                        m_owner.NudgeFromInput(false);
                        return true;

                    case "insertNewline:":
                        // Return commits and keeps the field focused, as Enter does on the other stacks
                        m_owner.m_controller.Commit();
                        return true;

                    default:
                        return false;
                }
            }

            private static bool TryReadArrowKey (out bool isUpArrow, out NSEventModifierMask modifiers)
            {
                NSEvent current = NSApplication.SharedApplication.CurrentEvent;
                isUpArrow = false;
                modifiers = 0;
                if (current == null || current.Type != NSEventType.KeyDown)
                {
                    return false;
                }

                modifiers = current.ModifierFlags;
                if (current.KeyCode == kUpArrowKeyCode)
                {
                    isUpArrow = true;
                    return true;
                }

                return current.KeyCode == kDownArrowKeyCode;
            }
        }
    }
}
