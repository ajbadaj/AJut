namespace AJut.UX.Controls
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// How a PageStack picks, lays out and moves between its pages, toolkit free, so WinUI3's, WPF's and AppKit's stacks
    /// behave the same. One page shows at a time, inset from both ends by the page buttons and a peek of each neighbor, and
    /// every other page sits along the stack beyond it, faded. Lengths are measured along the stack: widths for a
    /// horizontal stack, heights for a vertical one. A position is a page index that can sit between two pages while a
    /// transition runs.
    /// </summary>
    public static class PageStackRules
    {
        /// <summary>The page count's format when none is given: the shown page's number from one, and the page count</summary>
        public const string kDefaultPageCountFormat = "{0} of {1}";

        // A format placeholder ("{0}", "{1:N0}"), but not an escaped brace ("{{0}}")
        private static readonly Regex kPageCountPlaceholder = new Regex(@"(?<!\{)\{\d+[^{}]*\}(?!\})", RegexOptions.Compiled);

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

        /// <summary>Whether the page count ("3 of 7") shows: it is asked for, and there is more than one page to count</summary>
        public static bool IsPageCountShown (bool showPageCount, int pageCount) => showPageCount && (pageCount > 1);

        /// <summary>
        /// Whether anything shows past the pages, the dots or the page count. With nothing there, an unset page padding pads
        /// the far side of the pages too, since no strip sits there to space them.
        /// </summary>
        public static bool IsDotStripShown (bool showDots, bool showPageCount, int pageCount)
        {
            return AreDotsShown(showDots, pageCount) || IsPageCountShown(showPageCount, pageCount);
        }

        /// <summary>
        /// The page count's text in one line, as a screen reader reads it: <paramref name="format"/> filled with the shown
        /// page's number from one ({0}) and the page count ({1}), so "{0} of {1}" reads "3 of 7"
        /// </summary>
        public static string PageCountText (string? format, int effectiveSelectedIndex, int pageCount)
        {
            return String.Format(CultureInfo.CurrentCulture, format ?? kDefaultPageCountFormat, effectiveSelectedIndex + 1, pageCount);
        }

        /// <summary>
        /// The page count cut into the counts and the format's own words around them ("of" in "{0} of {1}"), so a stack can
        /// draw the words smaller and fainter than the numbers. The first two placeholders in the format are the counts; any
        /// past those stay in the trailing words. A vertical stack's count sits in a strip one dot wide, so there each word
        /// takes a line of its own. A horizontal one keeps the format's spacing, carried at the front of the part after it
        /// (" of", " 7"), since a text's leading spaces always take room and its trailing ones may not.
        /// </summary>
        public static PageCountParts SplitPageCount (string? format, int effectiveSelectedIndex, int pageCount, bool isVertical)
        {
            string pattern = format ?? kDefaultPageCountFormat;

            // 1. Cut the format at its first two placeholders
            string[] pieces = { pattern, String.Empty, String.Empty, String.Empty, String.Empty };
            MatchCollection placeholders = kPageCountPlaceholder.Matches(pattern);
            if (placeholders.Count > 0)
            {
                Match first = placeholders[0];
                int afterFirst = first.Index + first.Length;
                pieces[0] = pattern.Substring(0, first.Index);
                pieces[1] = first.Value;
                if (placeholders.Count == 1)
                {
                    pieces[4] = pattern.Substring(afterFirst);
                }
                else
                {
                    Match second = placeholders[1];
                    pieces[2] = pattern.Substring(afterFirst, second.Index - afterFirst);
                    pieces[3] = second.Value;
                    pieces[4] = pattern.Substring(second.Index + second.Length);
                }
            }

            // 2. Fill each piece, then lay its words out for the orientation
            for (int i = 0; i < pieces.Length; ++i)
            {
                pieces[i] = String.Format(CultureInfo.CurrentCulture, pieces[i], effectiveSelectedIndex + 1, pageCount);
            }

            if (isVertical)
            {
                for (int i = 0; i < pieces.Length; ++i)
                {
                    pieces[i] = String.Join("\n", pieces[i].Split(' ', StringSplitOptions.RemoveEmptyEntries));
                }
            }
            else
            {
                // Move each piece's trailing spaces to the front of the next piece that has text
                string carried = String.Empty;
                for (int i = 0; i < pieces.Length; ++i)
                {
                    string withCarried = carried + pieces[i];
                    string trimmed = withCarried.TrimEnd(' ');
                    carried = withCarried.Substring(trimmed.Length);
                    pieces[i] = trimmed;
                }
            }

            return new PageCountParts(pieces[0], pieces[1], pieces[2], pieces[3], pieces[4]);
        }

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

        /// <summary>
        /// Whether a dot strip slides to its new place when the page changes, or takes it at once. The strip slides while
        /// the strip it was and the strip it becomes share a dot, so the move reads as the dots shifting by however many
        /// dots the pick was away. A jump further than that (Home, End, or a far SelectedIndex) shares nothing to follow,
        /// and sliding a run of unrelated dots past would only blur.
        /// </summary>
        /// <param name="fromFirstDotPage">The page the strip's first dot stands for now, between two pages mid slide</param>
        /// <param name="toFirstDotPage">The page the new strip's first dot stands for (<see cref="PageStackDotWindow.FirstDotPage"/>)</param>
        /// <param name="toDotCount">How many dots the new strip shows</param>
        public static bool ShouldSlideDots (double fromFirstDotPage, int toFirstDotPage, int toDotCount)
        {
            return Math.Abs(toFirstDotPage - fromFirstDotPage) < toDotCount;
        }

        /// <summary>
        /// The pages a sliding dot strip holds dots for: the pages of the strip it was and of the strip it becomes, so the
        /// dots leaving slide out of view rather than vanish, and the ones arriving slide in. Kept inside the pages.
        /// </summary>
        /// <param name="fromFirstDotPage">The page the strip's first dot stands for now, between two pages mid slide</param>
        /// <param name="toFirstDotPage">The page the new strip's first dot stands for</param>
        /// <param name="toDotCount">How many dots the new strip shows</param>
        /// <param name="pageCount">How many pages the stack holds</param>
        public static DotSpan DotSlideSpan (double fromFirstDotPage, int toFirstDotPage, int toDotCount, int pageCount)
        {
            int first = (int)Math.Floor(Math.Min(fromFirstDotPage, toFirstDotPage));
            int last = (int)Math.Ceiling(Math.Max(fromFirstDotPage, toFirstDotPage)) + toDotCount - 1;
            return new DotSpan(Math.Max(0, first), Math.Min(pageCount - 1, last));
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>A run of pages with a dot each, from <paramref name="FirstPage"/> to <paramref name="LastPage"/></summary>
        public readonly record struct DotSpan (int FirstPage, int LastPage)
        {
            /// <summary>How many pages, and so dots, the run holds</summary>
            public int Count => Math.Max(0, this.LastPage - this.FirstPage + 1);
        }

        /// <summary>
        /// The page count cut into its counts and the format's own words around them (<see cref="SplitPageCount"/>). Any part
        /// can be empty: "{0} of {1}" has no leading or trailing words.
        /// </summary>
        public readonly record struct PageCountParts (string LeadingWords, string FirstCount, string MiddleWords, string SecondCount, string TrailingWords);
    }
}
