namespace AJut.Text
{
    using System;

    /// <summary>
    /// What a parse that starts at <see cref="TermExpression"/> accepts. Everything is zero by default, and zero means the
    /// default value, so <c>default(TermParseOptions)</c> is the standard grammar.
    /// </summary>
    public readonly struct TermParseOptions
    {
        /// <summary>
        /// The <see cref="MaxDepth"/> used when it's left at zero.
        /// </summary>
        public const int kDefaultMaxDepth = 64;

        /// <summary>
        /// How deep parentheses can nest before the parse refuses the expression, or 0 for <see cref="kDefaultMaxDepth"/>.
        /// Can't be negative. In a formula, a call's arguments, a unary '-' or '!', the sides of <c>? :</c>, and a
        /// <c>$( )</c> each nest a level too, and so does each table function a call reaches, so functions calling functions
        /// share the same limit.
        /// </summary>
        /// <remarks>
        /// Parsing, evaluating, and rendering each recurse once per level of nesting, and running out of stack ends the process
        /// instead of throwing something you can catch. Since an expression often comes from whoever's typing, the limit is what
        /// keeps a hostile one from doing that, so only raise it as far as you actually need.
        /// </remarks>
        public int MaxDepth { get; init; }

        /// <summary>
        /// What happens to whitespace inside a bare term of a matcher, <see cref="eTermWhitespace.CollapseToSingleSpace"/> by
        /// default.
        /// </summary>
        public eTermWhitespace InnerWhitespace { get; init; }

        /// <summary>
        /// Whether a formula's names ignore case, so <c>$Score</c> finds <c>$score</c> and <c>Max</c> finds <c>max</c>. Off by
        /// default, when names are case-sensitive.
        /// </summary>
        /// <remarks>
        /// Turning this on is a one-way choice for a host: text saved while it's on may only work while it's on, since two
        /// names that differ in case become one. A misspelled case still gets "did you mean" while it's off.
        /// </remarks>
        public bool IgnoreNameCase { get; init; }

        internal int MaxDepthOrDefault => this.MaxDepth == 0 ? kDefaultMaxDepth : this.MaxDepth;

        internal static void ThrowIfOutOfRange (TermParseOptions options)
        {
            if (options.MaxDepth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxDepth, "MaxDepth can't be negative");
            }
        }
    }
}
