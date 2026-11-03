namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using AppKit;
    using Foundation;
    using ObjCRuntime;

    // ===========[ ColorEditIngressControl ]====================================
    // A color swatch that opens the color picker when clicked, the AppKit counterpart of the
    // WinUI3 control of the same name. EditColor is AJut.UX's ColorArgb on every non-XAML stack,
    // so it is also the editor a PropertyGrid uses for the "ColorArgb" editor key.
    //
    // The picker is the system color panel, through an NSColorWell. The panel is one per app, and
    // the well is what makes sharing it safe: activating one well deactivates any other, so two
    // swatches can never both be driven by the panel. EditColor changes live as the user drags
    // in the panel, and UserEditComplete fires once, when the well lets go of the panel (the panel
    // closes or another well takes it), if the color ended up different. The panel is put away on a
    // click outside it, a second click on the swatch, or the swatch leaving its window, so it acts
    // like a popover rather than a window left open.
    //
    // The border shows the AJut highlight while this swatch owns the panel. Under the pointer, and while it owns the
    // panel, the swatch takes the edit overlay WinUI3's and Android's take: a dim and a dog-ear in its corner.
    public class ColorEditIngressControl : ThemedView, IUserEditNotifier
    {
        private const double kDefaultSize = 24.0;
        private const double kWellInset = 2.0;

        private static readonly ColorArgb kDefaultColor = new ColorArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

        private readonly IngressWell m_well;
        private readonly EditOverlay m_overlay;
        private ColorArgb m_editColor = kDefaultColor;
        private ColorArgb m_colorAtEditStart;
        private bool m_isEditing;
        private bool m_isPointerOver;
        private bool m_isReadOnly;
        private NSObject m_clickAwayMonitor;

        // ===========[ Setup/Construction/Teardown ]===========================
        public ColorEditIngressControl ()
        {
            this.BorderThickness = 1.0;
            this.CornerRadius = 3.0;
            this.BorderColorKey = ThemedColorKeys.ColorEditIngress_NormalBorder;

            // Bordered is deprecated from macOS 14 in favor of ColorWellStyle, but none of the styles is
            // a plain swatch: Default draws the classic bezel (a second border inside this control's own),
            // and Minimal and Expanded swap the color panel for a popover. Turning the bezel off is still
            // the only way to get just the swatch, and it still works on the systems that deprecate it.
#pragma warning disable CA1422
            m_well = new IngressWell(this)
            {
                Color = m_editColor.ToNSColor(),
                Bordered = false,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };
#pragma warning restore CA1422
            m_well.Activated += this.OnWellActivated;
            this.AddSubview(m_well);

            // Added after the well, so it draws over it; it takes no clicks, which go through to the well
            m_overlay = new EditOverlay { TranslatesAutoresizingMaskIntoConstraints = false };
            this.AddSubview(m_overlay);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_well.TopAnchor.ConstraintEqualTo(this.TopAnchor, (nfloat)kWellInset),
                m_well.BottomAnchor.ConstraintEqualTo(this.BottomAnchor, (nfloat)(-kWellInset)),
                m_well.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor, (nfloat)kWellInset),
                m_well.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor, (nfloat)(-kWellInset)),
                m_overlay.TopAnchor.ConstraintEqualTo(m_well.TopAnchor),
                m_overlay.BottomAnchor.ConstraintEqualTo(m_well.BottomAnchor),
                m_overlay.LeadingAnchor.ConstraintEqualTo(m_well.LeadingAnchor),
                m_overlay.TrailingAnchor.ConstraintEqualTo(m_well.TrailingAnchor),
            });

            // One tracking area for the control's whole life, kept in step with its visible rect by AppKit, for the
            // reasons ManagedButton gives for its own
            this.AddTrackingArea(new NSTrackingArea(
                CoreGraphics.CGRect.Empty,
                NSTrackingAreaOptions.MouseEnteredAndExited | NSTrackingAreaOptions.ActiveInActiveApp | NSTrackingAreaOptions.InVisibleRect,
                this,
                null
            ));
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native view whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls: the native view keeps the well and overlay it has.
        /// None of its managed state survives a dispose, so a call that needs that state can still fail: dispose it only
        /// once AppKit is done with it.
        /// </summary>
        protected ColorEditIngressControl (NativeHandle handle) : base(handle)
        {
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                m_well.Activated -= this.OnWellActivated;
                this.ReleasePanel();
                this.RemoveClickAwayMonitor();
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        /// <summary>Raised whenever <see cref="EditColor"/> changes, live while the user picks, or from code</summary>
        public event EventHandler<EventArgs> EditColorChanged;

        /// <summary>Raised once per edit, when the color panel is let go, if the color changed. Old and new values are ColorArgb.</summary>
        public event EventHandler<UserEditAppliedEventArgs> UserEditComplete;

        private void OnWellActivated (object sender, EventArgs e)
        {
            if (m_well.Color.TryToColorArgb(out ColorArgb picked))
            {
                this.SetEditColor(picked, updateWell: false);
            }
        }

        // ===========[ Properties ]============================================
        /// <summary>The color being edited</summary>
        public ColorArgb EditColor
        {
            get => m_editColor;
            set => this.SetEditColor(value, updateWell: true);
        }

        /// <summary>Shows the color without letting it be opened for editing</summary>
        public bool IsReadOnly
        {
            get => m_isReadOnly;
            set
            {
                m_isReadOnly = value;
                m_well.Enabled = !value;
                if (value)
                {
                    this.ReleasePanel();
                }
            }
        }

        /// <summary>True while this swatch has the color panel open</summary>
        public bool ShowEditDisplay => m_isEditing;

        public override CoreGraphics.CGSize IntrinsicContentSize => new CoreGraphics.CGSize((nfloat)kDefaultSize, (nfloat)kDefaultSize);

        // ===========[ Public Interface Methods ]==============================
        // A swatch taken off screen (its page navigated away) lets go of the panel, so the panel cannot be left
        // open setting a color nothing shows
        public override void ViewWillMoveToWindow (NSWindow newWindow)
        {
            if (newWindow == null)
            {
                this.ReleasePanel();
            }

            base.ViewWillMoveToWindow(newWindow);
        }

        public override void MouseEntered (NSEvent theEvent) => this.SetPointerOver(true);
        public override void MouseExited (NSEvent theEvent) => this.SetPointerOver(false);

        // ===========[ Helper Methods ]========================================
        private void SetPointerOver (bool isPointerOver)
        {
            m_isPointerOver = isPointerOver;
            this.RefreshOverlay();
        }

        private void RefreshOverlay () => m_overlay.ShowsEditLook = m_isPointerOver || m_isEditing;

        private void SetEditColor (ColorArgb value, bool updateWell)
        {
            if (m_editColor == value)
            {
                return;
            }

            m_editColor = value;
            if (updateWell)
            {
                m_well.Color = value.ToNSColor();
            }

            this.EditColorChanged?.Invoke(this, EventArgs.Empty);
        }

        // Deactivates the well and puts the panel away, if this swatch has it
        private void ReleasePanel ()
        {
            if (m_well.IsActive)
            {
                m_well.Deactivate();
                NSColorPanel.SharedColorPanel.OrderOut(null);
            }
        }

        // The color panel is a window, so on its own it stays up until it is closed. The picker is meant to be
        // ephemeral, the way a popover is, so while this swatch owns it a click anywhere outside the panel puts it
        // away. A click on this swatch is left to the swatch, which toggles. The eyedropper (the panel's color
        // sampler) runs its own event loop, so its clicks never come through here and sampling works as normal.
        private NSEvent OnLocalMouseDown (NSEvent theEvent)
        {
            if (theEvent.Window == NSColorPanel.SharedColorPanel)
            {
                return theEvent;
            }

            if (theEvent.Window == m_well.Window)
            {
                CoreGraphics.CGPoint inWell = m_well.ConvertPointFromView(theEvent.LocationInWindow, null);
                if (m_well.Bounds.Contains(inWell))
                {
                    return theEvent;
                }
            }

            this.ReleasePanel();
            return theEvent;
        }

        private void RemoveClickAwayMonitor ()
        {
            if (m_clickAwayMonitor != null)
            {
                NSEvent.RemoveMonitor(m_clickAwayMonitor);
                m_clickAwayMonitor = null;
            }
        }

        private void OnEditStarted ()
        {
            if (m_clickAwayMonitor == null)
            {
                m_clickAwayMonitor = NSEvent.AddLocalMonitorForEventsMatchingMask(
                    NSEventMask.LeftMouseDown | NSEventMask.RightMouseDown | NSEventMask.OtherMouseDown,
                    this.OnLocalMouseDown
                );
            }

            m_isEditing = true;
            m_colorAtEditStart = m_editColor;
            this.BorderColorKey = ThemedColorKeys.ColorEditIngress_HighlightBorder;
            this.RefreshOverlay();
        }

        private void OnEditEnded ()
        {
            this.RemoveClickAwayMonitor();
            if (!m_isEditing)
            {
                return;
            }

            m_isEditing = false;
            this.BorderColorKey = ThemedColorKeys.ColorEditIngress_NormalBorder;
            this.RefreshOverlay();
            if (m_colorAtEditStart != m_editColor)
            {
                this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(m_colorAtEditStart, m_editColor));
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // The edit overlay over the swatch, in WinUI3's literal colors: a dim over the whole swatch and a dog-ear in its
        // bottom trailing corner, edged along its foot with a light line. It is see-through to clicks.
        private sealed class EditOverlay : NSView
        {
            private const double kDogEarMax = 24.0;
            private const double kFootLineWidth = 1.0;
            private const uint kDim = 0x60000000;
            private const uint kDogEarFill = 0x33505050;
            private const uint kDogEarEdge = 0x3FFFFFFF;

            private bool m_showsEditLook;

            public EditOverlay ()
            {
            }

            // For a peer the runtime makes over an existing native overlay
            private EditOverlay (NativeHandle handle) : base(handle)
            {
            }

            public bool ShowsEditLook
            {
                get => m_showsEditLook;
                set
                {
                    if (m_showsEditLook != value)
                    {
                        m_showsEditLook = value;
                        this.NeedsDisplay = true;
                    }
                }
            }

            public override bool IsFlipped => true;

            public override NSView HitTest (CoreGraphics.CGPoint aPoint) => null;

            public override void DrawRect (CoreGraphics.CGRect dirtyRect)
            {
                if (!m_showsEditLook)
                {
                    return;
                }

                CoreGraphics.CGRect bounds = this.Bounds;
                ThemedColorKeys.Srgb(kDim).SetFill();
                NSBezierPath.FillRect(bounds);

                // The view is flipped, so the foot is the larger y
                double dogEar = Math.Min(kDogEarMax, Math.Min((double)bounds.Width, (double)bounds.Height));
                double right = (double)bounds.Right;
                double bottom = (double)bounds.Bottom;
                var corner = new NSBezierPath();
                corner.MoveTo(new CoreGraphics.CGPoint(right - dogEar, bottom));
                corner.LineTo(new CoreGraphics.CGPoint(right, bottom));
                corner.LineTo(new CoreGraphics.CGPoint(right, bottom - dogEar));
                corner.ClosePath();
                ThemedColorKeys.Srgb(kDogEarFill).SetFill();
                corner.Fill();

                // Stroked along its center, so it sits half its width up from the foot to stay inside the bounds
                double footY = bottom - (kFootLineWidth / 2.0);
                var foot = new NSBezierPath { LineWidth = (nfloat)kFootLineWidth };
                foot.MoveTo(new CoreGraphics.CGPoint(right - dogEar, footY));
                foot.LineTo(new CoreGraphics.CGPoint(right, footY));
                ThemedColorKeys.Srgb(kDogEarEdge).SetStroke();
                foot.Stroke();
            }
        }

        // Tells its owner when it takes the color panel and when it lets it go
        private sealed class IngressWell : NSColorWell
        {
            private readonly ColorEditIngressControl m_owner;

            public IngressWell (ColorEditIngressControl owner)
            {
                m_owner = owner;
            }

            // For a peer the runtime makes over an existing native well, which has no owner to tell
            private IngressWell (NativeHandle handle) : base(handle)
            {
            }

            // NSColorWell only activates (and opens the panel) on a click in its bezel. A click on the swatch itself
            // starts dragging the color out, and with Bordered off the whole well is swatch, so a mouse click never
            // opened the panel: it did nothing, or began a color drag. A mouse down here does what the bezel would,
            // and a second click on the swatch that has the panel puts it away. The cost is that a color can no longer
            // be dragged out of the swatch, which this control never set out to offer.
            public override void MouseDown (NSEvent theEvent)
            {
                this.TogglePanel();
            }

            // A keyboard press (Space on the focused swatch) comes through performClick, and an accessibility press
            // (VoiceOver's, or AXPress) through accessibilityPerformPress, and both land in NSColorWell's own toggle. That
            // deactivates an active well without putting the panel away, so after a second press the panel stays up
            // editing nothing, and leaving the window no longer closes it, since ReleasePanel only acts for an active
            // well. So both kinds of press toggle the way a click does.
            public override void PerformClick (NSObject sender)
            {
                this.TogglePanel();
            }

            public override bool AccessibilityPerformPress ()
            {
                if (!this.Enabled)
                {
                    return false;
                }

                this.TogglePanel();
                return true;
            }

            public override void Activate (bool exclusive)
            {
                base.Activate(exclusive);
                m_owner.OnEditStarted();
            }

            public override void Deactivate ()
            {
                base.Deactivate();
                m_owner.OnEditEnded();
            }

            // Opens the panel for this swatch, or puts it away when this swatch already has it
            private void TogglePanel ()
            {
                if (!this.Enabled)
                {
                    return;
                }

                if (this.IsActive)
                {
                    m_owner.ReleasePanel();
                    return;
                }

                this.Activate(true);
                NSApplication.SharedApplication.OrderFrontColorPanel(this);
            }
        }
    }
}
