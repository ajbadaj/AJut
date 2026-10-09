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
        /// Log a warning when the text read is a root document whose AJson version (0 when it has
        /// no version marker) is below this. Null follows
        /// <see cref="JsonHelper.WarnIfAJsonVersionBelow"/>, and 0 never warns.
        /// </summary>
        public int? WarnIfAJsonVersionBelow { get; set; }

        /// <summary>
        /// The AJson version to read text as when it has no version marker. Null, the default,
        /// lets the reader decide from the text itself. Below 2 reads every string as written,
        /// for text known to predate escaped strings; 2 or above decodes every string.
        /// </summary>
        public int? AssumeAJsonVersion { get; set; }

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
