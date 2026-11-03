namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Theming;
    using CoreFoundation;
    using CoreGraphics;
    using Foundation;
    using ObjCRuntime;
    using UIKit;

    /// <summary>
    /// Text that reads as a label until it is tapped, then turns into a text field to edit it, the phone version of WPF's
    /// EditableTextBlock: tap to rename. <see cref="EditTextInstigatorAction"/> picks the gesture (a tap by default, or a
    /// double tap, a long press, or only code through <see cref="IsEditing"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The edit rules are AJut.UX's <see cref="EditableTextState"/>, which Android's EditableTextBlock shares, and they are
    /// WPF's: <see cref="Text"/> follows every keystroke, Done keeps it, and Cancel puts back what was there before. A
    /// canceled edit is kept, and the next edit picks it up. An edit that ends any other way (the field giving up focus,
    /// the control leaving its window or being disabled) is kept or canceled by <see cref="LostFocusEditClose"/>. A
    /// hardware keyboard's Escape cancels.
    /// </para>
    /// <para>
    /// A phone has no Escape key, so the only cancel on a touch screen is a <see cref="LostFocusEditClose"/> of Cancel.
    /// A read only EditableTextBlock never starts editing. With no text, the label shows <see cref="NoTextSetMessage"/>,
    /// faded and italic; an edit that starts empty shows it as the field's placeholder. VoiceOver starts an edit with a
    /// double tap, whichever gesture is set, unless editing is Manually.
    /// </para>
    /// <para>
    /// The field holds this control weakly and the gestures' handlers are static, since anything a UIKit view keeps that
    /// held the view back would be a cycle the GC never breaks.
    /// </para>
    /// </remarks>
    public class EditableTextBlock : UIControl
    {
        private const string kDefaultNoTextSetMessage = "No text set";
        private const string kEditHint = "Edits the text.";

        private readonly EditableTextState m_state = new EditableTextState();
        private readonly UILabel m_label = new UILabel { TranslatesAutoresizingMaskIntoConstraints = false, Lines = 1, LineBreakMode = UILineBreakMode.TailTruncation };
        private readonly EditField m_field;
        private readonly UITapGestureRecognizer m_tap = new UITapGestureRecognizer(OnTapGesture) { CancelsTouchesInView = false };
        private readonly UILongPressGestureRecognizer m_longPress = new UILongPressGestureRecognizer(OnLongPressGesture);
        private UIFont m_font = UIFont.PreferredBody;
        private bool m_isReadOnly;
        private bool m_isClosing;
        private bool m_isTextWrapping;
        private int m_editRevision;
        private string m_noTextSetMessage = kDefaultNoTextSetMessage;
        private eEditTextTouchInstigator m_editTextInstigatorAction = eEditTextTouchInstigator.Tap;
        private eEditTextInitializationBehavior m_editTextInitializationBehavior = eEditTextInitializationBehavior.SelectAll;
        private eEditTextCloseAction m_lostFocusEditClose = eEditTextCloseAction.Apply;

        // ===========[ Setup/Construction/Teardown ]===================================
        public EditableTextBlock () : base(CGRect.Empty)
        {
            m_field = new EditField(this);
            this.Setup();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native view.</summary>
        protected EditableTextBlock (NativeHandle handle) : base(handle)
        {
            m_field = new EditField(this);
            this.Setup();
        }

        private void Setup ()
        {
            // 1. The label, and the field laid over the whole control, its text inset by the same amount as the label's,
            //  so the text stays put as an edit starts
            m_field.Hidden = true;
            m_field.ReturnKeyType = UIReturnKeyType.Done;
            m_field.BorderStyle = UITextBorderStyle.None;
            m_field.ShouldReturn = OnFieldShouldReturn;
            m_field.Layer.CornerRadius = MobileLook.EditableTextBlock.FieldCornerRadius;
            // The text box key's own unthemed default is the page's background (a text box draws its own border), which
            // would leave this borderless well invisible, so with no theme the well is a fill of its own
            m_field.BackgroundColor = (ThemedColors.IsThemeLoaded ? ThemedColors.Get(MobileLook.EditableTextBlock.FieldBackground) : null) ?? UIColor.TertiarySystemFill;
            m_field.TextColor = ThemedColors.Get(MobileLook.EditableTextBlock.FieldText) ?? UIColor.Label;

            // The tint is the caret and the selection; with no theme the field takes the view's tint, as stock fields do
            m_field.TintColor = ThemedColors.Get(MobileLook.EditableTextBlock.FieldSelection);

            this.AddSubview(m_label);
            this.AddSubview(m_field);
            float horizontalInset = MobileLook.EditableTextBlock.ContentHorizontalInset;
            float verticalInset = MobileLook.EditableTextBlock.ContentVerticalInset;
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                this.HeightAnchor.ConstraintGreaterThanOrEqualTo(MobileLook.EditableTextBlock.MinTouchHeight),
                m_label.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor, horizontalInset),
                m_label.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor, -horizontalInset),
                m_label.CenterYAnchor.ConstraintEqualTo(this.CenterYAnchor),
                m_label.TopAnchor.ConstraintGreaterThanOrEqualTo(this.TopAnchor, verticalInset),
                m_field.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_field.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                m_field.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_field.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
            });

            // 2. The gestures that start an edit, on the control itself so their static handlers find it as the view
            this.AddGestureRecognizer(m_tap);
            this.AddGestureRecognizer(m_longPress);
            this.ApplyFont();
            this.RefreshLabel();
        }

        public override void MovedToWindow ()
        {
            base.MovedToWindow();

            // Leaving the window ends an edit the way losing focus does; an edit started out of a window takes focus once
            // it has one
            if (this.Window == null)
            {
                this.CloseEdit(m_lostFocusEditClose);
            }
            else if (m_state.IsEditing && !m_field.IsFirstResponder)
            {
                this.FocusField();
            }
        }

        // ===========[ Events ]===================================
        /// <summary>
        /// Raised whenever <see cref="Text"/> changes: on every keystroke while editing, on a cancel, and from code. The
        /// changes an edit makes also send ValueChanged, which is what a binding listens for.
        /// </summary>
        public event EventHandler<EventArgs<string>> TextChanged;

        /// <summary>Raised when an edit starts or ends</summary>
        public event EventHandler IsEditingChanged;

        private static void OnTapGesture (UITapGestureRecognizer recognizer)
        {
            if ((recognizer.State == UIGestureRecognizerState.Recognized) && (recognizer.View is EditableTextBlock owner))
            {
                owner.BeginEdit();
            }
        }

        private static void OnLongPressGesture (UILongPressGestureRecognizer recognizer)
        {
            if ((recognizer.State == UIGestureRecognizerState.Began) && (recognizer.View is EditableTextBlock owner))
            {
                owner.BeginEdit();
            }
        }

        private static bool OnFieldShouldReturn (UITextField field)
        {
            if ((field is EditField editField) && editField.TryGetOwner(out EditableTextBlock owner))
            {
                owner.CloseEdit(eEditTextCloseAction.Apply);
            }

            return false;
        }

        // ===========[ Properties ]===================================
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
                    m_field.Text = value;
                }

                this.RefreshLabel();
                this.RaiseTextChanged(isFromEdit: false);
            }
        }

        /// <summary>
        /// Whether the text is being edited. Setting it starts an edit (with the keyboard up, once in a window) or keeps
        /// the one going; clearing it keeps the edit. Starting is refused while <see cref="IsReadOnly"/> or disabled.
        /// </summary>
        public bool IsEditing
        {
            get => m_state.IsEditing;
            set
            {
                if (value)
                {
                    this.BeginEdit();
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

        /// <summary>Whether editing is refused. Turning it on during an edit ends the edit as losing focus would.</summary>
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
        /// Whether the control takes touches and edits. A UIControl's Enabled never reaches its subviews, so the field is
        /// set here. Disabling it during an edit ends the edit as losing focus would, and draws the label in the disabled
        /// color.
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
                m_noTextSetMessage = value ?? string.Empty;
                this.RefreshLabel();
            }
        }

        /// <summary>The gesture that starts an edit. A tap by default.</summary>
        public eEditTextTouchInstigator EditTextInstigatorAction
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
        /// Whether an edit that ends any way but Done or Escape is kept or canceled: the field giving up focus, the control
        /// leaving its window, or it being disabled or made read only. Kept by default.
        /// </summary>
        public eEditTextCloseAction LostFocusEditClose
        {
            get => m_lostFocusEditClose;
            set => m_lostFocusEditClose = value;
        }

        /// <summary>Whether the label wraps its text onto more lines. Off by default: one line, cut short with an ellipsis.</summary>
        public bool IsTextWrapping
        {
            get => m_isTextWrapping;
            set
            {
                m_isTextWrapping = value;
                m_label.Lines = value ? 0 : 1;
                m_label.LineBreakMode = value ? UILineBreakMode.WordWrap : UILineBreakMode.TailTruncation;
            }
        }

        /// <summary>The font of the label and the field alike; the empty message is its italic. The body font by default.</summary>
        public UIFont Font
        {
            get => m_font;
            set
            {
                m_font = value ?? UIFont.PreferredBody;
                this.ApplyFont();
            }
        }

        // ===========[ Public Interface Methods ]===================================
        /// <summary>VoiceOver's double tap starts an edit, whichever gesture is set, unless editing is Manually</summary>
        public override bool AccessibilityActivate ()
        {
            if (!this.CanStartEditFromTouch())
            {
                return false;
            }

            this.BeginEdit();
            return m_state.IsEditing;
        }

        // ===========[ Helper Methods ]===================================
        private bool CanStartEditFromTouch () => this.Enabled && !m_isReadOnly && (m_editTextInstigatorAction != eEditTextTouchInstigator.Manually);

        private void BeginEdit ()
        {
            if (!this.Enabled || !m_state.TryBeginEdit(m_isReadOnly))
            {
                return;
            }

            // The field shows the edit's text (a canceled edit's, picked up), with the empty message as its placeholder when
            // the edit starts empty, as WPF shows it behind an edit that starts empty
            string textBefore = m_state.PreviousText;
            m_field.Text = m_state.Text ?? string.Empty;
            m_field.AttributedPlaceholder = m_state.StartedEditingEmpty ? this.MakeEmptyMessage(MobileLook.EditableTextBlock.EditingEmptyMessageOpacity) : null;
            m_label.Hidden = true;
            m_field.Hidden = false;
            this.RefreshGestures();

            // During the edit VoiceOver reaches the field itself, so the control stops being one element, and VoiceOver is
            // told the layout changed and moves to the field
            this.IsAccessibilityElement = false;
            this.FocusField();
            UIAccessibility.PostNotification(UIAccessibilityPostNotification.LayoutChanged, m_field);

            if (m_state.Text != textBefore)
            {
                this.RaiseTextChanged(isFromEdit: true);
            }

            this.IsEditingChanged?.Invoke(this, EventArgs.Empty);
        }

        // Ends an edit, if one is going on. Giving up focus comes back here through the field, so the end is guarded against
        // running twice.
        private void CloseEdit (eEditTextCloseAction action)
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
                m_field.ResignFirstResponder();
                m_field.Hidden = true;
                m_label.Hidden = false;
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

        // The keyboard comes up as the field takes focus, which needs a window. UIKit places the caret itself as a field
        // takes focus, so the starting selection is set a turn of the run loop later, if the same edit is still going.
        private void FocusField ()
        {
            if ((this.Window == null) || !m_field.BecomeFirstResponder())
            {
                return;
            }

            int revision = ++m_editRevision;
            var owner = new WeakReference<EditableTextBlock>(this);
            DispatchQueue.MainQueue.DispatchAsync(() =>
            {
                if (owner.TryGetTarget(out EditableTextBlock target) && (target.m_editRevision == revision) && target.m_state.IsEditing)
                {
                    target.ApplyStartingSelection();
                }
            });
        }

        private void ApplyStartingSelection ()
        {
            EditableTextState.Selection selection = m_state.GetStartingSelection(m_editTextInitializationBehavior);
            UITextPosition start = m_field.GetPosition(m_field.BeginningOfDocument, selection.Start);
            UITextPosition end = m_field.GetPosition(m_field.BeginningOfDocument, selection.End);
            if ((start != null) && (end != null))
            {
                m_field.SelectedTextRange = m_field.GetTextRange(start, end);
            }
        }

        private void OnFieldEdited ()
        {
            if (m_state.IsEditing)
            {
                m_state.Text = m_field.Text;
                this.RaiseTextChanged(isFromEdit: true);
            }
        }

        private void OnFieldEnded () => this.CloseEdit(m_lostFocusEditClose);

        private void OnFieldEscape () => this.CloseEdit(eEditTextCloseAction.Cancel);

        private void RefreshLabel ()
        {
            if (m_state.IsEmpty)
            {
                m_label.AttributedText = this.MakeEmptyMessage(MobileLook.EditableTextBlock.EmptyMessageOpacity);
            }
            else
            {
                m_label.AttributedText = null;
                m_label.Font = m_font;
                m_label.Text = m_state.Text;
                m_label.TextColor = this.LabelColor();
            }

            this.RefreshGestures();

            // Out of an edit the control is one element to VoiceOver, read as its text; during one, the field is reached
            // on its own
            bool canEdit = this.CanStartEditFromTouch();
            this.IsAccessibilityElement = !m_state.IsEditing;
            this.AccessibilityLabel = m_state.IsEmpty ? m_noTextSetMessage : m_state.Text;
            this.AccessibilityHint = canEdit ? kEditHint : null;
            this.AccessibilityTraits = this.Enabled ? UIAccessibilityTrait.None : UIAccessibilityTrait.NotEnabled;
        }

        // The gestures only listen when one of them starts an edit, and not during one, so taps inside the field go to it
        private void RefreshGestures ()
        {
            bool canEdit = this.CanStartEditFromTouch() && !m_state.IsEditing;
            m_tap.NumberOfTapsRequired = (m_editTextInstigatorAction == eEditTextTouchInstigator.DoubleTap) ? 2u : 1u;
            m_tap.Enabled = canEdit
                && ((m_editTextInstigatorAction == eEditTextTouchInstigator.Tap) || (m_editTextInstigatorAction == eEditTextTouchInstigator.DoubleTap));
            m_longPress.Enabled = canEdit && (m_editTextInstigatorAction == eEditTextTouchInstigator.LongPress);
        }

        private void ApplyFont ()
        {
            m_field.Font = m_font;
            this.RefreshLabel();
        }

        private UIColor LabelColor ()
        {
            return this.Enabled
                ? (ThemedColors.Get(MobileLook.EditableTextBlock.LabelText) ?? UIColor.Label)
                : (ThemedColors.Get(MobileLook.EditableTextBlock.LabelTextDisabled) ?? UIColor.TertiaryLabel);
        }

        // The empty message: the label's color faded, in the italic of the font, as on WPF
        private NSAttributedString MakeEmptyMessage (float opacity)
        {
            UIFontDescriptor italic = m_font.FontDescriptor.CreateWithTraits(UIFontDescriptorSymbolicTraits.Italic) ?? m_font.FontDescriptor;
            return new NSAttributedString(
                m_noTextSetMessage,
                new UIStringAttributes
                {
                    Font = UIFont.FromDescriptor(italic, 0f),
                    ForegroundColor = this.LabelColor().ColorWithAlpha(opacity),
                }
            );
        }

        // A change an edit made also goes out as ValueChanged, the control event a binding listens for; one made from code
        // does not, the same as every UIControl
        private void RaiseTextChanged (bool isFromEdit)
        {
            this.TextChanged?.Invoke(this, new EventArgs<string>(m_state.Text));
            if (isFromEdit)
            {
                this.SendActionForControlEvents(UIControlEvent.ValueChanged);
            }
        }

        // ===========[ Subclasses/structs ]===================================
        // The field holds its owner weakly, and listens to its own events (a self-subscription, which holds nothing back)
        private sealed class EditField : UITextField
        {
            private readonly WeakReference<EditableTextBlock> m_owner;

            public EditField (EditableTextBlock owner) : base(CGRect.Empty)
            {
                m_owner = new WeakReference<EditableTextBlock>(owner);
                this.TranslatesAutoresizingMaskIntoConstraints = false;
                this.EditingChanged += this.OnSelfEditingChanged;
                this.Ended += this.OnSelfEnded;
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing)
                {
                    this.EditingChanged -= this.OnSelfEditingChanged;
                    this.Ended -= this.OnSelfEnded;
                }

                base.Dispose(disposing);
            }

            public bool TryGetOwner (out EditableTextBlock owner) => m_owner.TryGetTarget(out owner);

            // The text sits in by the label's insets, so it stays where the label drew it
            public override CGRect TextRect (CGRect forBounds) => forBounds.Inset(MobileLook.EditableTextBlock.ContentHorizontalInset, MobileLook.EditableTextBlock.ContentVerticalInset);

            public override CGRect EditingRect (CGRect forBounds) => forBounds.Inset(MobileLook.EditableTextBlock.ContentHorizontalInset, MobileLook.EditableTextBlock.ContentVerticalInset);

            public override CGRect PlaceholderRect (CGRect forBounds) => forBounds.Inset(MobileLook.EditableTextBlock.ContentHorizontalInset, MobileLook.EditableTextBlock.ContentVerticalInset);

            // A hardware keyboard's Escape cancels the edit. A press handled here is kept from the field for its whole life,
            // its end and its cancel too, since UIKit asks a responder that takes a press's start to take the rest of it.
            public override void PressesBegan (NSSet<UIPress> presses, UIPressesEvent evt)
            {
                if (IsEscape(presses))
                {
                    if (this.TryGetOwner(out EditableTextBlock owner))
                    {
                        owner.OnFieldEscape();
                    }

                    return;
                }

                base.PressesBegan(presses, evt);
            }

            public override void PressesEnded (NSSet<UIPress> presses, UIPressesEvent evt)
            {
                if (!IsEscape(presses))
                {
                    base.PressesEnded(presses, evt);
                }
            }

            public override void PressesCancelled (NSSet<UIPress> presses, UIPressesEvent evt)
            {
                if (!IsEscape(presses))
                {
                    base.PressesCancelled(presses, evt);
                }
            }

            private static bool IsEscape (NSSet<UIPress> presses)
            {
                foreach (UIPress press in presses)
                {
                    if (press.Key?.KeyCode == UIKeyboardHidUsage.KeyboardEscape)
                    {
                        return true;
                    }
                }

                return false;
            }

            private void OnSelfEditingChanged (object sender, EventArgs e)
            {
                if (this.TryGetOwner(out EditableTextBlock owner))
                {
                    owner.OnFieldEdited();
                }
            }

            private void OnSelfEnded (object sender, EventArgs e)
            {
                if (this.TryGetOwner(out EditableTextBlock owner))
                {
                    owner.OnFieldEnded();
                }
            }
        }
    }
}
