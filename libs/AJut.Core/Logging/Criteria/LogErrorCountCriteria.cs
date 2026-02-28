namespace AJut
{
    /// <summary>
    /// A <see cref="LogScenarioCriteriaBase"/> that triggers when the cumulative error count
    /// since <see cref="InitiateScenario"/> was called reaches <see cref="CountThreshold"/>.
    /// Non-error log lines are ignored entirely.
    /// </summary>
    public class LogErrorCountCriteria : LogScenarioCriteriaBase
    {
        private long m_count;

        public long CountThreshold { get; set; } = 100;

        public override bool Evaluate (string message, bool isError)
        {
            if (isError)
            {
                ++m_count;
            }

            return m_count >= CountThreshold;
        }

        public override void InitiateScenario () => m_count = 0;

        public override void Reset () => m_count = 0;
    }
}
