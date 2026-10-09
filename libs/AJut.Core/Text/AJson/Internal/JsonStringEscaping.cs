namespace AJut.Text.AJson
{
    using System;
    using System.Buffers;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// JSON string escaping (RFC 8259 section 7) for the writer, and the matching unescape for the
    /// reader. The json tree holds strings as they are, unescaped: escaping only exists in text.
    /// </summary>
    internal static class JsonStringEscaping
    {
        private const int kUnicodeEscapeDigits = 4;
        private const int kFirstPrintableChar = ' ';

        // Every json string escapes these: the quote, the backslash and every control character
        private static readonly SearchValues<char> kAlwaysEscaped = SearchValues.Create(BuildAlwaysEscaped());

        // ===============================[ Public Interface Methods ]===========================
        /// <summary>
        /// Appends <paramref name="value"/> escaped for the inside of a json string: quote,
        /// backslash and every control character. <paramref name="quoteChar"/> is escaped too when
        /// the settings quote with something other than a double quote.
        /// </summary>
        public static void AppendEscaped (StringBuilder output, string value, char quoteChar)
        {
            int firstToEscape = IndexOfFirstToEscape(value, quoteChar);
            if (firstToEscape == -1)
            {
                output.Append(value);
                return;
            }

            output.Append(value, 0, firstToEscape);
            for (int index = firstToEscape; index < value.Length; ++index)
            {
                char c = value[index];
                switch (c)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\b': output.Append("\\b"); break;
                    case '\f': output.Append("\\f"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            output.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else if (c == quoteChar)
                        {
                            output.Append('\\').Append(c);
                        }
                        else
                        {
                            output.Append(c);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Unescapes the inside of a json string. A string holding an escape JSON does not have (a
        /// backslash before anything but <c>" \ / b f n r t u</c>, or a lone trailing backslash)
        /// was written before AJson escaped strings, and is read as written, undoing only the
        /// quote escape.
        /// </summary>
        public static string UnescapeLenient (ReadOnlySpan<char> raw)
        {
            int firstBackslash = raw.IndexOf('\\');
            if (firstBackslash == -1)
            {
                return raw.ToString();
            }

            // Before strings were escaped, the writer escaped only quotes (as \") and put every
            //  backslash out raw, so C:\dir\file was written as is. That text is told apart by an
            //  escape JSON does not have: \d here, or a lone backslash at the end. The reader looks
            //  for one across the whole text first and reads every string as old text if it finds
            //  one, so this check only decides for a string whose text gave no sign either way.
            if (!HasOnlyValidEscapes(raw, firstBackslash))
            {
                return ReadAsOldText(raw);
            }

            StringBuilder output = new StringBuilder(raw.Length);
            output.Append(raw.Slice(0, firstBackslash));
            for (int index = firstBackslash; index < raw.Length; ++index)
            {
                char c = raw[index];
                if (c != '\\')
                {
                    output.Append(c);
                    continue;
                }

                char escaped = raw[++index];
                switch (escaped)
                {
                    case 'b': output.Append('\b'); break;
                    case 'f': output.Append('\f'); break;
                    case 'n': output.Append('\n'); break;
                    case 'r': output.Append('\r'); break;
                    case 't': output.Append('\t'); break;
                    case 'u':
                        ushort codeUnit = ushort.Parse(
                            raw.Slice(index + 1, kUnicodeEscapeDigits),
                            NumberStyles.AllowHexSpecifier,
                            CultureInfo.InvariantCulture
                        );
                        output.Append((char)codeUnit);
                        index += kUnicodeEscapeDigits;
                        break;

                    // " \ and / stand for themselves
                    default: output.Append(escaped); break;
                }
            }

            return output.ToString();
        }

        /// <summary>
        /// Whether <paramref name="raw"/>, the inside of a json string, holds an escape JSON does
        /// not have. Only text written before AJson escaped strings can.
        /// </summary>
        public static bool HasEscapeJsonLacks (ReadOnlySpan<char> raw)
        {
            int firstBackslash = raw.IndexOf('\\');
            return firstBackslash != -1 && !HasOnlyValidEscapes(raw, firstBackslash);
        }

        /// <summary>
        /// Reads the inside of a json string written before AJson escaped strings: as written,
        /// undoing only the quote escape (<c>\"</c>) that writer used.
        /// </summary>
        public static string ReadAsOldText (ReadOnlySpan<char> raw) => raw.ToString().Replace("\\\"", "\"");

        // ===============================[ Helper Methods ]===========================
        // Runs on every string value and property name the writer puts out, and most of them need
        //  no escaping at all, so the search is a vectorized SearchValues scan rather than a loop
        //  over each char. A quote char other than the default needs its own search, but only up to
        //  the first char that already has to be escaped.
        private static int IndexOfFirstToEscape (string value, char quoteChar)
        {
            ReadOnlySpan<char> text = value.AsSpan();
            int first = text.IndexOfAny(kAlwaysEscaped);
            if (quoteChar == '"')
            {
                return first;
            }

            int searchLength = first == -1 ? text.Length : first;
            int quoteIndex = text.Slice(0, searchLength).IndexOf(quoteChar);
            return quoteIndex == -1 ? first : quoteIndex;
        }

        private static string BuildAlwaysEscaped ()
        {
            char[] chars = new char[kFirstPrintableChar + 2];
            for (int index = 0; index < kFirstPrintableChar; ++index)
            {
                chars[index] = (char)index;
            }

            chars[kFirstPrintableChar] = '"';
            chars[kFirstPrintableChar + 1] = '\\';
            return new string(chars);
        }

        private static bool HasOnlyValidEscapes (ReadOnlySpan<char> raw, int firstBackslash)
        {
            for (int index = firstBackslash; index < raw.Length; ++index)
            {
                if (raw[index] != '\\')
                {
                    continue;
                }

                if (index + 1 >= raw.Length)
                {
                    return false;
                }

                switch (raw[index + 1])
                {
                    case '"':
                    case '\\':
                    case '/':
                    case 'b':
                    case 'f':
                    case 'n':
                    case 'r':
                    case 't':
                        ++index;
                        break;

                    case 'u':
                        if (index + 1 + kUnicodeEscapeDigits >= raw.Length
                            || !IsHexDigits(raw.Slice(index + 2, kUnicodeEscapeDigits)))
                        {
                            return false;
                        }

                        index += 1 + kUnicodeEscapeDigits;
                        break;

                    default:
                        return false;
                }
            }

            return true;
        }

        private static bool IsHexDigits (ReadOnlySpan<char> digits)
        {
            foreach (char c in digits)
            {
                if (!Uri.IsHexDigit(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
