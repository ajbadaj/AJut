namespace AJut.UX.Docking
{
    /// <summary>
    /// What a <see cref="DockZoneViewModel"/> tells its manager when something a layout saves of it changes (its panels,
    /// their order, its child zones, its orientation, or its selected tab), so a manager that auto-saves can save. Every
    /// <see cref="DockingManagerBase{TWindow, TZone}"/> is one.
    /// </summary>
    internal interface IDockLayoutChangeListener
    {
        /// <summary>Something a layout saves changed</summary>
        void OnDockLayoutChanged ();
    }
}
