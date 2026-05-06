// V2 indexer (Pass 1 of the two-pass parse). Internal - the rented buffer never escapes
// to the consumer, the parser owns the lifetime in a try/finally inside JsonHelper.
namespace AJut.Text.AJson
{
    using System;
    using System.Buffers;
    using System.Runtime.CompilerServices;

    internal enum eSeparatorKind : byte
    {
        OpenBrace = 0,    // {
        CloseBrace = 1,   // }
        OpenBracket = 2,  // [
        CloseBracket = 3, // ]
        Colon = 4,        // :
        Comma = 5,        // ,
        Newline = 6,      // \n
        Quote = 7,        // "
        SingleQuote = 8,  // '
        UserDefined = 9,
    }

    internal readonly struct SeparatorRecord
    {
        public readonly int Position;
        public readonly eSeparatorKind Kind;

        public SeparatorRecord (int position, eSeparatorKind kind)
        {
            this.Position = position;
            this.Kind = kind;
        }

        public override string ToString () => $"[{this.Position}] {this.Kind}";
    }

    /// <summary>
    /// One-shot scan over the input span that records the position + kind of every structural
    /// marker the parser cares about. Comments are stripped at this layer (positions inside
    /// comments are not recorded). The output array is rented from <see cref="ArrayPool{T}"/>
    /// and must be returned via <see cref="Dispose"/>.
    /// </summary>
    internal sealed class SeparatorIndex : IDisposable
    {
        private const char kQuoteChar = '"';
        private SeparatorRecord[] m_buffer;
        private int m_count;

        // ===============================[ Construction ]===========================
        public SeparatorIndex (ReadOnlySpan<char> text, ParserRules rules = null)
        {
            rules = rules ?? new ParserRules();

            int estimatedSeparators = Math.Max(16, text.Length / 10);
            m_buffer = ArrayPool<SeparatorRecord>.Shared.Rent(estimatedSeparators);
            m_count = 0;

            this.ParseSeparators(text, rules);
        }

        // ===============================[ Properties ]===========================
        public int Count => m_count;

        // ===============================[ Public Interface Methods ]===========================
        /// <summary>
        /// Returns the position of the next separator at-or-after the given starting position,
        /// or -1 if none. The result position is strictly greater than the previously consumed
        /// position - callers seeking the *next after a known position* should pass start+1.
        /// </summary>
        public int NextAny (int startPos)
        {
            int idx = this.BinarySearchForStart(startPos);
            if (idx < 0)
            {
                return -1;
            }

            return m_buffer[idx].Position;
        }

        /// <summary>
        /// Returns position + kind of the next separator at-or-after start, or (-1, default) if none.
        /// </summary>
        public bool TryNext (int startPos, out int position, out eSeparatorKind kind)
        {
            int idx = this.BinarySearchForStart(startPos);
            if (idx < 0)
            {
                position = -1;
                kind = default;
                return false;
            }

            SeparatorRecord rec = m_buffer[idx];
            position = rec.Position;
            kind = rec.Kind;
            return true;
        }

        /// <summary>
        /// Returns the position of the next separator at-or-after start whose kind is in the
        /// given mask, or -1 if none. The mask is constructed by ORing together
        /// <c>(1 &lt;&lt; (int)eSeparatorKind.X)</c> bits.
        /// </summary>
        public int NextOfKinds (int startPos, int kindMask)
        {
            int idx = this.BinarySearchForStart(startPos);
            if (idx < 0)
            {
                return -1;
            }

            for (int i = idx; i < m_count; ++i)
            {
                SeparatorRecord rec = m_buffer[i];
                if ((kindMask & (1 << (int)rec.Kind)) != 0)
                {
                    return rec.Position;
                }
            }
            return -1;
        }

        public bool TryNextOfKinds (int startPos, int kindMask, out int position, out eSeparatorKind kind)
        {
            int idx = this.BinarySearchForStart(startPos);
            if (idx >= 0)
            {
                for (int i = idx; i < m_count; ++i)
                {
                    SeparatorRecord rec = m_buffer[i];
                    if ((kindMask & (1 << (int)rec.Kind)) != 0)
                    {
                        position = rec.Position;
                        kind = rec.Kind;
                        return true;
                    }
                }
            }

            position = -1;
            kind = default;
            return false;
        }

