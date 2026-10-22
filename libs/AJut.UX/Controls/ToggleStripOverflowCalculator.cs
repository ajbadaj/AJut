namespace AJut.UX.Controls
{
    using System.Collections.Generic;

    // ===========[ ToggleStripOverflowCalculator ]==============================
    // Pure layout math shared by every stack's ToggleStrip: given each item's length along the strip,
    // which items are selected, the length available, and the room to reserve for the overflow button,
    // decide which items stay in the strip and which move to the overflow popup.
    //
    // The math is one dimensional, so it serves a strip of either orientation: a horizontal strip passes
    // widths, a vertical one passes heights. Nothing here knows or cares which.
    //
    // Rules (this is the "leading-window" behavior - items never reorder):
    //  1. If every item fits in the available length, nothing overflows and the reserved button
    //     space is irrelevant (no room is paid for a button that is not going to show).
    //  2. Otherwise the item just picked (the strip's SelectedItem) gets first claim on the budget, so
    //     an item picked from the overflow popup comes into the strip even when other items were
    //     selected before it. Then the other selected items (in positional order), then the leftover
    //     budget is filled with unselected items (also in positional order). Selection only changes
    //     which items make the cut - the visible items keep their original order.
    //  3. The strip is never left completely empty when at least one item exists: the item just
    //     picked shows if there is one, otherwise the shortest.

    public static class ToggleStripOverflowCalculator
    {
        /// <summary>
        /// Splits a strip's items between the strip and its overflow popup. Every length is measured along the strip:
        /// widths for a horizontal strip, heights for a vertical one.
        /// </summary>
        /// <param name="itemLengths">Each item's length along the strip, in strip order</param>
        /// <param name="isSelected">Whether each item is selected, in strip order; selected items are kept first</param>
        /// <param name="availableLength">The strip's length</param>
        /// <param name="reservedOverflowLength">The overflow button's length, reserved only when something overflows</param>
        public static ToggleStripOverflowResult Compute (IReadOnlyList<double> itemLengths, IReadOnlyList<bool> isSelected, double availableLength, double reservedOverflowLength)
        {
            return Compute(itemLengths, isSelected, availableLength, reservedOverflowLength, -1);
        }

        /// <summary>
        /// Splits a strip's items between the strip and its overflow popup, with the item just picked claiming the strip
        /// first. Every length is measured along the strip: widths for a horizontal strip, heights for a vertical one.
        /// </summary>
        /// <param name="itemLengths">Each item's length along the strip, in strip order</param>
        /// <param name="isSelected">Whether each item is selected, in strip order; selected items are kept first</param>
        /// <param name="availableLength">The strip's length</param>
        /// <param name="reservedOverflowLength">The overflow button's length, reserved only when something overflows</param>
        /// <param name="pickedIndex">
        /// The item just picked (the strip's SelectedItem), kept ahead of every other item; -1, or an item that is not
        /// selected, for none
        /// </param>
        public static ToggleStripOverflowResult Compute (IReadOnlyList<double> itemLengths, IReadOnlyList<bool> isSelected, double availableLength, double reservedOverflowLength, int pickedIndex)
        {
            int count = itemLengths?.Count ?? 0;
            var visible = new List<int>(count);
            var overflow = new List<int>(count);

            // 1. Everything fits (or there is nothing to place): no overflow.
            double totalLength = 0.0;
            for (int i = 0; i < count; ++i)
            {
                totalLength += itemLengths[i];
            }

            if (count == 0 || totalLength <= availableLength)
            {
                for (int i = 0; i < count; ++i)
                {
                    visible.Add(i);
                }

                return new ToggleStripOverflowResult(visible, overflow);
            }

            // 2. Overflow is happening - leave room for the overflow button.
            double budget = availableLength - reservedOverflowLength;
            var keep = new bool[count];
            double used = 0.0;

            // 2a. The item just picked claims first, then the other selected items (positional order).
            bool hasPicked = (pickedIndex >= 0) && (pickedIndex < count) && IsItemSelected(isSelected, pickedIndex);
            if (hasPicked && (itemLengths[pickedIndex] <= budget))
            {
                keep[pickedIndex] = true;
                used += itemLengths[pickedIndex];
            }

            for (int i = 0; i < count; ++i)
            {
                if (!keep[i]
                    && IsItemSelected(isSelected, i)
                    && (used + itemLengths[i] <= budget))
                {
                    keep[i] = true;
                    used += itemLengths[i];
                }
            }

            // 2b. Fill the rest of the budget with unselected items (positional order).
            for (int i = 0; i < count; ++i)
            {
                if (keep[i] || IsItemSelected(isSelected, i))
                {
                    continue;
                }

                if (used + itemLengths[i] <= budget)
                {
                    keep[i] = true;
                    used += itemLengths[i];
                }
            }

            // 2c. Never show an empty strip - fall back to the item just picked, or else the shortest single item.
            if (used == 0.0 && hasPicked)
            {
                keep[pickedIndex] = true;
            }
            else if (used == 0.0)
            {
                int shortest = 0;
                for (int i = 1; i < count; ++i)
                {
                    if (itemLengths[i] < itemLengths[shortest])
                    {
                        shortest = i;
                    }
                }

                keep[shortest] = true;
            }

            // 3. Partition, preserving positional order in both lists.
            for (int i = 0; i < count; ++i)
            {
                if (keep[i])
                {
                    visible.Add(i);
                }
                else
                {
                    overflow.Add(i);
                }
            }

            return new ToggleStripOverflowResult(visible, overflow);
        }

        private static bool IsItemSelected (IReadOnlyList<bool> isSelected, int index)
            => isSelected != null && index < isSelected.Count && isSelected[index];
    }

    public readonly struct ToggleStripOverflowResult
    {
        public ToggleStripOverflowResult (IReadOnlyList<int> visibleIndices, IReadOnlyList<int> overflowIndices)
        {
            this.VisibleIndices = visibleIndices;
            this.OverflowIndices = overflowIndices;
        }

        public IReadOnlyList<int> VisibleIndices { get; }
        public IReadOnlyList<int> OverflowIndices { get; }
        public bool HasOverflow => this.OverflowIndices.Count > 0;
    }
}
