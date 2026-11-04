namespace AJut.UX
{
    /// <summary>How a window is shown</summary>
    public enum eWindowState
    {
        /// <summary>Not known yet, or the window is going away</summary>
        Unknown,

        /// <summary>Filling the screen's work area, with its title bar</summary>
        Maximized,

        /// <summary>Minimized to the taskbar or dock</summary>
        Minimized,

        /// <summary>A normal, sizable window</summary>
        Restored,

        /// <summary>Filling the whole screen with no title bar</summary>
        FullScreened,
    }
}
