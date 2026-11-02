namespace AJut.Text
{
    using System;

    /// <summary>
    /// A parsed formula or template, ready to evaluate as often as needed. Parse one with
    /// <see cref="TermExpression.ParseFormula"/> or <see cref="TermExpression.ParseTemplate"/>.
    /// </summary>
    /// <remarks>
    /// Never changes once parsed, so one instance can be evaluated from any number of threads at once, each with its own
    /// <see cref="TermValues"/>. Steps a formula sets live in each evaluation, never in the values handed in.
    /// </remarks>
    public sealed class TermFormula
    {
        private readonly FormulaProgram m_program;
        private readonly TermDeclarations.Entry[] m_hostReads;
        private readonly CheckedFormula.StepResult[] m_steps;
        private readonly int m_cost;
        private readonly bool m_ignoreNameCase;
        private readonly object m_withResultLock = new object();
        private string m_withResultName;
        private TermDeclarations m_withResult;

        // ===========[ Setup/Construction/Teardown ]==========================

        internal TermFormula (CheckedFormula checkedFormula, TermDeclarations declarations, string text, bool ignoreNameCase)
        {
            m_program = checkedFormula.Program;
            m_hostReads = checkedFormula.HostReads;
            m_steps = checkedFormula.Steps;
            m_cost = checkedFormula.Cost;
            m_ignoreNameCase = ignoreNameCase;
            this.ResultKind = checkedFormula.ResultKind;
            this.Declarations = declarations;
            this.Text = text;
        }

        // ===========[ Properties ]===========================================

        /// <summary>
        /// What the formula was parsed against.
        /// </summary>
        public TermDeclarations Declarations { get; }

        /// <summary>
        /// What evaluating gives back: the kind the parse wanted, or, when it wanted none, the kind of the last line.
        /// </summary>
        public eTermKind ResultKind { get; }

        /// <summary>
        /// The text it was parsed from.
        /// </summary>
        public string Text { get; }

        // ===========[ Public Interface Methods ]=============================

        /// <summary>
        /// Evaluates the formula with <paramref name="values"/>. Problems the values cause come back in the result rather
        /// than as exceptions.
        /// </summary>
        /// <param name="values">Made from <see cref="Declarations"/>, or from any declarations that declare every value this
        /// formula reads, as the same kind</param>
        /// <param name="options">Limits on the evaluation</param>
        /// <exception cref="ArgumentException">When <paramref name="values"/> is missing a value this formula reads, or has it
        /// as another kind</exception>
        public TermEvaluation Evaluate (TermValues values, TermEvaluateOptions options = default)
        {
            ArgumentNullException.ThrowIfNull(values);
            TermEvaluateOptions.ThrowIfOutOfRange(options);

            int[] hostIndices = this.MapHostReads(values);
            var context = new EvaluationContext(values, hostIndices, options.MaxWorkOrDefault, m_program.SlotCount);
            TermValue result = context.Charge(m_cost, 0) ? m_program.Run(context) : default;
            return context.HasFailed
                ? new TermEvaluation(context.BuildErrors())
                : new TermEvaluation(result, this, values, context.Frame);
        }

        /// <summary>
        /// These declarations plus every step this formula sets and its result as <paramref name="resultName"/>, for parsing a
        /// separate template that shows them. Evaluate the template with <see cref="TermEvaluation.ValuesWithResult"/> of the
        /// same name.
        /// </summary>
        /// <remarks>
        /// <para>A step with the same name as the result gives way to it, since the steps are the author's to name and the
        /// result's name is the host's: a formula ending <c>$total = sum($scores)</c> still works with a result called
        /// "total", and the template's <c>$total</c> is the result.</para>
        /// <para>Asking again with the same name gives back the same declarations, which can't be added to, so every evaluation's
        /// values are made from one set.</para>
        /// </remarks>
        /// <exception cref="ArgumentException">When <paramref name="resultName"/> isn't a name a formula can use, or is a value
        /// the host declared</exception>
        public TermDeclarations DeclarationsWithResult (string resultName)
        {
            if (!FormulaLexer.IsValidName(resultName, isFunction: false))
            {
                throw new ArgumentException(
                    $"'{resultName}' isn't a name a formula can use: use letters, digits, and '_'",
                    nameof(resultName)
                );
            }

            lock (m_withResultLock)
            {
                if (resultName == m_withResultName)
                {
                    return m_withResult;
                }

                if (this.Declarations.TryFind(resultName, m_ignoreNameCase, out TermDeclarations.Entry clash))
                {
                    throw new ArgumentException(
                        $"'{resultName}' is already a value the host declares, '{clash.Name}'",
                        nameof(resultName)
                    );
                }

                var declarations = new TermDeclarations(this.Declarations.Functions);
                for (int index = 0; index < this.Declarations.Count; ++index)
                {
                    TermDeclarations.Entry entry = this.Declarations[index];
                    declarations.AddEntry(entry.Name, entry.Kind, entry.IsOptional, entry.Print);
                }

                foreach (CheckedFormula.StepResult step in m_steps)
                {
                    if (!this.IsSameName(step.Name, resultName))
                    {
                        declarations.AddEntry(step.Name, step.Kind, optional: false, print: null);
                    }
                }

                declarations.AddEntry(resultName, this.ResultKind, optional: false, print: null);
                declarations.Freeze(m_ignoreNameCase);
                m_withResultName = resultName;
                m_withResult = declarations;
                return declarations;
            }
        }

        // ===========[ Internal Interface Methods ]===========================

        internal TermValues BuildValuesWithResult (string resultName, TermValues values, TermValue[] steps, TermValue result)
        {
            TermDeclarations declarations = this.DeclarationsWithResult(resultName);
            TermValues extended = declarations.CreateValues();

            // Host values as they were set, which declarations other than the formula's own may hold in another order
            for (int index = 0; index < this.Declarations.Count; ++index)
            {
                TermDeclarations.Entry entry = this.Declarations[index];
                if (
                    values.Declarations.TryFind(entry.Name, ignoreCase: false, out TermDeclarations.Entry given)
                    && values.IsSetAt(given.Index)
                )
                {
                    extended.SetComputed(index, values.GetAt(given.Index));
                }
            }

            foreach (CheckedFormula.StepResult step in m_steps)
            {
                if (!this.IsSameName(step.Name, resultName))
                {
                    declarations.TryFind(step.Name, ignoreCase: false, out TermDeclarations.Entry stepEntry);
                    extended.SetComputed(stepEntry.Index, steps[step.Slot]);
                }
            }

            extended.SetComputed(declarations.Count - 1, result);
            return extended;
        }

        // ===========[ Helpers ]==============================================

        private bool IsSameName (string left, string right)
        {
            return String.Equals(left, right, m_ignoreNameCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        // Where each host value this formula reads is in values, which can come from declarations other than its own
        private int[] MapHostReads (TermValues values)
        {
            var indices = new int[m_hostReads.Length];
            bool isOwn = ReferenceEquals(values.Declarations, this.Declarations);
            for (int index = 0; index < indices.Length; ++index)
            {
                TermDeclarations.Entry wanted = m_hostReads[index];
                if (isOwn)
                {
                    indices[index] = wanted.Index;
                    continue;
                }

                if (
                    !values.Declarations.TryFind(wanted.Name, ignoreCase: false, out TermDeclarations.Entry given)
                    || (given.Kind != wanted.Kind)
                )
                {
                    throw new ArgumentException(
                        $"The values don't declare '{wanted.Name}' as {wanted.Kind}, which this formula reads",
                        nameof(values)
                    );
                }

                indices[index] = given.Index;
            }

            return indices;
        }
    }
}
