namespace AJut.UX.Docking
{
    /// <summary>
    /// A tearoff window as a layout file keeps it: where it is and how big (in the units the toolkit places windows by,
    /// physical pixels from the top left on WinUI3), and whether it is maximized or full screen
    /// </summary>
    public readonly record struct DockWindowLayoutState (DockWindowBounds Bounds, bool IsMaximized, bool IsFullscreen);
}
