namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;
    using AJut.UX.Helpers;
    using static System.FormattableString;

    // ===========[ SvgParsing ]=================================================
    // Reads an svg document into an SvgDocument any toolkit can draw. This is the toolkit-free half
    // of WPF's SvgSerialization, which builds its SvgSource from what this reads, so every stack
    // reads a file the same way.
    //
    // What it reads:
    //  - svg, g and path elements, and the shapes rect, circle, ellipse, line, polyline and polygon,
    //    each drawn as a path of its outline. Any other element directly under the svg is kept whole
    //    in the metadata; a group's attributes are kept in the metadata by its id.
    //  - fill, stroke, stroke-width and opacity, as attributes or in style, with style winning where
    //    both give one, as CSS has it. A g's paint reaches what is inside it, under anything nearer,
    //    and its opacity multiplies into it. Something that names no fill anywhere is filled black,
    //    as SVG has it, and none is no paint.
    //  - transform on a g, a path or a shape: translate, scale, rotate (about a point too), skew,
    //    skewX, skewY and matrix, and the CSS forms translateX/Y and scaleX/Y. A list applies its
    //    last entry first, and a group's transform reaches everything inside it.
    //  - The svg's viewBox and preserveAspectRatio, which place the drawing in its width and height.
    //  - Numbers culture-invariant, with lengths in px, mm or cm, or no unit for px.
    //
    // What it does not read: text, images, use and symbol, gradients and patterns referenced by
    // url(...), clip paths, masks and filters, and CSS in a style element.
    public static class SvgParsing
    {
        /// <summary>CSS's pixels per inch, and the dpi a length in mm or cm is read at unless told otherwise</summary>
        public const double kCssPixelsPerInch = 96.0;

        private const double kMillimetersPerInch = 25.4;
        private static readonly Regex kTransformCapture = new Regex(@"([A-Za-z]+)\s*\(([^)]*)\)");
        private static readonly char[] kNumberListSeparators = { ',', ' ', '\t', '\r', '\n' };
        private static readonly Regex kRgbCapture = new Regex(@"rgba?\(([\w, #.]+)\)");
        private static readonly string[] kPaintAttributes = { "fill", "stroke", "stroke-width", "opacity" };
        private static readonly string[] kShapeNames = { "rect", "circle", "ellipse", "line", "polyline", "polygon" };
        private static readonly SvgPaint kDefaultFill = SvgPaint.FromColor(new ColorArgb(255, 0, 0, 0));

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Reads an svg document. Lengths in mm or cm are turned into pixels at <paramref name="dpiX"/> and
        /// <paramref name="dpiY"/>. Null when the stream is not xml (which is logged) or its root is not an svg element.
        /// </summary>
        public static SvgDocument? Parse (Stream stream, double dpiX = kCssPixelsPerInch, double dpiY = kCssPixelsPerInch)
        {
            var document = new XmlDocument();
            try
            {
                document.Load(stream);
            }
            catch (Exception exc)
            {
                Logger.LogError("Failed to load svg from stream", exc);
                return null;
            }

            XmlElement? svg = document.DocumentElement;
            if ((svg == null) || (svg.Name != "svg"))
            {
                return null;
            }

            var reader = new DocumentReader(svg, dpiX, dpiY);
            return reader.Read();
        }

        /// <summary>Reads an svg document from its text; see <see cref="Parse(Stream, double, double)"/></summary>
        public static SvgDocument? ParseText (string svgText, double dpiX = kCssPixelsPerInch, double dpiY = kCssPixelsPerInch)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svgText));
            return Parse(stream, dpiX, dpiY);
        }

        // ===========[ Helper Methods ]========================================
        // A length: px, mm or cm, or no unit for px. Numbers in svg are written with a period whatever culture the app
        //  runs in.
        private static bool TryReadLength (string text, double dpi, out double pixels)
        {
            text = text.Trim();
            double multiplier = 1.0;
            if (text.EndsWith("mm", StringComparison.Ordinal))
            {
                multiplier = dpi / kMillimetersPerInch;
                text = text.Substring(0, text.Length - 2);
            }
            else if (text.EndsWith("cm", StringComparison.Ordinal))
            {
                multiplier = dpi / (kMillimetersPerInch / 10.0);
                text = text.Substring(0, text.Length - 2);
            }
            else if (text.EndsWith("px", StringComparison.Ordinal))
            {
                text = text.Substring(0, text.Length - 2);
            }

            if (TryReadNumber(text, out double units))
            {
                pixels = units * multiplier;
                return true;
            }

            pixels = double.NaN;
            return false;
        }

        private static bool TryReadNumber (string text, out double value) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        // A list of numbers separated by commas, spaces or both, the way points, viewBox and transform lists write them
        private static bool TryReadNumbers (string text, out double[] numbers)
        {
            string[] parts = text.Split(kNumberListSeparators, StringSplitOptions.RemoveEmptyEntries);
            numbers = new double[parts.Length];
            for (int index = 0; index < parts.Length; ++index)
            {
                if (!TryReadNumber(parts[index], out numbers[index]))
                {
                    return false;
                }
            }

            return true;
        }
        private static bool TryReadDegrees (string text, out double degrees)
        {
            text = text.Trim();
            return TryReadNumber(text.EndsWith("deg", StringComparison.Ordinal) ? text.Substring(0, text.Length - 3) : text, out degrees);
        }

        // A fill or stroke: none is no paint, then a gradient where one is allowed, then a single color. What does not
        //  read is no paint.
        private static SvgPaint? ReadPaint (string text, bool allowGradient)
        {
            if (text.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (allowGradient && AJutColorHelper.TryGetGradientFromString(text, out GradientBuilder gradient))
            {
                return SvgPaint.FromGradient(gradient, text);
            }

            return TryReadColor(text, out ColorArgb color) ? SvgPaint.FromColor(color, text) : null;
        }

        // rgb(r, g, b) and rgba(r, g, b, a) with a from 0 to 1, then hex with or without its #, then a color name. A
        //  gradient's text read as a color is its first stop's color.
        private static bool TryReadColor (string text, out ColorArgb color)
        {
            Match rgb = kRgbCapture.Match(text);
            if (rgb.Success)
            {
                string[] numbers = rgb.Groups[1].Value.Trim().Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                double alpha = 1.0;
                if ((numbers.Length >= 3)
                        && byte.TryParse(numbers[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte r)
                        && byte.TryParse(numbers[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte g)
                        && byte.TryParse(numbers[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b)
                        && ((numbers.Length == 3) || TryReadNumber(numbers[3], out alpha)))
                {
                    color = new ColorArgb((byte)(Math.Clamp(alpha, 0.0, 1.0) * 255), r, g, b);
                    return true;
                }

                color = default;
                return false;
            }

            string trimmed = text.Trim();
            if (AJutColorHelper.TryGetColorFromHex(trimmed.StartsWith('#') ? trimmed : "#" + trimmed, out byte[] argb)
                || AJutColorHelper.TryGetNamedColor(trimmed, out argb))
            {
                color = new ColorArgb(argb[0], argb[1], argb[2], argb[3]);
                return true;
            }

            color = default;
            return false;
        }

        // Each entry is a name and its numbers, separated by commas, spaces or both. SVG applies a list's last entry
        //  first, so each entry read goes before the ones already read.
        private static SvgMatrix ReadTransformList (string text, double dpiX, double dpiY)
        {
            SvgMatrix result = SvgMatrix.Identity;
            foreach (Match entry in kTransformCapture.Matches(text))
            {
                string[] numbers = entry.Groups[2].Value.ToLowerInvariant().Split(kNumberListSeparators, StringSplitOptions.RemoveEmptyEntries);
                if ((numbers.Length > 0) && TryReadTransform(entry.Groups[1].Value.ToLowerInvariant(), numbers, dpiX, dpiY, out SvgMatrix transform))
                {
                    result = transform.Then(result);
                }
            }

            return result;
        }

        private static bool TryReadTransform (string name, string[] numbers, double dpiX, double dpiY, out SvgMatrix transform)
        {
            transform = SvgMatrix.Identity;
            switch (name)
            {
                // A translate with one number moves along x only
                case "translate":
                    if (TryReadLength(numbers[0], dpiX, out double translateX))
                    {
                        double translateY = 0.0;
                        if ((numbers.Length == 1) || TryReadLength(numbers[1], dpiY, out translateY))
                        {
                            transform = SvgMatrix.Translate(translateX, translateY);
                            return true;
                        }
                    }
                    return false;

                case "translatex":
                    if (TryReadLength(numbers[0], dpiX, out double onlyX))
                    {
                        transform = SvgMatrix.Translate(onlyX, 0.0);
                        return true;
                    }
                    return false;

                case "translatey":
                    if (TryReadLength(numbers[0], dpiY, out double onlyY))
                    {
                        transform = SvgMatrix.Translate(0.0, onlyY);
                        return true;
                    }
                    return false;

                // A scale with one number scales both axes by it
                case "scale":
                    if (TryReadNumber(numbers[0], out double scaleX))
                    {
                        double scaleY = scaleX;
                        if ((numbers.Length == 1) || TryReadNumber(numbers[1], out scaleY))
                        {
                            transform = SvgMatrix.Scale(scaleX, scaleY);
                            return true;
                        }
                    }
                    return false;

                case "scalex":
                    if (TryReadNumber(numbers[0], out double onlyScaleX))
                    {
                        transform = SvgMatrix.Scale(onlyScaleX, 1.0);
                        return true;
                    }
                    return false;

                case "scaley":
                    if (TryReadNumber(numbers[0], out double onlyScaleY))
                    {
                        transform = SvgMatrix.Scale(1.0, onlyScaleY);
                        return true;
                    }
                    return false;

                // An angle, and optionally the point to turn about
                case "rotate":
                    if (TryReadDegrees(numbers[0], out double angle))
                    {
                        transform = ((numbers.Length >= 3) && TryReadNumber(numbers[1], out double centerX) && TryReadNumber(numbers[2], out double centerY))
                            ? SvgMatrix.Rotate(angle, centerX, centerY)
                            : SvgMatrix.Rotate(angle);
                        return true;
                    }
                    return false;

                case "skew":
                    if (TryReadDegrees(numbers[0], out double skewX))
                    {
                        double skewY = 0.0;
                        if ((numbers.Length == 1) || TryReadDegrees(numbers[1], out skewY))
                        {
                            transform = SvgMatrix.Skew(skewX, skewY);
                            return true;
                        }
                    }
                    return false;

                case "skewx":
                    if (TryReadDegrees(numbers[0], out double onlySkewX))
                    {
                        transform = SvgMatrix.Skew(onlySkewX, 0.0);
                        return true;
                    }
                    return false;

                case "skewy":
                    if (TryReadDegrees(numbers[0], out double onlySkewY))
                    {
                        transform = SvgMatrix.Skew(0.0, onlySkewY);
                        return true;
                    }
                    return false;

                // matrix(a b c d e f) is SvgMatrix's fields in order
                case "matrix":
                    if (numbers.Length != 6)
                    {
                        return false;
                    }

                    double[] values = new double[6];
                    for (int index = 0; index < values.Length; ++index)
                    {
                        if (!TryReadNumber(numbers[index], out values[index]))
                        {
                            return false;
                        }
                    }

                    transform = new SvgMatrix(values[0], values[1], values[2], values[3], values[4], values[5]);
                    return true;
            }

            return false;
        }

        // ===========[ Subclasses/structs ]====================================
        // One read of one document: the dpi lengths are read at, and the metadata filled in as the elements are walked
        private sealed class DocumentReader
        {
            private readonly XmlElement m_svg;
            private readonly double m_dpiX;
            private readonly double m_dpiY;
            private readonly SvgMetadata m_metadata;

            public DocumentReader (XmlElement svg, double dpiX, double dpiY)
            {
                m_svg = svg;
                m_dpiX = dpiX;
                m_dpiY = dpiY;
                m_metadata = new SvgMetadata(svg.Attributes["id"]?.Value, ReadLengthAttribute(svg, "width", dpiX), ReadLengthAttribute(svg, "height", dpiY));
            }

            public SvgDocument Read ()
            {
                foreach (XmlAttribute attribute in m_svg.Attributes)
                {
                    if (attribute.Name.StartsWith("xmlns:", StringComparison.Ordinal))
                    {
                        m_metadata.GetOrGenerateNamespace(attribute.Name.Substring("xmlns:".Length), attribute.Value);
                    }
                    else
                    {
                        m_metadata.AddMetadata(attribute.Name, attribute.Value);
                    }
                }

                foreach (XmlNode child in m_svg.ChildNodes)
                {
                    // Comments and text sit beside the elements and carry nothing worth keeping, and what is drawn is in
                    //  the tree rather than here
                    if ((child is XmlElement) && (child.Name != "g") && (child.Name != "path") && (Array.IndexOf(kShapeNames, child.Name) < 0))
                    {
                        m_metadata.AddMetadata(child.Name, child.OuterXml);
                    }
                }

                return new SvgDocument(this.ReadGroup(m_svg, this.ReadViewBox(), InheritedPaint.None), m_metadata);
            }

            private static double ReadLengthAttribute (XmlElement element, string name, double dpi)
            {
                return (element.Attributes[name]?.Value is string text) && TryReadLength(text, dpi, out double pixels) ? pixels : double.NaN;
            }

            // The viewBox is the rectangle of the drawing's own coordinates that fills the svg's width and height. How
            //  it fills them is preserveAspectRatio's: none stretches each axis on its own, otherwise one even scale,
            //  the largest that fits (meet, the default) or the smallest that covers (slice), placed at the min, mid
            //  (the default) or max of each axis. With no width or height the viewBox's own size stands in. Without a
            //  viewBox the drawing's coordinates are the svg's.
            //  https://www.w3.org/TR/SVG11/coords.html#ViewBoxAttribute
            private SvgMatrix ReadViewBox ()
            {
                if ((m_svg.Attributes["viewBox"]?.Value is not string viewBoxText) || !TryReadNumbers(viewBoxText, out double[] box) || (box.Length != 4) || (box[2] <= 0.0) || (box[3] <= 0.0))
                {
                    return SvgMatrix.Identity;
                }

                double boxWidth = box[2];
                double boxHeight = box[3];
                double width = m_metadata.Width;
                double height = m_metadata.Height;
                double viewportWidth = !double.IsNaN(width) ? width : !double.IsNaN(height) ? height * boxWidth / boxHeight : boxWidth;
                double viewportHeight = !double.IsNaN(height) ? height : !double.IsNaN(width) ? width * boxHeight / boxWidth : boxHeight;

                string[] aspect = (m_svg.Attributes["preserveAspectRatio"]?.Value ?? string.Empty).Split(kNumberListSeparators, StringSplitOptions.RemoveEmptyEntries);
                int first = (aspect.Length > 0) && (aspect[0] == "defer") ? 1 : 0;
                string align = aspect.Length > first ? aspect[first] : "xMidYMid";
                bool slice = (aspect.Length > first + 1) && (aspect[first + 1] == "slice");

                double scaleX = viewportWidth / boxWidth;
                double scaleY = viewportHeight / boxHeight;
                double offsetX = 0.0;
                double offsetY = 0.0;
                if (align != "none")
                {
                    scaleX = scaleY = slice ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);
                    offsetX = _Place(align.StartsWith("xMin", StringComparison.Ordinal), align.StartsWith("xMax", StringComparison.Ordinal), viewportWidth - (boxWidth * scaleX));
                    offsetY = _Place(align.EndsWith("YMin", StringComparison.Ordinal), align.EndsWith("YMax", StringComparison.Ordinal), viewportHeight - (boxHeight * scaleY));
                }

                return SvgMatrix.Translate(-box[0], -box[1]).Then(SvgMatrix.Scale(scaleX, scaleY)).Then(SvgMatrix.Translate(offsetX, offsetY));

                static double _Place (bool atMin, bool atMax, double room) => atMin ? 0.0 : atMax ? room : room / 2.0;
            }

            // A group's transform applies to everything inside it, on top of what the groups around it apply, and so
            //  does its paint, under anything nearer
            private SvgNode ReadGroup (XmlNode group, SvgMatrix inheritedTransform, InheritedPaint inheritedPaint)
            {
                string? id = group.Attributes?["id"]?.Value;
                if (string.IsNullOrEmpty(id))
                {
                    id = $"__unnamed_{group.Name}_{Guid.NewGuid()}";
                }

                if (group.Attributes != null)
                {
                    foreach (XmlAttribute attribute in group.Attributes)
                    {
                        m_metadata.AddMetadata(id, attribute.Name, attribute.Value);
                    }
                }

                SvgMatrix groupTransform = this.ReadTransformOf(group).Then(inheritedTransform);
                InheritedPaint groupPaint = inheritedPaint.Under(ReadPaintProperties(group));
                var children = new List<SvgNode>();
                foreach (XmlNode child in group.ChildNodes)
                {
                    if (child.Name == "g")
                    {
                        children.Add(this.ReadGroup(child, groupTransform, groupPaint));
                    }
                    else if ((child is XmlElement element) && (this.ReadDrawn(element, groupTransform, groupPaint) is SvgNode drawn))
                    {
                        children.Add(drawn);
                    }
                }

                return new SvgNode(id, children);
            }

            // A path, or a shape drawn as the path of its outline. Its own paint wins over its groups'.
            private SvgNode? ReadDrawn (XmlElement element, SvgMatrix groupTransform, InheritedPaint groupPaint)
            {
                string? pathText = element.Name == "path" ? element.Attributes["d"]?.Value : this.ReadShapeAsPathText(element);
                if (pathText == null)
                {
                    return null;
                }

                InheritedPaint paint = groupPaint.Under(ReadPaintProperties(element));
                SvgPaint? fill = paint.Properties.TryGetValue("fill", out string? fillText) ? ReadPaint(fillText, allowGradient: false) : kDefaultFill;
                SvgPaint? stroke = paint.Properties.TryGetValue("stroke", out string? strokeText) ? ReadPaint(strokeText, allowGradient: true) : null;

                // A value that does not read leaves the default, rather than zeroing it
                double strokeWidth = 1.0;
                if (paint.Properties.TryGetValue("stroke-width", out string? strokeWidthText) && TryReadLength(strokeWidthText, m_dpiX, out double parsedStrokeWidth))
                {
                    strokeWidth = parsedStrokeWidth;
                }

                return new SvgNode(element.Attributes["id"]?.Value, pathText, this.ReadTransformOf(element).Then(groupTransform), fill, stroke, strokeWidth, paint.Opacity);
            }

            // The path of a shape's outline, in path markup every toolkit's parser reads, or null for an element that is
            //  not a shape and for a shape of no size, which SVG draws nothing for. A rect's corners round when it gives
            //  rx or ry (one stands in for the other, and neither runs past half the side); an ellipse missing one radius
            //  takes the other, as SVG 2 has it.
            //  https://www.w3.org/TR/SVG11/shapes.html
            private string? ReadShapeAsPathText (XmlElement shape)
            {
                switch (shape.Name)
                {
                    case "rect":
                    {
                        double x = this.ReadLength(shape, "x", m_dpiX);
                        double y = this.ReadLength(shape, "y", m_dpiY);
                        double width = this.ReadLength(shape, "width", m_dpiX);
                        double height = this.ReadLength(shape, "height", m_dpiY);
                        if ((width <= 0.0) || (height <= 0.0))
                        {
                            return null;
                        }

                        double rx = this.ReadLength(shape, "rx", m_dpiX, double.NaN);
                        double ry = this.ReadLength(shape, "ry", m_dpiY, double.NaN);
                        rx = Math.Clamp(double.IsNaN(rx) ? (double.IsNaN(ry) ? 0.0 : ry) : rx, 0.0, width / 2.0);
                        ry = Math.Clamp(double.IsNaN(ry) ? rx : ry, 0.0, height / 2.0);
                        if ((rx <= 0.0) || (ry <= 0.0))
                        {
                            return Invariant($"M{x},{y} H{x + width} V{y + height} H{x} Z");
                        }

                        return Invariant($"M{x + rx},{y} H{x + width - rx} A{rx},{ry} 0 0 1 {x + width},{y + ry} V{y + height - ry} A{rx},{ry} 0 0 1 {x + width - rx},{y + height} H{x + rx} A{rx},{ry} 0 0 1 {x},{y + height - ry} V{y + ry} A{rx},{ry} 0 0 1 {x + rx},{y} Z");
                    }

                    case "circle":
                    {
                        double radius = this.ReadLength(shape, "r", m_dpiX);
                        return radius > 0.0 ? _Ellipse(this.ReadLength(shape, "cx", m_dpiX), this.ReadLength(shape, "cy", m_dpiY), radius, radius) : null;
                    }

                    case "ellipse":
                    {
                        double rx = this.ReadLength(shape, "rx", m_dpiX, double.NaN);
                        double ry = this.ReadLength(shape, "ry", m_dpiY, double.NaN);
                        rx = double.IsNaN(rx) ? ry : rx;
                        ry = double.IsNaN(ry) ? rx : ry;
                        return (rx > 0.0) && (ry > 0.0) ? _Ellipse(this.ReadLength(shape, "cx", m_dpiX), this.ReadLength(shape, "cy", m_dpiY), rx, ry) : null;
                    }

                    case "line":
                        return Invariant($"M{this.ReadLength(shape, "x1", m_dpiX)},{this.ReadLength(shape, "y1", m_dpiY)} L{this.ReadLength(shape, "x2", m_dpiX)},{this.ReadLength(shape, "y2", m_dpiY)}");

                    // A list of x, y pairs; a number left without its partner is dropped, as SVG has it
                    case "polyline":
                    case "polygon":
                    {
                        if ((shape.Attributes["points"]?.Value is not string pointsText) || !TryReadNumbers(pointsText, out double[] numbers) || (numbers.Length < 4))
                        {
                            return null;
                        }

                        var text = new StringBuilder();
                        for (int index = 0; index + 1 < numbers.Length; index += 2)
                        {
                            text.Append(index == 0 ? 'M' : 'L').Append(Invariant($"{numbers[index]},{numbers[index + 1]} "));
                        }

                        if (shape.Name == "polygon")
                        {
                            text.Append('Z');
                        }

                        return text.ToString().TrimEnd();
                    }
                }

                return null;

                // Two half turns from the left end to the right and back
                static string _Ellipse (double cx, double cy, double rx, double ry)
                {
                    return Invariant($"M{cx - rx},{cy} A{rx},{ry} 0 1 0 {cx + rx},{cy} A{rx},{ry} 0 1 0 {cx - rx},{cy} Z");
                }
            }

            private double ReadLength (XmlElement element, string name, double dpi, double fallback = 0.0)
            {
                return (element.Attributes[name]?.Value is string text) && TryReadLength(text, dpi, out double pixels) ? pixels : fallback;
            }

            private SvgMatrix ReadTransformOf (XmlNode element)
            {
                return element.Attributes?["transform"]?.Value is string text ? ReadTransformList(text, m_dpiX, m_dpiY) : SvgMatrix.Identity;
            }

            // Paint can be given as attributes or in style, and style wins where both give one. Names and values are
            //  trimmed, since hand-written style puts a space after each ';' and ':'.
            private static Dictionary<string, string> ReadPaintProperties (XmlNode path)
            {
                var paint = new Dictionary<string, string>();
                foreach (string attributeName in kPaintAttributes)
                {
                    if (path.Attributes?[attributeName]?.Value is string attributeValue)
                    {
                        paint[attributeName] = attributeValue.Trim();
                    }
                }

                if (path.Attributes?["style"]?.Value is string style)
                {
                    foreach (string stylePart in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] nameAndValue = stylePart.Split(':', 2);
                        if (nameAndValue.Length == 2)
                        {
                            paint[nameAndValue[0].Trim().ToLowerInvariant()] = nameAndValue[1].Trim();
                        }
                    }
                }

                return paint;
            }
        }

        // What a group passes down to what is inside it: its fill, stroke and stroke-width, under any an element or a
        //  nearer group gives (inherit asks for the group's), and its opacity, which multiplies into each element's. SVG
        //  draws a translucent group as a whole, so where its elements overlap this is a little lighter than SVG's.
        private sealed record InheritedPaint (IReadOnlyDictionary<string, string> Properties, double Opacity)
        {
            public static InheritedPaint None { get; } = new InheritedPaint(new Dictionary<string, string>(), 1.0);

            public InheritedPaint Under (Dictionary<string, string> nearer)
            {
                var properties = new Dictionary<string, string>(this.Properties);
                foreach (KeyValuePair<string, string> property in nearer)
                {
                    if ((property.Key != "opacity") && (property.Value != "inherit"))
                    {
                        properties[property.Key] = property.Value;
                    }
                }

                double opacity = this.Opacity;
                if (nearer.TryGetValue("opacity", out string? opacityText) && TryReadNumber(opacityText, out double nearerOpacity))
                {
                    opacity *= nearerOpacity;
                }

                return new InheritedPaint(properties, opacity);
            }
        }
    }
}
