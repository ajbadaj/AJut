namespace AJut.Text.AJson
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Rules that dictate any special rules for parsing json text.
    /// </summary>
    public class ParserRules
    {
        /// <summary>
        /// Extra characters that should be treated as structural separators by the
        /// indexer pass. The actual parsing routine is not yet customizable.
        /// </summary>
        public List<char> AdditionalSeparatorChars { get; } = new List<char>();

        /// <summary>
        /// Comment markers the parser will strip during the indexer pass. JSON proper
        /// does not allow comments - AJut supports them anyway by request.
        /// </summary>
        /// <example>
        /// rules.CommentIndicators.Add(new Tuple&lt;string,string&gt;("//", "\n")); // line comment
        /// rules.CommentIndicators.Add(new Tuple&lt;string,string&gt;("/*", "*/")); // block comment
        /// </example>
        public List<Tuple<string, string>> CommentIndicators { get; } = new List<Tuple<string, string>>();

        /// <summary>
        /// When true, the parser only accepts strict JSON - quoted keys, no comments, no
        /// trailing commas, no unquoted string values. Default false matches V1 lenient behavior.
        /// </summary>
        public bool StrictMode { get; set; } = false;

        /// <summary>
        /// Returns a default ParserRules with C-style line and block comments enabled.
        /// </summary>
        public static ParserRules WithDefaultComments ()
        {
            ParserRules rules = new ParserRules();
            rules.CommentIndicators.Add(new Tuple<string, string>("//", "\n"));
            rules.CommentIndicators.Add(new Tuple<string, string>("/*", "*/"));
            return rules;
        }
    }
}
