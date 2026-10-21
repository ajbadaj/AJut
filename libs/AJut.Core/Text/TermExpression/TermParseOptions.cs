namespace AJut.Text
{
    /// <summary>
    /// What <see cref="TermExpression.Parse"/> accepts. Everything is zero by default, and zero means the default value, so
    /// <c>default(TermParseOptions)</c> is the standard grammar.
    /// </summary>
    public readonly struct TermParseOptions
    {
        /// <summary>
        /// The <see cref="MaxDepth"/> used when it's left at zero.
        /// </summary>
        public const int kDefaultMaxDepth = 64;

        /// <summary>
        /// How deep parentheses can nest before the parse refuses the expression, or 0 for <see cref="kDefaultMaxDepth"/>.
        /// Can't be negative.
        /// </summary>
        /// <remarks>
        /// Parsing, evaluating, and rendering each recurse once per level of nesting, and running out of stack ends the process
        /// instead of throwing something you can catch. Since an expression often comes from whoever's typing, the limit is what
        /// keeps a hostile one from doing that, so only raise it as far as you actually need.
        /// </remarks>
        public int MaxDepth { get; init; }

        /// <summary>
        /// What happens to whitespace inside a bare term, <see cref="eTermWhitespace.CollapseToSingleSpace"/> by default.
        /// </summary>
        public eTermWhitespace InnerWhitespace { get; init; }
    }
}
