namespace AJut.UX.Docking
{
    using System.Collections.Generic;
    using System.Linq;

    // ===========[ DockDragOrigin ]=============================================
    /// <summary>
    /// Where a dragged panel or zone came from, captured just before a drag tears it off, so a canceled drag can put it
    /// back: in the same zone at the same tab position with the same tab selected, or in the same place in the same split,
    /// at the split's old sizes. A toolkit captures one with <see cref="ForPanel"/> or <see cref="ForZone"/> before tearing
    /// off, and on cancel asks <see cref="TryFindReturn"/> where the tearoff's root zone goes, drops it there the way a
    /// drag would (which closes the tearoff), then calls <see cref="RestoreAfterReturn"/>.
    /// </summary>
    /// <remarks>
    /// Tearing off changes the tree it leaves. A panel leaving a zone that holds others leaves the zone as it was, so it
    /// goes back into that zone's tabs. A panel leaving a zone it was alone in, or a zone leaving a split, removes that zone
    /// from its split, and a split left with one child collapses into it (a split with no parent takes the child's place by
    /// copy). So the way back is worked out afterward from the neighbor the zone sat beside: still in the tree, it is split
    /// again on the side the zone was on; copied into a root, the root is. A root zone torn off whole is left empty in its
    /// slot, and simply filled again.
    /// </remarks>
    public sealed class DockDragOrigin
    {
        private readonly DockZoneViewModel m_zone;
        private readonly DockingContentAdapterModel? m_panel;
        private readonly int m_tabIndex;
        private readonly int m_selectedIndex;
        private readonly bool m_wasRoot;
        private readonly DockZoneViewModel? m_parent;
        private readonly eDockOrientation m_parentOrientation;
        private readonly DockZoneViewModel? m_neighbor;
        private readonly bool m_isBeforeNeighbor;
        private readonly List<double>? m_splitSizes;

        // ===========[ Setup/Construction/Teardown ]===========================
        private DockDragOrigin (DockZoneViewModel zone, DockingContentAdapterModel? panel)
        {
            m_zone = zone;
            m_panel = panel;
            m_tabIndex = panel != null ? zone.DockedContent.IndexOf(panel) : -1;
            m_selectedIndex = zone.SelectedIndex;
            m_wasRoot = zone.Parent == null;
            m_parent = zone.Parent;

            if (m_parent != null)
            {
                m_parentOrientation = m_parent.Orientation;
                int index = m_parent.Children.IndexOf(zone);

                // The zone goes back before the sibling after it, or after the one before it when it was last
                m_isBeforeNeighbor = index + 1 < m_parent.Children.Count;
                m_neighbor = m_isBeforeNeighbor ? m_parent.Children[index + 1] : m_parent.Children.ElementAtOrDefault(index - 1);
                m_splitSizes = CaptureChildSizes(m_parent);
            }
            else
            {
                m_splitSizes = CaptureChildSizes(zone);
            }
        }

        /// <summary>Captures where <paramref name="panel"/> is, before a drag tears it off on its own</summary>
        public static DockDragOrigin ForPanel (DockingContentAdapterModel panel) => new DockDragOrigin(panel.Location, panel);

        /// <summary>Captures where <paramref name="zone"/> is, before a drag tears the whole zone off</summary>
        public static DockDragOrigin ForZone (DockZoneViewModel zone) => new DockDragOrigin(zone, null);

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Where the root zone of the tearoff the drag made goes to put things back: <paramref name="target"/> takes it in
        /// <paramref name="direction"/>, the way a drop would. False when the place it came from is gone, and the tearoff
        /// stays where it is.
        /// </summary>
        public bool TryFindReturn (out DockZoneViewModel target, out eDockInsertionDirection direction)
        {
            target = m_zone;
            direction = eDockInsertionDirection.AddToTabbedDisplay;

            // 1. A root zone is never removed: whatever left it, it takes back
            if (m_wasRoot)
            {
                return m_zone.Manager != null;
            }

            // 2. A panel whose zone is still in its split goes back into its tabs
            if (m_panel != null && IsInTree(m_zone))
            {
                return true;
            }

            // 3. Otherwise the zone is split back in beside the neighbor it sat next to, or into the root that neighbor
            //    was copied into
            if (m_neighbor == null || m_parent == null)
            {
                return false;
            }

            if (IsInTree(m_neighbor))
            {
                target = m_neighbor;
            }
            else if (m_parent.Parent == null && m_parent.Manager != null)
            {
                target = m_parent;
            }
            else
            {
                return false;
            }

            direction = SideOf(m_parentOrientation, m_isBeforeNeighbor);
            return true;
        }

        /// <summary>
        /// Once the tearoff's root zone has been dropped where <see cref="TryFindReturn"/> said, puts the panel back at its
        /// tab position, selects what was selected, and sizes the split it went back into the way it was.
        /// </summary>
        /// <param name="returnedRoot">The tearoff's root zone that was dropped back</param>
        public void RestoreAfterReturn (DockZoneViewModel returnedRoot)
        {
            // 1. Back in its zone's tabs: a tab drop puts it last and selects it
            if ((m_panel != null)
                && (m_panel.Location == m_zone)
                && (m_tabIndex >= 0))
            {
                int currentIndex = m_zone.DockedContent.IndexOf(m_panel);
                if ((currentIndex != m_tabIndex) && (m_tabIndex < m_zone.DockedContent.Count))
                {
                    m_zone.SwapDockedContentOrder(currentIndex, m_tabIndex);
                }

                if (m_selectedIndex < m_zone.DockedContent.Count)
                {
                    m_zone.SelectedIndex = m_selectedIndex;
                }

                return;
            }

            // 2. Split back in: the split it is now a child of takes the old sizes. A root that was filled again takes the
            //    sizes its own children had.
            DockZoneViewModel? split = m_wasRoot ? m_zone : returnedRoot.Parent;
            if ((split != null)
                && (m_splitSizes != null)
                && (split.Children.Count == m_splitSizes.Count))
            {
                split.UI?.SetTargetSizeAsync(new List<double>(m_splitSizes));
            }
        }

        // ===========[ Helper Methods ]========================================
        // Whether the zone is still a child of the split it says is its parent: a removed zone keeps its parent reference
        private static bool IsInTree (DockZoneViewModel zone)
            => zone.Parent != null && zone.Parent.Children.Contains(zone);

        // The children's sizes along the split, or null when it isn't a split or a child has no view to measure
        private static List<double>? CaptureChildSizes (DockZoneViewModel split)
        {
            bool isHorizontal = split.Orientation == eDockOrientation.Horizontal;
            if (!isHorizontal && (split.Orientation != eDockOrientation.Vertical))
            {
                return null;
            }

            var sizes = new List<double>(split.Children.Count);
            foreach (DockZoneViewModel child in split.Children)
            {
                if (child.UI == null)
                {
                    return null;
                }

                sizes.Add(isHorizontal ? child.UI.RenderSize.Width : child.UI.RenderSize.Height);
            }

            return sizes;
        }

        private static eDockInsertionDirection SideOf (eDockOrientation splitOrientation, bool isBefore)
        {
            if (splitOrientation == eDockOrientation.Horizontal)
            {
                return isBefore ? eDockInsertionDirection.Left : eDockInsertionDirection.Right;
            }

            return isBefore ? eDockInsertionDirection.Top : eDockInsertionDirection.Bottom;
        }
    }
}
