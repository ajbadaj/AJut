namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.UX.Helpers;

    // ===========[ SvgNode ]====================================================
    // One element of an svg document as any toolkit can draw it: a group, which only holds other
    // nodes, or a drawn path with its paint. A drawn node carries its path twice: as written (its
    // markup, for a toolkit with a path parser of its own, the way WPF keeps using Geometry.Parse),
    // and parsed with its transform already applied (Path, in the document's coordinates, for a
    // toolkit that draws PathMarkupData).
    //
    // SVG strokes in an element's own units, so a stroke's width scales with the transforms over it,
    // the viewBox included. StrokeWidth is that width already scaled, in the document's coordinates,
    // since every toolkit draws the path there: WPF puts the transform on the geometry, which leaves
    // a Path's stroke alone, and the phones draw the already moved path. An uneven scale strokes at
    // its average, as one width can't stroke two ways.
    public sealed class SvgNode
    {
        // ===========[ Setup/Construction/Teardown ]===========================
        /// <summary>A group of <paramref name="children"/></summary>
        public SvgNode (string id, IEnumerable<SvgNode>? children = null)
        {
            this.Id = id;
            this.Children = children?.ToArray() ?? Array.Empty<SvgNode>();
            this.Transform = SvgMatrix.Identity;
            this.Opacity = 1.0;
            this.StrokeWidth = 1.0;
        }

        /// <summary>
        /// A drawn path. <paramref name="transform"/> takes the markup's coordinates to the document's, and is applied to
        /// <see cref="Path"/> and <see cref="StrokeWidth"/> here; <paramref name="strokeWidth"/> is in the path's own units,
        /// as the document writes it. Markup that does not parse leaves <see cref="Path"/> null and keeps the markup.
        /// </summary>
        public SvgNode (string? id, string pathText, SvgMatrix transform, SvgPaint? fill, SvgPaint? stroke, double strokeWidth, double opacity)
        {
            this.Id = id;
            this.Children = Array.Empty<SvgNode>();
            this.PathText = pathText;
            this.Transform = transform;
            this.Fill = fill;
            this.Stroke = stroke;
            this.StrokeWidth = strokeWidth * transform.AverageScale;
            this.Opacity = opacity;
            if (PathMarkup.TryParse(pathText, out PathMarkupData? data, out _))
            {
                this.Path = transform.Apply(data);
            }
        }

        // ===========[ Properties ]============================================
        /// <summary>The element's id. A group the document gave none gets a made-up one, as WPF's Svg has always given it; a path it gave none has none.</summary>
        public string? Id { get; }

        public IReadOnlyList<SvgNode> Children { get; }

        /// <summary>True for a path, false for a group</summary>
        public bool IsDrawn => this.PathText != null;

        /// <summary>The path data as the document wrote it (its d), in the path's own coordinates. Null for a group.</summary>
        public string? PathText { get; }

        /// <summary>
        /// The path in the document's coordinates, with <see cref="Transform"/> applied. Null for a group, and for path
        /// data <see cref="PathMarkup"/> cannot read.
        /// </summary>
        public PathMarkupData? Path { get; }

        /// <summary>What takes the path's own coordinates to the document's: its own transform, then each enclosing group's</summary>
        public SvgMatrix Transform { get; }

        /// <summary>The fill, or null for none</summary>
        public SvgPaint? Fill { get; }

        /// <summary>The stroke, or null for none</summary>
        public SvgPaint? Stroke { get; }

        /// <summary>The stroke's width in the document's coordinates: as the document writes it, scaled by <see cref="Transform"/></summary>
        public double StrokeWidth { get; }

        public double Opacity { get; }
    }
}
