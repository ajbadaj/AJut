// V2 reader (Pass 2 of the two-pass parse). Operates separator-to-separator over the
// SeparatorIndex output and the original char span. Errors-or-value contract: never
// throws on bad input; the returned Json carries any errors and whatever partial
// structure could be built before the failure.
namespace AJut.Text.AJson
{
    using System;

    internal static class JsonReader
    {
        private const int kBracketKindMask =
            (1 << (int)eSeparatorKind.OpenBrace) | (1 << (int)eSeparatorKind.OpenBracket);

        public static Json Parse (ReadOnlySpan<char> text, ParserRules rules)
        {
            if (text.Length == 0)
            {
                Json empty = new Json();
                empty.AddError("Empty input text");
                return empty;
            }

            rules = rules ?? new ParserRules();
            Json output = ParseWith(text, rules, oldTextQuoteRule: false);

            // Text written before strings were escaped can hold a value ending in a raw backslash,
            //  whose closing quote then reads as escaped and the parse fails. If it failed and a
            //  backslash sits in front of a quote, try once more reading quotes the way that text
            //  needs, and keep the result only if it parses clean. Escaped text always parses on the
            //  first pass, so this never runs for it, and strict mode never reads old text.
            if (output.HasErrors
                && !rules.StrictMode
                && text.IndexOf("\\\"".AsSpan()) != -1)
            {
                Json oldTextOutput = ParseWith(text, rules, oldTextQuoteRule: true);
                if (!oldTextOutput.HasErrors)
                {
                    return oldTextOutput;
                }
            }

            return output;
        }

        // ===============================[ Helper Methods ]===========================

        private static Json ParseWith (ReadOnlySpan<char> text, ParserRules rules, bool oldTextQuoteRule)
        {
            Json output = new Json();
            SeparatorIndex index = null;
            try
            {
                index = new SeparatorIndex(text, rules, oldTextQuoteRule);

                // Strict JSON has no comments, even when the rules configure comment indicators
                if (rules.StrictMode)
                {
                    for (int commentIndex = 0; commentIndex < index.CommentCount; ++commentIndex)
                    {
                        int commentStart = index.CommentStartAt(commentIndex);
                        output.AddError($"Strict mode violation - comment at position {commentStart}");
                    }
                }

                int firstOpen = index.NextOfKinds(0, kBracketKindMask);
                if (firstOpen == -1)
                {
                    // Bare-value case - no brackets at the top level.
                    output.Data = ReadUnquotedValue(text, index, 0, text.Length - 1);
                    return output;
                }

                if (text[firstOpen] == '{')
                {
                    output.Data = ReadDocument(text, index, output, rules, firstOpen, out _);
                }
                else
                {
                    output.Data = ReadArray(text, index, output, rules, firstOpen, out _);
                }

                return output;
            }
            catch (Exception exc)
            {
                output.AddError("Unexpected parse error: " + exc.Message);
                return output;
            }
            finally
            {
                index?.Dispose();
            }
        }

