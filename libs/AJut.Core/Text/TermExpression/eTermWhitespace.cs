namespace AJut.Text
{
    /// <summary>
    /// What happens to whitespace inside a bare (unquoted) term. Set on <see cref="TermParseOptions.InnerWhitespace"/>.
    /// Either way the ends of a bare term are trimmed, and a quoted term keeps its whitespace exactly as written.
    /// </summary>
    public enum eTermWhitespace
    {
        /// <summary>
        /// Every run of whitespace inside a bare term becomes one space, so <c>Mr  Toot</c> (two spaces) or <c>Mr</c>, a tab,
        /// then <c>Toot</c> are both the term "Mr Toot". A stray double space can't quietly make a different term.
        /// </summary>
        CollapseToSingleSpace,

        /// <summary>
        /// Whitespace inside a bare term is kept exactly as written, so <c>Mr  Toot</c> (two spaces) is a different term from
        /// <c>Mr Toot</c>.
        /// </summary>
        Preserve,
    }
}
