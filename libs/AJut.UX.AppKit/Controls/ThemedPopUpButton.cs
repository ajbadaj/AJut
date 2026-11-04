namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using Foundation;
    using ObjCRuntime;

    // ===========[ ThemedPopUpButton ]==========================================
    // A pop-up button drawn as WinUI3's ComboBox, from the stock keys its template reads (ComboBoxBackground,
    // ComboBoxBorderBrush, ComboBoxForeground and ComboBoxDropDownGlyphForeground, per state), so it themes with
    // everything else: unthemed in macOS's own colors, themed in the AJut palette. The closed button is AJut's;
    // the menu it opens is macOS's own, drawn by the system, as AppKit's menus always are.
    //
    // As with ThemedTextField, AppKit offers no way to restyle a stock NSPopUpButton from outside it, so the look
    // is in this button's cell: a rounded box with a 1 point border, the selected item's title padded in from the
    // left, and a chevron at the right in place of macOS's arrows. States, as WinUI3's: rest, pointer over, pressed
    // (while its menu is open), focused (keyboard focus on it, with Full Keyboard Access on) and disabled.
    public class ThemedPopUpButton : NSPopUpButton
    {
        private NSTrackingArea m_trackingArea;

        // ===========[ Setup/Construction/Teardown ]===========================
        public ThemedPopUpButton ()
            : this(CGRect.Empty, false)
        {
        }

        /// <summary>A pop-up button (<paramref name="pullsDown"/> false) or a pull-down one, as NSPopUpButton's own constructor</summary>
        public ThemedPopUpButton (CGRect frame, bool pullsDown)
            : base(frame, pullsDown)
        {
            // The cell goes in first: a new cell starts from its own defaults, so everything else is set on it after
            this.Cell = new ThemedPopUpButtonCell(pullsDown);
            this.FocusRingType = NSFocusRingType.None;
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native button whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls: the native button keeps its cell and items. None of
        /// its managed state survives a dispose, so a call that needs that state can still fail: dispose it only once
        /// AppKit is done with it.
        /// </summary>
        protected ThemedPopUpButton (NativeHandle handle) : base(handle)
        {
        }

        // ===========[ Properties ]============================================
        /// <summary>
        /// Wide enough for the widest item's title with the title padding and the chevron column. NSPopUpButton works its
        /// intrinsic size out without asking its cell's CellSize, so the cell's sizes alone left titles cut short.
        /// </summary>
        public override CGSize IntrinsicContentSize
        {
            get
            {
                CGSize size = base.IntrinsicContentSize;
                return (this.Cell is ThemedPopUpButtonCell cell) ? cell.WidenToTitles(size) : size;
            }
        }

        /// <summary>Off draws the button dimmed, in the disabled colors, as a disabled ComboBox does</summary>
        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;
                this.NeedsDisplay = true;
            }
        }

        // ===========[ Public Interface Methods ]==============================
        public override void UpdateTrackingAreas ()
        {
            base.UpdateTrackingAreas();
            if (m_trackingArea != null)
            {
                this.RemoveTrackingArea(m_trackingArea);
            }

            m_trackingArea = new NSTrackingArea(
                CGRect.Empty,
                NSTrackingAreaOptions.MouseEnteredAndExited | NSTrackingAreaOptions.ActiveInActiveApp | NSTrackingAreaOptions.InVisibleRect,
                this,
                null
            );
            this.AddTrackingArea(m_trackingArea);
        }

        public override void MouseEntered (NSEvent theEvent)
        {
            base.MouseEntered(theEvent);
            this.SetPointerOver(true);
        }

        public override void MouseExited (NSEvent theEvent)
        {
            base.MouseExited(theEvent);
            this.SetPointerOver(false);
        }

        public override void ViewDidMoveToWindow ()
        {
            base.ViewDidMoveToWindow();
            if (this.Window == null)
            {
                this.SetPointerOver(false);
            }
        }

        public override void ViewDidChangeEffectiveAppearance ()
        {
            base.ViewDidChangeEffectiveAppearance();
            this.NeedsDisplay = true;
        }

        public override bool BecomeFirstResponder ()
        {
            bool became = base.BecomeFirstResponder();
            this.NeedsDisplay = true;
            return became;
        }

        public override bool ResignFirstResponder ()
        {
            bool resigned = base.ResignFirstResponder();
            this.NeedsDisplay = true;
            return resigned;
        }

        // ===========[ Helper Methods ]========================================
        private void SetPointerOver (bool isPointerOver)
        {
            if ((this.Cell is ThemedPopUpButtonCell cell) && (cell.IsPointerOver != isPointerOver))
            {
                cell.IsPointerOver = isPointerOver;
                this.NeedsDisplay = true;
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // The button's cell: it draws the box and chevron where macOS draws its bezel, recolors the title for the state,
        // and pads the title the way WinUI3's ComboBox pads its content (12 in from the left, with room for the chevron
        // column on the right). AppKit highlights the cell while its menu is open, which is the pressed look.
        private sealed class ThemedPopUpButtonCell : NSPopUpButtonCell
        {
            private const double kCornerRadius = 4.0;
            private const double kBorderThickness = 1.0;
            private const double kTitlePaddingLeft = 12.0;
            private const double kChevronColumnWidth = 30.0;
            private const double kChevronHalfWidth = 4.0;
            private const double kChevronHalfHeight = 2.0;
            private const double kChevronLineWidth = 1.25;

            public ThemedPopUpButtonCell (bool pullsDown)
                : base(String.Empty, pullsDown)
            {
                this.ArrowPosition = NSPopUpArrowPosition.None;
                this.Bordered = true;
                this.Alignment = NSTextAlignment.Left;
            }

            // For a peer the runtime makes over an existing native cell
            private ThemedPopUpButtonCell (NativeHandle handle) : base(handle)
            {
            }

            /// <summary>Set by the button as the pointer comes and goes, since a cell gets no mouse events of its own</summary>
            public bool IsPointerOver { get; set; }

            public override void DrawBezelWithFrame (CGRect frame, NSView controlView)
            {
                eState state = this.StateFor(controlView);
                var halfBorder = (nfloat)(kBorderThickness / 2.0);
                var corner = (nfloat)kCornerRadius;
                bool isFlipped = controlView?.IsFlipped ?? true;
                NSBezierPath box = NSBezierPath.FromRoundedRect(frame.Inset(halfBorder, halfBorder), corner, corner);

                // Themed, ComboBoxBackground (and its focused key) is the pop-out surface, a gradient, as WinUI3's ComboBox
                // has it. A gradient runs top to bottom at 90 degrees in a flipped view, and the other way up in one that isn't.
                string backgroundKey = BackgroundKeyFor(state);
                NSGradient gradient = ThemedColors.GetThemedGradient(backgroundKey, controlView?.EffectiveAppearance);
                if (gradient != null)
                {
                    gradient.DrawInBezierPath(box, (nfloat)(isFlipped ? 90.0 : -90.0));
                }
                else
                {
                    ThemedColors.Get(backgroundKey).SetFill();
                    box.Fill();
                }

                ThemedColors.Get(BorderKeyFor(state)).SetStroke();
                box.LineWidth = (nfloat)kBorderThickness;
                box.Stroke();

                // The chevron, centered in its column at the right, pointing down, as WinUI3's glyph
                double centerX = (double)frame.GetMaxX() - (kChevronColumnWidth / 2.0);
                double centerY = (double)frame.GetMidY();
                double tipOffset = isFlipped ? kChevronHalfHeight : -kChevronHalfHeight;
                var chevron = new NSBezierPath
                {
                    LineWidth = (nfloat)kChevronLineWidth,
                    LineCapStyle = NSLineCapStyle.Round,
                    LineJoinStyle = NSLineJoinStyle.Round,
                };
                chevron.MoveTo(new CGPoint(centerX - kChevronHalfWidth, centerY - tipOffset));
                chevron.LineTo(new CGPoint(centerX, centerY + tipOffset));
                chevron.LineTo(new CGPoint(centerX + kChevronHalfWidth, centerY - tipOffset));
                ThemedColors.Get(GlyphKeyFor(state)).SetStroke();
                chevron.Stroke();
            }

            // AppKit hands this the title already centered in the button (the frame is the centered text's), whatever the
            // cell's alignment, so the frame is laid out again here: from the title padding to the chevron column, the
            // title left aligned in it and cut with an ellipsis if it runs long
            public override CGRect DrawTitle (NSAttributedString title, CGRect frame, NSView controlView)
            {
                if (title == null)
                {
                    return base.DrawTitle(title, frame, controlView);
                }

                CGRect titleFrame = frame;
                if (controlView != null)
                {
                    CGRect bounds = controlView.Bounds;
                    titleFrame.X = bounds.X + (nfloat)kTitlePaddingLeft;
                    nfloat width = bounds.Width - (nfloat)(kTitlePaddingLeft + kChevronColumnWidth);
                    titleFrame.Width = (width > 0) ? width : 0;
                }

                var paragraph = new NSMutableParagraphStyle
                {
                    Alignment = NSTextAlignment.Left,
                    LineBreakMode = NSLineBreakMode.TruncatingTail,
                };

                var recolored = new NSMutableAttributedString(title);
                var whole = new NSRange(0, recolored.Length);
                recolored.AddAttribute(NSStringAttributeKey.ForegroundColor, ThemedColors.Get(ForegroundKeyFor(this.StateFor(controlView))), whole);
                recolored.AddAttribute(NSStringAttributeKey.ParagraphStyle, paragraph, whole);
                return base.DrawTitle(recolored, titleFrame, controlView);
            }

            public override CGRect TitleRectForBounds (CGRect theRect)
            {
                CGRect rect = base.TitleRectForBounds(theRect);
                nfloat left = theRect.X + (nfloat)kTitlePaddingLeft;
                nfloat right = theRect.GetMaxX() - (nfloat)kChevronColumnWidth;
                rect.X = left;
                rect.Width = (right > left) ? right - left : 0;
                return rect;
            }

            // With its arrows off, macOS sizes the cell for its widest item's title and its own small margins only, so the
            // title was cut short at the button's own size. Both of AppKit's size questions (CellSize for the intrinsic
            // size, CellSizeForBounds for a given rect) get at least the widest title plus the title padding and the chevron
            // column. Neither adds to the other's answer, since one can be worked out from the other.
            public override CGSize CellSize => this.WidenToTitles(base.CellSize);

            public override CGSize CellSizeForBounds (CGRect bounds) => this.WidenToTitles(base.CellSizeForBounds(bounds));

            /// <summary>At least as wide as the widest item's title plus the title padding and the chevron column</summary>
            public CGSize WidenToTitles (CGSize size)
            {
                const double kTrailingSlack = 2.0;

                double widestTitle = 0.0;
                NSFont font = this.Font ?? NSFont.SystemFontOfSize(NSFont.SystemFontSize);
                NSMenu menu = this.Menu;
                if (menu != null)
                {
                    foreach (NSMenuItem item in menu.Items)
                    {
                        if (!String.IsNullOrEmpty(item.Title))
                        {
                            var measured = new NSAttributedString(item.Title, new NSStringAttributes { Font = font });
                            widestTitle = Math.Max(widestTitle, Math.Ceiling((double)measured.Size.Width));
                        }
                    }
                }

                double needed = widestTitle + kTitlePaddingLeft + kChevronColumnWidth + kTrailingSlack;
                return new CGSize((nfloat)Math.Max((double)size.Width, needed), size.Height);
            }

            private eState StateFor (NSView controlView)
            {
                if (!this.Enabled)
                {
                    return eState.Disabled;
                }

                if (this.Highlighted)
                {
                    return eState.Pressed;
                }

                if (this.IsPointerOver)
                {
                    return eState.PointerOver;
                }

                bool isFocused = (controlView?.Window != null) && (controlView.Window.FirstResponder == controlView);
                return isFocused ? eState.Focused : eState.Rest;
            }

            private static string BackgroundKeyFor (eState state)
            {
                switch (state)
                {
                    case eState.Disabled: return ThemedColorKeys.ComboBox_BackgroundDisabled;
                    case eState.Pressed: return ThemedColorKeys.ComboBox_BackgroundPressed;
                    case eState.PointerOver: return ThemedColorKeys.ComboBox_BackgroundPointerOver;
                    case eState.Focused: return ThemedColorKeys.ComboBox_BackgroundFocused;
                    default: return ThemedColorKeys.ComboBox_Background;
                }
            }

            private static string BorderKeyFor (eState state)
            {
                switch (state)
                {
                    case eState.Disabled: return ThemedColorKeys.ComboBox_BorderDisabled;
                    case eState.Pressed: return ThemedColorKeys.ComboBox_BorderPressed;
                    case eState.PointerOver: return ThemedColorKeys.ComboBox_BorderPointerOver;
                    case eState.Focused: return ThemedColorKeys.ComboBox_BorderFocused;
                    default: return ThemedColorKeys.ComboBox_Border;
                }
            }

            private static string ForegroundKeyFor (eState state)
            {
                switch (state)
                {
                    case eState.Disabled: return ThemedColorKeys.ComboBox_ForegroundDisabled;
                    case eState.Pressed: return ThemedColorKeys.ComboBox_ForegroundPressed;
                    case eState.PointerOver: return ThemedColorKeys.ComboBox_ForegroundPointerOver;
                    case eState.Focused: return ThemedColorKeys.ComboBox_ForegroundFocused;
                    default: return ThemedColorKeys.ComboBox_Foreground;
                }
            }

            private static string GlyphKeyFor (eState state)
            {
                switch (state)
                {
                    case eState.Disabled: return ThemedColorKeys.ComboBox_GlyphDisabled;
                    case eState.Focused: return ThemedColorKeys.ComboBox_GlyphFocused;
                    default: return ThemedColorKeys.ComboBox_Glyph;
                }
            }

            private enum eState
            {
                Rest,
                PointerOver,
                Pressed,
                Focused,
                Disabled,
            }
        }
    }
}