        private static JsonDocument ReadDocument (ReadOnlySpan<char> text, SeparatorIndex index, Json owner, ParserRules rules, int startIndex, out int endIndex)
        {
            JsonDocument doc = new JsonDocument();
            endIndex = -1;

            int searchPos = startIndex + 1;
            int lastStart = startIndex + 1;
            int insideQuoteStart = -1;
            string pendingKey = null;

            // The last comma, while no entry has started after it. Strict mode reports it as a
            //  trailing comma if the document closes first.
            int openCommaPos = -1;

            while (true)
            {
                if (!index.TryNext(searchPos, out int sepPos, out eSeparatorKind sepKind))
                {
                    break;
                }

                bool consumed = true;
                switch (sepKind)
                {
                    case eSeparatorKind.CloseBrace:
                        if (insideQuoteStart != -1)
                        {
                            consumed = false;
                            break;
                        }

                        if (pendingKey != null)
                        {
                            JsonValue tail = ReadUnquotedValue(text, index, lastStart, sepPos - 1);
                            if (tail != null)
                            {
                                if (rules.StrictMode && tail.IsQuoted == false && !LooksLikeJsonLiteralOrNumber(tail.StringValue))
                                {
                                    owner.AddError($"Strict mode violation - unquoted string value '{tail.StringValue}' at position {sepPos}");
                                }
                                doc.Add(pendingKey, tail);
                            }
                            pendingKey = null;
                        }
                        else
                        {
                            CheckStrictTextBeforeClose(
                                text, index, owner, rules,
                                lastStart, sepPos - 1, openCommaPos
                            );
                        }
                        endIndex = sepPos;
                        break;

                    case eSeparatorKind.OpenBrace:
                        {
                            if (insideQuoteStart != -1)
                            {
                                consumed = false;
                                break;
                            }

                            if (pendingKey == null)
                            {
                                owner.AddError($"Document parse error - nested document without preceding key at position {sepPos}");
                                endIndex = sepPos;
                                break;
                            }

                            CheckStrictStrayText(text, index, owner, rules, lastStart, sepPos - 1);
                            openCommaPos = -1;
                            JsonDocument child = ReadDocument(text, index, owner, rules, sepPos, out int childEnd);
                            if (childEnd == -1)
                            {
                                owner.AddError($"Unterminated nested document starting at position {sepPos}");
                                endIndex = sepPos;
                                break;
                            }

                            doc.Add(pendingKey, child);
                            pendingKey = null;
                            searchPos = childEnd + 1;
                            lastStart = childEnd + 1;
                            continue;
                        }

                    case eSeparatorKind.OpenBracket:
                        {
                            if (insideQuoteStart != -1)
                            {
                                consumed = false;
                                break;
                            }

                            if (pendingKey == null)
                            {
                                owner.AddError($"Array value without preceding key at position {sepPos}");
                                endIndex = sepPos;
                                break;
                            }

                            CheckStrictStrayText(text, index, owner, rules, lastStart, sepPos - 1);
                            openCommaPos = -1;
                            JsonArray childArr = ReadArray(text, index, owner, rules, sepPos, out int arrEnd);
                            if (arrEnd == -1)
                            {
                                owner.AddError($"Unterminated array starting at position {sepPos}");
                                endIndex = sepPos;
                                break;
                            }

                            doc.Add(pendingKey, childArr);
                            pendingKey = null;
                            searchPos = arrEnd + 1;
                            lastStart = arrEnd + 1;
                            continue;
                        }

                    case eSeparatorKind.Colon:
                        if (insideQuoteStart != -1)
                        {
                            consumed = false;
                            break;
                        }

                        // The chunk between lastStart and sepPos-1 is an unquoted key (lenient).
                        // Strict mode rejects it.
                        {
                            string keyChunk = TrimUnquoted(text, index, lastStart, sepPos - 1);
                            if (keyChunk.Length == 0)
                            {
                                owner.AddError($"Empty key at position {sepPos}");
                                consumed = false;
                                break;
                            }

                            if (rules.StrictMode)
                            {
                                owner.AddError($"Strict mode violation - unquoted key '{keyChunk}' at position {sepPos}");
                            }

                            pendingKey = keyChunk;
                            openCommaPos = -1;
                        }
                        break;

                    case eSeparatorKind.Quote:
                        if (insideQuoteStart == -1)
                        {
                            CheckStrictStrayText(text, index, owner, rules, lastStart, sepPos - 1);
                            openCommaPos = -1;
                            insideQuoteStart = sepPos + 1;
                        }
                        else
                        {
                            // Closing quote. Peek to decide if this quoted chunk is a key or a value.
                            int peekPos;
                            eSeparatorKind peekKind;
                            bool gotPeek = index.TryNext(sepPos + 1, out peekPos, out peekKind);

                            if (gotPeek && peekKind == eSeparatorKind.Colon)
                            {
                                CheckStrictStrayText(text, index, owner, rules, sepPos + 1, peekPos - 1);
                                pendingKey = ReadQuoted(text, insideQuoteStart, sepPos);
                                searchPos = peekPos + 1;
                                lastStart = peekPos + 1;
                                insideQuoteStart = -1;
                                continue;
                            }
                            else
                            {
                                if (pendingKey == null)
                                {
                                    owner.AddError($"Quoted value without preceding key at position {sepPos}");
                                    insideQuoteStart = -1;
                                    break;
                                }

                                string strValue = ReadQuoted(text, insideQuoteStart, sepPos);
                                doc.Add(pendingKey, new JsonValue(strValue, isQuoted: true));
                                pendingKey = null;
                                insideQuoteStart = -1;
                            }
                        }
                        break;

                    case eSeparatorKind.Comma:
                        if (insideQuoteStart != -1)
                        {
                            consumed = false;
                            break;
                        }

                        if (pendingKey != null)
                        {
                            JsonValue endValue = ReadUnquotedValue(text, index, lastStart, sepPos - 1);
                            if (endValue != null)
                            {
                                if (rules.StrictMode && endValue.IsQuoted == false && !LooksLikeJsonLiteralOrNumber(endValue.StringValue))
                                {
                                    owner.AddError($"Strict mode violation - unquoted string value '{endValue.StringValue}' at position {sepPos}");
                                }
                                doc.Add(pendingKey, endValue);
                            }
                            pendingKey = null;
                        }
                        else
                        {
                            CheckStrictStrayText(text, index, owner, rules, lastStart, sepPos - 1);
                        }

                        openCommaPos = sepPos;
                        break;

                    default:
                        consumed = false;
                        break;
                }

                if (endIndex != -1)
                {
                    break;
                }

                if (consumed)
                {
                    lastStart = sepPos + 1;
                }

                searchPos = sepPos + 1;
            }

            if (endIndex == -1)
            {
                owner.AddError($"Unterminated document starting at position {startIndex}");
            }

            return doc;
        }

