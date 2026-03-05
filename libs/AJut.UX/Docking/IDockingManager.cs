namespace AJut.UX.Docking
{
    using AJut.Tree;
    using System;

    /// <summary>
    /// Minimal contract that <see cref="DockZoneViewModel"/> needs from a docking manager.
    /// Implemented by the platform-specific DockingManager in each UI framework assembly.
    /// </summary>
    public interface IDockingManager
    {
        IDockableDisplayElement BuildNewDisplayElement (Type elementType);
        IEnumerable<DockZoneViewModel> GetAllRoots();

        bool LoadDockLayoutFromFile(string filePath);
        bool SaveDockLayoutToFile(string filePath = null);

        bool SaveDockLayoutToPersistentStorage();
        bool ReloadDockLayoutFromPersistentStorage();
    }

    public static class DockingManagerXT
    {
        /// <summary>
        /// Enumerate all the adapters docked in this docking manager (1 adapter for every 1 display)
        /// </summary>
        public static IEnumerable<DockingContentAdapterModel> EnumerateAdapters(this IDockingManager dockingManager)
        {
            return dockingManager.GetAllRoots()
                                 .SelectMany(dzvm => TreeTraversal<DockZoneViewModel>.All(dzvm))
                                 .SelectMany(dzvm => dzvm.DockedContent);
        }

        /// <summary>
        /// Enumerate all displays that are docked in this docking manager
        /// </summary>
        public static IEnumerable<IDockableDisplayElement> EnumerateDisplays(this IDockingManager dockingManager)
        {
            foreach (DockingContentAdapterModel adapter in dockingManager.EnumerateAdapters())
            {
                yield return adapter.Display;
            }
        }
    }
}
