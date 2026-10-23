namespace AJut.UX.Controls
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using CoreGraphics;
    using AJut.UX.Theming;
    using Foundation;
    using ObjCRuntime;
    using UIKit;

    // ===========[ NumericEditor ]==============================================
    // UIKit numeric editor: a text field with nudge buttons, over NumericEditorController, so it keeps the source
    // type (float stays float, int stays int) or the one EnforceNumericType pins, caps to Minimum / Maximum, and flags
    // out of bounds text exactly the way the WinUI3 and WPF NumericEditors do. This control only lays out the views
    // and routes their events in.
    //
    // Layout, leading to trailing: the label ("#" unless LabelText or LabelView says otherwise), the text field, the
    // warning glyph (shown while the out of bounds error is), and the decrease / increase buttons, which repeat
    // while held.
    //
    // Nudge tiers: a tap steps by Nudge. With a hardware keyboard (iPad), holding Shift steps by SmallNudge and
    // holding Command or Control steps by BigNudge, on the buttons and on the Up / Down arrow keys alike.
    //
    // ValueChanged is sent for a user edit only (typing, a nudge, a commit that changes the value), never for a
    // Value set from code, the same as every UIKit control; that is what the BindingSet adapters listen to.

    public class NumericEditor : UIControl, INumericEditorHost
    {
        // ===========[ Instance Fields ]===================================
        private readonly NumericEditorController m_controller;
        private readonly UIStackView m_row = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Alignment = UIStackViewAlignment.Fill,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };
        private readonly UILabel m_defaultLabel = new UILabel { Text = "#", TextColor = UIColor.SecondaryLabel };
        private readonly UITextField m_textField = new UITextField
        {
            KeyboardType = UIKeyboardType.NumbersAndPunctuation,
            AutocorrectionType = UITextAutocorrectionType.No,
            SpellCheckingType = UITextSpellCheckingType.No,
        };
        private readonly UIImageView m_warningGlyph = new UIImageView { Hidden = true, ContentMode = UIViewContentMode.Center };
        private readonly UIToolTipInteraction m_warningToolTip = new UIToolTipInteraction();
        private readonly NudgeButton m_decreaseButton;
        private readonly NudgeButton m_increaseButton;
        private UIView m_labelView;
        private object m_value;

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        private Type m_enforceNumericType;

        private bool m_isSettingFromCode;
        private double m_minimum = double.NegativeInfinity;
        private double m_maximum = double.PositiveInfinity;
        private eOutOfBoundsResponse m_outOfBoundsResponse = eOutOfBoundsResponse.ErrorAndToolTip;
        private bool m_isFieldHooked;

        // ===========[ Setup/Construction/Teardown ]===================================
        public NumericEditor () : base(CGRect.Empty)
        {
            m_controller = new NumericEditorController(this);
            m_decreaseButton = new NudgeButton(this, positive: false, "minus");
            m_increaseButton = new NudgeButton(this, positive: true, "plus");
            this.Setup();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native control.</summary>
        protected NumericEditor (NativeHandle handle) : base(handle)
        {
            m_controller = new NumericEditorController(this);
            m_decreaseButton = new NudgeButton(this, positive: false, "minus");
            m_increaseButton = new NudgeButton(this, positive: true, "plus");
            this.Setup();
        }

        private void Setup ()
        {
            const float kCornerRadius = 6f;
            const float kBorderWidth = 1f;
            const float kLeadingPadding = 8f;
            const float kSpacing = 4f;

            this.Layer.CornerRadius = kCornerRadius;
            this.Layer.BorderWidth = kBorderWidth;
            this.ClipsToBounds = true;

            m_row.Spacing = kSpacing;
            m_row.LayoutMargins = new UIEdgeInsets(0, kLeadingPadding, 0, 0);
            m_row.LayoutMarginsRelativeArrangement = true;
            m_defaultLabel.SetContentHuggingPriority((float)UILayoutPriority.Required, UILayoutConstraintAxis.Horizontal);
            m_textField.SetContentHuggingPriority((float)UILayoutPriority.DefaultLow, UILayoutConstraintAxis.Horizontal);

            m_warningGlyph.Image = UIImage.GetSystemImage("exclamationmark.triangle.fill");
            m_warningGlyph.TintColor = ThemedColors.Get(ThemedColorKeys.NumericEditor_ErrorBrush);
            m_textField.TextColor = ThemedColors.Get(ThemedColorKeys.TextControlForeground);
            m_warningGlyph.SetContentHuggingPriority((float)UILayoutPriority.Required, UILayoutConstraintAxis.Horizontal);
            m_warningGlyph.AddInteraction(m_warningToolTip);

            m_row.AddArrangedSubview(m_defaultLabel);
            m_row.AddArrangedSubview(m_textField);
            m_row.AddArrangedSubview(m_warningGlyph);
            m_row.AddArrangedSubview(m_decreaseButton);
            m_row.AddArrangedSubview(m_increaseButton);
            this.AddSubview(m_row);

            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_row.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_row.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                m_row.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_row.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
            });
        }

        public override void MovedToWindow ()
        {
            base.MovedToWindow();

            // The text field is a subview, so this control keeps it alive natively, and a handler of this control
            // held by the field would keep this control alive from the managed side: a cycle across native objects
            // the GC cannot see. So the field's events are hooked only while the editor is on screen, and unhooked
            // when it leaves its window, which is also when an edit in progress has ended.
            if (this.Window != null)
            {
                this.HookTextField();
            }
            else
            {
                this.UnhookTextField();
                m_decreaseButton.StopRepeating();
                m_increaseButton.StopRepeating();
            }
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                this.UnhookTextField();
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]===================================
        private void TextField_OnEditingChanged (object sender, EventArgs e)
        {
            m_controller.OnTextEdited(m_textField.Text);
        }

        private void TextField_OnEditingDidBegin (object sender, EventArgs e)
        {
            this.SetNeedsLayout();
        }

        private void TextField_OnEditingDidEnd (object sender, EventArgs e)
        {
            m_controller.Commit();
            this.SetNeedsLayout();
        }

        private bool TextField_OnShouldReturn (UITextField field)
        {
            // Return ends the edit, and the EditingDidEnd that follows commits it
            field.ResignFirstResponder();
            return false;
        }

        // ===========[ Properties ]===================================
        /// <summary>
        /// The value being edited, kept in its own numeric type, or in <see cref="EnforceNumericType"/> when that is set.
        /// Text given here (such as a value read from a file) comes back as the number it parsed to. Setting it from
        /// code does not send ValueChanged.
        /// </summary>
        public object Value
        {
            get => m_value;
            set
            {
                m_isSettingFromCode = true;
                try
                {
                    m_value = value;
                    m_controller.OnValueChanged(value);
                }
                finally
                {
                    m_isSettingFromCode = false;
                }
            }
        }

        /// <summary>
        /// The numeric type <see cref="Value"/> is kept in however it arrives (text, or a number of another type); null,
        /// the default, keeps whatever type each value has. A Value already set is converted to it, so the two can be
        /// set in either order. Like a Value set from code, that conversion does not send ValueChanged.
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        public Type EnforceNumericType
        {
            get => m_enforceNumericType;
            set
            {
                if (m_enforceNumericType == value)
                {
                    return;
                }

                m_enforceNumericType = value;
                m_isSettingFromCode = true;
                try
                {
                    m_controller.OnEnforcedTypeChanged(value);
                }
                finally
                {
                    m_isSettingFromCode = false;
                }
            }
        }

        /// <summary>The lowest allowed value; negative infinity (the default) is unconstrained. Like WinUI3's, it caps from the next edit on.</summary>
        public double Minimum
        {
            get => m_minimum;
            set => m_minimum = value;
        }

        /// <summary>The highest allowed value; positive infinity (the default) is unconstrained. Like WinUI3's, it caps from the next edit on.</summary>
        public double Maximum
        {
            get => m_maximum;
            set => m_maximum = value;
        }

        /// <summary>The step for a tap on a nudge button, or an arrow key with no modifier.</summary>
        public double Nudge { get; set; } = 1.0;

        /// <summary>The step while Command or Control is held on a hardware keyboard.</summary>
        public double BigNudge { get; set; } = 5.0;

        /// <summary>The step while Shift is held on a hardware keyboard.</summary>
        public double SmallNudge { get; set; } = 0.5;

        /// <summary>How the editor responds to text outside the bounds (default: ErrorAndToolTip).</summary>
        public eOutOfBoundsResponse OutOfBoundsResponse
        {
            get => m_outOfBoundsResponse;
            set
            {
                m_outOfBoundsResponse = value;
                m_controller.RefreshErrorState();
            }
        }

        /// <summary>The out of bounds message while the error is shown, otherwise null.</summary>
        public string ErrorMessage { get; private set; }

        /// <summary>The text of the default label, "#" unless set. Ignored while <see cref="LabelView"/> is set.</summary>
        public string LabelText
        {
            get => m_defaultLabel.Text;
            set => m_defaultLabel.Text = value;
        }

        /// <summary>A view shown in place of the default label, the way WinUI3's LabelContent replaces its "#" glyph. Null brings the default back.</summary>
        public UIView LabelView
        {
            get => m_labelView;
            set
            {
                m_labelView?.RemoveFromSuperview();
                m_labelView = value;
                m_defaultLabel.Hidden = value != null;
                if (value != null)
                {
                    m_row.InsertArrangedSubview(value, 0);
                }
            }
        }

        public UITextAlignment TextAlignment
        {
            get => m_textField.TextAlignment;
            set => m_textField.TextAlignment = value;
        }

        /// <summary>The text field, for anything this control does not surface (font, placeholder, input accessory).</summary>
        public UITextField TextField => m_textField;

        public override UIKeyCommand[] KeyCommands
        {
            get
            {
                // Up / Down nudge from a hardware keyboard, with Shift for the small step and Command or Control for
                // the big one. A single line text field otherwise takes the arrows to move its caret, so these ask
                // for priority over that system behavior (iOS 15).
                var selectorUp = new Selector("ajutNudgeUp:");
                var selectorDown = new Selector("ajutNudgeDown:");
                UIKeyModifierFlags[] modifiers = { 0, UIKeyModifierFlags.Shift, UIKeyModifierFlags.Command, UIKeyModifierFlags.Control };
                var commands = new UIKeyCommand[modifiers.Length * 2];
                for (int index = 0; index < modifiers.Length; ++index)
                {
                    commands[index * 2] = UIKeyCommand.Create(UIKeyCommand.UpArrow, modifiers[index], selectorUp);
                    commands[(index * 2) + 1] = UIKeyCommand.Create(UIKeyCommand.DownArrow, modifiers[index], selectorDown);
                }

                foreach (UIKeyCommand command in commands)
                {
                    command.WantsPriorityOverSystemBehavior = true;
                }

                return commands;
            }
        }

        // ===========[ Public Interface Methods ]===================================
        public override void LayoutSubviews ()
        {
            base.LayoutSubviews();

            // A layer border is a CGColor, which freezes a dynamic color, so it is resolved here: UIKit calls
            // LayoutSubviews again whenever the appearance (light/dark) changes.
            UIColor border;
            if (this.ErrorMessage != null)
            {
                border = ThemedColors.Get(ThemedColorKeys.NumericEditor_ErrorBrush);
            }
            else if (m_textField.IsEditing)
            {
                border = ThemedColors.Get(ThemedColorKeys.NumericEditor_FocusBorderBrush) ?? this.TintColor;
            }
            else
            {
                border = ThemedColors.Get(ThemedColorKeys.NumericEditor_NormalBorderBrush);
            }
            this.Layer.BorderColor = border.GetResolvedColor(this.TraitCollection).CGColor;
        }

        public override void TintColorDidChange ()
        {
            base.TintColorDidChange();
            this.SetNeedsLayout();
        }

        [Export("ajutNudgeUp:")]
        private void NudgeUpCommand (UIKeyCommand command) => this.DoNudge(positive: true, command.ModifierFlags);

        [Export("ajutNudgeDown:")]
        private void NudgeDownCommand (UIKeyCommand command) => this.DoNudge(positive: false, command.ModifierFlags);

        // ===========[ INumericEditorSettings ]===================================
        int INumericEditorSettings.DecimalPlacesAllowed => -1;
        object INumericEditorSettings.Minimum => NumericEditorController.ToMinimumBound(m_minimum);
        object INumericEditorSettings.Maximum => NumericEditorController.ToMaximumBound(m_maximum);
        eOutOfBoundsResponse INumericEditorSettings.OutOfBoundsResponse => m_outOfBoundsResponse;

        // ===========[ INumericEditorHost ]=======================================
        void INumericEditorHost.ShowText (string text)
        {
            text ??= string.Empty;
            if (m_textField.Text == text)
            {
                return;
            }

            // Setting Text puts the caret at the end, so while the user is typing, put it back where it was, at the
            // same offset from the start, clamped to the new length.
            UITextRange selection = m_textField.IsFirstResponder ? m_textField.SelectedTextRange : null;
            nint caret = selection == null ? 0 : m_textField.GetOffsetFromPosition(m_textField.BeginningOfDocument, selection.Start);
            m_textField.Text = text;
            if (selection != null)
            {
                UITextPosition position = m_textField.GetPosition(m_textField.BeginningOfDocument, Math.Min(caret, text.Length));
                if (position != null)
                {
                    m_textField.SelectedTextRange = m_textField.GetTextRange(position, position);
                }
            }
        }

        void INumericEditorHost.ShowValue (object value)
        {
            if (Equals(m_value, value))
            {
                return;
            }

            m_value = value;

            // The controller also writes the Value back when one set from code changed type on the way in (text
            // parsed to its number, or a number taken into the enforced type). That is still a code set, so like
            // every UIKit control it sends nothing: ValueChanged is for a user edit only.
            if (!m_isSettingFromCode)
            {
                this.SendActionForControlEvents(UIControlEvent.ValueChanged);
            }
        }

        void INumericEditorHost.ShowError (string errorMessage)
        {
            this.ErrorMessage = errorMessage;
            m_warningGlyph.Hidden = errorMessage == null;
            m_warningToolTip.DefaultToolTip = errorMessage;
            m_warningGlyph.AccessibilityLabel = errorMessage;
            this.SetNeedsLayout();
        }

        // ===========[ Helper Methods ]===================================
        private void HookTextField ()
        {
            if (m_isFieldHooked)
            {
                return;
            }

            m_isFieldHooked = true;
            m_textField.EditingChanged += this.TextField_OnEditingChanged;
            m_textField.EditingDidBegin += this.TextField_OnEditingDidBegin;
            m_textField.EditingDidEnd += this.TextField_OnEditingDidEnd;
            m_textField.ShouldReturn = this.TextField_OnShouldReturn;
        }

        private void UnhookTextField ()
        {
            if (!m_isFieldHooked)
            {
                return;
            }

            m_isFieldHooked = false;
            m_textField.EditingChanged -= this.TextField_OnEditingChanged;
            m_textField.EditingDidBegin -= this.TextField_OnEditingDidBegin;
            m_textField.EditingDidEnd -= this.TextField_OnEditingDidEnd;
            m_textField.ShouldReturn = null;
        }

        private void DoNudge (bool positive, UIKeyModifierFlags modifiers)
        {
            double amount = NumericEditorController.PickNudgeAmount(
                modifiers.HasFlag(UIKeyModifierFlags.Command) || modifiers.HasFlag(UIKeyModifierFlags.Control),
                modifiers.HasFlag(UIKeyModifierFlags.Shift),
                this.Nudge,
                this.BigNudge,
                this.SmallNudge
            );

            m_controller.Nudge(positive, amount);
        }

        // ===========[ Subclasses/structs ]===================================
        // A nudge button that steps once when pressed and keeps stepping while held, the way WinUI3's RepeatButton
        // does. It remembers the keyboard modifiers held when the press began, so Shift or Command picks the step.
        // It holds its editor weakly: the editor keeps the button alive natively as a subview, so a strong
        // reference back would be a cycle across native objects.
        private sealed class NudgeButton : UIButton
        {
            private const double kRepeatDelaySeconds = 0.4;
            private const double kRepeatIntervalSeconds = 0.08;

            private readonly WeakReference<NumericEditor> m_editor;
            private readonly bool m_positive;
            private UIKeyModifierFlags m_modifiers;
            private NSTimer m_timer;

            public NudgeButton (NumericEditor editor, bool positive, string symbolName) : base(CGRect.Empty)
            {
                const float kButtonWidth = 36f;

                m_editor = new WeakReference<NumericEditor>(editor);
                m_positive = positive;

                UIButtonConfiguration configuration = UIButtonConfiguration.PlainButtonConfiguration;
                configuration.Image = UIImage.GetSystemImage(symbolName);
                configuration.BaseForegroundColor = ThemedColors.Get(ThemedColorKeys.ButtonForeground);
                UIBackgroundConfiguration background = configuration.Background;
                background.BackgroundColor = ThemedColors.Get(ThemedColorKeys.ButtonBackground);
                background.CornerRadius = 0;
                configuration.Background = background;
                this.Configuration = configuration;
                this.AccessibilityLabel = positive ? "Increase" : "Decrease";

                this.TranslatesAutoresizingMaskIntoConstraints = false;
                this.WidthAnchor.ConstraintEqualTo(kButtonWidth).Active = true;
            }

            // While pressed the whole button shows the theme's primary highlight, the way WinUI3's nudge buttons do in
            // their Pressed state. UIKit asks for this on every state change. base.UpdateConfiguration is skipped so the
            // system does not lay its own dimmed highlight over it.
            public override void UpdateConfiguration ()
            {
                UIButtonConfiguration configuration = this.Configuration ?? UIButtonConfiguration.PlainButtonConfiguration;
                UIBackgroundConfiguration background = configuration.Background;
                background.BackgroundColor = ThemedColors.Get(this.Highlighted ? ThemedColorKeys.NumericEditor_NudgePressedBackground : ThemedColorKeys.ButtonBackground);
                background.CornerRadius = 0;
                configuration.Background = background;
                this.Configuration = configuration;
            }

            public override bool BeginTracking (UITouch uitouch, UIEvent uievent)
            {
                m_modifiers = uievent?.ModifierFlags ?? 0;
                this.Step();
                this.StopRepeating();
                m_timer = NSTimer.CreateScheduledTimer(TimeSpan.FromSeconds(kRepeatDelaySeconds), this.OnRepeatDelayElapsed);
                return base.BeginTracking(uitouch, uievent);
            }

            public override void EndTracking (UITouch uitouch, UIEvent uievent)
            {
                this.StopRepeating();
                base.EndTracking(uitouch, uievent);
            }

            public override void CancelTracking (UIEvent uievent)
            {
                this.StopRepeating();
                base.CancelTracking(uievent);
            }

            public void StopRepeating ()
            {
                m_timer?.Invalidate();
                m_timer = null;
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing)
                {
                    this.StopRepeating();
                }

                base.Dispose(disposing);
            }

            private void OnRepeatDelayElapsed (NSTimer timer)
            {
                m_timer = NSTimer.CreateRepeatingScheduledTimer(TimeSpan.FromSeconds(kRepeatIntervalSeconds), this.OnRepeatTick);
            }

            private void OnRepeatTick (NSTimer timer) => this.Step();

            private void Step ()
            {
                if (m_editor.TryGetTarget(out NumericEditor editor))
                {
                    editor.DoNudge(m_positive, m_modifiers);
                }
                else
                {
                    this.StopRepeating();
                }
            }
        }
    }
}