        private static JsonArray ReadArray (ReadOnlySpan<char> text, SeparatorIndex index, Json owner, ParserRules rules, int startIndex, out int endIndex)
        {
            JsonArray arr = new JsonArray();
            endIndex = -1;

            int searchPos = startIndex + 1;
            int lastStart = startIndex + 1;
            int insideQuoteStart = -1;

            // The last comma, while no item has started after it. Strict mode reports it as a
            //  trailing comma if the array closes first.
            int openCommaPos = -1;

            while (true)
            {
                if (!index.TryNext(searchPos, out int sepPos, out eSeparatorKind sepKind))
                {
                    break;
                }

                bool consumed = true;
                switch (sepKind)
                {
                    case eSeparatorKind.CloseBracket:
                        if (insideQuoteStart != -1)
                        {
                            consumed = false;
                            break;
                        }

                        // Trailing unquoted item between last comma and the close bracket.
                        {
                            JsonValue tail = ReadUnquotedValue(text, index, lastStart, sepPos - 1);
                            if (tail != null)
                            {
                                if (rules.StrictMode && tail.IsQuoted == false && !LooksLikeJsonLiteralOrNumber(tail.StringValue))
                                {
                                    owner.AddError($"Strict mode violation - unquoted string array element at position {sepPos}");
                                }
                                arr.Add(tail);
                            }
                            else
                            {
                                CheckStrictTextBeforeClose(
                                    text, index, owner, rules,
                                    lastStart, sepPos - 1, openCommaPos
                                );
                            }
                        }
                        endIndex = sepPos;
                        break;

                    case eSeparatorKind.OpenBrace:
                        if (insideQuoteStart != -1)
                        {
                            consumed = false;
                            break;
                        }

                        {
                            CheckStrictStrayText(text, index, owner, rules, lastStart, sepPos - 1);
                            openCommaPos = -1;
                            JsonDocument child = ReadDocument(text, index, owner, rules, sepPos, out int childEnd);
                            if (childEnd == -1)
                            {
                                owner.AddError($"Unterminated nested document in array at position {sepPos}");
                                endIndex = sepPos;
                                break;
                            }

                            arr.Add(child);

                            // Skip past optional comma after nested doc.
                            if (index.TryNext(childEnd + 1, out int peekPos, out eSeparatorKind peekKind)
                                && peekKind == eSeparatorKind.Comma)
                            {
                                CheckStrictStrayText(text, index, owner, rules, childEnd + 1, peekPos - 1);
                                openCommaPos = peekPos;
                                searchPos = peekPos + 1;
                                lastStart = peekPos + 1;
                            }
                            else
                            {
                                searchPos = childEnd + 1;
                                lastStart = childEnd + 1;
                            }
                            continue;
                        }

                    case eSeparatorKind.OpenBracket:
                        if (insideQuoteStart != -1)
                        {
                            consumed = false;
                            break;
                        }

                        {
                            CheckStrictStrayText(text, index, owner, rules, lastStart, sepPos - 1);
                            openCommaPos = -1;
                            JsonArray child = ReadArray(text, index, owner, rules, sepPos, out int childEnd);
                            if (childEnd == -1)
                            {
                                owner.AddError($"Unterminated nested array in array at position {sepPos}");
                                endIndex = sepPos;
                                break;
                            }

                            arr.Add(child);

                            if (index.TryNext(childEnd + 1, out int peekPos, out eSeparatorKind peekKind)
                                && peekKind == eSeparatorKind.Comma)
                            {
                                CheckStrictStrayText(text, index, owner, rules, childEnd + 1, peekPos - 1);
                                openCommaPos = peekPos;
                                searchPos = peekPos + 1;
                                lastStart = peekPos + 1;
                            }
                            else
                            {
                                searchPos = childEnd + 1;
                                lastStart = childEnd + 1;
                            }
                            continue;
                        }

                    case eSeparatorKind.Comma:
                        if (insideQuoteStart != -1)
                        {
                            consumed = false;
                            break;
                        }

                        {
                            JsonValue itemValue = ReadUnquotedValue(text, index, lastStart, sepPos - 1);
                            if (itemValue != null)
                            {
                                if (rules.StrictMode && itemValue.IsQuoted == false && !LooksLikeJsonLiteralOrNumber(itemValue.StringValue))
                                {
                                    owner.AddError($"Strict mode violation - unquoted string array element at position {sepPos}");
                                }
                                arr.Add(itemValue);
                            }
                        }

                        openCommaPos = sepPos;
                        break;

                    case eSeparatorKind.Quote:
                        if (insideQuoteStart != -1)
                        {
                            string strValue = ReadQuoted(text, insideQuoteStart, sepPos);
                            arr.Add(new JsonValue(strValue, isQuoted: true));
                            insideQuoteStart = -1;

                            // Skip past optional comma.
                            if (index.TryNext(sepPos + 1, out int peekPos, out eSeparatorKind peekKind)
                                && peekKind == eSeparatorKind.Comma)
                            {
                                CheckStrictStrayText(text, index, owner, rules, sepPos + 1, peekPos - 1);
                                openCommaPos = peekPos;
                                searchPos = peekPos + 1;
                                lastStart = peekPos + 1;
                                continue;
                            }
                        }
                        else
                        {
                            CheckStrictStrayText(text, index, owner, rules, lastStart, sepPos - 1);
                            openCommaPos = -1;
                            insideQuoteStart = sepPos + 1;
                        }
                        break;

                    default:
                        consumed = false;
                        break;
                }

                if (endIndex != -1)
                {
                    break;
                }

                if (consumed)
                {
                    lastStart = sepPos + 1;
                }

                searchPos = sepPos + 1;
            }

            if (endIndex == -1)
            {
                owner.AddError($"Unterminated array starting at position {startIndex}");
            }

            return arr;
        }

