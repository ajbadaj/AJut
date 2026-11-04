namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;

    /// <summary>A point in a docking drag: in screen units for a window's place, or in a view's own units for a pointer in it</summary>
    public readonly record struct DockPoint (double X, double Y);

    /// <summary>Where a new tearoff window goes when a drag tears a panel or zone off</summary>
    /// <param name="Width">The window's width in view units, at least the minimum tearoff width</param>
    /// <param name="Height">The window's height in view units, at least the minimum tearoff height</param>
    /// <param name="CursorOffset">Where the pointer sits within the window, in view units</param>
    /// <param name="ScreenCursorOffset">The same offset in screen units, which the window keeps from the pointer as it follows it</param>
    /// <param name="Origin">The window's top-left corner in screen units: the anchor less the screen cursor offset</param>
    public readonly record struct DockTearoffPlacement (double Width, double Height, DockPoint CursorOffset, DockPoint ScreenCursorOffset, DockPoint Origin);

    /// <summary>What a press on a tab has turned into as it moves</summary>
    public enum eTabDragKind
    {
        /// <summary>Not a drag (yet): the press has not moved far enough, and a release selects the tab</summary>
        None,

        /// <summary>A sideways drag along the strip, which moves the tab to another place in it</summary>
        Reorder,

        /// <summary>A drag up or down out of the strip, which tears the tab's panel off into a tearoff of its own</summary>
        Tearoff,
    }

    /// <summary>What dragging a dock zone's header moves</summary>
    public enum eHeaderDragSubject
    {
        /// <summary>Nothing: the header belongs to a tearoff's root zone, and the tearoff's title bar drags the window</summary>
        None,

        /// <summary>The zone whose header it is, torn off into a tearoff of its own</summary>
        ThisZone,

        /// <summary>The whole tearoff the zone is in, by its root zone, since tearing the zone off would leave the tearoff empty</summary>
        TearoffRoot,
    }

    // ===========[ DockDragRules ]==============================================
    /// <summary>
    /// The rules of a docking drag that never touch a view, shared by every toolkit's docking:
    /// <list type="bullet">
    /// <item>When a press has moved far enough to start a drag, and whether a tab drag reorders or tears off.</item>
    /// <item>Where a tearoff window goes and where the pointer sits in it.</item>
    /// <item>What a header drag moves.</item>
    /// <item>The drop overlay: which insertion widgets a zone shows, and which one is under a point in the overlay's fixed grid.</item>
    /// <item>Tab reordering: which tab is under the pointer, where the insert line goes, and whether the drop commits.</item>
    /// </list>
    /// The toolkit's views read the pointer, draw the overlay and move windows, and ask these.
    /// </summary>
    public static class DockDragRules
    {
        /// <summary>How far, in a view's own units, a press has to move before it starts a drag</summary>
        public const double kDefaultDragThreshold = 8.0;

        /// <summary>The narrowest a new tearoff window is made, however narrow the zone it came from was</summary>
        public const double kMinTearoffWidth = 200.0;

        /// <summary>The shortest a new tearoff window is made, however short the zone it came from was</summary>
        public const double kMinTearoffHeight = 50.0;

        /// <summary>Where the pointer sits down from a new tearoff's top edge when nothing says otherwise: on its title bar</summary>
        public const double kDefaultCursorOffsetY = 15.0;

        /// <summary>The size a single panel's tearoff starts from when its zone has no size to give</summary>
        public static readonly DockZoneSize kFallbackPanelSize = new DockZoneSize(400, 300);

        // The drop overlay is a 3 by 3 grid centered over the zone: Left in the middle row's first
        // column, Top in the middle column's first row, Right and Bottom opposite them, and the
        // center adds to the zone's tabs. The corners hold nothing.

        /// <summary>The width of the drop overlay's left and right columns</summary>
        public const double kOverlayEdgeColumnWidth = 20.0;

        /// <summary>The width of the drop overlay's middle column</summary>
        public const double kOverlayCenterColumnWidth = 44.0;

        /// <summary>The height of the drop overlay's top and bottom rows</summary>
        public const double kOverlayEdgeRowHeight = 19.0;

        /// <summary>The height of the drop overlay's middle row</summary>
        public const double kOverlayCenterRowHeight = 22.0;

        /// <summary>The drop overlay's whole width</summary>
        public const double kOverlayWidth = kOverlayEdgeColumnWidth + kOverlayCenterColumnWidth + kOverlayEdgeColumnWidth;

        /// <summary>The drop overlay's whole height</summary>
        public const double kOverlayHeight = kOverlayEdgeRowHeight + kOverlayCenterRowHeight + kOverlayEdgeRowHeight;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Whether a press has moved past <paramref name="threshold"/>, strictly: a header or title bar drag starts on this</summary>
        public static bool HasMovedPastThreshold (double dx, double dy, double threshold)
        {
            return ((dx * dx) + (dy * dy)) > (threshold * threshold);
        }

        /// <summary>Whether a press has moved at least <paramref name="threshold"/>: a tab drag starts on this, counting the threshold itself</summary>
        public static bool HasReachedThreshold (double dx, double dy, double threshold)
        {
            return ((dx * dx) + (dy * dy)) >= (threshold * threshold);
        }

        /// <summary>
        /// What a press on <paramref name="tab"/> that has moved by (<paramref name="dx"/>, <paramref name="dy"/>) has turned
        /// into. Short of <paramref name="threshold"/> it is nothing yet. Past it, mostly sideways reorders, and mostly up or
        /// down (or exactly diagonal) tears the tab off, or is still nothing when the tab's panel can't tear off. A toolkit
        /// decides once, on the first answer that is not None, and keeps it for the rest of the drag.
        /// </summary>
        /// <param name="tab">The docked panel the tab stands for</param>
        /// <param name="dx">How far the press has moved sideways, in view units</param>
        /// <param name="dy">How far the press has moved up or down, in view units</param>
        /// <param name="threshold">How far, in view units, a press has to move before it is a drag</param>
        public static eTabDragKind DecideTabDrag (DockingContentAdapterModel tab, double dx, double dy, double threshold)
        {
            if (!HasReachedThreshold(dx, dy, threshold))
            {
                return eTabDragKind.None;
            }

            if (Math.Abs(dx) > Math.Abs(dy))
            {
                return eTabDragKind.Reorder;
            }

            // A panel that can't tear off stays in the strip: the press is still nothing, so a later sideways move can
            // still reorder, and a release selects the tab
            return tab.CanTearoff ? eTabDragKind.Tearoff : eTabDragKind.None;
        }

        /// <summary>
        /// Places a new tearoff for a zone of <paramref name="zoneSize"/>, so that <paramref name="cursorOffset"/> within it
        /// lands on <paramref name="anchor"/>. The window is at least the minimum tearoff size, in whole units. The offset is
        /// kept inside the window, and without one the pointer sits centered on the window's title bar.
        /// </summary>
        /// <param name="zoneSize">The size of the zone or panel being torn off, in view units</param>
        /// <param name="anchor">Where the pointer is (or was pressed), in screen units</param>
        /// <param name="cursorOffset">Where the pointer should sit within the new window, in view units, or null for the default</param>
        /// <param name="displayScale">Screen units per view unit: 1 where the two are the same, the display scale where screen units are pixels</param>
        public static DockTearoffPlacement PlaceTearoff (DockZoneSize zoneSize, DockPoint anchor, DockPoint? cursorOffset, double displayScale)
        {
            double width = (int)Math.Max(zoneSize.Width, kMinTearoffWidth);
            double height = (int)Math.Max(zoneSize.Height, kMinTearoffHeight);

            double offsetX = cursorOffset.HasValue ? Math.Clamp(cursorOffset.Value.X, 0, width) : width / 2.0;
            double offsetY = cursorOffset.HasValue ? Math.Clamp(cursorOffset.Value.Y, 0, height) : kDefaultCursorOffsetY;

            // The offset is measured in the view, but the window follows a pointer measured on the screen, so it is kept
            // from the pointer in screen units
            var screenOffset = new DockPoint(offsetX * displayScale, offsetY * displayScale);
            return new DockTearoffPlacement(
                width,
                height,
                new DockPoint(offsetX, offsetY),
                screenOffset,
                new DockPoint(anchor.X - screenOffset.X, anchor.Y - screenOffset.Y)
            );
        }

        /// <summary>
        /// What dragging <paramref name="zone"/>'s header moves. In the root window, the zone itself. In a tearoff, nothing for
        /// the tearoff's root zone (its title bar drags the window), the zone itself when the root is a split (the rest keeps
        /// the tearoff from emptying), and otherwise the whole tearoff by its root zone. Tearing the zone itself off needs
        /// every panel in it to allow tearing off; when one doesn't, the header drags nothing.
        /// </summary>
        /// <param name="zone">The zone whose header is being dragged</param>
        /// <param name="isInTearoff">True when the zone is in a tearoff window, false when it is in the root window</param>
        /// <param name="tearoffRootZone">The root zone of the tearoff the zone is in, or null outside a tearoff</param>
        public static eHeaderDragSubject ChooseHeaderDragSubject (DockZoneViewModel zone, bool isInTearoff, DockZoneViewModel? tearoffRootZone)
        {
            if (isInTearoff)
            {
                if (tearoffRootZone == zone)
                {
                    return eHeaderDragSubject.None;
                }

                // Moving a tearoff that already exists tears nothing off, so what its panels allow doesn't matter
                bool isTearoffRootASplit = tearoffRootZone?.Orientation.IsFlagInGroup(eDockOrientation.AnySplitOrientation) == true;
                if (!isTearoffRootASplit)
                {
                    return eHeaderDragSubject.TearoffRoot;
                }
            }

            return CanTearOffEverythingIn(zone) ? eHeaderDragSubject.ThisZone : eHeaderDragSubject.None;
        }

        /// <summary>Whether the drop overlay shows its four split widgets on a zone of <paramref name="orientation"/>: not on an empty zone, which can only be filled</summary>
        public static bool ShowsSplitWidgets (eDockOrientation orientation) => orientation != eDockOrientation.Empty;

        /// <summary>
        /// Whether the drop overlay's center (add to the tabs) is enabled on a zone of <paramref name="orientation"/>: on an
        /// empty zone or a leaf, not on a split, which holds child zones rather than panels. It shows either way.
        /// </summary>
        public static bool IsCenterWidgetEnabled (eDockOrientation orientation)
        {
            return !ShowsSplitWidgets(orientation)
                || (orientation == eDockOrientation.Single)
                || (orientation == eDockOrientation.Tabbed);
        }

        /// <summary>
        /// The insertion widget at a point in the drop overlay's own coordinates (top left at 0,0), or null for a corner or a
        /// point outside the overlay. This is the grid alone: over a split the center comes back too, and is not a drop
        /// unless <see cref="IsCenterWidgetEnabled"/> says so, and over an empty zone only the center is shown.
        /// </summary>
        public static eDockInsertionDirection? FindOverlayDirectionAt (double x, double y)
        {
            int column = OverlayCell(x, kOverlayEdgeColumnWidth, kOverlayCenterColumnWidth);
            int row = OverlayCell(y, kOverlayEdgeRowHeight, kOverlayCenterRowHeight);
            const int kColumnsPerRow = 3;
            switch ((row * kColumnsPerRow) + column)
            {
                case 1: return eDockInsertionDirection.Top;
                case 3: return eDockInsertionDirection.Left;
                case 4: return eDockInsertionDirection.AddToTabbedDisplay;
                case 5: return eDockInsertionDirection.Right;
                case 7: return eDockInsertionDirection.Bottom;
                default: return null;
            }

            // Which of the three cells along one axis a position falls in, or a value no case matches when outside
            static int OverlayCell (double position, double edgeSize, double centerSize)
            {
                const int kOutside = 100;
                if (position < 0)
                {
                    return kOutside;
                }

                if (position < edgeSize)
                {
                    return 0;
                }

                if (position < edgeSize + centerSize)
                {
                    return 1;
                }

                return position < edgeSize + centerSize + edgeSize ? 2 : kOutside;
            }
        }

        /// <summary>
        /// The tab under <paramref name="x"/> in a strip of tabs of <paramref name="tabWidths"/> laid end to end from 0. Past the
        /// last tab counts as the last tab, and an empty strip gives 0.
        /// </summary>
        public static int FindTabIndexAtX (IReadOnlyList<double> tabWidths, double x)
        {
            double accumulated = 0;
            for (int index = 0; index < tabWidths.Count; ++index)
            {
                accumulated += tabWidths[index];
                if (x < accumulated)
                {
                    return index;
                }
            }

            return Math.Max(0, tabWidths.Count - 1);
        }

        /// <summary>
        /// Where the insert line goes while dragging tab <paramref name="sourceIndex"/> over tab <paramref name="targetIndex"/>:
        /// the target's leading edge, or its trailing edge when it is after the source. Null when over the tab's own place,
        /// where nothing moves.
        /// </summary>
        public static double? FindInsertLineX (IReadOnlyList<double> tabWidths, int sourceIndex, int targetIndex)
        {
            if (targetIndex == sourceIndex)
            {
                return null;
            }

            double lineX = 0;
            for (int index = 0; index < tabWidths.Count; ++index)
            {
                if (index == targetIndex)
                {
                    if (targetIndex > sourceIndex)
                    {
                        lineX += tabWidths[index];
                    }

                    break;
                }

                lineX += tabWidths[index];
            }

            return lineX;
        }

        /// <summary>Whether a tab reorder drop moves anything: the target is a real tab other than the source</summary>
        public static bool ShouldCommitTabReorder (int sourceIndex, int targetIndex, int tabCount)
        {
            return (targetIndex >= 0)
                && (targetIndex != sourceIndex)
                && (targetIndex < tabCount);
        }

        // ===========[ Helper Methods ]========================================
        // Whether every panel in the zone, and in the zones under it, can tear off
        private static bool CanTearOffEverythingIn (DockZoneViewModel zone)
        {
            foreach (DockingContentAdapterModel panel in zone.DockedContent)
            {
                if (!panel.CanTearoff)
                {
                    return false;
                }
            }

            foreach (DockZoneViewModel child in zone.Children)
            {
                if (!CanTearOffEverythingIn(child))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
