namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using ObjCRuntime;

    // ===========[ BusyWait ]===================================================
    // A busy indicator: the system's own spinner beside a line of text, the way WPF's BusyWait puts
    // its spinning glyph beside its content, and the same control the phones have. IsSpinning turns
    // it on and off, and SpinnerDock puts the spinner on any side of the text.
    //
    // As an overlay (IsOverlay) it is laid over what it covers, filling the same space. While
    // spinning it fills that space with the blockout fill and takes every click and scroll, so
    // nothing under it can be used, with the spinner and text in its middle; stopped, it draws
    // nothing and lets the mouse through. So an overlay can sit over a page for good and be switched
    // on while the page is busy, the way WPF's stack navigation shows its busy overlay. It blocks
    // the mouse only: keyboard focus can still reach what is under it, and a button under it can
    // still show its hover look, since AppKit follows the pointer by tracking area rather than by
    // what is on top.
    //
    // Stopped inline, the spinner hides but keeps its room, so the text does not move when it starts
    // again. Inline it never takes the mouse.
    //
    // The spinner is NSProgressIndicator's spinning style, which macOS draws in its own gray and has
    // no way to tint. It follows the view's light or dark appearance, which the AJut palette follows
    // too, so it sits with the text either way. As an overlay it is given the dark appearance, the
    // light spinner, to read on the dark blockout fill. It only turns while in a window.
    public class BusyWait : ThemedView
    {
        private readonly NSProgressIndicator m_spinner = new NSProgressIndicator
        {
            Style = NSProgressIndicatorStyle.Spinning,
            Indeterminate = true,
            IsDisplayedWhenStopped = false,
        };

        private readonly NSTextField m_label = NSTextField.CreateWrappingLabel(String.Empty);
        private string m_backgroundKeyBeforeOverlay;
        private bool m_isSpinning = true;
        private bool m_isOverlay;
        private bool m_isShowingOverlay;
        private double m_spinnerSize = MobileLook.BusyWait.SpinnerSize;
        private eBusyWaitSpinnerDock m_spinnerDock = eBusyWaitSpinnerDock.Right;

        // ===========[ Setup/Construction/Teardown ]===========================
        public BusyWait ()
        {
            // A wrapping label is selectable by default, which would put it in the key view loop
            m_label.Selectable = false;
            m_label.Alignment = NSTextAlignment.Center;
            m_label.Hidden = true;
            this.AddSubview(m_spinner);
            this.AddSubview(m_label);
            this.ApplyLook();
            this.ApplySpinning();
            this.Rearrange();
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native view whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls: the native view keeps the spinner and label it has.
        /// None of its managed state survives a dispose, so a call that needs that state can still fail: dispose it only
        /// once AppKit is done with it.
        /// </summary>
        protected BusyWait (NativeHandle handle) : base(handle)
        {
        }

        // ===========[ Properties ]============================================
        /// <summary>Whether the spinner is spinning. On by default, as on WPF.</summary>
        public bool IsSpinning
        {
            get => m_isSpinning;
            set
            {
                if (m_isSpinning != value)
                {
                    m_isSpinning = value;
                    this.ApplySpinning();
                }
            }
        }

        /// <summary>
        /// Whether this covers what is under it while spinning: the blockout fill, every click and scroll taken, and the
        /// spinner and text in the middle. Stopped, an overlay draws nothing and lets the mouse through. Off by default.
        /// </summary>
        public bool IsOverlay
        {
            get => m_isOverlay;
            set
            {
                if (m_isOverlay != value)
                {
                    m_isOverlay = value;
                    this.ApplyLook();
                    this.ApplySpinning();
                    this.Rearrange();
                }
            }
        }

        /// <summary>Which side of the text the spinner sits on. Right by default, as on WPF.</summary>
        public eBusyWaitSpinnerDock SpinnerDock
        {
            get => m_spinnerDock;
            set
            {
                if (m_spinnerDock != value)
                {
                    m_spinnerDock = value;
                    this.Rearrange();
                }
            }
        }

        /// <summary>The spinner's width and height, in points</summary>
        public double SpinnerSize
        {
            get => m_spinnerSize;
            set
            {
                if (m_spinnerSize != value)
                {
                    m_spinnerSize = value;
                    this.Rearrange();
                }
            }
        }

        /// <summary>The text beside the spinner. Null or empty shows the spinner alone.</summary>
        public string Text
        {
            get => m_label.StringValue;
            set
            {
                m_label.StringValue = value ?? String.Empty;
                this.ApplySpinning();
                this.Rearrange();
            }
        }

        /// <summary>The label, for styling the text the way the app styles its other text</summary>
        public NSTextField TextLabel => m_label;

        public override CGSize IntrinsicContentSize
        {
            get
            {
                CGSize content = this.MeasureContent(double.PositiveInfinity, out _, out _);
                double padding = 2.0 * this.Padding();
                return new CGSize((nfloat)(content.Width + padding), (nfloat)(content.Height + padding));
            }
        }

        private bool HasText => !String.IsNullOrEmpty(m_label.StringValue);

        private bool IsRow => (m_spinnerDock == eBusyWaitSpinnerDock.Left) || (m_spinnerDock == eBusyWaitSpinnerDock.Right);

        // ===========[ Public Interface Methods ]==============================
        // Left and Right lay the spinner and the text out in a row, Top and Bottom in a column, centered together in the
        //  middle and kept clear of the edges by the overlay's padding (none inline)
        public override void Layout ()
        {
            base.Layout();

            CGRect bounds = this.Bounds;
            double padding = this.Padding();
            CGSize content = this.MeasureContent((double)bounds.Width - (2.0 * padding), out CGSize labelSize, out double spacing);
            double left = ((double)bounds.Width - (double)content.Width) / 2.0;
            double top = ((double)bounds.Height - (double)content.Height) / 2.0;
            bool isSpinnerFirst = (m_spinnerDock == eBusyWaitSpinnerDock.Left) || (m_spinnerDock == eBusyWaitSpinnerDock.Top);

            CGPoint spinnerAt;
            CGPoint labelAt;
            if (this.IsRow)
            {
                double spinnerY = top + (((double)content.Height - m_spinnerSize) / 2.0);
                double labelY = top + (((double)content.Height - (double)labelSize.Height) / 2.0);
                spinnerAt = new CGPoint(isSpinnerFirst ? left : left + (double)labelSize.Width + spacing, spinnerY);
                labelAt = new CGPoint(isSpinnerFirst ? left + m_spinnerSize + spacing : left, labelY);
            }
            else
            {
                double spinnerX = left + (((double)content.Width - m_spinnerSize) / 2.0);
                double labelX = left + (((double)content.Width - (double)labelSize.Width) / 2.0);
                spinnerAt = new CGPoint(spinnerX, isSpinnerFirst ? top : top + (double)labelSize.Height + spacing);
                labelAt = new CGPoint(labelX, isSpinnerFirst ? top + m_spinnerSize + spacing : top);
            }

            m_spinner.Frame = new CGRect(spinnerAt, new CGSize((nfloat)m_spinnerSize, (nfloat)m_spinnerSize));
            m_label.Frame = new CGRect(labelAt, labelSize);
        }

        // A stopped overlay and an inline BusyWait let the mouse through to what is under them. A spinning overlay takes
        //  every point inside it for itself, so no click reaches anything under it.
        public override NSView HitTest (CGPoint aPoint)
        {
            if (!m_isShowingOverlay)
            {
                return null;
            }

            return (base.HitTest(aPoint) != null) ? this : null;
        }

        // Only a spinning overlay is hit (see HitTest), and it keeps each of these from whatever is under it and from the
        //  views above it in the responder chain, so nothing sees an up or a drag whose down it never saw, and a scroll
        //  does not scroll the page it covers
        public override void MouseDown (NSEvent theEvent)
        {
        }

        public override void MouseDragged (NSEvent theEvent)
        {
        }

        public override void MouseUp (NSEvent theEvent)
        {
        }

        public override void RightMouseDown (NSEvent theEvent)
        {
        }

        public override void RightMouseDragged (NSEvent theEvent)
        {
        }

        public override void RightMouseUp (NSEvent theEvent)
        {
        }

        public override void OtherMouseDown (NSEvent theEvent)
        {
        }

        public override void OtherMouseDragged (NSEvent theEvent)
        {
        }

        public override void OtherMouseUp (NSEvent theEvent)
        {
        }

        public override void ScrollWheel (NSEvent theEvent)
        {
            if (!m_isShowingOverlay)
            {
                base.ScrollWheel(theEvent);
            }
        }

        // A spinner turning out of a window has nobody to show it to, so it turns only while in one
        public override void ViewDidMoveToWindow ()
        {
            base.ViewDidMoveToWindow();
            this.ApplySpinning();
        }

        // ===========[ Helper Methods ]========================================
        private double Padding () => m_isOverlay ? MobileLook.BusyWait.OverlayPadding : 0.0;

        // The control size closest to the spinner's size (mini 10, small 16, regular 32), so it draws its own art for that
        //  size before any scaling to its frame
        private void Rearrange ()
        {
            const double kLargestMiniSize = 12.0;
            const double kLargestSmallSize = 24.0;

            m_spinner.ControlSize = (m_spinnerSize <= kLargestMiniSize)
                ? NSControlSize.Mini
                : ((m_spinnerSize <= kLargestSmallSize) ? NSControlSize.Small : NSControlSize.Regular);

            this.InvalidateIntrinsicContentSize();
            this.NeedsLayout = true;
        }

        // The size the spinner and text take together, the text wrapped to fit maxWidth (less the spinner in a row)
        private CGSize MeasureContent (double maxWidth, out CGSize labelSize, out double spacing)
        {
            // A width no text reaches, so the label measures on one line
            const double kUnboundedWidth = 100000.0;

            labelSize = CGSize.Empty;
            spacing = 0.0;
            if (this.HasText)
            {
                spacing = MobileLook.BusyWait.SpinnerSpacing;
                double labelMaxWidth = this.IsRow ? maxWidth - m_spinnerSize - spacing : maxWidth;
                if (double.IsInfinity(labelMaxWidth) || (labelMaxWidth <= 0.0))
                {
                    labelMaxWidth = kUnboundedWidth;
                }

                CGSize measured = m_label.Cell.CellSizeForBounds(new CGRect(0, 0, (nfloat)labelMaxWidth, (nfloat)kUnboundedWidth));
                labelSize = new CGSize((nfloat)Math.Ceiling((double)measured.Width), (nfloat)Math.Ceiling((double)measured.Height));
            }

            return this.IsRow
                ? new CGSize((nfloat)(m_spinnerSize + spacing + (double)labelSize.Width), (nfloat)Math.Max(m_spinnerSize, (double)labelSize.Height))
                : new CGSize((nfloat)Math.Max(m_spinnerSize, (double)labelSize.Width), (nfloat)(m_spinnerSize + spacing + (double)labelSize.Height));
        }

        private void ApplyLook ()
        {
            m_label.TextColor = ThemedColors.Get(m_isOverlay ? ThemedColorKeys.BusyWait_OverlayForeground : ThemedColorKeys.BusyWait_Foreground);
            m_spinner.Appearance = m_isOverlay ? NSAppearance.GetAppearance(NSAppearance.NameDarkAqua) : null;
        }

        // A spinning overlay fills itself and takes the mouse. The background key it replaced goes back when it stops, so a
        //  background an app gave an inline BusyWait stays.
        private void ApplySpinning ()
        {
            bool showsOverlay = m_isOverlay && m_isSpinning;
            if (showsOverlay && !m_isShowingOverlay)
            {
                m_backgroundKeyBeforeOverlay = this.BackgroundColorKey;
                this.BackgroundColorKey = ThemedColorKeys.BusyWait_OverlayBackground;
            }
            else if (!showsOverlay && m_isShowingOverlay)
            {
                this.BackgroundColorKey = m_backgroundKeyBeforeOverlay;
                m_backgroundKeyBeforeOverlay = null;
            }

            m_isShowingOverlay = showsOverlay;

            // Stopped, an overlay shows nothing; stopped inline, the spinner hides itself (it is not displayed when
            //  stopped) and keeps its frame, so the text stays put
            bool isContentShown = !m_isOverlay || m_isSpinning;
            m_label.Hidden = !isContentShown || !this.HasText;
            m_spinner.Hidden = !isContentShown;
            if (m_isSpinning && (this.Window != null))
            {
                m_spinner.StartAnimation(this);
            }
            else
            {
                m_spinner.StopAnimation(this);
            }
        }
    }
}
