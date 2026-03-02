namespace AJut.UX.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    public static class ColorHelpers
    {
        /// <summary>Supports #RGB (converts to #RRGGBB), #RRGGBB, and #AARRGGBB</summary>
        public static bool TryGetColorFromHex(string hex, out byte[] argb)
        {
            if (hex.StartsWith("#"))
            {
                if (hex.ToLower() == "#none")
                {
                    argb = [0, 255, 0, 255];
                    return true;
                }

                switch (hex.Length)
                {
                    // #RGB -> #RRGGBB
                    case 4:
                        {
                            argb = _ReadHexBytes(3, nibbleCount: 1);
                            return true;
                        }

                    // #ARGB -> #AARRGGBB
                    case 5:
                        {
                            argb = _ReadHexBytes(4, nibbleCount: 1);
                            return true;
                        }

                    // #RRGGBB
                    case 7:
                        {
                            argb = _ReadHexBytes(3, nibbleCount: 2);
                            return true;
                        }

                    // #AARRGGBB
                    case 9:
                        {
                            argb = _ReadHexBytes(4, nibbleCount: 2);
                            return true;
                        }
                }
            }

            argb = [0, 255, 0, 255];
            return false;

            byte[] _ReadHexBytes(int byteCount, int nibbleCount = 2)
            {
                byte[] bytes = new byte[4];
                int nextWriteIndex;
                switch (byteCount)
                {
                    case 4:
                        nextWriteIndex = 0;
                        break;

                    case 3:
                        bytes[0] = 255;
                        nextWriteIndex = 1;
                        break;

                    default:
                        throw new InvalidOperationException($"Error - can only request 4 or 3 bytes, not sure what to do with {byteCount} color bytes");
                }

                if (nibbleCount != 1 && nibbleCount != 2)
                {
                    throw new InvalidOperationException($"Expected chars per byte must be either 1 or 2, was {nibbleCount}");
                }

                for (int index = 0; index < byteCount; ++index)
                {
                    int start = (index * nibbleCount) + 1;

                    // For a string #AFA - we treat that as #AAFFAA
                    string digitChars = hex.Substring(start, nibbleCount);
                    if (nibbleCount == 1)
                    {
                        // If we have one char per byte, then we are guranteed to be working with a nibble
                        byte halfByte = (byte)Convert.ToUInt32(digitChars, 16);
                        bytes[nextWriteIndex++] = (byte)((byte)(halfByte << 4) | halfByte);
                    }
                    else
                    {
                        // Otherwise we're working with a full byte
                        bytes[nextWriteIndex++] = (byte)(Convert.ToUInt32(digitChars, 16));
                    }
                }

                return bytes;
            }
        }

        /// <summary>
        /// Parses a CSS gradient string into a platform-agnostic <see cref="GradientBuilder"/>.
        /// Supports linear-gradient and radial-gradient.
        /// Stop color formats: hex (#RGB / #RRGGBB / #AARRGGBB), rgb(r,g,b), rgba(r,g,b,a), transparent.
        /// </summary>
        public static bool TryGetGradientFromString(string gradientStr, out GradientBuilder gradientInfo)
        {
            gradientInfo = default;
            if (string.IsNullOrWhiteSpace(gradientStr))
            {
                return false;
            }

            gradientStr = gradientStr.Trim();

            bool isLinear = gradientStr.StartsWith("linear-gradient", StringComparison.OrdinalIgnoreCase);
            bool isRadial = !isLinear && gradientStr.StartsWith("radial-gradient", StringComparison.OrdinalIgnoreCase);
            if (!isLinear && !isRadial)
            {
                return false;
            }

            int openParen = gradientStr.IndexOf('(');
            if (openParen < 0 || gradientStr[gradientStr.Length - 1] != ')')
            {
                return false;
            }

            string inner = gradientStr.Substring(openParen + 1, gradientStr.Length - openParen - 2).Trim();
            var args = _SplitTopLevelCommas(inner);
            if (args.Count == 0)
            {
                return false;
            }

            return isLinear ? _TryParseLinear(args, out gradientInfo) : _TryParseRadial(args, out gradientInfo);

            // Split on top-level commas only - ignores commas inside rgb(...)/rgba(...)
            static List<string> _SplitTopLevelCommas(string s)
            {
                var parts = new List<string>();
                int depth = 0, start = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == '(')
                    {
                        ++depth; 
                    }
                    else if (c == ')')
                    {
                        --depth; 
                    }
                    else if (c == ',' && depth == 0)
                    {
                        parts.Add(s.Substring(start, i - start).Trim());
                        start = i + 1;
                    }
                }
                parts.Add(s.Substring(start).Trim());
                return parts;
            }

            static bool _TryParseLinear(List<string> args, out GradientBuilder gradient)
            {
                gradient = default;
                int stopStart = 0;
                float angleDeg = 180f; // CSS default: to bottom

                string first = args[0];
                if (_TryParseAngle(first, out float parsedAngle))
                {
                    angleDeg = parsedAngle;
                    stopStart = 1;
                }
                else if (first.StartsWith("to ", StringComparison.OrdinalIgnoreCase))
                {
                    angleDeg = _DirectionToAngle(first);
                    stopStart = 1;
                }

                var stops = _ParseColorStops(args, stopStart);
                if (stops == null)
                {
                    return false;
                }

                gradient = new GradientBuilder(stops, new LinearGradientParams(angleDeg));
                return true;
            }

            static bool _TryParseRadial(List<string> args, out GradientBuilder gradient)
            {
                gradient = default;
                int stopStart = 0;
                float cx = 0.5f, cy = 0.5f, rx = 0.5f, ry = 0.5f;

                // First arg is configuration if it doesn't look like a color stop
                if (args.Count > 0 && !_IsColorStart(args[0]))
                {
                    stopStart = 1;
                    string config = args[0];

                    // Split "circle 50% at 25% 75%" -> shapeSize="circle 50%", posStr="25% 75%"
                    int atIdx = _FindAtKeyword(config);
                    string shapeSize = atIdx >= 0 ? config.Substring(0, atIdx).Trim() : config;
                    string posStr = atIdx >= 0 ? config.Substring(atIdx + 4).Trim() : null;

                    if (posStr != null)
                    {
                        _ParsePosition(posStr, out cx, out cy);
                    }

                    if (!string.IsNullOrWhiteSpace(shapeSize))
                    {
                        string[] tokens = shapeSize.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        float parsedRx = -1f, parsedRy = -1f;
                        foreach (string tok in tokens)
                        {
                            // Shape keywords and CSS size keywords - size keywords default to 0.5 (see gap docs)
                            if (tok.Equals("circle", StringComparison.OrdinalIgnoreCase) ||
                                tok.Equals("ellipse", StringComparison.OrdinalIgnoreCase) ||
                                tok.Equals("closest-side", StringComparison.OrdinalIgnoreCase) ||
                                tok.Equals("farthest-side", StringComparison.OrdinalIgnoreCase) ||
                                tok.Equals("closest-corner", StringComparison.OrdinalIgnoreCase) ||
                                tok.Equals("farthest-corner", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (_TryParsePercentage(tok, out float v))
                            {
                                if (parsedRx < 0)
                                {
                                    parsedRx = v;
                                }
                                else
                                {
                                    parsedRy = v;
                                }
                            }
                        }

                        if (parsedRx >= 0)
                        {
                            rx = parsedRx;
                            ry = parsedRy >= 0 ? parsedRy : parsedRx; // circle: rx == ry
                        }
                    }
                }

                var stops = _ParseColorStops(args, stopStart);
                if (stops == null)
                {
                    return false;
                }

                gradient = new GradientBuilder(stops, new RadialGradientParams(cx, cy, rx, ry));
                return true;
            }

            // Find " at " as a word boundary within a radial config string
            static int _FindAtKeyword(string s)
            {
                for (int i = 0; i < s.Length - 3; i++)
                {
                    if (s[i] == ' ' &&
                        i + 3 < s.Length &&
                        string.Compare(s, i + 1, "at", 0, 2, StringComparison.OrdinalIgnoreCase) == 0 &&
                        s[i + 3] == ' ')
                    {
                        return i;
                    }
                }
                return -1;
            }

            static bool _TryParseAngle(string s, out float degrees)
            {
                degrees = 0f;

                if (s.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
                {
                    return float.TryParse(s.Substring(0, s.Length - 3), NumberStyles.Float, CultureInfo.InvariantCulture, out degrees);
                }

                if (s.EndsWith("turn", StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(s.Substring(0, s.Length - 4), NumberStyles.Float, CultureInfo.InvariantCulture, out float turns))
                {
                    degrees = turns * 360f;
                    return true;
                }

                if (s.EndsWith("rad", StringComparison.OrdinalIgnoreCase)
                    && float.TryParse(s.Substring(0, s.Length - 3), NumberStyles.Float, CultureInfo.InvariantCulture, out float radians))
                {
                    degrees = (float)(radians * 180.0 / Math.PI);
                    return true;
                }

                if (s.EndsWith("grad", StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(s.Substring(0, s.Length - 4), NumberStyles.Float, CultureInfo.InvariantCulture, out float grads))
                {
                    degrees = grads * 0.9f; // 400grad = 360deg
                    return true;
                }

                return false;
            }

            // Converts CSS "to <side-or-corner>" into an angle in degrees.
            // NOTE: diagonal angles (45/135/225/315 degrees) assume a square element - see gap docs.
            static float _DirectionToAngle(string direction)
            {
                bool top = direction.IndexOf("top", StringComparison.OrdinalIgnoreCase) >= 0;
                bool bottom = direction.IndexOf("bottom", StringComparison.OrdinalIgnoreCase) >= 0;
                bool left = direction.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0;
                bool right = direction.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0;

                if (top && right) { return 45f; }
                if (bottom && right) { return 135f; }
                if (bottom && left) { return 225f; }
                if (top && left) { return 315f; }
                if (top) { return 0f; }
                if (right) { return 90f; }
                if (bottom) { return 180f; }
                if (left) { return 270f; }
                return 180f;
            }

            // Parses a CSS background-position string ("center", "25% 75%", "top left", etc.)
            // into normalized [0,1] X/Y coordinates.
            static void _ParsePosition(string pos, out float x, out float y)
            {
                x = 0.5f; y = 0.5f;
                pos = pos.Trim();
                if (pos.Equals("center", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                string[] tokens = pos.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                float[] vals = new float[2];
                int filled = 0;
                foreach (string tok in tokens)
                {
                    if (filled >= 2) { break; }
                    if (tok.Equals("center", StringComparison.OrdinalIgnoreCase)) { vals[filled++] = 0.5f; }
                    else if (tok.Equals("left", StringComparison.OrdinalIgnoreCase) ||
                             tok.Equals("top", StringComparison.OrdinalIgnoreCase)) { vals[filled++] = 0f; }
                    else if (tok.Equals("right", StringComparison.OrdinalIgnoreCase) ||
                             tok.Equals("bottom", StringComparison.OrdinalIgnoreCase)) { vals[filled++] = 1f; }
                    else if (_TryParsePercentage(tok, out float v)) { vals[filled++] = v; }
                }

                if (filled >= 1) { x = vals[0]; }
                if (filled >= 2) { y = vals[1]; }
            }

            // Parses a CSS percentage string ("50%") into a normalized [0,1] float.
            // NOTE: absolute lengths (px, em, rem, etc.) require element size to normalize - not supported.
            static bool _TryParsePercentage(string s, out float normalized)
            {
                normalized = 0f;
                s = s.Trim();
                if (s.EndsWith("%") &&
                    float.TryParse(s.Substring(0, s.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
                {
                    normalized = pct / 100f;
                    return true;
                }
                return false;
            }

            // Heuristic: does this string look like the start of a color stop rather than gradient config?
            static bool _IsColorStart(string s)
            {
                return s.StartsWith("#") ||
                       s.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) ||
                       s.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) ||
                       s.StartsWith("transparent", StringComparison.OrdinalIgnoreCase);
            }

            static GradientStopBuilder[] _ParseColorStops(List<string> args, int startIdx)
            {
                if (startIdx >= args.Count)
                {
                    return null;
                }

                var raw = new List<(byte[] argb, float? offset)>(args.Count - startIdx);
                for (int i = startIdx; i < args.Count; i++)
                {
                    if (!_TryParseStop(args[i], out byte[] argb, out float? offset))
                    {
                        return null;
                    }
                    raw.Add((argb, offset));
                }

                if (raw.Count == 0)
                {
                    return null;
                }

                _DistributeOffsets(raw);

                var result = new GradientStopBuilder[raw.Count];
                for (int i = 0; i < raw.Count; i++)
                {
                    result[i] = new GradientStopBuilder(raw[i].offset!.Value, raw[i].argb);
                }
                return result;
            }

            static bool _TryParseStop(string stopStr, out byte[] argb, out float? offset)
            {
                argb = null;
                offset = null;

                if (stopStr.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
                {
                    int close = stopStr.IndexOf(')');
                    if (close < 0) { return false; }
                    if (!_TryParseRgba(stopStr.Substring(0, close + 1), out argb)) { return false; }
                    string rem = stopStr.Substring(close + 1).Trim();
                    if (_TryParsePercentage(rem, out float off)) { offset = off; }
                    return true;
                }

                if (stopStr.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
                {
                    int close = stopStr.IndexOf(')');
                    if (close < 0) { return false; }
                    if (!_TryParseRgb(stopStr.Substring(0, close + 1), out argb)) { return false; }
                    string rem = stopStr.Substring(close + 1).Trim();
                    if (_TryParsePercentage(rem, out float off)) { offset = off; }
                    return true;
                }

                if (stopStr.StartsWith("#"))
                {
                    int space = stopStr.IndexOf(' ');
                    string hex = space < 0 ? stopStr : stopStr.Substring(0, space);
                    if (!TryGetColorFromHex(hex, out argb)) { return false; }
                    if (space >= 0)
                    {
                        string rem = stopStr.Substring(space + 1).Trim();
                        if (_TryParsePercentage(rem, out float off)) { offset = off; }
                    }
                    return true;
                }

                // NOTE: only "transparent" is supported from the CSS named color set - see gap docs
                if (stopStr.StartsWith("transparent", StringComparison.OrdinalIgnoreCase) &&
                    (stopStr.Length == 11 || stopStr[11] == ' '))
                {
                    argb = [0, 0, 0, 0];
                    if (stopStr.Length > 11)
                    {
                        string rem = stopStr.Substring(12).Trim();
                        if (_TryParsePercentage(rem, out float off)) { offset = off; }
                    }
                    return true;
                }

                return false;
            }

            // Distributes missing offsets per CSS spec:
            // first defaults to 0, last to 1, interior gaps spread evenly between bracketing stops.
            static void _DistributeOffsets(List<(byte[] argb, float? offset)> stops)
            {
                if (!stops[0].offset.HasValue)
                {
                    stops[0] = (stops[0].argb, 0f);
                }
                if (!stops[stops.Count - 1].offset.HasValue)
                {
                    stops[stops.Count - 1] = (stops[stops.Count - 1].argb, 1f);
                }

                int i = 0;
                while (i < stops.Count)
                {
                    if (!stops[i].offset.HasValue)
                    {
                        int j = i + 1;
                        while (j < stops.Count && !stops[j].offset.HasValue) { ++j; }

                        float from = stops[i - 1].offset!.Value;
                        float to = stops[j].offset!.Value;
                        int span = j - i + 1;
                        for (int k = i; k < j; k++)
                        {
                            stops[k] = (stops[k].argb, from + (to - from) * (float)(k - (i - 1)) / span);
                        }

                        i = j;
                    }
                    else
                    {
                        i++;
                    }
                }
            }

            static bool _TryParseRgb(string s, out byte[] argb)
            {
                argb = null;
                int open = s.IndexOf('('), close = s.LastIndexOf(')');
                if (open < 0 || close < 0) { return false; }

                string[] parts = s.Substring(open + 1, close - open - 1).Split(',');
                if (parts.Length != 3) { return false; }
                if (!_TryParseChannel(parts[0], out byte r)) { return false; }
                if (!_TryParseChannel(parts[1], out byte g)) { return false; }
                if (!_TryParseChannel(parts[2], out byte b)) { return false; }
                argb = [255, r, g, b];
                return true;
            }

            static bool _TryParseRgba(string s, out byte[] argb)
            {
                argb = null;
                int open = s.IndexOf('('), close = s.LastIndexOf(')');
                if (open < 0 || close < 0) { return false; }

                string[] parts = s.Substring(open + 1, close - open - 1).Split(',');
                if (parts.Length != 4) { return false; }
                if (!_TryParseChannel(parts[0], out byte r)) { return false; }
                if (!_TryParseChannel(parts[1], out byte g)) { return false; }
                if (!_TryParseChannel(parts[2], out byte b)) { return false; }

                string aPart = parts[3].Trim();
                byte a;
                if (aPart.EndsWith("%") &&
                    float.TryParse(aPart.Substring(0, aPart.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float aPct))
                {
                    a = (byte)Math.Clamp((int)(aPct / 100f * 255f + 0.5f), 0, 255);
                }
                else if (float.TryParse(aPart, NumberStyles.Float, CultureInfo.InvariantCulture, out float aFloat))
                {
                    a = (byte)Math.Clamp((int)(aFloat * 255f + 0.5f), 0, 255);
                }
                else
                {
                    return false;
                }

                argb = [a, r, g, b];
                return true;
            }

            // Parses a single CSS color channel: integer 0-255 or percentage 0%-100%
            static bool _TryParseChannel(string s, out byte value)
            {
                value = 0;
                s = s.Trim();
                if (s.EndsWith("%") &&
                    float.TryParse(s.Substring(0, s.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
                {
                    value = (byte)Math.Clamp((int)(pct / 100f * 255f + 0.5f), 0, 255);
                    return true;
                }

                if (int.TryParse(s, out int iv))
                {
                    value = (byte)Math.Clamp(iv, 0, 255);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Color's normal ToString will give you hex, but if you have something like #CC000000 that can be stored as #C000 or #FF112233 can be stored as #123
        /// </summary>
        public static string GetSmallestHexString(byte A, byte R, byte G, byte B)
        {
            // We can skip A if it's max
            if (A == 255)
            {
                if (_IsDuplicateNibble(R) && _IsDuplicateNibble(G) && _IsDuplicateNibble(B))
                {
                    // Smallest #RGB
                    return $"#{R >> 4:X}{G >> 4:X}{B >> 4:X}";
                }

                return $"#{R:X2}{G:X2}{B:X2}";
            }

            if (_IsDuplicateNibble(A) && _IsDuplicateNibble(R) && _IsDuplicateNibble(G) && _IsDuplicateNibble(B))
            {
                // Smallest #ARGB
                return $"#{A >> 4:X}{R >> 4:X}{G >> 4:X}{B >> 4:X}";
            }

            return $"#{A:X2}{R:X2}{G:X2}{B:X2}";

            bool _IsDuplicateNibble(byte potentialHalfByte) => ((potentialHalfByte & 0b11110000) >> 4) == (potentialHalfByte & 0b00001111);
        }
    }


    #region ===[ CSS Gradient parser - WPF/WinUI3 cross-platform gap documentation ]===

    // The following CSS gradient features have no clean intersection across BOTH
    // WPF and WinUI3, and are therefore not modeled in GradientBuilder:
    //
    //   - conic-gradient / repeating-conic-gradient
    //       No equivalent in WPF or WinUI3.
    //
    //   - CSS diagonal direction keywords (to top right, etc.)
    //       CSS adapts the gradient angle to the element's aspect ratio so it hits
    //       the actual corner. GradientBuilder stores the geometric angle for a
    //       square element (45/135/225/315 degrees). Platform-specific consumers that
    //       care about exact corner alignment must recompute from element dimensions.
    //
    //   - Absolute-length stop positions (px, em, rem, vw, ...)
    //       Cannot normalize to [0,1] without knowing the rendered element size.
    //       Stops with absolute positions are not supported; only % is accepted.
    //
    //   - CSS radial size keywords (closest-side, farthest-corner, etc.)
    //       Same reason as above. When present, RadiusX/RadiusY default to 0.5.
    //
    //   - CSS #RRGGBBAA hex notation (alpha last)
    //       TryGetColorFromHex uses the #AARRGGBB byte order (ARGB convention).
    //       If a consumer passes a CSS-convention #RRGGBBAA string, the A and R
    //       bytes will be swapped. Keep this in mind when authoring gradient strings.
    //
    //   - Named CSS colors (e.g. red, cornflowerblue)
    //       Only the keyword "transparent" is recognized. (There are like 100-200 named
    //       colors - not doing that...)
    //
    //   - CSS4 space-separated rgb / rgba syntax  (e.g. rgb(255 0 0 / 0.5))
    //       Only the comma-separated CSS3 form is parsed.
    //
    //   - Color hints (bare midpoint position: "red, 30%, blue")
    //       No WPF/WinUI3 equivalent. Not parsed; use explicit gradient stops.
    //
    //   - Two-position color stops ("red 20% 60%")
    //       Must be expressed as two explicit stops instead.
    //
    //   - GradientOrigin / focal-point offset
    //       Both WPF and WinUI3 support a GradientOrigin that can be offset from
    //       Center, but CSS radial-gradient has no such concept. Consumers should
    //       default GradientOrigin to (CenterX, CenterY).
    //       Additional WinUI3 nuance: WPF GradientOrigin is relative to the bounding
    //       box center; WinUI3 GradientOrigin is relative to the top-left corner.
    //       Consumers must account for this when converting the same builder.
    //
    //   - SpreadMethod (Repeat / Reflect) on radial gradients
    //       WPF RadialGradientBrush inherits GradientBrush and supports SpreadMethod.
    //       WinUI3 RadialGradientBrush inherits XamlCompositionBrushBase (not
    //       GradientBrush), so Repeat/Reflect availability differs by platform and is
    //       not modeled here.


    public interface IGradientBuilderParams { }

    /// <summary>
    /// Angle for a CSS linear-gradient using CSS convention:
    /// 0 degrees = to-top, 90 = to-right, 180 = to-bottom (default when omitted), 270 = to-left.
    /// To derive WPF / WinUI3 normalized StartPoint and EndPoint (MappingMode=RelativeToBoundingBox):
    ///   double r = AngleDegrees * Math.PI / 180.0;
    ///   StartPoint = new Point(0.5 - Math.Sin(r) * 0.5,  0.5 + Math.Cos(r) * 0.5);
    ///   EndPoint   = new Point(0.5 + Math.Sin(r) * 0.5,  0.5 - Math.Cos(r) * 0.5);
    /// </summary>
    public record struct LinearGradientParams(float AngleDegrees) : IGradientBuilderParams;

    /// <summary>
    /// Radial gradient geometry, all values normalized to [0, 1] relative to the element
    /// bounding box - matching WPF / WinUI3 RadialGradientBrush (MappingMode=RelativeToBoundingBox).
    /// GradientOrigin is not modeled; consumers should default it to (CenterX, CenterY),
    /// adjusting for the WPF-vs-WinUI3 coordinate difference noted in the gap docs above.
    /// </summary>
    public record struct RadialGradientParams(float CenterX, float CenterY, float RadiusX, float RadiusY) : IGradientBuilderParams;

    /// <summary>A single gradient color stop. Offset is in [0, 1] (0 = start, 1 = end).</summary>
    public record struct GradientStopBuilder(float Offset, byte[] ARGB);

    /// <summary>
    /// Platform-agnostic gradient descriptor produced by <see cref="ColorHelpers.TryGetGradientFromString"/>.
    /// </summary>
    public record struct GradientBuilder
    {
        private IGradientBuilderParams m_params;

        private GradientBuilder(eBrushGradientType type, GradientStopBuilder[] stops)
        {
            this.Type = type;
            this.Stops = stops;
        }

        public GradientBuilder(GradientStopBuilder[] stops, LinearGradientParams gradientParams) : this(eBrushGradientType.Linear, stops)
        {
            m_params = gradientParams;
        }

        public GradientBuilder(GradientStopBuilder[] stops, RadialGradientParams gradientParams) : this(eBrushGradientType.Radial, stops)
        {
            m_params = gradientParams;
        }

        public eBrushGradientType Type { get; }
        public GradientStopBuilder[] Stops { get; }
        public LinearGradientParams LinearParams => (LinearGradientParams)m_params;
        public RadialGradientParams RadialParams => (RadialGradientParams)m_params;
    }

    public enum eBrushGradientType { Linear, Radial }

    #endregion
}
