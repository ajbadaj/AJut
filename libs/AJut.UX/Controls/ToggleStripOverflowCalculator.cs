namespace AJut.UX.Controls
{
    using System.Collections.Generic;

    // ===========[ ToggleStripOverflowCalculator ]==============================
    // Pure layout math shared by the WPF and WinUI3 ToggleStrip controls: given each item's
    // width, which items are selected, the available width, and the room to reserve for the
    // overflow button, decide which items stay in the strip and which move to the overflow popup.
    //
    // Rules (this is the "leading-window" behavior - items never reorder):
    //  1. If every item fits in the available width, nothing overflows and the reserved button
    //     space is irrelevant (we do not pay for a button we are not going to show).
    //  2. Otherwise selected items get first claim on the budget (in positional order), then the
    //     leftover budget is filled with unselected items (also in positional order). Selection
    //     only changes which items make the cut - the visible items keep their original order.
    //  3. The strip is never left completely empty when at least one item exists.

    public static class ToggleStripOverflowCalculator
    {
        public static ToggleStripOverflowResult Compute (IReadOnlyList<double> itemWidths, IReadOnlyList<bool> isSelected, double availableWidth, double reservedOverflowWidth)
        {
            int count = itemWidths?.Count ?? 0;
            var visible = new List<int>(count);
            var overflow = new List<int>(count);

            // 1. Everything fits (or there is nothing to place): no overflow.
            double totalWidth = 0.0;
            for (int i = 0; i < count; ++i)
            {
                totalWidth += itemWidths[i];
            }

            if (count == 0 || totalWidth <= availableWidth)
            {
                for (int i = 0; i < count; ++i)
                {
                    visible.Add(i);
                }

                return new ToggleStripOverflowResult(visible, overflow);
            }

            // 2. Overflow is happening - leave room for the overflow button.
            double budget = availableWidth - reservedOverflowWidth;
            var keep = new bool[count];
            double used = 0.0;

            // 2a. Selected items get first claim (positional order).
            for (int i = 0; i < count; ++i)
            {
                if (IsItemSelected(isSelected, i)
                    && (used + itemWidths[i] <= budget))
                {
                    keep[i] = true;
                    used += itemWidths[i];
                }
            }

            // 2b. Fill the rest of the budget with unselected items (positional order).
            for (int i = 0; i < count; ++i)
            {
                if (keep[i] || IsItemSelected(isSelected, i))
                {
                    continue;
                }

                if (used + itemWidths[i] <= budget)
                {
                    keep[i] = true;
                    used += itemWidths[i];
                }
            }

            // 2c. Never show an empty strip - fall back to the narrowest single item.
            if (used == 0.0)
            {
                int narrowest = 0;
                for (int i = 1; i < count; ++i)
                {
                    if (itemWidths[i] < itemWidths[narrowest])
                    {
                        narrowest = i;
                    }
                }

                keep[narrowest] = true;
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
