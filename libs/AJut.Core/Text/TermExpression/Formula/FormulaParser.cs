namespace AJut.Text
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using Token = FormulaLexer.Token;
    using eTokenKind = FormulaLexer.eTokenKind;

    /// <summary>
    /// Reads a formula's or a template's text into <see cref="FormulaSyntax"/>, refusing it at the first problem. Only the
    /// shape of the text is checked here: names and kinds are the checker's.
    /// </summary>
    /// <remarks>
    /// <para>Recursive descent, from the loosest binding to the tightest: <c>? :</c>, then <c>&amp;&amp;</c> or
    /// <c>||</c>, then comparisons, then <c>+ -</c>, then <c>* / %</c>, then unary <c>- !</c>, then a value. Each level's
    /// operators are read in a loop into one flat chain, so only the things that really nest (parentheses, a call's
    /// arguments, a unary operand, the sides of <c>? :</c>, and a <c>$( )</c>) go a level deeper. Those are what
    /// <see cref="TermParseOptions.MaxDepth"/> counts, and checking and evaluating recurse on exactly the same ones, which is
    /// what keeps a hostile formula from running any of them out of stack.</para>
    /// </remarks>
    internal sealed class FormulaParser
    {
        private readonly FormulaLexer m_lexer;
        private readonly string m_text;
        private readonly int m_maxDepth;
        private readonly bool m_ignoreNameCase;
        private readonly StringBuilder m_literal = new StringBuilder();
        private int m_depth;
        private TermError m_error;

        // ===========[ Setup/Construction/Teardown ]==========================

        private FormulaParser (string text, bool isTemplate, int maxDepth, bool ignoreNameCase)
        {
            m_text = text;
            m_lexer = new FormulaLexer(text, isTemplate);
            m_maxDepth = maxDepth;
            m_ignoreNameCase = ignoreNameCase;
        }

        // ===========[ Public Interface Methods ]=============================

        /// <summary>
        /// Reads a formula: steps, one per line or separated by ';', with the last one as the result. Gives back null and an
        /// error when the text isn't one.
        /// </summary>
        public static List<FormulaSyntax> ParseFormula (string text, int maxDepth, bool ignoreNameCase, out TermError error)
        {
            var parser = new FormulaParser(text, isTemplate: false, maxDepth, ignoreNameCase);
            List<FormulaSyntax> statements = parser.ReadStatements();
            error = parser.m_error;
            return statements;
        }

        /// <summary>
        /// Reads a template: text with <c>$name</c> and <c>$( expr )</c> in it, running to the end.
        /// </summary>
        public static FormulaSyntax.Text ParseTemplate (string text, int maxDepth, bool ignoreNameCase, out TermError error)
        {
            var parser = new FormulaParser(text, isTemplate: true, maxDepth, ignoreNameCase);
            FormulaSyntax.Text template = parser.ReadTextParts(start: 0, closeIndex: -1);
            error = parser.m_error;
            return template;
        }

        // ===========[ Helpers ]==============================================

        private List<FormulaSyntax> ReadStatements ()
        {
            var statements = new List<FormulaSyntax>();
            while (true)
            {
                while (m_lexer.Peek().Kind is eTokenKind.Newline or eTokenKind.Semicolon)
                {
                    m_lexer.Next();
                }

                if (m_lexer.Peek().Kind == eTokenKind.End)
                {
                    break;
                }

                FormulaSyntax statement = this.ReadStatement();
                if (statement == null)
                {
                    return null;
                }

                statements.Add(statement);

                Token after = m_lexer.Peek();
                if (after.Kind is not (eTokenKind.Newline or eTokenKind.Semicolon or eTokenKind.End))
                {
                    return this.FailUnexpected<List<FormulaSyntax>>(after);
                }
            }

            if (statements.Count == 0)
            {
                return this.Fail<List<FormulaSyntax>>(0, "The formula is empty.");
            }

            return statements;
        }

        private FormulaSyntax ReadStatement ()
        {
            Token first = m_lexer.Peek();
            if ((first.Kind != eTokenKind.Variable) || (m_lexer.Peek(1).Kind != eTokenKind.Assign))
            {
                return this.ReadExpression();
            }

            m_lexer.Next();
            Token assign = m_lexer.Next();
            if (!CanStartValue(m_lexer.Peek()))
            {
                return this.Fail<FormulaSyntax>(
                    assign.Start,
                    $"'=' at {At(assign.Start)} has nothing after it to set ${first.Value} to."
                );
            }

            FormulaSyntax value = this.ReadExpression();
            return value == null ? null : new FormulaSyntax.Step(first.Start, value.End, first.Value, value);
        }

        private FormulaSyntax ReadExpression () => this.ReadConditional();

        private FormulaSyntax ReadConditional ()
        {
            FormulaSyntax condition = this.ReadChain(FormulaSyntax.eChain.Logic);
            if ((condition == null) || (m_lexer.Peek().Kind != eTokenKind.Question))
            {
                return condition;
            }

            Token question = m_lexer.Next();
            if (!this.Enter(question.Start))
            {
                return null;
            }

            if (!CanStartValue(m_lexer.Peek()))
            {
                return this.Fail<FormulaSyntax>(question.Start, $"'?' at {At(question.Start)} has nothing after it.");
            }

            FormulaSyntax whenTrue = this.ReadConditional();
            if (whenTrue == null)
            {
                return null;
            }

            if (m_lexer.Peek().Kind != eTokenKind.Colon)
            {
                return this.Fail<FormulaSyntax>(
                    question.Start,
                    $"The '?' at {At(question.Start)} needs a ':' after its first value, and a value for when it's false."
                );
            }

            Token colon = m_lexer.Next();
            if (!CanStartValue(m_lexer.Peek()))
            {
                return this.Fail<FormulaSyntax>(colon.Start, $"':' at {At(colon.Start)} has nothing after it.");
            }

            FormulaSyntax whenFalse = this.ReadConditional();
            if (whenFalse == null)
            {
                return null;
            }

            --m_depth;
            return new FormulaSyntax.Conditional(condition.Start, whenFalse.End, condition, whenTrue, whenFalse, question.Start);
        }

        // Reads operands joined by this level's operators into one flat chain, or gives back the one operand when there are
        //  no operators
        private FormulaSyntax ReadChain (FormulaSyntax.eChain level)
        {
            FormulaSyntax first = this.ReadChainOperand(level);
            if (first == null)
            {
                return null;
            }

            List<FormulaSyntax> operands = null;
            List<FormulaSyntax.ChainOperator> operators = null;
            while (TryGetOperator(level, m_lexer.Peek().Kind, out eFormulaOperator op))
            {
                Token opToken = m_lexer.Next();
                if (!CanStartValue(m_lexer.Peek()))
                {
                    return this.Fail<FormulaSyntax>(
                        opToken.Start,
                        $"'{FormulaOperators.Symbol(op)}' at {At(opToken.Start)} has nothing after it."
                    );
                }

                FormulaSyntax next = this.ReadChainOperand(level);
                if (next == null)
                {
                    return null;
                }

                operands ??= new List<FormulaSyntax> { first };
                operators ??= new List<FormulaSyntax.ChainOperator>();
                operands.Add(next);
                operators.Add(new FormulaSyntax.ChainOperator(op, opToken.Start));
            }

            if (operands == null)
            {
                return first;
            }

            if (level == FormulaSyntax.eChain.Logic)
            {
                for (int index = 1; index < operators.Count; ++index)
                {
                    if (operators[index].Operator != operators[0].Operator)
                    {
                        return this.FailMixedLogic(operands, operators, operators[index]);
                    }
                }
            }

            return new FormulaSyntax.Chain(first.Start, operands[^1].End, level, operands, operators);
        }

        private FormulaSyntax ReadChainOperand (FormulaSyntax.eChain level)
        {
            return level == FormulaSyntax.eChain.Multiplicative
                ? this.ReadUnary()
                : this.ReadChain(level + 1);
        }

        private FormulaSyntax ReadUnary ()
        {
            Token token = m_lexer.Peek();
            if (token.Kind is not (eTokenKind.Minus or eTokenKind.Not))
            {
                return this.ReadValue();
            }

            m_lexer.Next();
            if (!CanStartValue(m_lexer.Peek()))
            {
                return this.Fail<FormulaSyntax>(
                    token.Start,
                    $"'{(token.Kind == eTokenKind.Not ? '!' : '-')}' at {At(token.Start)} has nothing after it."
                );
            }

            if (!this.Enter(token.Start))
            {
                return null;
            }

            FormulaSyntax operand = this.ReadUnary();
            --m_depth;
            return operand == null
                ? null
                : new FormulaSyntax.Unary(token.Start, operand.End, token.Kind == eTokenKind.Not, operand);
        }

        private FormulaSyntax ReadValue ()
        {
            Token token = m_lexer.Peek();
            switch (token.Kind)
            {
                case eTokenKind.Number:
                    m_lexer.Next();
                    return this.ReadNumber(token);

                case eTokenKind.Variable:
                    m_lexer.Next();
                    return new FormulaSyntax.Variable(token.Start, token.End, token.Value);

                case eTokenKind.Name:
                    return this.ReadName();

                case eTokenKind.QuoteOpen:
                    m_lexer.Next();
                    return this.ReadTextParts(token.Start, closeIndex: token.Start);

                case eTokenKind.LeftParenthesis:
                    return this.ReadGroup();
            }

            if (IsBinaryOperator(token.Kind))
            {
                return this.Fail<FormulaSyntax>(
                    token.Start,
                    $"'{m_text.Substring(token.Start, token.Length)}' at {At(token.Start)} has nothing before it."
                );
            }

            return this.FailUnexpected<FormulaSyntax>(token);
        }

        private FormulaSyntax ReadNumber (Token token)
        {
            double value = Double.Parse(token.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
            bool isWhole = token.Value.IndexOf('.') < 0;
            if (isWhole && !FormulaNumbers.IsExactWhole(value))
            {
                return this.Fail<FormulaSyntax>(
                    token.Start,
                    $"{token.Value} at {At(token.Start)} is too big a whole number to hold exactly."
                        + $" The biggest is {FormulaNumbers.kLargestExactWhole:0}."
                );
            }

            return new FormulaSyntax.Number(token.Start, token.End, value, isWhole);
        }

        // A name without a '$' is a keyword or a function call, and nothing else
        private FormulaSyntax ReadName ()
        {
            Token name = m_lexer.Next();
            StringComparison comparison = m_ignoreNameCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (String.Equals(name.Value, "true", comparison) || String.Equals(name.Value, "false", comparison))
            {
                return new FormulaSyntax.Bool(name.Start, name.End, String.Equals(name.Value, "true", comparison));
            }

            if (m_lexer.Peek().Kind != eTokenKind.LeftParenthesis)
            {
                return this.Fail<FormulaSyntax>(
                    name.Start,
                    $"'{name.Value}' at {At(name.Start)} needs a '$' in front to be a value, or '(' after it to be a function."
                );
            }

            Token open = m_lexer.Next();
            if (!this.Enter(open.Start))
            {
                return null;
            }

            var arguments = new List<FormulaSyntax>();
            if (m_lexer.Peek().Kind == eTokenKind.RightParenthesis)
            {
                Token emptyClose = m_lexer.Next();
                --m_depth;
                return new FormulaSyntax.Call(name.Start, emptyClose.End, name.Value, arguments);
            }

            while (true)
            {
                if (m_lexer.Peek().Kind == eTokenKind.End)
                {
                    return this.Fail<FormulaSyntax>(open.Start, $"The '(' at {At(open.Start)} is never closed.");
                }

                FormulaSyntax argument = this.ReadExpression();
                if (argument == null)
                {
                    return null;
                }

                arguments.Add(argument);
                Token after = m_lexer.Next();
                if (after.Kind == eTokenKind.RightParenthesis)
                {
                    --m_depth;
                    return new FormulaSyntax.Call(name.Start, after.End, name.Value, arguments);
                }

                if (after.Kind == eTokenKind.End)
                {
                    return this.Fail<FormulaSyntax>(open.Start, $"The '(' at {At(open.Start)} is never closed.");
                }

                if (after.Kind != eTokenKind.Comma)
                {
                    return this.FailUnexpected<FormulaSyntax>(after);
                }

                if (!CanStartValue(m_lexer.Peek()))
                {
                    return this.Fail<FormulaSyntax>(after.Start, $"',' at {At(after.Start)} has nothing after it.");
                }
            }
        }

        private FormulaSyntax ReadGroup ()
        {
            Token open = m_lexer.Next();
            if (!this.Enter(open.Start))
            {
                return null;
            }

            Token first = m_lexer.Peek();
            if (first.Kind == eTokenKind.RightParenthesis)
            {
                return this.Fail<FormulaSyntax>(open.Start, $"The parentheses at {At(open.Start)} are empty.");
            }

            if (first.Kind == eTokenKind.End)
            {
                return this.Fail<FormulaSyntax>(open.Start, $"The '(' at {At(open.Start)} is never closed.");
            }

            FormulaSyntax inner = this.ReadExpression();
            if (inner == null)
            {
                return null;
            }

            Token close = m_lexer.Peek();
            if (close.Kind == eTokenKind.End)
            {
                return this.Fail<FormulaSyntax>(open.Start, $"The '(' at {At(open.Start)} is never closed.");
            }

            if (close.Kind != eTokenKind.RightParenthesis)
            {
                return this.FailUnexpected<FormulaSyntax>(close);
            }

            m_lexer.Next();
            --m_depth;
            return new FormulaSyntax.Group(open.Start, close.End, inner);
        }

        // Reads text up to its closing quote, or for a template (closeIndex -1) to the end. closeIndex is where the opening
        //  quote is.
        private FormulaSyntax.Text ReadTextParts (int start, int closeIndex)
        {
            bool isQuoted = closeIndex >= 0;
            var parts = new List<FormulaSyntax.TextPart>();
            m_literal.Clear();
            while (true)
            {
                Token token = m_lexer.Next();
                switch (token.Kind)
                {
                    case eTokenKind.Text:
                    case eTokenKind.Escape:
                        m_literal.Append(token.Value);
                        break;

                    case eTokenKind.Variable:
                        this.FlushLiteral(parts);
                        parts.Add(new FormulaSyntax.TextPart(
                            null,
                            new FormulaSyntax.Variable(token.Start, token.End, token.Value),
                            IsInterpolation: false
                        ));
                        break;

                    case eTokenKind.InterpolationOpen:
                        {
                            this.FlushLiteral(parts);
                            FormulaSyntax expression = this.ReadInterpolation(token);
                            if (expression == null)
                            {
                                return null;
                            }

                            parts.Add(new FormulaSyntax.TextPart(null, expression, IsInterpolation: true));
                            break;
                        }

                    case eTokenKind.QuoteClose:
                        this.FlushLiteral(parts);
                        return new FormulaSyntax.Text(start, token.End, parts);

                    case eTokenKind.End:
                        if (isQuoted)
                        {
                            return this.Fail<FormulaSyntax.Text>(closeIndex, $"The '\"' at {At(closeIndex)} is never closed.");
                        }

                        this.FlushLiteral(parts);
                        return new FormulaSyntax.Text(start, token.Start, parts);

                    default:
                        // Text only ever gives back text, values, escapes, and its own ends, so this is an error token
                        return this.Fail<FormulaSyntax.Text>(
                            token.Start,
                            token.Value ?? $"Something at {At(token.Start)} can't be read."
                        );
                }
            }
        }

        private FormulaSyntax ReadInterpolation (Token open)
        {
            if (!this.Enter(open.Start))
            {
                return null;
            }

            // Quoted text inside this $( ) shares the literal builder, which is safe since the text around it was flushed
            //  before it started
            Token first = m_lexer.Peek();
            if (first.Kind == eTokenKind.InterpolationClose)
            {
                return this.Fail<FormulaSyntax>(open.Start, $"The $( ) at {At(open.Start)} is empty.");
            }

            if (first.Kind == eTokenKind.End)
            {
                return this.Fail<FormulaSyntax>(open.Start, $"The '$(' at {At(open.Start)} is never closed.");
            }

            FormulaSyntax expression = this.ReadExpression();
            if (expression == null)
            {
                return null;
            }

            Token close = m_lexer.Next();
            if (close.Kind == eTokenKind.End)
            {
                return this.Fail<FormulaSyntax>(open.Start, $"The '$(' at {At(open.Start)} is never closed.");
            }

            if (close.Kind != eTokenKind.InterpolationClose)
            {
                return this.FailUnexpected<FormulaSyntax>(close);
            }

            --m_depth;
            return expression;
        }

        private void FlushLiteral (List<FormulaSyntax.TextPart> parts)
        {
            if (m_literal.Length > 0)
            {
                parts.Add(new FormulaSyntax.TextPart(m_literal.ToString(), null, IsInterpolation: false));
                m_literal.Clear();
            }
        }

        private bool Enter (int index)
        {
            if (m_depth >= m_maxDepth)
            {
                this.Fail<object>(index, $"This nests more than {m_maxDepth} deep at {At(index)}.");
                return false;
            }

            ++m_depth;
            return true;
        }

        // Shows the chain read both ways it could be meant, the way TermMatcher does for & and |, using the text as written
        private FormulaSyntax FailMixedLogic (
            List<FormulaSyntax> operands,
            List<FormulaSyntax.ChainOperator> operators,
            FormulaSyntax.ChainOperator mixed
        )
        {
            string andFirst = this.RenderReading(operands, operators, eFormulaOperator.And);
            string orFirst = this.RenderReading(operands, operators, eFormulaOperator.Or);
            return this.Fail<FormulaSyntax>(
                mixed.Index,
                $"'&&' and '||' meet without parentheses at {At(mixed.Index)}. Write it as {andFirst} or {orFirst}."
            );
        }

        private string RenderReading (
            List<FormulaSyntax> operands,
            List<FormulaSyntax.ChainOperator> operators,
            eFormulaOperator tighter
        )
        {
            string looser = tighter == eFormulaOperator.And ? " || " : " && ";
            var output = new StringBuilder();
            int runStart = 0;
            for (int index = 0; index <= operators.Count; ++index)
            {
                bool endsRun = (index == operators.Count) || (operators[index].Operator != tighter);
                if (!endsRun)
                {
                    continue;
                }

                if (output.Length > 0)
                {
                    output.Append(looser);
                }

                FormulaSyntax first = operands[runStart];
                FormulaSyntax last = operands[index];
                string runText = m_text.Substring(first.Start, last.End - first.Start);
                output.Append(index > runStart ? $"({runText})" : runText);
                runStart = index + 1;
            }

            return output.ToString();
        }

        private T FailUnexpected<T> (Token token) where T : class
        {
            string message = token.Kind switch
            {
                eTokenKind.Error => token.Value,
                eTokenKind.Assign => $"'=' at {At(token.Start)} sets a step, which only works at the start of a line,"
                    + " like $hits = ... To compare, use '=='.",
                eTokenKind.RightParenthesis => $"The ')' at {At(token.Start)} has no '(' to close.",
                eTokenKind.Comma => $"',' at {At(token.Start)} only goes between a function's arguments.",
                eTokenKind.Colon => $"':' at {At(token.Start)} has no '?' before it.",
                eTokenKind.Semicolon => $"';' at {At(token.Start)} ends a step, so it can't be inside parentheses or a $( ).",
                eTokenKind.Question => $"'?' at {At(token.Start)} has nothing before it.",
                eTokenKind.End => $"Something is missing at the end, {At(token.Start)}.",
                eTokenKind.Newline => $"Something is missing at the end of the line, {At(token.Start)}.",
                _ when CanStartValue(token) => $"Something is missing before '{this.TextOf(token)}' at {At(token.Start)}:"
                    + (m_depth == 0
                        ? " an operator, or a new line for a new step."
                        : " an operator, or a ',' between arguments."),
                _ => $"'{this.TextOf(token)}' at {At(token.Start)} doesn't belong there.",
            };

            return this.Fail<T>(token.Start, message);
        }

        private string TextOf (Token token)
        {
            const int kLongestQuoted = 24;
            string written = m_text.Substring(token.Start, Math.Min(token.Length, kLongestQuoted));
            return token.Length > kLongestQuoted ? written + "..." : written;
        }

        private T Fail<T> (int index, string message) where T : class
        {
            m_error ??= new TermError(message, index);
            return null;
        }

        private static string At (int index) => FormulaLexer.At(index);

        private static bool CanStartValue (Token token)
        {
            return token.Kind is eTokenKind.Number
                or eTokenKind.Variable
                or eTokenKind.Name
                or eTokenKind.QuoteOpen
                or eTokenKind.LeftParenthesis
                or eTokenKind.Minus
                or eTokenKind.Not;
        }

        private static bool IsBinaryOperator (eTokenKind kind)
        {
            return kind is eTokenKind.Plus
                or eTokenKind.Times
                or eTokenKind.Divide
                or eTokenKind.Modulo
                or eTokenKind.EqualTo
                or eTokenKind.NotEqualTo
                or eTokenKind.LessThan
                or eTokenKind.LessOrEqual
                or eTokenKind.GreaterThan
                or eTokenKind.GreaterOrEqual
                or eTokenKind.And
                or eTokenKind.Or;
        }

        private static bool TryGetOperator (FormulaSyntax.eChain level, eTokenKind kind, out eFormulaOperator op)
        {
            switch (level)
            {
                case FormulaSyntax.eChain.Logic when kind == eTokenKind.And:
                    op = eFormulaOperator.And;
                    return true;
                case FormulaSyntax.eChain.Logic when kind == eTokenKind.Or:
                    op = eFormulaOperator.Or;
                    return true;
                case FormulaSyntax.eChain.Comparison when kind == eTokenKind.EqualTo:
                    op = eFormulaOperator.EqualTo;
                    return true;
                case FormulaSyntax.eChain.Comparison when kind == eTokenKind.NotEqualTo:
                    op = eFormulaOperator.NotEqualTo;
                    return true;
                case FormulaSyntax.eChain.Comparison when kind == eTokenKind.LessThan:
                    op = eFormulaOperator.LessThan;
                    return true;
                case FormulaSyntax.eChain.Comparison when kind == eTokenKind.LessOrEqual:
                    op = eFormulaOperator.LessOrEqual;
                    return true;
                case FormulaSyntax.eChain.Comparison when kind == eTokenKind.GreaterThan:
                    op = eFormulaOperator.GreaterThan;
                    return true;
                case FormulaSyntax.eChain.Comparison when kind == eTokenKind.GreaterOrEqual:
                    op = eFormulaOperator.GreaterOrEqual;
                    return true;
                case FormulaSyntax.eChain.Additive when kind == eTokenKind.Plus:
                    op = eFormulaOperator.Add;
                    return true;
                case FormulaSyntax.eChain.Additive when kind == eTokenKind.Minus:
                    op = eFormulaOperator.Subtract;
                    return true;
                case FormulaSyntax.eChain.Multiplicative when kind == eTokenKind.Times:
                    op = eFormulaOperator.Multiply;
                    return true;
                case FormulaSyntax.eChain.Multiplicative when kind == eTokenKind.Divide:
                    op = eFormulaOperator.Divide;
                    return true;
                case FormulaSyntax.eChain.Multiplicative when kind == eTokenKind.Modulo:
                    op = eFormulaOperator.Modulo;
                    return true;
            }

            op = default;
            return false;
        }
    }
}
