namespace AJut.UX.Controls
{
    using System;
    using AppKit;
    using CoreGraphics;
    using Foundation;

    // ===========[ ManagedButton ]==============================================
    // The base for AJut's AppKit buttons that draw themselves: it tracks which of the four
    // eManagedButtonState states the button is in, and redraws when that changes. Derived
    // buttons only draw for CurrentState.
    //
    // It takes a click the usual Mac way: press, drag off to cancel, release inside to activate.
    // Space or Return activates it when it has keyboard focus. Activating fires the standard
    // NSControl target-action (Activated in C#), so BindingSet.BindCommand works on any of them.
    //
    // Hover is tracked only while the app is active, which is how stock Mac buttons behave.
    // Mouse dragged and mouse up go to the view that took the mouse down, so a press is tracked
    // across all three without capturing anything.
    //
    // With IsRepeating set, holding the button down activates it straight away, again after a
    // short delay, then steadily until it is released or the pointer leaves it: a repeat button.
    //
    // A custom NSControl with no cell is not an accessibility element by default, so VoiceOver
    // would skip it. Each ManagedButton reports itself as a button, enabled or not, labeled with
    // whatever AccessibilityLabel was set to, else the text it draws (DefaultAccessibilityLabel),
    // else its tooltip, and VoiceOver's press activates it the same way a click does.
    public abstract class ManagedButton : NSControl
    {
        private const double kRepeatDelaySeconds = 0.4;
        private const double kRepeatIntervalSeconds = 0.05;

        private NSTrackingArea m_trackingArea;
        private NSTimer m_repeatTimer;
        private bool m_isPointerOver;
        private bool m_isPressed;

        // ===========[ Setup/Construction/Teardown ]===========================
        protected ManagedButton ()
        {
            this.Enabled = true;
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                this.StopRepeating();
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        // The first tick waits the repeat delay; from then on the timer runs at the interval
        private void OnRepeatTick (NSTimer timer)
        {
            if (!m_isPressed || !this.Enabled)
            {
                this.StopRepeating();
                return;
            }

            this.SendAction(this.Action, this.Target);
            if (timer.TimeInterval > kRepeatIntervalSeconds)
            {
                this.StopRepeating();
                m_repeatTimer = NSTimer.CreateRepeatingScheduledTimer(TimeSpan.FromSeconds(kRepeatIntervalSeconds), this.OnRepeatTick);
            }
        }

        // ===========[ Properties ]============================================
        public override bool IsFlipped => true;
        public override bool AcceptsFirstResponder () => this.Enabled;
        public override bool AcceptsFirstMouse (NSEvent theEvent) => true;

        /// <summary>Activates on press and keeps activating while held, like a stepper arrow, instead of once on release</summary>
        public bool IsRepeating { get; set; }

        /// <summary>
        /// The state the button is drawn in right now. A subclass can override it to draw one fixed state, which is how
        /// a theme preview shows pointer over and pressed with no pointer; clicks are still tracked as usual.
        /// </summary>
        public virtual eManagedButtonState CurrentState
        {
            get
            {
                if (!this.Enabled)
                {
                    return eManagedButtonState.Disabled;
                }

                if (m_isPressed)
                {
                    return eManagedButtonState.Pressed;
                }

                return m_isPointerOver ? eManagedButtonState.PointerOver : eManagedButtonState.Normal;
            }
        }

        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;
                if (!value)
                {
                    m_isPressed = false;
                }

                this.NeedsDisplay = true;
            }
        }

        // The focus ring follows the button's bounds, which is also its hit area
        public override CGRect FocusRingMaskBounds => this.Bounds;

        public override bool AccessibilityElement => true;
        public override string AccessibilityRole => NSAccessibilityRoles.ButtonRole;
        public override bool AccessibilityEnabled => this.Enabled;

        /// <summary>The label VoiceOver reads: whatever was set, else the text the button draws, else its tooltip</summary>
        public override string AccessibilityLabel
        {
            get
            {
                string label = base.AccessibilityLabel;
                if (String.IsNullOrEmpty(label))
                {
                    label = this.DefaultAccessibilityLabel;
                }

                return String.IsNullOrEmpty(label) ? this.ToolTip : label;
            }
            set => base.AccessibilityLabel = value;
        }