        // The inside of a quoted key or value, from just after its opening quote up to (not
        //  including) its closing quote, unescaped: the tree holds strings as they are.
        private static string ReadQuoted (ReadOnlySpan<char> text, int startPos, int closingQuotePos)
            => JsonStringEscaping.UnescapeLenient(text.Slice(startPos, closingQuotePos - startPos));

        // Trim leading/trailing whitespace and produce a JsonValue, or null if the chunk is empty.
        private static JsonValue ReadUnquotedValue (ReadOnlySpan<char> text, SeparatorIndex index, int startPos, int endPos)
        {
            string raw = TrimUnquoted(text, index, startPos, endPos);
            return raw.Length == 0 ? null : new JsonValue(raw, isQuoted: false);
        }

        private static string TrimUnquoted (ReadOnlySpan<char> text, SeparatorIndex index, int startPos, int endPos)
        {
            if (endPos < startPos)
            {
                return String.Empty;
            }

            // The indexer leaves comments out of the separator stream, but an unquoted chunk is
            //  sliced from the original text, so a comment sitting inside it has to come out here.
            //  It is replaced with a space rather than removed: a comment separates the text on
            //  either side of it, as in C and JSON5. Text with no comments takes the plain slice.
            ReadOnlySpan<char> chunk = index.HasCommentWithin(startPos, endPos)
                ? index.SliceWithCommentsAsSpaces(text, startPos, endPos).AsSpan()
                : text.Slice(startPos, endPos - startPos + 1);

            int s = 0;
            int e = chunk.Length - 1;
            while (s <= e && IsWhitespace(chunk[s])) { ++s; }
            while (e >= s && IsWhitespace(chunk[e])) { --e; }

            if (e < s)
            {
                return String.Empty;
            }

            return chunk.Slice(s, e - s + 1).ToString();
        }

