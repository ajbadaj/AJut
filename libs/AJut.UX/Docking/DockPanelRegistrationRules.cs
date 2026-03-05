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
    }
}
