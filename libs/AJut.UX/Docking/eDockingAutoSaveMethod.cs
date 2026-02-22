namespace AJut.UX.Docking
{
    public enum eDockingAutoSaveMethod
    {
        /// <summary>Never auto save.</summary>
        None,

        /// <summary>
        /// Auto save to the <see cref="DockingManager.DockLayoutPersistentStorageFile"/> whenever anything happens.
        /// </summary>
        AutoSaveOnAllChanges,

        /// <summary>
        /// Auto save to a temp file next to <see cref="DockingManager.DockLayoutPersistentStorageFile"/> whenever
        /// anything happens, but wait for explicit calls to save to the primary file.
        /// </summary>
        AutoSaveToTemp,
    }
}
