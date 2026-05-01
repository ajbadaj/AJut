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
            Json output = new Json();
            if (text.Length == 0)
            {
                output.AddError("Empty input text");
                return output;
            }

            rules = rules ?? new ParserRules();
            SeparatorIndex index = null;
            try
            {
                index = new SeparatorIndex(text, rules);

                int firstOpen = index.NextOfKinds(0, kBracketKindMask);
                if (firstOpen == -1)
                {
                    // Bare-value case - no brackets at the top level.
                    output.Data = ReadUnquotedValue(text, 0, text.Length - 1);
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

        // ===============================[ Helper Methods ]===========================

        private static JsonDocument ReadDocument (ReadOnlySpan<char> text, SeparatorIndex index, Json owner, ParserRules rules, int startIndex, out int endIndex)
        {
            JsonDocument doc = new JsonDocument();
            endIndex = -1;

            int searchPos = startIndex + 1;
            int lastStart = startIndex + 1;
            int insideQuoteStart = -1;
            string pendingKey = null;

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
                            JsonValue tail = ReadUnquotedValue(text, lastStart, sepPos - 1);
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
                            string keyChunk = TrimUnquoted(text, lastStart, sepPos - 1);
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
                        }
                        break;

                    case eSeparatorKind.Quote:
                        if (insideQuoteStart == -1)
                        {
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
                                pendingKey = text.Slice(insideQuoteStart, sepPos - insideQuoteStart).ToString();
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

                                string strValue = text.Slice(insideQuoteStart, sepPos - insideQuoteStart).ToString();
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
                            JsonValue endValue = ReadUnquotedValue(text, lastStart, sepPos - 1);
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
                        if (lastStart != sepPos)
                        {
                            JsonValue tail = ReadUnquotedValue(text, lastStart, sepPos - 1);
                            if (tail != null)
                            {
                                if (rules.StrictMode && tail.IsQuoted == false && !LooksLikeJsonLiteralOrNumber(tail.StringValue))
                                {
                                    owner.AddError($"Strict mode violation - unquoted string array element at position {sepPos}");
                                }
                                arr.Add(tail);
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
                            JsonValue itemValue = ReadUnquotedValue(text, lastStart, sepPos - 1);
                            if (itemValue != null)
                            {
                                if (rules.StrictMode && itemValue.IsQuoted == false && !LooksLikeJsonLiteralOrNumber(itemValue.StringValue))
                                {
                                    owner.AddError($"Strict mode violation - unquoted string array element at position {sepPos}");
                                }
                                arr.Add(itemValue);
                            }
                        }
                        break;

                    case eSeparatorKind.Quote:
                        if (insideQuoteStart != -1)
                        {
                            string strValue = text.Slice(insideQuoteStart, sepPos - insideQuoteStart).ToString();
                            arr.Add(new JsonValue(strValue, isQuoted: true));
                            insideQuoteStart = -1;

                            // Skip past optional comma.
                            if (index.TryNext(sepPos + 1, out int peekPos, out eSeparatorKind peekKind)
                                && peekKind == eSeparatorKind.Comma)
                            {
                                searchPos = peekPos + 1;
                                lastStart = peekPos + 1;
                                continue;
                            }
                        }
                        else
                        {
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

        // Trim leading/trailing whitespace and produce a JsonValue, or null if the chunk is empty.
        private static JsonValue ReadUnquotedValue (ReadOnlySpan<char> text, int startPos, int endPos)
        {
            if (endPos < startPos)
            {
                return null;
            }

            int s = startPos;
            int e = endPos;
            while (s <= e && IsWhitespace(text[s])) { ++s; }
            while (e >= s && IsWhitespace(text[e])) { --e; }

            if (e < s)
            {
                return null;
            }

            string raw = text.Slice(s, e - s + 1).ToString();
            return new JsonValue(raw, isQuoted: false);
        }

        private static string TrimUnquoted (ReadOnlySpan<char> text, int startPos, int endPos)
        {
            int s = startPos;
            int e = endPos;
            while (s <= e && IsWhitespace(text[s])) { ++s; }
            while (e >= s && IsWhitespace(text[e])) { --e; }

            if (e < s)
            {
                return String.Empty;
            }

            return text.Slice(s, e - s + 1).ToString();
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
