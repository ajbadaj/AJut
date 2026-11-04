namespace AJut.Text
{
    using System;
    using System.Collections.Generic;
    using eTokenKind = FormulaLexer.eTokenKind;

    /// <summary>
    /// Where every term expression starts. A matcher tests terms, like <c>code &amp; (frontend | ui)</c> against a record's
    /// tags (<see cref="ParseMatcher"/>). A formula computes a value from values the host supplies, like
    /// <c>($tens + $ones + 99) % 100 + 1</c> (<see cref="ParseFormula"/>). A template is text with values in it, like
    /// <c>$wins wins, $losses losses</c> (<see cref="ParseTemplate"/>). A table's own functions, which formulas can call, are
    /// parsed once with <see cref="ParseFunctions"/>.
    /// </summary>
    /// <remarks>
    /// <para>Every parse gives back a <see cref="TermParseResult{T}"/>, which says what's wrong with the text and where instead
    /// of throwing, since the text usually comes from whoever's typing. What it parses never changes afterward, so one parse
    /// can be used from any number of threads at once.</para>
    /// <para>Nothing a formula's author writes can run code, reach anything outside the values and functions the host gives
    /// it, hang, or throw: there's no member access, no loops or recursion, nesting is capped, and evaluation has a work
    /// limit. The language itself is laid out on <see cref="ParseFormula"/>.</para>
    /// </remarks>
    public static class TermExpression
    {
        // ===========[ Properties ]===========================================

        /// <summary>
        /// Every built-in function, for an editor to list, complete, or explain.
        /// </summary>
        public static IReadOnlyList<TermFunctionInfo> BuiltInFunctions => FormulaBuiltIns.All;

        // ===========[ Public Interface Methods ]=============================

        /// <summary>
        /// Parses <paramref name="text"/> into a <see cref="TermMatcher"/>. When the text isn't one, the result's error says
        /// what's wrong in words and where, and <see cref="TermParseResult{T}.ErrorIndex"/> points at the spot. A null text is
        /// empty, and refused.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">When <see cref="TermParseOptions.MaxDepth"/> is negative, or
        /// <see cref="TermParseOptions.InnerWhitespace"/> isn't one of <see cref="eTermWhitespace"/>'s values</exception>
        public static TermParseResult<TermMatcher> ParseMatcher (string text, TermParseOptions options = default)
        {
            return TermMatcher.Parse(text, options);
        }

        /// <summary>
        /// Parses <paramref name="text"/> into a formula over the values <paramref name="declarations"/> declares, checking every
        /// name and kind, so mistakes show while the author types rather than only for some values.
        /// </summary>
        /// <remarks>
        /// <para>The language, briefly:</para>
        /// <list type="bullet">
        /// <item>Values are marked with '$' (<c>$score</c>), and a name with no '$' is a function or <c>true</c>/<c>false</c>.
        /// Numbers do spreadsheet math: <c>7 / 2</c> is 3.5, <c>%</c> wraps like MOD, and <c>trunc</c>, <c>floor</c>,
        /// <c>ceil</c>, and <c>round</c> (halves away from zero) give whole numbers.</item>
        /// <item><c>+ - * / %</c>, comparisons, <c>&amp;&amp; || !</c> and <c>cond ? a : b</c>. <c>&amp;&amp;</c> and
        /// <c>||</c> can't mix without parentheses.</item>
        /// <item>A list and a single value apply the value to every item, and two lists go item by item: <c>$d &gt;= 8</c>
        /// over a pool is a list of true or false values, and <c>count($d &gt;= 8)</c> counts them.</item>
        /// <item>Steps, one per line or separated by ';', each <c>$name = ...</c>, with the last line as the result.</item>
        /// <item>Quoted text takes values: <c>"$hits hits"</c>, and <c>$( expr )</c> for anything more than a name.</item>
        /// <item>An optional value can only be used where <c>has($x)</c> has checked it.</item>
        /// </list>
        /// </remarks>
        /// <param name="text">The formula</param>
        /// <param name="declarations">The values it can use, and the functions it can call</param>
        /// <param name="wants">The kind the host wants back, or null for any. A whole number is accepted where a number is
        /// wanted, anything is printed where text is wanted, and a result that can have a fraction where a whole number is
        /// wanted is an error that says to round it.</param>
        /// <param name="options">How it's parsed</param>
        /// <exception cref="ArgumentOutOfRangeException">When <see cref="TermParseOptions.MaxDepth"/> is negative, or
        /// <paramref name="wants"/> isn't one of <see cref="eTermKind"/>'s values</exception>
        /// <exception cref="ArgumentException">When <see cref="TermParseOptions.IgnoreNameCase"/> is set and two declared names
        /// differ only in case</exception>
        public static TermParseResult<TermFormula> ParseFormula (
            string text,
            TermDeclarations declarations,
            eTermKind? wants = null,
            TermParseOptions options = default
        )
        {
            ArgumentNullException.ThrowIfNull(declarations);
            TermParseOptions.ThrowIfOutOfRange(options);
            if (wants.HasValue && !TermKinds.IsKnown(wants.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(wants), wants.Value, "That isn't a kind");
            }

            text ??= String.Empty;
            declarations.Freeze(options.IgnoreNameCase);
            List<FormulaSyntax> statements = FormulaParser.ParseFormula(
                text,
                options.MaxDepthOrDefault,
                options.IgnoreNameCase,
                out TermError syntaxError
            );
            if (syntaxError != null)
            {
                return new TermParseResult<TermFormula>(syntaxError);
            }

            CheckedFormula checkedFormula = FormulaChecker.CheckFormula(text, statements, declarations, options, wants);
            return checkedFormula.IsSuccess
                ? new TermParseResult<TermFormula>(
                    new TermFormula(checkedFormula, declarations, text, options.IgnoreNameCase)
                )
                : new TermParseResult<TermFormula>(checkedFormula.Errors);
        }

        /// <summary>
        /// Parses <paramref name="text"/> as a template: text with <c>$name</c> and <c>$( expr )</c> in it, the same as quoted
        /// text in a formula without the quotes. It gives back a <see cref="TermFormula"/> that always gives text.
        /// </summary>
        /// <remarks>
        /// <c>\$</c> is a dollar sign and <c>\\</c> a backslash, and any other backslash is just a backslash. Quotes and
        /// every other character are plain text, except inside <c>$( )</c>, which is formula again. When text runs straight on
        /// from a name, use <c>$( )</c>: <c>$($decade)s</c>.
        /// </remarks>
        /// <inheritdoc cref="ParseFormula" path="/exception"/>
        public static TermParseResult<TermFormula> ParseTemplate (
            string text,
            TermDeclarations declarations,
            TermParseOptions options = default
        )
        {
            ArgumentNullException.ThrowIfNull(declarations);
            TermParseOptions.ThrowIfOutOfRange(options);

            text ??= String.Empty;
            declarations.Freeze(options.IgnoreNameCase);
            FormulaSyntax.Text template = FormulaParser.ParseTemplate(
                text,
                options.MaxDepthOrDefault,
                options.IgnoreNameCase,
                out TermError syntaxError
            );
            if (syntaxError != null)
            {
                return new TermParseResult<TermFormula>(syntaxError);
            }

            CheckedFormula checkedTemplate = FormulaChecker.CheckTemplate(text, template, declarations, options);
            return checkedTemplate.IsSuccess
                ? new TermParseResult<TermFormula>(
                    new TermFormula(checkedTemplate, declarations, text, options.IgnoreNameCase)
                )
                : new TermParseResult<TermFormula>(checkedTemplate.Errors);
        }

        /// <summary>
        /// Parses a table's functions, the ones its authors wrote and the ones its host wrote in C#, into one set that
        /// formulas can call (hand it to <see cref="TermDeclarations"/>). Parse it once per table.
        /// </summary>
        /// <remarks>
        /// <para>Each broken function gets an error of its own, with its name on it (<see cref="TermError.FunctionName"/>),
        /// and the result still carries a usable <see cref="TermFunctions"/>: the rest work, and a call to a broken one fails
        /// at the call. An error about a function's name or parameters rather than its body has an index of -1.</para>
        /// <para>A table function wins over a host function of the same name, and a host function over a built-in, and that's
        /// never an error. <see cref="IsBuiltIn"/> says when an editor should warn about it.</para>
        /// </remarks>
        /// <exception cref="ArgumentException">When two host functions have the same name</exception>
        public static TermParseResult<TermFunctions> ParseFunctions (
            IEnumerable<TermFunctionSource> functions,
            IEnumerable<TermHostFunction> hostFunctions = null,
            TermParseOptions options = default
        )
        {
            return TermFunctions.Parse(functions, hostFunctions, options);
        }

        /// <summary>
        /// Whether <paramref name="name"/> is a built-in function, which a table or host function of that name would take the
        /// place of. That's allowed, and an editor should warn about it.
        /// </summary>
        public static bool IsBuiltIn (string name, bool ignoreCase = false)
        {
            return (name != null) && FormulaBuiltIns.TryFind(name, ignoreCase, out _);
        }

        /// <summary>
        /// Splits a formula into tokens for coloring, by how it's written alone. Works on broken text too, and never needs the
        /// declarations: the parse is what finds unknown names.
        /// </summary>
        public static IReadOnlyList<TermToken> TokenizeFormula (string text) => Tokenize(text, isTemplate: false);

        /// <summary>
        /// Splits a template into tokens for coloring, the same way <see cref="TokenizeFormula"/> does.
        /// </summary>
        public static IReadOnlyList<TermToken> TokenizeTemplate (string text) => Tokenize(text, isTemplate: true);

        // ===========[ Helpers ]==============================================

        private static IReadOnlyList<TermToken> Tokenize (string text, bool isTemplate)
        {
            var lexer = new FormulaLexer(text ?? String.Empty, isTemplate);
            var tokens = new List<TermToken>();
            int quoteDepth = 0;
            while (true)
            {
                FormulaLexer.Token token = lexer.Next();
                if (token.Kind == eTokenKind.End)
                {
                    return tokens.AsReadOnly();
                }

                if (token.Length == 0)
                {
                    continue;
                }

                eTermTokenKind kind;
                switch (token.Kind)
                {
                    case eTokenKind.Newline:
                        continue;

                    case eTokenKind.QuoteOpen:
                        ++quoteDepth;
                        kind = eTermTokenKind.String;
                        break;

                    case eTokenKind.QuoteClose:
                        --quoteDepth;
                        kind = eTermTokenKind.String;
                        break;

                    case eTokenKind.Text:
                        kind = quoteDepth > 0 ? eTermTokenKind.String : eTermTokenKind.Text;
                        break;

                    case eTokenKind.Name:
                        kind = token.Value is "true" or "false"
                            ? eTermTokenKind.Keyword
                            : (lexer.Peek().Kind == eTokenKind.LeftParenthesis ? eTermTokenKind.Function : eTermTokenKind.Error);
                        break;

                    default:
                        kind = KindOf(token.Kind);
                        break;
                }

                tokens.Add(new TermToken(token.Start, token.Length, kind));
            }
        }

        private static eTermTokenKind KindOf (eTokenKind kind)
        {
            return kind switch
            {
                eTokenKind.Error => eTermTokenKind.Error,
                eTokenKind.Number => eTermTokenKind.Number,
                eTokenKind.Variable => eTermTokenKind.Variable,
                eTokenKind.Escape => eTermTokenKind.Escape,
                eTokenKind.Semicolon
                    or eTokenKind.InterpolationOpen
                    or eTokenKind.InterpolationClose
                    or eTokenKind.LeftParenthesis
                    or eTokenKind.RightParenthesis
                    or eTokenKind.Comma => eTermTokenKind.Punctuation,
                _ => eTermTokenKind.Operator,
            };
        }
    }
}
