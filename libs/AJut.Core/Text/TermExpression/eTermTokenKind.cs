namespace AJut.Text
{
    /// <summary>
    /// What a <see cref="TermToken"/> is. Splitting text into tokens only looks at how it's written, so a name nobody
    /// declared is still a <see cref="Variable"/>: the parse is what says it's unknown.
    /// </summary>
    public enum eTermTokenKind
    {
        /// <summary>
        /// Literal text in a template
        /// </summary>
        Text,

        /// <summary>
        /// A value's name with its '$', like <c>$score</c>
        /// </summary>
        Variable,

        /// <summary>
        /// A number written in a formula
        /// </summary>
        Number,

        /// <summary>
        /// Quoted text in a formula, quotes included
        /// </summary>
        String,

        /// <summary>
        /// A backslash and what it stands for, like <c>\$</c>
        /// </summary>
        Escape,

        /// <summary>
        /// <c>+ - * / % == != &lt; &lt;= &gt; &gt;= &amp;&amp; || ! ? : =</c>
        /// </summary>
        Operator,

        /// <summary>
        /// Parentheses, commas, <c>;</c>, and the <c>$(</c> and <c>)</c> around a value in text
        /// </summary>
        Punctuation,

        /// <summary>
        /// A function's name, in front of its '('
        /// </summary>
        Function,

        /// <summary>
        /// <c>true</c> or <c>false</c>
        /// </summary>
        Keyword,

        /// <summary>
        /// Something that can't be read here
        /// </summary>
        Error,
    }
}
