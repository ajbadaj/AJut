namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using Foundation;
    using ObjCRuntime;

    // ===========[ ThemedTextField ]============================================
    // A text box drawn the AJut way, from the stock keys WinUI3's TextBox reads (TextControlBackground,
    // TextControlBorderBrush and the rest, per state), so it themes with everything else: unthemed it is
    // drawn in macOS's own colors, themed it follows the AJut palette, and restyling the palette (its
    // primary highlight, say) reaches its focused border the way it does on WinUI3.
    //
    // AppKit has no style system that restyles a stock NSTextField from outside it, so the look lives in
    // this field's cell, which draws the box, border and padding itself and takes no bezel or focus ring
    // from AppKit. Use it in place of an editable NSTextField; a plain label stays an NSTextField.
    //
    // States, as WinUI3's: rest, pointer over, focused (while being edited: the border in the focused
    // color, 2 points thick along the bottom, as WinUI3's focused thickness of 1,1,1,2 draws it), and
    // disabled. A read-only field (Editable false, still enabled) looks the same as one at rest, the way a
    // read-only TextBox does; turn Enabled off for the dimmed look.
    //
    // While the field is being edited its text is drawn by the window's field editor, an NSTextView laid
    // over the field. The cell hands the field editor the field's padded rect and the themed caret and
    // selection colors each time editing starts (SetUpFieldEditorAttributes), since that one text view is
    // shared by every field in the window.
    public class ThemedTextField : NSTextField
    {
        private const double kCornerRadius = 4.0;
        private const double kBorderThickness = 1.0;
        private const double kFocusedBottomThickness = 2.0;

        private NSTrackingArea m_trackingArea;
        private bool m_isPointerOver;
        private string m_placeholderText;

        // ===========[ Setup/Construction/Teardown ]===========================
        public ThemedTextField ()
        {
            // The cell goes in first: a new cell starts from its own defaults, so everything else is set on it after
            this.Cell = new ThemedTextFieldCell();
            this.Editable = true;
            this.Selectable = true;
            this.Bezeled = false;
            this.Bordered = false;
            this.DrawsBackground = false;
            this.FocusRingType = NSFocusRingType.None;
            this.Font = NSFont.SystemFontOfSize(NSFont.SystemFontSize);
            this.TextColor = ThemedColors.Get(ThemedColorKeys.TextControl_Foreground);
            this.Cell.Scrollable = true;
            this.Cell.Wraps = false;
            this.UsesSingleLineMode = true;
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native field whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls: the native field keeps its cell. None of its managed
        /// state survives a dispose, so a call that needs that state can still fail: dispose it only once AppKit is done
        /// with it.
        /// </summary>
        protected ThemedTextField (NativeHandle handle) : base(handle)
        {
        }

        // ===========[ Properties ]============================================
        /// <summary>Top-left origin, which is what every AJut layout assumes</summary>
        public override bool IsFlipped => true;

        /// <summary>Off draws the field dimmed, in the disabled colors, as a disabled TextBox does</summary>
        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;
                this.TextColor = ThemedColors.Get(value ? ThemedColorKeys.TextControl_Foreground : ThemedColorKeys.TextControl_ForegroundDisabled);
                this.ApplyPlaceholder();
                this.NeedsDisplay = true;
            }
        }

        /// <summary>
        /// Text shown while the field is empty, in the themed placeholder color. This is the field's placeholder: set it here
        /// rather than through PlaceholderString, which would draw in macOS's own placeholder color.
        /// </summary>
        public string PlaceholderText
        {
            get => m_placeholderText;
            set
            {
                m_placeholderText = value;
                this.ApplyPlaceholder();
            }
        }

        /// <summary>Whether the field's text is being edited now, which is when it shows as focused</summary>
        public bool IsEditing => this.CurrentEditor != null;

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

        // Editing starts here (the field hands itself over to the field editor), and the box redraws in its focused look.
        // Editing ending redraws the field anyway, since the cell draws the text again once the field editor leaves.
        public override bool BecomeFirstResponder ()
        {
            bool became = base.BecomeFirstResponder();
            this.NeedsDisplay = true;
            return became;
        }

        public override void DrawRect (CGRect dirtyRect)
        {
            this.DrawBox();
            base.DrawRect(dirtyRect);
        }

        // ===========[ Helper Methods ]========================================
        private void SetPointerOver (bool isPointerOver)
        {
            if (m_isPointerOver != isPointerOver)
            {
                m_isPointerOver = isPointerOver;
                this.NeedsDisplay = true;
            }
        }

        private void ApplyPlaceholder ()
        {
            if (m_placeholderText == null)
            {
                this.PlaceholderAttributedString = null;
                return;
            }

            string colorKey = this.Enabled ? ThemedColorKeys.TextControl_PlaceholderForeground : ThemedColorKeys.TextControl_PlaceholderForegroundDisabled;
            this.PlaceholderAttributedString = new NSAttributedString(m_placeholderText, new NSStringAttributes
            {
                Font = this.Font,
                ForegroundColor = ThemedColors.Get(colorKey),
            });
        }

        // The box, border and focused bottom edge, the way WinUI3's TextBox template draws its border element
        private void DrawBox ()
        {
            bool isFocused = this.IsEditing;
            string backgroundKey;
            string borderKey;
            if (!this.Enabled)
            {
                backgroundKey = ThemedColorKeys.TextControl_BackgroundDisabled;
                borderKey = ThemedColorKeys.TextControl_BorderDisabled;
            }
            else if (isFocused)
            {
                backgroundKey = ThemedColorKeys.TextControl_BackgroundFocused;
                borderKey = ThemedColorKeys.TextControl_BorderFocused;
            }
            else if (m_isPointerOver)
            {
                backgroundKey = ThemedColorKeys.TextControl_BackgroundPointerOver;
                borderKey = ThemedColorKeys.TextControl_BorderPointerOver;
            }
            else
            {
                backgroundKey = ThemedColorKeys.TextControl_Background;
                borderKey = ThemedColorKeys.TextControl_Border;
            }

            var halfBorder = (nfloat)(kBorderThickness / 2.0);
            var corner = (nfloat)kCornerRadius;
            CGRect bounds = this.Bounds;
            NSBezierPath box = NSBezierPath.FromRoundedRect(bounds.Inset(halfBorder, halfBorder), corner, corner);
            ThemedColors.Get(backgroundKey).SetFill();
            box.Fill();

            NSColor border = ThemedColors.Get(borderKey);
            border.SetStroke();
            box.LineWidth = (nfloat)kBorderThickness;
            box.Stroke();

            // Focused, the bottom edge thickens inward, clipped to the box so it follows the corners
            if (isFocused && this.Enabled)
            {
                NSGraphicsContext.CurrentContext.SaveGraphicsState();
                NSBezierPath.FromRoundedRect(bounds, corner, corner).AddClip();
                border.SetFill();
                NSBezierPath.FillRect(new CGRect(bounds.X, bounds.GetMaxY() - (nfloat)kFocusedBottomThickness, bounds.Width, (nfloat)kFocusedBottomThickness));
                NSGraphicsContext.CurrentContext.RestoreGraphicsState();
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // The field's cell: 6 in from either side (WinUI3's TextBox sets 10 on the left, which reads as too far in beside
        // the Mac's tighter text) and WinUI3's 5 over and 6 under, on the text when drawn and on the field editor while
        // editing, with the line centered when the box is taller than it. The field editor gets the themed caret and
        // selection colors as editing starts.
        private sealed class ThemedTextFieldCell : NSTextFieldCell
        {
            private const double kPaddingLeft = 6.0;
            private const double kPaddingRight = 6.0;
            private const double kPaddingTop = 5.0;
            private const double kPaddingBottom = 6.0;

            public ThemedTextFieldCell ()
            {
            }

            // For a peer the runtime makes over an existing native cell
            private ThemedTextFieldCell (NativeHandle handle) : base(handle)
            {
            }

            public override CGRect DrawingRectForBounds (CGRect theRect)
            {
                CGRect padded = base.DrawingRectForBounds(theRect);
                padded.X += (nfloat)kPaddingLeft;
                padded.Width -= (nfloat)(kPaddingLeft + kPaddingRight);

                // One line centers in the space between the top and bottom padding, the way a TextBox's content is
                // centered in its height; text that wraps starts under the top padding instead
                double lineHeight = this.LineHeight();
                double available = (double)padded.Height - kPaddingTop - kPaddingBottom;
                if (!this.Wraps && (lineHeight > 0.0) && (available > lineHeight))
                {
                    padded.Y += (nfloat)(kPaddingTop + Math.Floor((available - lineHeight) / 2.0));
                    padded.Height = (nfloat)lineHeight;
                }
                else
                {
                    padded.Y += (nfloat)kPaddingTop;
                    padded.Height -= (nfloat)(kPaddingTop + kPaddingBottom);
                }

                if (padded.Width < 0)
                {
                    padded.Width = 0;
                }

                if (padded.Height < 0)
                {
                    padded.Height = 0;
                }

                return padded;
            }

            public override CGSize CellSizeForBounds (CGRect bounds)
            {
                CGSize size = base.CellSizeForBounds(bounds);
                return new CGSize(size.Width + (nfloat)(kPaddingLeft + kPaddingRight), size.Height + (nfloat)(kPaddingTop + kPaddingBottom));
            }

            // The field editor is placed from the rect it is handed, so it gets the padded one, or it would sit on the box's
            // edge while editing and jump in again when editing ends
            public override void SelectWithFrame (CGRect aRect, NSView inView, NSText editor, NSObject delegateObject, nint selStart, nint selLength)
            {
                base.SelectWithFrame(this.DrawingRectForBounds(aRect), inView, editor, delegateObject, selStart, selLength);
            }

            public override void EditWithFrame (CGRect aRect, NSView inView, NSText editor, NSObject delegateObject, NSEvent theEvent)
            {
                base.EditWithFrame(this.DrawingRectForBounds(aRect), inView, editor, delegateObject, theEvent);
            }

            public override NSText SetUpFieldEditorAttributes (NSText textObj)
            {
                NSText editor = base.SetUpFieldEditorAttributes(textObj);
                if (editor is NSTextView textView)
                {
                    textView.InsertionPointColor = ThemedColors.Get(ThemedColorKeys.TextControl_ForegroundFocused);
                    textView.SelectedTextAttributes = new NSDictionary(
                        NSStringAttributeKey.BackgroundColor,
                        ThemedColors.Get(ThemedColorKeys.TextControl_SelectionHighlight)
                    );
                }

                return editor;
            }

            private double LineHeight ()
            {
                NSFont font = this.Font;
                return (font != null) ? Math.Ceiling((double)(font.Ascender - font.Descender + font.Leading)) : 0.0;
            }
        }
    }
}
