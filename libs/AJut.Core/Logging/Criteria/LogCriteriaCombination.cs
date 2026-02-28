namespace AJut
{
    using System.Collections.Generic;

    /// <summary>
    /// A <see cref="LogScenarioCriteriaBase"/> that combines child criteria using either AND or OR logic.
    /// Can be nested to build arbitrary trees (e.g. AND of an OR with another criterion).
    /// All children are always evaluated so stateful criteria (e.g. <see cref="LogTextMatchCriteria"/>
    /// with <see cref="LogTextMatchCriteria.RequiredMatchCount"/>) accumulate state on every call
    /// regardless of short-circuit outcome.
    /// </summary>
    public class LogCriteriaCombination : LogScenarioCriteriaBase
    {
        /// <summary>
        /// Default constructor - allows normal construction for serialization scenarios.
        /// </summary>
        public LogCriteriaCombination ()
        {
        }

        public eLogCombination Combination { get; set; }
        public List<LogScenarioCriteriaBase> Criteria { get; } = new List<LogScenarioCriteriaBase>();

        /// <summary>
        /// Creates a combination that requires all of the given criteria to be satisfied.
        /// </summary>
        public static LogCriteriaCombination And (params LogScenarioCriteriaBase[] criteria)
        {
            var combo = new LogCriteriaCombination { Combination = eLogCombination.And };
            combo.Criteria.AddRange(criteria);
            return combo;
        }

        /// <summary>
        /// Creates a combination that requires any one of the given criteria to be satisfied.
        /// </summary>
        public static LogCriteriaCombination Or (params LogScenarioCriteriaBase[] criteria)
        {
            var combo = new LogCriteriaCombination { Combination = eLogCombination.Or };
            combo.Criteria.AddRange(criteria);
            return combo;
        }

        public override bool Evaluate (string message, bool isError)
        {
            // Evaluate every child (no short-circuit) so stateful criteria always accumulate
            bool result = Combination == eLogCombination.And;
            foreach (var c in Criteria)
            {
                bool r = c.Evaluate(message, isError);
                result = Combination == eLogCombination.And ? result && r : result || r;
            }
            return result;
        }

        public override void InitiateScenario ()
        {
            foreach (var c in Criteria)
            {
                c.InitiateScenario();
            }
        }

        public override void Reset ()
        {
            foreach (var c in Criteria)
            {
                c.Reset();
            }
        }
    }
}
