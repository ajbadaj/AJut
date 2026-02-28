namespace AJut
{
    /// <summary>
    /// Abstract base for criteria used by <see cref="LogVerbosityScenario"/> to determine when to enter or exit an active state.
    /// </summary>
    public abstract class LogScenarioCriteriaBase
    {
        /// <summary>
        /// Evaluate this criteria against a log line (or an empty string when driven by a timer tick rather than a log line).
        /// Returns true when the criteria is satisfied.
        /// </summary>
        public abstract bool Evaluate (string message, bool isError);

        /// <summary>
        /// Called when this criteria begins active evaluation - for example, when the scenario is armed for entry
        /// or when the scenario has just activated and exit criteria start being watched.
        /// Time-based criteria start their clock here; match-count criteria reset their counter here.
        /// </summary>
        public abstract void InitiateScenario ();

        /// <summary>
        /// Reset all internal state as if never evaluated. For time criteria this clears the clock entirely
        /// (distinct from <see cref="InitiateScenario"/> which starts a new clock).
        /// </summary>
        public abstract void Reset ();
    }
}
