namespace AJut.Text
{
    /// <summary>
    /// One piece of a formula's or a template's text, for coloring it in an editor. From
    /// <see cref="TermExpression.TokenizeFormula"/> and <see cref="TermExpression.TokenizeTemplate"/>.
    /// </summary>
    /// <param name="Start">Where it starts, as an index from 0</param>
    /// <param name="Length">How many characters it covers</param>
    /// <param name="Kind">What it is</param>
    public readonly record struct TermToken (int Start, int Length, eTermTokenKind Kind);
}
