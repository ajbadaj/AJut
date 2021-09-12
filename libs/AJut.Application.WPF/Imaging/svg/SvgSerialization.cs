namespace AJut.Application
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
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
        private static readonly Regex kAllTransformCapture = new Regex(@"(\w+[ ]?\([\. \d\-,mmpxdeg]+\))");
        private static readonly Regex kJsTranslateCapture = new Regex(@"translatex?[ ]?\(([\-\d\., pxmm]*)\)");
        private static readonly Regex kJsRotateCapture = new Regex(@"rotate[ ]?\(([\-\d\. deg]*)\)");
        private static readonly Regex kJsScaleCapture = new Regex(@"scalex?[ ]?\(([\-\d\., pxmm]*)\)");
        private static readonly Regex kJsSkewCapture = new Regex(@"skewx?[ ]?\(([\-\d\., deg]*)\)");
        private static readonly Regex kJsColorCapture = new Regex(@"rgba?\(([\w, #]+)\)");

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
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                {
                    dpiX = graphics.DpiX;
                    dpiY = graphics.DpiY;
                }
            }
            catch { }


            SvgMetadata metadata;
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(stream);
            }
            catch (Exception exc)
            {
                Logger.LogError("Failed to load svg from stream", exc);
                return null;
            }

            XmlElement svg = doc.DocumentElement;
            if (svg.Name == "svg")
            {
                metadata = new SvgMetadata(
                    svg.Attributes["id"]?.Value,
                    _AttrOrDefault(svg, "width", _TryParseHorJSUnitStringToPixels, Double.NaN),
                    _AttrOrDefault(svg, "height", _TryParseVerJSUnitStringToPixels, Double.NaN)
                );

                foreach (XmlAttribute attr in svg.Attributes)
                {
                    if (attr.Name.StartsWith("xmlns:"))
                    {
                        metadata.GetOrGenerateNamespace(attr.Name.Substring("xmlns:".Length), attr.Value);
                    }
                    else
                    {
                        metadata.AddMetadata(attr.Name, attr.Value);
                    }
                }

                foreach (XmlElement child in svg.ChildNodes)
                {
                    if (child.Name != "g" && child.Name != "path")
                    {
                        metadata.AddMetadata(child.Name, child.OuterXml);
                    }
                }

                var node = _ReadPathsFromGeomContainer(svg);
                if (node != null)
                {
                    return new SvgSource(node, metadata);
                }
            }

            return null;

            bool _TryParseHorJSUnitStringToPixels (string _value, out double _pixelSize) => TryParseJSUnitStringToPixels(_value, dpiX, out _pixelSize);
            bool _TryParseVerJSUnitStringToPixels (string _value, out double _pixelSize) => TryParseJSUnitStringToPixels(_value, dpiY, out _pixelSize);


            T _AttrOrDefault<T> (XmlElement _src, string _attr, TryParser<T> _converter, T _default)
            {
                if (_src.Attributes[_attr]?.Value is string _strAttrValue && _converter(_strAttrValue, out T _found))
                {
                    return _found;
                }

                return _default;
            }

            SvgTreeElement _ReadPathsFromGeomContainer (XmlNode _geomNode)
            {
                string geomId = String.Empty;
                Transform localTransform = null;

                if (_geomNode.Attributes["id"].Value is string idStr)
                {
                    geomId = idStr;
                }

                if (geomId.IsNullOrEmpty())
                {
                    geomId = $"__unnamed_{_geomNode.Name}_{Guid.NewGuid()}";
                }

                foreach (XmlAttribute _attr in _geomNode.Attributes)
                {
                    metadata.AddMetadata(geomId, _attr.Name, _attr.Value);
                }

                if (_geomNode.Attributes["transform"]?.Value is string transformData)
                {
                    localTransform = ParseJsStringToTransform(transformData, dpiX, dpiY);
                }

                var currentNode = new SvgTreeElement(geomId, null);// localTransform);

                foreach (XmlNode _childNode in _geomNode.ChildNodes)
                {
                    if (_childNode.Name == "g")
                    {
                        currentNode.AddChild(_ReadPathsFromGeomContainer(_childNode));
                    }
                    else if (_childNode.Name == "path")
                    {
                        Geometry geom;
                        Brush fillBrush = null;
                        Brush strokeBrush = null;
                        double strokeWidth = 1.0;
                        double opacity = 1.0;
                        string id = null;


                        if (_childNode.Attributes["d"]?.Value is string geometryText)
                        {
                            PathGeometry pathGeom = new PathGeometry();
                            pathGeom.AddGeometry(Geometry.Parse(geometryText));
                            try
                            {
                                if (localTransform != null)
                                {
                                    pathGeom.Transform = localTransform;
                                }
                            }
                            catch { }

                            geom = pathGeom;
                        }
                        else
                        {
                            continue;
                        }


                        if (_childNode.Attributes["id"]?.Value is string pathId)
                        {
                            id = pathId;
                        }

                        if (_childNode.Attributes["style"]?.Value is string style)
                        {
                            foreach (string stylePart in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
                            {
                                string[] styleElement = stylePart.Split(':');
                                Debug.Assert(styleElement.Length == 2);
                                switch (styleElement[0].ToLower())
                                {
                                    case "fill":
                                        TryParseJsBrushFrom(styleElement[1], out fillBrush);
                                        break;

                                    case "stroke-width":
                                        // TODO: Can be em, or % - otherwise it's px
                                        double.TryParse(styleElement[1], out strokeWidth);
                                        break;

                                    case "stroke":
                                        strokeBrush = CoerceUtils.CoerceBrushFrom(styleElement[1]);
                                        break;

                                    case "opacity":
                                        double.TryParse(styleElement[1], out opacity);
                                        break;
                                }
                            }
                        }

                        currentNode.AddChild(new SvgTreeElement(id, geom, fillBrush, strokeWidth, strokeBrush, opacity));
                    }
                }

                return currentNode;
            }
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
            _AddAttr(svg, "width", $"{source.Metadata.Width}px");
            _AddAttr(svg, "height", $"{source.Metadata.Height}px");
            _AddAttr(svg, "viewBox", $"{_RoundForViewbox(rectCollector.Left)} {_RoundForViewbox(rectCollector.Top)} {_RoundForViewbox(rectCollector.Width)} {_RoundForViewbox(rectCollector.Height)}");
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
                    writeLast = () => _AddAttr(buildTargetNode, "d", evalTarget.Element.Data);
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

                string stroke = "";
                if (BrushToJSString(evalTarget.Element.StrokeBrush) is string strokeTxt)
                {
                    stroke = $"stroke:{strokeTxt};";
                }

                string opacity = "";
                if (evalTarget.Element.Opacity < 1.0)
                {
                    opacity = $"opacity:{evalTarget.Element.Opacity:0.0000}";
                }

                string strokeWidth = "";
                if (evalTarget.Element.StrokeWidth > 0.0)
                {
                    strokeWidth = $"stroke-width:{evalTarget.Element.StrokeWidth}px;";
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

            void _AddAttr (XmlNode _target, string _key, object _value)
            {
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

                _AppendWithGradientText(sb, "linear", $"{angle:0.00000}deg", lgb.GradientStops);
                return sb.ToString();
            }
            else if (brush is RadialGradientBrush rgb)
            {
                StringBuilder sb = new StringBuilder();
                _AppendWithGradientText(sb, "radial", $"at {rgb.Center.X:0.000}px {rgb.Center.Y:0.000}px", rgb.GradientStops);
                return sb.ToString();
            }

            return null;

            void _AppendWithGradientText (StringBuilder _target, string _type, string _starter, GradientStopCollection _stops)
            {
                _target.Append($"{_type}-gradient(");
                _target.Append(_starter);
                foreach (GradientStop stop in _stops)
                {
                    _target.Append($", {_ColorStr(stop.Color)} {stop.Offset:0.00}%");
                }
                _target.Append(")");
            }

            string _ColorStr (Color _color)
            {
                return $"rgb({_color.R},{_color.G},{_color.B})";
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
                return $"translate({translate.X:0.00000000}, {translate.Y:0.00000000})";
            }
            else if (transform is ScaleTransform scale)
            {
                return $"scale({scale.ScaleX:0.00000000}, {scale.ScaleY:0.00000000})";
            }
            else if (transform is RotateTransform rot)
            {
                return $"rotate({rot.Angle:0.00000000}deg)";
            }
            else if (transform is SkewTransform skew)
            {
                return $"skew({skew.AngleX:0.00000000}deg, {skew.AngleY:0.00000000}deg)";
            }
            else if (transform is TransformGroup group)
            {
                return String.Join(" ", group.Children.Select(TransformToJsString));
            }

            return "";
        }

        private static Transform ParseJsStringToTransform (string transformData, double dpiX, double dpiY)
        {
            transformData = transformData.ToLower();

            // =========[ Gather Outputs ]===========
            List<Transform> outputs = new List<Transform>();

            string[] allTransforms = kAllTransformCapture.Matches(transformData).Select(r => r.Groups[r.Groups.Count - 1].Value).ToArray();
            foreach (string transform in allTransforms)
            {
                // == Translate
                string translate = TryGetRegexMatch(kJsTranslateCapture, transform);
                if (translate != null)
                {
                    string[] numbers = translate.Trim().Split(',');
                    Debug.Assert(numbers.Length == 1 || numbers.Length == 2);
                    double y = 0.0;
                    if (TryParseJSUnitStringToPixels(numbers[0], dpiX, out double x)
                            && (numbers.Length == 1 || TryParseJSUnitStringToPixels(numbers[1], dpiY, out y)))
                    {
                        outputs.Add(new TranslateTransform(x, y));
                    }

                    continue;
                }

                // == Rotate
                string rotate = TryGetRegexMatch(kJsRotateCapture, transform);
                if (rotate != null)
                {
                    if (_TryParseDegrees(rotate, out double angle))
                    {
                        outputs.Add(new RotateTransform(angle));
                    }

                    continue;
                }

                // == Scale
                string scale = TryGetRegexMatch(kJsScaleCapture, transform);
                if (scale != null)
                {
                    string[] numbers = scale.Trim().Split(',');
                    Debug.Assert(numbers.Length == 1 || numbers.Length == 2);
                    double y = 0.0;
                    if (double.TryParse(numbers[0], out double x)
                            && (numbers.Length == 1 || double.TryParse(numbers[1], out y)))
                    {
                        outputs.Add(new ScaleTransform(x, y));
                    }

                    continue;
                }

                // == Skew
                string skew = TryGetRegexMatch(kJsSkewCapture, transform);
                if (skew != null)
                {
                    string[] numbers = skew.Trim().Split(',');
                    Debug.Assert(numbers.Length == 1 || numbers.Length == 2);
                    double skewY = 0.0;
                    if (_TryParseDegrees(numbers[0], out double skewX)
                            && (numbers.Length == 1 || _TryParseDegrees(numbers[1], out skewY)))
                    {
                        outputs.Add(new SkewTransform(skewX, skewY));
                    }

                    continue;
                }
            }

            // Return
            switch (outputs.Count)
            {
                case 0:
                    return Transform.Identity;
                
                case 1:
                    return outputs[0];

                default:
                    var group = new TransformGroup();
                    group.Children.AddEach(outputs);
                    return group;
            }

            
            bool _TryParseDegrees (string _target, out double _degrees)
            {
                return double.TryParse(_target.EndsWith("deg") ? _target.Substring(0, _target.Length - 3) : _target, out _degrees);
            }

            /*

            if (transformData.StartsWith("translate"))
            {
                string[] numbers = transformData.Substring("translate".Length).Trim('(', ')', ' ').Split(',');
                Debug.Assert(numbers.Length == 1 || numbers.Length == 2);

                double y = 0.0;
                if (double.TryParse(numbers[0], out double x) && (numbers.Length == 1 || double.TryParse(numbers[1], out y)))
                {
                    localTransform = new TranslateTransform(x, y);
                }
            }
            else if (transformData.StartsWith("scale"))
            {
                string[] numbers = transformData.Substring("scale".Length).Trim('(', ')', ' ').Split(',');
                Debug.Assert(numbers.Length == 1 || numbers.Length == 2);

                double y = 0.0;
                if (double.TryParse(numbers[0], out double x) && (numbers.Length == 1 || double.TryParse(numbers[1], out y)))
                {
                    localTransform = new ScaleTransform(x, y);
                }
            }
            
             */
        }

        private static bool TryParseJSUnitStringToPixels (string value, double dpiForAxis, out double pixelSize)
        {
            // Check the suffix
            string unitsPart = value.Substring(value.Length - 2, 2);
            double multiplier;
            switch (unitsPart)
            {
                case "mm": // millimeters
                    multiplier = dpiForAxis / 25.4;
                    break;

                case "cm": // centimeters
                    multiplier = dpiForAxis / 2.54;
                    break;

                case "px": // pixels (already what we want)
                    multiplier = 1.0;
                    break;

                default: // Either no suffix or unparasable
                    return double.TryParse(value, out pixelSize);
            }

            if (double.TryParse(value.Substring(0, value.Length-2), out double units))
            {
                pixelSize = units * multiplier;
                return true;
            }

            pixelSize = double.NaN;
            return false;
        }

        private static bool TryParseJsBrushFrom (string brushValue, out Brush brush)
        {
            brush = null;

            if (TryGetRegexMatch(kJsColorCapture, brushValue) is string rgb)
            {
                string[] numbers = rgb.Trim().Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Debug.Assert(numbers.Length == 3 || numbers.Length == 4);
                double opacity = 1.0;
                if (byte.TryParse(numbers[0], out byte r)
                        && byte.TryParse(numbers[1], out byte g)
                        && byte.TryParse(numbers[2], out byte b)
                        && (numbers.Length == 3 || double.TryParse(numbers[3], out opacity)))
                {
                    brush = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), r, g, b));
                    return true;
                }

                return false;
            }
            
            if (CoerceUtils.TryGetColorFromString(brushValue, out Color parsedColor))
            {
                brush = new SolidColorBrush(parsedColor);
                return true;
            }

            return false;
        }

        private static string TryGetRegexMatch (Regex eval, string input)
        {
            var _result = eval.Match(input);
            if (_result.Success)
            {
                return _result.Groups[_result.Groups.Count - 1].Value;
            }

            return null;
        }

        private delegate bool TryParser<T> (string source, out T value);

        private class SvgWriteElement
        {
            public XmlNode Parent { get; init; }
            public SvgTreeElement Element { get; init; }
        }
    }
}