namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Theming;
    using AppKit;
    using CoreAnimation;
    using CoreGraphics;
    using Foundation;
    using ObjCRuntime;

    // ===========[ EditableTextBlock ]==========================================
    // Text that reads as a label until it is double-clicked, then turns into a text field to edit it
    // in place: the AppKit version of WPF's EditableTextBlock. EditTextInstigatorAction picks what
    // starts an edit, as on WPF: a double click (the default), the pointer coming over the text, or
    // only code through IsEditing. A click focuses the control, and with it focused Return starts an
    // edit too, as WPF's does.
    //
    // The edit rules are AJut.UX's EditableTextState, which the phones' EditableTextBlocks share, and
    // they are WPF's: Text follows every keystroke, Return keeps it, and Escape puts back what was
    // there before. A canceled edit is kept, and the next edit picks it up. An edit that ends any
    // other way (Tab or a click moving the focus on, the control leaving its window, or being
    // disabled or made read only) is kept or canceled by LostFocusEditClose. A click on empty space
    // ends nothing, and neither does the window going inactive (which ends one on WPF), the same as
    // in any Mac text field. An edit the pointer started ends when the pointer leaves, unless the
    // field has been clicked into by then.
    //
    // A read only EditableTextBlock never edits, and its text can still be selected and copied. With
    // no text the label shows NoTextSetMessage, faded and italic, and an edit that starts empty shows
    // it as the field's placeholder. While editing, the control is a text box well, outlined in the
    // focused text box border, with the field laid exactly where the label was, so the text does not
    // move as an edit starts. The selection and the caret are the system's.
    //
    // An edit's changes send the control's action (Activated), which is what BindText listens for;
    // TextChanged covers changes from code too. The field's delegate holds this control weakly: the
    // field is a subview, so anything it keeps that held the control back would be a cycle the GC
    // never breaks.
    public class EditableTextBlock : NSControl
    {
        private const string kDefaultNoTextSetMessage = "No text set";
        private const string kEditHelp = "Edits the text.";

        private readonly EditableTextState m_state = new EditableTextState();
        private readonly NSTextField m_label = NSTextField.CreateLabel(String.Empty);
        private readonly NSTextField m_field = new NSTextField();
        private readonly FieldDelegate m_fieldDelegate;
        private NSFont m_font = NSFont.SystemFontOfSize(NSFont.SystemFontSize);
        private bool m_isReadOnly;
        private bool m_isClosing;
        private bool m_isTextWrapping;
        private bool m_isEditFromPointer;
        private string m_noTextSetMessage = kDefaultNoTextSetMessage;
        private eEditTextInstigator m_editTextInstigatorAction = eEditTextInstigator.DoubleClick;
        private eEditTextInitializationBehavior m_editTextInitializationBehavior = eEditTextInitializationBehavior.SelectAll;
        private eEditTextCloseAction m_lostFocusEditClose = eEditTextCloseAction.Apply;

        // ===========[ Setup/Construction/Teardown ]===========================
        public EditableTextBlock ()
        {
            this.WantsLayer = true;

            // 1. The label, cut short at its end on one line until IsTextWrapping, and read by VoiceOver as this control
            m_label.TranslatesAutoresizingMaskIntoConstraints = false;
            m_label.LineBreakMode = NSLineBreakMode.TruncatingTail;
            m_label.MaximumNumberOfLines = 1;
            m_label.AccessibilityElement = false;

            // 2. The field, borderless inside the well this control draws, and laid where the label is, so the two put the
            //  text in the same place
            m_field.TranslatesAutoresizingMaskIntoConstraints = false;
            m_field.Bordered = false;
            m_field.Bezeled = false;
            m_field.DrawsBackground = false;
            m_field.FocusRingType = NSFocusRingType.None;
            m_field.Hidden = true;
            m_field.TextColor = ThemedColors.Get(ThemedColorKeys.EditableTextBlock_FieldText);
            m_field.Cell.Scrollable = true;
            m_field.Cell.Wraps = false;
            m_field.Cell.SetSendsActionOnEndEditing(false);
            m_fieldDelegate = new FieldDelegate(this);
            m_field.Delegate = m_fieldDelegate;

            this.AddSubview(m_label);
            this.AddSubview(m_field);

            // The label decides the height: at least its own plus the insets, and pulled to just that, weakly, so a parent that
            //  sets no height of its own gets the same one on every layout pass. The field rides in the middle.
            nfloat horizontalInset = MobileLook.EditableTextBlock.ContentHorizontalInset;
            nfloat verticalInset = MobileLook.EditableTextBlock.ContentVerticalInset;
            NSLayoutConstraint hugLabelHeight = m_label.TopAnchor.ConstraintEqualTo(this.TopAnchor, verticalInset);
            hugLabelHeight.Priority = (float)NSLayoutPriority.DefaultLow;
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_label.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor, horizontalInset),
                m_label.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor, -horizontalInset),
                m_label.CenterYAnchor.ConstraintEqualTo(this.CenterYAnchor),
                m_label.TopAnchor.ConstraintGreaterThanOrEqualTo(this.TopAnchor, verticalInset),
                hugLabelHeight,
                m_field.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor, horizontalInset),
                m_field.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor, -horizontalInset),
                m_field.CenterYAnchor.ConstraintEqualTo(this.CenterYAnchor),
                m_field.TopAnchor.ConstraintGreaterThanOrEqualTo(this.TopAnchor, verticalInset),
            });

            // 3. The pointer coming and going, for an edit started by the pointer. One area for the control's whole life,
            //  kept in step with its visible rect by AppKit, as ManagedButton's is.
            this.AddTrackingArea(new NSTrackingArea(
                CGRect.Empty,
                NSTrackingAreaOptions.MouseEnteredAndExited | NSTrackingAreaOptions.ActiveInActiveApp | NSTrackingAreaOptions.InVisibleRect,
                this,
                null
            ));

            this.Enabled = true;
            this.ApplyFont();
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native control whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls: the native control keeps the subviews it already has.
        /// None of its managed state survives a dispose, so a call that needs that state can still fail: dispose it only
        /// once AppKit is done with it.
        /// </summary>
        protected EditableTextBlock (NativeHandle handle) : base(handle)
        {
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                m_field.Delegate = null;
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        /// <summary>
        /// Raised whenever <see cref="Text"/> changes: on every keystroke while editing, on a cancel, and from code. The
        /// changes an edit makes also send the control's action (Activated), which is what a binding listens for.
        /// </summary>
        public event EventHandler<EventArgs<string>> TextChanged;

        /// <summary>Raised when an edit starts or ends</summary>
        public event EventHandler IsEditingChanged;

        // ===========[ Properties ]============================================
        public override bool IsFlipped => true;

        /// <summary>The well is the layer's own color, set in UpdateLayer, so nothing is drawn</summary>
        public override bool WantsUpdateLayer => true;

        /// <summary>The text. While editing it follows every keystroke, as WPF's does.</summary>
        public string Text
        {
            get => m_state.Text;
            set
            {
                if (m_state.Text == value)
                {
                    return;
                }

                m_state.Text = value;
                if (m_state.IsEditing)
                {
                    this.ShowFieldText(value);
                }

                this.RefreshLabel();
                this.RaiseTextChanged(isFromEdit: false);
            }
        }

        /// <summary>
        /// Whether the text is being edited. Setting it starts an edit (with the field focused, once in a window) or keeps
        /// the one going; clearing it keeps the edit. Starting is refused while <see cref="IsReadOnly"/> or disabled.
        /// </summary>
        public bool IsEditing
        {
            get => m_state.IsEditing;
            set
            {
                if (value)
                {
                    this.BeginEdit(isFromPointer: false);
                }
                else
                {
                    this.CloseEdit(eEditTextCloseAction.Apply);
                }
            }
        }

        /// <summary>Whether <see cref="Text"/> is null or empty</summary>
        public bool IsEmpty => m_state.IsEmpty;

        /// <summary>The text as it was when the current edit started, which a cancel puts back</summary>
        public string PreviousText => m_state.PreviousText;

        /// <summary>The text of the last canceled edit, which the next edit starts from; null once an edit is kept</summary>
        public string EditText => m_state.EditText;

        /// <summary>Whether the current edit started with no text</summary>
        public bool StartedEditingEmpty => m_state.StartedEditingEmpty;

        /// <summary>
        /// Whether editing is refused. The text can still be selected and copied. Turning it on during an edit ends the
        /// edit as losing focus would.
        /// </summary>
        public bool IsReadOnly
        {
            get => m_isReadOnly;
            set
            {
                m_isReadOnly = value;
                if (value)
                {
                    this.CloseEdit(m_lostFocusEditClose);
                }

                this.RefreshLabel();
            }
        }

        /// <summary>
        /// Whether the control takes the mouse, the focus and edits. Disabling it during an edit ends the edit as losing
        /// focus would, and draws the label in the disabled color.
        /// </summary>
        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                if (base.Enabled == value)
                {
                    return;
                }

                base.Enabled = value;
                if (!value)
                {
                    this.CloseEdit(m_lostFocusEditClose);
                }

                m_field.Enabled = value;
                this.RefreshLabel();
            }
        }

        /// <summary>What the label shows with no text, faded and italic. "No text set" by default, as on WPF.</summary>
        public string NoTextSetMessage
        {
            get => m_noTextSetMessage;
            set
            {
                m_noTextSetMessage = value ?? String.Empty;
                this.RefreshLabel();
            }
        }

        /// <summary>
        /// What starts an edit: a double click by default, the pointer coming over the text, or only code. The property is
        /// spelled as the phones' is; WPF's is EditTextInstagatorAction.
        /// </summary>
        public eEditTextInstigator EditTextInstigatorAction
        {
            get => m_editTextInstigatorAction;
            set
            {
                m_editTextInstigatorAction = value;
                this.RefreshLabel();
            }
        }

        /// <summary>Where the caret goes as an edit starts. All the text is selected by default.</summary>
        public eEditTextInitializationBehavior EditTextInitializationBehavior
        {
            get => m_editTextInitializationBehavior;
            set => m_editTextInitializationBehavior = value;
        }

        /// <summary>
        /// Whether an edit that ends any way but Return or Escape is kept or canceled: Tab or a click moving the focus on,
        /// the control leaving its window, or it being disabled or made read only. Kept by default.
        /// </summary>
        public eEditTextCloseAction LostFocusEditClose
        {
            get => m_lostFocusEditClose;
            set => m_lostFocusEditClose = value;
        }

        /// <summary>
        /// Whether the label wraps its text onto more lines, as WPF's TextBlockTextWrapping does. Off by default: one line,
        /// cut short with an ellipsis, as WPF's TextBlockTextTrimming would.
        /// </summary>
        public bool IsTextWrapping
        {
            get => m_isTextWrapping;
            set
            {
                m_isTextWrapping = value;
                m_label.MaximumNumberOfLines = value ? 0 : 1;
                m_label.LineBreakMode = value ? NSLineBreakMode.ByWordWrapping : NSLineBreakMode.TruncatingTail;
                m_label.Cell.Wraps = value;

                // A wrapping label gives way across, so it wraps rather than pushing the control wider
                m_label.SetContentCompressionResistancePriority(value ? (float)NSLayoutPriority.DefaultLow : (float)NSLayoutPriority.DefaultHigh, NSLayoutConstraintOrientation.Horizontal);
                this.NeedsLayout = true;
            }
        }

        /// <summary>The font of the label and the field alike; the empty message is its italic. The system font by default.</summary>
        public override NSFont Font
        {
            get => m_font;
            set
            {
                m_font = value ?? NSFont.SystemFontOfSize(NSFont.SystemFontSize);
                this.ApplyFont();
            }
        }

        public override bool AcceptsFirstResponder () => this.CanStartEditFromUser() && !m_state.IsEditing;

        public override bool AcceptsFirstMouse (NSEvent theEvent) => true;

        // The focus ring follows the control's bounds, rounded as the well is
        public override CGRect FocusRingMaskBounds => this.Bounds;

        // Out of an edit the control is one element to VoiceOver, read as its text and pressed to edit; during one, the
        //  field is reached on its own, as the focused element
        public override bool AccessibilityElement => !m_state.IsEditing;

        public override string AccessibilityRole => this.CanStartEditFromUser() ? NSAccessibilityRoles.ButtonRole : NSAccessibilityRoles.StaticTextRole;

        public override bool AccessibilityEnabled => this.Enabled;

        public override string AccessibilityLabel
        {
            get
            {
                string label = base.AccessibilityLabel;
                if (!String.IsNullOrEmpty(label))
                {
                    return label;
                }

                return m_state.IsEmpty ? m_noTextSetMessage : m_state.Text;
            }
        }

        public override string AccessibilityHelp => this.CanStartEditFromUser() ? kEditHelp : base.AccessibilityHelp;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>VoiceOver's press starts an edit, whatever starts one by mouse, unless editing is Manually</summary>
        public override bool AccessibilityPerformPress ()
        {
            if (!this.CanStartEditFromUser())
            {
                return false;
            }

            this.BeginEdit(isFromPointer: false);
            return m_state.IsEditing;
        }

        // While editing the field takes its own clicks, and a read only label its own, for selecting its text. Otherwise
        //  every click on the label is this control's, so a double click on it starts an edit.
        public override NSView HitTest (CGPoint aPoint)
        {
            NSView hit = base.HitTest(aPoint);
            if ((hit == null) || m_state.IsEditing || m_isReadOnly)
            {
                return hit;
            }

            return this;
        }

        public override void MouseDown (NSEvent theEvent)
        {
            if (!this.Enabled)
            {
                return;
            }

            if ((theEvent.ClickCount == 2) && (m_editTextInstigatorAction == eEditTextInstigator.DoubleClick))
            {
                this.BeginEdit(isFromPointer: false);
                return;
            }

            // A click focuses the control, so Return can start an edit from the keyboard
            if (this.AcceptsFirstResponder())
            {
                this.Window?.MakeFirstResponder(this);
            }
        }

        public override void KeyDown (NSEvent theEvent)
        {
            if ((theEvent.Characters == "\r") && this.CanStartEditFromUser())
            {
                this.BeginEdit(isFromPointer: false);
                return;
            }

            base.KeyDown(theEvent);
        }

        // The pointer starts an edit without taking the focus from wherever it is: the field shows, and a click into it
        //  is what focuses it
        public override void MouseEntered (NSEvent theEvent)
        {
            if ((m_editTextInstigatorAction == eEditTextInstigator.MouseOver) && this.CanStartEditFromUser())
            {
                this.BeginEdit(isFromPointer: true);
            }
        }

        public override void MouseExited (NSEvent theEvent)
        {
            if (m_isEditFromPointer && (m_field.CurrentEditor == null))
            {
                this.CloseEdit(eEditTextCloseAction.Apply);
            }
        }

        public override void DrawFocusRingMask ()
        {
            nfloat radius = MobileLook.EditableTextBlock.FieldCornerRadius;
            NSBezierPath.FromRoundedRect(this.Bounds, radius, radius).Fill();
        }

        // Leaving the window ends an edit the way losing focus does. It ends here, while the window is still there to take
        //  the focus back from the field. Coming into a window from none keeps an edit started out of one.
        public override void ViewWillMoveToWindow (NSWindow newWindow)
        {
            if ((this.Window != null) && (newWindow != this.Window))
            {
                this.CloseEdit(m_lostFocusEditClose);
            }

            base.ViewWillMoveToWindow(newWindow);
        }

        // An edit started out of a window takes the focus once it has one
        public override void ViewDidMoveToWindow ()
        {
            base.ViewDidMoveToWindow();
            if ((this.Window != null) && m_state.IsEditing && !m_isEditFromPointer && (m_field.CurrentEditor == null))
            {
                this.FocusField();
            }
        }

        // A wrapping label is told how wide it is, so it measures its height for that width
        public override void Layout ()
        {
            base.Layout();
            if (m_isTextWrapping && (m_label.PreferredMaxLayoutWidth != m_label.Frame.Width))
            {
                m_label.PreferredMaxLayoutWidth = m_label.Frame.Width;
                base.Layout();
            }
        }

        // Layer colors are CGColors, resolved once when taken, so they are taken here, where the drawing appearance is this
        //  view's, and taken again when it changes, as ThemedView does
        public override void UpdateLayer ()
        {
            const double kWellBorderThickness = 1.0;

            CALayer layer = this.Layer;
            if (layer == null)
            {
                return;
            }

            bool isWell = m_state.IsEditing;
            layer.BackgroundColor = isWell ? ThemedColors.Get(ThemedColorKeys.EditableTextBlock_FieldBackground).CGColor : null;
            layer.BorderColor = isWell ? ThemedColors.Get(ThemedColorKeys.EditableTextBlock_FieldBorder).CGColor : null;
            layer.BorderWidth = isWell ? (nfloat)kWellBorderThickness : (nfloat)0.0;
            layer.CornerRadius = MobileLook.EditableTextBlock.FieldCornerRadius;

            // The empty message's faded color is the label color with an alpha laid on, which may be taken once rather
            //  than followed, so it is made again here, where a light or dark switch and a theme load or unload both land
            this.RefreshEmptyMessages();
        }

        public override void ViewDidChangeEffectiveAppearance ()
        {
            base.ViewDidChangeEffectiveAppearance();
            this.NeedsDisplay = true;
        }

        // ===========[ Helper Methods ]========================================
        private bool CanStartEditFromUser () => this.Enabled && !m_isReadOnly && (m_editTextInstigatorAction != eEditTextInstigator.Manually);

        private void BeginEdit (bool isFromPointer)
        {
            if (!this.Enabled || !m_state.TryBeginEdit(m_isReadOnly))
            {
                return;
            }

            // The field shows the edit's text (a canceled edit's, picked up), with the empty message as its placeholder when
            //  the edit starts empty, as WPF shows it behind an edit that starts empty
            m_isEditFromPointer = isFromPointer;
            string textBefore = m_state.PreviousText;
            m_field.StringValue = m_state.Text ?? String.Empty;
            m_field.PlaceholderAttributedString = new NSAttributedString(String.Empty);
            this.RefreshEmptyMessages();

            m_label.Hidden = true;
            m_field.Hidden = false;
            this.NeedsDisplay = true;
            if (!isFromPointer)
            {
                this.FocusField();
            }

            if (m_state.Text != textBefore)
            {
                this.RaiseTextChanged(isFromEdit: true);
            }

            this.IsEditingChanged?.Invoke(this, EventArgs.Empty);
        }

        // Ends an edit, if one is going on. Taking the focus from the field comes back here through its delegate, so the end
        //  is guarded against running twice. An edit ending because the focus already went somewhere else leaves the focus
        //  where it went.
        private void CloseEdit (eEditTextCloseAction action, bool isFocusLeaving = false)
        {
            if (m_isClosing || !m_state.IsEditing)
            {
                return;
            }

            m_isClosing = true;
            try
            {
                string textBefore = m_state.Text;
                m_state.Close(action);
                m_isEditFromPointer = false;

                // The field's text lives in the window's field editor while it is focused, and the window ends that session
                //  when it gives the focus to something else: this control, so Return starts another edit, or the window
                //  itself when this control will not take it
                NSWindow window = this.Window;
                if (!isFocusLeaving && (window != null) && (m_field.CurrentEditor != null))
                {
                    window.MakeFirstResponder(this.AcceptsFirstResponder() ? this : null);
                }

                m_field.Hidden = true;
                m_label.Hidden = false;
                this.NeedsDisplay = true;
                this.RefreshLabel();

                if (m_state.Text != textBefore)
                {
                    this.RaiseTextChanged(isFromEdit: true);
                }

                this.IsEditingChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                m_isClosing = false;
            }
        }

        // Taking the focus selects all the text. The starting selection goes on the field editor, the window's own text
        //  view, which holds the field's text while it is focused.
        private void FocusField ()
        {
            NSWindow window = this.Window;
            if ((window == null) || !window.MakeFirstResponder(m_field))
            {
                return;
            }

            NSText editor = m_field.CurrentEditor;
            if (editor != null)
            {
                EditableTextState.Selection selection = m_state.GetStartingSelection(m_editTextInitializationBehavior);
                editor.SelectedRange = new NSRange(selection.Start, selection.End - selection.Start);
            }
        }

        // Writing the text a focused field shows goes through its field editor, so the edit goes on with the caret where it
        //  was, clamped to the new length; unfocused, the field's own value is the text
        private void ShowFieldText (string text)
        {
            text ??= String.Empty;
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

        private void OnFieldEdited ()
        {
            if (m_state.IsEditing)
            {
                m_state.Text = m_field.StringValue;
                this.RaiseTextChanged(isFromEdit: true);
            }
        }

        private void OnFieldEnded () => this.CloseEdit(m_lostFocusEditClose, isFocusLeaving: true);

        private void CloseEditAndMoveFocus (bool isForward)
        {
            this.CloseEdit(m_lostFocusEditClose);
            NSWindow window = this.Window;
            if (window == null)
            {
                return;
            }

            if (isForward)
            {
                window.SelectKeyViewFollowingView(this);
            }
            else
            {
                window.SelectKeyViewPrecedingView(this);
            }
        }

        private void RefreshLabel ()
        {
            if (m_state.IsEmpty)
            {
                m_label.AttributedStringValue = this.MakeEmptyMessage(MobileLook.EditableTextBlock.EmptyMessageOpacity);
            }
            else
            {
                m_label.Font = m_font;
                m_label.TextColor = this.LabelColor();
                m_label.StringValue = m_state.Text;
            }

            // A read only label can have its text selected and copied
            m_label.Selectable = m_isReadOnly && this.Enabled;
        }

        // The empty message wherever it shows now: as the field's placeholder during an edit that started empty, and on the
        //  label while there is no text
        private void RefreshEmptyMessages ()
        {
            if (m_state.IsEditing)
            {
                if (m_state.StartedEditingEmpty)
                {
                    m_field.PlaceholderAttributedString = this.MakeEmptyMessage(MobileLook.EditableTextBlock.EditingEmptyMessageOpacity);
                }
            }
            else if (m_state.IsEmpty)
            {
                m_label.AttributedStringValue = this.MakeEmptyMessage(MobileLook.EditableTextBlock.EmptyMessageOpacity);
            }
        }

        private void ApplyFont ()
        {
            m_field.Font = m_font;
            this.RefreshLabel();
        }

        private NSColor LabelColor ()
        {
            return ThemedColors.Get(this.Enabled ? ThemedColorKeys.EditableTextBlock_LabelText : ThemedColorKeys.EditableTextBlock_LabelTextDisabled);
        }

        // The empty message: the label's color faded, in the italic of the font, as on WPF
        private NSAttributedString MakeEmptyMessage (float opacity)
        {
            NSFont italic = NSFontManager.SharedFontManager.ConvertFont(m_font, NSFontTraitMask.Italic) ?? m_font;
            return new NSAttributedString(
                m_noTextSetMessage,
                new NSStringAttributes
                {
                    Font = italic,
                    ForegroundColor = this.LabelColor().ColorWithAlphaComponent(opacity),
                }
            );
        }

        // A change an edit made also goes out as the control's action, which a binding listens for; one made from code does
        //  not, the same as every NSControl
        private void RaiseTextChanged (bool isFromEdit)
        {
            this.TextChanged?.Invoke(this, new EventArgs<string>(m_state.Text));

            // Sent the way -[NSControl sendAction:to:] sends it: nothing with no action, and up the responder chain with no
            //  target. The bindings' NSControl.SendAction throws on a null action or target instead.
            Selector action = this.Action;
            if (isFromEdit && (action != null))
            {
                NSApplication.SharedApplication.SendAction(action, this.Target, this);
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // The field's one delegate: keystrokes, the end of editing, and the Return, Escape and Tab keys. It holds its owner
        //  weakly, since the field keeps it and the owner keeps the field.
        private sealed class FieldDelegate : NSTextFieldDelegate
        {
            private readonly WeakReference<EditableTextBlock> m_owner;

            public FieldDelegate (EditableTextBlock owner)
            {
                m_owner = new WeakReference<EditableTextBlock>(owner);
            }

            // For a peer the runtime makes over an existing native delegate, which has no owner to tell
            private FieldDelegate (NativeHandle handle) : base(handle)
            {
                m_owner = new WeakReference<EditableTextBlock>(null);
            }

            public override void Changed (NSNotification notification)
            {
                if (m_owner.TryGetTarget(out EditableTextBlock owner))
                {
                    owner.OnFieldEdited();
                }
            }

            public override void EditingEnded (NSNotification notification)
            {
                if (m_owner.TryGetTarget(out EditableTextBlock owner))
                {
                    owner.OnFieldEnded();
                }
            }

            // Return keeps the edit and Escape cancels it, as on WPF. Tab and Shift+Tab end it the LostFocusEditClose way and
            //  move the focus on from the control, since the control takes the focus back as the edit ends, and the key
            //  view next to it could be the control itself.
            public override bool DoCommandBySelector (NSControl control, NSTextView textView, Selector commandSelector)
            {
                if (!m_owner.TryGetTarget(out EditableTextBlock owner))
                {
                    return false;
                }

                switch (commandSelector.Name)
                {
                    case "insertNewline:":
                        owner.CloseEdit(eEditTextCloseAction.Apply);
                        return true;

                    case "cancelOperation:":
                        owner.CloseEdit(eEditTextCloseAction.Cancel);
                        return true;

                    case "insertTab:":
                        owner.CloseEditAndMoveFocus(isForward: true);
                        return true;

                    case "insertBacktab:":
                        owner.CloseEditAndMoveFocus(isForward: false);
                        return true;

                    default:
                        return false;
                }
            }
        }
    }
}
