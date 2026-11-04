namespace AJut.Text
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A function the host writes in C#, for what text can't do, like reading the app's own data. Registered with
    /// <see cref="TermExpression.ParseFunctions"/> beside a table's functions, and checked and listed the same way.
    /// </summary>
    /// <remarks>
    /// The body gets its arguments as the kinds declared here (a whole number given for a number arrives as a number), and
    /// returns a value of the declared kind, or <see cref="TermValue.Error"/> to fail with a message for whoever typed the
    /// formula. It's host code: it isn't sandboxed, and what it throws isn't caught, since catching it would hide a bug.
    /// </remarks>
    public sealed class TermHostFunction
    {
        // ===========[ Setup/Construction/Teardown ]==========================

        /// <param name="name">What a formula calls it by</param>
        /// <param name="parameterKinds">The kind of each argument, in order</param>
        /// <param name="returns">The kind it gives back</param>
        /// <param name="body">What it does</param>
        /// <exception cref="ArgumentException">When <paramref name="name"/> isn't a function name, or a kind isn't one of
        /// <see cref="eTermKind"/>'s values</exception>
        public TermHostFunction (
            string name,
            IReadOnlyList<eTermKind> parameterKinds,
            eTermKind returns,
            Func<IReadOnlyList<TermValue>, TermValue> body
        )
        {
            ArgumentNullException.ThrowIfNull(parameterKinds);
            ArgumentNullException.ThrowIfNull(body);
            if (!FormulaLexer.IsValidName(name, isFunction: true))
            {
                throw new ArgumentException(
                    $"'{name}' isn't a function name: start with a letter or '_', then use letters, digits, and '_'",
                    nameof(name)
                );
            }

            foreach (eTermKind kind in parameterKinds)
            {
                if (!TermKinds.IsKnown(kind))
                {
                    throw new ArgumentException($"{kind} isn't a kind", nameof(parameterKinds));
                }
            }

            if (!TermKinds.IsKnown(returns))
            {
                throw new ArgumentException($"{returns} isn't a kind", nameof(returns));
            }

            this.Name = name;
            this.ParameterKinds = parameterKinds;
            this.Returns = returns;
            this.Body = body;
        }

        // ===========[ Properties ]===========================================

        public string Name { get; }
        public IReadOnlyList<eTermKind> ParameterKinds { get; }
        public eTermKind Returns { get; }
        public Func<IReadOnlyList<TermValue>, TermValue> Body { get; }
    }
}
