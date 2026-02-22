namespace AJut.UX.Docking
{
    using System;

    /// <summary>
    /// Minimal contract that <see cref="DockZoneViewModel"/> needs from a docking manager.
    /// Implemented by the platform-specific DockingManager in each UI framework assembly.
    /// </summary>
    public interface IDockingManager
    {
        IDockableDisplayElement BuildNewDisplayElement (Type elementType);
    }
}
