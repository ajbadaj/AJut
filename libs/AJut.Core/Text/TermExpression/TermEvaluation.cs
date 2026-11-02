namespace AJut.Text
{
    using System;
    using System.Collections.Generic;
    using AJut.Storage;

    /// <summary>
    /// What <see cref="TermFormula.Evaluate"/> gives back: the value, or what went wrong and where. Only what the values
    /// themselves cause can go wrong here (dividing by zero, lists of different lengths, a value the host didn't supply),
    /// since everything else is caught when the formula is parsed.
    /// </summary>
    public sealed class TermEvaluation : Result<TermValue>
    {
        private readonly TermFormula m_formula;
        private readonly TermValues m_values;
        private readonly TermValue[] m_steps;

        // ===========[ Setup/Construction/Teardown ]==========================

        internal TermEvaluation (TermValue value, TermFormula formula, TermValues values, TermValue[] steps) : base(value)
        {
            m_formula = formula;
            m_values = values;
            m_steps = steps;
            this.ErrorIndex = -1;
            this.TermErrors = Array.Empty<TermError>();
        }

        internal TermEvaluation (IReadOnlyList<TermError> errors)
        {
            foreach (TermError error in errors)
            {
                this.AddError(error.Message);
            }

            this.TermErrors = errors;
            this.ErrorIndex = errors[0].Index;
        }

        // ===========[ Properties ]===========================================

        /// <summary>
        /// Where the problem is, as an index (from 0) into the formula, or -1 when the evaluation succeeded. When the problem
        /// was inside a table function, this is the call in the formula that led there, and <see cref="TermErrors"/> also has
        /// the spot in the function's body.
        /// </summary>
        public int ErrorIndex { get; }

        /// <summary>
        /// Every error, with where each one is.
        /// </summary>
        public IReadOnlyList<TermError> TermErrors { get; }

        // ===========[ Public Interface Methods ]=============================

        /// <summary>
        /// The values this evaluation had, plus every step the formula set and the result under <paramref name="name"/>, ready
        /// for a template parsed with <see cref="TermFormula.DeclarationsWithResult"/> of the same name.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the evaluation failed</exception>
        /// <exception cref="ArgumentException">When <paramref name="name"/> is already a value or a step's name</exception>
        public TermValues ValuesWithResult (string name)
        {
            if (this.HasErrors)
            {
                throw new InvalidOperationException("The evaluation failed, so there's no result to carry on with");
            }

            return m_formula.BuildValuesWithResult(name, m_values, m_steps, this.Value);
        }
    }
}
