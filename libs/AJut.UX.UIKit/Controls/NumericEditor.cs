namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using CoreAnimation;
    using CoreGraphics;
    using AJut.UX.Helpers;
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
    // Layout, leading to trailing, as Android's: the label (none unless LabelText or LabelView gives one), then the frame,
    // holding the decrease button, the text field in its well, the warning glyph (shown while the out of bounds error
    // is) and the increase button. The buttons repeat while held.
    //
    // Look: MobileLook.NumericEditor's, which Android's NumericEditor draws too, so the keys, sizes and shading all come
    // from there and only the unthemed defaults (in ThemedColorKeys) are UIKit's own. The frame is one rounded rect on the
    // pop-out surface, outlined with the NumericEditor border keys; the number sits in a well of the text box color, inset
    // from the frame's top and bottom and shaded at its edges; and the nudge buttons have no fill of their own, so they
    // read as part of the frame, until pressed, when they fill with the bright highlight.
    //
    // Nudge tiers: a tap steps by Nudge. With a hardware keyboard (iPad), holding Shift steps by SmallNudge and
    // holding Command or Control steps by BigNudge, on the buttons and on the Up / Down arrow keys alike.
    //
    // IsReadOnly shows the value without letting it be typed or nudged, the same as the AppKit and Android editors: the
    // field and the buttons are disabled, the number dims, and the arrow keys do nothing.
    //
    // ValueChanged is sent for a user edit only (typing, a nudge, a commit that changes the value), never for a
    // Value set from code, the same as every UIKit control; that is what the BindingSet adapters listen to.

    public class NumericEditor : UIControl, INumericEditorHost
    {
        // Themed, the frame is the pop-out surface AJut's themed NumericEditor style gives it on WinUI3. Unthemed, the
        // stock control fill Android's falls back to.
        private static readonly ThemedGradient kSurface = new ThemedGradient(
            MobileLook.NumericEditor.FrameSurface,
            new[] { ThemedGradient.Stop(0.0, 0xFFFDFDFD), ThemedGradient.Stop(1.0, 0xFFFDFDFD) },
            new[] { ThemedGradient.Stop(0.0, 0xFF2B2B2B), ThemedGradient.Stop(1.0, 0xFF2B2B2B) }
        );

        // Read-only, the frame is the disabled surface, so an editor that can't be edited looks disabled; unthemed, a
        // little flatter than the rest surface. A solid key comes back as two equal stops.
        private static readonly ThemedGradient kDisabledSurface = new ThemedGradient(
            MobileLook.NumericEditor.FrameSurfaceDisabled,
            new[] { ThemedGradient.Stop(0.0, 0xFFF3F3F3), ThemedGradient.Stop(1.0, 0xFFF3F3F3) },
            new[] { ThemedGradient.Stop(0.0, 0xFF262626), ThemedGradient.Stop(1.0, 0xFF262626) }
        );

        // ===========[ Instance Fields ]===================================
        private readonly NumericEditorController m_controller;
        private readonly CAGradientLayer m_surface = new CAGradientLayer
        {
            StartPoint = new CGPoint(0.5, 0.0),
            EndPoint = new CGPoint(0.5, 1.0),
            ZPosition = -1,
        };
        // The label, then the frame
        private readonly UIStackView m_outerRow = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Alignment = UIStackViewAlignment.Center,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };
        private readonly UIView m_frame = new UIView { ClipsToBounds = true };

        // The buttons and the well, inside the frame
        private readonly UIStackView m_row = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Alignment = UIStackViewAlignment.Fill,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };
        private readonly UILabel m_label = new UILabel { Hidden = true };
        private readonly UITextField m_textField = new UITextField
        {
            KeyboardType = UIKeyboardType.NumbersAndPunctuation,
            AutocorrectionType = UITextAutocorrectionType.No,
            SpellCheckingType = UITextSpellCheckingType.No,
        };
        private readonly UIImageView m_warningGlyph = new UIImageView { Hidden = true, ContentMode = UIViewContentMode.Center };
        private readonly UIToolTipInteraction m_warningToolTip = new UIToolTipInteraction();
        private readonly FieldWell m_well;
        private readonly NudgeButton m_decreaseButton;
        private readonly NudgeButton m_increaseButton;
        private ErrorBubble m_errorBubble;
        private UIView m_labelView;
        private object m_value;

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        private Type m_enforceNumericType;

        private bool m_isSettingFromCode;
        private double m_minimum = double.NegativeInfinity;
        private double m_maximum = double.PositiveInfinity;
        private eOutOfBoundsResponse m_outOfBoundsResponse = eOutOfBoundsResponse.ErrorAndToolTip;
        private bool m_isReadOnly;
        private bool m_isFieldHooked;
        private bool m_isPointerOverFrame;

        // ===========[ Setup/Construction/Teardown ]===================================
        public NumericEditor () : base(CGRect.Empty)
        {
            m_controller = new NumericEditorController(this);
            m_well = new FieldWell(m_textField);
            m_decreaseButton = new NudgeButton(this, positive: false, "minus");
            m_increaseButton = new NudgeButton(this, positive: true, "plus");
            this.Setup();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native control.</summary>
        protected NumericEditor (NativeHandle handle) : base(handle)
        {
            m_controller = new NumericEditorController(this);
            m_well = new FieldWell(m_textField);
            m_decreaseButton = new NudgeButton(this, positive: false, "minus");
            m_increaseButton = new NudgeButton(this, positive: true, "plus");
            this.Setup();
        }

        private void Setup ()
        {
            // 1. The frame: rounded, outlined, on the surface. The surface sits under every subview (a negative
            //  zPosition), and the layer's border is drawn over it. The row inside is inset by the border, so a pressed
            //  button fills inside the outline rather than over it.
            m_frame.Layer.CornerRadius = MobileLook.NumericEditor.CornerRadius;
            m_frame.Layer.BorderWidth = MobileLook.NumericEditor.BorderThickness;
            m_frame.Layer.InsertSublayer(m_surface, 0);
            m_frame.AddSubview(m_row);
            nfloat border = MobileLook.NumericEditor.BorderThickness;
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_row.LeadingAnchor.ConstraintEqualTo(m_frame.LeadingAnchor, border),
                m_row.TrailingAnchor.ConstraintEqualTo(m_frame.TrailingAnchor, -border),
                m_row.TopAnchor.ConstraintEqualTo(m_frame.TopAnchor, border),
                m_row.BottomAnchor.ConstraintEqualTo(m_frame.BottomAnchor, -border),
            });

            // 2. Decrease, the well, the warning glyph, increase
            m_well.SetContentHuggingPriority((float)UILayoutPriority.DefaultLow, UILayoutConstraintAxis.Horizontal);
            m_textField.SetContentHuggingPriority((float)UILayoutPriority.DefaultLow, UILayoutConstraintAxis.Horizontal);
            m_textField.TextColor = ThemedColors.Get(MobileLook.NumericEditor.Number);
            m_textField.TextAlignment = UITextAlignment.Center;
            m_warningGlyph.Image = UIImage.GetSystemImage("exclamationmark.triangle.fill");
            m_warningGlyph.TintColor = ThemedColors.Get(MobileLook.NumericEditor.FrameBorderError);
            m_warningGlyph.SetContentHuggingPriority((float)UILayoutPriority.Required, UILayoutConstraintAxis.Horizontal);
            m_warningGlyph.AddInteraction(m_warningToolTip);

            // A pointer shows the message as the glyph's tooltip; a finger has no hover, so a long press on the glyph
            // shows it in a bubble, as Android's long press tooltip does. The handler is static, for the reason the hover
            // handler below is.
            m_warningGlyph.UserInteractionEnabled = true;
            m_warningGlyph.AddGestureRecognizer(new UILongPressGestureRecognizer(OnWarningGlyphLongPress));
            m_row.AddArrangedSubview(m_decreaseButton);
            m_row.AddArrangedSubview(m_well);
            m_row.AddArrangedSubview(m_warningGlyph);
            m_row.AddArrangedSubview(m_increaseButton);

            // 3. The label in front of the frame, hidden until it is given text
            m_label.TextColor = ThemedColors.Get(MobileLook.NumericEditor.Label);
            m_label.SetContentHuggingPriority((float)UILayoutPriority.Required, UILayoutConstraintAxis.Horizontal);
            m_frame.SetContentHuggingPriority((float)UILayoutPriority.DefaultLow, UILayoutConstraintAxis.Horizontal);
            m_outerRow.Spacing = MobileLook.NumericEditor.LabelSpacing;
            m_outerRow.AddArrangedSubview(m_label);
            m_outerRow.AddArrangedSubview(m_frame);
            this.AddSubview(m_outerRow);

            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_outerRow.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_outerRow.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                m_outerRow.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_outerRow.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
            });

            // 4. A mouse, trackpad or Pencil over the frame brightens its border. The view keeps its gesture recognizers
            //  alive, so a handler that captured this editor would tie the two into a cycle across native objects: the
            //  handler is static and finds the editor through the recognizer's View.
            this.AddGestureRecognizer(new UIHoverGestureRecognizer(OnHover));
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
                m_isPointerOverFrame = false;
                this.HideErrorBubble();
            }
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                this.UnhookTextField();
                this.HideErrorBubble();
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

        // The recognizer is on the glyph, deep in the editor, so the editor is found up the glyph's superviews
        private static void OnWarningGlyphLongPress (UILongPressGestureRecognizer recognizer)
        {
            if (recognizer.State != UIGestureRecognizerState.Began)
            {
                return;
            }

            for (UIView view = recognizer.View?.Superview; view != null; view = view.Superview)
            {
                if (view is NumericEditor owner)
                {
                    owner.ShowErrorBubble();
                    return;
                }
            }
        }

        // The recognizer is on the whole editor, so the frame is told apart from the label by where the pointer is, as
        // Android's editor does
        private static void OnHover (UIHoverGestureRecognizer recognizer)
        {
            if (recognizer.View is NumericEditor owner)
            {
                bool isHovering = (recognizer.State == UIGestureRecognizerState.Began)
                    || (recognizer.State == UIGestureRecognizerState.Changed);
                bool isOverFrame = isHovering && owner.m_frame.Bounds.Contains(recognizer.LocationInView(owner.m_frame));
                if (isOverFrame != owner.m_isPointerOverFrame)
                {
                    owner.m_isPointerOverFrame = isOverFrame;
                    owner.SetNeedsLayout();
                }
            }
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

        /// <summary>How many decimal places the value may have; -1 (the default) for any. As Android's.</summary>
        public int DecimalPlacesAllowed { get; set; } = -1;

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

        /// <summary>
        /// Shows the value without letting it be typed or nudged, and looks disabled, as an editor that can't be edited
        /// does: the field and the nudge buttons are disabled, the number and glyphs take the disabled text color, the
        /// frame the disabled surface and border, and the arrow keys and the pointer do nothing. A Value set from code still
        /// shows.
        /// </summary>
        public bool IsReadOnly
        {
            get => m_isReadOnly;
            set
            {
                m_isReadOnly = value;
                this.SetNeedsLayout();

                // An edit in progress ends first, which commits it the way leaving the field does
                if (value)
                {
                    m_textField.ResignFirstResponder();
                }

                m_textField.Enabled = !value;
                m_textField.TextColor = ThemedColors.Get(value ? MobileLook.NumericEditor.ReadOnlyText : MobileLook.NumericEditor.Number);
                m_decreaseButton.Enabled = !value;
                m_increaseButton.Enabled = !value;
                if (value)
                {
                    m_decreaseButton.StopRepeating();
                    m_increaseButton.StopRepeating();
                }
            }
        }

        /// <summary>The out of bounds message while the error is shown, otherwise null.</summary>
        public string ErrorMessage { get; private set; }

        /// <summary>The label shown in front of the frame, as Android's Label is; null or empty (the default) for none. Ignored while <see cref="LabelView"/> is set.</summary>
        public string LabelText
        {
            get => m_label.Text;
            set
            {
                m_label.Text = value;
                this.UpdateLabelVisibility();
            }
        }

        /// <summary>A view shown in front of the frame in place of the text label, the way WinUI3's LabelContent replaces its "#" glyph. Null brings the text label back.</summary>
        public UIView LabelView
        {
            get => m_labelView;
            set
            {
                m_labelView?.RemoveFromSuperview();
                m_labelView = value;
                if (value != null)
                {
                    m_outerRow.InsertArrangedSubview(value, 0);
                }

                this.UpdateLabelVisibility();
            }
        }

        /// <summary>Where the number sits in its well: centered by default, as on Android</summary>
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
            // LayoutSubviews again whenever the appearance (light/dark) changes. A key with no unthemed color of its own
            // takes the tint (the focused border), or a dim system color (the read-only border).
            string borderKey = MobileLook.NumericEditor.FrameBorderFor(m_isReadOnly, this.ErrorMessage != null, m_isPointerOverFrame, m_textField.IsEditing);
            UIColor border = ThemedColors.Get(borderKey)
                ?? ((borderKey == MobileLook.NumericEditor.FrameBorderDisabled) ? UIColor.TertiaryLabel : this.TintColor);
            m_frame.Layer.BorderColor = border.GetResolvedColor(this.TraitCollection).CGColor;

            // The surface's colors are CGColors as well, so they are worked out here for the same reason
            IReadOnlyList<AJutGradientStop> stops = (m_isReadOnly ? kDisabledSurface : kSurface).Resolve(this.TraitCollection.UserInterfaceStyle);
            var colors = new CGColor[stops.Count];
            var locations = new NSNumber[stops.Count];
            for (int index = 0; index < stops.Count; ++index)
            {
                colors[index] = stops[index].Color.ToUIColor().CGColor;
                locations[index] = new NSNumber(stops[index].Offset);
            }

            // The frame is an arranged view of the outer row, and a stack view places its arranged views in its own
            // LayoutSubviews, which runs after this one. Read now, its bounds would be the last pass's: empty the first
            // time, and a size behind after a rotation or a split view resize. Laying the row out first gives this pass's.
            m_outerRow.LayoutIfNeeded();

            CATransaction.Begin();
            CATransaction.DisableActions = true;
            m_surface.Frame = m_frame.Bounds;
            m_surface.Colors = colors;
            m_surface.Locations = locations;
            CATransaction.Commit();
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
        int INumericEditorSettings.DecimalPlacesAllowed => this.DecimalPlacesAllowed;
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
            this.HideErrorBubble();
            this.SetNeedsLayout();
        }

        // ===========[ Helper Methods ]===================================
        // The bubble goes in the window, over everything, above the glyph (below it when there's no room above), and is
        // kept inside the safe area. It takes no touches, so a touch under it carries on as usual.
        private void ShowErrorBubble ()
        {
            const float kScreenMargin = 8f;

            this.HideErrorBubble();
            UIWindow window = this.Window;
            if ((window == null) || (this.ErrorMessage == null))
            {
                return;
            }

            var bubble = new ErrorBubble(this.ErrorMessage, this.TraitCollection);
            CGSize size = bubble.Fit(MobileLook.NumericEditor.ErrorBubbleMaxWidth);
            CGRect glyph = m_warningGlyph.ConvertRectToView(m_warningGlyph.Bounds, window);
            CGRect safe = window.SafeAreaLayoutGuide.LayoutFrame;

            nfloat x = glyph.GetMidX() - (size.Width / 2f);
            x = (nfloat)Math.Max((double)(safe.Left + kScreenMargin), Math.Min((double)x, (double)(safe.Right - kScreenMargin - size.Width)));
            nfloat y = glyph.Top - MobileLook.NumericEditor.ErrorBubbleGap - size.Height;
            if (y < safe.Top + kScreenMargin)
            {
                y = glyph.Bottom + MobileLook.NumericEditor.ErrorBubbleGap;
            }

            bubble.Frame = new CGRect(x, y, size.Width, size.Height);
            window.AddSubview(bubble);
            m_errorBubble = bubble;
            bubble.StartExpiry();
        }

        private void HideErrorBubble ()
        {
            m_errorBubble?.RemoveFromSuperview();
            m_errorBubble = null;
        }

        // The text label shows only with text to show, and never while a LabelView stands in for it
        private void UpdateLabelVisibility () => m_label.Hidden = (m_labelView != null) || string.IsNullOrEmpty(m_label.Text);

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
            // The buttons are disabled while read only, but the arrow keys come through the responder chain regardless
            if (m_isReadOnly)
            {
                return;
            }

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
        // The error message on a long press: the text in a rounded, outlined fill, in the tooltip keys. It takes no
        // touches, VoiceOver skips it (the glyph already reads the message), and it takes itself away after
        // ErrorBubbleDuration. The border is a CGColor, resolved for the appearance it is shown in, which it lives too
        // briefly to see change.
        private sealed class ErrorBubble : UIView
        {
            private readonly UILabel m_text;

            public ErrorBubble (string message, UITraitCollection traits) : base(CGRect.Empty)
            {
                this.UserInteractionEnabled = false;
                this.AccessibilityElementsHidden = true;
                this.BackgroundColor = ThemedColors.Get(MobileLook.NumericEditor.ErrorBubbleBackground) ?? UIColor.SecondarySystemBackground;
                this.Layer.CornerRadius = MobileLook.NumericEditor.ErrorBubbleCornerRadius;
                this.Layer.BorderWidth = MobileLook.NumericEditor.BorderThickness;
                this.Layer.BorderColor = (ThemedColors.Get(MobileLook.NumericEditor.ErrorBubbleBorder) ?? UIColor.Separator).GetResolvedColor(traits).CGColor;

                m_text = new UILabel
                {
                    Text = message,
                    Lines = 0,
                    Font = UIFont.PreferredFootnote,
                    TextColor = ThemedColors.Get(MobileLook.NumericEditor.ErrorBubbleText) ?? UIColor.Label,
                };

                this.AddSubview(m_text);
            }

            /// <summary>Wraps the text within <paramref name="maxWidth"/> and returns the bubble's size around it</summary>
            public CGSize Fit (nfloat maxWidth)
            {
                nfloat padding = MobileLook.NumericEditor.ErrorBubblePadding;
                CGSize text = m_text.SizeThatFits(new CGSize(maxWidth - (2f * padding), nfloat.MaxValue));
                m_text.Frame = new CGRect(padding, padding, text.Width, text.Height);
                return new CGSize(text.Width + (2f * padding), text.Height + (2f * padding));
            }

            /// <summary>Takes the bubble away once its time is up</summary>
            public void StartExpiry ()
            {
                NSTimer.CreateScheduledTimer(MobileLook.NumericEditor.ErrorBubbleDuration, new BubbleExpiry(this).Expire);
            }
        }

        // Removes a bubble when its time is up. It holds the bubble weakly, so the timer pins nothing, and a bubble
        // already hidden or replaced is only removed again, which does nothing.
        private sealed class BubbleExpiry
        {
            private readonly WeakReference<UIView> m_bubble;

            public BubbleExpiry (UIView bubble)
            {
                m_bubble = new WeakReference<UIView>(bubble);
            }

            public void Expire (NSTimer timer)
            {
                if (m_bubble.TryGetTarget(out UIView bubble))
                {
                    bubble.RemoveFromSuperview();
                }
            }
        }

        // A nudge button that steps once when pressed and keeps stepping while held, the way WinUI3's RepeatButton
        // does. It remembers the keyboard modifiers held when the press began, so Shift or Command picks the step.
        // It holds its editor weakly: the editor keeps the button alive natively as a subview, so a strong
        // reference back would be a cycle across native objects.
        private sealed class NudgeButton : UIButton
        {
            private readonly WeakReference<NumericEditor> m_editor;
            private readonly bool m_positive;
            private UIKeyModifierFlags m_modifiers;
            private NSTimer m_timer;

            public NudgeButton (NumericEditor editor, bool positive, string symbolName) : base(CGRect.Empty)
            {
                m_editor = new WeakReference<NumericEditor>(editor);
                m_positive = positive;

                UIButtonConfiguration configuration = UIButtonConfiguration.PlainButtonConfiguration;
                configuration.Image = UIImage.GetSystemImage(symbolName);
                configuration.BaseForegroundColor = ThemedColors.Get(MobileLook.NumericEditor.StepGlyph);
                UIBackgroundConfiguration background = configuration.Background;
                background.BackgroundColor = UIColor.Clear;
                background.CornerRadius = 0;
                configuration.Background = background;
                this.Configuration = configuration;
                this.AccessibilityLabel = positive ? "Increase" : "Decrease";

                this.TranslatesAutoresizingMaskIntoConstraints = false;
                this.WidthAnchor.ConstraintEqualTo(MobileLook.NumericEditor.StepButtonWidth).Active = true;
            }

            public override void UpdateConfiguration ()
            {
                // The button is the editor's frame showing through at rest, takes a wash of the bright highlight under a
                // mouse, trackpad or Pencil, and fills with it when pressed, so a press reads clearly on touch too, where
                // nothing leads into it. Disabled (a read only editor) its glyph dims as the number does, with no hover.
                // UIKit asks for this on every state change; base.UpdateConfiguration is skipped so the system lays no
                // dimmed highlight of its own over it.
                UIColor fill = UIColor.Clear;
                string foregroundKey = MobileLook.NumericEditor.StepGlyph;
                if (!this.Enabled)
                {
                    foregroundKey = MobileLook.NumericEditor.ReadOnlyText;
                }
                else if (this.Highlighted)
                {
                    fill = ThemedColors.Get(MobileLook.NumericEditor.StepPressedFill);
                }
                else if (this.Hovered)
                {
                    UIColor pointerOver = ThemedColors.Get(MobileLook.NumericEditor.StepPointerOverFill);
                    fill = pointerOver.ColorWithAlpha(pointerOver.CGColor.Alpha * MobileLook.NumericEditor.StepPointerOverOpacity);
                }

                UIButtonConfiguration configuration = this.Configuration ?? UIButtonConfiguration.PlainButtonConfiguration;
                UIBackgroundConfiguration background = configuration.Background;
                background.BackgroundColor = fill;
                background.CornerRadius = 0;
                configuration.Background = background;
                configuration.BaseForegroundColor = ThemedColors.Get(foregroundKey);
                this.Configuration = configuration;
            }

            public override bool BeginTracking (UITouch uitouch, UIEvent uievent)
            {
                m_modifiers = uievent?.ModifierFlags ?? 0;
                this.Step();
                this.StopRepeating();
                m_timer = NSTimer.CreateScheduledTimer(MobileLook.NumericEditor.RepeatDelay, this.OnRepeatDelayElapsed);
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
                m_timer = NSTimer.CreateRepeatingScheduledTimer(MobileLook.NumericEditor.RepeatInterval, this.OnRepeatTick);
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

        // The text field's well: the text box color, inset from the frame's top and bottom so the frame shows around it,
        // and sunk into it by the edge lighting WinUI3's NumericEditor draws with two nested borders, a shadow along the
        // top and left and a light line along the bottom and right. The lighting is the same literal shading over any
        // theme, as it is there. A view that draws itself is drawn again when the appearance changes, so the themed fill
        // follows light and dark with nothing to refresh.
        private sealed class FieldWell : UIView
        {
            private const float kInset = MobileLook.NumericEditor.WellInset;
            private const float kShadowThickness = MobileLook.NumericEditor.WellShadowThickness;
            private const float kLightThickness = MobileLook.NumericEditor.WellLightThickness;
            private const float kTextPadding = MobileLook.NumericEditor.WellTextPadding;
            private static readonly UIColor kShadow = MobileLook.NumericEditor.WellShadow.ToUIColor();
            private static readonly UIColor kLight = MobileLook.NumericEditor.WellLight.ToUIColor();

            public FieldWell (UITextField field) : base(CGRect.Empty)
            {
                this.BackgroundColor = UIColor.Clear;
                this.Opaque = false;
                this.ContentMode = UIViewContentMode.Redraw;

                field.TranslatesAutoresizingMaskIntoConstraints = false;
                this.AddSubview(field);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    field.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor, kShadowThickness + kTextPadding),
                    field.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor, -(kLightThickness + kTextPadding)),
                    field.TopAnchor.ConstraintEqualTo(this.TopAnchor, kInset + kShadowThickness),
                    field.BottomAnchor.ConstraintEqualTo(this.BottomAnchor, -(kInset + kLightThickness)),
                });
            }

            public override void Draw (CGRect rect)
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
                Fill(ThemedColors.Get(MobileLook.NumericEditor.WellSurface), left + kShadowThickness, top + kShadowThickness, right - kLightThickness, bottom - kLightThickness);
            }

            private static void Fill (UIColor color, nfloat left, nfloat top, nfloat right, nfloat bottom)
            {
                if ((color == null) || (right <= left) || (bottom <= top))
                {
                    return;
                }

                color.SetFill();
                UIGraphics.RectFill(new CGRect(left, top, right - left, bottom - top));
            }
        }
    }
}
