namespace AJut.UX.Controls
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using AJut.UX.Helpers;
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
    // It draws the flagship look, WinUI3's NumericEditor template: a frame on the pop-out surface,
    // with the number in a sunk well of the text box color on the left, and a narrow column on the
    // right that is the increase button over its top half and the decrease button over its bottom
    // half, with a "#" marking the editor as numeric drawn under both. The frame's border shows
    // focus, brightens under the mouse, and turns the error color for out-of-range text, which also
    // shows a warning glyph between the well and the column, and the error is the editor's tooltip. A step
    // button is clear at rest, takes the bright highlight under the mouse, and a dark sheen while
    // pressed, both at the 0.7 opacity WinUI3 gives its RepeatButtons. An optional label sits in
    // front of the frame, and a read-only editor dims its number and its mark.
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
    //
    // An AppKit view and its managed peer keep each other alive while anything native holds the
    // view: the native side roots the peer. The editor natively retains its frame, field and
    // buttons, and the field keeps its delegate, so none of them may hold the editor strongly, or
    // an editor dropped without Dispose is never collected. The frame, the field and its delegate
    // hold it weakly, and the editor listens to its buttons only while it is in a window.
    public class NumericEditor : ThemedView, INumericEditorHost
    {
        // WinUI3's template: a 1 point frame with 2 point corners, and the label sits this far in front of it
        private const double kBorderThickness = 1.0;
        private const double kCornerRadius = 2.0;
        private const double kLabelSpacing = 6.0;
        private const double kMinimumHeight = 24.0;

        private readonly NumericEditorController m_controller;
        private readonly EditorTextField m_field;
        private readonly FieldDelegate m_fieldDelegate;
        private readonly NSTextField m_label;
        private readonly NSImageView m_warningGlyph;
        private readonly FillStack m_outerRow;
        private readonly FrameView m_frame;
        private readonly FillStack m_row;
        private readonly StepColumn m_stepColumn;
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
        private bool m_isPointerOverFrame;
        private bool m_isReadOnly;
        private bool m_isListeningToButtons;
        private bool m_isEndingEdit;
        private readonly ErrorToolTipOwner m_errorToolTipOwner;
        private nint m_errorToolTipTag;
        private bool m_hasErrorToolTipArea;

        // ===========[ Setup/Construction/Teardown ]===========================
        public NumericEditor ()
        {
            // 0. What gives the error's tooltip its text (see RefreshErrorToolTipArea); first, since sizing calls on it
            m_errorToolTipOwner = new ErrorToolTipOwner(this);

            // 1. The label, in front of the frame only once it has text (see Label)
            m_label = NSTextField.CreateLabel(String.Empty);
            m_label.TextColor = ThemedColors.Get(ThemedColorKeys.NumericEditor_Label);
            m_label.SetContentHuggingPriorityForOrientation((float)NSLayoutPriority.Required, NSLayoutConstraintOrientation.Horizontal);

            // 2. The field itself, borderless, in the well. WinUI3's number is in Consolas; the Mac's monospaced system
            //    font is its counterpart, so digits line up the same way as the value changes.
            const double kRegularFontWeight = 0.0;
            m_field = new EditorTextField(this)
            {
                Bordered = false,
                DrawsBackground = false,
                FocusRingType = NSFocusRingType.None,
                TextColor = ThemedColors.Get(ThemedColorKeys.NumericEditor_Text),
                Font = NSFont.MonospacedSystemFont(NSFont.SystemFontSize, (nfloat)kRegularFontWeight),
            };
            m_field.Cell.Scrollable = true;
            m_field.Cell.SetSendsActionOnEndEditing(false);
            m_fieldDelegate = new FieldDelegate(this);
            m_field.Delegate = m_fieldDelegate;
            var well = new FieldWell(m_field);

            // 3. The warning glyph, in the row only while the text is out of range (see ShowWarningGlyph)
            m_warningGlyph = new NSImageView
            {
                Image = NSImage.GetSystemSymbol("exclamationmark.triangle.fill", null),
                ContentTintColor = ThemedColors.Get(ThemedColorKeys.NumericEditor_ErrorBorder),
            };
            m_warningGlyph.SetContentHuggingPriorityForOrientation((float)NSLayoutPriority.Required, NSLayoutConstraintOrientation.Horizontal);

            // 4. The step column: increase over its top half, decrease over its bottom half, as WinUI3's. The editor
            //    listens to the buttons only while in a window (see ViewDidMoveToWindow).
            m_increaseButton = new NudgeButton(isPositive: true);
            m_decreaseButton = new NudgeButton(isPositive: false);
            m_stepColumn = new StepColumn(m_increaseButton, m_decreaseButton);

            // 5. The frame and the row inside it. The row is inset by the frame's border, so a pressed button fills
            //    inside the outline rather than over it.
            m_row = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal);
            m_row.Add(well, grows: true);
            m_row.Add(m_stepColumn);
            m_row.TranslatesAutoresizingMaskIntoConstraints = false;

            m_frame = new FrameView(this) { TranslatesAutoresizingMaskIntoConstraints = false };
            m_frame.AddSubview(m_row);
            var border = (nfloat)kBorderThickness;
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_row.TopAnchor.ConstraintEqualTo(m_frame.TopAnchor, border),
                m_row.BottomAnchor.ConstraintEqualTo(m_frame.BottomAnchor, -border),
                m_row.LeadingAnchor.ConstraintEqualTo(m_frame.LeadingAnchor, border),
                m_row.TrailingAnchor.ConstraintEqualTo(m_frame.TrailingAnchor, -border),
            });

            // 6. The label (once it has text) and the frame, which takes the rest of the width
            m_outerRow = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: kLabelSpacing);
            m_outerRow.Add(m_frame, grows: true);
            m_outerRow.TranslatesAutoresizingMaskIntoConstraints = false;
            this.AddSubview(m_outerRow);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_outerRow.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_outerRow.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_outerRow.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_outerRow.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                m_outerRow.HeightAnchor.ConstraintGreaterThanOrEqualTo((nfloat)kMinimumHeight),
            });

            m_controller = new NumericEditorController(this);
            m_controller.OnValueChanged(null);
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native view whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls: the native view keeps the field and buttons it has.
        /// None of its managed state survives a dispose, so a call that needs that state can still fail: dispose it only
        /// once AppKit is done with it.
        /// </summary>
        protected NumericEditor (NativeHandle handle) : base(handle)
        {
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                this.ListenToButtons(false);
                this.RemoveErrorToolTipArea();
                m_field.Delegate = null;
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Out of a window the editor drops its handlers on its buttons, so the buttons it retains natively hold nothing
        /// that leads back to it (see the class comment); back in one it hooks them again. A pointer that was over the
        /// frame when it left can't be over it any more.
        /// </summary>
        public override void ViewDidMoveToWindow ()
        {
            base.ViewDidMoveToWindow();
            this.ListenToButtons(this.Window != null);
            this.RefreshErrorToolTipArea();
            if (this.Window == null)
            {
                this.SetPointerOverFrame(false);
            }
        }

        // The error's tooltip area covers the editor, so it follows the editor's size
        public override void SetFrameSize (CGSize newSize)
        {
            base.SetFrameSize(newSize);
            this.RefreshErrorToolTipArea();
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

        /// <summary>
        /// Shows the value without letting it be typed or nudged, in the editor's disabled look (a control that can't be
        /// changed looks disabled, as every other editor in a read-only PropertyGrid does): the disabled surface, a plain
        /// text box border with no hover or focus change, and the number and # dimmed. The text can still be selected and copied.
        /// </summary>
        public bool IsReadOnly
        {
            get => m_isReadOnly;
            set
            {
                m_isReadOnly = value;
                m_field.Editable = !value;
                m_field.TextColor = ThemedColors.Get(value ? ThemedColorKeys.NumericEditor_ReadOnlyText : ThemedColorKeys.NumericEditor_Text);
                m_increaseButton.Enabled = !value;
                m_decreaseButton.Enabled = !value;
                m_stepColumn.IsReadOnly = value;
                m_frame.NeedsDisplay = true;
            }
        }

        /// <summary>The out-of-range message showing now, or null</summary>
        public string ErrorMessage => m_errorMessage;

        /// <summary>
        /// Text shown in front of the frame, the way a field's header reads; null or empty, the default, shows none and
        /// gives the frame the whole width
        /// </summary>
        public string Label
        {
            get => String.IsNullOrEmpty(m_label.StringValue) ? null : m_label.StringValue;
            set
            {
                m_label.StringValue = value ?? String.Empty;
                this.ShowLabel(!String.IsNullOrEmpty(value));
            }
        }

        /// <summary>How the text sits in the field</summary>
        public NSTextAlignment TextAlignment
        {
            get => m_field.Alignment;
            set => m_field.Alignment = value;
        }

        /// <summary>The text field inside, for anything not passed through here (font, for one)</summary>
        public NSTextField TextField => m_field;

        // The key the frame's border is drawn in now: the error, then the pointer, then focus, as WinUI3 orders them. Each
        // is a shared key, registered here with its macOS default as NumericEditor_ErrorBorder and the rest.
        // The shared rule every non-XAML editor follows: the error wins, then read-only (the plain text box border whatever
        // the pointer or focus does), then the pointer, then focus, as WinUI3 orders them
        private string FrameBorderKey => MobileLook.NumericEditor.FrameBorderFor(m_isReadOnly, m_errorMessage != null, m_isPointerOverFrame, m_isFocused);

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
            //
            // The commit at the end of an edit is the exception. The end-of-editing notification
            // arrives with the field editor still attached, but the field has already taken its value
            // from it, so text written only to the editor is dropped as the editor leaves, and 43.1
            // stayed on screen after committing to 43. Ending, the text goes to both.
            NSText editor = m_field.CurrentEditor;
            if (m_isEndingEdit)
            {
                if (editor != null)
                {
                    editor.Value = text;
                }

                m_field.StringValue = text;
                return;
            }

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

        // The error's tooltip makes no tooltip calls here: the editor's one tooltip area asks for its text as it shows (see
        // ErrorToolTipOwner), so this only keeps the message, the glyph and the border
        void INumericEditorHost.ShowError (string errorMessage)
        {
            if (String.Equals(m_errorMessage, errorMessage, StringComparison.Ordinal))
            {
                return;
            }

            m_errorMessage = errorMessage;
            this.ShowWarningGlyph(errorMessage);
            m_frame.NeedsDisplay = true;
        }

        // ===========[ Helper Methods ]========================================
        // One tooltip area over the whole editor, as WinUI3 puts the error's tooltip on the whole control. Its text comes
        // from ErrorToolTipOwner as AppKit is about to show it, so going into or out of an error makes no tooltip calls at
        // all. Setting tooltips on the editor, its field mid-edit, its buttons and its glyph as an error came in left the
        // app showing no tooltips anywhere until it restarted. The area only moves when the editor's size or window changes.
        private void RefreshErrorToolTipArea ()
        {
            this.RemoveErrorToolTipArea();
            if ((m_errorToolTipOwner == null) || (this.Window == null) || this.Bounds.IsEmpty)
            {
                return;
            }

            m_errorToolTipTag = this.AddToolTip(this.Bounds, m_errorToolTipOwner, IntPtr.Zero);
            m_hasErrorToolTipArea = true;
        }

        private void RemoveErrorToolTipArea ()
        {
            if (m_hasErrorToolTipArea)
            {
                this.RemoveToolTip(m_errorToolTipTag);
                m_hasErrorToolTipArea = false;
            }
        }

        private void ListenToButtons (bool listen)
        {
            if (listen == m_isListeningToButtons)
            {
                return;
            }

            m_isListeningToButtons = listen;
            if (listen)
            {
                m_increaseButton.Activated += this.OnIncreaseActivated;
                m_decreaseButton.Activated += this.OnDecreaseActivated;
            }
            else
            {
                m_increaseButton.Activated -= this.OnIncreaseActivated;
                m_decreaseButton.Activated -= this.OnDecreaseActivated;
            }
        }

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
                m_frame.NeedsDisplay = true;
            }
        }

        private void SetPointerOverFrame (bool isPointerOver)
        {
            if (m_isPointerOverFrame != isPointerOver)
            {
                m_isPointerOverFrame = isPointerOver;
                m_frame.NeedsDisplay = true;
            }
        }

        // A FillStack keeps a hidden child's space, so the label goes in front of the frame only while it has text
        private void ShowLabel (bool show)
        {
            bool isShown = m_label.Superview == m_outerRow;
            if (show && !isShown)
            {
                m_outerRow.Insert(0, m_label, alignment: eFillStackCrossAlignment.Center);
            }
            else if (!show && isShown)
            {
                m_outerRow.Remove(m_label);
            }
        }

        // The same reason puts the glyph in the row, between the well and the step column as WinUI3 has it, only while
        // there is an error, and takes it out again when it clears. VoiceOver reads the error from the image's
        // accessibility description: an image view's AccessibilityLabel is read-only in the bindings (it comes from the
        // image), unlike UIKit's, which the iOS editor sets directly.
        private void ShowWarningGlyph (string errorMessage)
        {
            const int kGlyphIndex = 1;

            m_warningGlyph.Image.AccessibilityDescription = errorMessage;
            bool isShown = m_warningGlyph.Superview == m_row;
            if ((errorMessage != null) && !isShown)
            {
                m_row.Insert(kGlyphIndex, m_warningGlyph, alignment: eFillStackCrossAlignment.Center);
            }
            else if ((errorMessage == null) && isShown)
            {
                m_row.Remove(m_warningGlyph);
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // The editor's frame: the pop-out surface in a rounded rect, outlined in the border for the editor's state, with
        // the row clipped to its corners. It reports the mouse coming and going to the editor, which picks the border.
        // It holds the editor weakly: the editor retains it natively (see the editor's class comment).
        private sealed class FrameView : NSView
        {
            private readonly WeakReference<NumericEditor> m_owner;

            public FrameView (NumericEditor owner)
            {
                m_owner = new WeakReference<NumericEditor>(owner);
                this.WantsLayer = true;
                this.ClipToCorners();

                // One area for the frame's life, kept in step with its visible rect by AppKit, as ManagedButton's is.
                // The step buttons sit inside it, so the frame stays under the pointer while one of them is.
                this.AddTrackingArea(new NSTrackingArea(
                    CGRect.Empty,
                    NSTrackingAreaOptions.MouseEnteredAndExited | NSTrackingAreaOptions.ActiveInActiveApp | NSTrackingAreaOptions.InVisibleRect,
                    this,
                    null
                ));
            }

            // For a peer the runtime makes over an existing native view, which has no owner to tell
            private FrameView (NativeHandle handle) : base(handle)
            {
                m_owner = new WeakReference<NumericEditor>(null);
            }

            public override bool IsFlipped => true;

            // The backing layer can be made later than WantsLayer is set, so the clip is put on it again with each layout
            public override void Layout ()
            {
                base.Layout();
                this.ClipToCorners();
            }

            public override void MouseEntered (NSEvent theEvent)
            {
                if (m_owner.TryGetTarget(out NumericEditor owner))
                {
                    owner.SetPointerOverFrame(true);
                }
            }

            public override void MouseExited (NSEvent theEvent)
            {
                if (m_owner.TryGetTarget(out NumericEditor owner))
                {
                    owner.SetPointerOverFrame(false);
                }
            }

            public override void ViewDidChangeEffectiveAppearance ()
            {
                base.ViewDidChangeEffectiveAppearance();
                this.NeedsDisplay = true;
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                var halfBorder = (nfloat)(kBorderThickness / 2.0);
                var corner = (nfloat)kCornerRadius;
                NSBezierPath outline = NSBezierPath.FromRoundedRect(this.Bounds.Inset(halfBorder, halfBorder), corner, corner);

                // The surface is a gradient themed, and a solid control fill unthemed. Read-only, it is the disabled surface.
                bool hasOwner = m_owner.TryGetTarget(out NumericEditor owner);
                string surfaceKey = (hasOwner && owner.m_isReadOnly) ? ThemedColorKeys.NumericEditor_ReadOnlyFrameBackground : ThemedColorKeys.NumericEditor_FrameBackground;
                NSGradient gradient = ThemedColors.GetThemedGradient(surfaceKey, this.EffectiveAppearance);
                if (gradient != null)
                {
                    gradient.DrawInBezierPath(outline, (nfloat)90);
                }
                else
                {
                    ThemedColors.Get(surfaceKey).SetFill();
                    outline.Fill();
                }

                string borderKey = hasOwner ? owner.FrameBorderKey : ThemedColorKeys.NumericEditor_NormalBorder;
                outline.LineWidth = (nfloat)kBorderThickness;
                ThemedColors.Get(borderKey).SetStroke();
                outline.Stroke();
            }

            // The row inside is clipped to the frame's rounded corners, so a pressed step button doesn't fill past them
            private void ClipToCorners ()
            {
                if (this.Layer != null)
                {
                    this.Layer.CornerRadius = (nfloat)kCornerRadius;
                    this.Layer.MasksToBounds = true;
                }
            }
        }

        // The number's well: the text box color, set in from the frame on every side so the frame's surface shows around
        // it, and sunk into it by the edge lighting WinUI3's NumericEditor draws with two nested borders, a shadow along
        // the top and left and a light line along the bottom and right. The lighting is the same literal shading over any
        // theme, as it is on WinUI3.
        private sealed class FieldWell : NSView
        {
            // WinUI3's: the well's margin, its inner border (#8000, 1.4 on the top and left) and outer border (#5FFF, 1
            // on the right and bottom), and the TextBox's padding of 4 across and 2 down
            private const double kInset = 3.0;
            private const double kShadowThickness = 1.4;
            private const double kLightThickness = 1.0;
            private const double kTextPaddingX = 4.0;
            private const double kTextPaddingY = 2.0;
            private static readonly NSColor kShadow = new ColorArgb(0x88, 0x00, 0x00, 0x00).ToNSColor();
            private static readonly NSColor kLight = new ColorArgb(0x55, 0xFF, 0xFF, 0xFF).ToNSColor();

            public FieldWell (NSTextField field)
            {
                this.TranslatesAutoresizingMaskIntoConstraints = false;
                field.TranslatesAutoresizingMaskIntoConstraints = false;
                this.AddSubview(field);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    field.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor, (nfloat)(kInset + kShadowThickness + kTextPaddingX)),
                    field.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor, (nfloat)(-(kInset + kLightThickness + kTextPaddingX))),
                    field.CenterYAnchor.ConstraintEqualTo(this.CenterYAnchor),
                    field.TopAnchor.ConstraintGreaterThanOrEqualTo(this.TopAnchor, (nfloat)(kInset + kShadowThickness + kTextPaddingY)),
                    field.BottomAnchor.ConstraintLessThanOrEqualTo(this.BottomAnchor, (nfloat)(-(kInset + kLightThickness + kTextPaddingY))),
                });
            }

            // For a peer the runtime makes over an existing native view
            private FieldWell (NativeHandle handle) : base(handle)
            {
            }

            public override bool IsFlipped => true;

            public override void ViewDidChangeEffectiveAppearance ()
            {
                base.ViewDidChangeEffectiveAppearance();
                this.NeedsDisplay = true;
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                CGRect bounds = this.Bounds.Inset((nfloat)kInset, (nfloat)kInset);
                nfloat left = bounds.X;
                nfloat top = bounds.Y;
                nfloat right = bounds.X + bounds.Width;
                nfloat bottom = bounds.Y + bounds.Height;
                var shadow = (nfloat)kShadowThickness;
                var light = (nfloat)kLightThickness;

                // The light line runs the whole right side and the whole bottom, and the shadow the rest of the top and
                // left, the way the outer border (bottom and right) holds the inner one (top and left) on WinUI3
                Fill(kLight, right - light, top, right, bottom);
                Fill(kLight, left, bottom - light, right - light, bottom);
                Fill(kShadow, left, top, right - light, top + shadow);
                Fill(kShadow, left, top + shadow, left + shadow, bottom - light);
                Fill(ThemedColors.Get(ThemedColorKeys.NumericEditor_TextBoxBackground), left + shadow, top + shadow, right - light, bottom - light);
            }

            private static void Fill (NSColor color, nfloat left, nfloat top, nfloat right, nfloat bottom)
            {
                if ((color == null) || (right <= left) || (bottom <= top))
                {
                    return;
                }

                color.SetFill();
                NSBezierPath.FillRect(new CGRect(left, top, right - left, bottom - top));
            }
        }

        // The column on the right of the well, WinUI3's button column: at least 22 wide, the increase button over its top
        // half and the decrease button over its bottom half, with the "#" mark drawn under both, so a button's fill washes
        // over it the way WinUI3's RepeatButtons sit over its label. The mark is WinUI3's own path, fitted inside the
        // column's padding and the Viewbox's margin, and dims with the number while read-only.
        private sealed class StepColumn : NSView
        {
            private const double kMinimumWidth = 22.0;

            // WinUI3's column padding of 3 plus its Viewbox's margin of 3, on every side of the mark
            private const double kMarkInset = 6.0;

            private const string kMarkPathData = "m 5.7274579 31.027604 c 6.1e-4 -0.181901 0.29828 -1.991458 0.66146 -4.02124 0.36319 -2.02978 0.66034 -3.845483 0.66034 -4.034895 0 -0.264424 -0.76791 -0.344385 -3.3073 -0.344385 H 0.43466788 V 21.436459 20.245834 H 4.0015079 c 3.4835 0 3.56991 -0.01391 3.69861 -0.595313 0.32839 -1.483507 0.93664 -5.067738 0.93664 -5.519332 0 -0.468693 -0.25631 -0.499939 -4.10105 -0.499939 H 0.43466788 V 12.440625 11.25 H 4.8002979 c 2.40109 0 4.37335 -0.02976 4.3828 -0.06614 0.009 -0.03638 0.46088 -2.5069269 1.0031901 -5.4901039 l 0.98601 -5.42395898 1.40053 -0.08075 c 1.2453 -0.0718 1.38481 -0.02782 1.25873 0.396875 -0.24227 0.81606498 -1.75522 9.54501688 -1.75522 10.12673388 0 0.537766 0.0985 0.549066 4.16719 0.477924 l 4.16718 -0.07286 0.91985 -5.4901039 0.91985 -5.49010398 h 1.26297 c 1.02759 0 1.26239 0.08629 1.25991 0.46302 -0.002 0.254662 -0.3575 2.54661498 -0.7907 5.09322998 -0.4332 2.546614 -0.78902 4.8385669 -0.7907 5.0932289 -0.003 0.418257 0.34226 0.46302 3.56882 0.46302 h 3.57188 v 1.190625 1.190625 h -3.83646 c -2.11005 0 -3.83725 0.02977 -3.83821 0.06615 -9.7e-4 0.03638 -0.2383 1.435365 -0.52741 3.108854 -0.28912 1.67349 -0.52645 3.132005 -0.52742 3.241146 -9.6e-4 0.109141 1.96278 0.198438 4.36387 0.198438 h 4.36563 v 1.184377 1.184377 l -4.56406 0.07239 -4.56407 0.07239 -0.58489 3.439583 c -0.32169 1.891771 -0.6545 3.826537 -0.73959 4.29948 -0.14998 0.833622 -0.19476 0.859895 -1.46563 0.859895 -0.72101 0 -1.30978 -0.0893 -1.30838 -0.198437 10e-4 -0.109141 0.29791 -1.924844 0.65891 -4.034896 0.36101 -2.110052 0.65752 -3.988236 0.65892 -4.173741 0.002 -0.262543 -0.94974 -0.321173 -4.29481 -0.264584 l -4.2973501 0.0727 -0.77415 4.299479 -0.77415 4.299479 h -1.2785 c -0.84701 0 -1.27812 -0.111619 -1.27738 -0.330729 z M 18.823788 19.980316 c 0.30491 -0.493356 1.14494 -5.950483 0.94788 -6.157766 -0.10749 -0.113067 -2.03088 -0.172598 -4.2742 -0.132292 l -4.07876 0.07328 -0.54914 3.112841 c -0.30203 1.712062 -0.49141 3.170578 -0.42084 3.241145 0.25749 0.257487 8.21167 0.12717 8.37506 -0.137212 z";
            private static readonly PathMarkupData kMarkPath = PathMarkup.Parse(kMarkPathData);

            private readonly NudgeButton m_increaseButton;
            private readonly NudgeButton m_decreaseButton;
            private bool m_isReadOnly;

            public StepColumn (NudgeButton increaseButton, NudgeButton decreaseButton)
            {
                m_increaseButton = increaseButton;
                m_decreaseButton = decreaseButton;
                this.TranslatesAutoresizingMaskIntoConstraints = false;
                this.AddSubview(increaseButton);
                this.AddSubview(decreaseButton);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    this.WidthAnchor.ConstraintGreaterThanOrEqualTo((nfloat)kMinimumWidth),
                    increaseButton.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                    increaseButton.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                    increaseButton.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                    decreaseButton.TopAnchor.ConstraintEqualTo(increaseButton.BottomAnchor),
                    decreaseButton.HeightAnchor.ConstraintEqualTo(increaseButton.HeightAnchor),
                    decreaseButton.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                    decreaseButton.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                    decreaseButton.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                });

                // The column has no width of its own past its minimum, so it holds to it rather than taking the well's space
                NSLayoutConstraint hugWidth = this.WidthAnchor.ConstraintEqualTo((nfloat)kMinimumWidth);
                hugWidth.Priority = (float)NSLayoutPriority.DefaultLow;
                hugWidth.Active = true;
            }

            // For a peer the runtime makes over an existing native view
            private StepColumn (NativeHandle handle) : base(handle)
            {
            }

            public override bool IsFlipped => true;

            /// <summary>Dims the mark with the number</summary>
            public bool IsReadOnly
            {
                get => m_isReadOnly;
                set
                {
                    m_isReadOnly = value;
                    this.NeedsDisplay = true;
                }
            }

            public override void ViewDidChangeEffectiveAppearance ()
            {
                base.ViewDidChangeEffectiveAppearance();
                this.NeedsDisplay = true;
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                CGRect area = this.Bounds.Inset((nfloat)kMarkInset, (nfloat)kMarkInset);
                if ((area.Width <= 0) || (area.Height <= 0))
                {
                    return;
                }

                // Uniform, as WinUI3's Viewbox stretches it, then moved into the inset area
                PathFit fit = PathFit.Compute(kMarkPath.Bounds, (double)area.Width, (double)area.Height, ePathStretch.Uniform);
                fit = fit with { OffsetX = fit.OffsetX + (double)area.X, OffsetY = fit.OffsetY + (double)area.Y };
                ThemedColors.Get(m_isReadOnly ? ThemedColorKeys.NumericEditor_ReadOnlyText : ThemedColorKeys.NumericEditor_Mark).SetFill();
                kMarkPath.ToBezierPath(fit).Fill();
            }
        }

        // A step button, one half of the step column: clear at rest, the bright highlight under the mouse, and WinUI3's dark
        // sheen while pressed, all at the 0.7 opacity WinUI3 sets on its RepeatButtons. It steps on press and repeats
        // while held. The sheens are WinUI3's literal gradients, the same over any theme as they are there.
        private sealed class NudgeButton : ManagedButton
        {
            private const string kIncreaseToolTip = "Increase";
            private const string kDecreaseToolTip = "Decrease";

            private const double kOpacity = 0.7;

            private static readonly NSGradient kIncreasePressedSheen = MakeSheen(
                new AJutGradientStop(0.00, new ColorArgb(0xBB, 0x88, 0x88, 0x88)),
                new AJutGradientStop(0.05, new ColorArgb(0xCC, 0x33, 0x33, 0x33)),
                new AJutGradientStop(0.30, new ColorArgb(0xEE, 0x22, 0x22, 0x22)),
                new AJutGradientStop(0.65, new ColorArgb(0xBB, 0x66, 0x66, 0x66)),
                new AJutGradientStop(1.00, new ColorArgb(0xAA, 0x11, 0x11, 0x11))
            );

            private static readonly NSGradient kDecreasePressedSheen = MakeSheen(
                new AJutGradientStop(0.00, new ColorArgb(0xAA, 0x11, 0x11, 0x11)),
                new AJutGradientStop(0.35, new ColorArgb(0xCC, 0x00, 0x00, 0x00)),
                new AJutGradientStop(0.75, new ColorArgb(0xCC, 0x88, 0x88, 0x88)),
                new AJutGradientStop(1.00, new ColorArgb(0xEE, 0x11, 0x11, 0x11))
            );

            private readonly bool m_isPositive;

            public NudgeButton (bool isPositive)
            {
                m_isPositive = isPositive;
                this.IsRepeating = true;
                this.ToolTip = isPositive ? kIncreaseToolTip : kDecreaseToolTip;
                this.AlphaValue = (nfloat)kOpacity;
                this.TranslatesAutoresizingMaskIntoConstraints = false;
            }

            // For a peer the runtime makes over an existing native button
            private NudgeButton (NativeHandle handle) : base(handle)
            {
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                eManagedButtonState state = this.CurrentState;
                if (state == eManagedButtonState.Pressed)
                {
                    // In a flipped view, 90 degrees puts the first stop at the top, as WinUI3's 0,0 to 0,1 runs
                    (m_isPositive ? kIncreasePressedSheen : kDecreasePressedSheen).DrawInRect(this.Bounds, (nfloat)90);
                }
                else if (state == eManagedButtonState.PointerOver)
                {
                    ThemedColors.Get(m_isPositive ? ThemedColorKeys.NumericEditor_IncreasePointerOverFill : ThemedColorKeys.NumericEditor_DecreasePointerOverFill).SetFill();
                    NSBezierPath.FillRect(this.Bounds);
                }
            }

            private static NSGradient MakeSheen (params AJutGradientStop[] stops)
            {
                var colors = new NSColor[stops.Length];
                var locations = new double[stops.Length];
                for (int i = 0; i < stops.Length; ++i)
                {
                    colors[i] = stops[i].Color.ToNSColor();
                    locations[i] = stops[i].Offset;
                }

                return new NSGradient(colors, locations);
            }
        }

        // Gives the editor's tooltip area its text as AppKit is about to show it: the out-of-range message, or none, which
        // shows no tooltip. AppKit doesn't retain a tooltip owner, so the editor keeps this one, and it holds the editor
        // weakly (see the editor's class comment).
        private sealed class ErrorToolTipOwner : NSObject
        {
            private readonly WeakReference<NumericEditor> m_owner;

            public ErrorToolTipOwner (NumericEditor owner)
            {
                m_owner = new WeakReference<NumericEditor>(owner);
            }

            // For a peer the runtime makes over an existing native owner, which has no editor to tell
            private ErrorToolTipOwner (NativeHandle handle) : base(handle)
            {
                m_owner = new WeakReference<NumericEditor>(null);
            }

            // NSViewToolTipOwner's view:stringForToolTip:point:userData:
            [Export("view:stringForToolTip:point:userData:")]
            public string StringForToolTip (NSView view, nint tag, CGPoint point, IntPtr userData)
            {
                return m_owner.TryGetTarget(out NumericEditor owner) ? owner.m_errorMessage : null;
            }
        }

        // Knows when it takes focus. Losing focus shows up as the end of editing, in FieldDelegate. The editor retains it
        // natively, so it holds the editor weakly (see the editor's class comment).
        private sealed class EditorTextField : NSTextField
        {
            private readonly WeakReference<NumericEditor> m_owner;

            public EditorTextField (NumericEditor owner)
            {
                m_owner = new WeakReference<NumericEditor>(owner);
            }

            // For a peer the runtime makes over an existing native field, which has no owner to tell
            private EditorTextField (NativeHandle handle) : base(handle)
            {
                m_owner = new WeakReference<NumericEditor>(null);
            }

            public override bool BecomeFirstResponder ()
            {
                bool became = base.BecomeFirstResponder();
                if (became && m_owner.TryGetTarget(out NumericEditor owner))
                {
                    owner.SetFocused(true);
                }

                return became;
            }
        }

        // The field's one delegate: keystrokes, the end of editing, and the arrow and Return keys. The field keeps it, so
        // it holds the editor weakly (see the editor's class comment).
        private sealed class FieldDelegate : NSTextFieldDelegate
        {
            // Virtual key codes (kVK_UpArrow, kVK_DownArrow), the same on every keyboard layout
            private const ushort kUpArrowKeyCode = 126;
            private const ushort kDownArrowKeyCode = 125;

            private readonly WeakReference<NumericEditor> m_owner;

            public FieldDelegate (NumericEditor owner)
            {
                m_owner = new WeakReference<NumericEditor>(owner);
            }

            // For a peer the runtime makes over an existing native delegate, which has no owner to tell
            private FieldDelegate (NativeHandle handle) : base(handle)
            {
                m_owner = new WeakReference<NumericEditor>(null);
            }

            public override void Changed (NSNotification notification)
            {
                if (m_owner.TryGetTarget(out NumericEditor owner))
                {
                    owner.m_controller.OnTextEdited(owner.m_field.StringValue);
                }
            }

            public override void EditingEnded (NSNotification notification)
            {
                if (m_owner.TryGetTarget(out NumericEditor owner))
                {
                    owner.m_isEndingEdit = true;
                    try
                    {
                        owner.m_controller.Commit();
                    }
                    finally
                    {
                        owner.m_isEndingEdit = false;
                    }

                    owner.SetFocused(false);
                }
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
                if (!m_owner.TryGetTarget(out NumericEditor owner))
                {
                    return false;
                }

                if (TryReadArrowKey(out bool isUpArrow, out NSEventModifierMask modifiers))
                {
                    owner.NudgeFromInput(isUpArrow, modifiers);
                    return true;
                }

                switch (commandSelector.Name)
                {
                    case "moveUp:":
                        owner.NudgeFromInput(true);
                        return true;

                    case "moveDown:":
                        owner.NudgeFromInput(false);
                        return true;

                    case "insertNewline:":
                        // Return commits and keeps the field focused, as Enter does on the other stacks
                        owner.m_controller.Commit();
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
