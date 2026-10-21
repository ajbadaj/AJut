namespace AJut.Text
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    // Reading text into an expression, plus the term rules that rendering shares with it
    public sealed partial class TermExpression
    {
        // ===========[ Public Interface Methods ]=============================

        /// <summary>
        /// Parses <paramref name="text"/> into an expression. When the text isn't one, the result's error says what's wrong in
        /// words and where, ready to show whoever typed it, and <see cref="ParseResult.ErrorIndex"/> points at the spot.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">When <see cref="TermParseOptions.MaxDepth"/> is negative, or
        /// <see cref="TermParseOptions.InnerWhitespace"/> isn't one of <see cref="eTermWhitespace"/>'s values</exception>
        public static ParseResult Parse (string text, TermParseOptions options = default)
        {
            if (options.MaxDepth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxDepth, "MaxDepth can't be negative");
            }

            if (options.InnerWhitespace is not (eTermWhitespace.CollapseToSingleSpace or eTermWhitespace.Preserve))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    options.InnerWhitespace,
                    "InnerWhitespace isn't a known value"
                );
            }

            int maxDepth = options.MaxDepth == 0 ? TermParseOptions.kDefaultMaxDepth : options.MaxDepth;
            bool collapseWhitespace = options.InnerWhitespace == eTermWhitespace.CollapseToSingleSpace;
            return new Parser(text ?? String.Empty, maxDepth, collapseWhitespace).Run();
        }

        // ===========[ Helpers ]==============================================

        // A bare term runs until one of these, which is why a term holding any of them needs quotes. Whitespace doesn't end a
        //  bare term, so Mr Toot | Tommy Boy is two terms with no quotes needed.
        private static bool EndsBareTerm (char c) => c is '&' or '|' or '(' or ')' or '"' or '!';

        private static bool IsOperator (char c) => c is '&' or '|';

        // Inside quotes, \" is a quote and \\ is a backslash. Any other backslash, and every backslash outside quotes, is just
        //  a backslash.
        private static bool IsEscapeAt (ReadOnlySpan<char> text, int index)
        {
            return (text[index] == '\\')
                && (index + 1 < text.Length)
                && (text[index + 1] is '"' or '\\');
        }

        private static bool HasLetterOrDigit (ReadOnlySpan<char> term)
        {
            foreach (Rune rune in term.EnumerateRunes())
            {
                if (Rune.IsLetterOrDigit(rune))
                {
                    return true;
                }
            }

            return false;
        }

        // Whether every bit of whitespace in the term is one ' ' between two other characters. That's what a bare term comes
        //  out as after its ends are trimmed and its whitespace collapsed, so it's also when a term can be written bare.
        private static bool HasOnlySingleSpacesInside (ReadOnlySpan<char> term)
        {
            for (int index = 0; index < term.Length; ++index)
            {
                if (!Char.IsWhiteSpace(term[index]))
                {
                    continue;
                }

                bool isInside = (index > 0) && (index < term.Length - 1);
                if ((term[index] != ' ') || !isInside || Char.IsWhiteSpace(term[index + 1]))
                {
                    return false;
                }
            }

            return true;
        }

        // Turns every run of whitespace inside a trimmed term into one space. Most terms have nothing to collapse, and those
        //  are copied as is.
        private static string CollapseWhitespace (ReadOnlySpan<char> term)
        {
            if (HasOnlySingleSpacesInside(term))
            {
                return term.ToString();
            }

            var collapsed = new StringBuilder(term.Length);
            bool isInWhitespace = false;
            foreach (char c in term)
            {
                if (!Char.IsWhiteSpace(c))
                {
                    collapsed.Append(c);
                    isInWhitespace = false;
                }
                else if (!isInWhitespace)
                {
                    collapsed.Append(' ');
                    isInWhitespace = true;
                }
            }

            return collapsed.ToString();
        }

        // A term is written bare only when parsing it bare, with whitespace collapsed, gives back exactly the same term. That
        //  keeps the canonical text reading back the same whichever eTermWhitespace it's parsed with.
        private static bool NeedsQuotes (string term)
        {
            foreach (char c in term)
            {
                if (EndsBareTerm(c))
                {
                    return true;
                }
            }

            return !HasOnlySingleSpacesInside(term) || !HasLetterOrDigit(term);
        }

        private static void AppendTerm (StringBuilder output, string term)
        {
            if (!NeedsQuotes(term))
            {
                output.Append(term);
                return;
            }

            output.Append('"');
            foreach (char c in term)
            {
                if ((c == '"') || (c == '\\'))
                {
                    output.Append('\\');
                }

                output.Append(c);
            }

            output.Append('"');
        }

        private static Node[] LayOut (ParsedNode root)
        {
            var nodes = new List<Node>();
            _LayOutInto(root, nodes);
            return nodes.ToArray();

            static void _LayOutInto (ParsedNode node, List<Node> output)
            {
                if (node.Kind == eNodeKind.Term)
                {
                    output.Add(Node.ForTerm(node.TermIndex, node.IsNegated));
                    return;
                }

                // The group's own entry goes in first, and gets its size once its children are in after it
                int groupIndex = output.Count;
                output.Add(default);
                foreach (ParsedNode child in node.Children)
                {
                    _LayOutInto(child, output);
                }

                output[groupIndex] = Node.ForGroup(
                    node.Kind,
                    node.IsNegated,
                    node.Children.Count,
                    size: output.Count - groupIndex
                );
            }
        }

        // ===========[ Subclasses/structs ]===================================

        /// <summary>
        /// A step of the expression while it's being parsed, before it's laid out flat.
        /// </summary>
        private sealed class ParsedNode
        {
            public static ParsedNode ForTerm (int termIndex)
            {
                return new ParsedNode { Kind = eNodeKind.Term, TermIndex = termIndex };
            }

            /// <summary>
            /// Builds a group of <paramref name="kind"/> from two or more operands, splicing in the children of any operand
            /// that's already a group of the same kind and isn't negated, since <c>(a &amp; b) &amp; c</c> is just
            /// <c>a &amp; b &amp; c</c>. That leaves every group nested inside another as a place where <c>&amp;</c> and
            /// <c>|</c> meet, which is what rendering relies on.
            /// </summary>
            public static ParsedNode ForGroup (eNodeKind kind, List<ParsedNode> operands)
            {
                var children = new List<ParsedNode>(operands.Count);
                foreach (ParsedNode operand in operands)
                {
                    if ((operand.Kind == kind) && !operand.IsNegated)
                    {
                        children.AddRange(operand.Children);
                    }
                    else
                    {
                        children.Add(operand);
                    }
                }

                return new ParsedNode { Kind = kind, Children = children };
            }

            public eNodeKind Kind { get; private init; }
            public int TermIndex { get; private init; } = -1;
            public List<ParsedNode> Children { get; private init; }
            public bool IsNegated { get; set; }
        }

        /// <summary>
        /// Reads one text into an expression, refusing it at the first problem.
        /// </summary>
        /// <remarks>
        /// Recursive descent over the text itself, with no separate list of tokens. A group is operands joined by operators,
        /// and an operand is any number of '!' in front of a bare term, a quoted term, or a group in parentheses. Each method
        /// gives back null once something's been refused, and the refusal is kept for <see cref="Run"/> to hand back.
        /// </remarks>
        private sealed class Parser
        {
            private readonly string m_text;
            private readonly int m_maxDepth;
            private readonly bool m_collapseWhitespace;
            private readonly List<string> m_terms = new List<string>();
            private readonly Dictionary<string, int> m_termIndexByText = new Dictionary<string, int>(StringComparer.Ordinal);
            private int m_position;
            private string m_error;
            private int m_errorIndex;

            // ===========[ Setup/Construction/Teardown ]==========================

            public Parser (string text, int maxDepth, bool collapseWhitespace)
            {
                m_text = text;
                m_maxDepth = maxDepth;
                m_collapseWhitespace = collapseWhitespace;
            }

            // ===========[ Properties ]===========================================

            private bool IsAtEnd => m_position >= m_text.Length;

            // ===========[ Public Interface Methods ]=============================

            public ParseResult Run ()
            {
                ParsedNode root = this.ReadGroup(depth: 0, openIndex: -1);
                return root == null
                    ? new ParseResult(m_error, m_errorIndex)
                    : new ParseResult(new TermExpression(LayOut(root), m_terms.ToArray()));
            }

            // ===========[ Helpers ]==============================================

            // Reads operands joined by operators until the text ends or a ')' closes the group. openIndex is where the group's
            //  '(' is, or -1 for the whole expression.
            private ParsedNode ReadGroup (int depth, int openIndex)
            {
                bool isWholeExpression = openIndex < 0;

                // 1. The first operand, which has nothing in front of it
                this.SkipWhitespace();
                if (this.IsAtEnd)
                {
                    return isWholeExpression
                        ? this.Fail(0, "The expression is empty.")
                        : this.Fail(openIndex, $"The '(' at {At(openIndex)} is never closed.");
                }

                if (m_text[m_position] == ')')
                {
                    return isWholeExpression
                        ? this.Fail(m_position, $"The ')' at {At(m_position)} has no '(' to close.")
                        : this.Fail(openIndex, $"The parentheses at {At(openIndex)} are empty.");
                }

                if (IsOperator(m_text[m_position]))
                {
                    if (!this.TryReadOperator(out OperatorToken leading))
                    {
                        return null;
                    }

                    return this.Fail(leading.Index, $"'{this.TextOf(leading)}' at {At(leading.Index)} has nothing before it.");
                }

                ParsedNode operand = this.ReadOperand(depth);
                if (operand == null)
                {
                    return null;
                }

                // 2. Then operators, each joining one more operand. operators[i] is the one between operands[i] and
                //  operands[i + 1], which is what both ways of writing a mixed group get built from.
                var operands = new List<ParsedNode> { operand };
                var operators = new List<eNodeKind>();
                OperatorToken firstOperator = default;
                OperatorToken mixedOperator = default;
                bool isMixed = false;
                while (true)
                {
                    this.SkipWhitespace();
                    if (this.IsAtEnd || (m_text[m_position] == ')'))
                    {
                        break;
                    }

                    if (!IsOperator(m_text[m_position]))
                    {
                        return this.Fail(
                            m_position,
                            $"Missing '&' or '|' before '{this.DescribeTokenAt(m_position)}' at {At(m_position)}."
                        );
                    }

                    if (!this.TryReadOperator(out OperatorToken nextOperator))
                    {
                        return null;
                    }

                    if (operators.Count == 0)
                    {
                        firstOperator = nextOperator;
                    }
                    else if (!isMixed && (nextOperator.Kind != firstOperator.Kind))
                    {
                        isMixed = true;
                        mixedOperator = nextOperator;
                    }

                    operators.Add(nextOperator.Kind);

                    this.SkipWhitespace();
                    if (this.IsAtEnd || (m_text[m_position] == ')') || IsOperator(m_text[m_position]))
                    {
                        return this.Fail(
                            nextOperator.Index,
                            $"'{this.TextOf(nextOperator)}' at {At(nextOperator.Index)} has nothing after it."
                        );
                    }

                    operand = this.ReadOperand(depth);
                    if (operand == null)
                    {
                        return null;
                    }

                    operands.Add(operand);
                }

                // 3. The group has to end the way it started: the whole expression at the end of the text, a group in
                //  parentheses at its ')'
                if (isWholeExpression && !this.IsAtEnd)
                {
                    return this.Fail(m_position, $"The ')' at {At(m_position)} has no '(' to close.");
                }

                if (!isWholeExpression)
                {
                    if (this.IsAtEnd)
                    {
                        return this.Fail(openIndex, $"The '(' at {At(openIndex)} is never closed.");
                    }

                    ++m_position;
                }

                // 4. Mixing & and | is only refused once the whole group has read cleanly, since the refusal shows the group
                //  written both ways
                if (isMixed)
                {
                    return this.FailMixed(openIndex, firstOperator, mixedOperator, operands, operators);
                }

                return operands.Count == 1 ? operands[0] : ParsedNode.ForGroup(firstOperator.Kind, operands);
            }

            // Only called where an operand starts, so never at the end of the text, a ')', or an operator
            private ParsedNode ReadOperand (int depth)
            {
                // 1. Any number of '!' in front, each one flipping what follows
                bool isNegated = false;
                int lastNotIndex = -1;
                while (!this.IsAtEnd && (m_text[m_position] == '!'))
                {
                    isNegated = !isNegated;
                    lastNotIndex = m_position;
                    ++m_position;
                    this.SkipWhitespace();
                }

                if ((lastNotIndex >= 0) && (this.IsAtEnd || (m_text[m_position] == ')') || IsOperator(m_text[m_position])))
                {
                    return this.Fail(lastNotIndex, $"'!' at {At(lastNotIndex)} has nothing after it.");
                }

                // 2. Then the operand itself
                ParsedNode operand = m_text[m_position] switch
                {
                    '(' => this.ReadParenthesizedGroup(depth),
                    '"' => this.ReadQuotedTerm(),
                    _ => this.ReadBareTerm(),
                };

                if ((operand != null) && isNegated)
                {
                    operand.IsNegated = !operand.IsNegated;
                }

                return operand;
            }

            private ParsedNode ReadParenthesizedGroup (int depth)
            {
                int openIndex = m_position;
                if (depth >= m_maxDepth)
                {
                    return this.Fail(openIndex, $"Parentheses nest more than {m_maxDepth} deep at {At(openIndex)}.");
                }

                ++m_position;
                return this.ReadGroup(depth + 1, openIndex);
            }

            private ParsedNode ReadQuotedTerm ()
            {
                int openIndex = m_position;
                int closeIndex = this.FindClosingQuote(openIndex, out bool hasEscapes);
                if (closeIndex < 0)
                {
                    return this.Fail(openIndex, $"The '\"' at {At(openIndex)} is never closed.");
                }

                if (closeIndex == openIndex + 1)
                {
                    return this.Fail(openIndex, $"The quotes at {At(openIndex)} are empty.");
                }

                m_position = closeIndex + 1;
                ReadOnlySpan<char> quoted = m_text.AsSpan(openIndex + 1, closeIndex - openIndex - 1);
                return ParsedNode.ForTerm(this.IndexOfTerm(hasEscapes ? Unescape(quoted) : quoted.ToString()));
            }

            // A bare term starts past any whitespace and runs to the next operator, parenthesis, quote or '!', with its trailing
            //  whitespace left off
            private ParsedNode ReadBareTerm ()
            {
                int start = m_position;
                m_position = this.FindBareTermEnd(start, out int writtenEnd);

                ReadOnlySpan<char> written = m_text.AsSpan(start, writtenEnd - start);
                if (!HasLetterOrDigit(written))
                {
                    return this.Fail(
                        start,
                        $"'{written.ToString()}' at {At(start)} is not a term: a term needs at least one letter or digit,"
                            + " or quotes around it."
                    );
                }

                string term = m_collapseWhitespace ? CollapseWhitespace(written) : written.ToString();
                return ParsedNode.ForTerm(this.IndexOfTerm(term));
            }

            // Gives back the index of whatever ends the bare term at start (or the end of the text), and sets writtenEnd to
            //  where the term itself stops once its trailing whitespace is left off
            private int FindBareTermEnd (int start, out int writtenEnd)
            {
                int end = start;
                while ((end < m_text.Length) && !EndsBareTerm(m_text[end]))
                {
                    ++end;
                }

                writtenEnd = end;
                while ((writtenEnd > start) && Char.IsWhiteSpace(m_text[writtenEnd - 1]))
                {
                    --writtenEnd;
                }

                return end;
            }

            // Reads a run of '&' or '|'. One is the operator, two is accepted for anybody used to writing && and ||, and a longer
            //  run is refused.
            private bool TryReadOperator (out OperatorToken token)
            {
                const int kLongestOperator = 2;

                int start = m_position;
                char symbol = m_text[start];
                while (!this.IsAtEnd && (m_text[m_position] == symbol))
                {
                    ++m_position;
                }

                token = new OperatorToken(symbol == '&' ? eNodeKind.And : eNodeKind.Or, start, m_position - start);
                if (token.Length > kLongestOperator)
                {
                    this.Fail(
                        start,
                        $"'{this.TextOf(token)}' at {At(start)} is not an operator: use '{symbol}'."
                    );
                    return false;
                }

                return true;
            }

            // Finds the '"' that closes the one at openIndex, stepping over escapes, or -1 when nothing closes it
            private int FindClosingQuote (int openIndex, out bool hasEscapes)
            {
                hasEscapes = false;
                for (int index = openIndex + 1; index < m_text.Length; ++index)
                {
                    if (m_text[index] == '"')
                    {
                        return index;
                    }

                    if (IsEscapeAt(m_text, index))
                    {
                        hasEscapes = true;
                        ++index;
                    }
                }

                return -1;
            }

            private static string Unescape (ReadOnlySpan<char> quoted)
            {
                var unescaped = new StringBuilder(quoted.Length);
                for (int index = 0; index < quoted.Length; ++index)
                {
                    if (IsEscapeAt(quoted, index))
                    {
                        ++index;
                    }

                    unescaped.Append(quoted[index]);
                }

                return unescaped.ToString();
            }

            private ParsedNode FailMixed (
                int openIndex,
                OperatorToken firstOperator,
                OperatorToken mixedOperator,
                List<ParsedNode> operands,
                List<eNodeKind> operators
            )
            {
                string andFirst = this.RenderReading(operands, operators, eNodeKind.And);
                string orFirst = this.RenderReading(operands, operators, eNodeKind.Or);
                string meeting = $"'{this.TextOf(firstOperator)}' and '{this.TextOf(mixedOperator)}' meet without parentheses"
                    + $" at {At(mixedOperator.Index)}.";

                // The whole expression can be shown rewritten as is. A group in parentheses is shown with its parentheses, so
                //  each way reads as a straight replacement for the group at that position.
                string rewrites = openIndex < 0
                    ? $"Write it as {andFirst} or {orFirst}."
                    : $"Write the group at {At(openIndex)} as ({andFirst}) or ({orFirst}).";

                return this.Fail(mixedOperator.Index, $"{meeting} {rewrites}");
            }

            // Writes a mixed group the way it reads when tighter joins first: each run of operands joined by tighter becomes
            //  a group, and the runs are joined by the other operator. A mixed group holds both, so there are always two or
            //  more runs.
            private string RenderReading (List<ParsedNode> operands, List<eNodeKind> operators, eNodeKind tighter)
            {
                var runs = new List<ParsedNode>();
                var run = new List<ParsedNode> { operands[0] };
                for (int operatorIndex = 0; operatorIndex < operators.Count; ++operatorIndex)
                {
                    if (operators[operatorIndex] != tighter)
                    {
                        runs.Add(_RunAsOperand(run));
                        run = new List<ParsedNode>();
                    }

                    run.Add(operands[operatorIndex + 1]);
                }

                runs.Add(_RunAsOperand(run));

                eNodeKind looser = tighter == eNodeKind.And ? eNodeKind.Or : eNodeKind.And;
                ParsedNode reading = ParsedNode.ForGroup(looser, runs);
                return new TermExpression(LayOut(reading), m_terms.ToArray()).ToString();

                ParsedNode _RunAsOperand (List<ParsedNode> runOperands)
                {
                    return runOperands.Count == 1 ? runOperands[0] : ParsedNode.ForGroup(tighter, runOperands);
                }
            }

            // The text of whatever starts at index, for naming it in a refusal
            private string DescribeTokenAt (int index)
            {
                char c = m_text[index];
                if (c is '(' or '!')
                {
                    return c.ToString();
                }

                int end;
                if (c == '"')
                {
                    int closeIndex = this.FindClosingQuote(index, out _);
                    end = closeIndex < 0 ? m_text.Length : closeIndex + 1;
                }
                else
                {
                    this.FindBareTermEnd(index, out end);
                }

                return m_text.Substring(index, end - index);
            }

            private int IndexOfTerm (string term)
            {
                if (!m_termIndexByText.TryGetValue(term, out int termIndex))
                {
                    termIndex = m_terms.Count;
                    m_terms.Add(term);
                    m_termIndexByText.Add(term, termIndex);
                }

                return termIndex;
            }

            private void SkipWhitespace ()
            {
                while (!this.IsAtEnd && Char.IsWhiteSpace(m_text[m_position]))
                {
                    ++m_position;
                }
            }

            private string TextOf (OperatorToken token) => m_text.Substring(token.Index, token.Length);

            private ParsedNode Fail (int index, string message)
            {
                m_errorIndex = index;
                m_error = message;
                return null;
            }

            // Errors count positions from 1, for whoever's reading them. ParseResult.ErrorIndex stays an index from 0.
            private static string At (int index) => $"position {index + 1}";

            // ===========[ Subclasses/structs ]===================================

            private readonly record struct OperatorToken (eNodeKind Kind, int Index, int Length);
        }
    }
}
