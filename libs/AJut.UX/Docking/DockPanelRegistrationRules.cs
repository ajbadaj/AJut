namespace AJut.UX.Docking
{
    /// <summary>
    /// Rules that govern panel creation policy, applied at factory registration time.
    /// These control how many instances can exist and where new panels are placed.
    /// </summary>
    public record struct DockPanelRegistrationRules
    {
        /// <summary>When true, only one instance of this panel type can exist at a time.</summary>
        public bool SingleInstanceOnly { get; init; }

        /// <summary>
        /// When true, if no instance of this panel type exists after a layout load (or on
        /// first launch), the manager automatically creates one in the first matching zone.
        /// </summary>
        public bool GuaranteedOnStart { get; init; }

        /// <summary>
        /// Hint for which root zone this panel type prefers. Matches against the GroupId
        /// of registered root zones. Falls back to the first available leaf zone if no match.
        /// Null means no preference.
        /// </summary>
        public string DefaultGroupId { get; init; }

        /// <summary>
        /// Default width for a tearoff/spawn window when no previous size is remembered.
        /// Zero or negative means no preference (platform will use a default).
        /// </summary>
        public double SpawnWidth { get; init; }

        /// <summary>
        /// Default height for a tearoff/spawn window when no previous size is remembered.
        /// Zero or negative means no preference (platform will use a default).
        /// </summary>
        public double SpawnHeight { get; init; }

        /// <summary>When true, this panel type will not appear in the <see cref="DockPanelAddRemoveToolbar"/>.</summary>
        public bool IsHiddenFromToolbar { get; init; }

        /// <summary>When true, this panel type will not appear in menus managed by <c>DockingManager.ManageMenu</c>.</summary>
        public bool IsHiddenFromMenu { get; init; }

        /// <summary>
        /// Factory default for <see cref="DockingContentAdapterModel.IsClosable"/>. Applied before
        /// the panel's <c>Setup()</c> runs, so Setup can still override. Null means no factory opinion.
        /// </summary>
        public bool? DefaultIsClosable { get; init; }

        /// <summary>
        /// Factory default for <see cref="DockingContentAdapterModel.HideDontClose"/>. Applied before
        /// the panel's <c>Setup()</c> runs, so Setup can still override. Null means no factory opinion.
        /// </summary>
        public bool? DefaultHideDontClose { get; init; }

        /// <summary>
        /// Factory default for <see cref="DockingContentAdapterModel.CanTearoff"/>. Applied before
        /// the panel's <c>Setup()</c> runs, so Setup can still override. Null means no factory opinion.
        /// </summary>
        public bool? DefaultCanTearoff { get; init; }
    }
}
