namespace AJut.UX.Controls
{
    using System;

    /// <summary>
    /// How a PageStack picks, lays out and moves between its pages, toolkit free, so WinUI3's, WPF's and AppKit's stacks
    /// behave the same. One page shows at a time, inset from both ends by the page buttons and a peek of each neighbor, and
    /// every other page sits along the stack beyond it, faded. Lengths are measured along the stack: widths for a
    /// horizontal stack, heights for a vertical one. A position is a page index that can sit between two pages while a
    /// transition runs.
    /// </summary>
    public static class PageStackRules
    {
        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// The page a stack shows for the selected index it was given: that index kept inside the pages, or -1 with none.
        /// The given index is left as it is, so an index set before the pages are added (as XAML does) still lands once
        /// they are.
        /// </summary>
        public static int EffectiveSelectedIndex (int selectedIndex, int pageCount)
        {
            return pageCount <= 0 ? -1 : Math.Max(0, Math.Min(pageCount - 1, selectedIndex));
        }

        /// <summary>Whether the back page button is enabled: the stack is not on its first page</summary>
        public static bool CanGoBack (int effectiveSelectedIndex) => effectiveSelectedIndex > 0;

        /// <summary>Whether the forward page button is enabled: the stack is not on its last page</summary>
        public static bool CanGoForward (int effectiveSelectedIndex, int pageCount)
        {
            return (effectiveSelectedIndex >= 0) && (effectiveSelectedIndex < (pageCount - 1));
        }

        /// <summary>Whether the page buttons show at all: there is another page to go to</summary>
        public static bool ArePageButtonsShown (int pageCount) => pageCount > 1;

        /// <summary>Whether the dot strip shows: it is asked for, and there is more than one page to tell apart</summary>
        public static bool AreDotsShown (bool showDots, int pageCount) => showDots && (pageCount > 1);

        /// <summary>
        /// The selected index after pages are inserted, so the stack keeps showing the page it showed. An index that named
        /// no page before (the stack still being filled) is left as it is.
        /// </summary>
        public static int SelectedIndexAfterInsert (int selectedIndex, int oldPageCount, int insertedAt, int insertedCount)
        {
            if ((selectedIndex < 0) || (selectedIndex >= oldPageCount))
            {
                return selectedIndex;
            }

            return insertedAt <= selectedIndex ? selectedIndex + insertedCount : selectedIndex;
        }

        /// <summary>
        /// The selected index after pages are removed, so the stack keeps showing the page it showed. When that page is one
        /// of those removed, the stack goes to the page that took its place, or to the new last page. An index that named no
        /// page before is left as it is.
        /// </summary>
        public static int SelectedIndexAfterRemoval (int selectedIndex, int oldPageCount, int removedAt, int removedCount)
        {
            if ((selectedIndex < 0) || (selectedIndex >= oldPageCount))
            {
                return selectedIndex;
            }

            if (selectedIndex < removedAt)
            {
                return selectedIndex;
            }

            if (selectedIndex >= (removedAt + removedCount))
            {
                return selectedIndex - removedCount;
            }

            int newPageCount = oldPageCount - removedCount;
            return Math.Max(0, Math.Min(removedAt, newPageCount - 1));
        }

        /// <summary>
        /// The space kept at each end of the stack beyond the shown page: a page button's length, the peek of the neighbor
        /// page past it, and the gap between pages. With no page buttons there is no neighbor to peek at, so the page takes
        /// the whole stack.
        /// </summary>
        /// <param name="arePageButtonsShown">Whether the page buttons show (<see cref="ArePageButtonsShown"/>)</param>
        /// <param name="buttonLength">A page button's length along the stack</param>
        /// <param name="pagePeek">How much of each neighbor page shows between its page button and the shown page</param>
        /// <param name="pageSpacing">The gap between one page and the next</param>
        public static double PageClearance (bool arePageButtonsShown, double buttonLength, double pagePeek, double pageSpacing)
        {
            if (!arePageButtonsShown)
            {
                return 0.0;
            }

            return Math.Max(0.0, buttonLength) + Math.Max(0.0, pagePeek) + Math.Max(0.0, pageSpacing);
        }

        /// <summary>
        /// How far along the stack <paramref name="pageIndex"/> sits from where the shown page sits, for the stack at
        /// <paramref name="position"/>: zero for the page at the position, a page length and a gap per page away
        /// </summary>
        public static double PageOffset (int pageIndex, double position, double pageLength, double pageSpacing)
        {
            return (pageIndex - position) * (pageLength + Math.Max(0.0, pageSpacing));
        }

        /// <summary>
        /// How opaque <paramref name="pageIndex"/> draws for the stack at <paramref name="position"/>: fully at the position,
        /// fading to <paramref name="inactivePageOpacity"/> a page away and staying there beyond, so a page fades in as a
        /// transition brings it to the middle
        /// </summary>
        public static double PageOpacity (int pageIndex, double position, double inactivePageOpacity)
        {
            double inactive = Math.Max(0.0, Math.Min(1.0, inactivePageOpacity));
            double distance = Math.Min(1.0, Math.Abs(pageIndex - position));
            return 1.0 - (distance * (1.0 - inactive));
        }

        /// <summary>
        /// The eased share of a transition done, for the share of its time gone by: a cubic ease out, quick to leave and
        /// gentle to land, kept inside zero to one
        /// </summary>
        public static double EaseOut (double timeProgress)
        {
            double t = Math.Max(0.0, Math.Min(1.0, timeProgress));
            double remaining = 1.0 - t;
            return 1.0 - (remaining * remaining * remaining);
        }

        /// <summary>Whether a transition of <paramref name="duration"/> is done once <paramref name="elapsed"/> has gone by</summary>
        public static bool IsTransitionDone (TimeSpan elapsed, TimeSpan duration) => (duration <= TimeSpan.Zero) || (elapsed >= duration);

        /// <summary>
        /// Where a transition from <paramref name="fromPosition"/> to <paramref name="toPosition"/> has the stack once
        /// <paramref name="elapsed"/> of its <paramref name="duration"/> has gone by. A transition started while another
        /// runs starts from wherever that one had got to, so the pages never jump. No duration is no animation.
        /// </summary>
        public static double TransitionPosition (double fromPosition, double toPosition, TimeSpan elapsed, TimeSpan duration)
        {
            if (IsTransitionDone(elapsed, duration))
            {
                return toPosition;
            }

            double timeProgress = elapsed.TotalMilliseconds / duration.TotalMilliseconds;
            return fromPosition + ((toPosition - fromPosition) * EaseOut(timeProgress));
        }
    }
}