        public void Dispose ()
        {
            if (m_buffer != null)
            {
                ArrayPool<SeparatorRecord>.Shared.Return(m_buffer);
                m_buffer = null;
                m_count = 0;
            }
        }

        // ===============================[ Helper Methods ]===========================
        private void ParseSeparators (ReadOnlySpan<char> text, ParserRules rules)
        {
            // Build a small map of "is this user-defined separator?" to keep the inner loop cheap.
            // For default operation this is empty.
            ReadOnlySpan<char> userSeparators = rules.AdditionalSeparatorChars.Count > 0
                ? rules.AdditionalSeparatorChars.ToArray().AsSpan()
                : ReadOnlySpan<char>.Empty;

            bool insideQuote = false;
            string activeCommentEnd = null;

            for (int i = 0; i < text.Length; ++i)
            {
                if (activeCommentEnd != null)
                {
                    int remaining = text.Length - i;
                    if (remaining >= activeCommentEnd.Length)
                    {
                        ReadOnlySpan<char> slice = text.Slice(i, activeCommentEnd.Length);
                        if (slice.SequenceEqual(activeCommentEnd.AsSpan()))
                        {
                            i += activeCommentEnd.Length - 1;
                            activeCommentEnd = null;
                        }
                    }
                    continue;
                }

                char ch = text[i];

                if (insideQuote)
                {
                    if (ch == kQuoteChar && (i == 0 || text[i - 1] != '\\'))
                    {
                        insideQuote = false;
                        this.Mark(i, eSeparatorKind.Quote);
                    }
                    continue;
                }

                // Comment start probing - cheap path: only enter the loop if there's any
                // comment indicator at all. Most inputs in V2 will not have comment indicators
                // configured (lenient comment support is opt-in via ParserRules).
                if (rules.CommentIndicators.Count > 0)
                {
                    bool startedComment = false;
                    for (int idx = 0; idx < rules.CommentIndicators.Count; ++idx)
                    {
                        string commentStart = rules.CommentIndicators[idx].Item1;
                        int remaining = text.Length - i;
                        if (remaining < commentStart.Length)
                        {
                            continue;
                        }

                        if (text.Slice(i, commentStart.Length).SequenceEqual(commentStart.AsSpan()))
                        {
                            activeCommentEnd = rules.CommentIndicators[idx].Item2;
                            i += commentStart.Length - 1;
                            startedComment = true;
                            break;
                        }
                    }

                    if (startedComment)
                    {
                        continue;
                    }
                }

                eSeparatorKind kind;
                switch (ch)
                {
                    case '{': kind = eSeparatorKind.OpenBrace; break;
                    case '}': kind = eSeparatorKind.CloseBrace; break;
                    case '[': kind = eSeparatorKind.OpenBracket; break;
                    case ']': kind = eSeparatorKind.CloseBracket; break;
                    case ':': kind = eSeparatorKind.Colon; break;
                    case ',': kind = eSeparatorKind.Comma; break;
                    case '\n': kind = eSeparatorKind.Newline; break;
                    case '\'': kind = eSeparatorKind.SingleQuote; break;
                    case kQuoteChar:
                        insideQuote = true;
                        kind = eSeparatorKind.Quote;
                        break;
                    default:
                        if (!userSeparators.IsEmpty && userSeparators.Contains(ch))
                        {
                            kind = eSeparatorKind.UserDefined;
                            break;
                        }
                        continue;
                }

                this.Mark(i, kind);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Mark (int position, eSeparatorKind kind)
        {
            if (m_count == m_buffer.Length)
            {
                int newSize = m_buffer.Length * 2;
                SeparatorRecord[] grown = ArrayPool<SeparatorRecord>.Shared.Rent(newSize);
                Array.Copy(m_buffer, grown, m_count);
                ArrayPool<SeparatorRecord>.Shared.Return(m_buffer);
                m_buffer = grown;
            }

            m_buffer[m_count++] = new SeparatorRecord(position, kind);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int BinarySearchForStart (int targetPos)
        {
            // Find the smallest index where Position >= targetPos.
            int left = 0;
            int right = m_count - 1;
            while (left <= right)
            {
                int mid = left + ((right - left) >> 1);
                if (m_buffer[mid].Position < targetPos)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            return left < m_count ? left : -1;
        }
    }
}
