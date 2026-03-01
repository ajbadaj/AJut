namespace AJut
{
    /// <summary>
    /// A <see cref="LogScenarioCriteriaBase"/> that triggers when the cumulative count
    /// of log lines (of any type) since <see cref="InitiateScenario"/> was called
    /// reaches <see cref="CountThreshold"/>. Re-entrant: resets automatically on each
    /// scenario activation so it can fire unlimited times.
    /// </summary>
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
