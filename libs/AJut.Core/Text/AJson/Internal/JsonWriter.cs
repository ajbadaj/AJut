// V2 writer - tree to text. Produces a string by walking JsonValue/JsonDocument/JsonArray
// and emitting JSON. Pretty-print and compact-print are both controlled by JsonBuilderSettings.
namespace AJut.Text.AJson
{
    using System;
    using System.Text;

    internal static class JsonWriter
    {
        private static readonly JsonBuilderSettings g_defaultPretty = new JsonBuilderSettings();
        private static readonly JsonBuilderSettings g_defaultCompact = JsonBuilderSettings.BuildMinifiedSettings();

        public static string Write (JsonValue root, JsonBuilderSettings settings = null)
        {
            settings = settings ?? g_defaultPretty;
            StringBuilder sb = new StringBuilder(EstimateSize(root));
            WriteValue(sb, root, 0, settings);
            return sb.ToString();
        }

        public static string WriteCompact (JsonValue root)
        {
            return Write(root, g_defaultCompact);
        }

        // ===============================[ Helper Methods ]===========================
        private static int EstimateSize (JsonValue value)
        {
            if (value == null)
            {
                return 16;
            }
            if (value.IsValue)
            {
                return (value.StringValue?.Length ?? 0) + 4;
            }
            // Documents/arrays - cheap heuristic. The StringBuilder grows as needed anyway.
            return 256;
        }

        private static void WriteValue (StringBuilder sb, JsonValue value, int currentTabbing, JsonBuilderSettings settings)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            if (value.IsDocument)
            {
                WriteDocument(sb, (JsonDocument)value, currentTabbing, settings);
                return;
            }

            if (value.IsArray)
            {
                WriteArray(sb, (JsonArray)value, currentTabbing, settings);
                return;
            }

            WriteSimpleValue(sb, value, settings);
        }

        private static void WriteDocument (StringBuilder sb, JsonDocument doc, int currentTabbing, JsonBuilderSettings settings)
        {
            sb.Append('{');
            ++currentTabbing;

            int count = doc.Count;
            for (int i = 0; i < count; ++i)
            {
                MakeNewline(sb, currentTabbing, settings);

                string key = doc.KeyAt(i);
                JsonValue child = doc.ValueAt(i);

                WritePropertyHeading(sb, key, settings);
                WriteValue(sb, child, currentTabbing, settings);

                if (i < count - 1)
                {
                    sb.Append(',');
                }
            }

            --currentTabbing;
            if (count > 0)
            {
                MakeNewline(sb, currentTabbing, settings);
            }

            sb.Append('}');
        }

        private static void WriteArray (StringBuilder sb, JsonArray array, int currentTabbing, JsonBuilderSettings settings)
        {
            sb.Append('[');
            ++currentTabbing;

            int count = array.Count;
            for (int i = 0; i < count; ++i)
            {
                MakeNewline(sb, currentTabbing, settings);
                WriteValue(sb, array[i], currentTabbing, settings);

                if (i < count - 1)
                {
                    sb.Append(',');
                }
            }

            --currentTabbing;
            if (count > 0)
            {
                MakeNewline(sb, currentTabbing, settings);
            }

            sb.Append(']');
        }

        private static void WriteSimpleValue (StringBuilder sb, JsonValue value, JsonBuilderSettings settings)
        {
            string raw = value.StringValue;
            if (raw == null)
            {
                return;
            }

            bool quote = settings.PropertyValueQuoting == ePropertyValueQuoting.QuoteAll
                      || (settings.PropertyValueQuoting == ePropertyValueQuoting.QuoteAnyUsuallyQuotedItem && value.IsQuoted);

            if (quote)
            {
                sb.Append(settings.PropertyValueQuoteChars);
                sb.Append(raw);
                sb.Append(settings.PropertyValueQuoteChars);
            }
            else
            {
                sb.Append(raw);
            }
        }

        internal static void WritePropertyHeading (StringBuilder sb, string propName, JsonBuilderSettings settings)
        {
            if (settings.QuotePropertyNames)
            {
                sb.Append(settings.PropertyNameQuoteChars);
                sb.Append(propName);
                sb.Append(settings.PropertyNameQuoteChars);
            }
            else
            {
                sb.Append(propName);
            }

            sb.Append(settings.SpacingAroundPropertyIndicators);
            sb.Append(':');
            sb.Append(settings.SpacingAroundPropertyIndicators);
        }

        internal static void MakeNewline (StringBuilder sb, int currentTabbing, JsonBuilderSettings settings)
        {
            if (!String.IsNullOrEmpty(settings.Newline))
            {
                sb.Append(settings.Newline);
            }

            if (!String.IsNullOrEmpty(settings.Tabbing))
            {
                for (int t = 0; t < currentTabbing; ++t)
                {
                    sb.Append(settings.Tabbing);
                }
            }
        }
    }
}
