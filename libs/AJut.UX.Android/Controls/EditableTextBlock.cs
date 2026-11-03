namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics;
    using Android.Graphics.Drawables;
    using Android.Runtime;
    using Android.Text;
    using Android.Text.Style;
    using Android.Util;
    using Android.Views;
    using Android.Views.InputMethods;
    using Android.Widget;

    /// <summary>
    /// Text that reads as a label until it is tapped, then turns into a text field to edit it, the phone version of WPF's
    /// EditableTextBlock: tap to rename. <see cref="EditTextInstigatorAction"/> picks the gesture (a tap by default, or a
    /// double tap, a long press, or only code through <see cref="IsEditing"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The edit rules are AJut.UX's <see cref="EditableTextState"/>, which UIKit's EditableTextBlock shares, and they are
    /// WPF's: <see cref="Text"/> follows every keystroke, Done keeps it, and Cancel puts back what was there before. A
    /// canceled edit is kept, and the next edit picks it up. An edit that ends any other way (the keyboard dismissed with
    /// back, focus moving elsewhere, the control leaving its window or being disabled) is kept or canceled by
    /// <see cref="LostFocusEditClose"/>. A hardware keyboard's Escape cancels.
    /// </para>
    /// <para>
    /// A phone has no Escape key, so the only cancel on a touch screen is a <see cref="LostFocusEditClose"/> of Cancel.
    /// A read only EditableTextBlock never starts editing. With no text, the label shows <see cref="NoTextSetMessage"/>,
    /// faded and italic; an edit that starts empty shows it as the field's hint. In a layout: android:text,
    /// android:textSize, android:enabled, ajutIsReadOnly, ajutNoTextSetMessage, ajutEditTextInstigatorAction,
    /// ajutEditTextInitializationBehavior and ajutLostFocusEditClose.
    /// </para>
    /// </remarks>
    [Register("ajut.ux.controls.EditableTextBlock")]
    public class EditableTextBlock : FrameLayout
    {
        private const string kDefaultNoTextSetMessage = "No text set";

        // The look is MobileLook.EditableTextBlock's, which UIKit's EditableTextBlock draws too. The literals are the
        // unthemed look: plain text colors, and a light gray or dark gray well while editing.
        private static readonly ThemedColor kLabelText = ThemedColor.FromArgb(MobileLook.EditableTextBlock.LabelText, 0xDE000000, 0xFFFFFFFF);
        private static readonly ThemedColor kLabelTextDisabled = ThemedColor.FromArgb(MobileLook.EditableTextBlock.LabelTextDisabled, 0x61000000, 0x80FFFFFF);
        private static readonly ThemedColor kFieldBackground = ThemedColor.FromArgb(MobileLook.EditableTextBlock.FieldBackground, 0xFFECECEC, 0xFF3A3A3A);
        private static readonly ThemedColor kFieldText = ThemedColor.FromArgb(MobileLook.EditableTextBlock.FieldText, 0xFF000000, 0xFFFFFFFF);
        private static readonly ThemedColor kFieldSelection = ThemedColor.FromArgb(MobileLook.EditableTextBlock.FieldSelection, 0xFF0078D4, 0xFF4CA3E8);

        private readonly EditableTextState m_state = new EditableTextState();
        private TextView m_label = null!;
        private EditField m_field = null!;
        private GestureDetector m_doubleTapDetector = null!;
        private Java.Lang.Object? m_keyboardWatcher;
        private bool m_hasSeenKeyboard;
        private float m_density = 1f;
        private bool m_isReadOnly;
        private bool m_isPushingText;
        private bool m_isClosing;
        private bool m_isTextWrapping;
        private string m_noTextSetMessage = kDefaultNoTextSetMessage;
        private eEditTextTouchInstigator m_editTextInstigatorAction = eEditTextTouchInstigator.Tap;
        private eEditTextInitializationBehavior m_editTextInitializationBehavior = eEditTextInitializationBehavior.SelectAll;
        private eEditTextCloseAction m_lostFocusEditClose = eEditTextCloseAction.Apply;

        // ===========[ Setup/Construction/Teardown ]===================================
        public EditableTextBlock (Context context) : base(context)
        {
            this.Initialize(context, null);
        }

        public EditableTextBlock (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            this.Initialize(context, attrs);
        }

        public EditableTextBlock (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.Initialize(context, attrs);
        }

        protected EditableTextBlock (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override void OnAttachedToWindow ()
        {
            base.OnAttachedToWindow();
            m_label.Click -= this.OnLabelClick;
            m_label.Click += this.OnLabelClick;
            m_label.LongClick -= this.OnLabelLongClick;
            m_label.LongClick += this.OnLabelLongClick;
            m_label.Touch -= this.OnLabelTouch;
            m_label.Touch += this.OnLabelTouch;
            m_field.TextChanged -= this.OnFieldTextChanged;
            m_field.TextChanged += this.OnFieldTextChanged;
            m_field.EditorAction -= this.OnFieldEditorAction;
            m_field.EditorAction += this.OnFieldEditorAction;
            m_field.FocusChange -= this.OnFieldFocusChange;
            m_field.FocusChange += this.OnFieldFocusChange;
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                m_keyboardWatcher ??= new KeyboardWatcher(this);
                m_field.SetWindowInsetsAnimationCallback((WindowInsetsAnimation.Callback)m_keyboardWatcher);
            }

            // Hooking a click or long click makes a view clickable, which would take a row's taps from under a label that
            // starts no edit, so the label's touch settings go back to what the control says
            this.RefreshLabel();

            // An edit started before the control was in a window takes focus, and the keyboard, now that it is
            if (m_state.IsEditing && !m_field.HasFocus)
            {
                this.FocusField();
            }
        }

        // Leaving the window ends an edit the way losing focus does
        protected override void OnDetachedFromWindow ()
        {
            this.CloseEdit(m_lostFocusEditClose);
            m_label.Click -= this.OnLabelClick;
            m_label.LongClick -= this.OnLabelLongClick;
            m_label.Touch -= this.OnLabelTouch;
            m_field.TextChanged -= this.OnFieldTextChanged;
            m_field.EditorAction -= this.OnFieldEditorAction;
            m_field.FocusChange -= this.OnFieldFocusChange;
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                m_field.SetWindowInsetsAnimationCallback(null);
            }

            base.OnDetachedFromWindow();
        }

        // ===========[ Events ]===================================
        /// <summary>Raised whenever <see cref="Text"/> changes: on every keystroke while editing, on a cancel, and from code</summary>
        public event EventHandler<EventArgs<string?>>? TextChanged;

        /// <summary>Raised when an edit starts or ends</summary>
        public event EventHandler? IsEditingChanged;

        // A screen reader's double tap arrives as a click whichever gesture is set, and is the only way it has to start an
        // edit set to a double tap, so with one running a click starts any edit the label takes touches for, as VoiceOver's
        // activate does on iOS
        private void OnLabelClick (object? sender, EventArgs e)
        {
            if ((m_editTextInstigatorAction == eEditTextTouchInstigator.Tap)
                || ((m_editTextInstigatorAction != eEditTextTouchInstigator.Manually) && this.IsScreenReaderRunning()))
            {
                this.BeginEdit();
            }
        }

        private void OnLabelLongClick (object? sender, View.LongClickEventArgs e)
        {
            e.Handled = m_editTextInstigatorAction == eEditTextTouchInstigator.LongPress;
            if (e.Handled)
            {
                this.BeginEdit();
            }
        }

        // A double tap has no listener of its own on a view, so the label's touches go through a gesture detector for it;
        // they are left unhandled, so the label's click and long click still see them
        private void OnLabelTouch (object? sender, View.TouchEventArgs e)
        {
            e.Handled = false;
            if ((m_editTextInstigatorAction == eEditTextTouchInstigator.DoubleTap) && (e.Event != null))
            {
                m_doubleTapDetector.OnTouchEvent(e.Event);
            }
        }

        private void OnFieldTextChanged (object? sender, TextChangedEventArgs e)
        {
            if (m_isPushingText || !m_state.IsEditing)
            {
                return;
            }

            m_state.Text = m_field.Text;
            this.RaiseTextChanged();
        }

        private void OnFieldEditorAction (object? sender, TextView.EditorActionEventArgs e)
        {
            bool isEnter = (e.Event != null) && (e.Event.KeyCode == Keycode.Enter);
            e.Handled = (e.ActionId == ImeAction.Done) || isEnter;
            if (e.Handled && ((e.Event == null) || (e.Event.Action == KeyEventActions.Down)))
            {
                this.CloseEdit(eEditTextCloseAction.Apply);
            }
        }

        private void OnFieldFocusChange (object? sender, View.FocusChangeEventArgs e)
        {
            if (!e.HasFocus)
            {
                this.CloseEdit(m_lostFocusEditClose);
            }
        }

        // ===========[ Properties ]===================================
        /// <summary>The text. While editing it follows every keystroke, as WPF's does.</summary>
        public string? Text
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
                    this.PushTextToField();
                }

                this.RefreshLabel();
                this.RaiseTextChanged();
            }
        }

        /// <summary>
        /// Whether the text is being edited. Setting it starts an edit (shown with the keyboard up) or keeps the one going;
        /// clearing it keeps the edit. Starting is refused while <see cref="IsReadOnly"/> or disabled.
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
        public string? PreviousText => m_state.PreviousText;

        /// <summary>The text of the last canceled edit, which the next edit starts from; null once an edit is kept</summary>
        public string? EditText => m_state.EditText;

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
        /// Whether an edit that ends any way but Done or Escape is kept or canceled: the keyboard dismissed with back, focus
        /// moving elsewhere, the control leaving its window, or it being disabled or made read only. Kept by default.
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
                this.RefreshLabel();
            }
        }

        /// <summary>The text's size in sp, for the label and the field alike. 16 by default, Android's body text size.</summary>
        public float TextSize
        {
            get => m_label.TextSize / TypedValue.ApplyDimension(ComplexUnitType.Sp, 1f, this.Resources!.DisplayMetrics);
            set
            {
                m_label.SetTextSize(ComplexUnitType.Sp, value);
                m_field.SetTextSize(ComplexUnitType.Sp, value);
            }
        }

        /// <summary>
        /// Whether the control takes touches and edits. A ViewGroup's Enabled never reaches its children, so the label and
        /// field are set here. Disabling it during an edit ends the edit as losing focus would.
        /// </summary>
        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;

                // A peer the runtime made for an existing Java view (the IntPtr constructor) has no label or field
                if (m_label == null)
                {
                    return;
                }

                if (!value)
                {
                    this.CloseEdit(m_lostFocusEditClose);
                }

                m_label.Enabled = value;
                m_field.Enabled = value;
                this.RefreshLabel();
            }
        }

        // ===========[ Helper Methods ]===================================
        private void Initialize (Context context, IAttributeSet? attrs)
        {
            m_density = context.Resources?.DisplayMetrics?.Density ?? 1f;
            int horizontalInset = (int)(MobileLook.EditableTextBlock.ContentHorizontalInset * m_density);
            int verticalInset = (int)(MobileLook.EditableTextBlock.ContentVerticalInset * m_density);
            this.SetMinimumHeight((int)(MobileLook.EditableTextBlock.MinTouchHeight * m_density));

            // 1. The label, and the field laid over it in the same place, with the same insets, so the text stays put
            m_label = new TextView(context) { Ellipsize = TextUtils.TruncateAt.End };
            m_label.SetPadding(horizontalInset, verticalInset, horizontalInset, verticalInset);
            m_field = new EditField(context, this)
            {
                ImeOptions = ImeAction.Done,
                InputType = InputTypes.ClassText,
                Visibility = ViewStates.Gone,
            };
            m_field.SetSingleLine(true);
            m_field.SetPadding(horizontalInset, verticalInset, horizontalInset, verticalInset);

            // An EditText's own text is bigger than a TextView's, so both are given one size, or the text would jump as an
            // edit starts
            const float kDefaultTextSize = 16f;
            m_label.SetTextSize(ComplexUnitType.Sp, kDefaultTextSize);
            m_field.SetTextSize(ComplexUnitType.Sp, kDefaultTextSize);
            this.AddView(m_label, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent, GravityFlags.CenterVertical));
            this.AddView(m_field, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent, GravityFlags.CenterVertical));
            m_doubleTapDetector = new GestureDetector(context, new DoubleTapListener(this));

            // 2. The field's look while editing: a well, the edit text color, a see through selection, and on Android 10
            // and later (where a caret can be set) a caret in the text color
            var well = new GradientDrawable();
            well.SetColor(ThemeColors.Resolve(context, kFieldBackground).ToAndroidColor());
            well.SetCornerRadius(MobileLook.EditableTextBlock.FieldCornerRadius * m_density);
            m_field.Background = well;
            ColorArgb fieldText = ThemeColors.Resolve(context, kFieldText);
            m_field.SetTextColor(fieldText.ToAndroidColor());
            m_field.SetHighlightColor(WithOpacity(ThemeColors.Resolve(context, kFieldSelection), MobileLook.EditableTextBlock.SelectionOpacity).ToAndroidColor());
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                const float kCaretWidth = 2f;
                var caret = new GradientDrawable();
                caret.SetColor(fieldText.ToAndroidColor());
                caret.SetSize((int)Math.Max(1f, kCaretWidth * m_density), 0);
                m_field.TextCursorDrawable = caret;
            }

            // 3. What the layout set
            bool isEnabled = true;
            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutEditableTextBlock);
                m_state.Text = values.GetString(Resource.Styleable.AJutEditableTextBlock_android_text);
                if (values.HasValue(Resource.Styleable.AJutEditableTextBlock_android_textSize))
                {
                    float size = values.GetDimension(Resource.Styleable.AJutEditableTextBlock_android_textSize, m_label.TextSize);
                    m_label.SetTextSize(ComplexUnitType.Px, size);
                    m_field.SetTextSize(ComplexUnitType.Px, size);
                }

                isEnabled = values.GetBoolean(Resource.Styleable.AJutEditableTextBlock_android_enabled, true);
                m_isReadOnly = values.GetBoolean(Resource.Styleable.AJutEditableTextBlock_ajutIsReadOnly, false);
                m_noTextSetMessage = values.GetString(Resource.Styleable.AJutEditableTextBlock_ajutNoTextSetMessage) ?? kDefaultNoTextSetMessage;
                m_editTextInstigatorAction = (eEditTextTouchInstigator)values.GetInt(Resource.Styleable.AJutEditableTextBlock_ajutEditTextInstigatorAction, (int)eEditTextTouchInstigator.Tap);
                m_editTextInitializationBehavior = (eEditTextInitializationBehavior)values.GetInt(Resource.Styleable.AJutEditableTextBlock_ajutEditTextInitializationBehavior, (int)eEditTextInitializationBehavior.SelectAll);
                m_lostFocusEditClose = (eEditTextCloseAction)values.GetInt(Resource.Styleable.AJutEditableTextBlock_ajutLostFocusEditClose, (int)eEditTextCloseAction.Apply);
                values.Recycle();
            }

            this.Enabled = isEnabled;
            this.RefreshLabel();
        }

        private static ColorArgb WithOpacity (ColorArgb color, float opacity) => new ColorArgb((byte)(color.A * opacity), color.R, color.G, color.B);

        private void BeginEdit ()
        {
            if (!this.Enabled || !m_state.TryBeginEdit(m_isReadOnly))
            {
                return;
            }

            m_hasSeenKeyboard = false;

            // The field shows the edit's text (a canceled edit's, picked up), with the empty message as its hint when the
            // edit starts empty, as WPF shows it behind an edit that starts empty
            string? textBefore = m_state.PreviousText;
            this.PushTextToField();
            m_field.HintFormatted = m_state.StartedEditingEmpty ? this.MakeEmptyMessage(MobileLook.EditableTextBlock.EditingEmptyMessageOpacity) : null;
            m_label.Visibility = ViewStates.Invisible;
            m_field.Visibility = ViewStates.Visible;

            EditableTextState.Selection selection = m_state.GetStartingSelection(m_editTextInitializationBehavior);
            m_field.SetSelection(selection.Start, selection.End);
            this.FocusField();

            if (m_state.Text != textBefore)
            {
                this.RaiseTextChanged();
            }

            this.IsEditingChanged?.Invoke(this, EventArgs.Empty);
        }

        // Ends an edit, if one is going on. Hiding the field takes its focus away, which comes back here as a focus change,
        // so the end is guarded against running twice.
        private void CloseEdit (eEditTextCloseAction action)
        {
            if (m_isClosing || !m_state.IsEditing)
            {
                return;
            }

            m_isClosing = true;
            try
            {
                string? textBefore = m_state.Text;
                m_state.Close(action);

                var keyboard = (InputMethodManager?)this.Context?.GetSystemService(Context.InputMethodService);
                // The field's own token is gone once it has left its window, which a group's children do before the group
                // itself, so as this leaves its window only its own token still says which window the keyboard is for
                keyboard?.HideSoftInputFromWindow(this.WindowToken ?? m_field.WindowToken, HideSoftInputFlags.None);
                m_field.Visibility = ViewStates.Gone;
                m_label.Visibility = ViewStates.Visible;
                this.RefreshLabel();

                if (m_state.Text != textBefore)
                {
                    this.RaiseTextChanged();
                }

                this.IsEditingChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                m_isClosing = false;
            }
        }

        // The keyboard comes up as the field takes focus, which needs a window. It connects to a field a frame after the
        // field takes focus, and asking for it before then does nothing, so the ask is posted.
        private void FocusField ()
        {
            if (this.IsAttachedToWindow && m_field.RequestFocus())
            {
                m_field.Post(this.ShowKeyboardForEdit);
            }
        }

        // Posted, so the edit may be over by the time it runs; then there is nothing to show the keyboard for
        private void ShowKeyboardForEdit ()
        {
            if (m_state.IsEditing && m_field.HasFocus)
            {
                var keyboard = (InputMethodManager?)this.Context?.GetSystemService(Context.InputMethodService);
                // An explicit ask (no flags): the user asked for the edit, and an implicit one is the keyboard's to refuse
                keyboard?.ShowSoftInput(m_field, 0);
            }
        }

        // The edit's text into the field, with the caret at the end of it, without it coming back as a keystroke
        private void PushTextToField ()
        {
            m_isPushingText = true;
            try
            {
                m_field.Text = m_state.Text ?? string.Empty;
                m_field.SetSelection(m_field.Text?.Length ?? 0);
            }
            finally
            {
                m_isPushingText = false;
            }
        }

        private void RefreshLabel ()
        {
            Context? context = this.Context;
            if ((context == null) || (m_label == null))
            {
                return;
            }

            m_label.SetSingleLine(!m_isTextWrapping);
            m_label.Ellipsize = m_isTextWrapping ? null : TextUtils.TruncateAt.End;
            if (m_state.IsEmpty)
            {
                m_label.TextFormatted = this.MakeEmptyMessage(MobileLook.EditableTextBlock.EmptyMessageOpacity);
            }
            else
            {
                m_label.Text = m_state.Text;
                m_label.SetTextColor(ThemeColors.Resolve(context, this.Enabled ? kLabelText : kLabelTextDisabled).ToAndroidColor());
            }

            // The label only takes touches when one of them starts an edit, so a row it sits in still takes the rest
            bool canEdit = this.Enabled && !m_isReadOnly && (m_editTextInstigatorAction != eEditTextTouchInstigator.Manually);
            m_label.Clickable = canEdit;
            m_label.LongClickable = canEdit && (m_editTextInstigatorAction == eEditTextTouchInstigator.LongPress);
        }

        // The empty message: the label's color faded, and italic, as on WPF
        private SpannableString MakeEmptyMessage (float opacity)
        {
            ColorArgb color = ThemeColors.Resolve(this.Context!, this.Enabled ? kLabelText : kLabelTextDisabled);
            var message = new SpannableString(m_noTextSetMessage);
            message.SetSpan(new ForegroundColorSpan(WithOpacity(color, opacity).ToAndroidColor()), 0, m_noTextSetMessage.Length, SpanTypes.ExclusiveExclusive);
            message.SetSpan(new StyleSpan(TypefaceStyle.Italic), 0, m_noTextSetMessage.Length, SpanTypes.ExclusiveExclusive);
            return message;
        }

        private void RaiseTextChanged () => this.TextChanged?.Invoke(this, new EventArgs<string?>(m_state.Text));

        // The field tells its owner when the keyboard is dismissed with back (the field keeps focus then, so no focus
        // change says the edit is over) and when a hardware keyboard's Escape is pressed
        private void OnFieldKeyboardDismissed () => this.CloseEdit(m_lostFocusEditClose);

        private void OnFieldEscape () => this.CloseEdit(eEditTextCloseAction.Cancel);

        // From Android 11 the keyboard's own show and hide is followed through the field's window insets. An app that
        // targets Android 16 runs with predictive back, and then back with the keyboard up goes to the keyboard alone and
        // never reaches the field as a key, so the keyboard going away is the only sign the edit was dismissed. It only
        // counts once the keyboard has shown for this edit, so the hide before it comes up says nothing.
        private void OnKeyboardAnimationEnded ()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(30) || !m_state.IsEditing)
            {
                return;
            }

            bool isKeyboardShown = m_field.RootWindowInsets?.IsVisible(WindowInsets.Type.Ime()) ?? false;
            if (isKeyboardShown)
            {
                m_hasSeenKeyboard = true;
            }
            else if (m_hasSeenKeyboard)
            {
                this.CloseEdit(m_lostFocusEditClose);
            }
        }

        private bool IsScreenReaderRunning ()
        {
            var accessibility = (Android.Views.Accessibility.AccessibilityManager?)this.Context?.GetSystemService(Context.AccessibilityService);
            return (accessibility != null) && accessibility.IsTouchExplorationEnabled;
        }

        private void OnLabelDoubleTap ()
        {
            if (m_label.Clickable)
            {
                this.BeginEdit();
            }
        }

        // ===========[ Subclasses/structs ]===================================
        // Hears the end of each keyboard animation on the field; ContinueOnSubtree, so it takes nothing from views below
        [System.Runtime.Versioning.SupportedOSPlatform("android30.0")]
        private sealed class KeyboardWatcher : WindowInsetsAnimation.Callback
        {
            private readonly EditableTextBlock m_owner;

            public KeyboardWatcher (EditableTextBlock owner) : base((int)WindowInsetsAnimationDispatchMode.ContinueOnSubtree)
            {
                m_owner = owner;
            }

            public override WindowInsets OnProgress (WindowInsets insets, System.Collections.Generic.IList<WindowInsetsAnimation> runningAnimations) => insets;

            public override void OnEnd (WindowInsetsAnimation animation) => m_owner.OnKeyboardAnimationEnded();
        }

        private sealed class EditField : EditText
        {
            private readonly EditableTextBlock m_owner;

            public EditField (Context context, EditableTextBlock owner) : base(context)
            {
                m_owner = owner;
            }

            // Back with the keyboard up goes to the keyboard first, and only hides it; this hears it on its way there
            public override bool OnKeyPreIme (Keycode keyCode, KeyEvent? e)
            {
                if ((keyCode == Keycode.Back) && (e?.Action == KeyEventActions.Up))
                {
                    m_owner.OnFieldKeyboardDismissed();
                }

                return base.OnKeyPreIme(keyCode, e);
            }

            public override bool OnKeyDown (Keycode keyCode, KeyEvent? e)
            {
                if (keyCode == Keycode.Escape)
                {
                    m_owner.OnFieldEscape();
                    return true;
                }

                return base.OnKeyDown(keyCode, e);
            }
        }

        private sealed class DoubleTapListener : GestureDetector.SimpleOnGestureListener
        {
            private readonly EditableTextBlock m_owner;

            public DoubleTapListener (EditableTextBlock owner)
            {
                m_owner = owner;
            }

            public override bool OnDoubleTap (MotionEvent e)
            {
                m_owner.OnLabelDoubleTap();
                return true;
            }
        }
    }
}
