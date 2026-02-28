namespace AJut
{
    using System;

    /// <summary>
    /// A <see cref="LogScenarioCriteriaBase"/> that activates after a specified duration has elapsed
    /// since <see cref="InitiateScenario"/> was called. The message and isError parameters are ignored -
    /// only elapsed time matters. Passing an empty string from a timer tick (<see cref="LogVerbosityManager.EvaluateAllCriteria"/>)
    /// works identically to being driven by a real log line.
    /// </summary>
    public class LogTimeCriteria : LogScenarioCriteriaBase
    {
        private DateTime? m_armedAt;

        public TimeSpan Duration { get; set; }

        public override bool Evaluate (string message, bool isError)
            => m_armedAt.HasValue && (DateTime.Now - m_armedAt.Value) >= Duration;

        public override void InitiateScenario () => m_armedAt = DateTime.Now;

        public override void Reset () => m_armedAt = null;
    }
}
