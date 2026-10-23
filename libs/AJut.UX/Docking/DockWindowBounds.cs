namespace AJut.UX.Docking
{
    /// <summary>
    /// Where a docking window sits and how big it is, in whatever units the toolkit's window placement uses (WinUI3
    /// places windows in physical pixels, WPF in device independent units)
    /// </summary>
    public readonly record struct DockWindowBounds (double X, double Y, double Width, double Height);
}
