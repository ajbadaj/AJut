namespace AJut.Text
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Splits a formula or a template into tokens, for both the parser and <see cref="TermExpression.TokenizeFormula"/>.
    /// Never fails: anything it can't read becomes an error token, and it carries on after it.
    /// </summary>
    /// <remarks>
    /// <para>Text and expressions nest inside each other: a template is text with <c>$( )</c> expressions in it, an
    /// expression can hold quoted text, and that text can hold <c>$( )</c> again. So the lexer keeps a stack of what it's
    /// inside. The stack only ever changes on characters it can see (a quote, a <c>$(</c>, the parenthesis that closes one),
    /// which is why splitting needs no help from the parser, and why the tokenizer can run it alone on broken text.</para>
    /// <para>A newline ends a step only at the top of a formula, outside every parenthesis. Everywhere else it's
    /// whitespace, so a long call can be split across lines.</para>
    /// </remarks>
    internal sealed class FormulaLexer
    {
        private readonly string m_text;
        private readonly List<Frame> m_frames = new List<Frame>();
        private readonly Token[] m_lookahead = new Token[2];
        private int m_lookaheadCount;
        private int m_position;

        // ===========[ Setup/Construction/Teardown ]==========================

        public FormulaLexer (string text, bool isTemplate)
        {
            m_text = text;
            m_frames.Add(new Frame(isTemplate ? eMode.Template : eMode.Expression, isInterpolation: false, openIndex: -1));
        }

        // ===========[ Public Interface Methods ]=============================

        public static bool IsNameChar (char c) => Char.IsLetterOrDigit(c) || (c == '_');

        public static bool IsFunctionNameStart (char c) => Char.IsLetter(c) || (c == '_');

        /// <summary>
        /// A name a host or a table gives a value or a function: what can follow a '$', or what a bare function name
        /// is
        /// </summary>
        public static bool IsValidName (string name, bool isFunction)
        {
            if (String.IsNullOrEmpty(name) || (isFunction && !IsFunctionNameStart(name[0])))
            {
                return false;
            }

            foreach (char c in name)
            {
                if (!IsNameChar(c))
                {
                    return false;
                }
            }

            return true;
        }

        public Token Peek (int offset = 0)
        {
            while (m_lookaheadCount <= offset)
            {
                m_lookahead[m_lookaheadCount++] = this.Read();
            }

            return m_lookahead[offset];
        }

        public Token Next ()
        {
            Token token = this.Peek();
            m_lookahead[0] = m_lookahead[1];
            --m_lookaheadCount;
            return token;
        }

        // ===========[ Helpers ]==============================================

        private bool IsAtEnd => m_position >= m_text.Length;

        private Token Read ()
        {
            if (m_frames.Count == 0)
            {
                return new Token(eTokenKind.End, m_text.Length, 0, null);
            }

            Frame frame = m_frames[^1];
            return frame.Mode == eMode.Expression ? this.ReadExpression(frame) : this.ReadText(frame);
        }

        private Token ReadText (Frame frame)
        {
            bool isQuoted = frame.Mode == eMode.Quoted;
            int start = m_position;
            if (this.IsAtEnd)
            {
                if (!isQuoted)
                {
                    return new Token(eTokenKind.End, start, 0, null);
                }

                // Nothing closes the quote. The error points at the quote that opened it, and takes up no text of its own.
                m_frames.RemoveAt(m_frames.Count - 1);
                return Error(frame.OpenIndex, 0, $"The '\"' at {At(frame.OpenIndex)} is never closed.");
            }

            char c = m_text[start];
            if (isQuoted && (c == '"'))
            {
                ++m_position;
                m_frames.RemoveAt(m_frames.Count - 1);
                return new Token(eTokenKind.QuoteClose, start, 1, null);
            }

            if (this.IsEscapeAt(start, isQuoted))
            {
                m_position += 2;
                return new Token(eTokenKind.Escape, start, 2, m_text[start + 1].ToString());
            }

            if (c == '$')
            {
                return this.ReadDollarInText(start);
            }

            // A run of plain text, up to whatever means something
            while (!this.IsAtEnd)
            {
                char next = m_text[m_position];
                if ((next == '$') || (isQuoted && (next == '"')) || this.IsEscapeAt(m_position, isQuoted))
                {
                    break;
                }

                ++m_position;
            }

            return new Token(eTokenKind.Text, start, m_position - start, m_text.Substring(start, m_position - start));
        }

        // \$ is a dollar sign and \\ a backslash, and in quoted text \" is a quote. Any other backslash is just a backslash.
        private bool IsEscapeAt (int index, bool isQuoted)
        {
            if ((m_text[index] != '\\') || (index + 1 >= m_text.Length))
            {
                return false;
            }

            char escaped = m_text[index + 1];
            return (escaped == '$') || (escaped == '\\') || (isQuoted && (escaped == '"'));
        }

        private Token ReadDollarInText (int start)
        {
            if ((start + 1 < m_text.Length) && (m_text[start + 1] == '('))
            {
                m_position += 2;
                m_frames.Add(new Frame(eMode.Expression, isInterpolation: true, openIndex: start));
                return new Token(eTokenKind.InterpolationOpen, start, 2, null);
            }

            if ((start + 1 < m_text.Length) && IsNameChar(m_text[start + 1]))
            {
                return this.ReadVariable(start);
            }

            ++m_position;
            return Error(
                start,
                1,
                $"A '$' needs a name or '(' after it, and the one at {At(start)} has neither."
                    + " For a dollar sign, write \\$."
            );
        }

        private Token ReadExpression (Frame frame)
        {
            // 1. Whitespace, where a newline at the top of a formula ends a step
            while (!this.IsAtEnd)
            {
                char space = m_text[m_position];
                if (space == '\n')
                {
                    if (!frame.IsInterpolation && (frame.ParenthesisDepth == 0))
                    {
                        ++m_position;
                        return new Token(eTokenKind.Newline, m_position - 1, 1, null);
                    }

                    ++m_position;
                }
                else if (Char.IsWhiteSpace(space))
                {
                    ++m_position;
                }
                else
                {
                    break;
                }
            }

            int start = m_position;
            if (this.IsAtEnd)
            {
                return new Token(eTokenKind.End, start, 0, null);
            }

            // 2. Then whatever starts here
            char c = m_text[start];
            if (c is >= '0' and <= '9')
            {
                return this.ReadNumber(start);
            }

            if (IsFunctionNameStart(c))
            {
                while (!this.IsAtEnd && IsNameChar(m_text[m_position]))
                {
                    ++m_position;
                }

                return new Token(eTokenKind.Name, start, m_position - start, m_text.Substring(start, m_position - start));
            }

            switch (c)
            {
                case '$':
                    if ((start + 1 < m_text.Length) && IsNameChar(m_text[start + 1]))
                    {
                        return this.ReadVariable(start);
                    }

                    ++m_position;
                    if (!this.IsAtEnd && (m_text[m_position] == '('))
                    {
                        ++m_position;
                        return Error(
                            start,
                            2,
                            $"'$(' at {At(start)} puts a value into text."
                                + " In a formula, use plain parentheses."
                        );
                    }

                    return Error(start, 1, $"A '$' needs a name after it, and the one at {At(start)} has none.");

                case '"':
                    ++m_position;
                    m_frames.Add(new Frame(eMode.Quoted, isInterpolation: false, openIndex: start));
                    return new Token(eTokenKind.QuoteOpen, start, 1, null);

                case '(':
                    ++m_position;
                    this.UpdateTop(frame.WithParenthesisDepth(frame.ParenthesisDepth + 1));
                    return new Token(eTokenKind.LeftParenthesis, start, 1, null);

                case ')':
                    ++m_position;
                    if (frame.ParenthesisDepth > 0)
                    {
                        this.UpdateTop(frame.WithParenthesisDepth(frame.ParenthesisDepth - 1));
                        return new Token(eTokenKind.RightParenthesis, start, 1, null);
                    }

                    if (frame.IsInterpolation)
                    {
                        m_frames.RemoveAt(m_frames.Count - 1);
                        return new Token(eTokenKind.InterpolationClose, start, 1, null);
                    }

                    return new Token(eTokenKind.RightParenthesis, start, 1, null);

                case '=':
                    return this.ReadOneOrTwo(start, '=', eTokenKind.Assign, eTokenKind.EqualTo);

                case '!':
                    return this.ReadOneOrTwo(start, '=', eTokenKind.Not, eTokenKind.NotEqualTo);

                case '<':
                    return this.ReadOneOrTwo(start, '=', eTokenKind.LessThan, eTokenKind.LessOrEqual);

                case '>':
                    return this.ReadOneOrTwo(start, '=', eTokenKind.GreaterThan, eTokenKind.GreaterOrEqual);

                case '&':
                case '|':
                    if ((start + 1 < m_text.Length) && (m_text[start + 1] == c))
                    {
                        m_position += 2;
                        return new Token(c == '&' ? eTokenKind.And : eTokenKind.Or, start, 2, null);
                    }

                    ++m_position;
                    return Error(start, 1, $"'{c}' at {At(start)} isn't an operator. Use '{c}{c}'.");
            }

            ++m_position;
            eTokenKind single = c switch
            {
                '+' => eTokenKind.Plus,
                '-' => eTokenKind.Minus,
                '*' => eTokenKind.Times,
                '/' => eTokenKind.Divide,
                '%' => eTokenKind.Modulo,
                '?' => eTokenKind.Question,
                ':' => eTokenKind.Colon,
                ',' => eTokenKind.Comma,
                ';' => eTokenKind.Semicolon,
                _ => eTokenKind.Error,
            };

            if (single != eTokenKind.Error)
            {
                return new Token(single, start, 1, null);
            }

            // A character outside the basic plane is two chars, and is shown (and skipped) whole
            if (Char.IsHighSurrogate(c) && !this.IsAtEnd && Char.IsLowSurrogate(m_text[m_position]))
            {
                ++m_position;
            }

            string written = m_text.Substring(start, m_position - start);
            return Error(start, m_position - start, $"'{written}' at {At(start)} can't be in a formula.");
        }

        private Token ReadNumber (int start)
        {
            while (!this.IsAtEnd && (m_text[m_position] is >= '0' and <= '9'))
            {
                ++m_position;
            }

            if ((m_position + 1 < m_text.Length) && (m_text[m_position] == '.') && (m_text[m_position + 1] is >= '0' and <= '9'))
            {
                ++m_position;
                while (!this.IsAtEnd && (m_text[m_position] is >= '0' and <= '9'))
                {
                    ++m_position;
                }
            }

            return new Token(eTokenKind.Number, start, m_position - start, m_text.Substring(start, m_position - start));
        }

        // A name runs to the first character that can't be in one, so $name's ends at the apostrophe
        private Token ReadVariable (int start)
        {
            m_position = start + 1;
            while (!this.IsAtEnd && IsNameChar(m_text[m_position]))
            {
                ++m_position;
            }

            return new Token(eTokenKind.Variable, start, m_position - start, m_text.Substring(start + 1, m_position - start - 1));
        }

        private Token ReadOneOrTwo (int start, char second, eTokenKind one, eTokenKind two)
        {
            if ((start + 1 < m_text.Length) && (m_text[start + 1] == second))
            {
                m_position += 2;
                return new Token(two, start, 2, null);
            }

            ++m_position;
            return new Token(one, start, 1, null);
        }

        private void UpdateTop (Frame frame) => m_frames[^1] = frame;

        private static Token Error (int start, int length, string message) => new Token(eTokenKind.Error, start, length, message);

        /// <summary>
        /// Messages count positions from 1, for whoever's reading them
        /// </summary>
        public static string At (int index) => $"position {index + 1}";

        // ===========[ Subclasses/structs ]===================================

        /// <summary>
        /// What a <see cref="Token"/> is.
        /// </summary>
        public enum eTokenKind : byte
        {
            /// <summary>
            /// The end of the text
            /// </summary>
            End,

            /// <summary>
            /// Something that can't be read. <see cref="Token.Value"/> is the message saying why.
            /// </summary>
            Error,

            /// <summary>
            /// A newline that ends a step
            /// </summary>
            Newline,
            Semicolon,

            /// <summary>
            /// A number, written with digits and maybe a '.'. <see cref="Token.Value"/> is how it's written.
            /// </summary>
            Number,

            /// <summary>
            /// A value's name with its '$'. <see cref="Token.Value"/> is the name without it.
            /// </summary>
            Variable,

            /// <summary>
            /// A name without a '$': a function or a keyword
            /// </summary>
            Name,

            QuoteOpen,
            QuoteClose,

            /// <summary>
            /// Literal text, in a template or between quotes. <see cref="Token.Value"/> is the text.
            /// </summary>
            Text,

            /// <summary>
            /// A backslash and what it stands for. <see cref="Token.Value"/> is the character it stands for.
            /// </summary>
            Escape,

            /// <summary>
            /// The '$(' that puts an expression into text
            /// </summary>
            InterpolationOpen,

            /// <summary>
            /// The ')' that closes a '$('
            /// </summary>
            InterpolationClose,

            LeftParenthesis,
            RightParenthesis,
            Comma,
            Plus,
            Minus,
            Times,
            Divide,
            Modulo,

            /// <summary>
            /// A single '=', which sets a step
            /// </summary>
            Assign,
            EqualTo,
            NotEqualTo,
            LessThan,
            LessOrEqual,
            GreaterThan,
            GreaterOrEqual,
            And,
            Or,
            Not,
            Question,
            Colon,
        }

        /// <summary>
        /// What the lexer is inside of.
        /// </summary>
        private enum eMode : byte
        {
            /// <summary>
            /// Formula code: a whole formula, or what's inside a '$('
            /// </summary>
            Expression,

            /// <summary>
            /// Text between quotes, inside an expression
            /// </summary>
            Quoted,

            /// <summary>
            /// The text of a template, which runs to the end
            /// </summary>
            Template,
        }

        /// <summary>
        /// One token: what it is, where it is, and, for some kinds, what it holds.
        /// </summary>
        public readonly record struct Token (eTokenKind Kind, int Start, int Length, string Value)
        {
            public int End => this.Start + this.Length;
        }

        private readonly struct Frame
        {
            public Frame (eMode mode, bool isInterpolation, int openIndex, int parenthesisDepth = 0)
            {
                this.Mode = mode;
                this.IsInterpolation = isInterpolation;
                this.OpenIndex = openIndex;
                this.ParenthesisDepth = parenthesisDepth;
            }

            public eMode Mode { get; }

            /// <summary>
            /// Whether this is the expression inside a '$(', which its own ')' closes
            /// </summary>
            public bool IsInterpolation { get; }

            /// <summary>
            /// Where the quote or '$(' that opened this is, or -1 for the whole text
            /// </summary>
            public int OpenIndex { get; }

            /// <summary>
            /// How many plain parentheses are open in this expression
            /// </summary>
            public int ParenthesisDepth { get; }

            public Frame WithParenthesisDepth (int depth) => new Frame(this.Mode, this.IsInterpolation, this.OpenIndex, depth);
        }
    }
}
