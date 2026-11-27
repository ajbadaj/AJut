namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using AJut.DateAndTime;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using CoreAnimation;
    using CoreGraphics;
    using Foundation;
    using ObjCRuntime;
    using UIKit;

    // ===========[ DateTimeEditIngressControl ]=================================
    // UIKit date and time editor. At rest its text field shows the value, in DisplayFormat or the culture's own short form.
    // Typed text is read by TextResolver (DateTimeTextResolver when there is none), so "in two weeks", "friday" and "+3d"
    // are as good as a written date. The button presents UIKit's own UIDatePicker, an inline calendar, time wheels, or
    // both, as a popover anchored on the button on iPad and as a sheet on iPhone (UIKit adapts the popover), as
    // ColorEditIngressControl presents its picker.
    //
    // What typed text comes to, what a picked date or time does to the value, and when an edit is complete are AJut.UX's
    // DateTimeEditController, which the AppKit and Android controls run too, over the DateTimeEditRules every stack calls.
    //
    // Editing:
    //   Text changes          -> read as typed, for the invalid look and the tooltip; nothing changes yet
    //   Return or focus leaving -> valid text becomes the value; text that can't be read stays, to be fixed
    //   Escape (a hardware keyboard) -> the value goes back in the field
    //   Button                -> anything typed is settled, then the pickers open on the value
    //   A pick                -> the value changes right away, and ValueChanged is sent; with only a date to pick,
    //                            picking it closes the pickers
    //   Pickers closed        -> UserEditComplete, if the value changed
    //   Read-only             -> the field takes no typing; the pickers open marked read-only, and take no picks
    //
    // The look is MobileLook.DateTimeEditIngressControl's, which Android's control draws too.

    /// <summary>
    /// A date and time field that takes free-form typed text ("tomorrow", "in two weeks", "friday 3pm", a written date)
    /// and has a button that opens UIKit's date and time pickers. It sends ValueChanged when a person changes the value,
    /// and shows text it can't read with the invalid border and a warning glyph that says why.
    /// </summary>
    public class DateTimeEditIngressControl : UIControl, IUserEditNotifier, IDateTimeEditHost
    {
        // What UIDatePicker is handed and hands back is a moment, which it places on a day by its own time zone. A date
        //  goes in at the middle of its day, so no zone or daylight saving shift can move it onto the day before or after.
        private const int kMiddayHour = 12;

        // Themed, the frame is the pop-out surface; unthemed, the stock control fill Android's falls back to
        private static readonly ThemedGradient kSurface = new ThemedGradient(
            MobileLook.DateTimeEditIngressControl.FrameSurface,
            new[] { ThemedGradient.Stop(0.0, 0xFFFDFDFD), ThemedGradient.Stop(1.0, 0xFFFDFDFD) },
            new[] { ThemedGradient.Stop(0.0, 0xFF2B2B2B), ThemedGradient.Stop(1.0, 0xFF2B2B2B) }
        );

        // ===========[ Instance Fields ]===================================
        private readonly DateTimeEditController m_controller;
        private readonly CAGradientLayer m_surface = new CAGradientLayer
        {
            StartPoint = new CGPoint(0.5, 0.0),
            EndPoint = new CGPoint(0.5, 1.0),
            ZPosition = -1,
        };
        private readonly UIView m_frame = new UIView { ClipsToBounds = true, TranslatesAutoresizingMaskIntoConstraints = false };
        private readonly UIStackView m_row = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Alignment = UIStackViewAlignment.Fill,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };
        private readonly UIView m_well = new UIView();
        private readonly UITextField m_textField = new UITextField
        {
            AutocorrectionType = UITextAutocorrectionType.No,
            SpellCheckingType = UITextSpellCheckingType.No,
            ReturnKeyType = UIReturnKeyType.Done,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };
        private readonly UIImageView m_warningGlyph = new UIImageView { Hidden = true, ContentMode = UIViewContentMode.Center };
        private readonly UIToolTipInteraction m_warningToolTip = new UIToolTipInteraction();
        private readonly PickerButton m_pickerButton;
        private string m_unsetTextPrompt;
        private UIImage m_pickerButtonContent;
        private PickerController m_openPickers;
        private bool m_isFieldHooked;

        // ===========[ Setup/Construction/Teardown ]===================================
        public DateTimeEditIngressControl () : base(CGRect.Empty)
        {
            m_controller = new DateTimeEditController(this);
            m_pickerButton = new PickerButton(this);
            this.Setup();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native control.</summary>
        protected DateTimeEditIngressControl (NativeHandle handle) : base(handle)
        {
            m_controller = new DateTimeEditController(this);
            m_pickerButton = new PickerButton(this);
            this.Setup();
        }

        private void Setup ()
        {
            nfloat border = MobileLook.DateTimeEditIngressControl.BorderThickness;
            nfloat wellInset = MobileLook.DateTimeEditIngressControl.WellInset;
            nfloat textPadding = MobileLook.DateTimeEditIngressControl.WellTextPadding;

            // 1. The frame: rounded, outlined, on the surface, which sits under every subview (a negative zPosition). The
            //  row inside is inset by the border, so the picker button's fill sits inside the outline.
            m_frame.Layer.CornerRadius = MobileLook.DateTimeEditIngressControl.CornerRadius;
            m_frame.Layer.BorderWidth = border;
            m_frame.Layer.InsertSublayer(m_surface, 0);
            m_frame.AddSubview(m_row);
            this.AddSubview(m_frame);

            // 2. The field in its well, the warning glyph, the picker button
            m_well.BackgroundColor = ThemedColors.Get(MobileLook.DateTimeEditIngressControl.WellSurface);
            m_well.Layer.CornerRadius = (nfloat)Math.Max(0.0, (double)(MobileLook.DateTimeEditIngressControl.CornerRadius - border));
            m_well.AddSubview(m_textField);
            m_well.SetContentHuggingPriority((float)UILayoutPriority.DefaultLow, UILayoutConstraintAxis.Horizontal);
            m_textField.TextColor = ThemedColors.Get(MobileLook.DateTimeEditIngressControl.Text);

            m_warningGlyph.Image = UIImage.GetSystemImage("exclamationmark.triangle.fill");
            m_warningGlyph.TintColor = ThemedColors.Get(MobileLook.DateTimeEditIngressControl.WarningGlyph);
            m_warningGlyph.SetContentHuggingPriority((float)UILayoutPriority.Required, UILayoutConstraintAxis.Horizontal);
            m_warningGlyph.AddInteraction(m_warningToolTip);

            m_row.Spacing = MobileLook.DateTimeEditIngressControl.WarningGlyphSpacing;
            m_row.AddArrangedSubview(m_well);
            m_row.AddArrangedSubview(m_warningGlyph);
            m_row.AddArrangedSubview(m_pickerButton);

            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_frame.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_frame.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                m_frame.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_frame.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_frame.HeightAnchor.ConstraintGreaterThanOrEqualTo(MobileLook.DateTimeEditIngressControl.MinTouchHeight),
                m_row.LeadingAnchor.ConstraintEqualTo(m_frame.LeadingAnchor, border + wellInset),
                m_row.TrailingAnchor.ConstraintEqualTo(m_frame.TrailingAnchor, -border),
                m_row.TopAnchor.ConstraintEqualTo(m_frame.TopAnchor, border),
                m_row.BottomAnchor.ConstraintEqualTo(m_frame.BottomAnchor, -border),
                m_textField.LeadingAnchor.ConstraintEqualTo(m_well.LeadingAnchor, textPadding),
                m_textField.TrailingAnchor.ConstraintEqualTo(m_well.TrailingAnchor, -textPadding),
                m_textField.TopAnchor.ConstraintEqualTo(m_well.TopAnchor, wellInset),
                m_textField.BottomAnchor.ConstraintEqualTo(m_well.BottomAnchor, -wellInset),
            });

            this.ApplyPlaceholder();
            this.ApplyAccessibility();
            m_controller.ShowValueAsText();
        }

        public override void MovedToWindow ()
        {
            base.MovedToWindow();

            // The text field is a subview, so this control keeps it alive natively, and a handler of this control held by
            // the field would keep this control alive from the managed side: a cycle across native objects the GC cannot
            // see. So the field's events are hooked only while the control is on screen, as NumericEditor's are, and a
            // control leaving its window closes its pickers.
            if (this.Window != null)
            {
                this.HookTextField();
            }
            else
            {
                this.UnhookTextField();
                this.ClosePickers();
            }
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                this.UnhookTextField();
                m_openPickers?.Release();
                m_openPickers = null;
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]===================================
        /// <summary>
        /// Raised once a person has changed the value: when typed text is taken, and when the pickers close on a different
        /// value than they opened on. Values set from code don't raise it. Old and new values are DateTime?.
        /// </summary>
        public event EventHandler<UserEditAppliedEventArgs> UserEditComplete;

        private void TextField_OnEditingChanged (object sender, EventArgs e) => m_controller.OnTextEdited(m_textField.Text);

        private void TextField_OnEditingDidBegin (object sender, EventArgs e) => this.SetNeedsLayout();

        private void TextField_OnEditingDidEnd (object sender, EventArgs e)
        {
            m_controller.CommitText();
            this.SetNeedsLayout();
        }

        private bool TextField_OnShouldReturn (UITextField field)
        {
            // Return ends the edit, and the EditingDidEnd that follows takes the text
            field.ResignFirstResponder();
            return false;
        }

        // ===========[ Properties ]===================================
        /// <summary>
        /// The date and time being edited, or null for none. Setting it in code shows it and sends no ValueChanged. A date
        /// only control keeps it at midnight when it sets it, and a time only control leaves its date as it was.
        /// </summary>
        public DateTime? EditDateTime
        {
            get => m_controller.Value;
            set => m_controller.Value = value;
        }

        /// <summary>Whether the date is edited. With this and <see cref="IncludeTime"/> both off, both are edited.</summary>
        public bool IncludeDate
        {
            get => m_controller.IncludeDate;
            set
            {
                m_controller.IncludeDate = value;
                this.OnWhatIsEditedChanged();
            }
        }

        /// <summary>Whether the time of day is edited</summary>
        public bool IncludeTime
        {
            get => m_controller.IncludeTime;
            set
            {
                m_controller.IncludeTime = value;
                this.OnWhatIsEditedChanged();
            }
        }

        /// <summary>
        /// The .NET date and time format the value is shown in ("yyyy-MM-dd", "ddd, MMM d"). With none, it is the culture's
        /// short date, short time, or both, by what is edited.
        /// A format .NET can't use (one half typed, say) shows the culture's form as well, until it is fixed.
        /// </summary>
        public string DisplayFormat
        {
            get => m_controller.DisplayFormat;
            set => m_controller.DisplayFormat = value;
        }

        /// <summary>
        /// The clock a time is shown and picked on: the culture's own (the default), twelve hour with AM and PM, or twenty
        /// four hour with neither. It sets the shown text while there is no <see cref="DisplayFormat"/>, and the time
        /// wheels either way.
        /// </summary>
        public eTimeFormat TimeFormat
        {
            get => m_controller.TimeFormat;
            set => m_controller.TimeFormat = value;
        }

        /// <summary>What reads typed text and writes the value as text. With none, <see cref="DateTimeTextResolver.Default"/> does both.</summary>
        public IDateTimeTextResolver TextResolver
        {
            get => m_controller.TextResolver;
            set => m_controller.TextResolver = value;
        }

        /// <summary>When true, empty text is invalid rather than a way to set no value</summary>
        public bool TreatEmptyAsInvalid
        {
            get => m_controller.TreatEmptyAsInvalid;
            set => m_controller.TreatEmptyAsInvalid = value;
        }

        /// <summary>
        /// Shows the value without letting it change: the field takes no typing, and its text is in italics. The picker
        /// button still opens the pickers, marked read-only, to show the value, and they take no picks.
        /// </summary>
        public bool IsReadOnly
        {
            get => m_controller.IsReadOnly;
            set
            {
                // Read-only looks as editable does but for the text, in italics and a touch faded
                const float kReadOnlyTextAlpha = 0.85f;
                m_controller.IsReadOnly = value;
                m_textField.Enabled = !value;
                m_textField.Font = WithItalics(m_textField.Font ?? UIFont.PreferredBody, value);
                m_textField.Alpha = value ? kReadOnlyTextAlpha : 1f;
                m_openPickers?.ShowReadOnly(value);
            }
        }

        /// <summary>The prompt the field shows while there is no value and nothing typed</summary>
        public string UnsetTextPrompt
        {
            get => m_unsetTextPrompt;
            set
            {
                m_unsetTextPrompt = value;
                this.ApplyPlaceholder();
                this.ApplyAccessibility();
            }
        }

        /// <summary>
        /// The image the picker button shows, tinted as the button's glyph. With none, it is the calendar symbol, or the
        /// clock symbol for a time only control.
        /// </summary>
        public UIImage PickerButtonContent
        {
            get => m_pickerButtonContent;
            set
            {
                m_pickerButtonContent = value;
                m_pickerButton.SetNeedsUpdateConfiguration();
            }
        }

        /// <summary>Whether the text in the field can be read as a value</summary>
        public bool IsTextValid => m_controller.IsTextValid;

        /// <summary>Why the text in the field can't be read, or null when it can</summary>
        public string InvalidTextReason => m_controller.InvalidTextReason;

        /// <summary>What typed text will come to, or why it can't be read</summary>
        public string CurrentToolTip => m_controller.CurrentToolTip;

        /// <summary>True while the pickers are open</summary>
        public bool ShowEditDisplay => m_openPickers != null;

        /// <summary>The text field, for anything this control does not surface (font, input accessory)</summary>
        public UITextField TextField => m_textField;

        public override UIKeyCommand[] KeyCommands
        {
            get
            {
                // Escape from a hardware keyboard puts the value back, while something typed is waiting to be taken
                var revert = UIKeyCommand.Create(UIKeyCommand.Escape, 0, new Selector("ajutRevertText:"));
                revert.WantsPriorityOverSystemBehavior = true;
                return new[] { revert };
            }
        }

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// Opens the pickers on the value, unless they are open already or the control is in no window. Anything typed is
        /// settled first, so they open on the value the field shows. Read-only, they open marked read-only, to show it.
        /// </summary>
        public void OpenPickers ()
        {
            if (m_openPickers != null)
            {
                return;
            }

            UIViewController presenter = this.FindViewController();
            if ((presenter == null) || !m_controller.BeginPickerEdit())
            {
                return;
            }

            // A controller that is already presenting something cannot present again, so present from the top of
            //  whatever stack is showing
            while (presenter.PresentedViewController != null)
            {
                presenter = presenter.PresentedViewController;
            }

            m_openPickers = new PickerController(this, m_controller.ShowsDate, m_controller.ShowsTime, m_controller.IsReadOnly, m_controller.UsesTwelveHourClock);
            m_openPickers.ShowValue(m_controller.Value);
            m_openPickers.ModalPresentationStyle = UIModalPresentationStyle.Popover;
            UIPopoverPresentationController popover = m_openPickers.PopoverPresentationController;
            if (popover != null)
            {
                popover.SourceView = m_pickerButton;
                popover.SourceRect = m_pickerButton.Bounds;
                popover.Delegate = m_openPickers.DismissWatcher;
            }

            m_pickerButton.SetNeedsUpdateConfiguration();
            presenter.PresentViewController(m_openPickers, true, null);
        }

        /// <summary>Closes the pickers if they are open, which completes the edit</summary>
        public void ClosePickers ()
        {
            PickerController pickers = m_openPickers;
            if (pickers == null)
            {
                return;
            }

            // A dismiss from code tells the popover's delegate nothing, so the edit is ended here
            this.OnPickersClosed();
            pickers.DismissViewController(true, null);
        }

        /// <summary>VoiceOver's activate on the control opens the pickers, read-only or not</summary>
        public override bool AccessibilityActivate ()
        {
            this.OpenPickers();
            return true;
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews();

            // A layer border is a CGColor, which freezes a dynamic color, so it is resolved here: UIKit calls
            //  LayoutSubviews again whenever the appearance (light or dark) changes. A key with no unthemed color of its own
            //  takes the tint (the focused border), as NumericEditor's does.
            string borderKey = MobileLook.DateTimeEditIngressControl.FrameBorderFor(!m_controller.IsTextValid, m_textField.IsEditing);
            UIColor border = ThemedColors.Get(borderKey) ?? this.TintColor;
            m_frame.Layer.BorderColor = border.GetResolvedColor(this.TraitCollection).CGColor;

            // The surface's colors are CGColors as well, so they are worked out here for the same reason
            IReadOnlyList<AJutGradientStop> stops = kSurface.Resolve(this.TraitCollection.UserInterfaceStyle);
            var colors = new CGColor[stops.Count];
            var locations = new NSNumber[stops.Count];
            for (int index = 0; index < stops.Count; ++index)
            {
                colors[index] = stops[index].Color.ToUIColor().CGColor;
                locations[index] = new NSNumber(stops[index].Offset);
            }

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

        [Export("ajutRevertText:")]
        private void RevertTextCommand (UIKeyCommand command)
        {
            if (m_controller.HasUncommittedText)
            {
                m_controller.ShowValueAsText();
            }
        }

        /// <summary>
        /// Makes typed text the value, when it reads as one, as Return and focus leaving the field do. Returns whether typed
        /// text was taken.
        /// </summary>
        internal bool CommitText () => m_controller.CommitText();

        // ===========[ IDateTimeEditHost Implementations ]===================================
        void IDateTimeEditHost.ShowText (string text)
        {
            if (m_textField.Text != text)
            {
                m_textField.Text = text;
            }
        }

        void IDateTimeEditHost.OnValueChanged (bool isFromPerson)
        {
            m_openPickers?.ShowValue(m_controller.Value);
            if (isFromPerson)
            {
                this.SendActionForControlEvents(UIControlEvent.ValueChanged);
            }
        }

        void IDateTimeEditHost.ShowTextState ()
        {
            // Called from the controller's setters, which can run before Setup has built the views
            if (m_warningGlyph == null)
            {
                return;
            }

            m_warningGlyph.Hidden = m_controller.IsTextValid;
            m_warningGlyph.AccessibilityLabel = m_controller.InvalidTextReason;
            m_warningToolTip.DefaultToolTip = m_controller.InvalidTextReason;
            this.AccessibilityHint = m_controller.CurrentToolTip;
            this.SetNeedsLayout();
        }

        void IDateTimeEditHost.RaiseUserEditComplete (DateTime? oldValue, DateTime? newValue)
        {
            this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(oldValue, newValue));
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

        private void OnWhatIsEditedChanged ()
        {
            // Called from the setters, which can run before Setup has built the button
            if (m_pickerButton == null)
            {
                return;
            }

            m_pickerButton.SetNeedsUpdateConfiguration();
            this.ApplyAccessibility();
        }

        private void ApplyPlaceholder ()
        {
            m_textField.AttributedPlaceholder = new NSAttributedString(
                m_unsetTextPrompt ?? String.Empty,
                foregroundColor: ThemedColors.Get(MobileLook.DateTimeEditIngressControl.PlaceholderText)
            );
        }

        private void ApplyAccessibility ()
        {
            bool showsDate = m_controller.ShowsDate;
            bool showsTime = m_controller.ShowsTime;
            string whatIsEdited = (showsDate && showsTime) ? "Date and time" : (showsDate ? "Date" : "Time");
            m_textField.AccessibilityLabel = String.IsNullOrEmpty(m_unsetTextPrompt) ? whatIsEdited : m_unsetTextPrompt;
            m_pickerButton.AccessibilityLabel = showsDate ? "Pick a date" : "Pick a time";
        }

        // NSDatePicker and UIDatePicker have no setting for their clock: they draw the hour as their locale does. So a
        //  picker keeps the current locale when it is on the clock wanted already, and otherwise takes one that is: en_US
        //  for twelve hours, en_GB for twenty four. That locale writes the AM and PM in English, the one cost of choosing a
        //  clock the person's own locale doesn't use. NSDateFormatter's "j" template is a locale's own hour, which carries
        //  an "a" (the AM or PM) on a twelve hour clock.
        private static NSLocale GetPickerLocale (bool usesTwelveHourClock)
        {
            const string kLocalesOwnHour = "j";
            const string kTwelveHourLocale = "en_US";
            const string kTwentyFourHourLocale = "en_GB";
            NSLocale current = NSLocale.CurrentLocale;
            string localesHour = NSDateFormatter.GetDateFormatFromTemplate(kLocalesOwnHour, 0, current) ?? String.Empty;
            if (localesHour.Contains('a') == usesTwelveHourClock)
            {
                return current;
            }

            return new NSLocale(usesTwelveHourClock ? kTwelveHourLocale : kTwentyFourHourLocale);
        }

        // The font as it is, with its italics turned on or off, so a font set on TextField is kept
        private static UIFont WithItalics (UIFont font, bool isItalic)
        {
            UIFontDescriptorSymbolicTraits traits = font.FontDescriptor.SymbolicTraits;
            traits = isItalic ? (traits | UIFontDescriptorSymbolicTraits.Italic) : (traits & ~UIFontDescriptorSymbolicTraits.Italic);
            UIFontDescriptor changed = font.FontDescriptor.CreateWithTraits(traits);
            return (changed != null) ? UIFont.FromDescriptor(changed, 0) : font;
        }

        private void OnDatePicked (DateTime pickedDate)
        {
            if (m_controller.PickDate(pickedDate))
            {
                this.ClosePickers();
            }
        }

        private void OnTimePicked (TimeSpan pickedTimeOfDay) => m_controller.PickTimeOfDay(pickedTimeOfDay);

        // However the pickers went: a pick, a tap outside, a swipe, or the control closing them
        private void OnPickersClosed ()
        {
            PickerController pickers = m_openPickers;
            if (pickers == null)
            {
                return;
            }

            m_openPickers = null;
            pickers.Release();
            m_pickerButton.SetNeedsUpdateConfiguration();
            m_controller.EndPickerEdit();
        }

        // ===========[ Subclasses/structs ]===================================
        // The picker button: the frame's surface at rest, the highlight under a mouse, trackpad or Pencil, and the pressed
        //  fill while pressed and while its pickers are open, with its glyph lit on those fills
        private sealed class PickerButton : UIButton
        {
            private readonly WeakReference<DateTimeEditIngressControl> m_owner;

            public PickerButton (DateTimeEditIngressControl owner) : base(CGRect.Empty)
            {
                m_owner = new WeakReference<DateTimeEditIngressControl>(owner);
                this.Configuration = UIButtonConfiguration.PlainButtonConfiguration;
                this.TranslatesAutoresizingMaskIntoConstraints = false;
                this.WidthAnchor.ConstraintEqualTo(MobileLook.DateTimeEditIngressControl.PickerButtonWidth).Active = true;
                this.TouchUpInside += this.OnTouchUpInside;
            }

            public override void UpdateConfiguration ()
            {
                // UIKit asks for this on every state change; base.UpdateConfiguration is skipped so the system lays no
                //  dimmed highlight of its own over it, as NumericEditor's step buttons do
                bool isOpen = m_owner.TryGetTarget(out DateTimeEditIngressControl owner) && owner.ShowEditDisplay;
                UIColor fill = UIColor.Clear;
                string foregroundKey = MobileLook.DateTimeEditIngressControl.ButtonGlyph;
                if (this.Highlighted || isOpen)
                {
                    fill = ThemedColors.Get(MobileLook.DateTimeEditIngressControl.ButtonPressedFill) ?? this.TintColor;
                    foregroundKey = MobileLook.DateTimeEditIngressControl.ButtonHighlightGlyph;
                }
                else if (this.Hovered)
                {
                    fill = ThemedColors.Get(MobileLook.DateTimeEditIngressControl.ButtonPointerOverFill) ?? this.TintColor;
                    foregroundKey = MobileLook.DateTimeEditIngressControl.ButtonHighlightGlyph;
                }

                UIImage image = owner?.m_pickerButtonContent;
                if ((image == null) && (owner != null))
                {
                    image = UIImage.GetSystemImage(owner.m_controller.ShowsDate ? "calendar" : "clock");
                }

                UIButtonConfiguration configuration = this.Configuration ?? UIButtonConfiguration.PlainButtonConfiguration;
                configuration.Image = image;
                UIBackgroundConfiguration background = configuration.Background;
                background.BackgroundColor = fill;
                background.CornerRadius = 0;
                configuration.Background = background;
                configuration.BaseForegroundColor = ThemedColors.Get(foregroundKey);
                this.Configuration = configuration;
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing)
                {
                    this.TouchUpInside -= this.OnTouchUpInside;
                }

                base.Dispose(disposing);
            }

            private void OnTouchUpInside (object sender, EventArgs e)
            {
                if (m_owner.TryGetTarget(out DateTimeEditIngressControl owner))
                {
                    owner.OpenPickers();
                }
            }
        }

        // One open picker edit: the view controller presented, its date pickers, and the popover's delegate, made when the
        //  pickers open and released when they close. Everything in it holds the control weakly, since UIKit keeps a
        //  presented controller and its delegate alive while it shows.
        private sealed class PickerController : UIViewController
        {
            private readonly WeakReference<DateTimeEditIngressControl> m_owner;
            private readonly UIStackView m_stack = new UIStackView
            {
                Axis = UILayoutConstraintAxis.Vertical,
                Alignment = UIStackViewAlignment.Center,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };
            private readonly UILabel m_readOnlyNote = new UILabel { Text = "Read-only" };
            private readonly UIDatePicker m_calendar;
            private readonly UIDatePicker m_timePicker;
            private bool m_isSyncing;
            private bool m_isReleased;

            public PickerController (DateTimeEditIngressControl owner, bool showsDate, bool showsTime, bool isReadOnly, bool usesTwelveHourClock) : base(null, null)
            {
                // Shown over the pickers while the control is read-only, when they open only to show the value
                const float kReadOnlyNoteAlpha = 0.7f;
                m_owner = new WeakReference<DateTimeEditIngressControl>(owner);
                this.DismissWatcher = new PopoverDismissWatcher(owner);
                m_stack.Spacing = MobileLook.DateTimeEditIngressControl.PickerSpacing;
                m_readOnlyNote.Font = WithItalics(UIFont.PreferredSubheadline, true);
                m_readOnlyNote.Alpha = kReadOnlyNoteAlpha;
                m_stack.AddArrangedSubview(m_readOnlyNote);
                if (showsDate)
                {
                    m_calendar = new UIDatePicker { Mode = UIDatePickerMode.Date, PreferredDatePickerStyle = UIDatePickerStyle.Inline };
                    m_calendar.ValueChanged += this.OnCalendarChanged;
                    m_stack.AddArrangedSubview(m_calendar);
                }

                if (showsTime)
                {
                    m_timePicker = new UIDatePicker
                    {
                        Mode = UIDatePickerMode.Time,
                        PreferredDatePickerStyle = UIDatePickerStyle.Wheels,
                        Locale = GetPickerLocale(usesTwelveHourClock),
                    };
                    m_timePicker.ValueChanged += this.OnTimeChanged;
                    m_stack.AddArrangedSubview(m_timePicker);
                }

                this.ShowReadOnly(isReadOnly);
            }

            /// <summary>The popover's delegate, which hears a dismiss a person made (a tap outside, a swipe down)</summary>
            public PopoverDismissWatcher DismissWatcher { get; }

            public override void ViewDidLoad ()
            {
                base.ViewDidLoad();
                const float kPadding = 12f;

                this.View.BackgroundColor = UIColor.SystemBackground;
                this.View.AddSubview(m_stack);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    m_stack.LeadingAnchor.ConstraintEqualTo(this.View.SafeAreaLayoutGuide.LeadingAnchor, kPadding),
                    m_stack.TrailingAnchor.ConstraintEqualTo(this.View.SafeAreaLayoutGuide.TrailingAnchor, -kPadding),
                    m_stack.TopAnchor.ConstraintEqualTo(this.View.SafeAreaLayoutGuide.TopAnchor, kPadding),
                });

                CGSize fitting = m_stack.SystemLayoutSizeFittingSize(UIView.UILayoutFittingCompressedSize);
                this.PreferredContentSize = new CGSize(fitting.Width + (2 * kPadding), fitting.Height + (2 * kPadding));
            }

            /// <summary>Brings the pickers to <paramref name="value"/> (now, for no value) without reporting back</summary>
            public void ShowValue (DateTime? value)
            {
                DateTime shown = value ?? DateTime.Now;
                m_isSyncing = true;
                try
                {
                    if (m_calendar != null)
                    {
                        m_calendar.Date = (NSDate)DateTime.SpecifyKind(shown.Date.AddHours(kMiddayHour), DateTimeKind.Local);
                    }

                    if (m_timePicker != null)
                    {
                        m_timePicker.Date = (NSDate)DateTime.SpecifyKind(shown, DateTimeKind.Local);
                    }
                }
                finally
                {
                    m_isSyncing = false;
                }
            }

            /// <summary>
            /// Marks the pickers read-only, or not. The time wheels are simply off. An inline calendar that is off stops
            /// paging through the months, so it stays on, and a date tapped is put straight back (OnCalendarChanged).
            /// </summary>
            public void ShowReadOnly (bool isReadOnly)
            {
                m_readOnlyNote.Hidden = !isReadOnly;
                if (m_timePicker != null)
                {
                    m_timePicker.Enabled = !isReadOnly;
                }
            }

            /// <summary>Ends the edit's subscriptions, so a controller UIKit still holds while it animates away leads back to nothing</summary>
            public void Release ()
            {
                if (m_isReleased)
                {
                    return;
                }

                m_isReleased = true;
                if (m_calendar != null)
                {
                    m_calendar.ValueChanged -= this.OnCalendarChanged;
                }

                if (m_timePicker != null)
                {
                    m_timePicker.ValueChanged -= this.OnTimeChanged;
                }

                this.DismissWatcher.Release();
            }

            private void OnCalendarChanged (object sender, EventArgs e)
            {
                if (m_isSyncing || m_isReleased || !m_owner.TryGetTarget(out DateTimeEditIngressControl owner))
                {
                    return;
                }

                if (owner.IsReadOnly)
                {
                    this.ShowValue(owner.EditDateTime);
                    return;
                }

                owner.OnDatePicked(((DateTime)m_calendar.Date).ToLocalTime().Date);
            }

            private void OnTimeChanged (object sender, EventArgs e)
            {
                if (!m_isSyncing && !m_isReleased && m_owner.TryGetTarget(out DateTimeEditIngressControl owner))
                {
                    DateTime picked = ((DateTime)m_timePicker.Date).ToLocalTime();
                    owner.OnTimePicked(new TimeSpan(picked.Hour, picked.Minute, 0));
                }
            }
        }

        // Hears a dismiss a person made, from the popover on iPad or the sheet UIKit makes of it on iPhone. UIKit tells the
        //  delegate only about those, so a dismiss from code ends the edit itself (ClosePickers).
        private sealed class PopoverDismissWatcher : UIPopoverPresentationControllerDelegate
        {
            private WeakReference<DateTimeEditIngressControl> m_owner;

            public PopoverDismissWatcher (DateTimeEditIngressControl owner)
            {
                m_owner = new WeakReference<DateTimeEditIngressControl>(owner);
            }

            public void Release () => m_owner = null;

            public override void DidDismiss (UIPresentationController presentationController)
            {
                if ((m_owner != null) && m_owner.TryGetTarget(out DateTimeEditIngressControl owner))
                {
                    owner.OnPickersClosed();
                }
            }
        }
    }
}
