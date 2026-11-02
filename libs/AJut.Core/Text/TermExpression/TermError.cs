namespace AJut.Text
{
    /// <summary>
    /// One problem with a parse or an evaluation, and where it is.
    /// </summary>
    /// <remarks>
    /// A problem inside a table function is reported twice: once at the call, in the text that made it, and once in the
    /// function's own body, with <see cref="FunctionName"/> saying which. That lets an editor show the same mistake on the
    /// formula that calls the function and on the function's own box.
    /// </remarks>
    public sealed class TermError
    {
        // ===========[ Setup/Construction/Teardown ]==========================

        internal TermError (string message, int index, string functionName = null)
        {
            this.Message = message;
            this.Index = index;
            this.FunctionName = functionName;
        }

        // ===========[ Properties ]===========================================

        /// <summary>
        /// What's wrong, in words for whoever typed it. Any position it mentions counts from 1.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Where the problem is, as an index from 0: into the parsed text, or into the body of <see cref="FunctionName"/>.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// The table function whose body <see cref="Index"/> points into, or null when it points into the parsed text itself.
        /// </summary>
        public string FunctionName { get; }

        // ===========[ Public Interface Methods ]=============================

        public override string ToString () => this.Message;
    }
}
