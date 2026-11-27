namespace AJut.UX.Controls
{
    using System;
    using AJut.DateAndTime;
    using AJut.UX.Theming;
    using AppKit;
    using CoreAnimation;
    using CoreGraphics;
    using Foundation;
    using ObjCRuntime;

    // ===========[ DateTimeEditIngressControl ]=================================
    // A date and time field with a picker button, the AppKit port of WinUI3's and WPF's DateTimeEditIngressControl. At rest
    // the field shows the value, in DisplayFormat or the culture's own short form. Typed text is read by TextResolver
    // (DateTimeTextResolver when there is none), so "in two weeks", "friday" and "+3d" are as good as a written date. The
    // button opens a popover holding the Mac's own NSDatePicker: a graphical calendar, a time field with a stepper, or
    // both, as IncludeDate and IncludeTime ask.
    //
    // What typed text comes to, what a picked date or time does to the value, and when an edit is complete are AJut.UX's
    // DateTimeEditController, which the UIKit and Android controls run too, over the DateTimeEditRules every stack calls.
    // This class shows the result and hosts the pickers.
    //
    // The look is PathSelectionControl's, which is WinUI3's: a bordered row (thicker along the bottom, rounded at the right
    // end) holding the text in a well that lights up while focused, the warning glyph while the text can't be read, then
    // the picker button. The border is the accent while focus is inside, and the invalid color while the text can't be
    // read, which wins.
    //
    // Editing:
    //  - Every keystroke reads the text again, for the invalid look and the tooltip, and changes nothing yet.
    //  - Return, or leaving the field, makes valid text the value; text that can't be read stays, to be fixed.
    //  - Escape puts the value back.
    //  - The picker button settles anything typed, then opens the popover on the value. A pick changes the value right
    //    away; with only a date to pick, picking it closes the popover. UserEditComplete fires when it closes, if the
    //    value changed.
    //
    // The text field's edits are read through one NSTextFieldDelegate, as PathSelectionControl's are, since Escape and
    // Return reach a text field only through its delegate (doCommandBySelector).
    //
    // Lifetime: the field, its delegate, the button, the date pickers' handler and the popover's delegate all hold the
    //  control weakly, since the control retains them.
    /// <summary>
    /// A date and time field that takes free-form typed text ("tomorrow", "in two weeks", "friday 3pm", a written date)
    /// and has a button that opens the Mac's date and time pickers. It judges typed text as it is typed and shows text it
    /// can't read with a red border, a warning glyph and the reason as its tooltip.
    /// </summary>
    public class DateTimeEditIngressControl : ThemedView, IUserEditNotifier, IDateTimeEditHost
    {
        private const double kButtonWidth = 26.0;
        private const double kCornerRadius = 5.0;
        private const double kBorderThickness = 1.0;
        private const double kWarningGlyphWidth = 22.0;
        private const double kFontSize = 12.0;
        private const double kTextBoxHeight = 22.0;
        private const double kTextPadding = 7.0;
        private const double kPopoverPadding = 8.0;

        // What NSDatePicker is handed and hands back is a moment, which it places on a day by its own time zone. A date goes
        //  in at the middle of its day, so no zone or daylight saving shift can move it onto the day before or after.
        private const int kMiddayHour = 12;

        private readonly DateTimeEditController m_controller;
        private readonly DateFrame m_frame;
        private readonly ThemedView m_bottomEdge;
        private readonly FillStack m_row;
        private readonly DateWell m_well;
        private readonly DateTextField m_field;
        private readonly DateFieldDelegate m_fieldDelegate;
        private readonly NSImageView m_warningGlyph;
        private readonly PickerButton m_pickerButton;
        private readonly NSDatePicker m_calendar;
        private readonly NSDatePicker m_timePicker;
        private readonly NSTextField m_readOnlyNote;
        private readonly DatePickerHandler m_pickerHandler;
        private readonly NSPopover m_popover;
        private readonly PopoverDelegate m_popoverDelegate;

        private string m_unsetTextPrompt;
        private NSImage m_pickerButtonContent;
        private bool m_isFocused;
        private bool m_isSyncingPickers;

        // ===========[ Setup/Construction/Teardown ]===========================
        public DateTimeEditIngressControl ()
        {
            m_controller = new DateTimeEditController(this);

            // 1. The frame and its thicker bottom edge
            m_frame = new DateFrame
            {
                BackgroundColorKey = ThemedColorKeys.DateTimeEdit_Background,
                BorderColorKey = ThemedColorKeys.DateTimeEdit_NormalBorder,
                BorderThickness = kBorderThickness,
                CornerRadius = kCornerRadius,
            };

            m_bottomEdge = new ThemedView
            {
                BackgroundColorKey = ThemedColorKeys.DateTimeEdit_NormalBorder,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };

            // 2. The field, borderless in its well
            m_field = new DateTextField(this)
            {
                Bordered = false,
                DrawsBackground = false,
                FocusRingType = NSFocusRingType.None,
                Font = NSFont.SystemFontOfSize((nfloat)kFontSize),
                TextColor = ThemedColors.Get(ThemedColorKeys.DateTimeEdit_Text),
                UsesSingleLineMode = true,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };
            m_field.Cell.Wraps = false;
            m_field.Cell.Scrollable = true;
            m_field.Cell.SetSendsActionOnEndEditing(false);
            m_fieldDelegate = new DateFieldDelegate(this);
            m_field.Delegate = m_fieldDelegate;

            m_well = new DateWell();
            m_well.AddSubview(m_field);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_field.LeadingAnchor.ConstraintEqualTo(m_well.LeadingAnchor, (nfloat)(DateWell.kMargin + kTextPadding)),
                m_field.TrailingAnchor.ConstraintEqualTo(m_well.TrailingAnchor, (nfloat)(-(DateWell.kMargin + kTextPadding))),
                m_field.CenterYAnchor.ConstraintEqualTo(m_well.CenterYAnchor),
                m_well.HeightAnchor.ConstraintEqualTo((nfloat)(kTextBoxHeight + (2.0 * DateWell.kMargin))),
            });

            // 3. The warning glyph, in the row only while the text can't be read (see ShowTextState)
            m_warningGlyph = new NSImageView
            {
                Image = NSImage.GetSystemSymbol("exclamationmark.triangle.fill", null),
                ContentTintColor = ThemedColors.Get(ThemedColorKeys.DateTimeEdit_WarningIconForeground),
            };
            m_warningGlyph.TranslatesAutoresizingMaskIntoConstraints = false;
            m_warningGlyph.WidthAnchor.ConstraintEqualTo((nfloat)kWarningGlyphWidth).Active = true;

            // 4. The picker button, which rounds the right end
            m_pickerButton = new PickerButton(this);
            m_pickerButton.TranslatesAutoresizingMaskIntoConstraints = false;
            m_pickerButton.WidthAnchor.ConstraintEqualTo((nfloat)kButtonWidth).Active = true;

            // 5. Layout: the row inside the frame's border, and above its thicker bottom edge
            m_row = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal);
            m_row.Add(m_well, grows: true);
            m_row.Add(m_pickerButton);
            m_row.TranslatesAutoresizingMaskIntoConstraints = false;
            m_frame.AddSubview(m_row);
            m_frame.AddSubview(m_bottomEdge);
            this.AddSubview(m_frame);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_row.TopAnchor.ConstraintEqualTo(m_frame.TopAnchor, (nfloat)kBorderThickness),
                m_row.LeadingAnchor.ConstraintEqualTo(m_frame.LeadingAnchor, (nfloat)kBorderThickness),
                m_row.TrailingAnchor.ConstraintEqualTo(m_frame.TrailingAnchor, (nfloat)(-kBorderThickness)),
                m_row.BottomAnchor.ConstraintEqualTo(m_bottomEdge.TopAnchor),
                m_bottomEdge.HeightAnchor.ConstraintEqualTo((nfloat)kBorderThickness),
                m_bottomEdge.BottomAnchor.ConstraintEqualTo(m_frame.BottomAnchor, (nfloat)(-kBorderThickness)),
                m_bottomEdge.LeadingAnchor.ConstraintEqualTo(m_frame.LeadingAnchor),
                m_bottomEdge.TrailingAnchor.ConstraintEqualTo(m_frame.TrailingAnchor, (nfloat)(-kCornerRadius)),
                m_frame.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_frame.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_frame.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_frame.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
            });

            // 6. The pickers, in a popover made once and reused across opens: the Mac's graphical calendar, and a time
            //    field with a stepper. Only the ones what is edited asks for are shown; a stack view leaves a hidden one out.
            m_calendar = new NSDatePicker
            {
                DatePickerStyle = NSDatePickerStyle.ClockAndCalendar,
                DatePickerElements = NSDatePickerElementFlags.YearMonthDateDay,
                Bezeled = false,
                DrawsBackground = false,
            };
            m_timePicker = new NSDatePicker
            {
                DatePickerStyle = NSDatePickerStyle.TextFieldAndStepper,
                DatePickerElements = NSDatePickerElementFlags.HourMinute,
            };
            m_pickerHandler = new DatePickerHandler(this);
            m_calendar.Activated += m_pickerHandler.OnCalendarActivated;
            m_timePicker.Activated += m_pickerHandler.OnTimePickerActivated;

            // Shown over the pickers while the control is read-only, when they open only to show the value
            const double kReadOnlyNoteAlpha = 0.7;
            m_readOnlyNote = NSTextField.CreateLabel("Read-only");
            m_readOnlyNote.Font = NSFontManager.SharedFontManager.ConvertFont(NSFont.SystemFontOfSize((nfloat)kFontSize), NSFontTraitMask.Italic);
            m_readOnlyNote.AlphaValue = (nfloat)kReadOnlyNoteAlpha;
            m_readOnlyNote.Hidden = true;

            NSStackView pickers = NSStackView.FromViews(new NSView[] { m_readOnlyNote, m_calendar, m_timePicker });
            pickers.Orientation = NSUserInterfaceLayoutOrientation.Vertical;
            pickers.Alignment = NSLayoutAttribute.CenterX;
            pickers.Spacing = (nfloat)MobileLook.DateTimeEditIngressControl.PickerSpacing;
            pickers.EdgeInsets = new NSEdgeInsets((nfloat)kPopoverPadding, (nfloat)kPopoverPadding, (nfloat)kPopoverPadding, (nfloat)kPopoverPadding);

            m_popoverDelegate = new PopoverDelegate(this);
            m_popover = new NSPopover
            {
                Behavior = NSPopoverBehavior.Transient,
                ContentViewController = new NSViewController { View = pickers },
                Delegate = m_popoverDelegate,
            };

            this.ApplyPlaceholder();
            this.ApplyAccessibilityLabel();
            m_controller.ShowValueAsText();
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native control whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls: the native control keeps the subviews it already has.
        /// None of its managed state survives a dispose, so a call that needs that state can still fail: dispose it only
        /// once AppKit is done with it.
        /// </summary>
        protected DateTimeEditIngressControl (NativeHandle handle) : base(handle)
        {
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing && (m_controller != null))
            {
                m_field.Delegate = null;
                m_calendar.Activated -= m_pickerHandler.OnCalendarActivated;
                m_timePicker.Activated -= m_pickerHandler.OnTimePickerActivated;
                m_popover.Delegate = null;
                if (m_popover.Shown)
                {
                    m_popover.Close();
                }
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        /// <summary>Raised whenever <see cref="EditDateTime"/> changes: from typing, a picker, or code. Bind two-way through this.</summary>
        public event EventHandler<EventArgs> EditDateTimeChanged;

        /// <summary>
        /// Raised once a person has changed the value: when typed text is taken, and when the picker popover closes on a
        /// different value than it opened on. Values set from code don't raise it. Old and new values are DateTime?.
        /// </summary>
        public event EventHandler<UserEditAppliedEventArgs> UserEditComplete;

        // ===========[ Properties ]============================================
        /// <summary>
        /// The date and time being edited, or null for none. A date only control keeps it at midnight when it sets it, and
        /// a time only control leaves its date as it was.
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
        /// picker either way.
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
                const double kReadOnlyTextAlpha = 0.85;
                m_controller.IsReadOnly = value;
                m_field.Editable = !value;
                NSFont upright = NSFont.SystemFontOfSize((nfloat)kFontSize);
                m_field.Font = value ? NSFontManager.SharedFontManager.ConvertFont(upright, NSFontTraitMask.Italic) : upright;
                m_field.AlphaValue = value ? (nfloat)kReadOnlyTextAlpha : (nfloat)1.0;
                this.ApplyPickerReadOnly();
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
                this.ApplyAccessibilityLabel();
            }
        }

        /// <summary>
        /// The image the picker button shows, drawn as a template in the button's glyph color. With none, it is the calendar
        /// symbol, or the clock symbol for a time only control.
        /// </summary>
        public NSImage PickerButtonContent
        {
            get => m_pickerButtonContent;
            set
            {
                m_pickerButtonContent = value;
                m_pickerButton.NeedsDisplay = true;
            }
        }

        /// <summary>Whether the text in the field can be read as a value</summary>
        public bool IsTextValid => m_controller.IsTextValid;

        /// <summary>Why the text in the field can't be read, or null when it can</summary>
        public string InvalidTextReason => m_controller.InvalidTextReason;

        /// <summary>The control's tooltip: what typed text will come to, or why it can't be read</summary>
        public string CurrentToolTip => m_controller.CurrentToolTip;

        /// <summary>True while the picker popover is open</summary>
        public bool ShowEditDisplay => m_popover.Shown;

        /// <summary>The text field inside, for anything not passed through here (font, for one)</summary>
        public NSTextField TextField => m_field;

        // ===========[ Public Interface Methods ]==============================
        // A control taken off screen (its page navigated away) closes its popover, which completes a picker edit in progress
        public override void ViewWillMoveToWindow (NSWindow newWindow)
        {
            if ((newWindow == null) && (m_popover != null) && m_popover.Shown)
            {
                m_popover.Close();
            }

            base.ViewWillMoveToWindow(newWindow);
        }

        // ===========[ IDateTimeEditHost Implementations ]=====================
        // Writing the text a field already shows loses the caret, so only a difference is written. While the field is being
        //  edited its text lives in the window's field editor, so it is written through that as an edit, which registers it
        //  with the editor's undo, as PathSelectionControl does.
        void IDateTimeEditHost.ShowText (string text)
        {
            if ((m_field == null) || (m_field.StringValue == text))
            {
                return;
            }

            NSText editor = m_field.CurrentEditor;
            if (editor == null)
            {
                m_field.StringValue = text;
                return;
            }

            var whole = new NSRange(0, editor.Value?.Length ?? 0);
            if ((editor is NSTextView textView) && textView.ShouldChangeText(whole, text))
            {
                textView.Replace(whole, text);
                textView.DidChangeText();
            }
            else
            {
                editor.Value = text;
            }

            editor.SelectedRange = new NSRange(text.Length, 0);
        }

        void IDateTimeEditHost.OnValueChanged (bool isFromPerson)
        {
            this.EditDateTimeChanged?.Invoke(this, EventArgs.Empty);
        }

        void IDateTimeEditHost.ShowTextState ()
        {
            if (m_frame == null)
            {
                return;
            }

            string toolTip = m_controller.CurrentToolTip;
            m_frame.ToolTip = toolTip;
            m_field.ToolTip = toolTip;
            m_warningGlyph.ToolTip = m_controller.InvalidTextReason;
            if (m_warningGlyph.Image != null)
            {
                m_warningGlyph.Image.AccessibilityDescription = m_controller.InvalidTextReason;
            }

            this.RefreshVisualState();
        }

        void IDateTimeEditHost.RaiseUserEditComplete (DateTime? oldValue, DateTime? newValue)
        {
            this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(oldValue, newValue));
        }

        // ===========[ Helper Methods ]========================================
        // The invalid border wins over the focused one; the well's focused look follows focus alone, so text that can't be
        //  read shows its border around it rather than taking it away mid-edit
        private void RefreshVisualState ()
        {
            bool isShowingInvalid = !m_controller.IsTextValid;
            string borderKey = isShowingInvalid
                ? ThemedColorKeys.DateTimeEdit_InvalidBorder
                : (m_isFocused ? ThemedColorKeys.DateTimeEdit_FocusedBorder : ThemedColorKeys.DateTimeEdit_NormalBorder);
            m_frame.BorderColorKey = borderKey;
            m_bottomEdge.BackgroundColorKey = borderKey;
            m_well.IsFocused = m_isFocused;

            // A FillStack keeps a hidden child's space, so the glyph goes into the row only while it applies, between the
            //  well and the picker button
            bool isInRow = m_warningGlyph.Superview == m_row;
            if (isShowingInvalid && !isInRow)
            {
                m_row.Insert(1, m_warningGlyph, alignment: eFillStackCrossAlignment.Center);
            }
            else if (!isShowingInvalid && isInRow)
            {
                m_row.Remove(m_warningGlyph);
            }
        }

        private void SetFocused (bool isFocused)
        {
            if (m_isFocused != isFocused)
            {
                m_isFocused = isFocused;
                this.RefreshVisualState();
            }
        }

        private void OnWhatIsEditedChanged ()
        {
            this.ApplyAccessibilityLabel();
            m_pickerButton.NeedsDisplay = true;
        }

        private void ApplyPlaceholder ()
        {
            m_field.PlaceholderAttributedString = new NSAttributedString(
                m_unsetTextPrompt ?? String.Empty,
                new NSStringAttributes
                {
                    ForegroundColor = ThemedColors.Get(ThemedColorKeys.DateTimeEdit_PlaceholderText),
                    Font = m_field.Font,
                }
            );
        }

        private void ApplyAccessibilityLabel ()
        {
            if (!String.IsNullOrEmpty(m_unsetTextPrompt))
            {
                m_field.AccessibilityLabel = m_unsetTextPrompt;
                return;
            }

            bool showsDate = m_controller.ShowsDate;
            bool showsTime = m_controller.ShowsTime;
            m_field.AccessibilityLabel = (showsDate && showsTime) ? "Date and time" : (showsDate ? "Date" : "Time");
        }

        private void OpenPickers ()
        {
            if (m_popover.Shown || !m_controller.BeginPickerEdit())
            {
                return;
            }

            m_calendar.Hidden = !m_controller.ShowsDate;
            m_timePicker.Hidden = !m_controller.ShowsTime;
            this.ApplyPickerReadOnly();
            m_timePicker.Locale = GetPickerLocale(m_controller.UsesTwelveHourClock);
            this.SyncPickersToValue();

            NSView content = m_popover.ContentViewController.View;
            content.LayoutSubtreeIfNeeded();
            m_popover.ContentSize = content.FittingSize;
            m_popover.Show(m_pickerButton.Bounds, m_pickerButton, NSRectEdge.MaxYEdge);
            m_pickerButton.NeedsDisplay = true;
        }

        // Read-only, the pickers open to show the value. The time field is simply off. A graphical NSDatePicker that is off
        //  greys out and stops paging through the months, so the calendar stays on, and a date clicked is put straight
        //  back (OnCalendarPicked).
        private void ApplyPickerReadOnly ()
        {
            m_readOnlyNote.Hidden = !m_controller.IsReadOnly;
            m_timePicker.Enabled = !m_controller.IsReadOnly;
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

        private void SyncPickersToValue ()
        {
            m_isSyncingPickers = true;
            try
            {
                DateTime shown = m_controller.Value ?? DateTime.Now;
                m_calendar.DateValue = (NSDate)DateTime.SpecifyKind(shown.Date.AddHours(kMiddayHour), DateTimeKind.Local);
                m_timePicker.DateValue = (NSDate)DateTime.SpecifyKind(shown, DateTimeKind.Local);
            }
            finally
            {
                m_isSyncingPickers = false;
            }
        }

        private void OnCalendarPicked ()
        {
            if (m_isSyncingPickers)
            {
                return;
            }

            if (m_controller.IsReadOnly)
            {
                this.SyncPickersToValue();
                return;
            }

            DateTime picked = ((DateTime)m_calendar.DateValue).ToLocalTime();
            if (m_controller.PickDate(picked.Date))
            {
                m_popover.Close();
            }
        }

        private void OnTimePicked ()
        {
            if (m_isSyncingPickers)
            {
                return;
            }

            DateTime picked = ((DateTime)m_timePicker.DateValue).ToLocalTime();
            m_controller.PickTimeOfDay(new TimeSpan(picked.Hour, picked.Minute, 0));
        }

        private void OnPopoverClosed ()
        {
            m_pickerButton.NeedsDisplay = true;
            m_controller.EndPickerEdit();
        }

        // ===========[ Subclasses/structs ]====================================
        // The bordered frame: rounded at its right end only, as WinUI3's is, where the picker button sits
        private sealed class DateFrame : ThemedView
        {
            public DateFrame ()
            {
                this.TranslatesAutoresizingMaskIntoConstraints = false;
            }

            private DateFrame (NativeHandle handle) : base(handle)
            {
            }

            protected override void ApplyLayerColors (CALayer layer)
            {
                base.ApplyLayerColors(layer);
                layer.MaskedCorners = CACornerMask.MaxXMinYCorner | CACornerMask.MaxXMaxYCorner;
            }
        }

        // The well the text sits in, inset from the row: while focused it fills, with a dark edge along its top and left and
        //  a light one along its bottom and right, WinUI3's rim lighting
        private sealed class DateWell : NSView
        {
            public const double kMargin = 3.0;
            private const double kRimThickness = 1.0;

            private bool m_isFocused;

            public DateWell ()
            {
                this.TranslatesAutoresizingMaskIntoConstraints = false;
            }

            private DateWell (NativeHandle handle) : base(handle)
            {
            }

            public override bool IsFlipped => true;

            public bool IsFocused
            {
                get => m_isFocused;
                set
                {
                    if (m_isFocused != value)
                    {
                        m_isFocused = value;
                        this.NeedsDisplay = true;
                    }
                }
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                if (!m_isFocused)
                {
                    return;
                }

                CGRect well = this.Bounds.Inset((nfloat)kMargin, (nfloat)kMargin);
                ThemedColors.Get(ThemedColorKeys.DateTimeEdit_FocusedWellBackground).SetFill();
                NSBezierPath.FillRect(well);

                // The view is flipped, so the top edge is at the well's lowest y
                ThemedColors.Get(ThemedColorKeys.DateTimeEdit_FocusRimDark).SetFill();
                NSBezierPath.FillRect(new CGRect(well.X, well.Y, well.Width, (nfloat)kRimThickness));
                NSBezierPath.FillRect(new CGRect(well.X, well.Y, (nfloat)kRimThickness, well.Height));

                ThemedColors.Get(ThemedColorKeys.DateTimeEdit_FocusRimBright).SetFill();
                NSBezierPath.FillRect(new CGRect(well.X, well.Bottom - (nfloat)kRimThickness, well.Width, (nfloat)kRimThickness));
                NSBezierPath.FillRect(new CGRect(well.Right - (nfloat)kRimThickness, well.Y, (nfloat)kRimThickness, well.Height));
            }

            public override void ViewDidChangeEffectiveAppearance ()
            {
                base.ViewDidChangeEffectiveAppearance();
                this.NeedsDisplay = true;
            }
        }

        // Knows when it takes focus. Losing focus shows up as the end of editing, in DateFieldDelegate.
        private sealed class DateTextField : NSTextField
        {
            private readonly WeakReference<DateTimeEditIngressControl> m_owner;

            public DateTextField (DateTimeEditIngressControl owner)
            {
                m_owner = new WeakReference<DateTimeEditIngressControl>(owner);
            }

            private DateTextField (NativeHandle handle) : base(handle)
            {
            }

            public override bool BecomeFirstResponder ()
            {
                bool became = base.BecomeFirstResponder();
                if (became && (m_owner != null) && m_owner.TryGetTarget(out DateTimeEditIngressControl owner))
                {
                    owner.SetFocused(true);
                }

                return became;
            }
        }

        // The field's one delegate: keystrokes, the end of editing, Return and Escape
        private sealed class DateFieldDelegate : NSTextFieldDelegate
        {
            private readonly WeakReference<DateTimeEditIngressControl> m_owner;

            public DateFieldDelegate (DateTimeEditIngressControl owner)
            {
                m_owner = new WeakReference<DateTimeEditIngressControl>(owner);
            }

            private DateFieldDelegate (NativeHandle handle) : base(handle)
            {
            }

            public override void Changed (NSNotification notification)
            {
                if (this.TryGetOwner(out DateTimeEditIngressControl owner))
                {
                    owner.m_controller.OnTextEdited(owner.m_field.StringValue);
                }
            }

            public override void EditingEnded (NSNotification notification)
            {
                if (this.TryGetOwner(out DateTimeEditIngressControl owner))
                {
                    owner.m_controller.CommitText();
                    owner.SetFocused(false);
                }
            }

            public override bool DoCommandBySelector (NSControl control, NSTextView textView, Selector commandSelector)
            {
                if (!this.TryGetOwner(out DateTimeEditIngressControl owner))
                {
                    return false;
                }

                switch (commandSelector.Name)
                {
                    // Return keeps the field focused and takes the text, as leaving it does
                    case "insertNewline:":
                        owner.m_controller.CommitText();
                        return true;

                    case "cancelOperation:":
                        owner.m_controller.ShowValueAsText();
                        return true;

                    default:
                        return false;
                }
            }

            private bool TryGetOwner (out DateTimeEditIngressControl owner)
            {
                owner = null;
                return (m_owner != null) && m_owner.TryGetTarget(out owner);
            }
        }

        // Opens the pickers: no fill at rest, so it reads as part of the control, then the highlight under the pointer, while
        //  pressed and while its popover is open, with the glyph lit on it. It rounds the control's right end.
        private sealed class PickerButton : ManagedButton
        {
            // Inside the frame's 1 point border, so 1 under its corners and the fill meets the border's curve
            private const double kEndCornerRadius = 4.0;
            private const double kGlyphSize = 14.0;

            private readonly WeakReference<DateTimeEditIngressControl> m_owner;

            public PickerButton (DateTimeEditIngressControl owner)
            {
                m_owner = new WeakReference<DateTimeEditIngressControl>(owner);
                this.Activated += this.OnActivated;
            }

            private PickerButton (NativeHandle handle) : base(handle)
            {
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing && (m_owner != null))
                {
                    this.Activated -= this.OnActivated;
                }

                base.Dispose(disposing);
            }

            // While the popover is open the button stays lit, as a pressed button does
            public override eManagedButtonState CurrentState
            {
                get
                {
                    eManagedButtonState state = base.CurrentState;
                    if ((state != eManagedButtonState.Disabled) && this.TryGetOwner(out DateTimeEditIngressControl owner) && owner.ShowEditDisplay)
                    {
                        return eManagedButtonState.Pressed;
                    }

                    return state;
                }
            }

            protected override string DefaultAccessibilityLabel
            {
                get
                {
                    if (this.TryGetOwner(out DateTimeEditIngressControl owner) && !owner.m_controller.ShowsDate)
                    {
                        return "Pick a time";
                    }

                    return "Pick a date";
                }
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                if (!this.TryGetOwner(out DateTimeEditIngressControl owner))
                {
                    return;
                }

                eManagedButtonState state = this.CurrentState;
                string backgroundKey = BackgroundKeyFor(state);
                if (backgroundKey != null)
                {
                    ThemedColors.Get(backgroundKey).SetFill();
                    RightRoundedRect(this.Bounds, kEndCornerRadius).Fill();
                }

                NSImage glyph = owner.m_pickerButtonContent
                    ?? NSImage.GetSystemSymbol(owner.m_controller.ShowsDate ? "calendar" : "clock", null);
                if (glyph == null)
                {
                    return;
                }

                var glyphRect = new CGRect(
                    ((double)this.Bounds.Width - kGlyphSize) / 2.0,
                    ((double)this.Bounds.Height - kGlyphSize) / 2.0,
                    kGlyphSize,
                    kGlyphSize
                );
                DrawTinted(glyph, glyphRect, ThemedColors.Get(ForegroundKeyFor(state)));
            }

            private void OnActivated (object sender, EventArgs e)
            {
                if (this.TryGetOwner(out DateTimeEditIngressControl owner))
                {
                    owner.OpenPickers();
                }
            }

            private bool TryGetOwner (out DateTimeEditIngressControl owner)
            {
                owner = null;
                return (m_owner != null) && m_owner.TryGetTarget(out owner);
            }

            // Draws the image as a template, in one color. The tint is laid over the image's own pixels only (source atop)
            //  inside a transparency layer of its own, which starts empty, so the button's fill under the glyph keeps its
            //  color; without the layer, source atop would tint the fill as well.
            private static void DrawTinted (NSImage image, CGRect rect, NSColor color)
            {
                CGContext context = NSGraphicsContext.CurrentContext.CGContext;
                context.SaveState();
                context.BeginTransparencyLayer();
                image.Draw(rect, CGRect.Empty, NSCompositingOperation.SourceOver, (nfloat)1.0, true, null);
                color.SetFill();
                NSGraphics.RectFill(rect, NSCompositingOperation.SourceAtop);
                context.EndTransparencyLayer();
                context.RestoreState();
            }

            private static NSBezierPath RightRoundedRect (CGRect rect, double radius)
            {
                var path = new NSBezierPath();
                path.MoveTo(new CGPoint(rect.Left, rect.Top));
                path.LineTo(new CGPoint(rect.Right - (nfloat)radius, rect.Top));
                path.CurveTo(new CGPoint(rect.Right, rect.Top + (nfloat)radius), new CGPoint(rect.Right, rect.Top), new CGPoint(rect.Right, rect.Top));
                path.LineTo(new CGPoint(rect.Right, rect.Bottom - (nfloat)radius));
                path.CurveTo(new CGPoint(rect.Right - (nfloat)radius, rect.Bottom), new CGPoint(rect.Right, rect.Bottom), new CGPoint(rect.Right, rect.Bottom));
                path.LineTo(new CGPoint(rect.Left, rect.Bottom));
                path.ClosePath();
                return path;
            }

            private static string BackgroundKeyFor (eManagedButtonState state)
            {
                switch (state)
                {
                    case eManagedButtonState.PointerOver: return ThemedColorKeys.DateTimeEdit_ButtonPointerOverBackground;
                    case eManagedButtonState.Pressed: return ThemedColorKeys.DateTimeEdit_ButtonPressedBackground;
                    default: return null;
                }
            }

            private static string ForegroundKeyFor (eManagedButtonState state)
            {
                switch (state)
                {
                    case eManagedButtonState.PointerOver:
                    case eManagedButtonState.Pressed:
                        return ThemedColorKeys.DateTimeEdit_ButtonHighlightForeground;

                    case eManagedButtonState.Disabled:
                        return ThemedColorKeys.DateTimeEdit_DisabledText;

                    default:
                        return ThemedColorKeys.DateTimeEdit_ButtonIconForeground;
                }
            }
        }

        // Hears the date pickers' actions. NSControl holds its action target weakly, so this holds the control weakly too,
        //  and the control holds this, which keeps it alive as long as the pickers it listens to.
        private sealed class DatePickerHandler
        {
            private readonly WeakReference<DateTimeEditIngressControl> m_owner;

            public DatePickerHandler (DateTimeEditIngressControl owner)
            {
                m_owner = new WeakReference<DateTimeEditIngressControl>(owner);
            }

            public void OnCalendarActivated (object sender, EventArgs e)
            {
                if (m_owner.TryGetTarget(out DateTimeEditIngressControl owner))
                {
                    owner.OnCalendarPicked();
                }
            }

            public void OnTimePickerActivated (object sender, EventArgs e)
            {
                if (m_owner.TryGetTarget(out DateTimeEditIngressControl owner))
                {
                    owner.OnTimePicked();
                }
            }
        }

        // Hears the popover close, however it closed: a pick, a click outside it, Escape, or the control
        private sealed class PopoverDelegate : NSPopoverDelegate
        {
            private readonly WeakReference<DateTimeEditIngressControl> m_owner;

            public PopoverDelegate (DateTimeEditIngressControl owner)
            {
                m_owner = new WeakReference<DateTimeEditIngressControl>(owner);
            }

            private PopoverDelegate (NativeHandle handle) : base(handle)
            {
            }

            public override void DidClose (NSNotification notification)
            {
                if ((m_owner != null) && m_owner.TryGetTarget(out DateTimeEditIngressControl owner))
                {
                    owner.OnPopoverClosed();
                }
            }
        }
    }
}
