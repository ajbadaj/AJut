namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;

    // ===========[ SimpleWrapLayout ]============================================
    // SimpleWrapPanel's layout, the same on every stack that has one. Children flow left to right at
    // the size they want (never stretched), and the next child drops to a new line when it would run
    // past the available width. HorizontalSpacing goes between the children on a line, VerticalSpacing
    // between lines, and a line is as tall as its tallest child.
    //
    // The rules, all deliberate:
    //  - The first child on a line never wraps, even when it alone is wider than the space: there is
    //    nowhere better to put it, and wrapping it would leave an empty line above it. It overflows.
    //  - A child that wants no size at all takes no slot and adds no spacing. One that wants a width of
    //    zero but some height still sits in the flow.
    //  - An unbounded width (infinite, or not a number) never wraps, and the layout wants its widest
    //    line. A bounded width wraps, and the layout wants no more than it.
    //
    // A toolkit measures its children, hands their sizes in with the width it has (the width offered
    // when measuring, the width given when arranging), and places each child at its slot.
    public sealed class SimpleWrapLayout
    {
        // ===========[ Setup/Construction/Teardown ]===========================
        private SimpleWrapLayout (double width, double height, Slot[] slots)
        {
            this.Width = width;
            this.Height = height;
            this.Slots = slots;
        }

        // ===========[ Properties ]============================================
        /// <summary>The width the layout wants: its widest line, and no more than a bounded available width</summary>
        public double Width { get; }

        /// <summary>The height the layout wants: every line's height, with the spacing between them</summary>
        public double Height { get; }

        /// <summary>Where each child goes, in the order their sizes were given</summary>
        public IReadOnlyList<Slot> Slots { get; }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Lays out children of the given sizes in <paramref name="availableWidth"/>, which may be unbounded</summary>
        public static SimpleWrapLayout Compute (IReadOnlyList<WrapItemSize> sizes, double availableWidth, double horizontalSpacing, double verticalSpacing)
        {
            bool unbounded = double.IsInfinity(availableWidth) || double.IsNaN(availableWidth);
            var slots = new Slot[sizes.Count];
            double x = 0.0;
            double y = 0.0;
            double lineHeight = 0.0;
            double widest = 0.0;
            for (int index = 0; index < sizes.Count; ++index)
            {
                WrapItemSize size = sizes[index];
                if ((size.Width <= 0.0) && (size.Height <= 0.0))
                {
                    slots[index] = default;
                    continue;
                }

                if (!unbounded && (x > 0.0) && ((x + horizontalSpacing + size.Width) > availableWidth))
                {
                    widest = Math.Max(widest, x);
                    y += lineHeight + verticalSpacing;
                    x = 0.0;
                    lineHeight = 0.0;
                }

                if (x > 0.0)
                {
                    x += horizontalSpacing;
                }

                slots[index] = new Slot(x, y, size.Width, size.Height, true);
                x += size.Width;
                lineHeight = Math.Max(lineHeight, size.Height);
            }

            widest = Math.Max(widest, x);
            return new SimpleWrapLayout(unbounded ? widest : Math.Min(widest, availableWidth), y + lineHeight, slots);
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>
        /// Where one child goes. A child that wants no size at all (zero by zero, a collapsed one) takes no place in the
        /// flow: <see cref="IsPlaced"/> is false and its slot is empty, so it leaves no gap.
        /// </summary>
        public readonly record struct Slot (double X, double Y, double Width, double Height, bool IsPlaced);
    }
}
