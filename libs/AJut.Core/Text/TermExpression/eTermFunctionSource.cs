namespace AJut.Text
{
    /// <summary>
    /// Where a function comes from. When two have the same name, a table's wins over a host's, and a host's over a built-in,
    /// so a built-in added later never breaks text somebody already saved.
    /// </summary>
    public enum eTermFunctionSource
    {
        /// <summary>
        /// One every formula has
        /// </summary>
        BuiltIn,

        /// <summary>
        /// One the host wrote in C# (<see cref="TermHostFunction"/>)
        /// </summary>
        Host,

        /// <summary>
        /// One an author wrote as text (<see cref="TermFunctionSource"/>)
        /// </summary>
        Table,
    }
}
