namespace AJut
{
    /// <summary>
    /// A <see cref="LogScenarioCriteriaBase"/> that triggers when the cumulative count
    /// of log lines (of any type) since <see cref="InitiateScenario"/> was called
    /// reaches <see cref="CountThreshold"/>. Re-entrant: resets automatically on each
    /// scenario activation so it can fire unlimited times.
    /// </summary>
    /// <remarks>
    /// As an exit criteria, a threshold of N lets through the line that activated the scenario plus the N-1 lines after it,
    /// since the line that reaches the count is the one that closes the window. So a threshold of 1 is exactly the
    /// activating line.
    /// </remarks>
    public class LogCountCriteria : LogScenarioCriteriaBase
    {
        private long m_count;

        public long CountThreshold { get; set; } = 5;

        public override bool Evaluate (string message, bool isError)
            => ++m_count >= CountThreshold;

        public override void InitiateScenario () => m_count = 0;

        public override void Reset () => m_count = 0;
    }
}
