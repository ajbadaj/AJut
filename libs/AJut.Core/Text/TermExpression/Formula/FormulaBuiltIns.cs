namespace AJut.Text
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The functions every formula has, without a host or a table registering anything. A table's function or a
    /// host's of the same name wins over one of these, so adding one here in a later release never breaks text
    /// somebody already saved.
    /// </summary>
    internal static class FormulaBuiltIns
    {
        /// <summary>
        /// The most decimal places round() and fixed() take
        /// </summary>
        public const int kMostDecimalPlaces = 15;

        private static readonly Dictionary<string, eBuiltIn> g_byName = new Dictionary<string, eBuiltIn>(StringComparer.Ordinal);
        private static readonly Dictionary<string, eBuiltIn> g_byNameIgnoringCase
            = new Dictionary<string, eBuiltIn>(StringComparer.OrdinalIgnoreCase);

        // ===========[ Setup/Construction/Teardown ]==========================

        static FormulaBuiltIns ()
        {
            var all = new List<TermFunctionInfo>
            {
                _Add(
                    "sum",
                    eBuiltIn.Sum,
                    "sum(list) or sum(a, b, ...)",
                    "The total. A whole number when every item is one. An empty list gives 0."
                ),
                _Add(
                    "min",
                    eBuiltIn.Min,
                    "min(list) or min(a, b, ...)",
                    "The smallest item, with its label. An empty list is an error."
                ),
                _Add(
                    "max",
                    eBuiltIn.Max,
                    "max(list) or max(a, b, ...)",
                    "The largest item, with its label. An empty list is an error."
                ),
                _Add(
                    "count",
                    eBuiltIn.Count,
                    "count(list)",
                    "How many are true, for a list of true or false values. Otherwise how many items there are."
                ),
                _Add(
                    "highest",
                    eBuiltIn.Highest,
                    "highest(list, n)",
                    "The n highest items, with their labels, in the order they came in. A tie goes to the earlier item."
                ),
                _Add(
                    "lowest",
                    eBuiltIn.Lowest,
                    "lowest(list, n)",
                    "The n lowest items, with their labels, in the order they came in. A tie goes to the earlier item."
                ),
                _Add(
                    "join",
                    eBuiltIn.Join,
                    "join(list) or join(list, separator)",
                    "The items printed as they print, joined with \", \" or the separator."
                ),
                _Add(
                    "trunc",
                    eBuiltIn.Trunc,
                    "trunc(x)",
                    "The whole number toward zero: trunc(-3.5) is -3. Item by item on a list."
                ),
                _Add("floor", eBuiltIn.Floor, "floor(x)", "The whole number below: floor(-3.5) is -4. Item by item on a list."),
                _Add("ceil", eBuiltIn.Ceil, "ceil(x)", "The whole number above: ceil(3.2) is 4. Item by item on a list."),
                _Add(
                    "round",
                    eBuiltIn.Round,
                    "round(x) or round(x, places)",
                    "The nearest whole number, halves away from zero, or a number rounded to that many decimal places."
                ),
                _Add(
                    "fixed",
                    eBuiltIn.Fixed,
                    "fixed(x, places)",
                    "Text with exactly that many decimal places, halves away from zero: fixed(3.5, 2) is 3.50."
                ),
                _Add(
                    "label",
                    eBuiltIn.Label,
                    "label(x)",
                    "The label as text, or the number printed when there is none. Item by item on a list."
                ),
                _Add("number", eBuiltIn.Number, "number(x)", "The number without its label. Item by item on a list."),
                _Add(
                    "clamp",
                    eBuiltIn.Clamp,
                    "clamp(x, low, high)",
                    "x kept between low and high. Item by item on a list, and an item already inside keeps its label."
                ),
                _Add("abs", eBuiltIn.Abs, "abs(x)", "How far from zero, so abs(-3) is 3. Item by item on a list."),
                _Add(
                    "avg",
                    eBuiltIn.Avg,
                    "avg(list) or avg(a, b, ...)",
                    "The average, always a number that can have a fraction. An empty list is an error."
                ),
                _Add(
                    "any",
                    eBuiltIn.Any,
                    "any(list)",
                    "Whether any item of a list of true or false values is true. An empty list gives false."
                ),
                _Add(
                    "all",
                    eBuiltIn.All,
                    "all(list)",
                    "Whether every item of a list of true or false values is true. An empty list gives true."
                ),
                _Add(
                    "has",
                    eBuiltIn.Has,
                    "has($x)",
                    "Whether an optional value is set. An optional value can only be used where has() has checked it."
                ),
            };

            All = all.AsReadOnly();

            TermFunctionInfo _Add (string name, eBuiltIn builtIn, string usage, string description)
            {
                g_byName.Add(name, builtIn);
                g_byNameIgnoringCase.Add(name, builtIn);
                return new TermFunctionInfo(name, usage, description, eTermFunctionSource.BuiltIn, null);
            }
        }

        // ===========[ Properties ]===========================================

        public static IReadOnlyList<TermFunctionInfo> All { get; }

        public static IEnumerable<string> Names => g_byName.Keys;

        // ===========[ Public Interface Methods ]=============================

        // The same problems are found while typing, when the deciding argument is a written number, and while evaluating,
        //  when it comes from a value, and they read the same either way

        public static string ClampBoundsProblem (double low, double high, int callIndex)
        {
            return $"clamp() at {FormulaMessages.At(callIndex)} has a low of {FormulaNumbers.Format(low)}"
                + $" above its high of {FormulaNumbers.Format(high)}.";
        }

        public static string DecimalPlacesProblem (string name, double asked, int index)
        {
            return $"{name}() takes 0 to {kMostDecimalPlaces} decimal places,"
                + $" and is given {FormulaNumbers.Format(asked)} at {FormulaMessages.At(index)}.";
        }

        public static string KeepCountProblem (string name, double count, int index)
        {
            return $"{name}() can't keep {FormulaNumbers.Format(count)} items, at {FormulaMessages.At(index)}.";
        }

        public static string DivideByZeroProblem (int index) => $"Dividing by zero at {FormulaMessages.At(index)}.";

        public static string ModuloByZeroProblem (int index) => $"'%' by zero at {FormulaMessages.At(index)} has no answer.";

        /// <summary>
        /// The problem with a built-in call whose deciding argument is a written number, like <c>clamp($x, 300, -15)</c>,
        /// <c>round($x, 20)</c> or <c>highest($scores, -1)</c>, or null when there's none. When the argument comes from a
        /// value instead, the same problem is an evaluation error.
        /// </summary>
        public static string WrittenArgumentProblem (
            eBuiltIn builtIn,
            string name,
            IReadOnlyList<FormulaSyntax> arguments,
            int callIndex,
            out int problemIndex
        )
        {
            problemIndex = callIndex;
            switch (builtIn)
            {
                case eBuiltIn.Clamp when arguments.Count == 3:
                    if (FormulaSyntax.TryGetWrittenNumber(arguments[1], out double low)
                        && FormulaSyntax.TryGetWrittenNumber(arguments[2], out double high)
                        && (low > high))
                    {
                        return ClampBoundsProblem(low, high, callIndex);
                    }

                    break;

                case eBuiltIn.Round when arguments.Count == 2:
                case eBuiltIn.Fixed when arguments.Count == 2:
                    if (FormulaSyntax.TryGetWrittenNumber(arguments[1], out double places)
                        && ((places < 0) || (places > kMostDecimalPlaces)))
                    {
                        problemIndex = arguments[1].Start;
                        return DecimalPlacesProblem(name, places, problemIndex);
                    }

                    break;

                case eBuiltIn.Highest when arguments.Count == 2:
                case eBuiltIn.Lowest when arguments.Count == 2:
                    if (FormulaSyntax.TryGetWrittenNumber(arguments[1], out double count) && (count < 0))
                    {
                        problemIndex = arguments[1].Start;
                        return KeepCountProblem(name, count, problemIndex);
                    }

                    break;
            }

            return null;
        }

        /// <summary>
        /// The problem with a <c>/</c> or <c>%</c> by a written zero in <paramref name="chain"/>, like <c>$x / 0</c>, or null
        /// when there's none.
        /// </summary>
        public static string WrittenZeroDivisorProblem (FormulaSyntax.Chain chain, out int problemIndex)
        {
            problemIndex = -1;
            if (chain.Level != FormulaSyntax.eChain.Multiplicative)
            {
                return null;
            }

            for (int opIndex = 0; opIndex < chain.Operators.Count; ++opIndex)
            {
                FormulaSyntax.ChainOperator op = chain.Operators[opIndex];
                bool isDivision = op.Operator is eFormulaOperator.Divide or eFormulaOperator.Modulo;
                if (
                    isDivision
                    && FormulaSyntax.TryGetWrittenNumber(chain.Operands[opIndex + 1], out double divisor)
                    && (divisor == 0)
                )
                {
                    problemIndex = op.Index;
                    return op.Operator == eFormulaOperator.Divide ? DivideByZeroProblem(op.Index) : ModuloByZeroProblem(op.Index);
                }
            }

            return null;
        }

        public static bool TryFind (string name, bool ignoreCase, out eBuiltIn builtIn)
        {
            return (ignoreCase ? g_byNameIgnoringCase : g_byName).TryGetValue(name, out builtIn);
        }

        // ===========[ Subclasses/structs ]===================================

        public enum eBuiltIn : byte
        {
            Sum,
            Min,
            Max,
            Count,
            Highest,
            Lowest,
            Join,
            Trunc,
            Floor,
            Ceil,

            /// <summary>
            /// round(x), to a whole number
            /// </summary>
            Round,

            /// <summary>
            /// round(x, places), which the checker picks from round's arguments
            /// </summary>
            RoundToPlaces,
            Fixed,
            Label,
            Number,
            Has,
            Clamp,
            Abs,
            Avg,
            Any,
            All,
        }
    }
}
