namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using System.Xml;
    using AJut.Storage;
    using AJut.Tree;
    using File = System.IO.File;
    using Stream = System.IO.Stream;

    /// <summary>
    /// Utilities for serialization to and from the scalable vector graphics (svg) format
    /// </summary>
    public static class SvgSerialization
    {
        private const double mm_per_inch = 25.4;

        // ==================== [ Main Interface ] ========================

        /// <summary>
        /// Load an <see cref="SvgSource"/> from the given file path
        /// </summary>
        public static SvgSource LoadSvg (string filePath)
        {
            using (Stream file = File.OpenRead(filePath))
            {
                return LoadSvg(file);
            }
        }

        /// <summary>
        /// Load an <see cref="SvgSource"/> from the given stream
        /// </summary>
        public static SvgSource LoadSvg (Stream stream)
        {
            return LoadSvg(stream, IntPtr.Zero);
        }

        /// <summary>
        /// Load an <see cref="SvgSource"/> from the given stream, providing hwnd for accurate calculation of dpi conversion for units specified in mm
        /// </summary>
        public static SvgSource LoadSvg (Stream stream, IntPtr hwnd)
        {
            // Note: This is a bit hacky
            double dpiX = 96.0;
            double dpiY = 96.0;
            try
            {
                // Note: This is a bit hacky as we do not have access to an actual display yet. That means this will
                //  be the default display's dpi I believe.
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd(hwnd))
                {
                    dpiX = graphics.DpiX;
                    dpiY = graphics.DpiY;
                }
            }
            catch { }


            SvgDocument document = SvgParsing.Parse(stream, dpiX, dpiY);
            return document == null ? null : new SvgSource(ToTreeElement(document.Root), document.Metadata);
        }

        /// <summary>
        /// Convert an existing <see cref="Path"/> to an <see cref="SvgSource"/>
        /// </summary>
        public static SvgSource ConvertPathToSvg (Path path)
        {
            string id = path.Name ?? Guid.NewGuid().ToString();
            return new SvgSource(new SvgTreeElement($"{id}__root", path.Data, path.Fill, path.StrokeThickness, path.Stroke), new SvgMetadata(id, path.ActualWidth, path.ActualHeight));
        }

        /// <summary>
        /// Write an <see cref="SvgSource"/> to the given file path
        /// </summary>
        public static void WriteSvgTo (SvgSource source, string filePath)
        {
            using (Stream file = File.Open(filePath, System.IO.FileMode.Truncate, System.IO.FileAccess.Write))
            {
                WriteSvgTo(source, file);
            }
        }

        /// <summary>
        /// Write an <see cref="SvgSource"/> to the given stream
        /// </summary>
        public static void WriteSvgTo (SvgSource source, Stream stream)
        {
            // Note: This is a bit hacky
            double dpiX = 96.0;
            double dpiY = 96.0;
            try
            {
                // Note: This is a bit hacky as we do not have access to an actual display yet. That means this will
                //  be the default display's dpi I believe.
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                {
                    dpiX = graphics.DpiX;
                    dpiY = graphics.DpiY;
                }
            }
            catch { }

            XmlDocument doc = new XmlDocument();
            doc.AppendChild(doc.CreateXmlDeclaration("1.0", Encoding.UTF8.EncodingName, "no"));
            var svg = doc.CreateNode(XmlNodeType.Element, "svg", "http://www.w3.org/2000/svg");
            doc.AppendChild(svg);

            _AddAttr(svg, "id", source.Metadata.Id);
            int hor_mm_scaler = (int)Math.Round((source.Metadata.Width / dpiX) * mm_per_inch);
            int ver_mm_scaler = (int)Math.Round((source.Metadata.Height / dpiY) * mm_per_inch);

            // The viewbox is specified as a rect which acts as the coordinate space
            var rects = TreeTraversal<SvgTreeElement>.All(source.Root).Where(e => e.Data != null).Select(e => e.Data.Bounds).ToList();
            Rect rectCollector = rects.First();

            foreach (var element in rects.Skip(1))
            {
                rectCollector = Rect.Union(rectCollector, element);
            }


            int width = (int)rectCollector.Width;
            int height = (int)rectCollector.Height;
            // Every number is written culture-invariant: svg numbers use a period whatever culture the app runs in
            _AddAttr(svg, "width", FormattableString.Invariant($"{source.Metadata.Width}px"));
            _AddAttr(svg, "height", FormattableString.Invariant($"{source.Metadata.Height}px"));
            _AddAttr(svg, "viewBox", FormattableString.Invariant($"{_RoundForViewbox(rectCollector.Left)} {_RoundForViewbox(rectCollector.Top)} {_RoundForViewbox(rectCollector.Width)} {_RoundForViewbox(rectCollector.Height)}"));
            _AddAttr(svg, "version", "1.1");

            double _RoundForViewbox (double _part) => Math.Round(_part, 6, MidpointRounding.ToZero);

            var processingQueue = new Queue<SvgWriteElement>();
            source.Root.Children.ForEach(c => processingQueue.Enqueue(new SvgWriteElement { Parent = svg, Element = c }));
            while (processingQueue.Count > 0)
            {
                var evalTarget = processingQueue.Dequeue();
                XmlNode buildTargetNode;
                Action writeLast = null;

                // Path unique stuff
                if (evalTarget.Element.Data != null)
                {
                    buildTargetNode = doc.CreateNode(XmlNodeType.Element, "path", "http://www.w3.org/2000/svg");
                    writeLast = () => _AddAttr(buildTargetNode, "d", ((IFormattable)evalTarget.Element.Data).ToString(null, CultureInfo.InvariantCulture));

                    // The data is written as the geometry's own figures, and a read puts a path's transforms (its own,
                    //  its groups' and the viewBox's) on the geometry, so they go with it or the path comes back moved
                    Transform geometryTransform = evalTarget.Element.Data.Transform;
                    if ((geometryTransform != null) && !geometryTransform.Value.IsIdentity)
                    {
                        _AddAttr(buildTargetNode, "transform", TransformToJsString(geometryTransform));
                    }
                }
                // Geom unique stuff
                else
                {
                    buildTargetNode = doc.CreateNode(XmlNodeType.Element, "g", "http://www.w3.org/2000/svg");
                    if (evalTarget.Element.LocalTransform != null && evalTarget.Element.LocalTransform != Transform.Identity)
                    {
                        writeLast = () => _AddAttr(buildTargetNode, "transform", TransformToJsString(evalTarget.Element.LocalTransform));
                    }
                }

                _AddAttr(buildTargetNode, "id", evalTarget.Element.Id);

                // Style
                string fill = "";
                if (BrushToJSString(evalTarget.Element.FillBrush) is string brushTxt)
                {
                    fill = $"fill:{brushTxt};";
                }
                else if (evalTarget.Element.Data != null)
                {
                    // SVG fills a path that names no fill black, so a path with none says so
                    fill = "fill:none;";
                }

                string stroke = "";
                if (BrushToJSString(evalTarget.Element.StrokeBrush) is string strokeTxt)
                {
                    stroke = $"stroke:{strokeTxt};";
                }

                string opacity = "";
                if (evalTarget.Element.Opacity < 1.0)
                {
                    opacity = FormattableString.Invariant($"opacity:{evalTarget.Element.Opacity:0.0000}");
                }

                string strokeWidth = "";
                if (evalTarget.Element.StrokeWidth > 0.0)
                {
                    strokeWidth = FormattableString.Invariant($"stroke-width:{evalTarget.Element.StrokeWidth}px;");
                }

                string style = $"{fill}{stroke}{strokeWidth}{opacity}";
                if (style.IsNotNullOrEmpty())
                {
                    _AddAttr(buildTargetNode, "style", style);
                }

                writeLast?.Invoke();

                evalTarget.Parent.AppendChild(buildTargetNode);
                evalTarget.Element.Children.ForEach(c => processingQueue.Enqueue(new SvgWriteElement { Parent = buildTargetNode, Element = c }));
            }

            doc.AppendChild(svg);

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace
            };
            using (XmlWriter writer = XmlWriter.Create(stream, settings))
            {
                doc.Save(writer);
            }

            // A value there is none of (a path the document gave no id) writes no attribute
            void _AddAttr (XmlNode _target, string _key, object _value)
            {
                if (_value == null)
                {
                    return;
                }

                var _attr = doc.CreateAttribute(_key);
                _attr.Value = _value.ToString();
                _target.Attributes.Append(_attr);
            }
        }

        // ==================== [ Helper Utilities ] ========================

        private static string BrushToJSString (this Brush brush)
        {
            if (brush is SolidColorBrush solid)
            {
                return _ColorStr(solid.Color);
            }
            else if (brush is LinearGradientBrush lgb)
            {
                StringBuilder sb = new StringBuilder();
                Vector up = new Vector(0, 1);
                Vector dir = (Vector)lgb.EndPoint - (Vector)lgb.StartPoint;
                dir.Normalize();
                double angle = Vector.AngleBetween(up, dir);

                _AppendWithGradientText(sb, "linear", FormattableString.Invariant($"{angle:0.00000}deg"), lgb.GradientStops);
                return sb.ToString();
            }
            else if (brush is RadialGradientBrush rgb)
            {
                StringBuilder sb = new StringBuilder();
                _AppendWithGradientText(sb, "radial", FormattableString.Invariant($"at {rgb.Center.X:0.000}px {rgb.Center.Y:0.000}px"), rgb.GradientStops);
                return sb.ToString();
            }

            return null;

            // A stop's offset runs 0 to 1 and a gradient's text gives it as a percent
            void _AppendWithGradientText (StringBuilder _target, string _type, string _starter, GradientStopCollection _stops)
            {
                _target.Append($"{_type}-gradient(");
                _target.Append(_starter);
                foreach (GradientStop stop in _stops)
                {
                    _target.Append(FormattableString.Invariant($", {_ColorStr(stop.Color)} {stop.Offset * 100.0:0.####}%"));
                }
                _target.Append(")");
            }

            // An alpha short of opaque is written as rgba's 0 to 1, rounded up at the sixth place, so reading it back
            //  (which truncates to a byte) and any tool that rounds both come back to the same byte
            static string _ColorStr (Color _color)
            {
                if (_color.A == 255)
                {
                    return $"rgb({_color.R},{_color.G},{_color.B})";
                }

                double alpha = Math.Ceiling((_color.A / 255.0) * 1e6) / 1e6;
                return FormattableString.Invariant($"rgba({_color.R},{_color.G},{_color.B},{alpha:0.######})");
            }
        }

        private static string TransformToJsString (this Transform transform)
        {
            if (transform == Transform.Identity)
            {
                return "";
            }

            if (transform is TranslateTransform translate)
            {
                return FormattableString.Invariant($"translate({translate.X:0.00000000}, {translate.Y:0.00000000})");
            }
            else if (transform is ScaleTransform scale)
            {
                return FormattableString.Invariant($"scale({scale.ScaleX:0.00000000}, {scale.ScaleY:0.00000000})");
            }
            else if (transform is RotateTransform rot)
            {
                if ((rot.CenterX != 0.0) || (rot.CenterY != 0.0))
                {
                    return FormattableString.Invariant($"rotate({rot.Angle:0.00000000} {rot.CenterX:0.00000000} {rot.CenterY:0.00000000})");
                }

                return FormattableString.Invariant($"rotate({rot.Angle:0.00000000}deg)");
            }
            else if (transform is SkewTransform skew)
            {
                return FormattableString.Invariant($"skew({skew.AngleX:0.00000000}deg, {skew.AngleY:0.00000000}deg)");
            }
            else if (transform is MatrixTransform matrixTransform)
            {
                Matrix m = matrixTransform.Matrix;
                return FormattableString.Invariant($"matrix({m.M11:0.########} {m.M12:0.########} {m.M21:0.########} {m.M22:0.########} {m.OffsetX:0.########} {m.OffsetY:0.########})");
            }
            else if (transform is TransformGroup group)
            {
                // WPF's group applies its first child first and SVG's list applies its last entry first, so the children
                //  are written backwards, the mirror of how they are read
                return String.Join(" ", group.Children.Reverse().Select(TransformToJsString));
            }

            return "";
        }

        // The shared read, in WPF's types. WPF parses the path data itself, as it always has, and the transform goes
        //  on the geometry, never on the element, so the template, which also applies each element's FinalTransform,
        //  does not apply it twice.
        private static SvgTreeElement ToTreeElement (SvgNode node)
        {
            if (!node.IsDrawn)
            {
                var group = new SvgTreeElement(node.Id, null);
                foreach (SvgNode child in node.Children)
                {
                    group.AddChild(ToTreeElement(child));
                }

                return group;
            }

            PathGeometry geometry = new PathGeometry();
            geometry.AddGeometry(Geometry.Parse(node.PathText));
            if (!node.Transform.IsIdentity)
            {
                SvgMatrix m = node.Transform;
                geometry.Transform = new MatrixTransform(new Matrix(m.M11, m.M12, m.M21, m.M22, m.OffsetX, m.OffsetY));
            }

            return new SvgTreeElement(node.Id, geometry, ToBrush(node.Fill), node.StrokeWidth, ToBrush(node.Stroke), node.Opacity);
        }

        private static Brush ToBrush (SvgPaint paint)
        {
            if (paint == null)
            {
                return null;
            }

            if (paint.Kind == eSvgPaintKind.Gradient)
            {
                return CoerceUtils.CoerceBrushFrom(paint.Text);
            }

            return new SolidColorBrush(Color.FromArgb(paint.Color.A, paint.Color.R, paint.Color.G, paint.Color.B));
        }

        private class SvgWriteElement
        {
            public XmlNode Parent { get; init; }
            public SvgTreeElement Element { get; init; }
        }
    }
}