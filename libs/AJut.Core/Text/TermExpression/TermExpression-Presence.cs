namespace AJut.Text
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using System.Text;

    // The ways a term can count as present: held by a collection, found in text, or whatever the caller says
    public sealed partial class TermExpression
    {
        // ===========[ Helpers ]==============================================

        // The comparison is picked here as a constant rather than handed down as a StringComparison, so the JIT can fold away
        //  the switch inside String.Equals and IndexOf instead of running it on every compare
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSameTerm (string candidate, string term, bool matchCase)
        {
            return matchCase
                ? String.Equals(candidate, term)
                : String.Equals(candidate, term, StringComparison.OrdinalIgnoreCase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int IndexOfTerm (string text, string term, int startIndex, bool matchCase)
        {
            return matchCase
                ? text.IndexOf(term, startIndex, StringComparison.Ordinal)
                : text.IndexOf(term, startIndex, StringComparison.OrdinalIgnoreCase);
        }

        private static bool FindsTerm (string text, string term, bool matchCase, bool wholeWords)
        {
            if (text == null)
            {
                return false;
            }

            int matchIndex = IndexOfTerm(text, term, 0, matchCase);
            if (!wholeWords)
            {
                return matchIndex >= 0;
            }

            // Both comparisons on offer are ordinal, which match the term character for character, so a match is always
            //  exactly as long as the term. A match that touches a word character isn't a whole word, but a later one might be.
            while (matchIndex >= 0)
            {
                if (IsWholeWordAt(text, matchIndex, term.Length))
                {
                    return true;
                }

                matchIndex = IndexOfTerm(text, term, matchIndex + 1, matchCase);
            }

            return false;
        }

        private static bool IsWholeWordAt (string text, int start, int length)
        {
            // Decoding runes rather than reading chars, so a letter outside the basic plane (two chars in a .NET string) still
            //  counts as a letter. At either end of the text there's nothing to decode, which counts as a boundary.
            bool touchesBefore = (Rune.DecodeLastFromUtf16(text.AsSpan(0, start), out Rune before, out _) == OperationStatus.Done)
                && IsWordRune(before);
            bool touchesAfter = (Rune.DecodeFromUtf16(text.AsSpan(start + length), out Rune after, out _) == OperationStatus.Done)
                && IsWordRune(after);

            return !touchesBefore && !touchesAfter;
        }

        private static bool IsWordRune (Rune rune)
        {
            if (Rune.IsLetterOrDigit(rune))
            {
                return true;
            }

            // A combining mark is part of the letter in front of it (an 'e' then a combining acute is how some text writes 'é'),
            //  and connector punctuation is '_' and its relatives, which join words rather than split them
            UnicodeCategory category = Rune.GetUnicodeCategory(rune);
            return (category == UnicodeCategory.NonSpacingMark)
                || (category == UnicodeCategory.SpacingCombiningMark)
                || (category == UnicodeCategory.ConnectorPunctuation);
        }

        // ===========[ Subclasses/structs ]===================================

        /// <summary>
        /// Decides whether a term counts as present for one evaluation.
        /// </summary>
        /// <remarks>
        /// Evaluation is generic over the struct implementing this, rather than taking a delegate, so the JIT (and NativeAOT)
        /// compiles it once per kind of presence test with the test's call inlined, and nothing is allocated to evaluate.
        /// </remarks>
        private interface ITermPresence
        {
            bool IsPresent (string term);
        }

        /// <summary>
        /// A term is present when an array holds it.
        /// </summary>
        private readonly struct ArrayPresence : ITermPresence
        {
            private readonly string[] m_terms;
            private readonly bool m_matchCase;

            public ArrayPresence (string[] terms, TermMatchOptions options)
            {
                m_terms = terms;
                m_matchCase = options.MatchCase;
            }

            public bool IsPresent (string term)
            {
                foreach (string candidate in m_terms)
                {
                    if (IsSameTerm(candidate, term, m_matchCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// A term is present when a collection holds it.
        /// </summary>
        private readonly struct CollectionPresence : ITermPresence
        {
            private readonly IReadOnlyCollection<string> m_terms;
            private readonly IReadOnlyList<string> m_termsByIndex;
            private readonly HashSet<string> m_matchingSet;
            private readonly bool m_matchCase;

            public CollectionPresence (IReadOnlyCollection<string> terms, TermMatchOptions options)
            {
                m_terms = terms;
                m_termsByIndex = terms as IReadOnlyList<string>;
                m_matchCase = options.MatchCase;

                // A set that already compares the way the options ask answers in one lookup. Any other set (one that ignores
                //  case when the options match it, say) would answer by its own comparison instead, so it gets scanned.
                m_matchingSet = ((terms is HashSet<string> set) && ComparesLike(set.Comparer, options.MatchCase)) ? set : null;
            }

            public bool IsPresent (string term)
            {
                if (m_matchingSet != null)
                {
                    return m_matchingSet.Contains(term);
                }

                // Indexing a list doesn't allocate an enumerator, so it gets its own loop
                if (m_termsByIndex != null)
                {
                    for (int index = 0; index < m_termsByIndex.Count; ++index)
                    {
                        if (IsSameTerm(m_termsByIndex[index], term, m_matchCase))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                foreach (string candidate in m_terms)
                {
                    if (IsSameTerm(candidate, term, m_matchCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool ComparesLike (IEqualityComparer<string> comparer, bool matchCase)
            {
                // A HashSet<string> built without a comparer reports EqualityComparer<string>.Default, which is ordinal
                return matchCase
                    ? (comparer == EqualityComparer<string>.Default) || StringComparer.Ordinal.Equals(comparer)
                    : StringComparer.OrdinalIgnoreCase.Equals(comparer);
            }
        }

        /// <summary>
        /// A term is present when it's found in a text, or in any of several.
        /// </summary>
        private readonly struct TextPresence : ITermPresence
        {
            // One of these is set: a single text, an array of them (read directly, like ArrayPresence does), or a list
            private readonly string m_text;
            private readonly string[] m_textArray;
            private readonly IReadOnlyList<string> m_textList;
            private readonly bool m_matchCase;
            private readonly bool m_wholeWords;

            public TextPresence (string text, TermMatchOptions options)
            {
                m_text = text;
                m_textArray = null;
                m_textList = null;
                m_matchCase = options.MatchCase;
                m_wholeWords = options.WholeWords;
            }

            public TextPresence (IReadOnlyList<string> texts, TermMatchOptions options)
            {
                m_text = null;
                m_textArray = texts as string[];
                m_textList = m_textArray == null ? texts : null;
                m_matchCase = options.MatchCase;
                m_wholeWords = options.WholeWords;
            }

            public bool IsPresent (string term)
            {
                if (m_textArray != null)
                {
                    foreach (string text in m_textArray)
                    {
                        if (FindsTerm(text, term, m_matchCase, m_wholeWords))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                if (m_textList != null)
                {
                    for (int index = 0; index < m_textList.Count; ++index)
                    {
                        if (FindsTerm(m_textList[index], term, m_matchCase, m_wholeWords))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                return FindsTerm(m_text, term, m_matchCase, m_wholeWords);
            }
        }

        /// <summary>
        /// A term is present when the caller says so.
        /// </summary>
        private readonly struct CallerPresence : ITermPresence
        {
            private readonly Func<string, bool> m_isPresent;

            public CallerPresence (Func<string, bool> isPresent)
            {
                m_isPresent = isPresent;
            }

            public bool IsPresent (string term) => m_isPresent(term);
        }
    }
}