        // Strict JSON has only whitespace between its structural markers, keys and values. The
        //  lenient reader passes over text it has no use for (before a quote, around a nested
        //  document, between a value and its comma), which in strict mode would let a comment
        //  through when no comment indicators are configured, or any other stray text.
        private static void CheckStrictStrayText (ReadOnlySpan<char> text, SeparatorIndex index, Json owner, ParserRules rules, int startPos, int endPos)
        {
            if (!rules.StrictMode)
            {
                return;
            }

            string stray = TrimUnquoted(text, index, startPos, endPos);
            if (stray.Length != 0)
            {
                owner.AddError($"Strict mode violation - unexpected text '{stray}' at position {startPos}");
            }
        }

        // At a close brace or bracket with no value left to read: anything in front of it is stray,
        //  and if nothing is, a comma still waiting for its entry is a trailing comma.
        private static void CheckStrictTextBeforeClose (ReadOnlySpan<char> text, SeparatorIndex index, Json owner, ParserRules rules, int startPos, int endPos, int openCommaPos)
        {
            if (!rules.StrictMode)
            {
                return;
            }

            string stray = TrimUnquoted(text, index, startPos, endPos);
            if (stray.Length != 0)
            {
                owner.AddError($"Strict mode violation - unexpected text '{stray}' at position {startPos}");
            }
            else if (openCommaPos != -1)
            {
                owner.AddError($"Strict mode violation - trailing comma at position {openCommaPos}");
            }
        }

        private static bool IsWhitespace (char c)
        {
            return c == ' ' || c == '\t' || c == '\r' || c == '\n';
        }

        private static bool LooksLikeJsonLiteralOrNumber (string s)
        {
            if (String.IsNullOrEmpty(s))
            {
                return true;
            }

            if (s == "true" || s == "false" || s == "null")
            {
                return true;
            }

            char first = s[0];
            return first == '-' || first == '+' || (first >= '0' && first <= '9') || first == '.';
        }
    }
}
