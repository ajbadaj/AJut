namespace AJut.UX.Controls
{
    using System;
    using System.ComponentModel;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using Foundation;

    // ===========[ ThemedWindow ]===============================================
    // The AppKit sibling of ThemedWindowRootControl: a window whose chrome is drawn from AJut's
    // themed colors, with a border that tells you whether the window is key and turns the close
    // warning color while the pointer is over the close button.
    //
    // macOS draws the close, minimize and zoom buttons (the "traffic lights") itself, top left,
    // and they cannot be recolored, so unlike WinUI3 there are no caption buttons to draw here.
    // The window keeps the system title bar for them, made transparent with its title hidden
    // (FullSizeContentView), and lays its own title strip in that space.
    //
    // Chrome state is decided by AJut.UX's WindowChromeStateTracker, the same tracker the other
    // stacks use. This window only feeds it: key state, window state, and pointer enter and exit
    // over the traffic lights, read from tracking areas added to the system's own buttons.
    // AppKit gives no public signal for a traffic light being pressed (each button runs its own
    // mouse-tracking loop), so pressed is always reported as none.
    //
    // ThemedWindow is its own NSWindowDelegate. An NSWindow's C# events (DidBecomeKey and the
    // rest) are implemented by the bindings through an internal delegate, and setting Delegate as
    // well replaces that one: on a run of a similar window, a window that did both hung at startup
    // with no window and no error. So do not set Delegate on a ThemedWindow, and do not use its
    // NSWindow events; use the events ThemedWindow declares (Closing, Closed, KeyStateChanged,
    // TitleStripDragging).
    public class ThemedWindow : NSWindow
    {
        private const double kDefaultTitleBarHeight = 32.0;
        private const double kDefaultBorderThickness = 2.0;
        private const double kDefaultCornerRadius = 10.0;

        // Room left of the title for the traffic lights: three 14pt buttons at a 20pt pitch from a
        // 7pt inset leaves the zoom button ending near 61pt, plus a gap before the title starts
        private const double kTrafficLightClearance = 78.0;

        // Just under the priority an NSWindow holds its own size at, so content can fill the window
        // but never size it (see PinWithoutSizingWindow)
        private const float kBelowWindowSizeStayPut = (float)NSLayoutPriority.WindowSizeStayPut - 1f;

        private static readonly NSWindowButton[] kCaptionButtons = new[] { NSWindowButton.CloseButton, NSWindowButton.MiniaturizeButton, NSWindowButton.ZoomButton };

        private readonly WindowChromeStateTracker m_chrome = new WindowChromeStateTracker();
        private readonly ChromeDelegate m_delegate;
        private readonly ThemedView m_rootView;
        private readonly FillStack m_rootStack;
        private readonly ThemedView m_titleStrip;
        private readonly FillStack m_titleStripStack;
        private readonly NSTextField m_titleLabel;
        private readonly NSView m_titleBarContentHost;
        private readonly NSView m_contentsHost;
        private readonly CaptionButtonWatcher[] m_captionWatchers = new CaptionButtonWatcher[kCaptionButtons.Length];
        private NSLayoutConstraint m_titleStripHeight;
        private NSView m_windowContents;
        private NSView m_titleBarContent;
        private double m_borderThickness = kDefaultBorderThickness;
        private bool m_isTornDown;

        // ===========[ Setup/Construction/Teardown ]===========================
        /// <summary>Creates a titled, closable, resizable window with AJut chrome, its contents sized to <paramref name="contentRect"/></summary>
        public ThemedWindow (CGRect contentRect)
            : base(
                contentRect,
                NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Miniaturizable | NSWindowStyle.Resizable | NSWindowStyle.FullSizeContentView,
                NSBackingStore.Buffered,
                false
            )
        {
            this.ReleaseWhenClosed(false);
            this.TitlebarAppearsTransparent = true;
            this.TitleVisibility = NSWindowTitleVisibility.Hidden;
            this.BackgroundColor = ThemedColors.Get(ThemedColorKeys.ThemedWindow_Background);

            // 1. Root: the bordered surface everything sits in
            m_rootView = new ThemedView
            {
                BackgroundColorKey = ThemedColorKeys.ThemedWindow_Background,
                BorderColorKey = ThemedColorKeys.ThemedWindow_ActiveBorder,
                BorderThickness = m_borderThickness,
                CornerRadius = kDefaultCornerRadius,
            };
            m_rootStack = new FillStack(NSUserInterfaceLayoutOrientation.Vertical);
            m_rootStack.TranslatesAutoresizingMaskIntoConstraints = false;
            m_rootView.AddSubview(m_rootStack);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_rootStack.TopAnchor.ConstraintEqualTo(m_rootView.TopAnchor),
                m_rootStack.BottomAnchor.ConstraintEqualTo(m_rootView.BottomAnchor),
                m_rootStack.LeadingAnchor.ConstraintEqualTo(m_rootView.LeadingAnchor),
                m_rootStack.TrailingAnchor.ConstraintEqualTo(m_rootView.TrailingAnchor),
            });

            // 2. Title strip: title text, then any custom title bar content filling the rest
            m_titleStrip = new TitleStripView { BackgroundColorKey = ThemedColorKeys.ThemedWindow_ActiveTitleBarBackground };
            m_titleStripStack = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: 8.0)
            {
                Padding = new NSEdgeInsets(0, (nfloat)kTrafficLightClearance, 0, 8),
            };
            m_titleStripStack.TranslatesAutoresizingMaskIntoConstraints = false;
            m_titleStrip.AddSubview(m_titleStripStack);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_titleStripStack.TopAnchor.ConstraintEqualTo(m_titleStrip.TopAnchor),
                m_titleStripStack.BottomAnchor.ConstraintEqualTo(m_titleStrip.BottomAnchor),
                m_titleStripStack.LeadingAnchor.ConstraintEqualTo(m_titleStrip.LeadingAnchor),
                m_titleStripStack.TrailingAnchor.ConstraintEqualTo(m_titleStrip.TrailingAnchor),
            });

            m_titleLabel = NSTextField.CreateLabel(String.Empty);
            m_titleLabel.TextColor = ThemedColors.Get(ThemedColorKeys.ThemedWindow_TitleForeground);
            m_titleLabel.Font = NSFont.TitleBarFontOfSize((nfloat)0);
            m_titleLabel.LineBreakMode = NSLineBreakMode.TruncatingTail;

            // A label resists truncation at 750, which would widen the window to fit a long title.
            // Below the window's own size priority it truncates instead.
            m_titleLabel.SetContentCompressionResistancePriority(kBelowWindowSizeStayPut, NSLayoutConstraintOrientation.Horizontal);
            m_titleStripStack.Add(m_titleLabel, alignment: eFillStackCrossAlignment.Center);

            m_titleBarContentHost = new NSView();
            m_titleStripStack.Add(m_titleBarContentHost, grows: true);

            m_titleStripHeight = m_titleStrip.HeightAnchor.ConstraintEqualTo((nfloat)kDefaultTitleBarHeight);
            m_titleStripHeight.Active = true;
            m_rootStack.Add(m_titleStrip);

            // 3. Contents host, taking everything below the title strip. It clips, so contents larger
            // than the window are cut at its edge rather than drawn past it (see PinWithoutSizingWindow).
            m_contentsHost = new NSView { ClipsToBounds = true };
            m_rootStack.Add(m_contentsHost, grows: true);

            this.ContentView = m_rootView;

            // 4. Chrome state: this window feeds the tracker, the tracker decides the look
            m_delegate = new ChromeDelegate(this);
            this.Delegate = m_delegate;
            m_chrome.PropertyChanged += this.OnChromePropertyChanged;
            m_chrome.IsActivated = this.IsKeyWindow;
            this.RefreshWindowState();
            this.InstallCaptionButtonWatchers();
            this.ApplyChromeState();
        }

        // Undo everything the window hooked up. Runs once, when the window closes.
        private void Teardown ()
        {
            if (m_isTornDown)
            {
                return;
            }

            m_isTornDown = true;
            this.RemoveCaptionButtonWatchers();
            m_chrome.PropertyChanged -= this.OnChromePropertyChanged;
            this.Delegate = null;
        }

        // ===========[ Events ]================================================
        /// <summary>
        /// Raised when the user (or <see cref="RequestClose"/>) asks the window to close. Set Cancel to keep it
        /// open. To confirm asynchronously, cancel, ask, and call Close() once the answer is yes: Close()
        /// closes without asking again.
        /// </summary>
        public event EventHandler<CancelEventArgs> Closing;

        /// <summary>Raised as the window closes, after its chrome hooks are released</summary>
        public event EventHandler<EventArgs> Closed;

        /// <summary>Raised when the window becomes or stops being the key window</summary>
        public event EventHandler<EventArgs> KeyStateChanged;

        /// <summary>
        /// Raised when a single click lands on the title strip, before the strip drags the window. Set Handled to drag (or
        /// not) some other way, the way a docking manager runs its own drag for a tearoff window.
        /// </summary>
        public event EventHandler<TitleStripDragEventArgs> TitleStripDragging;

        private void OnChromePropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WindowChromeStateTracker.State)
                || e.PropertyName == nameof(WindowChromeStateTracker.ShowsWindowBorder))
            {
                this.ApplyChromeState();
            }
        }

        // ===========[ Properties ]============================================
        /// <summary>
        /// The chrome state tracker this window feeds. Read it to follow the window's chrome state (key,
        /// inactive, hovering close), the same way on every AJut stack.
        /// </summary>
        public WindowChromeStateTracker Chrome => m_chrome;

        /// <summary>
        /// The window's contents, shown below the title strip. They fill the rest of the window unless they hug their own
        /// size harder than the window holds its size (priority 500), the way a label does: those keep their own size at the
        /// top left. Either way the contents never resize the window.
        /// </summary>
        public NSView WindowContents
        {
            get => m_windowContents;
            set
            {
                if (m_windowContents == value)
                {
                    return;
                }

                m_windowContents?.RemoveFromSuperview();
                m_windowContents = value;
                if (value != null)
                {
                    PinWithoutSizingWindow(value, m_contentsHost, fillsHeight: false);
                }
            }
        }

        /// <summary>Custom title bar content, shown right of the title and filling the rest of the title strip</summary>
        public NSView TitleBarContent
        {
            get => m_titleBarContent;
            set
            {
                if (m_titleBarContent == value)
                {
                    return;
                }

                m_titleBarContent?.RemoveFromSuperview();
                m_titleBarContent = value;
                if (value != null)
                {
                    PinWithoutSizingWindow(value, m_titleBarContentHost, fillsHeight: true);
                }
            }
        }

        /// <summary>The title, drawn in AJut's title strip (the system title is hidden)</summary>
        public override string Title
        {
            get => base.Title;
            set
            {
                base.Title = value;
                m_titleLabel.StringValue = value ?? String.Empty;
            }
        }

        /// <summary>Height of the title strip in points. The traffic lights sit in its top 28pt whatever it is set to.</summary>
        public double TitleBarHeight
        {
            get => (double)m_titleStripHeight.Constant;
            set => m_titleStripHeight.Constant = (nfloat)value;
        }

        /// <summary>Window border width in points. The border is dropped in fullscreen, whatever this is.</summary>
        public double BorderThickness
        {
            get => m_borderThickness;
            set
            {
                m_borderThickness = value;
                this.ApplyChromeState();
            }
        }

        /// <summary>
        /// Corner radius of the border in points. Match it to the system's window corners, or the border
        /// and the window's own rounded edge part ways at the corners.
        /// </summary>
        public double CornerRadius
        {
            get => m_rootView.CornerRadius;
            set => m_rootView.CornerRadius = value;
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Asks the window to close the way its close button does, raising <see cref="Closing"/> first</summary>
        public void RequestClose ()
        {
            this.PerformClose(this);
        }

        // ===========[ Helper Methods ]========================================
        private void HandleKeyStateChanged (bool isKey)
        {
            m_chrome.IsActivated = isKey;
            this.KeyStateChanged?.Invoke(this, EventArgs.Empty);
        }

        // True when a TitleStripDragging handler took the press, so the strip leaves the window where it is
        private bool HandleTitleStripMouseDown (NSEvent mouseDown)
        {
            var args = new TitleStripDragEventArgs(mouseDown);
            this.TitleStripDragging?.Invoke(this, args);
            return args.Handled;
        }

        private bool HandleShouldClose ()
        {
            var args = new CancelEventArgs();
            this.Closing?.Invoke(this, args);
            return !args.Cancel;
        }

        private void HandleWillClose ()
        {
            this.Teardown();
            this.Closed?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyChromeState ()
        {
            eWindowChromeState state = m_chrome.State;
            m_rootView.BorderColorKey = WindowChromeStateTracker.PickForState(
                state,
                ThemedColorKeys.ThemedWindow_ActiveBorder,
                ThemedColorKeys.ThemedWindow_InactiveBorder,
                ThemedColorKeys.ThemedWindow_CloseHoverBorder
            );
            m_titleStrip.BackgroundColorKey = WindowChromeStateTracker.PickForState(
                state,
                ThemedColorKeys.ThemedWindow_ActiveTitleBarBackground,
                ThemedColorKeys.ThemedWindow_InactiveTitleBarBackground,
                null
            );
            m_rootView.BorderThickness = m_chrome.ShowsWindowBorder ? m_borderThickness : 0.0;
        }

        private void RefreshWindowState ()
        {
            if (this.IsMiniaturized)
            {
                m_chrome.WindowState = eWindowState.Minimized;
            }
            else if ((this.StyleMask & NSWindowStyle.FullScreenWindow) == NSWindowStyle.FullScreenWindow)
            {
                m_chrome.WindowState = eWindowState.FullScreened;
            }
            else if (this.IsZoomed)
            {
                m_chrome.WindowState = eWindowState.Maximized;
            }
            else
            {
                m_chrome.WindowState = eWindowState.Restored;
            }
        }

        // The traffic lights are the system's views, and the system can swap them out (entering or
        // leaving fullscreen rebuilds the title bar), so watchers are removed from whatever buttons
        // they were on and installed fresh on whatever buttons are there now
        private void InstallCaptionButtonWatchers ()
        {
            this.RemoveCaptionButtonWatchers();
            for (int i = 0; i < kCaptionButtons.Length; ++i)
            {
                NSButton button = this.StandardWindowButton(kCaptionButtons[i]);
                if (button != null)
                {
                    m_captionWatchers[i] = new CaptionButtonWatcher(this, button, ToCaptionButton(kCaptionButtons[i]));
                }
            }
        }

        private void RemoveCaptionButtonWatchers ()
        {
            for (int i = 0; i < m_captionWatchers.Length; ++i)
            {
                m_captionWatchers[i]?.Detach();
                m_captionWatchers[i] = null;
            }
        }

        private void OnCaptionButtonPointerEntered (eCaptionButton button)
        {
            m_chrome.SetSystemCaptionButtonPointer(button, null);
        }

        private void OnCaptionButtonPointerExited (eCaptionButton button)
        {
            // Moving straight from one traffic light to the next can deliver the next one's enter
            // before this exit, so only clear when the exit is for the button still held as hovered
            if (m_chrome.HoveredCaptionButton == button)
            {
                m_chrome.SetSystemCaptionButtonPointer(null, null);
            }
        }

        private static eCaptionButton ToCaptionButton (NSWindowButton button)
        {
            switch (button)
            {
                case NSWindowButton.CloseButton: return eCaptionButton.Close;
                case NSWindowButton.MiniaturizeButton: return eCaptionButton.Minimize;
                default: return eCaptionButton.Maximize;
            }
        }

        // Pins content into one of the window's hosts so that it fills the host but can never size
        // the window.
        //
        // An NSWindow whose content uses Auto Layout keeps its size only at priority 500
        // (NSLayoutPriority.WindowSizeStayPut), and a user dragging the window edge pushes at 510
        // (DragThatCanResizeWindow). Any chain of constraints stronger than that, running from the
        // content out to the window's edges, wins: the window is resized to satisfy it, and the user
        // cannot drag it past it either. A label's vertical content hugging is 750, so a label pinned
        // to all four edges with required constraints pulls the window down to the label's height. A
        // 420x260 window opened with one wrapping label in it came up about 60pt tall that way.
        //
        // So the top and leading pins stay required, and the trailing and bottom pins sit just under
        // WindowSizeStayPut. Content with no intrinsic size, or hugging below 499 (containers, scroll
        // views, FillStack growers), still stretches to fill; content that hugs harder than the window
        // holds its size keeps its own size at the top left, and the window stays the size it was
        // given. No content-to-edge constraint is above 500 either way, so content never shrinks or
        // grows the window, and the contents host clips whatever does not fit.
        //
        // Title bar content keeps a required bottom pin: the title strip's height is a required
        // constant, so filling it vertically can never move the window.
        private static void PinWithoutSizingWindow (NSView child, NSView host, bool fillsHeight)
        {
            child.TranslatesAutoresizingMaskIntoConstraints = false;
            host.AddSubview(child);

            NSLayoutConstraint trailing = child.TrailingAnchor.ConstraintEqualTo(host.TrailingAnchor);
            trailing.Priority = kBelowWindowSizeStayPut;

            NSLayoutConstraint bottom = child.BottomAnchor.ConstraintEqualTo(host.BottomAnchor);
            if (!fillsHeight)
            {
                bottom.Priority = kBelowWindowSizeStayPut;
            }

            NSLayoutConstraint.ActivateConstraints(new[]
            {
                child.TopAnchor.ConstraintEqualTo(host.TopAnchor),
                child.LeadingAnchor.ConstraintEqualTo(host.LeadingAnchor),
                trailing,
                bottom,
            });
        }

        // ===========[ Subclasses/structs ]====================================
        // The title strip drags the window (unless a TitleStripDragging handler takes the press, the
        // way a docking manager drags its tearoffs) and answers a double-click the way the user has set
        // double-clicking a title bar to behave in System Settings (Desktop & Dock, "Double-click
        // a window's title bar to"). That setting is stored as the global default
        // AppleActionOnDoubleClick: "Maximize" (zoom), "Minimize", or "None"; it is unset on a Mac
        // that has never changed it, and unset means zoom.
        private sealed class TitleStripView : ThemedView
        {
            public override bool MouseDownCanMoveWindow => false;

            public override void MouseDown (NSEvent theEvent)
            {
                NSWindow window = this.Window;
                if (window == null)
                {
                    base.MouseDown(theEvent);
                    return;
                }

                if (theEvent.ClickCount == 2)
                {
                    string action = NSUserDefaults.StandardUserDefaults.StringForKey("AppleActionOnDoubleClick");
                    if (action == "Minimize")
                    {
                        window.PerformMiniaturize(this);
                    }
                    else if (action != "None")
                    {
                        window.PerformZoom(this);
                    }

                    return;
                }

                if ((window as ThemedWindow)?.HandleTitleStripMouseDown(theEvent) == true)
                {
                    return;
                }

                window.PerformWindowDrag(theEvent);
            }
        }

        /// <summary>A single click on a <see cref="ThemedWindow"/>'s title strip, which a handler can take over from the window drag</summary>
        public sealed class TitleStripDragEventArgs : EventArgs
        {
            internal TitleStripDragEventArgs (NSEvent mouseDown)
            {
                this.MouseDown = mouseDown;
            }

            /// <summary>The mouse down on the title strip</summary>
            public NSEvent MouseDown { get; }

            /// <summary>Set true when the handler drags (or decides not to); the title strip then leaves the window where it is</summary>
            public bool Handled { get; set; }
        }

        // Watches one traffic light for the pointer entering and leaving it. AppKit sends a
        // tracking area's enter and exit to its owner, which has to be a responder, so each
        // watcher is the owner of the one tracking area it adds to its button.
        private sealed class CaptionButtonWatcher : NSResponder
        {
            private readonly eCaptionButton m_which;
            private ThemedWindow m_owner;
            private NSButton m_button;
            private NSTrackingArea m_trackingArea;

            public CaptionButtonWatcher (ThemedWindow owner, NSButton button, eCaptionButton which)
            {
                m_owner = owner;
                m_button = button;
                m_which = which;

                // ActiveAlways: the traffic lights react to the pointer in an inactive window too.
                // InVisibleRect: the area follows the button's bounds without being rebuilt.
                m_trackingArea = new NSTrackingArea(
                    button.Bounds,
                    NSTrackingAreaOptions.MouseEnteredAndExited | NSTrackingAreaOptions.ActiveAlways | NSTrackingAreaOptions.InVisibleRect,
                    this,
                    null
                );
                button.AddTrackingArea(m_trackingArea);
            }

            public void Detach ()
            {
                if (m_button != null && m_trackingArea != null)
                {
                    m_button.RemoveTrackingArea(m_trackingArea);
                }

                m_trackingArea = null;
                m_button = null;
                m_owner = null;
            }

            public override void MouseEntered (NSEvent theEvent)
            {
                m_owner?.OnCaptionButtonPointerEntered(m_which);
            }

            public override void MouseExited (NSEvent theEvent)
            {
                m_owner?.OnCaptionButtonPointerExited(m_which);
            }
        }

        // The window's one delegate, see the class comment for why it is not the C# events
        private sealed class ChromeDelegate : NSWindowDelegate
        {
            private readonly ThemedWindow m_owner;

            public ChromeDelegate (ThemedWindow owner)
            {
                m_owner = owner;
            }

            public override void DidBecomeKey (NSNotification notification) => m_owner.HandleKeyStateChanged(true);
            public override void DidResignKey (NSNotification notification) => m_owner.HandleKeyStateChanged(false);
            public override bool WindowShouldClose (NSObject sender) => m_owner.HandleShouldClose();
            public override void WillClose (NSNotification notification) => m_owner.HandleWillClose();

            public override void DidResize (NSNotification notification)
            {
                m_owner.RefreshWindowState();
            }

            public override void DidMiniaturize (NSNotification notification)
            {
                m_owner.RefreshWindowState();
            }

            public override void DidDeminiaturize (NSNotification notification)
            {
                m_owner.RefreshWindowState();
            }

            public override void DidEnterFullScreen (NSNotification notification)
            {
                m_owner.RefreshWindowState();
                m_owner.InstallCaptionButtonWatchers();
            }

            public override void DidExitFullScreen (NSNotification notification)
            {
                m_owner.RefreshWindowState();
                m_owner.InstallCaptionButtonWatchers();
            }
        }
    }
}
