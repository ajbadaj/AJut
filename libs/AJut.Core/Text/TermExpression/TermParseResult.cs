namespace AJut.Text
{
    using System;
    using System.Collections.Generic;
    using AJut.Storage;

    /// <summary>
    /// What every parse that starts at <see cref="TermExpression"/> gives back: what was parsed, or what's wrong with the
    /// text and where, ready to show whoever typed it.
    /// </summary>
    /// <remarks>
    /// <see cref="Result.Errors"/> holds each error's message, and <see cref="TermErrors"/> holds the same errors with where
    /// each one is.
    /// </remarks>
    public sealed class TermParseResult<T> : Result<T>
    {
        // ===========[ Setup/Construction/Teardown ]==========================

        internal TermParseResult (T value) : base(value)
        {
            this.ErrorIndex = -1;
            this.TermErrors = Array.Empty<TermError>();
        }

        internal TermParseResult (TermError error) : this(new[] { error })
        {
        }

        internal TermParseResult (IReadOnlyList<TermError> errors, T value = default) : base(value)
        {
            foreach (TermError error in errors)
            {
                this.AddError(error.Message);
            }

            this.TermErrors = errors;
            this.ErrorIndex = errors.Count > 0 ? errors[0].Index : -1;
        }

        // ===========[ Properties ]===========================================

        /// <summary>
        /// Where the first problem is, as an index (from 0) you can point a caret at, or -1 when the parse succeeded. The error
        /// itself gives the same spot as a position counted from 1, for whoever's reading it. When the first error is inside a
        /// function's body, this is an index into that body (see <see cref="TermError.FunctionName"/>), and when it's about a
        /// function's name or parameters rather than its body, it's -1 there too.
        /// </summary>
        public int ErrorIndex { get; }

        /// <summary>
        /// Every error, with where each one is: in the parsed text itself, or in a function's body.
        /// </summary>
        public IReadOnlyList<TermError> TermErrors { get; }
    }
}
