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
    // It draws the look the iOS and Android editors share, from MobileLook.NumericEditor: one rounded
    // frame on the pop-out surface holding the decrease button, the number in a shaded well of the
    // text box color, and the increase button, with an optional label in front of the frame. Under
    // the mouse the frame's border brightens and a step button takes a wash of the bright highlight;
    // pressed, the button fills with it. As on iOS and Android, out-of-range text shows a warning
    // glyph beside the number (its tooltip is the error) as well as the error border, and a
    // read-only editor dims its number and its step glyphs.
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
        // MobileLook's step buttons are 44 wide, a touch target. A Mac is used with a mouse, so they keep the narrower
        // width a desktop stepper has.
        private const double kStepButtonWidth = 22.0;
        private const double kMinimumHeight = 24.0;

        private readonly NumericEditorController m_controller;
        private readonly EditorTextField m_field;
        private readonly FieldDelegate m_fieldDelegate;
        private readonly NSTextField m_label;
        private readonly NSImageView m_warningGlyph;
        private readonly FillStack m_outerRow;
        private readonly FrameView m_frame;
        private readonly FillStack m_row;
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

        // ===========[ Setup/Construction/Teardown ]===========================
        public NumericEditor ()
        {
            // 1. The label, in front of the frame only once it has text (see Label)
            m_label = NSTextField.CreateLabel(String.Empty);
            m_label.TextColor = ThemedColors.Get(ThemedColorKeys.NumericEditor_Label);
            m_label.SetContentHuggingPriorityForOrientation((float)NSLayoutPriority.Required, NSLayoutConstraintOrientation.Horizontal);

            // 2. The field itself, borderless, in the well
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
            var well = new FieldWell(m_field);

            // 3. The warning glyph, in the row only while the text is out of range (see ShowWarningGlyph)
            m_warningGlyph = new NSImageView
            {
                Image = NSImage.GetSystemSymbol("exclamationmark.triangle.fill", null),
                ContentTintColor = ThemedColors.Get(ThemedColorKeys.NumericEditor_ErrorBorder),
            };
            m_warningGlyph.SetContentHuggingPriorityForOrientation((float)NSLayoutPriority.Required, NSLayoutConstraintOrientation.Horizontal);

            // 4. The step buttons, decrease before the number and increase after it, as the iOS and Android editors
            //    have them. The editor listens to them only while in a window (see ViewDidMoveToWindow).
            m_decreaseButton = new NudgeButton(isPositive: false);
            m_increaseButton = new NudgeButton(isPositive: true);
            m_decreaseButton.WidthAnchor.ConstraintEqualTo((nfloat)kStepButtonWidth).Active = true;
            m_increaseButton.WidthAnchor.ConstraintEqualTo((nfloat)kStepButtonWidth).Active = true;

            // 5. The frame and the row inside it. The row is inset by the frame's border, so a pressed button fills
            //    inside the outline rather than over it.
            m_row = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal);
            m_row.Add(m_decreaseButton);
            m_row.Add(well, grows: true);
            m_row.Add(m_increaseButton);
            m_row.TranslatesAutoresizingMaskIntoConstraints = false;

            m_frame = new FrameView(this) { TranslatesAutoresizingMaskIntoConstraints = false };
            m_frame.AddSubview(m_row);
            nfloat border = MobileLook.NumericEditor.BorderThickness;
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_row.TopAnchor.ConstraintEqualTo(m_frame.TopAnchor, border),
                m_row.BottomAnchor.ConstraintEqualTo(m_frame.BottomAnchor, -border),
                m_row.LeadingAnchor.ConstraintEqualTo(m_frame.LeadingAnchor, border),
                m_row.TrailingAnchor.ConstraintEqualTo(m_frame.TrailingAnchor, -border),
            });

            // 6. The label (once it has text) and the frame, which takes the rest of the width
            m_outerRow = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: MobileLook.NumericEditor.LabelSpacing);
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
            if (this.Window == null)
            {
                this.SetPointerOverFrame(false);
            }
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

        /// <summary>Shows the value, dimmed, without letting it be typed or nudged; the text can still be selected and copied</summary>
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
        private string FrameBorderKey => MobileLook.NumericEditor.FrameBorderFor(m_errorMessage != null, m_isPointerOverFrame, m_isFocused);

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
            this.ShowWarningGlyph(errorMessage);
            m_frame.NeedsDisplay = true;
        }

        // ===========[ Helper Methods ]========================================
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

        // The same reason puts the glyph in the row, between the well and the increase button, only while there is an
        // error, and takes it out again when it clears. VoiceOver reads the error from the image's accessibility
        // description: an image view's AccessibilityLabel is read-only in the bindings (it comes from the image), unlike
        // UIKit's, which the iOS editor sets directly.
        private void ShowWarningGlyph (string errorMessage)
        {
            const int kGlyphIndex = 2;

            m_warningGlyph.ToolTip = errorMessage;
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
                nfloat halfBorder = (nfloat)(MobileLook.NumericEditor.BorderThickness / 2.0f);
                nfloat corner = MobileLook.NumericEditor.CornerRadius;
                NSBezierPath outline = NSBezierPath.FromRoundedRect(this.Bounds.Inset(halfBorder, halfBorder), corner, corner);

                // The surface is a gradient themed, and a solid control fill unthemed
                NSGradient gradient = ThemedColors.GetThemedGradient(ThemedColorKeys.NumericEditor_FrameBackground, this.EffectiveAppearance);
                if (gradient != null)
                {
                    gradient.DrawInBezierPath(outline, (nfloat)90);
                }
                else
                {
                    ThemedColors.Get(ThemedColorKeys.NumericEditor_FrameBackground).SetFill();
                    outline.Fill();
                }

                string borderKey = m_owner.TryGetTarget(out NumericEditor owner) ? owner.FrameBorderKey : ThemedColorKeys.NumericEditor_NormalBorder;
                outline.LineWidth = MobileLook.NumericEditor.BorderThickness;
                ThemedColors.Get(borderKey).SetStroke();
                outline.Stroke();
            }

            // The row inside is clipped to the frame's rounded corners, so a pressed end button doesn't fill past them
            private void ClipToCorners ()
            {
                if (this.Layer != null)
                {
                    this.Layer.CornerRadius = MobileLook.NumericEditor.CornerRadius;
                    this.Layer.MasksToBounds = true;
                }
            }
        }

        // The number's well: the text box color, inset from the frame's top and bottom so the frame shows around it, and
        // sunk into it by the edge lighting WinUI3's NumericEditor draws with two nested borders, a shadow along the top
        // and left and a light line along the bottom and right. The lighting is the same literal shading over any theme,
        // as it is there and on iOS and Android.
        private sealed class FieldWell : NSView
        {
            private const float kInset = MobileLook.NumericEditor.WellInset;
            private const float kShadowThickness = MobileLook.NumericEditor.WellShadowThickness;
            private const float kLightThickness = MobileLook.NumericEditor.WellLightThickness;
            private const float kTextPadding = MobileLook.NumericEditor.WellTextPadding;
            private static readonly NSColor kShadow = MobileLook.NumericEditor.WellShadow.ToNSColor();
            private static readonly NSColor kLight = MobileLook.NumericEditor.WellLight.ToNSColor();

            public FieldWell (NSTextField field)
            {
                this.TranslatesAutoresizingMaskIntoConstraints = false;
                field.TranslatesAutoresizingMaskIntoConstraints = false;
                this.AddSubview(field);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    field.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor, kShadowThickness + kTextPadding),
                    field.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor, -(kLightThickness + kTextPadding)),
                    field.CenterYAnchor.ConstraintEqualTo(this.CenterYAnchor),
                    field.TopAnchor.ConstraintGreaterThanOrEqualTo(this.TopAnchor, kInset + kShadowThickness),
                    field.BottomAnchor.ConstraintLessThanOrEqualTo(this.BottomAnchor, -(kInset + kLightThickness)),
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
                CGRect bounds = this.Bounds;
                nfloat left = bounds.X;
                nfloat top = bounds.Y + kInset;
                nfloat right = bounds.X + bounds.Width;
                nfloat bottom = bounds.Y + bounds.Height - kInset;

                // The light line runs the whole right side and the whole bottom, and the shadow the rest of the top and
                // left, the way the outer border (bottom and right) holds the inner one (top and left) on WinUI3
                Fill(kLight, right - kLightThickness, top, right, bottom);
                Fill(kLight, left, bottom - kLightThickness, right - kLightThickness, bottom);
                Fill(kShadow, left, top, right - kLightThickness, top + kShadowThickness);
                Fill(kShadow, left, top + kShadowThickness, left + kShadowThickness, bottom - kLightThickness);
                Fill(ThemedColors.Get(ThemedColorKeys.NumericEditor_TextBoxBackground), left + kShadowThickness, top + kShadowThickness, right - kLightThickness, bottom - kLightThickness);
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

        // A step button: the frame's surface showing through at rest, a wash of the bright highlight under the mouse, and
        // the full highlight while pressed, with a minus or plus stroked in the step glyph color (dimmed with the number
        // while read-only). It steps on press and repeats while held.
        private sealed class NudgeButton : ManagedButton
        {
            private readonly bool m_isPositive;

            public NudgeButton (bool isPositive)
            {
                m_isPositive = isPositive;
                this.IsRepeating = true;
                this.ToolTip = isPositive ? "Increase" : "Decrease";
                this.TranslatesAutoresizingMaskIntoConstraints = false;
            }

            // For a peer the runtime makes over an existing native button
            private NudgeButton (NativeHandle handle) : base(handle)
            {
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                const double kGlyphHalfLength = 4.0;
                const double kGlyphLineWidth = 1.5;

                // 1. The fill, only under the mouse or while pressed
                eManagedButtonState state = this.CurrentState;
                NSColor fill = null;
                if (state == eManagedButtonState.Pressed)
                {
                    fill = ThemedColors.Get(ThemedColorKeys.NumericEditor_StepPressedFill);
                }
                else if (state == eManagedButtonState.PointerOver)
                {
                    fill = WithOpacity(ThemedColors.Get(ThemedColorKeys.NumericEditor_StepPointerOverFill), MobileLook.NumericEditor.StepPointerOverOpacity);
                }

                if (fill != null)
                {
                    fill.SetFill();
                    NSBezierPath.FillRect(this.Bounds);
                }

                // 2. The minus or plus
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

                string glyphKey = (state == eManagedButtonState.Disabled) ? ThemedColorKeys.NumericEditor_ReadOnlyText : ThemedColorKeys.NumericEditor_StepGlyph;
                ThemedColors.Get(glyphKey).SetStroke();
                glyph.Stroke();
            }

            // The color at its own alpha scaled by opacity, read under the drawing appearance, so a translucent key stays
            // as translucent relative to itself
            private static NSColor WithOpacity (NSColor color, double opacity)
            {
                NSColor srgb = color?.UsingColorSpace(NSColorSpace.SRGBColorSpace);
                double alpha = (srgb != null) ? (double)srgb.AlphaComponent : 1.0;
                return color?.ColorWithAlphaComponent((nfloat)(alpha * opacity));
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
                    owner.m_controller.Commit();
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
