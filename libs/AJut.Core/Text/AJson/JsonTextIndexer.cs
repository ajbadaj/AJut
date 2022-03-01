namespace AJut.Text.AJson
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Class that stores json separator char index locations for fast processing of json
    /// </summary>
    public class JsonTextIndexer
    {
        private const char QuoteChar = '\"';

        private readonly List<FoundSeparator> m_separatorOccurenceTracker = new List<FoundSeparator>();
        private readonly Dictionary<char, List<int>> m_separatorIndexTracker = new Dictionary<char, List<int>>
        {
            { '{', new List<int>() },
            { '}', new List<int>() },
            { '[', new List<int>() },
            { ']', new List<int>() },
            { ':', new List<int>() },
            { ',', new List<int>() },
            { '\n', new List<int>() },
            { QuoteChar, new List<int>() },
            { '\'', new List<int>() },
        };

        public JsonTextIndexer(string text, ParserRules rules = null)
        {
            rules = rules ?? new ParserRules();
            foreach(char separator in rules.AdditionalSeparatorChars)
            {
                m_separatorIndexTracker.Add(separator, new List<int>());
            }

            var commentStartChars = new List<char>(rules.CommentIndicators.Select(c => c.Item1.First()));

            bool isBetweenQuoteStartAndEnd = false;
            string targetCommentEnd = null;
            for(int currCharTargetIndex = 0; currCharTargetIndex < text.Length; ++currCharTargetIndex)
            {
                // If we're waiting for the end of a comment, then skip text until it's found
                if (targetCommentEnd != null)
                {
                    string subStr = text.Substring(currCharTargetIndex, targetCommentEnd.Length);
                    if (subStr == targetCommentEnd)
                    {
                        currCharTargetIndex += targetCommentEnd.Length - 1;
                        targetCommentEnd = null;
                    }

                    continue;
                }

                char charInQuestion = text[currCharTargetIndex];

                // If we've started looking for a quote, skip text until end quote is found
                if (isBetweenQuoteStartAndEnd)
                {
                    if (charInQuestion == QuoteChar && text[currCharTargetIndex - 1] != '\\')
                    {
                        isBetweenQuoteStartAndEnd = false;
                        _StoreFoundSeparators(currCharTargetIndex, QuoteChar, m_separatorIndexTracker[QuoteChar]);
                    }
                }
                else
                {
                    // First, look for comments, if we're starting one of those that superceeds everything else
                    for (int index = 0; index < commentStartChars.Count; ++index)
                    {
                        char commentStart = commentStartChars[index];
                        if (charInQuestion == commentStart)
                        {
                            string commentStr = rules.CommentIndicators[index].Item1;
                            string subStr = text.Substring(currCharTargetIndex, commentStr.Length);
                            if (subStr == commentStr)
                            {
                                targetCommentEnd = rules.CommentIndicators[index].Item2;
                                break;
                            }
                        }
                    }

                    // Second look for separators, that's the main goal!
                    if (targetCommentEnd == null)
                    {
                        foreach (var kvp in m_separatorIndexTracker)
                        {
                            if (kvp.Key == charInQuestion)
                            {
                                // Additionally, keep track of the separator is a quote, because in that case we need to ignore
                                //  everything until the end quote, so we need to go into quote searching mode.
                                if (kvp.Key == QuoteChar)
                                {
                                    isBetweenQuoteStartAndEnd = true;
                                }

                                _StoreFoundSeparators(currCharTargetIndex, charInQuestion, kvp.Value);
                                break;
                            }
                        }
                    }
                }
            }

            // Note: We're looping in order, thus all of our add calls will be putting items in index order
            //          without having to do binary searches so we'll just call add
            void _StoreFoundSeparators (int _characterIndex, char _separator, List<int> _separatorIndexTracker)
            {
                m_separatorOccurenceTracker.Add(new FoundSeparator(_characterIndex, _separator));
                _separatorIndexTracker.Add(_characterIndex);
            }
        }

        public int Next(char c, int start)
        {
            if(m_separatorIndexTracker.Count == 0)
            {
                return -1;
            }

            if(!m_separatorIndexTracker.ContainsKey(c))
            {
                return -1;
            }

            var list = m_separatorIndexTracker[c];
            if(list == null)
            {
                return -1;
            }

            int index = list.BinarySearch(start);
            if (index < 0)
            {
                index = ~index;
            }
            else
            {
                index = index + 1;
            }

            for (; index < list.Count; ++index)
            {
                if ( list[index] > start )
                {
                    return list[index];
                }
            }

            return -1;
        }

        public int NextAny(int start, params char[] set)
        {
            if (m_separatorOccurenceTracker.Count == 0)
            {
                return -1;
            }

            int index = m_separatorOccurenceTracker.BinarySearchXT(f => start.CompareTo(f.Index));
            if (index < 0)
            {
                index = ~index;
            }

            for (; index < m_separatorOccurenceTracker.Count; ++index)
            {
                var foundChar = m_separatorOccurenceTracker[index];
                if (foundChar.Index >= start && foundChar.PassesFilter(set))
                {
                    return m_separatorOccurenceTracker[index].Index;
                }
            }

            return -1;
        }

        private class FoundSeparator
        {
            public FoundSeparator (int index, char separator)
            {
                this.Index = index;
                this.SeparatorChar = separator;
            }

            public int Index { get; }
            public char SeparatorChar { get; }

            public bool PassesFilter (char[] targets)
            {
                return targets == null || targets.Length == 0 || targets.Contains(this.SeparatorChar);
            }

            public override string ToString ()
            {
                return $"{this.Index}] = '{this.SeparatorChar}'";
            }
        }
    }
}
