namespace AJut.UX.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;

    // ===========[ PathMarkup ]=================================================
    // Parses the XAML path mini-language (the string a Path's Data or a GeometryButton's
    // BackgroundGeometry takes, SVG path data plus an optional F0/F1 fill rule) into a
    // PathMarkupData that any toolkit can draw: absolute MoveTo, LineTo, CubicTo and Close only.
    //
    // Everything else is lowered here so that no toolkit has to:
    //  - H and V become LineTo.
    //  - C, S, Q and T all become CubicTo. S and T reflect the previous segment's last control
    //    point (only when the previous segment was the same family, otherwise the control point is
    //    the current point, per SVG). A quadratic goes to a cubic by degree elevation, which is
    //    exact: c1 = p0 + 2/3 (q - p0), c2 = p + 2/3 (q - p).
    //  - A (elliptical arc) becomes cubics. No toolkit draws SVG's endpoint arcs natively:
    //    NSBezierPath and UIBezierPath arcs are circular and center-based, Android's arcTo takes an
    //    axis-aligned oval, and none of them take an x-axis rotation. See AppendArc for how.
    //  - After Z, the current point returns to the subpath's start, and a drawing command that
    //    follows without its own M starts a new subpath there. That MoveTo is written out
    //    explicitly, so no toolkit's idea of the current point after close matters.
    //
    // The grammar follows WPF's parser, which real geometry strings lean on:
    //  - Numbers are culture-invariant, with an optional sign, decimals and an exponent, and need
    //    no separator when the next one starts with a sign or a second decimal point: "-1-2" is
    //    -1 and -2, ".5.5" is .5 and .5.
    //  - Commas and whitespace separate anything, and both are optional between a command letter
    //    and its numbers.
    //  - A command letter repeats implicitly while numbers keep coming. Extra pairs after M (or m)
    //    are line-tos (relative after m).
    //  - Arc flags are single 0 or 1 characters and need no separator: "a1 1 0 00 1 1".
    //  - F0 (even-odd, also the default) or F1 (nonzero) may only come first.
    //  - The first command has to be M or m.
    //
    // Reference: SVG 1.1 path data grammar and implementation notes,
    // https://www.w3.org/TR/SVG11/paths.html#PathDataBNF and
    // https://www.w3.org/TR/SVG11/implnote.html#ArcImplementationNotes
    public static class PathMarkup
    {
        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Parses path markup. On failure <paramref name="data"/> is null and <paramref name="error"/> says
        /// what was wrong and at which character offset. Null or whitespace-only markup parses to an
        /// empty path.
        /// </summary>
        public static bool TryParse (string? markup, [NotNullWhen(true)] out PathMarkupData? data, [NotNullWhen(false)] out string? error)
        {
            var parser = new Parser(markup ?? String.Empty);
            if (parser.Run(out error))
            {
                data = new PathMarkupData(parser.FillRule, parser.Commands);
                return true;
            }

            data = null;
            return false;
        }

        /// <summary>Parses path markup, throwing a <see cref="FormatException"/> that says what was wrong and where</summary>
        public static PathMarkupData Parse (string? markup)
        {
            if (!TryParse(markup, out PathMarkupData? data, out string? error))
            {
                throw new FormatException(error);
            }

            return data;
        }

        // ===========[ Subclasses/structs ]====================================
        private sealed class Parser
        {
            private readonly string m_text;
            private readonly List<PathCommand> m_commands = new List<PathCommand>();
            private int m_index;
            private string? m_error;

            private PathPoint m_current;
            private PathPoint m_subpathStart;
            private bool m_hasSubpath;
            private bool m_needsMoveAfterClose;

            // The reflection point for S and T: only valid right after a segment of the same family
            private PathPoint m_lastCubicControl;
            private PathPoint m_lastQuadraticControl;
            private char m_lastCommandFamily;

            public Parser (string text)
            {
                m_text = text;
            }

            public ePathFillRule FillRule { get; private set; } = ePathFillRule.EvenOdd;
            public List<PathCommand> Commands => m_commands;

            public bool Run ([NotNullWhen(false)] out string? error)
            {
                this.SkipSeparators();
                this.ReadFillRule();

                bool isFirstCommand = true;
                while (m_error == null)
                {
                    this.SkipSeparators();
                    if (m_index >= m_text.Length)
                    {
                        break;
                    }

                    char letter = m_text[m_index];
                    if (!IsCommandLetter(letter))
                    {
                        this.Fail($"expected a path command letter, found '{letter}'");
                        break;
                    }

                    if (isFirstCommand && letter != 'M' && letter != 'm')
                    {
                        this.Fail($"path markup has to start with M or m, found '{letter}'");
                        break;
                    }

                    isFirstCommand = false;
                    ++m_index;
                    this.ReadCommand(letter);
                }

                error = m_error;
                return m_error == null;
            }

            // ----- Reading -----
            private void ReadFillRule ()
            {
                if (m_index < m_text.Length && (m_text[m_index] == 'F' || m_text[m_index] == 'f'))
                {
                    int start = m_index;
                    ++m_index;
                    this.SkipSeparators();
                    if (m_index < m_text.Length && m_text[m_index] == '0')
                    {
                        this.FillRule = ePathFillRule.EvenOdd;
                    }
                    else if (m_index < m_text.Length && m_text[m_index] == '1')
                    {
                        this.FillRule = ePathFillRule.Nonzero;
                    }
                    else
                    {
                        this.Fail("a fill rule is F0 or F1", start);
                        return;
                    }

                    ++m_index;
                }
            }

            private void ReadCommand (char letter)
            {
                bool isRelative = Char.IsLower(letter);
                char command = Char.ToUpperInvariant(letter);

                // Z takes no numbers, and repeating it means nothing
                if (command == 'Z')
                {
                    this.ClosePath();
                    return;
                }

                // Every other command needs at least one full set of numbers, then repeats while
                // more numbers follow
                bool isFirstSet = true;
                do
                {
                    if (!this.ReadOneSet(command, isRelative, isFirstSet))
                    {
                        return;
                    }

                    isFirstSet = false;
                    this.SkipSeparators();
                }
                while (m_error == null && m_index < m_text.Length && IsNumberStart(m_text[m_index]));
            }

            private bool ReadOneSet (char command, bool isRelative, bool isFirstSet)
            {
                PathPoint origin = isRelative ? m_current : default;
                switch (command)
                {
                    case 'M':
                    {
                        if (!this.TryReadPoint(origin, out PathPoint point))
                        {
                            return false;
                        }

                        // Pairs after the first are implicit line-tos
                        if (isFirstSet)
                        {
                            this.MoveTo(point);
                        }
                        else
                        {
                            this.LineTo(point);
                        }

                        return true;
                    }

                    case 'L':
                    {
                        if (!this.TryReadPoint(origin, out PathPoint point))
                        {
                            return false;
                        }

                        this.LineTo(point);
                        return true;
                    }

                    case 'H':
                    {
                        if (!this.TryReadNumber(out double x))
                        {
                            return false;
                        }

                        this.LineTo(new PathPoint(isRelative ? m_current.X + x : x, m_current.Y));
                        return true;
                    }

                    case 'V':
                    {
                        if (!this.TryReadNumber(out double y))
                        {
                            return false;
                        }

                        this.LineTo(new PathPoint(m_current.X, isRelative ? m_current.Y + y : y));
                        return true;
                    }

                    case 'C':
                    {
                        if (!this.TryReadPoint(origin, out PathPoint control1)
                            || !this.TryReadPoint(origin, out PathPoint control2)
                            || !this.TryReadPoint(origin, out PathPoint end))
                        {
                            return false;
                        }

                        this.CubicTo(control1, control2, end);
                        return true;
                    }

                    case 'S':
                    {
                        if (!this.TryReadPoint(origin, out PathPoint control2)
                            || !this.TryReadPoint(origin, out PathPoint end))
                        {
                            return false;
                        }

                        PathPoint control1 = m_lastCommandFamily == 'C' ? Reflect(m_lastCubicControl, m_current) : m_current;
                        this.CubicTo(control1, control2, end);
                        return true;
                    }

                    case 'Q':
                    {
                        if (!this.TryReadPoint(origin, out PathPoint control)
                            || !this.TryReadPoint(origin, out PathPoint end))
                        {
                            return false;
                        }

                        this.QuadraticTo(control, end);
                        return true;
                    }

                    case 'T':
                    {
                        if (!this.TryReadPoint(origin, out PathPoint end))
                        {
                            return false;
                        }

                        PathPoint control = m_lastCommandFamily == 'Q' ? Reflect(m_lastQuadraticControl, m_current) : m_current;
                        this.QuadraticTo(control, end);
                        return true;
                    }

                    case 'A':
                    {
                        if (!this.TryReadNumber(out double radiusX)
                            || !this.TryReadNumber(out double radiusY)
                            || !this.TryReadNumber(out double rotationDegrees)
                            || !this.TryReadFlag(out bool isLargeArc)
                            || !this.TryReadFlag(out bool isSweepPositive)
                            || !this.TryReadPoint(origin, out PathPoint end))
                        {
                            return false;
                        }

                        this.ArcTo(radiusX, radiusY, rotationDegrees, isLargeArc, isSweepPositive, end);
                        return true;
                    }
                }

                this.Fail($"unknown path command '{command}'");
                return false;
            }

            private bool TryReadPoint (PathPoint origin, out PathPoint point)
            {
                if (this.TryReadNumber(out double x) && this.TryReadNumber(out double y))
                {
                    point = new PathPoint(origin.X + x, origin.Y + y);
                    return true;
                }

                point = default;
                return false;
            }

            private bool TryReadFlag (out bool flag)
            {
                this.SkipSeparators();
                if (m_index < m_text.Length && (m_text[m_index] == '0' || m_text[m_index] == '1'))
                {
                    flag = m_text[m_index] == '1';
                    ++m_index;
                    return true;
                }

                flag = false;
                this.Fail("an arc flag is 0 or 1");
                return false;
            }

            // Scans the longest number starting here: sign, digits, one decimal point, digits, then
            // an optional exponent. A second decimal point or a sign ends it, which is what makes
            // ".5.5" and "-1-2" two numbers each.
            private bool TryReadNumber (out double value)
            {
                this.SkipSeparators();
                int start = m_index;
                int index = m_index;

                if (index < m_text.Length && (m_text[index] == '+' || m_text[index] == '-'))
                {
                    ++index;
                }

                bool hasDigits = false;
                while (index < m_text.Length && Char.IsAsciiDigit(m_text[index]))
                {
                    ++index;
                    hasDigits = true;
                }

                if (index < m_text.Length && m_text[index] == '.')
                {
                    ++index;
                    while (index < m_text.Length && Char.IsAsciiDigit(m_text[index]))
                    {
                        ++index;
                        hasDigits = true;
                    }
                }

                if (!hasDigits)
                {
                    value = 0.0;
                    this.Fail(m_index < m_text.Length ? $"expected a number, found '{m_text[m_index]}'" : "expected a number, found the end of the markup");
                    return false;
                }

                // The exponent only counts if digits follow it, so "1e" stops before the e
                if (index < m_text.Length && (m_text[index] == 'e' || m_text[index] == 'E'))
                {
                    int exponentIndex = index + 1;
                    if (exponentIndex < m_text.Length && (m_text[exponentIndex] == '+' || m_text[exponentIndex] == '-'))
                    {
                        ++exponentIndex;
                    }

                    if (exponentIndex < m_text.Length && Char.IsAsciiDigit(m_text[exponentIndex]))
                    {
                        index = exponentIndex;
                        while (index < m_text.Length && Char.IsAsciiDigit(m_text[index]))
                        {
                            ++index;
                        }
                    }
                }

                if (!Double.TryParse(m_text.AsSpan(start, index - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    this.Fail("could not read the number here", start);
                    return false;
                }

                m_index = index;
                return true;
            }

            private void SkipSeparators ()
            {
                while (m_index < m_text.Length && (Char.IsWhiteSpace(m_text[m_index]) || m_text[m_index] == ','))
                {
                    ++m_index;
                }
            }

            private void Fail (string message) => this.Fail(message, m_index);

            private void Fail (string message, int offset)
            {
                if (m_error == null)
                {
                    m_error = $"Invalid path markup at character {offset}: {message}";
                }
            }

            // ----- Writing -----
            private void MoveTo (PathPoint point)
            {
                m_commands.Add(PathCommand.MoveTo(point));
                m_current = point;
                m_subpathStart = point;
                m_hasSubpath = true;
                m_needsMoveAfterClose = false;
                m_lastCommandFamily = 'M';
            }

            private void LineTo (PathPoint point)
            {
                this.EnsureSubpathAfterClose();
                m_commands.Add(PathCommand.LineTo(point));
                m_current = point;
                m_lastCommandFamily = 'L';
            }

            private void CubicTo (PathPoint control1, PathPoint control2, PathPoint end)
            {
                this.EnsureSubpathAfterClose();
                m_commands.Add(PathCommand.CubicTo(control1, control2, end));
                m_current = end;
                m_lastCubicControl = control2;
                m_lastCommandFamily = 'C';
            }

            private void QuadraticTo (PathPoint control, PathPoint end)
            {
                const double kTwoThirds = 2.0 / 3.0;

                this.EnsureSubpathAfterClose();
                PathPoint start = m_current;
                var control1 = new PathPoint(start.X + (kTwoThirds * (control.X - start.X)), start.Y + (kTwoThirds * (control.Y - start.Y)));
                var control2 = new PathPoint(end.X + (kTwoThirds * (control.X - end.X)), end.Y + (kTwoThirds * (control.Y - end.Y)));
                m_commands.Add(PathCommand.CubicTo(control1, control2, end));
                m_current = end;
                m_lastQuadraticControl = control;
                m_lastCommandFamily = 'Q';
            }

            private void ClosePath ()
            {
                if (!m_hasSubpath || m_needsMoveAfterClose)
                {
                    // Nothing open to close: a Z right after another Z draws nothing
                    return;
                }

                m_commands.Add(PathCommand.Close());
                m_current = m_subpathStart;
                m_needsMoveAfterClose = true;
                m_lastCommandFamily = 'Z';
            }

            private void EnsureSubpathAfterClose ()
            {
                if (m_needsMoveAfterClose)
                {
                    m_commands.Add(PathCommand.MoveTo(m_subpathStart));
                    m_needsMoveAfterClose = false;
                }
            }

            // An SVG arc is given by its end points, radii, x-axis rotation and two flags, but cubic
            // beziers need its center and angles. The conversion is the one from the SVG
            // implementation notes (F.6.5, "conversion from endpoint to center parameterization"):
            //  1. Rotate the half-chord into the ellipse's own frame (x1', y1').
            //  2. If the radii are too small to span the end points, scale them up uniformly until
            //     they just do (F.6.6), which is what every SVG renderer does rather than failing.
            //  3. Solve for the center in that frame, picking the root the two flags ask for, and
            //     rotate it back.
            //  4. Find the start angle and the sweep, then force the sweep's sign to the sweep flag.
            // The sweep is then split into pieces of a quarter turn or less, and each piece of the
            // unit circle becomes one cubic with control arms of length k = 4/3 * tan(delta / 4),
            // the standard approximation (worst radial error about 0.027% of the radius for a
            // quarter turn). Each piece is mapped back through the ellipse's scale, rotation and
            // center. The last piece ends exactly on the given end point rather than on the
            // computed one, so the arc never leaves a hairline gap before the next segment.
            //
            // Degenerate arcs follow the notes too: an arc to the current point draws nothing, and
            // a zero radius draws a straight line.
            private void ArcTo (double radiusX, double radiusY, double rotationDegrees, bool isLargeArc, bool isSweepPositive, PathPoint end)
            {
                const double kHalfPi = Math.PI / 2.0;
                const double kSplitTolerance = 1e-9;

                PathPoint start = m_current;
                if (start == end)
                {
                    m_lastCommandFamily = 'A';
                    return;
                }

                radiusX = Math.Abs(radiusX);
                radiusY = Math.Abs(radiusY);
                if (radiusX == 0.0 || radiusY == 0.0)
                {
                    this.LineTo(end);
                    m_lastCommandFamily = 'A';
                    return;
                }

                double phi = rotationDegrees * Math.PI / 180.0;
                double cosPhi = Math.Cos(phi);
                double sinPhi = Math.Sin(phi);

                // 1. Half-chord in the ellipse's frame
                double halfDx = (start.X - end.X) / 2.0;
                double halfDy = (start.Y - end.Y) / 2.0;
                double x1 = (cosPhi * halfDx) + (sinPhi * halfDy);
                double y1 = (-sinPhi * halfDx) + (cosPhi * halfDy);

                // 2. Grow the radii if they cannot reach
                double lambda = ((x1 * x1) / (radiusX * radiusX)) + ((y1 * y1) / (radiusY * radiusY));
                if (lambda > 1.0)
                {
                    double scale = Math.Sqrt(lambda);
                    radiusX *= scale;
                    radiusY *= scale;
                }

                // 3. Center
                double rx2 = radiusX * radiusX;
                double ry2 = radiusY * radiusY;
                double numerator = (rx2 * ry2) - (rx2 * y1 * y1) - (ry2 * x1 * x1);
                double denominator = (rx2 * y1 * y1) + (ry2 * x1 * x1);
                double coefficient = Math.Sqrt(Math.Max(0.0, numerator / denominator));
                if (isLargeArc == isSweepPositive)
                {
                    coefficient = -coefficient;
                }

                double centerXPrime = coefficient * (radiusX * y1 / radiusY);
                double centerYPrime = coefficient * -(radiusY * x1 / radiusX);
                double centerX = (cosPhi * centerXPrime) - (sinPhi * centerYPrime) + ((start.X + end.X) / 2.0);
                double centerY = (sinPhi * centerXPrime) + (cosPhi * centerYPrime) + ((start.Y + end.Y) / 2.0);

                // 4. Angles
                double startAngle = AngleBetween(1.0, 0.0, (x1 - centerXPrime) / radiusX, (y1 - centerYPrime) / radiusY);
                double sweep = AngleBetween(
                    (x1 - centerXPrime) / radiusX,
                    (y1 - centerYPrime) / radiusY,
                    (-x1 - centerXPrime) / radiusX,
                    (-y1 - centerYPrime) / radiusY
                );

                if (!isSweepPositive && sweep > 0.0)
                {
                    sweep -= 2.0 * Math.PI;
                }
                else if (isSweepPositive && sweep < 0.0)
                {
                    sweep += 2.0 * Math.PI;
                }

                // 5. Quarter turns or less, one cubic each
                int segmentCount = Math.Max(1, (int)Math.Ceiling((Math.Abs(sweep) / kHalfPi) - kSplitTolerance));
                double delta = sweep / segmentCount;
                double k = 4.0 / 3.0 * Math.Tan(delta / 4.0);

                this.EnsureSubpathAfterClose();
                double angle = startAngle;
                for (int segment = 0; segment < segmentCount; ++segment)
                {
                    double nextAngle = angle + delta;
                    double cosA = Math.Cos(angle);
                    double sinA = Math.Sin(angle);
                    double cosB = Math.Cos(nextAngle);
                    double sinB = Math.Sin(nextAngle);

                    PathPoint control1 = MapFromUnitCircle(cosA - (k * sinA), sinA + (k * cosA));
                    PathPoint control2 = MapFromUnitCircle(cosB + (k * sinB), sinB - (k * cosB));
                    PathPoint segmentEnd = segment == segmentCount - 1 ? end : MapFromUnitCircle(cosB, sinB);
                    m_commands.Add(PathCommand.CubicTo(control1, control2, segmentEnd));
                    angle = nextAngle;
                }

                m_current = end;
                m_lastCommandFamily = 'A';

                PathPoint MapFromUnitCircle (double unitX, double unitY)
                {
                    double scaledX = radiusX * unitX;
                    double scaledY = radiusY * unitY;
                    return new PathPoint(
                        centerX + (cosPhi * scaledX) - (sinPhi * scaledY),
                        centerY + (sinPhi * scaledX) + (cosPhi * scaledY)
                    );
                }
            }

            // ----- Helpers -----
            private static double AngleBetween (double ux, double uy, double vx, double vy)
            {
                return Math.Atan2((ux * vy) - (uy * vx), (ux * vx) + (uy * vy));
            }

            private static PathPoint Reflect (PathPoint control, PathPoint about)
            {
                return new PathPoint((2.0 * about.X) - control.X, (2.0 * about.Y) - control.Y);
            }

            private static bool IsCommandLetter (char c)
            {
                switch (Char.ToUpperInvariant(c))
                {
                    case 'M':
                    case 'L':
                    case 'H':
                    case 'V':
                    case 'C':
                    case 'S':
                    case 'Q':
                    case 'T':
                    case 'A':
                    case 'Z':
                        return true;

                    default:
                        return false;
                }
            }

            private static bool IsNumberStart (char c) => Char.IsAsciiDigit(c) || c == '-' || c == '+' || c == '.';
        }
    }
}
