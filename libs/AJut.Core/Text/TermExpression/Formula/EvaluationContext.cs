namespace AJut.Text
{
    using System.Collections.Generic;

    /// <summary>
    /// Everything one evaluation of a formula carries while it runs: the values of its steps, where its host values are, how
    /// much work it's done, and what went wrong if something did.
    /// </summary>
    /// <remarks>
    /// Nothing an author writes can throw, so a problem is recorded here and every node hands back as soon as it sees one,
    /// rather than unwinding with an exception. Only the first problem is kept.
    /// </remarks>
    internal sealed class EvaluationContext
    {
        private readonly TermValues m_values;
        private readonly int[] m_hostIndices;
        private readonly long m_maxWork;
        private long m_work;
        private string m_failureMessage;
        private int m_failureIndex;
        private string m_failureFunction;
        private string m_chain;
        private TermError m_callSiteFailure;

        // ===========[ Setup/Construction/Teardown ]==========================

        /// <summary>
        /// hostIndices maps each host value the formula reads to where it is in values
        /// </summary>
        public EvaluationContext (TermValues values, int[] hostIndices, int maxWork, int frameSize)
        {
            m_values = values;
            m_hostIndices = hostIndices;
            m_maxWork = maxWork;
            this.Frame = new TermValue[frameSize];
        }

        // ===========[ Properties ]===========================================

        /// <summary>
        /// The steps (and parameters, inside a function) of whatever is running now. A call swaps in its own and puts the
        /// caller's back when it returns.
        /// </summary>
        public TermValue[] Frame { get; set; }

        /// <summary>
        /// The table function whose body is running, or null for the formula itself
        /// </summary>
        public string CurrentFunction { get; set; }

        public bool HasFailed { get; private set; }

        // ===========[ Public Interface Methods ]=============================

        /// <summary>
        /// Counts work toward the evaluation's limit, failing it once the limit is passed. Function calls are what can make a
        /// short formula do a huge amount of work, so they count their whole body every time they run, lists count each
        /// item, and text counts each character it makes.
        /// </summary>
        public bool Charge (int amount, int index)
        {
            m_work += amount;
            if (m_work <= m_maxWork)
            {
                return true;
            }

            this.Fail(index, "This took too much work to figure out.");
            return false;
        }

        /// <summary>
        /// Records a problem at <paramref name="index"/> in whatever's running, and gives back a value for the node to hand
        /// back, which nothing will read.
        /// </summary>
        public TermValue Fail (int index, string message)
        {
            if (!this.HasFailed)
            {
                this.HasFailed = true;
                m_failureMessage = message;
                m_failureIndex = index;
                m_failureFunction = this.CurrentFunction;
            }

            return default;
        }

        /// <summary>
        /// Called by a function call whose body failed, on the way back out. Each call adds itself to the story, and the one
        /// in the formula itself becomes the error shown at the call: "share($gold, 0) at position 5: inside share, ...".
        /// </summary>
        public void ReportFailedCall (string functionName, string callText, int callIndex, bool isInFormula)
        {
            m_chain = $"inside {functionName}, {m_chain ?? FormulaMessages.LowerFirst(m_failureMessage)}";
            if (isInFormula)
            {
                m_callSiteFailure = new TermError($"{callText} at {FormulaMessages.At(callIndex)}: {m_chain}", callIndex);
            }
        }

        public bool IsHostSet (int hostSlot) => m_values.IsSetAt(m_hostIndices[hostSlot]);

        public bool TryReadHost (int hostSlot, out TermValue value)
        {
            int index = m_hostIndices[hostSlot];
            if (!m_values.IsSetAt(index))
            {
                value = default;
                return false;
            }

            value = m_values.GetAt(index);
            return true;
        }

        /// <summary>
        /// What went wrong, as the errors a <see cref="TermEvaluation"/> reports: the problem where it happened, and, when
        /// that was inside a function, the call in the formula that led there first.
        /// </summary>
        public IReadOnlyList<TermError> BuildErrors ()
        {
            var inner = new TermError(m_failureMessage, m_failureIndex, m_failureFunction);
            return m_callSiteFailure == null ? new[] { inner } : new[] { m_callSiteFailure, inner };
        }
    }
}
