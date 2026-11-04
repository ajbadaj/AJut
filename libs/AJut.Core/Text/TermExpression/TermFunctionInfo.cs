namespace AJut.Text
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One function a formula can call, for an editor to list, complete, or explain.
    /// </summary>
    public sealed class TermFunctionInfo
    {
        // ===========[ Setup/Construction/Teardown ]==========================

        internal TermFunctionInfo (
            string name,
            string usage,
            string description,
            eTermFunctionSource source,
            IReadOnlyList<string> parameters
        )
        {
            this.Name = name;
            this.Usage = usage;
            this.Description = description;
            this.Source = source;
            this.Parameters = parameters ?? Array.Empty<string>();
        }

        // ===========[ Properties ]===========================================

        public string Name { get; }

        /// <summary>
        /// How a call is written, like <c>highest(list, n)</c>
        /// </summary>
        public string Usage { get; }

        /// <summary>
        /// What it does, in a sentence or two, or null for a table function
        /// </summary>
        public string Description { get; }

        public eTermFunctionSource Source { get; }

        /// <summary>
        /// A table function's parameter names, with no '$'. Empty for the others.
        /// </summary>
        public IReadOnlyList<string> Parameters { get; }
    }
}
