namespace AJut.Text.AJson
{
    using System;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// JSON string escaping (RFC 8259 section 7) for the writer, and the matching unescape for the
    /// reader. The json tree holds strings as they are, unescaped: escaping only exists in text.
    /// </summary>
    internal static class JsonStringEscaping
    {
        private const int kUnicodeEscapeDigits = 4;

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
        /// Unescapes the inside of a json string. Text written before AJson escaped strings is read
        /// the way it was written, where it can be told apart (see the remarks in the body).
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
            //  escape JSON does not have: \d here, or a lone backslash at the end. A string with one
            //  is read as old text, undoing only the quote escape. Old text whose backslashes all
            //  happen to form real escapes (C:\temp, where \t is a tab) cannot be told apart, and
            //  reads as escaped.
            if (!HasOnlyValidEscapes(raw, firstBackslash))
            {
                return raw.ToString().Replace("\\\"", "\"");
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
                        output.Append((char)ushort.Parse(raw.Slice(index + 1, kUnicodeEscapeDigits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
                        index += kUnicodeEscapeDigits;
                        break;

                    // " \ and / stand for themselves
                    default: output.Append(escaped); break;
                }
            }

            return output.ToString();
        }

        // ===============================[ Helper Methods ]===========================
        private static int IndexOfFirstToEscape (string value, char quoteChar)
        {
            for (int index = 0; index < value.Length; ++index)
            {
                char c = value[index];
                if (c < ' ' || c == '"' || c == '\\' || c == quoteChar)
                {
                    return index;
                }
            }

            return -1;
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
