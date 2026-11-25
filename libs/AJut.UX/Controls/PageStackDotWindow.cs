namespace AJut.UX.Controls
{
    using System;

    /// <summary>
    /// Which pages a PageStack's dot strip shows a dot for, toolkit free. The strip shows the current page's dot with up to
    /// DotReach dots behind it and DotReach ahead (so a reach of 5 is 11 dots), and a smaller, faded edge dot at each end
    /// of that window while there are more pages past it. Near either end the window slides rather than shrinks, so it
    /// keeps its full count and the current dot sits off center. An edge dot goes to the page just past the window, which
    /// is how a person crosses more pages than the window shows.
    /// </summary>
    /// <param name="CurrentPage">The page the stack shows, or -1 with no pages</param>
    /// <param name="FirstPage">The first page in the window</param>
    /// <param name="LastPage">The last page in the window, one before <paramref name="FirstPage"/> with no pages</param>
    /// <param name="HasBackEdgeDot">Whether there are pages before the window, shown by an edge dot ahead of the first</param>
    /// <param name="HasForwardEdgeDot">Whether there are pages after the window, shown by an edge dot after the last</param>
    public readonly record struct PageStackDotWindow (int CurrentPage, int FirstPage, int LastPage, bool HasBackEdgeDot, bool HasForwardEdgeDot)
    {
        /// <summary>The window for <paramref name="pageCount"/> pages showing <paramref name="currentPage"/></summary>
        /// <param name="pageCount">How many pages the stack holds</param>
        /// <param name="currentPage">The page it shows, kept inside the pages</param>
        /// <param name="dotReach">How many dots show on each side of the current one; below zero counts as zero</param>
        public static PageStackDotWindow For (int pageCount, int currentPage, int dotReach)
        {
            if (pageCount <= 0)
            {
                return new PageStackDotWindow(-1, 0, -1, false, false);
            }

            int current = Math.Max(0, Math.Min(pageCount - 1, currentPage));
            int reach = Math.Max(0, dotReach);

            // Worked in long, so a reach set near int.MaxValue to mean "every page" doesn't overflow
            int windowSize = (int)Math.Min(pageCount, (2L * reach) + 1L);
            int first = Math.Max(0, Math.Min(pageCount - windowSize, current - reach));
            int last = first + windowSize - 1;
            return new PageStackDotWindow(current, first, last, first > 0, last < pageCount - 1);
        }

        /// <summary>How many dots the strip shows: the window's, and its edge dots</summary>
        public int DotCount => (this.LastPage - this.FirstPage + 1) + (this.HasBackEdgeDot ? 1 : 0) + (this.HasForwardEdgeDot ? 1 : 0);

        /// <summary>The dot at <paramref name="dotIndex"/>, counting from the back edge dot when there is one</summary>
        public Dot DotAt (int dotIndex)
        {
            if ((dotIndex < 0) || (dotIndex >= this.DotCount))
            {
                throw new ArgumentOutOfRangeException(nameof(dotIndex), dotIndex, $"The strip has {this.DotCount} dots");
            }

            if (this.HasBackEdgeDot)
            {
                if (dotIndex == 0)
                {
                    return new Dot(this.FirstPage - 1, ePageStackDotKind.BackEdge);
                }

                --dotIndex;
            }

            int page = this.FirstPage + dotIndex;
            if (page > this.LastPage)
            {
                return new Dot(this.LastPage + 1, ePageStackDotKind.ForwardEdge);
            }

            return new Dot(page, page == this.CurrentPage ? ePageStackDotKind.Current : ePageStackDotKind.Page);
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>One dot in the strip</summary>
        /// <param name="PageIndex">The page clicking it goes to: its own page, or for an edge dot, the page just past the window</param>
        /// <param name="Kind">What the dot stands for, which sets how it looks</param>
        public readonly record struct Dot (int PageIndex, ePageStackDotKind Kind)
        {
            /// <summary>Whether it is one of the smaller, faded dots at the ends of the window</summary>
            public bool IsEdge => (this.Kind == ePageStackDotKind.BackEdge) || (this.Kind == ePageStackDotKind.ForwardEdge);
        }
    }
}
