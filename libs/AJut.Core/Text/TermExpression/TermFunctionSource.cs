namespace AJut.Text
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One function an author wrote, as text, for <see cref="TermExpression.ParseFunctions"/>: a name, its parameters' names,
    /// and a body. The host collects the name and parameters in fields of its own, so a definition line is never parsed.
    /// </summary>
    /// <remarks>
    /// The body is an ordinary formula: steps are allowed, and the last line is the result. It sees only its parameters
    /// (plus the built-ins and other functions), never the values of whatever formula calls it, so it means the same thing
    /// wherever it's called. A function with no parameters is a constant, like <c>taxRate()</c> with the body <c>0.2</c>.
    /// Keep the source text rather than anything parsed from it: text survives new versions of the engine.
    /// </remarks>
    public sealed class TermFunctionSource
    {
        // ===========[ Setup/Construction/Teardown ]==========================

        /// <param name="name">What a formula calls it by, with no '$'</param>
        /// <param name="parameters">The names its body uses for its arguments, with no '$', in order</param>
        /// <param name="body">The formula it runs</param>
        public TermFunctionSource (string name, IReadOnlyList<string> parameters, string body)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentNullException.ThrowIfNull(body);
            this.Name = name;
            this.Parameters = parameters;
            this.Body = body;
        }

        // ===========[ Properties ]===========================================

        public string Name { get; }
        public IReadOnlyList<string> Parameters { get; }
        public string Body { get; }
    }
}
