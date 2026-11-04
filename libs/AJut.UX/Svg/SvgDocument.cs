namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.Helpers;

    // ===========[ SvgDocument ]================================================
    // A parsed svg document as any toolkit can draw it: the root node (the svg element itself, as a
    // group) and the metadata kept beside it. SvgParsing makes these; WPF's SvgSerialization builds
    // its SvgSource from one, and the mobile toolkits draw one directly.
    public sealed class SvgDocument
    {
        /// <summary>
        /// The miter limit a toolkit joins strokes with, and <see cref="PaintedBounds"/> measures them by: SVG's default
        /// stroke-miterlimit, a miter's length over the stroke's width, past which a corner is beveled. Svg files' own
        /// stroke-miterlimit, stroke-linejoin and stroke-linecap are not read, so every stroke is mitered with butt caps.
        /// </summary>
        public const double kStrokeMiterLimit = 4.0;

        private PathBounds? m_drawnBounds;
        private PathBounds? m_paintedBounds;

        // ===========[ Setup/Construction/Teardown ]===========================
        public SvgDocument (SvgNode root, SvgMetadata metadata)
        {
            this.Root = root;
            this.Metadata = metadata;
        }

        // ===========[ Properties ]============================================
        public SvgNode Root { get; }
        public SvgMetadata Metadata { get; }

        /// <summary>Every drawn node, in the order they are painted: depth first, each group's children in document order</summary>
        public IEnumerable<SvgNode> DrawnNodes
        {
            get
            {
                var pending = new Stack<SvgNode>();
                pending.Push(this.Root);
                while (pending.Count > 0)
                {
                    SvgNode node = pending.Pop();
                    if (node.IsDrawn)
                    {
                        yield return node;
                        continue;
                    }

                    for (int index = node.Children.Count - 1; index >= 0; --index)
                    {
                        pending.Push(node.Children[index]);
                    }
                }
            }
        }

        /// <summary>
        /// The bounds of the drawn paths' outlines, in the document's coordinates and not counting stroke widths.
        /// <see cref="PaintedBounds"/> adds the strokes. <see cref="PathBounds.Empty"/> when nothing draws.
        /// </summary>
        public PathBounds DrawnBounds => m_drawnBounds ??= this.ComputeDrawnBounds();

        /// <summary>
        /// The bounds of everything drawn with its strokes, in the document's coordinates, which is what a toolkit fits
        /// into its view and sizes itself by: each path's outline, and its stroke, half of which lies outside the outline,
        /// with its corners mitered within <see cref="kStrokeMiterLimit"/> and its open ends butt capped, as SVG joins
        /// and caps by default. WPF's Svg counts its strokes too, since each element is a Path, whose natural size is its
        /// render bounds with its pen (Shape.GetNaturalSize,
        /// https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Shapes/Shape.cs),
        /// though at a Path's own miter limit of 10. <see cref="PathBounds.Empty"/> when nothing draws.
        /// </summary>
        public PathBounds PaintedBounds => m_paintedBounds ??= this.ComputePaintedBounds();

        // ===========[ Helper Methods ]========================================
        private PathBounds ComputeDrawnBounds ()
        {
            bool hasAny = false;
            double left = 0.0;
            double top = 0.0;
            double right = 0.0;
            double bottom = 0.0;
            foreach (SvgNode node in this.DrawnNodes)
            {
                if ((node.Path == null) || node.Path.Bounds.IsEmpty)
                {
                    continue;
                }

                PathBounds bounds = node.Path.Bounds;
                if (!hasAny)
                {
                    hasAny = true;
                    left = bounds.X;
                    top = bounds.Y;
                    right = bounds.Right;
                    bottom = bounds.Bottom;
                    continue;
                }

                left = Math.Min(left, bounds.X);
                top = Math.Min(top, bounds.Y);
                right = Math.Max(right, bounds.Right);
                bottom = Math.Max(bottom, bounds.Bottom);
            }

            return hasAny ? new PathBounds(left, top, right - left, bottom - top) : PathBounds.Empty;
        }

        private PathBounds ComputePaintedBounds ()
        {
            PathBounds painted = PathBounds.Empty;
            foreach (SvgNode node in this.DrawnNodes)
            {
                if (node.Path == null)
                {
                    continue;
                }

                painted = Union(painted, node.Path.Bounds);
                if ((node.Stroke != null) && (node.StrokeWidth > 0.0))
                {
                    painted = Union(painted, PathStrokeBounds.Compute(node.Path, node.StrokeWidth, kStrokeMiterLimit));
                }
            }

            return painted;

            static PathBounds Union (PathBounds a, PathBounds b)
            {
                if (a.IsEmpty)
                {
                    return b;
                }

                if (b.IsEmpty)
                {
                    return a;
                }

                double left = Math.Min(a.X, b.X);
                double top = Math.Min(a.Y, b.Y);
                return new PathBounds(left, top, Math.Max(a.Right, b.Right) - left, Math.Max(a.Bottom, b.Bottom) - top);
            }
        }
    }
}
