namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using Foundation;
    using ObjCRuntime;

    // ===========[ ThemedCheckBox ]=============================================
    // A check box drawn as WinUI3's CheckBox, from the stock keys its template reads (CheckBoxCheckBackgroundFill,
    // CheckBoxCheckBackgroundStroke, CheckBoxCheckGlyphForeground and CheckBoxForeground, for each of unchecked,
    // checked and indeterminate at rest, pointer over, pressed and disabled), so it themes with everything else:
    // unthemed in macOS's own colors, themed in the AJut palette, its checked fill the palette's accent rather than
    // the one in System Settings.
    //
    // It is a stock switch button (NSButtonType.Switch) with its own cell: the cell draws the box and check where
    // macOS draws its check box image, and recolors the title. Everything else, clicking, the keyboard, the mixed
    // state and VoiceOver, is the stock button's. Set AllowsMixedState for a three-state box, as on any NSButton.
    public class ThemedCheckBox : NSButton
    {
        private NSTrackingArea m_trackingArea;

        // ===========[ Setup/Construction/Teardown ]===========================
        public ThemedCheckBox ()
        {
            // The cell goes in first: a new cell starts from its own defaults, so everything else is set on it after
            this.Cell = new ThemedCheckBoxCell();
            this.SetButtonType(NSButtonType.Switch);
            this.FocusRingType = NSFocusRingType.None;
        }

        /// <summary>A check box with <paramref name="title"/> beside it</summary>
        public ThemedCheckBox (string title)
            : this()
        {
            this.Title = title ?? String.Empty;
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native button whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls: the native button keeps its cell. None of its managed
        /// state survives a dispose, so a call that needs that state can still fail: dispose it only once AppKit is done
        /// with it.
        /// </summary>
        protected ThemedCheckBox (NativeHandle handle) : base(handle)
        {
        }

        // ===========[ Properties ]============================================
        /// <summary>Off draws the box dimmed, in the disabled colors, as a disabled CheckBox does</summary>
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

        // ===========[ Helper Methods ]========================================
        private void SetPointerOver (bool isPointerOver)
        {
            if ((this.Cell is ThemedCheckBoxCell cell) && (cell.IsPointerOver != isPointerOver))
            {
                cell.IsPointerOver = isPointerOver;
                this.NeedsDisplay = true;
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // The button's cell. WinUI3's box is 20 square with 4 point corners beside 14 point text; beside the Mac's 13
        // point text it is 16 square with 3 point corners, centered where macOS puts its own check box image. The check is
        // a stroked tick and indeterminate a bar, both in the glyph color. AppKit highlights the cell while the mouse is
        // down in it, which is the pressed look.
        private sealed class ThemedCheckBoxCell : NSButtonCell
        {
            private const double kBoxSize = 16.0;
            private const double kCornerRadius = 3.0;
            private const double kBorderThickness = 1.0;
            private const double kGlyphLineWidth = 1.75;

            public ThemedCheckBoxCell ()
            {
            }

            // For a peer the runtime makes over an existing native cell
            private ThemedCheckBoxCell (NativeHandle handle) : base(handle)
            {
            }

            /// <summary>Set by the button as the pointer comes and goes, since a cell gets no mouse events of its own</summary>
            public bool IsPointerOver { get; set; }

            public override void DrawImage (NSImage image, CGRect frame, NSView controlView)
            {
                // 1. The box, centered where macOS would have drawn its image
                var size = (nfloat)kBoxSize;
                var halfBorder = (nfloat)(kBorderThickness / 2.0);
                var corner = (nfloat)kCornerRadius;
                var boxRect = new CGRect(frame.GetMidX() - (size / 2), frame.GetMidY() - (size / 2), size, size);
                NSBezierPath box = NSBezierPath.FromRoundedRect(boxRect.Inset(halfBorder, halfBorder), corner, corner);

                ThemedColorKeys.eCheckBoxCheck check = this.CheckFor();
                ThemedColorKeys.eCheckBoxInteraction interaction = this.InteractionFor();
                ThemedColors.Get(ThemedColorKeys.CheckBoxKey(ThemedColorKeys.CheckBox_BoxFillPart, check, interaction)).SetFill();
                box.Fill();
                ThemedColors.Get(ThemedColorKeys.CheckBoxKey(ThemedColorKeys.CheckBox_BoxStrokePart, check, interaction)).SetStroke();
                box.LineWidth = (nfloat)kBorderThickness;
                box.Stroke();

                // 2. The tick or the bar. Points are laid out top down, and flipped here for an unflipped view.
                if (check == ThemedColorKeys.eCheckBoxCheck.Unchecked)
                {
                    return;
                }

                bool isFlipped = controlView?.IsFlipped ?? true;
                double left = (double)boxRect.X;
                double top = (double)boxRect.Y;
                double extent = (double)boxRect.Height;
                CGPoint At (double x, double y) => new CGPoint(left + (x * extent), isFlipped ? top + (y * extent) : top + ((1.0 - y) * extent));

                var glyph = new NSBezierPath
                {
                    LineWidth = (nfloat)kGlyphLineWidth,
                    LineCapStyle = NSLineCapStyle.Round,
                    LineJoinStyle = NSLineJoinStyle.Round,
                };

                if (check == ThemedColorKeys.eCheckBoxCheck.Checked)
                {
                    glyph.MoveTo(At(0.26, 0.52));
                    glyph.LineTo(At(0.43, 0.69));
                    glyph.LineTo(At(0.75, 0.33));
                }
                else
                {
                    glyph.MoveTo(At(0.28, 0.5));
                    glyph.LineTo(At(0.72, 0.5));
                }

                ThemedColors.Get(ThemedColorKeys.CheckBoxKey(ThemedColorKeys.CheckBox_GlyphPart, check, interaction)).SetStroke();
                glyph.Stroke();
            }

            public override CGRect DrawTitle (NSAttributedString title, CGRect frame, NSView controlView)
            {
                if (title == null)
                {
                    return base.DrawTitle(title, frame, controlView);
                }

                var recolored = new NSMutableAttributedString(title);
                string colorKey = ThemedColorKeys.CheckBoxKey(ThemedColorKeys.CheckBox_ForegroundPart, this.CheckFor(), this.InteractionFor());
                recolored.AddAttribute(NSStringAttributeKey.ForegroundColor, ThemedColors.Get(colorKey), new NSRange(0, recolored.Length));
                return base.DrawTitle(recolored, frame, controlView);
            }

            private ThemedColorKeys.eCheckBoxCheck CheckFor ()
            {
                switch (this.State)
                {
                    case NSCellStateValue.On: return ThemedColorKeys.eCheckBoxCheck.Checked;
                    case NSCellStateValue.Mixed: return ThemedColorKeys.eCheckBoxCheck.Indeterminate;
                    default: return ThemedColorKeys.eCheckBoxCheck.Unchecked;
                }
            }

            private ThemedColorKeys.eCheckBoxInteraction InteractionFor ()
            {
                if (!this.Enabled)
                {
                    return ThemedColorKeys.eCheckBoxInteraction.Disabled;
                }

                if (this.Highlighted)
                {
                    return ThemedColorKeys.eCheckBoxInteraction.Pressed;
                }

                return this.IsPointerOver ? ThemedColorKeys.eCheckBoxInteraction.PointerOver : ThemedColorKeys.eCheckBoxInteraction.Rest;
            }
        }
    }
}
