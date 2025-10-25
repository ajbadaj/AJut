namespace AJut.Text.AJson
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Reduce the iteration time of json indexing, by looping through the whole string once and marking separators, 
    /// and then dealing directly with separators during json parsing.
    /// </summary>
    public class JsonTextIndexer
    {
        private const char kQuoteChar = '"';
        private readonly List<FoundSeparator> m_separatorsFound = new List<FoundSeparator>();

        private readonly HashSet<char> m_separatorChars = new HashSet<char>(9)
        {
            '{', '}', '[', ']', ':', ',', '\n', kQuoteChar, '\''
        };

        // ====================================[ Setup / Construction ]=================================================

        public JsonTextIndexer(string text, ParserRules rules = null)
        {
            rules = rules ?? new ParserRules();

            // Build separator flags from default set + rules
            foreach (char sep in rules.AdditionalSeparatorChars)
            {
                m_separatorChars.Add(sep);
            }

            // Pre-size the list to avoid resizing (assume ~10% of chars are separators)
            int estimatedSeparators = text.Length / 10;
            m_separatorsFound.Capacity = estimatedSeparators;

            this.ParseSeparators(text, rules);
        }


        // ====================================[ Public Interface Methods ]=================================================

        public int Next(char c, int start)
        {
            if (m_separatorsFound.Count == 0)
            {
                return -1;
            }

            // Binary search to find starting position
            int index = this.BinarySearchForStart(start);
            if (index < 0)
            {
                return -1;
            }

            // Linear scan from binary search result (already optimal position)
            for (int i = index; i < m_separatorsFound.Count; i++)
            {
                var sep = m_separatorsFound[i];
                if (sep.Char == c && sep.Index > start)
                {
                    return sep.Index;
                }
            }

            return -1;
        }

        public int NextAny(int start, params char[] set)
        {
            if (m_separatorsFound.Count == 0)
            {
                return -1;
            }

            // Binary search to find starting position
            int index = BinarySearchForStart(start);
            if (index < 0)
                return -1;

            // If no filter provided, return first result
            if (set == null || set.Length == 0)
            {
                var sep = m_separatorsFound[index];
                return sep.Index >= start ? sep.Index : -1;
            }

            // Using span for faster contains check
            ReadOnlySpan<char> filterSpan = set.AsSpan();

            for (int i = index; i < m_separatorsFound.Count; i++)
            {
                var sep = m_separatorsFound[i];
                if (sep.Index >= start && filterSpan.Contains(sep.Char))
                {
                    return sep.Index;
                }
            }

            return -1;
        }

        // ====================================[ Private Workhorse Methods ]=================================================

        private void ParseSeparators(string text, ParserRules rules)
        {
            ReadOnlySpan<char> textSpan = text.AsSpan();

            char[] commentStartCharsArray = null;
            ReadOnlySpan<char> commentStartChars = default;

            if (rules.CommentIndicators.Count > 0)
            {
                commentStartCharsArray = ArrayPool<char>.Shared.Rent(rules.CommentIndicators.Count);
                for (int i = 0; i < rules.CommentIndicators.Count; i++)
                {
                    commentStartCharsArray[i] = rules.CommentIndicators[i].Item1[0];
                }
                commentStartChars = commentStartCharsArray.AsSpan(0, rules.CommentIndicators.Count);
            }

            try
            {
                bool insideQuote = false;
                string commentEnd = null;

                for (int i = 0; i < textSpan.Length; i++)
                {
                    if (commentEnd != null)
                    {
                        // Check if we've reached comment end
                        int remaining = textSpan.Length - i;
                        if (remaining >= commentEnd.Length)
                        {
                            var slice = textSpan.Slice(i, commentEnd.Length);
                            if (slice.SequenceEqual(commentEnd.AsSpan()))
                            {
                                i += commentEnd.Length - 1;
                                commentEnd = null;
                            }
                        }

                        continue;
                    }

                    char ch = textSpan[i];

                    // Handle quoted sections
                    if (insideQuote)
                    {
                        // Check for end quote (with escape handling)
                        if (ch == kQuoteChar && (i == 0 || textSpan[i - 1] != '\\'))
                        {
                            insideQuote = false;
                            this.MarkFoundSeparator(i, kQuoteChar);
                        }

                        continue;
                    }

                    // Check for comment start (span-based)
                    if (!commentStartChars.IsEmpty && commentStartChars.Contains(ch))
                    {
                        for (int idx = 0; idx < rules.CommentIndicators.Count; idx++)
                        {
                            string commentStart = rules.CommentIndicators[idx].Item1;
                            int remaining = textSpan.Length - i;

                            if (remaining >= commentStart.Length)
                            {
                                var slice = textSpan.Slice(i, commentStart.Length);
                                if (slice.SequenceEqual(commentStart.AsSpan()))
                                {
                                    commentEnd = rules.CommentIndicators[idx].Item2;
                                    break;
                                }
                            }
                        }

                        if (commentEnd != null)
                        {
                            continue;
                        }
                    }

                    // Check for separators
                    if (m_separatorChars.Contains(ch))
                    {
                        if (ch == kQuoteChar)
                        {
                            insideQuote = true;
                        }

                        this.MarkFoundSeparator(i, ch);
                    }
                }
            }
            finally
            {
                if (commentStartCharsArray != null)
                {
                    ArrayPool<char>.Shared.Return(commentStartCharsArray);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MarkFoundSeparator(int index, char separator)
        {
            m_separatorsFound.Add(new FoundSeparator(index, separator));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int BinarySearchForStart(int targetIndex)
        {
            int left = 0;
            int right = m_separatorsFound.Count - 1;

            while (left <= right)
            {
                int mid = left + ((right - left) >> 1); // Faster than (left + right) / 2
                int midIndex = m_separatorsFound[mid].Index;

                if (midIndex < targetIndex)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            return left < m_separatorsFound.Count ? left : -1;
        }

        // ====================================[ Private Substructures ]=================================================

        [StructLayout(LayoutKind.Auto)]
        private readonly struct FoundSeparator
        {
            public readonly int Index;
            public readonly char Char;

            public FoundSeparator(int index, char ch)
            {
                Index = index;
                Char = ch;
            }

            public override string ToString() => $"{Index}] = '{Char}'";
        }
    }
}