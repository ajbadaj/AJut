namespace AJut.Text
{
    using System.Collections.Generic;

    /// <summary>
    /// A piece of a formula as it was written, before anything about kinds or names is known. The parser makes these and the
    /// checker turns them into <see cref="FormulaNode"/>s. A function's body stays as syntax, since it gets checked once for
    /// each set of argument kinds it's called with.
    /// </summary>
    internal abstract class FormulaSyntax
    {
        // ===========[ Setup/Construction/Teardown ]==========================

        protected FormulaSyntax (int start, int end)
        {
            this.Start = start;
            this.End = end;
        }

        // ===========[ Properties ]===========================================

        /// <summary>
        /// Where it starts in its text, as an index from 0
        /// </summary>
        public int Start { get; }

        /// <summary>
        /// The index just past where it ends
        /// </summary>
        public int End { get; }

        // ===========[ Public Interface Methods ]=============================

        /// <summary>
        /// The number <paramref name="syntax"/> is when it's written as one, like <c>15</c>, <c>-15</c> or <c>(2.5)</c>. A
        /// problem with a number written into the text doesn't depend on any value, so it can be refused while the author
        /// types instead of on some evaluations.
        /// </summary>
        public static bool TryGetWrittenNumber (FormulaSyntax syntax, out double value)
        {
            switch (syntax)
            {
                case Number number:
                    value = number.Value;
                    return true;

                case Group group:
                    return TryGetWrittenNumber(group.Inner, out value);

                case Unary unary when !unary.IsNot && TryGetWrittenNumber(unary.Operand, out double negated):
                    value = -negated;
                    return true;
            }

            value = 0;
            return false;
        }

        // ===========[ Subclasses/structs ]===================================

        /// <summary>
        /// A chain of operands joined by operators that bind equally tightly, like <c>$a + $b - $c</c>. Kept flat rather than
        /// nested, so a long chain is read and evaluated in a loop, and a megabyte of <c>1 + 1 + ...</c> never recurses.
        /// </summary>
        public enum eChain : byte
        {
            /// <summary>
            /// <c>&amp;&amp;</c> or <c>||</c>
            /// </summary>
            Logic,

            /// <summary>
            /// <c>== != &lt; &lt;= &gt; &gt;=</c>
            /// </summary>
            Comparison,

            /// <summary>
            /// <c>+ -</c>
            /// </summary>
            Additive,

            /// <summary>
            /// <c>* / %</c>
            /// </summary>
            Multiplicative,
        }

        public sealed class Number : FormulaSyntax
        {
            public Number (int start, int end, double value, bool isWhole) : base(start, end)
            {
                this.Value = value;
                this.IsWhole = isWhole;
            }

            public double Value { get; }

            /// <summary>
            /// Written with no '.', so it's an int
            /// </summary>
            public bool IsWhole { get; }
        }

        public sealed class Bool : FormulaSyntax
        {
            public Bool (int start, int end, bool value) : base(start, end)
            {
                this.Value = value;
            }

            public bool Value { get; }
        }

        public sealed class Variable : FormulaSyntax
        {
            public Variable (int start, int end, string name) : base(start, end)
            {
                this.Name = name;
            }

            /// <summary>
            /// The name as written, without its '$'
            /// </summary>
            public string Name { get; }
        }

        /// <summary>
        /// Literal text with values in it: quoted text in a formula, or a whole template.
        /// </summary>
        public sealed class Text : FormulaSyntax
        {
            public Text (int start, int end, List<TextPart> parts) : base(start, end)
            {
                this.Parts = parts;
            }

            public List<TextPart> Parts { get; }
        }

        /// <summary>
        /// A parenthesized expression. Kept, rather than replaced by what's inside, so a refusal can quote the text as written.
        /// </summary>
        public sealed class Group : FormulaSyntax
        {
            public Group (int start, int end, FormulaSyntax inner) : base(start, end)
            {
                this.Inner = inner;
            }

            public FormulaSyntax Inner { get; }
        }

        public sealed class Unary : FormulaSyntax
        {
            public Unary (int start, int end, bool isNot, FormulaSyntax operand) : base(start, end)
            {
                this.IsNot = isNot;
                this.Operand = operand;
            }

            /// <summary>
            /// '!' rather than '-'
            /// </summary>
            public bool IsNot { get; }

            public FormulaSyntax Operand { get; }
        }

        public sealed class Chain : FormulaSyntax
        {
            public Chain (
                int start,
                int end,
                eChain level,
                List<FormulaSyntax> operands,
                List<ChainOperator> operators
            ) : base(start, end)
            {
                this.Level = level;
                this.Operands = operands;
                this.Operators = operators;
            }

            public eChain Level { get; }
            public List<FormulaSyntax> Operands { get; }

            /// <summary>
            /// <c>Operators[i]</c> joins <c>Operands[i]</c> and <c>Operands[i + 1]</c>
            /// </summary>
            public List<ChainOperator> Operators { get; }
        }

        public sealed class Conditional : FormulaSyntax
        {
            public Conditional (
                int start,
                int end,
                FormulaSyntax condition,
                FormulaSyntax whenTrue,
                FormulaSyntax whenFalse,
                int questionIndex
            ) : base(start, end)
            {
                this.Condition = condition;
                this.WhenTrue = whenTrue;
                this.WhenFalse = whenFalse;
                this.QuestionIndex = questionIndex;
            }

            public FormulaSyntax Condition { get; }
            public FormulaSyntax WhenTrue { get; }
            public FormulaSyntax WhenFalse { get; }
            public int QuestionIndex { get; }
        }

        public sealed class Call : FormulaSyntax
        {
            public Call (int start, int end, string name, List<FormulaSyntax> arguments) : base(start, end)
            {
                this.Name = name;
                this.Arguments = arguments;
            }

            public string Name { get; }
            public List<FormulaSyntax> Arguments { get; }
        }

        /// <summary>
        /// <c>$name = value</c>, which only ever stands as a whole line.
        /// </summary>
        public sealed class Step : FormulaSyntax
        {
            public Step (int start, int end, string name, FormulaSyntax value) : base(start, end)
            {
                this.Name = name;
                this.Value = value;
            }

            public string Name { get; }
            public FormulaSyntax Value { get; }
        }

        /// <summary>
        /// One piece of a <see cref="Text"/>: literal text, or an expression to print there. IsInterpolation says the
        /// expression was written in <c>$( )</c> rather than as a bare <c>$name</c>, which nests a level deeper.
        /// </summary>
        public readonly record struct TextPart (string Literal, FormulaSyntax Expression, bool IsInterpolation);

        public readonly record struct ChainOperator (eFormulaOperator Operator, int Index);
    }

    /// <summary>
    /// An operator that joins two operands in a <see cref="FormulaSyntax.Chain"/>.
    /// </summary>
    internal enum eFormulaOperator : byte
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulo,
        EqualTo,
        NotEqualTo,
        LessThan,
        LessOrEqual,
        GreaterThan,
        GreaterOrEqual,
        And,
        Or,
    }

    internal static class FormulaOperators
    {
        public static string Symbol (eFormulaOperator op)
        {
            return op switch
            {
                eFormulaOperator.Add => "+",
                eFormulaOperator.Subtract => "-",
                eFormulaOperator.Multiply => "*",
                eFormulaOperator.Divide => "/",
                eFormulaOperator.Modulo => "%",
                eFormulaOperator.EqualTo => "==",
                eFormulaOperator.NotEqualTo => "!=",
                eFormulaOperator.LessThan => "<",
                eFormulaOperator.LessOrEqual => "<=",
                eFormulaOperator.GreaterThan => ">",
                eFormulaOperator.GreaterOrEqual => ">=",
                eFormulaOperator.And => "&&",
                _ => "||",
            };
        }
    }
}
