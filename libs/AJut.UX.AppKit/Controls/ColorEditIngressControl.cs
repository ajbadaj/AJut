namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using AppKit;
    using Foundation;

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
    // The border shows the AJut highlight while this swatch owns the panel.
    public class ColorEditIngressControl : ThemedView, IUserEditNotifier
    {
        private const double kDefaultSize = 24.0;
        private const double kWellInset = 2.0;

        private static readonly ColorArgb kDefaultColor = new ColorArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

        private readonly IngressWell m_well;
        private ColorArgb m_editColor = kDefaultColor;
        private ColorArgb m_colorAtEditStart;
        private bool m_isEditing;
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
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_well.TopAnchor.ConstraintEqualTo(this.TopAnchor, (nfloat)kWellInset),
                m_well.BottomAnchor.ConstraintEqualTo(this.BottomAnchor, (nfloat)(-kWellInset)),
                m_well.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor, (nfloat)kWellInset),
                m_well.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor, (nfloat)(-kWellInset)),
            });
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

        // ===========[ Helper Methods ]========================================
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
            if (m_colorAtEditStart != m_editColor)
            {
                this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(m_colorAtEditStart, m_editColor));
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // Tells its owner when it takes the color panel and when it lets it go
        private sealed class IngressWell : NSColorWell
        {
            private readonly ColorEditIngressControl m_owner;

            public IngressWell (ColorEditIngressControl owner)
            {
                m_owner = owner;
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
