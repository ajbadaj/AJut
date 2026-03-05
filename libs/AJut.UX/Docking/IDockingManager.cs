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

        /// <summary>
        /// Minimum pixel dimension (width or height) that a dock zone panel may occupy
        /// when sizes are recalculated after add/remove. Default: 50.
        /// </summary>
        double MinPanelDimension { get; set; }

        bool LoadDockLayoutFromFile(string filePath);
        bool SaveDockLayoutToFile(string filePath = null);

        bool SaveDockLayoutToPersistentStorage();
        bool ReloadDockLayoutFromPersistentStorage();

        /// <summary>Observable state broker for panel add/remove/show/hide UI.</summary>
        DockPanelAddRemoveUISync UISyncVM { get; }

        /// <summary>
        /// Create and dock a new panel of the given type. Uses <see cref="DockPanelRegistrationRules.DefaultGroupId"/>
        /// to find a preferred zone, falling back to the first available leaf zone.
        /// </summary>
        void AddPanel (Type panelType);

        /// <summary>
        /// For single-instance types: show if hidden, hide if visible.
        /// For multi-instance types: equivalent to <see cref="AddPanel"/>.
        /// </summary>
        void TogglePanel (Type panelType);

        /// <summary>
        /// Close or hide the given panel based on its <see cref="DockingContentAdapterModel.HideDontClose"/> setting.
        /// </summary>
        void RemoveOrHidePanel (DockingContentAdapterModel adapter);

        // ===========[ Platform Hooks for Shared Logic ]===================================

        /// <summary>
        /// Returns the registration rules for a panel type, or null if not registered.
        /// </summary>
        DockPanelRegistrationRules? GetPanelRules (Type panelType);

        /// <summary>
        /// Find the best available dock zone for the given group ID. Falls back to the first
        /// available leaf zone if no group match. Returns null if no zones exist.
        /// </summary>
        DockZoneViewModel FindTargetZoneForGroup (string groupId);

        /// <summary>
        /// Called when a panel is being hidden. Returns platform-specific state (e.g. tearoff
        /// window reference, position, size) that can be used to restore the panel later.
        /// Returns null if no platform-specific state needs to be captured (e.g. panel is in main window).
        /// </summary>
        HiddenPanelPlatformState CaptureHideState (DockingContentAdapterModel adapter);

        /// <summary>
        /// Attempts to restore a hidden panel to its previous location using the platform-specific
        /// state captured by <see cref="CaptureHideState"/>. Returns true if successful.
        /// </summary>
        bool TryRestoreFromHideState (object hideState, DockingContentAdapterModel adapter);

        /// <summary>
        /// Called after a panel has been hidden and removed from its zone. Performs platform-specific
        /// cleanup such as closing an empty tearoff window.
        /// </summary>
        void AfterPanelHidden (object hideState);

        /// <summary>
        /// Creates a new tearoff window and docks the given adapter into it. Used as a fallback
        /// when restoring a hidden panel whose original tearoff is gone.
        /// </summary>
        bool CreateTearoffForPanel (DockingContentAdapterModel adapter, double x, double y, double width, double height);

        /// <summary>
        /// Returns true if <paramref name="zone"/> is the root zone of a tearoff window that
        /// would become orphaned (empty) if its content were removed. Callers should close the
        /// tearoff window after performing the operation (e.g. after a single-element tear-off).
        /// </summary>
        bool IsTearoffRootThatWouldOrphan (DockZoneViewModel zone);

        /// <summary>
        /// Close the tearoff window that hosts the given root zone, if any.
        /// </summary>
        void CloseTearoffForRootZone (DockZoneViewModel rootZone);
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
