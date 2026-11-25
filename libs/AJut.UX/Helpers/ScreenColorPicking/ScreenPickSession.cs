namespace AJut.UX.Helpers
{
    /// <summary>Where a pick from screen stands</summary>
    internal enum eScreenPickState
    {
        /// <summary>Still waiting on the user to pick or back out</summary>
        Picking,

        /// <summary>The user clicked a point, see <see cref="ScreenPickSession.PickedX"/> and <see cref="ScreenPickSession.PickedY"/></summary>
        Picked,

        /// <summary>The user backed out, or something took the pick away from them</summary>
        Canceled,
    }

    /// <summary>
    /// The rules of a pick from screen, kept apart from the overlay window that feeds it input: what picks, what cancels,
    /// and that once a pick has ended nothing changes how it ended.
    /// </summary>
    /// <remarks>
    /// Each click or key acts on its release, and only a release whose press the overlay saw counts. The press that
    /// started the pick (a button that acts on press, a key held down) can leave its release to arrive at the overlay,
    /// and acting on it would pick or cancel before the user did anything. Acting on the release also means the overlay
    /// is never torn down between a press and its release, which would hand the release to whatever window is under it.
    /// </remarks>
    internal sealed class ScreenPickSession
    {
        private bool m_isLeftButtonDown;
        private bool m_isRightButtonDown;
        private bool m_isEscapeDown;

        // ===========[ Properties ]===================================
        /// <summary>Where the pick stands</summary>
        public eScreenPickState State { get; private set; } = eScreenPickState.Picking;

        /// <summary>True once the pick has been made or canceled</summary>
        public bool IsDone => this.State != eScreenPickState.Picking;

        /// <summary>True while the left button is held during the pick, so a release would take the color under it</summary>
        public bool IsPressing => !this.IsDone && m_isLeftButtonDown;

        /// <summary>The x of the picked point, in the overlay's own coordinates, once <see cref="State"/> is Picked</summary>
        public int PickedX { get; private set; }

        /// <summary>The y of the picked point, in the overlay's own coordinates, once <see cref="State"/> is Picked</summary>
        public int PickedY { get; private set; }

        // ===========[ Public Interface Methods ]===================================
        public void LeftButtonDown ()
        {
            if (!this.IsDone)
            {
                m_isLeftButtonDown = true;
            }
        }

        /// <summary>Picks the point the button was let go at, if the overlay saw the press</summary>
        public void LeftButtonUp (int x, int y)
        {
            if (this.IsDone || !m_isLeftButtonDown)
            {
                return;
            }

            this.PickedX = x;
            this.PickedY = y;
            this.State = eScreenPickState.Picked;
        }

        public void RightButtonDown ()
        {
            if (!this.IsDone)
            {
                m_isRightButtonDown = true;
            }
        }

        /// <summary>Cancels, if the overlay saw the press</summary>
        public void RightButtonUp ()
        {
            if (!this.IsDone && m_isRightButtonDown)
            {
                this.State = eScreenPickState.Canceled;
            }
        }

        public void EscapeDown ()
        {
            if (!this.IsDone)
            {
                m_isEscapeDown = true;
            }
        }

        /// <summary>Cancels, if the overlay saw the press</summary>
        public void EscapeUp ()
        {
            if (!this.IsDone && m_isEscapeDown)
            {
                this.State = eScreenPickState.Canceled;
            }
        }

        /// <summary>
        /// Cancels because the pick was taken away from the user: the overlay lost the mouse or activation (Alt+Tab, the
        /// Windows key, a system prompt), or whatever started the pick went away.
        /// </summary>
        public void Interrupt ()
        {
            if (!this.IsDone)
            {
                this.State = eScreenPickState.Canceled;
            }
        }
    }
}
