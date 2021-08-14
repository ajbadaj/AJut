namespace AJut.Text.AJson
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Rules that dictate any special rules for parsing json text
    /// </summary>
    public class ParserRules
    {
        /// <summary>
        /// TODO: The intent with this was to allow customization of what counts as a separator, in practice the actual
        /// parsing routine is not customizable yet so this goes relatively unused at the moment.
        /// </summary>
        public List<char> AdditionalSeparatorChars { get; } = new List<char>();

        /// <summary>
        /// Special markup for comments in your json - TECHNICALLY this is not supported (see http://www.json.com for spec info), but I choose to support it anyway.
        /// </summary>
        /// <example>
        /// An example might be...
        /// ParserRules rules = new ParserRules();
        /// rules.CommentIndicators.Add(new Tuple("//", "\n")); A line comment style, ie // comment there
        /// rules.CommentIndicators.Add(new Tuple("/*", "*/")); A block style comment, ie /* some comment */
        /// </example>
        public List<Tuple<string, string>> CommentIndicators { get; } = new List<Tuple<string, string>>();
    }

}
