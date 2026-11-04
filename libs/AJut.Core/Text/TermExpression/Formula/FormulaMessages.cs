namespace AJut.Text
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Wording shared by everything that reports a problem to whoever typed a formula
    /// </summary>
    internal static class FormulaMessages
    {
        // A suggestion is only worth making when the name is a slip of a key or two from a real one
        private const int kLongestNameToSuggestFor = 64;

        public static string At (int index) => FormulaLexer.At(index);

        /// <summary>
        /// For carrying one message on inside another: "Dividing by zero" reads as "dividing by zero" after a colon
        /// </summary>
        public static string LowerFirst (string message)
        {
            if (String.IsNullOrEmpty(message) || !Char.IsUpper(message[0]) || ((message.Length > 1) && Char.IsUpper(message[1])))
            {
                return message;
            }

            return Char.ToLowerInvariant(message[0]) + message.Substring(1);
        }

        /// <summary>
        /// " Did you mean $score?" for the known name closest to <paramref name="name"/>, or empty when nothing is close. A
        /// name that only differs in case is always suggested, since with names case-sensitive that's the likeliest slip.
        /// </summary>
        public static string Suggest (string name, IEnumerable<string> candidates, string prefix)
        {
            if (name.Length > kLongestNameToSuggestFor)
            {
                return String.Empty;
            }

            string best = null;
            int bestDistance = Int32.MaxValue;
            foreach (string candidate in candidates)
            {
                if (String.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                {
                    return $" Did you mean {prefix}{candidate}?";
                }

                if (candidate.Length > kLongestNameToSuggestFor)
                {
                    continue;
                }

                int distance = EditDistance(name, candidate);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            // One slip in a short name, two in a longer one. Any more and the suggestion is a guess.
            const int kLongestShortName = 4;
            int allowed = name.Length <= kLongestShortName ? 1 : 2;
            return (best != null) && (bestDistance <= allowed) ? $" Did you mean {prefix}{best}?" : String.Empty;
        }

        // Levenshtein distance, two rows at a time. Both names are short, which the callers make sure of.
        private static int EditDistance (string left, string right)
        {
            Span<int> previous = stackalloc int[right.Length + 1];
            Span<int> current = stackalloc int[right.Length + 1];
            for (int column = 0; column <= right.Length; ++column)
            {
                previous[column] = column;
            }

            for (int row = 1; row <= left.Length; ++row)
            {
                current[0] = row;
                for (int column = 1; column <= right.Length; ++column)
                {
                    int substitution = previous[column - 1] + (left[row - 1] == right[column - 1] ? 0 : 1);
                    current[column] = Math.Min(Math.Min(previous[column] + 1, current[column - 1] + 1), substitution);
                }

                current.CopyTo(previous);
            }

            return previous[right.Length];
        }
    }
}