        /// <summary>The text the button draws, if any, which names it to VoiceOver when no AccessibilityLabel is set</summary>
        protected virtual string DefaultAccessibilityLabel => null;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Activates the button as a click does, firing its action</summary>
        public override void PerformClick (NSObject sender)
        {
            if (this.Enabled)
            {
                this.SendAction(this.Action, this.Target);
            }
        }

        /// <summary>VoiceOver's press, which activates the button the way a click does</summary>
        public override bool AccessibilityPerformPress ()
        {
            if (!this.Enabled)
            {
                return false;
            }

            this.PerformClick(this);
            return true;
        }

        public override void UpdateTrackingAreas ()
        {
            base.UpdateTrackingAreas();
            if (m_trackingArea != null)
            {
                this.RemoveTrackingArea(m_trackingArea);
            }

            m_trackingArea = new NSTrackingArea(
                this.Bounds,
                NSTrackingAreaOptions.MouseEnteredAndExited | NSTrackingAreaOptions.ActiveInActiveApp | NSTrackingAreaOptions.InVisibleRect,
                this,
                null
            );
            this.AddTrackingArea(m_trackingArea);
        }

        public override void MouseEntered (NSEvent theEvent) => this.SetPointerOver(true);
        public override void MouseExited (NSEvent theEvent) => this.SetPointerOver(false);

        public override void MouseDown (NSEvent theEvent)
        {
            if (!this.Enabled)
            {
                return;
            }

            this.SetPressed(true);
            if (this.IsRepeating)
            {
                // A repeat button acts on the press, not the release
                this.SendAction(this.Action, this.Target);
                this.StopRepeating();
                m_repeatTimer = NSTimer.CreateRepeatingScheduledTimer(TimeSpan.FromSeconds(kRepeatDelaySeconds), this.OnRepeatTick);
            }
        }

        public override void MouseDragged (NSEvent theEvent)
        {
            if (this.Enabled)
            {
                this.SetPressed(this.IsInside(theEvent));
            }
        }

        public override void MouseUp (NSEvent theEvent)
        {
            bool wasPressed = m_isPressed;
            this.SetPressed(false);
            if (this.IsRepeating)
            {
                this.StopRepeating();
                return;
            }

            if (wasPressed && this.Enabled && this.IsInside(theEvent))
            {
                this.SendAction(this.Action, this.Target);
            }
        }

        public override void KeyDown (NSEvent theEvent)
        {
            string characters = theEvent.Characters;
            if (this.Enabled && (characters == " " || characters == "\r"))
            {
                this.PerformClick(this);
                return;
            }

            base.KeyDown(theEvent);
        }

        public override void ViewDidChangeEffectiveAppearance ()
        {
            base.ViewDidChangeEffectiveAppearance();
            this.NeedsDisplay = true;
        }

        public override void DrawFocusRingMask ()
        {
            NSBezierPath.FillRect(this.Bounds);
        }

        // ===========[ Helper Methods ]========================================
        /// <summary>Drops the press being tracked, so its release does not activate the button (a press a container took over, say)</summary>
        protected void CancelPress ()
        {
            this.StopRepeating();
            this.SetPressed(false);
        }

        private void StopRepeating ()
        {
            if (m_repeatTimer != null)
            {
                m_repeatTimer.Invalidate();
                m_repeatTimer = null;
            }
        }

        private void SetPointerOver (bool isPointerOver)
        {
            if (m_isPointerOver != isPointerOver)
            {
                m_isPointerOver = isPointerOver;
                this.NeedsDisplay = true;
            }
        }

        private void SetPressed (bool isPressed)
        {
            if (m_isPressed != isPressed)
            {
                m_isPressed = isPressed;
                this.NeedsDisplay = true;
            }
        }

        private bool IsInside (NSEvent theEvent)
        {
            CGPoint point = this.ConvertPointFromView(theEvent.LocationInWindow, null);
            return this.Bounds.Contains(point);
        }
    }
}
