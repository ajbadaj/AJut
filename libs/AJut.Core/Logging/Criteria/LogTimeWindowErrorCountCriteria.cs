namespace AJut
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A <see cref="LogScenarioCriteriaBase"/> that triggers when the number of errors logged
    /// within a sliding <see cref="TimeWindow"/> reaches <see cref="CountThreshold"/>.
    /// Non-error log lines are ignored entirely.
    /// <para/>
    /// Internally maintains a queue of <see cref="DateTime.UtcNow"/> ticks per error, evicting
    /// entries that fall outside the window on each evaluation. Each entry is 8 bytes; for
    /// errors-only this is negligible in practice, but be mindful when choosing a large window
    /// in error-heavy scenarios.
    /// </summary>
    public class LogTimeWindowErrorCountCriteria : LogScenarioCriteriaBase
    {
        private readonly Queue<long> m_errorTimestamps = new Queue<long>();

        public long CountThreshold { get; set; } = 100;
        public TimeSpan TimeWindow { get; set; } = TimeSpan.FromSeconds(1);

        public override bool Evaluate (string message, bool isError)
        {
            if (!isError)
            {
                return false;
            }

            long now = DateTime.UtcNow.Ticks;
            long windowTicks = TimeWindow.Ticks;

            while (m_errorTimestamps.Count > 0 && (now - m_errorTimestamps.Peek()) > windowTicks)
            {
                m_errorTimestamps.Dequeue();
            }

            m_errorTimestamps.Enqueue(now);
            return m_errorTimestamps.Count >= CountThreshold;
        }

        public override void InitiateScenario () => m_errorTimestamps.Clear();

        public override void Reset () => m_errorTimestamps.Clear();
    }
}
