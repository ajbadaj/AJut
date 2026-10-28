namespace AJut.UX
{
    using System;

    /// <summary>Which look a themed window's chrome (its border and title bar) is in</summary>
    public enum eWindowChromeState
    {
        /// <summary>The window is active, and the pointer is over none of its caption buttons</summary>
        Normal,

        /// <summary>Another window is active</summary>
        Inactive,

        /// <summary>The pointer is over a caption button other than close</summary>
        ChromeButtonsHover,

        /// <summary>The pointer is over the close button: the warning that a click closes the window</summary>
        CloseHover,
    }

    /// <summary>The system caption buttons a window's title bar has</summary>
    public enum eCaptionButton
    {
        /// <summary>Minimize (macOS: miniaturize)</summary>
        Minimize,

        /// <summary>Maximize or restore (macOS: zoom or fullscreen)</summary>
        Maximize,

        /// <summary>Close</summary>
        Close,
    }

    /// <summary>Where the pointer is over a title bar's own chrome, as opposed to the system's caption buttons</summary>
    public enum eChromeHover
    {
        /// <summary>Over none of the chrome's buttons</summary>
        None,

        /// <summary>Over the chrome's buttons, close aside</summary>
        ChromeButtons,

        /// <summary>Over the close button's area</summary>
        Close,
    }

    // ===========[ WindowChromeStateTracker ]===================================
    // The part of a themed window that decides which look its chrome is in, shared by every toolkit's
    // themed window. The window reports what happens to it (activation, the pointer over a system
    // caption button or over its own title bar, its window state), and reads back State,
    // IsHoveringOverClose and ShowsWindowBorder to draw itself. PickForState picks the brush or
    // color for a state, so every toolkit falls back the same way.
    //
    // State is worked out from the inputs every time one changes, never set by one input alone, so
    // the order inputs arrive in can't leave it stale:
    //  - A system caption button under the pointer decides first: close is CloseHover, any other
    //    is ChromeButtonsHover.
    //  - Otherwise the title bar's own hover zone decides the same way. Title bar moves are passed
    //    over while a system caption button has the pointer: the system owns that area, and the
    //    toolkit still reports moves there at the window's edge.
    //  - Otherwise it is Normal while the window is active, Inactive while it isn't.
    //  - IsHoveringOverClose is State == CloseHover, so the two never disagree.
    //  - The border shows unless the window is maximized or fullscreen. An Unknown window state,
    //    which a window reports while it is going away, changes nothing.
    public sealed class WindowChromeStateTracker : NotifyPropertyChanged
    {
        private eWindowChromeState m_state = eWindowChromeState.Normal;
        private bool m_isActivated = true;
        private bool m_isHoveringOverClose;
        private eCaptionButton? m_hoveredCaptionButton;
        private eCaptionButton? m_pressedCaptionButton;
        private eChromeHover m_titleBarHover = eChromeHover.None;
        private eWindowState m_windowState = eWindowState.Unknown;
        private bool m_showsWindowBorder = true;

        // ===========[ Properties ]============================================
        /// <summary>Which look the chrome is in</summary>
        public eWindowChromeState State
        {
            get => m_state;
            private set => this.SetAndRaiseIfChanged(ref m_state, value);
        }

        /// <summary>True while the pointer is over the close button</summary>
        public bool IsHoveringOverClose
        {
            get => m_isHoveringOverClose;
            private set => this.SetAndRaiseIfChanged(ref m_isHoveringOverClose, value);
        }

        /// <summary>Whether the window is the active (key) window: Normal or Inactive when the pointer is over no button</summary>
        public bool IsActivated
        {
            get => m_isActivated;
            set
            {
                this.SetAndRaiseIfChanged(ref m_isActivated, value);
                this.RefreshState();
            }
        }

        /// <summary>The system caption button the pointer is over, or null</summary>
        public eCaptionButton? HoveredCaptionButton => m_hoveredCaptionButton;

        /// <summary>The system caption button held down, or null</summary>
        public eCaptionButton? PressedCaptionButton => m_pressedCaptionButton;

        /// <summary>How the window is shown. Setting Unknown is ignored.</summary>
        public eWindowState WindowState
        {
            get => m_windowState;
            set
            {
                if (value == eWindowState.Unknown || value == m_windowState)
                {
                    return;
                }

                m_windowState = value;
                this.RaisePropertiesChanged(nameof(WindowState));
                this.ShowsWindowBorder = value != eWindowState.Maximized && value != eWindowState.FullScreened;
            }
        }

        /// <summary>Whether the window border shows: not while maximized or fullscreen</summary>
        public bool ShowsWindowBorder
        {
            get => m_showsWindowBorder;
            private set => this.SetAndRaiseIfChanged(ref m_showsWindowBorder, value);
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// The pointer moved onto, off of or pressed a system caption button: the buttons the OS draws, which the
        /// toolkit's own pointer events don't see. Null for none.
        /// </summary>
        public void SetSystemCaptionButtonPointer (eCaptionButton? hovered, eCaptionButton? pressed)
        {
            // Coming off a system button, the title bar zone recorded before it is stale: the title bar's own
            // pointer events report the pointer afresh from here
            if (m_hoveredCaptionButton.HasValue && !hovered.HasValue)
            {
                m_titleBarHover = eChromeHover.None;
            }

            m_hoveredCaptionButton = hovered;
            m_pressedCaptionButton = pressed;
            this.RaisePropertiesChanged(nameof(HoveredCaptionButton), nameof(PressedCaptionButton));
            this.RefreshState();
        }

        /// <summary>
        /// The pointer moved over the title bar's own chrome. Passed over while a system caption button has the pointer.
        /// </summary>
        public void SetTitleBarHover (eChromeHover hover)
        {
            if (m_hoveredCaptionButton.HasValue)
            {
                return;
            }

            m_titleBarHover = hover;
            this.RefreshState();
        }

        /// <summary>The pointer left the window's own area: its title bar zones no longer have the pointer</summary>
        public void ClearTitleBarHover ()
        {
            m_titleBarHover = eChromeHover.None;
            this.RefreshState();
        }

        /// <summary>
        /// Drops the title bar's close hover, for a window that no longer tracks its title bar (fullscreen, or going away).
        /// A system close button under the pointer still counts.
        /// </summary>
        public void ReleaseCloseHover ()
        {
            if (m_titleBarHover == eChromeHover.Close)
            {
                m_titleBarHover = eChromeHover.None;
                this.RefreshState();
            }
        }

        /// <summary>
        /// Picks what to draw for <paramref name="state"/>: <paramref name="inactive"/> while inactive,
        /// <paramref name="closeHover"/> while hovering close, and <paramref name="normal"/> otherwise, including in
        /// place of either of those when it is null.
        /// </summary>
        public static T PickForState<T> (eWindowChromeState state, T normal, T? inactive, T? closeHover)
            where T : class
        {
            switch (state)
            {
                case eWindowChromeState.Inactive: return inactive ?? normal;
                case eWindowChromeState.CloseHover: return closeHover ?? normal;
                default: return normal;
            }
        }

        // ===========[ Helper Methods ]========================================
        private void RefreshState ()
        {
            this.State = this.DeriveState();
            this.IsHoveringOverClose = m_state == eWindowChromeState.CloseHover;
        }

        private eWindowChromeState DeriveState ()
        {
            if (m_hoveredCaptionButton.HasValue)
            {
                return m_hoveredCaptionButton == eCaptionButton.Close ? eWindowChromeState.CloseHover : eWindowChromeState.ChromeButtonsHover;
            }

            switch (m_titleBarHover)
            {
                case eChromeHover.Close: return eWindowChromeState.CloseHover;
                case eChromeHover.ChromeButtons: return eWindowChromeState.ChromeButtonsHover;
                default: return m_isActivated ? eWindowChromeState.Normal : eWindowChromeState.Inactive;
            }
        }
    }
}
