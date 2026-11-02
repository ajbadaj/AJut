namespace AJut.Text
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using eBuiltIn = FormulaBuiltIns.eBuiltIn;

    /// <summary>
    /// One checked step of a formula, ready to run. The checker makes these from <see cref="FormulaSyntax"/> once every name
    /// is found and every kind is known, so evaluating never checks a kind: the only problems left are the ones the values
    /// themselves cause, like dividing by zero.
    /// </summary>
    /// <remarks>
    /// A tree of small classes, each evaluating itself. A formula runs once each time its values come in, rather than
    /// once per record in a filter, so the flat array TermMatcher uses for speed isn't worth what it would cost here,
    /// where a function's body is checked again for each set of argument kinds it's called with. Nodes never change
    /// once made, so a checked formula can run on any number of threads at once.
    /// </remarks>
    internal abstract class FormulaNode
    {
        // ===========[ Setup/Construction/Teardown ]==========================

        protected FormulaNode (int index, eTermKind kind)
        {
            this.Index = index;
            this.Kind = kind;
        }

        // ===========[ Properties ]===========================================

        /// <summary>
        /// Where this is in its text, for pointing at it when something goes wrong
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// What this always evaluates to. A list kind always gives a list, and a single kind a single value.
        /// </summary>
        public eTermKind Kind { get; }

        // ===========[ Public Interface Methods ]=============================

        public abstract TermValue Evaluate (EvaluationContext context);

        // ===========[ Helpers ]==============================================

        private static string At (int index) => FormulaMessages.At(index);

        // A list and a single value apply the single value to every item, and two lists go item by item
        private static TermValue ApplyBinary (
            EvaluationContext context,
            eFormulaOperator op,
            int index,
            TermValue left,
            TermValue right,
            eTermKind resultKind
        )
        {
            if (!left.IsList && !right.IsList)
            {
                return ApplySingle(context, op, index, left, right, resultKind);
            }

            int count = left.IsList ? left.Items.Length : right.Items.Length;
            if (left.IsList && right.IsList && (left.Items.Length != right.Items.Length))
            {
                return context.Fail(
                    index,
                    $"Two lists of different lengths meet at '{FormulaOperators.Symbol(op)}' at {At(index)}:"
                        + $" one has {left.Items.Length} items and the other {right.Items.Length}."
                );
            }

            if (!context.Charge(count, index))
            {
                return default;
            }

            eTermKind itemKind = TermKinds.ItemOf(resultKind);
            var items = new TermValue[count];
            for (int itemIndex = 0; itemIndex < count; ++itemIndex)
            {
                items[itemIndex] = ApplySingle(
                    context,
                    op,
                    index,
                    left.IsList ? left.Items[itemIndex] : left,
                    right.IsList ? right.Items[itemIndex] : right,
                    itemKind
                );

                if (context.HasFailed)
                {
                    return default;
                }
            }

            return TermValue.Trusted(resultKind, items);
        }

        private static TermValue ApplySingle (
            EvaluationContext context,
            eFormulaOperator op,
            int index,
            TermValue left,
            TermValue right,
            eTermKind resultKind
        )
        {
            double a = left.Number;
            double b = right.Number;
            switch (op)
            {
                case eFormulaOperator.EqualTo:
                case eFormulaOperator.NotEqualTo:
                    {
                        // Text compares ordinal and case-sensitive, and costs work for its length, so comparing long text
                        //  over and over through functions can't run on unchecked. Everything else compares by its number, a
                        //  bool's being 1 or 0.
                        if ((left.Kind == eTermKind.String)
                            && !context.Charge(Math.Max(left.AsString.Length, right.AsString.Length), index))
                        {
                            return default;
                        }

                        bool isEqual = left.Kind == eTermKind.String
                            ? String.Equals(left.AsString, right.AsString, StringComparison.Ordinal)
                            : a == b;

                        return TermValue.Computed(eTermKind.Bool, (isEqual == (op == eFormulaOperator.EqualTo)) ? 1 : 0);
                    }

                case eFormulaOperator.LessThan: return TermValue.Computed(eTermKind.Bool, a < b ? 1 : 0);
                case eFormulaOperator.LessOrEqual: return TermValue.Computed(eTermKind.Bool, a <= b ? 1 : 0);
                case eFormulaOperator.GreaterThan: return TermValue.Computed(eTermKind.Bool, a > b ? 1 : 0);
                case eFormulaOperator.GreaterOrEqual: return TermValue.Computed(eTermKind.Bool, a >= b ? 1 : 0);
                case eFormulaOperator.And: return TermValue.Computed(eTermKind.Bool, ((a != 0) && (b != 0)) ? 1 : 0);
                case eFormulaOperator.Or: return TermValue.Computed(eTermKind.Bool, ((a != 0) || (b != 0)) ? 1 : 0);
            }

            double result;
            switch (op)
            {
                case eFormulaOperator.Add:
                    result = a + b;
                    break;

                case eFormulaOperator.Subtract:
                    result = a - b;
                    break;

                case eFormulaOperator.Multiply:
                    result = a * b;
                    break;

                case eFormulaOperator.Divide:
                    if (b == 0)
                    {
                        return context.Fail(index, FormulaBuiltIns.DivideByZeroProblem(index));
                    }

                    result = a / b;
                    break;

                default:
                    if (b == 0)
                    {
                        return context.Fail(index, FormulaBuiltIns.ModuloByZeroProblem(index));
                    }

                    result = FormulaNumbers.Mod(a, b);
                    break;
            }

            return MakeNumber(context, index, resultKind, result);
        }

        // Every whole number a formula makes is checked against 2^53, past which a double would quietly round it
        private static TermValue MakeNumber (EvaluationContext context, int index, eTermKind kind, double number)
        {
            if ((kind == eTermKind.Int) && !FormulaNumbers.IsExactWhole(number))
            {
                return context.Fail(
                    index,
                    $"A whole number past {FormulaNumbers.kLargestExactWhole:0} at {At(index)} is too big to hold exactly."
                );
            }

            return TermValue.Computed(kind, number);
        }

        // ===========[ Subclasses/structs ]===================================

        public sealed class Constant : FormulaNode
        {
            private readonly TermValue m_value;

            public Constant (int index, TermValue value, string written = null) : base(index, value.Kind)
            {
                m_value = value;
                this.Written = written;
            }

            /// <summary>
            /// How a number written in the formula was written, like "2.0", or null for anything else
            /// </summary>
            public string Written { get; }

            public TermValue Value => m_value;

            public override TermValue Evaluate (EvaluationContext context) => m_value;
        }

        /// <summary>
        /// A value the host supplies, as it was set, label and print rule included.
        /// </summary>
        public sealed class HostRead : FormulaNode
        {
            private readonly int m_hostSlot;

            public HostRead (int index, eTermKind kind, int hostSlot, string name) : base(index, kind)
            {
                m_hostSlot = hostSlot;
                this.Name = name;
            }

            public string Name { get; }

            public override TermValue Evaluate (EvaluationContext context)
            {
                if (!context.TryReadHost(m_hostSlot, out TermValue value))
                {
                    return context.Fail(this.Index, $"${this.Name} at {At(this.Index)} wasn't given a value.");
                }

                return value;
            }
        }

        /// <summary>
        /// A step, or a parameter inside a function.
        /// </summary>
        public sealed class SlotRead : FormulaNode
        {
            private readonly int m_slot;

            public SlotRead (int index, eTermKind kind, int slot, string name) : base(index, kind)
            {
                m_slot = slot;
                this.Name = name;
            }

            public string Name { get; }

            public override TermValue Evaluate (EvaluationContext context) => context.Frame[m_slot];
        }

        /// <summary>
        /// has($x). A step or a parameter is always set, which a host slot of -1 says.
        /// </summary>
        public sealed class HasCheck : FormulaNode
        {
            private readonly int m_hostSlot;

            public HasCheck (int index, int hostSlot) : base(index, eTermKind.Bool)
            {
                m_hostSlot = hostSlot;
            }

            public override TermValue Evaluate (EvaluationContext context)
            {
                bool isSet = (m_hostSlot < 0) || context.IsHostSet(m_hostSlot);
                return TermValue.Computed(eTermKind.Bool, isSet ? 1 : 0);
            }
        }

        /// <summary>
        /// An int taken where a float is wanted, or a list of ints where a list of floats is. Labels stay.
        /// </summary>
        public sealed class Widen : FormulaNode
        {
            private readonly FormulaNode m_inner;

            public Widen (FormulaNode inner, eTermKind kind) : base(inner.Index, kind)
            {
                m_inner = inner;
            }

            public FormulaNode Inner => m_inner;

            public override TermValue Evaluate (EvaluationContext context)
            {
                TermValue value = m_inner.Evaluate(context);
                return context.HasFailed ? default : value.AsKind(this.Kind);
            }
        }

        /// <summary>
        /// Any value printed into text, the way a template would print it.
        /// </summary>
        public sealed class Print : FormulaNode
        {
            private readonly FormulaNode m_inner;

            public Print (FormulaNode inner) : base(inner.Index, eTermKind.String)
            {
                m_inner = inner;
            }

            public FormulaNode Inner => m_inner;

            public override TermValue Evaluate (EvaluationContext context)
            {
                TermValue value = m_inner.Evaluate(context);
                if (context.HasFailed)
                {
                    return default;
                }

                var output = new StringBuilder();
                value.PrintTo(output);
                return context.Charge(output.Length, this.Index) ? TermValue.String(output.ToString()) : default;
            }
        }

        /// <summary>
        /// Quoted text in a formula, or a whole template: literal pieces with values printed between them.
        /// </summary>
        public sealed class Text : FormulaNode
        {
            private readonly string[] m_literals;
            private readonly FormulaNode[] m_values;

            /// <summary>
            /// Each part is a literal or a value, never both: m_literals[i] is null where m_values[i] is the part
            /// </summary>
            public Text (int index, string[] literals, FormulaNode[] values) : base(index, eTermKind.String)
            {
                m_literals = literals;
                m_values = values;
            }

            public override TermValue Evaluate (EvaluationContext context)
            {
                var output = new StringBuilder();
                for (int partIndex = 0; partIndex < m_literals.Length; ++partIndex)
                {
                    int lengthBefore = output.Length;
                    FormulaNode valueNode = m_values[partIndex];
                    if (valueNode == null)
                    {
                        output.Append(m_literals[partIndex]);
                    }
                    else
                    {
                        TermValue value = valueNode.Evaluate(context);
                        if (context.HasFailed)
                        {
                            return default;
                        }

                        value.PrintTo(output);
                    }

                    if (!context.Charge(output.Length - lengthBefore, this.Index))
                    {
                        return default;
                    }
                }

                return TermValue.String(output.ToString());
            }
        }

        public sealed class Negate : FormulaNode
        {
            private readonly FormulaNode m_operand;

            public Negate (int index, FormulaNode operand) : base(index, operand.Kind)
            {
                m_operand = operand;
            }

            public FormulaNode Operand => m_operand;

            public override TermValue Evaluate (EvaluationContext context)
            {
                TermValue value = m_operand.Evaluate(context);
                if (context.HasFailed)
                {
                    return default;
                }

                if (!value.IsList)
                {
                    return TermValue.Computed(this.Kind, -value.Number);
                }

                if (!context.Charge(value.Items.Length, this.Index))
                {
                    return default;
                }

                eTermKind itemKind = TermKinds.ItemOf(this.Kind);
                var items = new TermValue[value.Items.Length];
                for (int itemIndex = 0; itemIndex < items.Length; ++itemIndex)
                {
                    items[itemIndex] = TermValue.Computed(itemKind, -value.Items[itemIndex].Number);
                }

                return TermValue.Trusted(this.Kind, items);
            }
        }

        public sealed class Not : FormulaNode
        {
            private readonly FormulaNode m_operand;

            public Not (int index, FormulaNode operand) : base(index, operand.Kind)
            {
                m_operand = operand;
            }

            public FormulaNode Operand => m_operand;

            public override TermValue Evaluate (EvaluationContext context)
            {
                TermValue value = m_operand.Evaluate(context);
                if (context.HasFailed)
                {
                    return default;
                }

                if (!value.IsList)
                {
                    return TermValue.Computed(eTermKind.Bool, value.Number != 0 ? 0 : 1);
                }

                if (!context.Charge(value.Items.Length, this.Index))
                {
                    return default;
                }

                var items = new TermValue[value.Items.Length];
                for (int itemIndex = 0; itemIndex < items.Length; ++itemIndex)
                {
                    items[itemIndex] = TermValue.Computed(eTermKind.Bool, value.Items[itemIndex].Number != 0 ? 0 : 1);
                }

                return TermValue.Trusted(eTermKind.BoolList, items);
            }
        }

        /// <summary>
        /// A flat chain of arithmetic or comparisons, left to right. <c>m_stepKinds[i]</c> is what the chain is after its
        /// i-th operator, which the checker worked out.
        /// </summary>
        public sealed class Chain : FormulaNode
        {
            private readonly FormulaNode[] m_operands;
            private readonly FormulaSyntax.ChainOperator[] m_operators;
            private readonly eTermKind[] m_stepKinds;

            public Chain (int index, FormulaNode[] operands, FormulaSyntax.ChainOperator[] operators, eTermKind[] stepKinds)
                : base(index, stepKinds[^1])
            {
                m_operands = operands;
                m_operators = operators;
                m_stepKinds = stepKinds;
            }

            public IReadOnlyList<FormulaNode> Operands => m_operands;
            public IReadOnlyList<FormulaSyntax.ChainOperator> Operators => m_operators;
            public IReadOnlyList<eTermKind> StepKinds => m_stepKinds;

            public override TermValue Evaluate (EvaluationContext context)
            {
                TermValue result = m_operands[0].Evaluate(context);
                for (int opIndex = 0; (opIndex < m_operators.Length) && !context.HasFailed; ++opIndex)
                {
                    TermValue right = m_operands[opIndex + 1].Evaluate(context);
                    if (context.HasFailed)
                    {
                        break;
                    }

                    FormulaSyntax.ChainOperator op = m_operators[opIndex];
                    result = ApplyBinary(context, op.Operator, op.Index, result, right, m_stepKinds[opIndex]);
                }

                return context.HasFailed ? default : result;
            }
        }

        /// <summary>
        /// A chain of all <c>&amp;&amp;</c> or all <c>||</c>. Over single values it stops at the first side that decides it,
        /// which is what lets <c>has($x) &amp;&amp; $x &gt; 2</c> never read an unset value. Over lists it goes item by item,
        /// and the checker makes sure nothing in a list chain depends on stopping early.
        /// </summary>
        public sealed class Logic : FormulaNode
        {
            private readonly FormulaNode[] m_operands;
            private readonly FormulaSyntax.ChainOperator[] m_operators;

            public Logic (
                int index,
                eTermKind kind,
                FormulaNode[] operands,
                FormulaSyntax.ChainOperator[] operators
            ) : base(index, kind)
            {
                m_operands = operands;
                m_operators = operators;
            }

            public IReadOnlyList<FormulaNode> Operands => m_operands;

            public override TermValue Evaluate (EvaluationContext context)
            {
                bool isAnd = m_operators[0].Operator == eFormulaOperator.And;
                if (this.Kind == eTermKind.Bool)
                {
                    foreach (FormulaNode operand in m_operands)
                    {
                        TermValue value = operand.Evaluate(context);
                        if (context.HasFailed)
                        {
                            return default;
                        }

                        if ((value.Number != 0) != isAnd)
                        {
                            return TermValue.Computed(eTermKind.Bool, isAnd ? 0 : 1);
                        }
                    }

                    return TermValue.Computed(eTermKind.Bool, isAnd ? 1 : 0);
                }

                TermValue result = m_operands[0].Evaluate(context);
                for (int opIndex = 0; (opIndex < m_operators.Length) && !context.HasFailed; ++opIndex)
                {
                    TermValue right = m_operands[opIndex + 1].Evaluate(context);
                    if (context.HasFailed)
                    {
                        break;
                    }

                    FormulaSyntax.ChainOperator op = m_operators[opIndex];
                    bool isListSoFar = result.IsList || right.IsList;
                    result = ApplyBinary(
                        context,
                        op.Operator,
                        op.Index,
                        result,
                        right,
                        isListSoFar ? eTermKind.BoolList : eTermKind.Bool
                    );
                }

                return context.HasFailed ? default : result;
            }
        }

        public sealed class Conditional : FormulaNode
        {
            private readonly FormulaNode m_condition;
            private readonly FormulaNode m_whenTrue;
            private readonly FormulaNode m_whenFalse;

            public Conditional (
                int index,
                eTermKind kind,
                FormulaNode condition,
                FormulaNode whenTrue,
                FormulaNode whenFalse
            ) : base(index, kind)
            {
                m_condition = condition;
                m_whenTrue = whenTrue;
                m_whenFalse = whenFalse;
            }

            public FormulaNode WhenTrue => m_whenTrue;
            public FormulaNode WhenFalse => m_whenFalse;

            public override TermValue Evaluate (EvaluationContext context)
            {
                TermValue condition = m_condition.Evaluate(context);
                if (context.HasFailed)
                {
                    return default;
                }

                return (condition.Number != 0 ? m_whenTrue : m_whenFalse).Evaluate(context);
            }
        }

        /// <summary>
        /// A call to a table function, run with the body checked for exactly these argument kinds.
        /// </summary>
        public sealed class TableCall : FormulaNode
        {
            private readonly FormulaFunctionBody m_body;
            private readonly FormulaNode[] m_arguments;
            private readonly string m_callText;

            public TableCall (
                int index,
                FormulaFunctionBody body,
                FormulaNode[] arguments,
                string callText
            ) : base(index, body.ResultKind)
            {
                m_body = body;
                m_arguments = arguments;
                m_callText = callText;
            }

            public string FunctionName => m_body.Name;

            public override TermValue Evaluate (EvaluationContext context)
            {
                // The whole body counts every time it runs, which is what stops a chain of functions that each call the
                //  one before twice: short to write, and a billion calls to run
                if (!context.Charge(m_body.Cost, this.Index))
                {
                    return default;
                }

                var frame = new TermValue[m_body.Program.SlotCount];
                for (int argumentIndex = 0; argumentIndex < m_arguments.Length; ++argumentIndex)
                {
                    frame[argumentIndex] = m_arguments[argumentIndex].Evaluate(context);
                    if (context.HasFailed)
                    {
                        return default;
                    }
                }

                TermValue[] callerFrame = context.Frame;
                string caller = context.CurrentFunction;
                context.Frame = frame;
                context.CurrentFunction = m_body.Name;
                TermValue result = m_body.Program.Run(context);
                context.Frame = callerFrame;
                context.CurrentFunction = caller;

                if (context.HasFailed)
                {
                    context.ReportFailedCall(m_body.Name, m_callText, this.Index, isInFormula: caller == null);
                    return default;
                }

                return result;
            }
        }

        /// <summary>
        /// A call to a function the host wrote in C#. Its arguments arrive as the kinds it declared.
        /// </summary>
        public sealed class HostCall : FormulaNode
        {
            private readonly TermHostFunction m_function;
            private readonly FormulaNode[] m_arguments;
            private readonly string m_callText;

            public HostCall (
                int index,
                TermHostFunction function,
                FormulaNode[] arguments,
                string callText
            ) : base(index, function.Returns)
            {
                m_function = function;
                m_arguments = arguments;
                m_callText = callText;
            }

            public string FunctionName => m_function.Name;

            public override TermValue Evaluate (EvaluationContext context)
            {
                var arguments = new TermValue[m_arguments.Length];
                for (int argumentIndex = 0; argumentIndex < arguments.Length; ++argumentIndex)
                {
                    arguments[argumentIndex] = m_arguments[argumentIndex].Evaluate(context);
                    if (context.HasFailed)
                    {
                        return default;
                    }
                }

                // Host code isn't sandboxed and its exceptions aren't caught: catching them would hide a bug in the host
                TermValue result = m_function.Body(arguments);
                if (result.IsError)
                {
                    return context.Fail(this.Index, $"{m_callText} at {At(this.Index)}: {result.ErrorMessage}");
                }

                if (!TermKinds.Accepts(this.Kind, result.Kind))
                {
                    throw new InvalidOperationException(
                        $"The host function {m_function.Name} declared it returns {this.Kind},"
                            + $" and returned {result.DescribeForHost()}"
                    );
                }

                return result.AsKind(this.Kind);
            }
        }

        public sealed class BuiltIn : FormulaNode
        {
            private readonly eBuiltIn m_builtIn;
            private readonly FormulaNode[] m_arguments;
            private readonly string m_name;

            public BuiltIn (int index, eTermKind kind, eBuiltIn builtIn, string name, FormulaNode[] arguments) : base(index, kind)
            {
                m_builtIn = builtIn;
                m_name = name;
                m_arguments = arguments;
            }

            public string Name => m_name;

            public override TermValue Evaluate (EvaluationContext context)
            {
                var arguments = new TermValue[m_arguments.Length];
                for (int argumentIndex = 0; argumentIndex < arguments.Length; ++argumentIndex)
                {
                    arguments[argumentIndex] = m_arguments[argumentIndex].Evaluate(context);
                    if (context.HasFailed)
                    {
                        return default;
                    }
                }

                // Each list a built-in reads counts once per item
                foreach (TermValue argument in arguments)
                {
                    if (argument.IsList && !context.Charge(argument.Items.Length, this.Index))
                    {
                        return default;
                    }
                }

                switch (m_builtIn)
                {
                    case eBuiltIn.Sum: return this.Sum(context, arguments);
                    case eBuiltIn.Min: return this.Pick(context, arguments, isMax: false);
                    case eBuiltIn.Max: return this.Pick(context, arguments, isMax: true);
                    case eBuiltIn.Count: return TermValue.Computed(eTermKind.Int, Count(arguments[0]));
                    case eBuiltIn.Highest: return this.Keep(context, arguments, isHighest: true);
                    case eBuiltIn.Lowest: return this.Keep(context, arguments, isHighest: false);
                    case eBuiltIn.Join: return this.Join(context, arguments);
                    case eBuiltIn.Trunc: return this.EachItem(context, arguments, FormulaNumbers.eRounding.Truncate);
                    case eBuiltIn.Floor: return this.EachItem(context, arguments, FormulaNumbers.eRounding.Floor);
                    case eBuiltIn.Ceil: return this.EachItem(context, arguments, FormulaNumbers.eRounding.Ceiling);
                    case eBuiltIn.Round: return this.EachItem(context, arguments, FormulaNumbers.eRounding.AwayFromZero);
                    case eBuiltIn.Avg: return this.Average(context, arguments);
                    case eBuiltIn.Any: return TermValue.Computed(eTermKind.Bool, Count(arguments[0]) > 0 ? 1 : 0);
                    case eBuiltIn.All: return TermValue.Computed(eTermKind.Bool, AllTrue(arguments[0]) ? 1 : 0);
                    case eBuiltIn.Clamp: return this.Clamp(context, arguments);
                    default: return this.EachItem(context, arguments, FormulaNumbers.eRounding.AwayFromZero);
                }
            }

            private TermValue Average (EvaluationContext context, TermValue[] arguments)
            {
                TermValue[] items = (arguments.Length == 1) && arguments[0].IsList ? arguments[0].Items : arguments;
                if (items.Length == 0)
                {
                    return context.Fail(this.Index, $"avg() of an empty list at {At(this.Index)} has no answer.");
                }

                double total = 0;
                foreach (TermValue item in items)
                {
                    total += item.Number;
                }

                return TermValue.Computed(eTermKind.Float, total / items.Length);
            }

            private static bool AllTrue (TermValue list) => Count(list) == list.Items.Length;

            // An item already between low and high is given back as it is, label and all, the way min and max give back the
            //  item they pick. One past either bound becomes that bound, which has no label.
            private TermValue Clamp (EvaluationContext context, TermValue[] arguments)
            {
                double low = arguments[1].Number;
                double high = arguments[2].Number;
                if (low > high)
                {
                    return context.Fail(this.Index, FormulaBuiltIns.ClampBoundsProblem(low, high, this.Index));
                }

                TermValue value = arguments[0];
                eTermKind itemKind = TermKinds.ItemOf(this.Kind);
                if (!value.IsList)
                {
                    return ClampItem(value, low, high, itemKind);
                }

                var items = new TermValue[value.Items.Length];
                for (int itemIndex = 0; itemIndex < items.Length; ++itemIndex)
                {
                    items[itemIndex] = ClampItem(value.Items[itemIndex], low, high, itemKind);
                }

                return TermValue.Trusted(this.Kind, items);
            }

            private static TermValue ClampItem (TermValue item, double low, double high, eTermKind kind)
            {
                if (item.Number < low)
                {
                    return TermValue.Computed(kind, low);
                }

                return item.Number > high ? TermValue.Computed(kind, high) : item.AsKind(kind);
            }

            private TermValue Sum (EvaluationContext context, TermValue[] arguments)
            {
                TermValue[] items = (arguments.Length == 1) && arguments[0].IsList ? arguments[0].Items : arguments;
                double total = 0;
                foreach (TermValue item in items)
                {
                    total += item.Number;
                }

                return MakeNumber(context, this.Index, this.Kind, total);
            }

            // min and max give back the item itself, label and all, so a picked item labeled "Gold" still prints Gold. A tie
            //  goes to the earlier item.
            private TermValue Pick (EvaluationContext context, TermValue[] arguments, bool isMax)
            {
                TermValue[] items = (arguments.Length == 1) && arguments[0].IsList ? arguments[0].Items : arguments;
                if (items.Length == 0)
                {
                    return context.Fail(this.Index, $"{m_name}() of an empty list at {At(this.Index)} has no answer.");
                }

                TermValue picked = items[0];
                for (int itemIndex = 1; itemIndex < items.Length; ++itemIndex)
                {
                    double candidate = items[itemIndex].Number;
                    if (isMax ? candidate > picked.Number : candidate < picked.Number)
                    {
                        picked = items[itemIndex];
                    }
                }

                return picked.AsKind(this.Kind);
            }

            private static double Count (TermValue list)
            {
                if (list.Kind != eTermKind.BoolList)
                {
                    return list.Items.Length;
                }

                int trueCount = 0;
                foreach (TermValue item in list.Items)
                {
                    if (item.Number != 0)
                    {
                        ++trueCount;
                    }
                }

                return trueCount;
            }

            // highest and lowest keep the items in the order they came in, so highest of 3, 6, 1, 6 with n 2 is 6, 6 and
            //  lowest of the same with n 2 is 3, 1
            private TermValue Keep (EvaluationContext context, TermValue[] arguments, bool isHighest)
            {
                TermValue[] items = arguments[0].Items;
                double n = arguments[1].Number;
                if (n < 0)
                {
                    return context.Fail(
                        m_arguments[1].Index,
                        FormulaBuiltIns.KeepCountProblem(m_name, n, m_arguments[1].Index)
                    );
                }

                if (n >= items.Length)
                {
                    return arguments[0];
                }

                // Rank every item, best first, with a tie going to the earlier item, then keep the first n of the ranking in
                //  their original order
                int[] ranking = new int[items.Length];
                for (int itemIndex = 0; itemIndex < ranking.Length; ++itemIndex)
                {
                    ranking[itemIndex] = itemIndex;
                }

                Array.Sort(ranking, new RankComparer(items, isHighest));

                bool[] isKept = new bool[items.Length];
                for (int rank = 0; rank < (int)n; ++rank)
                {
                    isKept[ranking[rank]] = true;
                }

                var kept = new TermValue[(int)n];
                int keptCount = 0;
                for (int itemIndex = 0; itemIndex < items.Length; ++itemIndex)
                {
                    if (isKept[itemIndex])
                    {
                        kept[keptCount++] = items[itemIndex];
                    }
                }

                return TermValue.Trusted(this.Kind, kept);
            }

            private TermValue Join (EvaluationContext context, TermValue[] arguments)
            {
                string separator = arguments.Length > 1 ? arguments[1].AsString : ", ";
                var output = new StringBuilder();
                TermValue[] items = arguments[0].Items;
                for (int itemIndex = 0; itemIndex < items.Length; ++itemIndex)
                {
                    if (itemIndex > 0)
                    {
                        output.Append(separator);
                    }

                    items[itemIndex].PrintTo(output);
                }

                return context.Charge(output.Length, this.Index) ? TermValue.String(output.ToString()) : default;
            }

            // trunc, floor, ceil, round, round to places, fixed, label, and number: each works on one item, and item by item
            //  on a list
            private TermValue EachItem (EvaluationContext context, TermValue[] arguments, FormulaNumbers.eRounding rounding)
            {
                int places = 0;
                if (m_builtIn is eBuiltIn.RoundToPlaces or eBuiltIn.Fixed)
                {
                    double asked = arguments[1].Number;
                    if ((asked < 0) || (asked > FormulaBuiltIns.kMostDecimalPlaces))
                    {
                        return context.Fail(
                            m_arguments[1].Index,
                            FormulaBuiltIns.DecimalPlacesProblem(m_name, asked, m_arguments[1].Index)
                        );
                    }

                    places = (int)asked;
                }

                TermValue value = arguments[0];
                if (!value.IsList)
                {
                    return this.OneItem(context, value, rounding, places, this.Kind);
                }

                eTermKind itemKind = TermKinds.ItemOf(this.Kind);
                var items = new TermValue[value.Items.Length];
                for (int itemIndex = 0; itemIndex < items.Length; ++itemIndex)
                {
                    items[itemIndex] = this.OneItem(context, value.Items[itemIndex], rounding, places, itemKind);
                    if (context.HasFailed)
                    {
                        return default;
                    }
                }

                return TermValue.Trusted(this.Kind, items);
            }

            private TermValue OneItem (
                EvaluationContext context,
                TermValue item,
                FormulaNumbers.eRounding rounding,
                int places,
                eTermKind kind
            )
            {
                switch (m_builtIn)
                {
                    case eBuiltIn.RoundToPlaces:
                        return TermValue.Computed(kind, FormulaNumbers.RoundToPlaces(item.Number, places));

                    case eBuiltIn.Fixed:
                        return TermValue.String(FormulaNumbers.Fixed(item.Number, places));

                    case eBuiltIn.Label:
                        {
                            if (item.Label != null)
                            {
                                return TermValue.String(item.Label);
                            }

                            var plain = new StringBuilder();
                            item.PrintPlainTo(plain);
                            return context.Charge(plain.Length, this.Index) ? TermValue.String(plain.ToString()) : default;
                        }

                    case eBuiltIn.Number:
                        return item.Bare();

                    case eBuiltIn.Abs:
                        return TermValue.Computed(kind, Math.Abs(item.Number));

                    default:
                        return MakeNumber(context, this.Index, kind, FormulaNumbers.RoundWhole(item.Number, rounding));
                }
            }

            private sealed class RankComparer : IComparer<int>
            {
                private readonly TermValue[] m_items;
                private readonly bool m_isHighest;

                public RankComparer (TermValue[] items, bool isHighest)
                {
                    m_items = items;
                    m_isHighest = isHighest;
                }

                public int Compare (int left, int right)
                {
                    int byValue = m_items[left].Number.CompareTo(m_items[right].Number);
                    if (byValue != 0)
                    {
                        return m_isHighest ? -byValue : byValue;
                    }

                    return left.CompareTo(right);
                }
            }
        }
    }

    /// <summary>
    /// The steps of a formula or a function's body, in order. The last one's value is the result.
    /// </summary>
    internal sealed class FormulaProgram
    {
        private readonly FormulaNode[] m_statements;
        private readonly int[] m_slots;

        /// <summary>
        /// slots[i] is the step slot statement i sets, or -1 when it sets none
        /// </summary>
        public FormulaProgram (FormulaNode[] statements, int[] slots, int slotCount)
        {
            m_statements = statements;
            m_slots = slots;
            this.SlotCount = slotCount;
        }

        /// <summary>
        /// How many steps (and parameters, for a function) a run keeps values for
        /// </summary>
        public int SlotCount { get; }

        public TermValue Run (EvaluationContext context)
        {
            TermValue last = default;
            for (int index = 0; index < m_statements.Length; ++index)
            {
                last = m_statements[index].Evaluate(context);
                if (context.HasFailed)
                {
                    return default;
                }

                if (m_slots[index] >= 0)
                {
                    context.Frame[m_slots[index]] = last;
                }
            }

            return last;
        }
    }

    /// <summary>
    /// A table function's body, checked for one set of argument kinds.
    /// </summary>
    internal sealed class FormulaFunctionBody
    {
        public FormulaFunctionBody (string name, FormulaProgram program, eTermKind resultKind, int cost, int height)
        {
            this.Name = name;
            this.Program = program;
            this.ResultKind = resultKind;
            this.Cost = cost;
            this.Height = height;
        }

        public string Name { get; }
        public FormulaProgram Program { get; }
        public eTermKind ResultKind { get; }

        /// <summary>
        /// The work one call costs: one for every piece of the body, plus one for the call
        /// </summary>
        public int Cost { get; }

        /// <summary>
        /// How deep the body nests, counting the bodies of whatever it calls, so a call at depth d needs d + Height to stay
        /// within the depth limit
        /// </summary>
        public int Height { get; }
    }
}
