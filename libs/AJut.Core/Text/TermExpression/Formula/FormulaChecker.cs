namespace AJut.Text
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Turns parsed <see cref="FormulaSyntax"/> into <see cref="FormulaNode"/>s, finding every name and working out the kind
    /// of every piece from what the host declared. Everything an author can get wrong that doesn't depend on the values
    /// themselves is caught here, while they type, rather than only for some values and not others.
    /// </summary>
    /// <remarks>
    /// <para>One checker checks one text: a formula, a template, or a table function's body for one set of argument kinds.
    /// It refuses at the first problem, like the parser.</para>
    /// <para>Nesting is counted the same way the parser counts it, on top of the depth the text is called at. A formula
    /// starts at 0, and a function's body starts one past the call that reaches it, so nesting and functions calling
    /// functions share the one <see cref="TermParseOptions.MaxDepth"/> budget, and every recursion while checking or
    /// evaluating stays inside it however the depth was reached.</para>
    /// </remarks>
    internal sealed partial class FormulaChecker
    {
        // A call quoted in a message is cut short past this, so a huge call doesn't make a huge message
        private const int kLongestQuotedCall = 60;

        private readonly string m_text;
        private readonly TermDeclarations m_declarations;
        private readonly TermFunctions m_functions;
        private readonly bool m_ignoreNameCase;
        private readonly int m_maxDepth;
        private readonly int m_baseDepth;
        private readonly string m_functionName;
        private readonly List<eTermKind> m_slotKinds = new List<eTermKind>();
        private readonly Dictionary<string, int> m_slotByName;
        private readonly HashSet<string> m_parameterNames;
        private readonly Dictionary<string, List<int>> m_stepPositions;
        private readonly List<TermDeclarations.Entry> m_hostReads = new List<TermDeclarations.Entry>();
        private readonly Dictionary<string, int> m_hostSlotByName = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<CheckedOptional> m_checkedOptionals = new List<CheckedOptional>();
        private int m_depth;
        private int m_deepest;
        private int m_nodeCount;
        private TermError m_error;
        private TermError m_innerError;
        private string m_chain;
        private bool m_failedOnDepth;

        // ===========[ Setup/Construction/Teardown ]==========================

        private FormulaChecker (
            string text,
            TermDeclarations declarations,
            TermFunctions functions,
            bool ignoreNameCase,
            int maxDepth,
            int baseDepth,
            string functionName
        )
        {
            m_text = text;
            m_declarations = declarations;
            m_functions = functions;
            m_ignoreNameCase = ignoreNameCase;
            m_maxDepth = maxDepth;
            m_baseDepth = baseDepth;
            m_functionName = functionName;

            StringComparer comparer = ignoreNameCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            m_slotByName = new Dictionary<string, int>(comparer);
            m_parameterNames = new HashSet<string>(comparer);
            m_stepPositions = new Dictionary<string, List<int>>(comparer);
        }

        // ===========[ Public Interface Methods ]=============================

        public static CheckedFormula CheckFormula (
            string text,
            List<FormulaSyntax> statements,
            TermDeclarations declarations,
            TermParseOptions options,
            eTermKind? wants
        )
        {
            var checker = new FormulaChecker(
                text,
                declarations,
                declarations.Functions,
                options.IgnoreNameCase,
                options.MaxDepthOrDefault,
                0,
                null
            );
            return checker.CheckStatements(statements, wants);
        }

        public static CheckedFormula CheckTemplate (
            string text,
            FormulaSyntax.Text template,
            TermDeclarations declarations,
            TermParseOptions options
        )
        {
            var checker = new FormulaChecker(
                text,
                declarations,
                declarations.Functions,
                options.IgnoreNameCase,
                options.MaxDepthOrDefault,
                0,
                null
            );
            return checker.CheckStatements(new List<FormulaSyntax> { template }, eTermKind.String);
        }

        /// <summary>
        /// Checks a table function's body with its parameters as <paramref name="argumentKinds"/>, starting at
        /// <paramref name="baseDepth"/>.
        /// </summary>
        public static CheckedFormula CheckFunctionBody (
            TermFunctions functions,
            TermFunctions.Entry entry,
            eTermKind[] argumentKinds,
            int baseDepth,
            int maxDepth
        )
        {
            var checker = new FormulaChecker(
                entry.Body,
                null,
                functions,
                functions.IgnoresNameCase,
                maxDepth,
                baseDepth,
                entry.Name
            );
            for (int index = 0; index < argumentKinds.Length; ++index)
            {
                string parameter = entry.Parameters[index];
                checker.m_slotByName[parameter] = checker.AddSlot(argumentKinds[index]);
                checker.m_parameterNames.Add(parameter);
            }

            return checker.CheckStatements(entry.Statements, wants: null);
        }

        // ===========[ Helpers ]==============================================

        // ------ Statements

        private CheckedFormula CheckStatements (List<FormulaSyntax> statements, eTermKind? wants)
        {
            // 1. Where each step is set, so a use before its line can say where it's set
            foreach (FormulaSyntax statement in statements)
            {
                if (statement is FormulaSyntax.Step step)
                {
                    if (!m_stepPositions.TryGetValue(step.Name, out List<int> positions))
                    {
                        positions = new List<int>();
                        m_stepPositions.Add(step.Name, positions);
                    }

                    positions.Add(step.Start);
                }
            }

            // 2. Each statement in order, a step's name only taking effect once its value is checked, so $x = $x + 1 reads
            //  the $x from before it
            var nodes = new List<FormulaNode>(statements.Count);
            var slots = new List<int>(statements.Count);
            for (int index = 0; index < statements.Count; ++index)
            {
                bool isLast = index == statements.Count - 1;
                FormulaSyntax statement = statements[index];
                if (statement is FormulaSyntax.Step step)
                {
                    if (!this.CanBeStepName(step))
                    {
                        return this.Failed();
                    }

                    FormulaNode value = this.Check(step.Value, isPrinted: false);
                    if (value == null)
                    {
                        return this.Failed();
                    }

                    int slot = this.AddSlot(value.Kind);
                    m_slotByName[step.Name] = slot;
                    nodes.Add(value);
                    slots.Add(slot);
                    continue;
                }

                FormulaNode node = this.Check(statement, isPrinted: isLast && (wants == eTermKind.String));
                if (node == null)
                {
                    return this.Failed();
                }

                nodes.Add(node);
                slots.Add(-1);
            }

            // 3. The last line is the result, in the kind the host wants. A last line that sets a step gives that step's
            //  value, read back as a line of its own when it needs converting, so the step keeps its own kind.
            FormulaSyntax lastStatement = statements[^1];
            FormulaNode result = nodes[^1];
            if (lastStatement is FormulaSyntax.Step lastStep)
            {
                result = new FormulaNode.SlotRead(lastStep.Start, result.Kind, slots[^1], lastStep.Name);
            }

            FormulaNode converted = this.ConvertResult(result, wants, lastStatement);
            if (converted == null)
            {
                return this.Failed();
            }

            if (slots[^1] < 0)
            {
                nodes[^1] = converted;
            }
            else if (!ReferenceEquals(converted, result))
            {
                nodes.Add(converted);
                slots.Add(-1);
            }

            var steps = new List<CheckedFormula.StepResult>();
            foreach (KeyValuePair<string, int> named in m_slotByName)
            {
                if (!m_parameterNames.Contains(named.Key))
                {
                    steps.Add(new CheckedFormula.StepResult(named.Key, named.Value, m_slotKinds[named.Value]));
                }
            }

            var program = new FormulaProgram(nodes.ToArray(), slots.ToArray(), m_slotKinds.Count);
            return CheckedFormula.Succeeded(
                program,
                converted.Kind,
                m_hostReads.ToArray(),
                steps.ToArray(),
                m_nodeCount + 1,
                m_deepest
            );
        }

        private bool CanBeStepName (FormulaSyntax.Step step)
        {
            if (m_parameterNames.Contains(step.Name))
            {
                this.Fail(
                    step.Start,
                    $"${step.Name} at {At(step.Start)} is one of {m_functionName}'s parameters, so a step can't set it."
                        + " Give the step its own name."
                );
                return false;
            }

            if (
                (m_declarations != null)
                && m_declarations.TryFind(step.Name, m_ignoreNameCase, out TermDeclarations.Entry declared)
            )
            {
                this.Fail(
                    step.Start,
                    $"${declared.Name} at {At(step.Start)} comes from the host, so a step can't set it."
                        + " Give the step its own name."
                );
                return false;
            }

            return true;
        }

        private FormulaNode ConvertResult (FormulaNode result, eTermKind? wants, FormulaSyntax statement)
        {
            if (wants == null)
            {
                return result;
            }

            eTermKind wanted = wants.Value;
            if (wanted == eTermKind.String)
            {
                return result.Kind == eTermKind.String ? result : new FormulaNode.Print(result);
            }

            if (result.Kind == wanted)
            {
                return result;
            }

            if (TermKinds.Accepts(wanted, result.Kind))
            {
                return new FormulaNode.Widen(result, wanted);
            }

            if (LosesFraction(wanted, result.Kind))
            {
                return this.FailFraction(result, $"{TermKinds.Describe(wanted)} is wanted");
            }

            return this.Fail(
                statement.Start,
                $"This gives {TermKinds.Describe(result.Kind)}, and {TermKinds.Describe(wanted)} is wanted"
                    + $" ({At(statement.Start)})."
            );
        }

        // ------ Expressions

        private FormulaNode Check (FormulaSyntax syntax, bool isPrinted)
        {
            ++m_nodeCount;
            switch (syntax)
            {
                case FormulaSyntax.Number number:
                    return new FormulaNode.Constant(
                        number.Start,
                        number.IsWhole ? TermValue.Int((long)number.Value) : TermValue.Float(number.Value),
                        m_text.Substring(number.Start, number.End - number.Start)
                    );

                case FormulaSyntax.Bool flag:
                    return new FormulaNode.Constant(flag.Start, TermValue.Bool(flag.Value));

                case FormulaSyntax.Variable variable:
                    return this.CheckVariable(variable);

                case FormulaSyntax.Text text:
                    return this.CheckText(text);

                case FormulaSyntax.Group group:
                    {
                        if (!this.Enter(group.Start))
                        {
                            return null;
                        }

                        FormulaNode inner = this.Check(group.Inner, isPrinted);
                        this.Leave();
                        return inner;
                    }

                case FormulaSyntax.Unary unary:
                    return this.CheckUnary(unary);

                case FormulaSyntax.Chain chain:
                    return chain.Level == FormulaSyntax.eChain.Logic ? this.CheckLogic(chain) : this.CheckChain(chain);

                case FormulaSyntax.Conditional conditional:
                    return this.CheckConditional(conditional, isPrinted);

                case FormulaSyntax.Call call:
                    return this.CheckCall(call);
            }

            return this.Fail(syntax.Start, $"A step at {At(syntax.Start)} can only be a line of its own.");
        }

        private FormulaNode CheckVariable (FormulaSyntax.Variable variable)
        {
            string name = variable.Name;
            if (m_slotByName.TryGetValue(name, out int slot))
            {
                return new FormulaNode.SlotRead(variable.Start, m_slotKinds[slot], slot, name);
            }

            if ((m_declarations != null) && m_declarations.TryFind(name, m_ignoreNameCase, out TermDeclarations.Entry declared))
            {
                // An unset value only turns up for some values, so using one unchecked is refused here, where it can be fixed
                if (declared.IsOptional && !this.MarkChecked(declared.Name))
                {
                    return this.Fail(
                        variable.Start,
                        $"${declared.Name} at {At(variable.Start)} might not be set, so check it first:"
                            + $" has(${declared.Name}) ? ... : ..., or has(${declared.Name}) && ..."
                    );
                }

                return new FormulaNode.HostRead(variable.Start, declared.Kind, this.HostSlotFor(declared), declared.Name);
            }

            if (m_stepPositions.TryGetValue(name, out List<int> positions))
            {
                foreach (int setAt in positions)
                {
                    if (setAt > variable.Start)
                    {
                        return this.Fail(
                            variable.Start,
                            $"${name} is used before it's set ({At(variable.Start)}; it's set at {At(setAt)})."
                        );
                    }
                }
            }

            // Set further up, but not yet: this is the value it's first being set to
            if (positions != null)
            {
                return this.Fail(
                    variable.Start,
                    $"${name} at {At(variable.Start)} is used in its own first definition, before it has a value."
                );
            }

            string onlyParameters = m_functionName == null
                ? String.Empty
                : (m_parameterNames.Count == 0
                    ? $" A function only sees its parameters, and {m_functionName} has none."
                    : $" A function only sees its parameters, and {m_functionName}'s are"
                        + $" {DescribeParameters(m_parameterNames)}.");

            return this.Fail(
                variable.Start,
                $"Unknown name '${name}' at {At(variable.Start)}."
                    + $"{FormulaMessages.Suggest(name, this.KnownValueNames(), "$")}{onlyParameters}"
            );
        }

        private FormulaNode CheckText (FormulaSyntax.Text text)
        {
            var literals = new string[text.Parts.Count];
            var values = new FormulaNode[text.Parts.Count];
            for (int index = 0; index < text.Parts.Count; ++index)
            {
                FormulaSyntax.TextPart part = text.Parts[index];
                if (part.Expression == null)
                {
                    literals[index] = part.Literal;
                    continue;
                }

                if (part.IsInterpolation && !this.Enter(part.Expression.Start))
                {
                    return null;
                }

                values[index] = this.Check(part.Expression, isPrinted: true);
                if (part.IsInterpolation)
                {
                    this.Leave();
                }

                if (values[index] == null)
                {
                    return null;
                }
            }

            return new FormulaNode.Text(text.Start, literals, values);
        }

        private FormulaNode CheckUnary (FormulaSyntax.Unary unary)
        {
            if (!this.Enter(unary.Start))
            {
                return null;
            }

            FormulaNode operand = this.Check(unary.Operand, isPrinted: false);
            this.Leave();
            if (operand == null)
            {
                return null;
            }

            if (unary.IsNot)
            {
                return TermKinds.ItemOf(operand.Kind) == eTermKind.Bool
                    ? new FormulaNode.Not(unary.Start, operand)
                    : this.Fail(
                        unary.Start,
                        $"'!' needs true or false, and {What(unary.Operand, "what follows it")} is"
                            + $" {TermKinds.Describe(operand.Kind)} ({At(unary.Start)})."
                    );
            }

            return TermKinds.IsNumber(operand.Kind)
                ? new FormulaNode.Negate(unary.Start, operand)
                : this.Fail(
                    unary.Start,
                    $"'-' needs a number, and {What(unary.Operand, "what follows it")} is"
                        + $" {TermKinds.Describe(operand.Kind)} ({At(unary.Start)})."
                );
        }

        // Arithmetic and comparisons, left to right
        private FormulaNode CheckChain (FormulaSyntax.Chain chain)
        {
            var operands = new FormulaNode[chain.Operands.Count];
            var stepKinds = new eTermKind[chain.Operators.Count];
            operands[0] = this.Check(chain.Operands[0], isPrinted: false);
            if (operands[0] == null)
            {
                return null;
            }

            eTermKind soFar = operands[0].Kind;
            for (int opIndex = 0; opIndex < chain.Operators.Count; ++opIndex)
            {
                operands[opIndex + 1] = this.Check(chain.Operands[opIndex + 1], isPrinted: false);
                if (operands[opIndex + 1] == null)
                {
                    return null;
                }

                FormulaSyntax.ChainOperator op = chain.Operators[opIndex];
                string problem = chain.Level == FormulaSyntax.eChain.Comparison
                    ? CompareStep(op, soFar, operands[opIndex + 1].Kind, out soFar)
                    : ArithmeticStep(op, soFar, operands[opIndex + 1].Kind, out soFar);

                if (problem != null)
                {
                    return this.Fail(op.Index, problem);
                }

                stepKinds[opIndex] = soFar;
            }

            string zeroDivisor = FormulaBuiltIns.WrittenZeroDivisorProblem(chain, out int zeroAt);
            if (zeroDivisor != null)
            {
                return this.Fail(zeroAt, zeroDivisor);
            }

            return new FormulaNode.Chain(chain.Start, operands, chain.Operators.ToArray(), stepKinds);
        }

        private static string ArithmeticStep (
            FormulaSyntax.ChainOperator op,
            eTermKind left,
            eTermKind right,
            out eTermKind result
        )
        {
            result = left;
            string symbol = FormulaOperators.Symbol(op.Operator);
            if (!TermKinds.IsNumber(left))
            {
                return $"'{symbol}' needs numbers, and its left side is {TermKinds.Describe(left)} ({At(op.Index)}).";
            }

            if (!TermKinds.IsNumber(right))
            {
                return $"'{symbol}' needs numbers, and its right side is {TermKinds.Describe(right)} ({At(op.Index)}).";
            }

            // '/' always leaves room for a fraction, and so does anything with a float in it
            bool isWhole = (TermKinds.ItemOf(left) == eTermKind.Int)
                && (TermKinds.ItemOf(right) == eTermKind.Int)
                && (op.Operator != eFormulaOperator.Divide);

            result = TermKinds.WithListness(
                isWhole ? eTermKind.Int : eTermKind.Float,
                TermKinds.IsList(left) || TermKinds.IsList(right)
            );
            return null;
        }

        private static string CompareStep (FormulaSyntax.ChainOperator op, eTermKind left, eTermKind right, out eTermKind result)
        {
            result = TermKinds.WithListness(eTermKind.Bool, TermKinds.IsList(left) || TermKinds.IsList(right));
            string symbol = FormulaOperators.Symbol(op.Operator);
            bool isEquality = op.Operator is eFormulaOperator.EqualTo or eFormulaOperator.NotEqualTo;
            if (TermKinds.IsNumber(left) && TermKinds.IsNumber(right))
            {
                return null;
            }

            eTermKind leftItem = TermKinds.ItemOf(left);
            eTermKind rightItem = TermKinds.ItemOf(right);
            if (isEquality && (leftItem == rightItem))
            {
                return null;
            }

            if (!isEquality)
            {
                eTermKind notNumber = TermKinds.IsNumber(left) ? right : left;
                string side = TermKinds.IsNumber(left) ? "right" : "left";
                return $"'{symbol}' compares numbers, and its {side} side is {TermKinds.Describe(notNumber)} ({At(op.Index)}).";
            }

            bool isTextAndNumber = ((leftItem == eTermKind.String) && TermKinds.IsNumber(right))
                || ((rightItem == eTermKind.String) && TermKinds.IsNumber(left));

            return isTextAndNumber
                ? $"'{symbol}' compares text with a number ({At(op.Index)})."
                : $"'{symbol}' compares {TermKinds.Describe(left)} with {TermKinds.Describe(right)} ({At(op.Index)}).";
        }

        private FormulaNode CheckLogic (FormulaSyntax.Chain chain)
        {
            bool isAnd = chain.Operators[0].Operator == eFormulaOperator.And;
            string symbol = isAnd ? "&&" : "||";
            int checkedBefore = m_checkedOptionals.Count;
            var operands = new FormulaNode[chain.Operands.Count];
            bool isList = false;
            for (int index = 0; index < operands.Length; ++index)
            {
                operands[index] = this.Check(chain.Operands[index], isPrinted: false);
                if (operands[index] == null)
                {
                    return null;
                }

                if (TermKinds.ItemOf(operands[index].Kind) != eTermKind.Bool)
                {
                    int opAt = chain.Operators[Math.Max(0, index - 1)].Index;
                    string side = index == 0 ? "left" : "right";
                    return this.Fail(
                        opAt,
                        $"'{symbol}' needs true or false values, and its {side} side is"
                            + $" {TermKinds.Describe(operands[index].Kind)} ({At(opAt)})."
                    );
                }

                isList |= TermKinds.IsList(operands[index].Kind);

                // After has($x) &&, the rest of the chain can use $x
                if (isAnd)
                {
                    this.AddCheckedNames(chain.Operands[index]);
                }
            }

            // Over lists nothing stops early, so a has() check in a list chain can't guard what comes after it
            CheckedOptional usedInList = null;
            for (int index = checkedBefore; index < m_checkedOptionals.Count; ++index)
            {
                if (isList && m_checkedOptionals[index].WasUsed)
                {
                    usedInList = m_checkedOptionals[index];
                    break;
                }
            }

            m_checkedOptionals.RemoveRange(checkedBefore, m_checkedOptionals.Count - checkedBefore);
            if (usedInList != null)
            {
                return this.Fail(
                    usedInList.Index,
                    $"has(${usedInList.Name}) && ... can only guard single true or false values,"
                        + $" and this is a list ({At(usedInList.Index)})."
                        + $" Use has(${usedInList.Name}) ? ... : ... instead."
                );
            }

            return new FormulaNode.Logic(
                chain.Start,
                isList ? eTermKind.BoolList : eTermKind.Bool,
                operands,
                chain.Operators.ToArray()
            );
        }

        private FormulaNode CheckConditional (FormulaSyntax.Conditional conditional, bool isPrinted)
        {
            int questionAt = conditional.QuestionIndex;
            FormulaNode condition = this.Check(conditional.Condition, isPrinted: false);
            if (condition == null)
            {
                return null;
            }

            if (condition.Kind == eTermKind.BoolList)
            {
                return this.Fail(
                    questionAt,
                    $"A list of true or false values can't decide '?' ({At(questionAt)}). count() can make it one value."
                );
            }

            if (condition.Kind != eTermKind.Bool)
            {
                return this.Fail(
                    questionAt,
                    $"'?' needs true or false before it, and this is {TermKinds.Describe(condition.Kind)} ({At(questionAt)})."
                );
            }

            if (!this.Enter(questionAt))
            {
                return null;
            }

            // The true side can use whatever the condition checked with has()
            int checkedBefore = m_checkedOptionals.Count;
            this.AddCheckedNames(conditional.Condition);
            FormulaNode whenTrue = this.Check(conditional.WhenTrue, isPrinted);
            m_checkedOptionals.RemoveRange(checkedBefore, m_checkedOptionals.Count - checkedBefore);

            FormulaNode whenFalse = whenTrue == null ? null : this.Check(conditional.WhenFalse, isPrinted);
            this.Leave();
            if (whenFalse == null)
            {
                return null;
            }

            if (TermKinds.TryUnify(whenTrue.Kind, whenFalse.Kind, out eTermKind kind))
            {
                return new FormulaNode.Conditional(
                    conditional.Start,
                    kind,
                    condition,
                    Widened(whenTrue, kind),
                    Widened(whenFalse, kind)
                );
            }

            // A value that's printed can be anything, so the two sides needn't match: "a natural 20" or the number
            if (isPrinted)
            {
                return new FormulaNode.Conditional(
                    conditional.Start,
                    eTermKind.String,
                    condition,
                    Printed(whenTrue),
                    Printed(whenFalse)
                );
            }

            return this.Fail(
                questionAt,
                $"The two sides of '? :' are different sorts of value ({At(questionAt)}):"
                    + $" {TermKinds.Describe(whenTrue.Kind)} before the ':' and {TermKinds.Describe(whenFalse.Kind)} after it."
                    + " They have to match unless the value is printed as text."
            );
        }

        private FormulaNode CheckCall (FormulaSyntax.Call call)
        {
            if ((m_functions != null) && m_functions.TryFindTable(call.Name, m_ignoreNameCase, out TermFunctions.Entry entry))
            {
                return this.CheckTableCall(call, entry);
            }

            if ((m_functions != null) && m_functions.TryFindHost(call.Name, m_ignoreNameCase, out TermHostFunction host))
            {
                return this.CheckHostCall(call, host);
            }

            if (FormulaBuiltIns.TryFind(call.Name, m_ignoreNameCase, out FormulaBuiltIns.eBuiltIn builtIn))
            {
                return this.CheckBuiltIn(call, builtIn);
            }

            return this.Fail(
                call.Start,
                $"Unknown function '{call.Name}' at {At(call.Start)}."
                    + $"{FormulaMessages.Suggest(call.Name, this.KnownFunctionNames(), String.Empty)}"
            );
        }

        private FormulaNode CheckTableCall (FormulaSyntax.Call call, TermFunctions.Entry entry)
        {
            if (entry.Error != null)
            {
                m_innerError ??= entry.Error;
                return this.Fail(
                    call.Start,
                    $"{this.CallText(call)} at {At(call.Start)} can't run until {entry.Name} is fixed:"
                        + $" {FormulaMessages.LowerFirst(entry.Error.Message)}"
                );
            }

            if (call.Arguments.Count != entry.Parameters.Length)
            {
                return this.Fail(
                    call.Start,
                    $"{entry.Name}() takes {Plural(entry.Parameters.Length, "argument")}"
                        + $" ({DescribeParameters(entry.Parameters)}),"
                        + $" and is given {call.Arguments.Count}, at {At(call.Start)}."
                );
            }

            FormulaNode[] arguments = this.CheckArguments(call);
            if (arguments == null)
            {
                return null;
            }

            var kinds = new eTermKind[arguments.Length];
            for (int index = 0; index < kinds.Length; ++index)
            {
                kinds[index] = arguments[index].Kind;
            }

            FormulaFunctionBody body = m_functions.Specialize(
                entry,
                kinds,
                m_baseDepth + m_depth + 1,
                m_maxDepth,
                out CheckedFormula failure
            );
            if (body == null)
            {
                return this.FailInside(call, entry.Name, failure);
            }

            m_deepest = Math.Max(m_deepest, m_depth + 1 + body.Height);
            return new FormulaNode.TableCall(call.Start, body, arguments, this.CallText(call));
        }

        private FormulaNode CheckHostCall (FormulaSyntax.Call call, TermHostFunction host)
        {
            if (call.Arguments.Count != host.ParameterKinds.Count)
            {
                return this.Fail(
                    call.Start,
                    $"{host.Name}() takes {Plural(host.ParameterKinds.Count, "argument")},"
                        + $" and is given {call.Arguments.Count}, at {At(call.Start)}."
                );
            }

            FormulaNode[] arguments = this.CheckArguments(call);
            if (arguments == null)
            {
                return null;
            }

            for (int index = 0; index < arguments.Length; ++index)
            {
                eTermKind wanted = host.ParameterKinds[index];
                if (!TermKinds.Accepts(wanted, arguments[index].Kind))
                {
                    return LosesFraction(wanted, arguments[index].Kind)
                        ? this.FailFraction(
                            arguments[index],
                            $"{host.Name}() wants {TermKinds.Describe(wanted)} for argument {index + 1}"
                        )
                        : this.Fail(
                            call.Arguments[index].Start,
                            $"{host.Name}() wants {TermKinds.Describe(wanted)} for argument {index + 1},"
                                + $" and is given {TermKinds.Describe(arguments[index].Kind)}"
                                + $" ({At(call.Arguments[index].Start)})."
                        );
                }

                arguments[index] = Widened(arguments[index], wanted);
            }

            return new FormulaNode.HostCall(call.Start, host, arguments, this.CallText(call));
        }

        private FormulaNode[] CheckArguments (FormulaSyntax.Call call)
        {
            if (!this.Enter(call.Start))
            {
                return null;
            }

            var arguments = new FormulaNode[call.Arguments.Count];
            for (int index = 0; index < arguments.Length; ++index)
            {
                arguments[index] = this.Check(call.Arguments[index], isPrinted: false);
                if (arguments[index] == null)
                {
                    return null;
                }
            }

            this.Leave();
            return arguments;
        }

        // ------ Optional values

        // has($x) where $x is an optional host value, as written (in parentheses or not), not shadowed by a function of the
        //  same name
        private bool TryGetCheckedName (FormulaSyntax syntax, out string declaredName)
        {
            declaredName = null;
            if ((Unwrap(syntax) is not FormulaSyntax.Call call) || (call.Arguments.Count != 1) || !this.IsBuiltInHas(call.Name))
            {
                return false;
            }

            if ((Unwrap(call.Arguments[0]) is not FormulaSyntax.Variable variable)
                || (m_declarations == null)
                || !m_declarations.TryFind(variable.Name, m_ignoreNameCase, out TermDeclarations.Entry declared)
                || !declared.IsOptional)
            {
                return false;
            }

            declaredName = declared.Name;
            return true;
        }

        // What a ?: condition checks: a has() call, or a chain of them joined with && (alongside anything else)
        private void AddCheckedNames (FormulaSyntax condition)
        {
            FormulaSyntax unwrapped = Unwrap(condition);
            if (
                (unwrapped is FormulaSyntax.Chain chain)
                && (chain.Level == FormulaSyntax.eChain.Logic)
                && (chain.Operators[0].Operator == eFormulaOperator.And)
            )
            {
                foreach (FormulaSyntax operand in chain.Operands)
                {
                    this.AddCheckedNames(operand);
                }

                return;
            }

            if (this.TryGetCheckedName(unwrapped, out string declaredName))
            {
                m_checkedOptionals.Add(new CheckedOptional(declaredName, unwrapped.Start));
            }
        }

        private bool MarkChecked (string declaredName)
        {
            for (int index = m_checkedOptionals.Count - 1; index >= 0; --index)
            {
                if (m_checkedOptionals[index].Name == declaredName)
                {
                    m_checkedOptionals[index].WasUsed = true;
                    return true;
                }
            }

            return false;
        }

        private bool IsBuiltInHas (string name)
        {
            if (
                (m_functions != null)
                && (
                    m_functions.TryFindTable(name, m_ignoreNameCase, out _)
                    || m_functions.TryFindHost(name, m_ignoreNameCase, out _)
                )
            )
            {
                return false;
            }

            return FormulaBuiltIns.TryFind(name, m_ignoreNameCase, out FormulaBuiltIns.eBuiltIn builtIn)
                && (builtIn == FormulaBuiltIns.eBuiltIn.Has);
        }

        // ------ Failing

        private FormulaNode Fail (int index, string message)
        {
            m_error ??= new TermError(message, index, m_functionName);
            return null;
        }

        // The where and why of a fraction where a whole number is wanted, pointing at what made it: "'/' can leave a
        //  fraction (position 8), and a whole number is wanted. Use trunc(), ..."
        private FormulaNode FailFraction (FormulaNode node, string wantedClause)
        {
            FractionSource source = FindFraction(node);
            return this.Fail(
                source.Index,
                $"{source.What} ({At(source.Index)}), and {wantedClause}."
                    + " Use trunc(), floor(), ceil(), or round() to say which way it goes."
            );
        }

        // A function's body failed for this call. The call reports it with the story of how it got there, and the error in
        //  the body itself comes along so an editor can show it on the function's own box too.
        private FormulaNode FailInside (FormulaSyntax.Call call, string functionName, CheckedFormula failure)
        {
            if (m_error != null)
            {
                return null;
            }

            m_failedOnDepth |= failure.FailedOnDepth;
            m_chain = $"inside {functionName}, {failure.Chain}";
            m_innerError = failure.InnerError;
            return this.Fail(call.Start, $"{this.CallText(call)} at {At(call.Start)}: {m_chain}");
        }

        private CheckedFormula Failed ()
        {
            return CheckedFormula.Failed(
                m_error,
                m_innerError,
                m_chain ?? FormulaMessages.LowerFirst(m_error.Message),
                m_failedOnDepth
            );
        }

        private bool Enter (int index)
        {
            ++m_depth;
            if (m_baseDepth + m_depth > m_maxDepth)
            {
                m_failedOnDepth = true;
                string how = m_baseDepth > 0 ? "Nesting and functions calling functions go" : "This nests";
                this.Fail(index, $"{how} more than {m_maxDepth} deep at {At(index)}.");
                return false;
            }

            m_deepest = Math.Max(m_deepest, m_depth);
            return true;
        }

        private void Leave () => --m_depth;

        // ------ Small

        private int AddSlot (eTermKind kind)
        {
            m_slotKinds.Add(kind);
            return m_slotKinds.Count - 1;
        }

        private int HostSlotFor (TermDeclarations.Entry declared)
        {
            if (!m_hostSlotByName.TryGetValue(declared.Name, out int hostSlot))
            {
                hostSlot = m_hostReads.Count;
                m_hostReads.Add(declared);
                m_hostSlotByName.Add(declared.Name, hostSlot);
            }

            return hostSlot;
        }

        private IEnumerable<string> KnownValueNames ()
        {
            foreach (string name in m_slotByName.Keys)
            {
                yield return name;
            }

            if (m_declarations != null)
            {
                foreach (string name in m_declarations.Names)
                {
                    yield return name;
                }
            }
        }

        private IEnumerable<string> KnownFunctionNames ()
        {
            if (m_functions != null)
            {
                foreach (string name in m_functions.Names)
                {
                    yield return name;
                }
            }

            foreach (string name in FormulaBuiltIns.Names)
            {
                yield return name;
            }
        }

        private string CallText (FormulaSyntax.Call call)
        {
            int length = call.End - call.Start;
            return length > kLongestQuotedCall
                ? m_text.Substring(call.Start, kLongestQuotedCall - 3) + "..."
                : m_text.Substring(call.Start, length);
        }

        private static FormulaNode Widened (FormulaNode node, eTermKind kind)
            => node.Kind == kind ? node : new FormulaNode.Widen(node, kind);

        private static FormulaNode Printed (FormulaNode node)
            => node.Kind == eTermKind.String ? node : new FormulaNode.Print(node);

        private static FormulaSyntax Unwrap (FormulaSyntax syntax)
        {
            while (syntax is FormulaSyntax.Group group)
            {
                syntax = group.Inner;
            }

            return syntax;
        }

        // A fraction would be lost: a float (or a list of them) where an int (or a list of them) is wanted
        private static bool LosesFraction (eTermKind wanted, eTermKind actual)
        {
            return (TermKinds.ItemOf(wanted) == eTermKind.Int)
                && (TermKinds.ItemOf(actual) == eTermKind.Float)
                && (TermKinds.IsList(wanted) == TermKinds.IsList(actual));
        }

        // How a message names what an operator or function was given: "$score", or "what follows it"
        private static string What (FormulaSyntax syntax, string otherwise = "its argument")
        {
            return Unwrap(syntax) is FormulaSyntax.Variable variable ? $"${variable.Name}" : otherwise;
        }

        private static string DescribeParameters (IEnumerable<string> parameters)
        {
            var named = new List<string>();
            foreach (string parameter in parameters)
            {
                named.Add("$" + parameter);
            }

            return named.Count == 0 ? "none" : String.Join(", ", named);
        }

        private static string Plural (int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

        private static string At (int index) => FormulaMessages.At(index);

        // Finds what first made a value able to hold a fraction, for pointing at it
        private static FractionSource FindFraction (FormulaNode node)
        {
            switch (node)
            {
                case FormulaNode.Chain chain:
                    {
                        if (TermKinds.ItemOf(chain.Operands[0].Kind) == eTermKind.Float)
                        {
                            return FindFraction(chain.Operands[0]);
                        }

                        for (int opIndex = 0; opIndex < chain.Operators.Count; ++opIndex)
                        {
                            if (chain.Operators[opIndex].Operator == eFormulaOperator.Divide)
                            {
                                return new FractionSource("'/' can leave a fraction", chain.Operators[opIndex].Index);
                            }

                            if (TermKinds.ItemOf(chain.Operands[opIndex + 1].Kind) == eTermKind.Float)
                            {
                                return FindFraction(chain.Operands[opIndex + 1]);
                            }
                        }

                        break;
                    }

                case FormulaNode.Negate negate:
                    return FindFraction(negate.Operand);

                case FormulaNode.Widen widen:
                    return FindFraction(widen.Inner);

                case FormulaNode.Conditional conditional:
                    {
                        bool isTrueSideFloat = (conditional.WhenTrue is not FormulaNode.Widen)
                            && (TermKinds.ItemOf(conditional.WhenTrue.Kind) == eTermKind.Float);
                        return FindFraction(isTrueSideFloat ? conditional.WhenTrue : conditional.WhenFalse);
                    }

                case FormulaNode.Constant constant:
                    return new FractionSource(
                        $"The number {constant.Written ?? FormulaNumbers.Format(constant.Value.Number)} has a fraction",
                        constant.Index
                    );

                case FormulaNode.HostRead hostRead:
                    return new FractionSource($"${hostRead.Name} can have a fraction", hostRead.Index);

                case FormulaNode.SlotRead slotRead:
                    return new FractionSource($"${slotRead.Name} can have a fraction", slotRead.Index);

                case FormulaNode.BuiltIn builtIn:
                    return new FractionSource($"{builtIn.Name}() can give a fraction", builtIn.Index);

                case FormulaNode.TableCall tableCall:
                    return new FractionSource($"{tableCall.FunctionName}() can give a fraction", tableCall.Index);

                case FormulaNode.HostCall hostCall:
                    return new FractionSource($"{hostCall.FunctionName}() can give a fraction", hostCall.Index);
            }

            return new FractionSource("This can have a fraction", node.Index);
        }

        // ===========[ Subclasses/structs ]===================================

        private readonly record struct FractionSource (string What, int Index);

        /// <summary>
        /// An optional value a has() check makes usable, for as long as the check covers.
        /// </summary>
        private sealed class CheckedOptional
        {
            public CheckedOptional (string name, int index)
            {
                this.Name = name;
                this.Index = index;
            }

            public string Name { get; }

            /// <summary>
            /// Where the has() check is
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// Whether anything read the value thanks to this check
            /// </summary>
            public bool WasUsed { get; set; }
        }
    }

    /// <summary>
    /// What checking a formula, a template, or a function's body gives back: the program and what it reads and sets, or why
    /// it couldn't be checked.
    /// </summary>
    internal sealed class CheckedFormula
    {
        private CheckedFormula ()
        {
        }

        public static CheckedFormula Succeeded (
            FormulaProgram program,
            eTermKind resultKind,
            TermDeclarations.Entry[] hostReads,
            StepResult[] steps,
            int cost,
            int height
        )
        {
            return new CheckedFormula
            {
                Program = program,
                ResultKind = resultKind,
                HostReads = hostReads,
                Steps = steps,
                Cost = cost,
                Height = height,
            };
        }

        public static CheckedFormula Failed (TermError error, TermError innerError, string chain, bool failedOnDepth)
        {
            return new CheckedFormula
            {
                Error = error,
                InnerError = innerError ?? (error.FunctionName != null ? error : null),
                Chain = chain,
                FailedOnDepth = failedOnDepth,
            };
        }

        public bool IsSuccess => this.Error == null;
        public FormulaProgram Program { get; private init; }
        public eTermKind ResultKind { get; private init; }

        /// <summary>
        /// The host values it reads, by slot
        /// </summary>
        public TermDeclarations.Entry[] HostReads { get; private init; }

        /// <summary>
        /// Every step it sets, by its last definition
        /// </summary>
        public StepResult[] Steps { get; private init; }

        public int Cost { get; private init; }
        public int Height { get; private init; }

        /// <summary>
        /// The problem, in the text that was checked
        /// </summary>
        public TermError Error { get; private init; }

        /// <summary>
        /// When the problem is inside a function's body, the problem there, with the function's name on it
        /// </summary>
        public TermError InnerError { get; private init; }

        /// <summary>
        /// The problem as a caller tells it after "inside share, ": the message itself, or the next call in.
        /// </summary>
        public string Chain { get; private init; }

        /// <summary>
        /// Whether it failed only because of how deep it was called, which says nothing about a call from somewhere shallower
        /// </summary>
        public bool FailedOnDepth { get; private init; }

        public IReadOnlyList<TermError> Errors => this.InnerError == null || ReferenceEquals(this.InnerError, this.Error)
            ? new[] { this.Error }
            : new[] { this.Error, this.InnerError };

        public readonly record struct StepResult (string Name, int Slot, eTermKind Kind);
    }
}
