namespace AJut.Text
{
    /// <summary>
    /// How <see cref="TermExpression"/> decides a term is present, for both
    /// <see cref="TermExpression.Evaluate(System.Collections.Generic.IReadOnlyCollection{string}, TermMatchOptions)"/> and
    /// <see cref="TermExpression.IsMatch(string, TermMatchOptions)"/>. Both options are off by default, so
    /// <c>default(TermMatchOptions)</c> ignores case and finds a term anywhere, the way most find boxes start out.
    /// </summary>
    /// <remarks>
    /// Comparison is always ordinal, with or without case. A culture aware comparison can match text of a different length
    /// than the term (ignorable characters, or "ss" matching "ß"), which breaks the whole word check, and it makes whether
    /// something matches depend on the culture of the machine running it.
    /// </remarks>
    public readonly struct TermMatchOptions
    {
        /// <summary>
        /// Match case exactly (ordinal) instead of ignoring it (ordinal ignore case).
        /// </summary>
        public bool MatchCase { get; init; }

        /// <summary>
        /// When searching text, only count a match that isn't touching a word character on either side, so "pill" no longer
        /// matches inside "Pilly". A word character is a letter, a decimal digit, a combining mark, or connector punctuation
        /// like '_'. Membership is already whole term, so evaluating against a collection ignores this.
        /// </summary>
        public bool WholeWords { get; init; }
    }
}
