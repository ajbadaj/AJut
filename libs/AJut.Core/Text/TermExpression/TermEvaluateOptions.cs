namespace AJut.Text
{
    using System;

    /// <summary>
    /// What <see cref="TermFormula.Evaluate"/> allows. Everything is zero by default, and zero means the default value.
    /// </summary>
    public readonly struct TermEvaluateOptions
    {
        /// <summary>
        /// The <see cref="MaxWork"/> used when it's left at zero: far more than any formula written by hand needs.
        /// </summary>
        public const int kDefaultMaxWork = 1_000_000;

        /// <summary>
        /// How much work one evaluation can do before it stops with an error, or 0 for <see cref="kDefaultMaxWork"/>. Can't
        /// be negative.
        /// </summary>
        /// <remarks>
        /// A formula has no loops, and every step runs once, but functions calling functions can still multiply work: thirty
        /// table functions that each call the one before twice is about a billion calls from a few lines of text. So every
        /// function call counts its whole body, every list operation counts each item, and making text counts each character,
        /// and passing the limit stops the evaluation with "This took too much work to figure out."
        /// </remarks>
        public int MaxWork { get; init; }

        internal int MaxWorkOrDefault => this.MaxWork == 0 ? kDefaultMaxWork : this.MaxWork;

        internal static void ThrowIfOutOfRange (TermEvaluateOptions options)
        {
            if (options.MaxWork < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxWork, "MaxWork can't be negative");
            }
        }
    }
}
