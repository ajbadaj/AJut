namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using Foundation;

    // ===========[ ToggleStrip ]================================================
    // A row (or, with Orientation vertical, a column) of toggle buttons for picking one (or, with
    // AllowMultiSelect, several) of a few options: the AppKit view over AJut.UX's
    // ToggleStripItemCollection, which owns every selection rule (single-select swapping, the
    // deselect veto, restoring a selection across an ItemsSource change, naming items). This class
    // only draws the items and lays them out; which edges draw separators and which corners round
    // is AJut.UX's ToggleStripItemEdges, the same on every stack.
    //
    // It is a strip of AJut-drawn buttons rather than an NSSegmentedControl. A segmented control
    // cannot take per-item padding, separators of a set thickness, uniform sizing, an overflow
    // menu or a vertical layout, and its look cannot follow the AJut palette.
    //
    // OverflowBehavior, when the items do not fit along the strip:
    //  - Clip: the strip is cut off at its end.
    //  - Scroll: the strip scrolls along its length, and a newly selected item is scrolled into view.
    //    The mouse wheel scrolls a horizontal strip sideways. There are no scrollers.
    //  - OverflowPopup: AJut.UX's ToggleStripOverflowCalculator decides which items stay in the strip
    //    (the one just picked first, then the other selected ones, order kept), and the rest go in a
    //    menu behind a chevron at the end:
    //    the right of a horizontal strip, the foot of a vertical one. The menu opens below it.
    //
    // The ItemsSource collection is only listened to while the strip is in a window, so a
    // long-lived source never keeps a strip that was dropped without being cleaned up. Coming back
    // into a window rebuilds from the source, catching anything that changed while it was out.
    public class ToggleStrip : NSView, IToggleStripHost
    {
        private const double kOverflowButtonLength = 22.0;

        private readonly ToggleStripItemCollection m_items;
        private readonly List<ItemButton> m_buttons = new List<ItemButton>();
        private readonly OverflowChevronButton m_overflowButton;
        private readonly StripScrollView m_scrollView;
        private readonly FlippedDocumentView m_scrollDocument;
        private IEnumerable m_itemsSource;
        private INotifyCollectionChanged m_listenedSource;
        private object m_selectedItem;
        private IList m_selectedItems;
        private bool m_isStoringSelection;
        private bool m_allowMultiSelect;
        private bool m_allowNoSelection;
        private string m_displayPropertyPath = String.Empty;
        private Func<object, string> m_itemNameSelector;
        private eToggleStripOverflow m_overflowBehavior = eToggleStripOverflow.Clip;
        private NSUserInterfaceLayoutOrientation m_orientation = NSUserInterfaceLayoutOrientation.Horizontal;
        private double m_separatorThickness = 1.0;
        private NSEdgeInsets m_itemPadding = new NSEdgeInsets(4, 8, 4, 8);
        private bool m_ensureUniformSize;
        private double m_cornerRadius = 2.0;
        private NSFont m_font = NSFont.SystemFontOfSize(NSFont.SystemFontSize);
        private readonly List<int> m_overflowIndices = new List<int>();
        private readonly ToggleStripDragScroll m_dragScroll = new ToggleStripDragScroll();

        // ===========[ Setup/Construction/Teardown ]===========================
        public ToggleStrip ()
        {
            m_items = new ToggleStripItemCollection(this);
            m_items.CollectionChanged += this.OnItemsChanged;

            m_overflowButton = new OverflowChevronButton { Hidden = true };
            m_overflowButton.Activated += this.OnOverflowButtonActivated;
            this.AddSubview(m_overflowButton);

            // No scrollers: they draw over the strip they scroll. The strip scrolls with the wheel, a trackpad
            // swipe, and by scrolling a newly selected item into view.
            m_scrollDocument = new FlippedDocumentView();
            m_scrollView = new StripScrollView
            {
                HasHorizontalScroller = false,
                HasVerticalScroller = false,
                DrawsBackground = false,
                DocumentView = m_scrollDocument,
                Hidden = true,
            };
            this.AddSubview(m_scrollView);

            this.ClipsToBounds = true;
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                this.ListenToSource(null);
                m_items.CollectionChanged -= this.OnItemsChanged;
                m_overflowButton.Activated -= this.OnOverflowButtonActivated;
                foreach (ItemButton button in m_buttons)
                {
                    button.Activated -= this.OnItemButtonActivated;
                    button.Detach();
                }

                m_buttons.Clear();
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        /// <summary>Raised after the selection changes, whether the user changed it or code did</summary>
        public event EventHandler<ToggleStripSelectionChangedEventArgs> SelectionChanged;

        public override void ViewDidMoveToWindow ()
        {
            base.ViewDidMoveToWindow();
            if (this.Window != null)
            {
                this.ListenToSource(m_itemsSource as INotifyCollectionChanged);
                m_items.ResetFromSource(m_itemsSource, m_selectedItem);
            }
            else
            {
                this.ListenToSource(null);
            }
        }

        private void OnSourceCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            m_items.ApplySourceChange(e);
        }

        private void OnItemsChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            this.RebuildButtons();
        }

        private void OnItemButtonActivated (object sender, EventArgs e)
        {
            // The item's own rules decide; a refused deselect comes back as a change notification
            // and the button redraws as still selected
            ToggleStripItem item = ((ItemButton)sender).Item;
            item.IsSelected = !item.IsSelected;
        }

        private void OnOverflowButtonActivated (object sender, EventArgs e)
        {
            this.ShowOverflowMenu();
        }

        private void OnOverflowMenuItemActivated (object sender, EventArgs e)
        {
            int index = (int)((NSMenuItem)sender).Tag;
            if (index >= 0 && index < m_items.Count)
            {
                ToggleStripItem item = m_items[index];
                item.IsSelected = !item.IsSelected;
            }
        }

        // ===========[ Properties ]============================================
        public override bool IsFlipped => true;

        /// <summary>The items to show. An INotifyCollectionChanged source is followed as it changes.</summary>
        public IEnumerable ItemsSource
        {
            get => m_itemsSource;
            set
            {
                if (m_itemsSource == value)
                {
                    return;
                }

                m_itemsSource = value;
                if (this.Window != null)
                {
                    this.ListenToSource(value as INotifyCollectionChanged);
                }

                m_items.ResetFromSource(value, m_selectedItem);
            }
        }

        /// <summary>
        /// The selected item's data (the last one selected, with AllowMultiSelect). Setting it selects the
        /// matching item by Equals, under the strip's usual rules.
        /// </summary>
        public object SelectedItem
        {
            get => m_selectedItem;
            set
            {
                if (m_isStoringSelection)
                {
                    return;
                }

                m_items.SelectData(value);
            }
        }

        /// <summary>
        /// Every selected item's data, in strip order. Give it a list to have that list kept filled in
        /// place (an observable list then notifies as it changes); otherwise a new list is set on each
        /// change. Like WinUI3's ToggleStrip, changes made to the list from outside are not read back.
        /// </summary>
        public IList SelectedItems
        {
            get => m_selectedItems;
            set => m_selectedItems = value;
        }

        /// <summary>The items as the strip holds them, with their names and selection</summary>
        public IReadOnlyList<ToggleStripItem> Items => m_items;

        public bool AllowMultiSelect
        {
            get => m_allowMultiSelect;
            set => m_allowMultiSelect = value;
        }

        public bool AllowNoSelection
        {
            get => m_allowNoSelection;
            set => m_allowNoSelection = value;
        }

        /// <summary>The property of each item's data shown as its name; the data's ToString when empty</summary>
        public string DisplayPropertyPath
        {
            get => m_displayPropertyPath;
            set
            {
                m_displayPropertyPath = value ?? String.Empty;
                m_items.ResetNames();
            }
        }

        /// <summary>
        /// Names each item from its data, and wins over <see cref="DisplayPropertyPath"/> when set. Prefer this
        /// in a trimmed or AOT build, where the reflection DisplayPropertyPath needs can be stripped.
        /// </summary>
        public Func<object, string> ItemNameSelector
        {
            get => m_itemNameSelector;
            set
            {
                m_itemNameSelector = value;
                m_items.ResetNames();
            }
        }

        /// <summary>What happens when the items do not fit; Clip by default</summary>
        public eToggleStripOverflow OverflowBehavior
        {
            get => m_overflowBehavior;
            set
            {
                m_overflowBehavior = value;
                this.RebuildButtons();
            }
        }

        /// <summary>Lays the items out left to right (the default) or top to bottom</summary>
        public NSUserInterfaceLayoutOrientation Orientation
        {
            get => m_orientation;
            set
            {
                m_orientation = value;
                this.RebuildButtons();
            }
        }

        /// <summary>Width of the line between neighboring items, in points</summary>
        public double SeparatorThickness
        {
            get => m_separatorThickness;
            set
            {
                m_separatorThickness = value;
                this.RelayoutItems();
            }
        }

        /// <summary>Space around each item's name</summary>
        public NSEdgeInsets ItemPadding
        {
            get => m_itemPadding;
            set
            {
                m_itemPadding = value;
                this.RelayoutItems();
            }
        }

        /// <summary>Makes every item as wide as the widest one; a vertical strip's items always are</summary>
        public bool EnsureUniformSize
        {
            get => m_ensureUniformSize;
            set
            {
                m_ensureUniformSize = value;
                this.RelayoutItems();
            }
        }

        /// <summary>Corner radius of the strip, and of the outer corners of its first and last items</summary>
        public double CornerRadius
        {
            get => m_cornerRadius;
            set
            {
                m_cornerRadius = value;
                this.RelayoutItems();
            }
        }

        /// <summary>The font item names are drawn in</summary>
        public NSFont Font
        {
            get => m_font;
            set
            {
                m_font = value ?? NSFont.SystemFontOfSize(NSFont.SystemFontSize);
                this.RelayoutItems();
            }
        }

        public override CGSize IntrinsicContentSize
        {
            get
            {
                double[] lengths = this.MeasureItemLengths();
                // Every item measures with a separator's room, and the first one drops it
                double total = lengths.Length > 0 ? -m_separatorThickness : 0.0;
                foreach (double length in lengths)
                {
                    total += length;
                }

                return this.IsVertical
                    ? new CGSize((nfloat)this.MeasureWidestItemWidth(), (nfloat)total)
                    : new CGSize((nfloat)total, (nfloat)this.MeasureItemHeight());
            }
        }

        private bool IsVertical => m_orientation == NSUserInterfaceLayoutOrientation.Vertical;

        // ===========[ IToggleStripHost Implementations ]======================
        void IToggleStripHost.OnSelectionChanged (ToggleStripSelectionChange change)
        {
            m_isStoringSelection = true;
            try
            {
                m_selectedItem = change.SelectedItem;
                this.StoreSelectedItems(change.CurrentSelection);
            }
            finally
            {
                m_isStoringSelection = false;
            }

            // Selection changes which items claim the row first, so the split is redone
            if (m_overflowBehavior == eToggleStripOverflow.OverflowPopup)
            {
                this.NeedsLayout = true;
            }
            else if (m_overflowBehavior == eToggleStripOverflow.Scroll && change.Selected != null)
            {
                this.ScrollItemIntoView(change.Selected);
            }

            this.SelectionChanged?.Invoke(this, new ToggleStripSelectionChangedEventArgs(change.PreviousSelection, change.CurrentSelection));
        }

        // ===========[ Public Interface Methods ]==============================
        public override void Layout ()
        {
            base.Layout();
            this.LayoutItems();
        }

        public override void DrawRect (CGRect dirtyRect)
        {
            nfloat halfBorder = (nfloat)(m_separatorThickness / 2.0);
            NSBezierPath outline = NSBezierPath.FromRoundedRect(this.Bounds.Inset(halfBorder, halfBorder), (nfloat)m_cornerRadius, (nfloat)m_cornerRadius);

            NSGradient gradient = ThemedColors.GetThemedGradient(ThemedColorKeys.ToggleStrip_Background, this.EffectiveAppearance);
            if (gradient != null)
            {
                gradient.DrawInBezierPath(outline, (nfloat)90);
            }
            else
            {
                ThemedColors.Get(ThemedColorKeys.ToggleStrip_Background).SetFill();
                outline.Fill();
            }

            if (m_separatorThickness > 0.0)
            {
                outline.LineWidth = (nfloat)m_separatorThickness;
                ThemedColors.Get(ThemedColorKeys.ToggleStrip_Border).SetStroke();
                outline.Stroke();
            }
        }

        public override void ViewDidChangeEffectiveAppearance ()
        {
            base.ViewDidChangeEffectiveAppearance();
            this.NeedsDisplay = true;
        }

        // ===========[ Helper Methods ]========================================
        private void ListenToSource (INotifyCollectionChanged source)
        {
            if (m_listenedSource != null)
            {
                m_listenedSource.CollectionChanged -= this.OnSourceCollectionChanged;
            }

            m_listenedSource = source;
            if (m_listenedSource != null)
            {
                m_listenedSource.CollectionChanged -= this.OnSourceCollectionChanged;
                m_listenedSource.CollectionChanged += this.OnSourceCollectionChanged;
            }
        }

        private void StoreSelectedItems (IList current)
        {
            // Fill a list we were handed in place, the way WinUI3 does for an observable one, so a
            // binding to it sees the changes; otherwise hand out a new list
            if (m_selectedItems != null && !m_selectedItems.IsFixedSize && !m_selectedItems.IsReadOnly)
            {
                m_selectedItems.Clear();
                foreach (object data in current)
                {
                    m_selectedItems.Add(data);
                }
            }
            else
            {
                m_selectedItems = new List<object>(EnumerateData(current));
            }
        }

        private void RebuildButtons ()
        {
            foreach (ItemButton button in m_buttons)
            {
                button.Activated -= this.OnItemButtonActivated;
                button.Detach();
                button.RemoveFromSuperview();
            }

            m_buttons.Clear();
            NSView host = m_overflowBehavior == eToggleStripOverflow.Scroll ? m_scrollDocument : this;
            foreach (ToggleStripItem item in m_items)
            {
                var button = new ItemButton(this, item);
                button.Activated += this.OnItemButtonActivated;
                m_buttons.Add(button);
                host.AddSubview(button);
            }

            m_scrollView.Hidden = m_overflowBehavior != eToggleStripOverflow.Scroll;
            m_scrollView.IsVertical = this.IsVertical;
            this.RelayoutItems();
        }

        private void RelayoutItems ()
        {
            this.InvalidateIntrinsicContentSize();
            this.NeedsLayout = true;
            this.NeedsDisplay = true;
            foreach (ItemButton button in m_buttons)
            {
                button.NeedsDisplay = true;
            }
        }

        // Each item's length along the strip, with room for a separator leading it; the first item
        // placed has none, and the layout takes that room back off it
        private double[] MeasureItemLengths ()
        {
            var lengths = new double[m_buttons.Count];
            if (this.IsVertical)
            {
                double height = this.MeasureItemHeight() + m_separatorThickness;
                for (int i = 0; i < lengths.Length; ++i)
                {
                    lengths[i] = height;
                }

                return lengths;
            }

            double widest = 0.0;
            for (int i = 0; i < lengths.Length; ++i)
            {
                lengths[i] = m_buttons[i].MeasureWidth() + m_separatorThickness;
                widest = Math.Max(widest, lengths[i]);
            }

            if (m_ensureUniformSize)
            {
                for (int i = 0; i < lengths.Length; ++i)
                {
                    lengths[i] = widest;
                }
            }

            return lengths;
        }

        private double MeasureWidestItemWidth ()
        {
            double widest = 0.0;
            foreach (ItemButton button in m_buttons)
            {
                widest = Math.Max(widest, button.MeasureWidth());
            }

            return widest;
        }

        private double MeasureItemHeight ()
        {
            double textHeight = Math.Ceiling((double)(m_font.Ascender - m_font.Descender + m_font.Leading));
            return textHeight + (double)m_itemPadding.Top + (double)m_itemPadding.Bottom;
        }

        private void LayoutItems ()
        {
            bool isVertical = this.IsVertical;
            double[] lengths = this.MeasureItemLengths();
            double crossSize = (double)(isVertical ? this.Bounds.Width : this.Bounds.Height);
            double available = (double)(isVertical ? this.Bounds.Height : this.Bounds.Width);

            m_overflowIndices.Clear();
            bool[] isVisible = new bool[lengths.Length];
            switch (m_overflowBehavior)
            {
                case eToggleStripOverflow.OverflowPopup:
                {
                    // The item just picked (SelectedItem) claims the strip ahead of the other selected ones
                    var selected = new bool[lengths.Length];
                    int pickedIndex = -1;
                    for (int i = 0; i < selected.Length; ++i)
                    {
                        selected[i] = m_items[i].IsSelected;
                        if (selected[i] && Equals(m_items[i].Data, m_selectedItem))
                        {
                            pickedIndex = i;
                        }
                    }

                    // The chevron's room includes the separator the last item then draws before it
                    ToggleStripOverflowResult result = ToggleStripOverflowCalculator.Compute(lengths, selected, available, kOverflowButtonLength + m_separatorThickness, pickedIndex);
                    foreach (int index in result.VisibleIndices)
                    {
                        isVisible[index] = true;
                    }

                    m_overflowIndices.AddRange(result.OverflowIndices);
                    break;
                }

                default:
                    for (int i = 0; i < isVisible.Length; ++i)
                    {
                        isVisible[i] = true;
                    }

                    break;
            }

            // 1. Place the visible items along the strip, telling each which edges draw separators and
            //    which corners round. A length carries room for a leading separator: the first item
            //    has none, and the last one takes a trailing one when the chevron follows it.
            int visibleCount = 0;
            foreach (bool visible in isVisible)
            {
                if (visible)
                {
                    ++visibleCount;
                }
            }

            double position = 0.0;
            int placed = 0;
            bool hasOverflow = m_overflowIndices.Count > 0;
            for (int i = 0; i < m_buttons.Count; ++i)
            {
                ItemButton button = m_buttons[i];
                button.Hidden = !isVisible[i];
                if (!isVisible[i])
                {
                    continue;
                }

                ToggleStripItemEdges edges = ToggleStripItemEdges.For(placed, visibleCount, isVertical, hasOverflow);
                bool hasLeadingSeparator = isVertical ? edges.SeparatorTop : edges.SeparatorLeft;
                bool hasTrailingSeparator = isVertical ? edges.SeparatorBottom : edges.SeparatorRight;
                double length = lengths[i]
                    - (hasLeadingSeparator ? 0.0 : m_separatorThickness)
                    + (hasTrailingSeparator ? m_separatorThickness : 0.0);

                button.SetEdges(edges);
                button.Frame = isVertical
                    ? new CGRect(0, (nfloat)position, (nfloat)crossSize, (nfloat)length)
                    : new CGRect((nfloat)position, 0, (nfloat)length, (nfloat)crossSize);
                position += length;
                ++placed;
            }

            // 2. The overflow chevron at the strip's end (centered at the foot of a vertical strip), or
            //    the scrolling document
            m_overflowButton.Hidden = !hasOverflow;
            if (hasOverflow)
            {
                m_overflowButton.Frame = isVertical
                    ? new CGRect((nfloat)((crossSize - kOverflowButtonLength) / 2.0), (nfloat)position, (nfloat)kOverflowButtonLength, (nfloat)kOverflowButtonLength)
                    : new CGRect((nfloat)position, 0, (nfloat)kOverflowButtonLength, (nfloat)crossSize);
            }

            if (m_overflowBehavior == eToggleStripOverflow.Scroll)
            {
                m_scrollView.Frame = this.Bounds;
                m_scrollDocument.Frame = isVertical
                    ? new CGRect(0, 0, (nfloat)crossSize, (nfloat)position)
                    : new CGRect(0, 0, (nfloat)position, (nfloat)crossSize);
            }
        }

        // Click-and-drag scrolling in Scroll mode. AppKit sends a press's drags and release to the view that took the
        // mouse down, which is the item button, so the button reports them here. Positions are in the strip's own
        // (flipped) coordinates, which stay put while the content scrolls under them.
        private void BeginDragScroll (NSEvent mouseDown)
        {
            if (m_overflowBehavior == eToggleStripOverflow.Scroll)
            {
                m_dragScroll.Begin(this.PositionAlongStrip(mouseDown));
            }
        }

        // True once the press is a drag, so the button stops tracking it as a click
        private bool MoveDragScroll (NSEvent mouseDragged)
        {
            double delta = m_dragScroll.Move(this.PositionAlongStrip(mouseDragged));
            if (delta != 0.0)
            {
                // The content follows the pointer, so the scroll offset moves against it
                m_scrollView.ScrollAlong(-delta);
            }

            return m_dragScroll.IsDragging;
        }

        // True when the press was a drag, so the release must not click the item
        private bool EndDragScroll () => m_dragScroll.End();

        private double PositionAlongStrip (NSEvent theEvent)
        {
            CGPoint point = this.ConvertPointFromView(theEvent.LocationInWindow, null);
            return this.IsVertical ? (double)point.Y : (double)point.X;
        }

        private void ScrollItemIntoView (ToggleStripItem item)
        {
            foreach (ItemButton button in m_buttons)
            {
                if (button.Item == item)
                {
                    m_scrollDocument.ScrollRectToVisible(button.Frame);
                    return;
                }
            }
        }

        // An NSMenu pops up and tracks synchronously, so its handlers are unhooked as soon as
        // PopUpMenu returns
        private void ShowOverflowMenu ()
        {
            var menu = new NSMenu { AutoEnablesItems = false };
            var menuItems = new List<NSMenuItem>(m_overflowIndices.Count);
            foreach (int index in m_overflowIndices)
            {
                ToggleStripItem item = m_items[index];
                var menuItem = new NSMenuItem(item.Name ?? String.Empty)
                {
                    Tag = index,
                    State = item.IsSelected ? NSCellStateValue.On : NSCellStateValue.Off,
                };
                menuItem.Activated += this.OnOverflowMenuItemActivated;
                menuItems.Add(menuItem);
                menu.AddItem(menuItem);
            }

            menu.PopUpMenu(null, new CGPoint(0, m_overflowButton.Bounds.Height), m_overflowButton);

            foreach (NSMenuItem menuItem in menuItems)
            {
                menuItem.Activated -= this.OnOverflowMenuItemActivated;
            }
        }

        private static IEnumerable<object> EnumerateData (IList list)
        {
            foreach (object data in list)
            {
                yield return data;
            }
        }

        // Items have square inner corners and round outer ones, each corner as ToggleStripItemEdges
        // says: a horizontal strip's first item rounds its left corners and its last its right ones,
        // a vertical strip's first its top corners and its last its bottom ones. Drawn as lines and
        // quarter-circle cubics, with the standard 0.5523 control arm for a quarter circle.
        private static NSBezierPath MakeItemPath (CGRect rect, double topLeftRadius, double topRightRadius, double bottomRightRadius, double bottomLeftRadius)
        {
            const double kQuarterCircleArm = 0.5523;

            double left = (double)rect.Left;
            double right = (double)rect.Right;
            double top = (double)rect.Top;
            double bottom = (double)rect.Bottom;
            double maxRadius = Math.Min((double)rect.Width, (double)rect.Height) / 2.0;
            double topLeft = Math.Min(topLeftRadius, maxRadius);
            double topRight = Math.Min(topRightRadius, maxRadius);
            double bottomRight = Math.Min(bottomRightRadius, maxRadius);
            double bottomLeft = Math.Min(bottomLeftRadius, maxRadius);

            var path = new NSBezierPath();
            path.MoveTo(Point(left + topLeft, top));
            path.LineTo(Point(right - topRight, top));
            Corner(path, right - topRight, top, right, top + topRight, topRight, isOnTop: true, isOnRight: true);
            path.LineTo(Point(right, bottom - bottomRight));
            Corner(path, right, bottom - bottomRight, right - bottomRight, bottom, bottomRight, isOnTop: false, isOnRight: true);
            path.LineTo(Point(left + bottomLeft, bottom));
            Corner(path, left + bottomLeft, bottom, left, bottom - bottomLeft, bottomLeft, isOnTop: false, isOnRight: false);
            path.LineTo(Point(left, top + topLeft));
            Corner(path, left, top + topLeft, left + topLeft, top, topLeft, isOnTop: true, isOnRight: false);
            path.ClosePath();
            return path;

            static CGPoint Point (double x, double y) => new CGPoint((nfloat)x, (nfloat)y);

            // From (x0, y0) to (x1, y1) around a corner; each control point runs from its end
            // point toward the corner itself
            static void Corner (NSBezierPath path, double x0, double y0, double x1, double y1, double radius, bool isOnTop, bool isOnRight)
            {
                if (radius <= 0.0)
                {
                    path.LineTo(Point(x1, y1));
                    return;
                }

                double cornerX = isOnRight ? Math.Max(x0, x1) : Math.Min(x0, x1);
                double cornerY = isOnTop ? Math.Min(y0, y1) : Math.Max(y0, y1);
                double arm = radius * kQuarterCircleArm;
                path.CurveTo(
                    Point(x1, y1),
                    Point(x0 + (Math.Sign(cornerX - x0) * arm), y0 + (Math.Sign(cornerY - y0) * arm)),
                    Point(x1 + (Math.Sign(cornerX - x1) * arm), y1 + (Math.Sign(cornerY - y1) * arm))
                );
            }
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>What <see cref="SelectionChanged"/> reports: the selected data before and after</summary>
        public sealed class ToggleStripSelectionChangedEventArgs : EventArgs
        {
            public ToggleStripSelectionChangedEventArgs (IList previousSelection, IList currentSelection)
            {
                this.PreviousSelection = previousSelection;
                this.CurrentSelection = currentSelection;
            }

            public IList PreviousSelection { get; }
            public IList CurrentSelection { get; }
        }

        private sealed class FlippedDocumentView : NSView
        {
            public override bool IsFlipped => true;
        }

        // The overflow button: a thin down chevron in the button foreground for its state, filled only while the
        // pointer is over it or it is pressed, the way the WinUI3 strip shows a glyph on its overflow button. A
        // GeometryButton fills and traces in button face colors, which all but vanish on the dark palette.
        private sealed class OverflowChevronButton : ManagedButton
        {
            public OverflowChevronButton ()
            {
                this.ToolTip = "More";
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                const double kGlyphHalfWidth = 4.0;
                const double kGlyphLineWidth = 1.5;

                eManagedButtonState state = this.CurrentState;
                if (state == eManagedButtonState.PointerOver || state == eManagedButtonState.Pressed)
                {
                    string backgroundKey = state == eManagedButtonState.Pressed ? ThemedColorKeys.BrushButton_PressedBackground : ThemedColorKeys.BrushButton_PointerOverBackground;
                    ThemedColors.Get(backgroundKey).SetFill();
                    NSBezierPath.FillRect(this.Bounds);
                }

                double centerX = (double)this.Bounds.Width / 2.0;
                double centerY = (double)this.Bounds.Height / 2.0;
                var glyph = new NSBezierPath
                {
                    LineWidth = (nfloat)kGlyphLineWidth,
                    LineCapStyle = NSLineCapStyle.Round,
                    LineJoinStyle = NSLineJoinStyle.Round,
                };

                // Points down; the view is flipped, so +y is down
                glyph.MoveTo(new CGPoint(centerX - kGlyphHalfWidth, centerY - kGlyphHalfWidth / 2.0));
                glyph.LineTo(new CGPoint(centerX, centerY + kGlyphHalfWidth / 2.0));
                glyph.LineTo(new CGPoint(centerX + kGlyphHalfWidth, centerY - kGlyphHalfWidth / 2.0));
                ThemedColors.Get(ForegroundKeyFor(state)).SetStroke();
                glyph.Stroke();
            }

            private static string ForegroundKeyFor (eManagedButtonState state)
            {
                switch (state)
                {
                    case eManagedButtonState.PointerOver: return ThemedColorKeys.BrushButton_PointerOverForeground;
                    case eManagedButtonState.Pressed: return ThemedColorKeys.BrushButton_PressedForeground;
                    case eManagedButtonState.Disabled: return ThemedColorKeys.BrushButton_DisabledForeground;
                    default: return ThemedColorKeys.BrushButton_NormalForeground;
                }
            }
        }

        // The Scroll mode scroller. A mouse wheel only scrolls vertically, so on a horizontal strip it did nothing; there
        // a mostly vertical scroll is turned into a sideways one, as the WinUI3 strip's BumpStack does. A mostly
        // sideways one (a trackpad swipe) goes through as normal, and so does everything on a vertical strip.
        private sealed class StripScrollView : NSScrollView
        {
            public bool IsVertical { get; set; }

            public override void ScrollWheel (NSEvent theEvent)
            {
                // A line based wheel reports its deltas in lines, a trackpad in points
                const double kWheelLineStep = 16.0;

                double deltaX = (double)theEvent.ScrollingDeltaX;
                double deltaY = (double)theEvent.ScrollingDeltaY;
                if (this.IsVertical || Math.Abs(deltaY) <= Math.Abs(deltaX) || this.DocumentView == null)
                {
                    base.ScrollWheel(theEvent);
                    return;
                }

                double step = theEvent.HasPreciseScrollingDeltas ? deltaY : deltaY * kWheelLineStep;
                this.ScrollAlong(-step);
            }

            /// <summary>Moves the scroll offset along the strip by <paramref name="offsetDelta"/> points, kept inside the content</summary>
            public void ScrollAlong (double offsetDelta)
            {
                NSView document = this.DocumentView;
                if (document == null)
                {
                    return;
                }

                NSClipView clip = this.ContentView;
                CGRect visible = clip.Bounds;
                if (this.IsVertical)
                {
                    double maxY = Math.Max(0.0, (double)document.Frame.Height - (double)visible.Height);
                    clip.ScrollToPoint(new CGPoint(visible.X, Math.Clamp((double)visible.Y + offsetDelta, 0.0, maxY)));
                }
                else
                {
                    double maxX = Math.Max(0.0, (double)document.Frame.Width - (double)visible.Width);
                    clip.ScrollToPoint(new CGPoint(Math.Clamp((double)visible.X + offsetDelta, 0.0, maxX), visible.Y));
                }

                this.ReflectScrolledClipView(clip);
            }
        }

        // One item: draws its background for its state and selection, the separators its edges call
        // for, and its name. It follows its item's name and selection.
        private sealed class ItemButton : ManagedButton
        {
            private readonly ToggleStrip m_owner;
            private ToggleStripItem m_item;
            private ToggleStripItemEdges m_edges;

            public ItemButton (ToggleStrip owner, ToggleStripItem item)
            {
                m_owner = owner;
                m_item = item;
                m_item.PropertyChanged += this.OnItemPropertyChanged;
            }

            public ToggleStripItem Item => m_item;

            // VoiceOver reads the item's name, and whether it is picked
            protected override string DefaultAccessibilityLabel => m_item?.Name;
            public override bool AccessibilitySelected => m_item?.IsSelected == true;

            // In Scroll mode a press that moves past the drag threshold scrolls the strip instead of clicking the item
            public override void MouseDown (NSEvent theEvent)
            {
                base.MouseDown(theEvent);
                m_owner.BeginDragScroll(theEvent);
            }

            public override void MouseDragged (NSEvent theEvent)
            {
                if (m_owner.MoveDragScroll(theEvent))
                {
                    this.CancelPress();
                    return;
                }

                base.MouseDragged(theEvent);
            }

            public override void MouseUp (NSEvent theEvent)
            {
                if (m_owner.EndDragScroll())
                {
                    this.CancelPress();
                    return;
                }

                base.MouseUp(theEvent);
            }

            public void Detach ()
            {
                if (m_item != null)
                {
                    m_item.PropertyChanged -= this.OnItemPropertyChanged;
                    m_item = null;
                }
            }

            public void SetEdges (ToggleStripItemEdges edges)
            {
                if (m_edges != edges)
                {
                    m_edges = edges;
                    this.NeedsDisplay = true;
                }
            }

            /// <summary>The name's width plus the item's padding, without any separator</summary>
            public double MeasureWidth ()
            {
                double textWidth = Math.Ceiling((double)this.MakeName(NSColor.Black).Size.Width);
                return textWidth + (double)m_owner.m_itemPadding.Left + (double)m_owner.m_itemPadding.Right;
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                if (m_item == null)
                {
                    return;
                }

                CGRect bounds = this.Bounds;
                double thickness = m_owner.m_separatorThickness;
                double radius = m_owner.m_cornerRadius;
                var separator = new NSEdgeInsets(
                    (nfloat)(m_edges.SeparatorTop ? thickness : 0.0),
                    (nfloat)(m_edges.SeparatorLeft ? thickness : 0.0),
                    (nfloat)(m_edges.SeparatorBottom ? thickness : 0.0),
                    (nfloat)(m_edges.SeparatorRight ? thickness : 0.0)
                );
                var surfaceRect = new CGRect(
                    bounds.X + separator.Left,
                    bounds.Y + separator.Top,
                    bounds.Width - separator.Left - separator.Right,
                    bounds.Height - separator.Top - separator.Bottom
                );

                // 1. Background, if this state has one
                string backgroundKey = this.BackgroundKeyForState();
                if (backgroundKey != null)
                {
                    NSBezierPath surface = MakeItemPath(
                        surfaceRect,
                        m_edges.RoundTopLeft ? radius : 0.0,
                        m_edges.RoundTopRight ? radius : 0.0,
                        m_edges.RoundBottomRight ? radius : 0.0,
                        m_edges.RoundBottomLeft ? radius : 0.0
                    );

                    NSGradient gradient = ThemedColors.GetThemedGradient(backgroundKey, this.EffectiveAppearance);
                    if (gradient != null)
                    {
                        gradient.DrawInBezierPath(surface, (nfloat)90);
                    }
                    else
                    {
                        ThemedColors.Get(backgroundKey).SetFill();
                        surface.Fill();
                    }
                }

                // 2. Separators, on the edges that have one
                ThemedColors.Get(ThemedColorKeys.ToggleStrip_Border).SetFill();
                if (separator.Left > 0)
                {
                    NSBezierPath.FillRect(new CGRect(bounds.X, bounds.Y, separator.Left, bounds.Height));
                }

                if (separator.Top > 0)
                {
                    NSBezierPath.FillRect(new CGRect(bounds.X, bounds.Y, bounds.Width, separator.Top));
                }

                if (separator.Right > 0)
                {
                    NSBezierPath.FillRect(new CGRect(bounds.Right - separator.Right, bounds.Y, separator.Right, bounds.Height));
                }

                if (separator.Bottom > 0)
                {
                    NSBezierPath.FillRect(new CGRect(bounds.X, bounds.Bottom - separator.Bottom, bounds.Width, separator.Bottom));
                }

                // 3. Name
                string foregroundKey = m_item.IsSelected ? ThemedColorKeys.ToggleStripButton_CheckedForeground : ThemedColorKeys.ToggleStripButton_Foreground;
                NSColor foreground = ThemedColors.Get(foregroundKey);
                if (!this.Enabled)
                {
                    const double kDisabledTextAlpha = 0.5;
                    foreground = foreground.ColorWithAlphaComponent((nfloat)kDisabledTextAlpha);
                }

                NSAttributedString name = this.MakeName(foreground);
                CGSize size = name.Size;
                name.DrawAtPoint(new CGPoint(
                    surfaceRect.X + ((surfaceRect.Width - size.Width) / 2),
                    surfaceRect.Y + ((surfaceRect.Height - size.Height) / 2)
                ));
            }

            private void OnItemPropertyChanged (object sender, PropertyChangedEventArgs e)
            {
                // A refused deselect still raises IsSelected, which is what resyncs this button
                this.NeedsDisplay = true;
                if (e.PropertyName == nameof(ToggleStripItem.Name))
                {
                    m_owner.RelayoutItems();
                }
            }

            private string BackgroundKeyForState ()
            {
                eManagedButtonState state = this.CurrentState;
                if (m_item.IsSelected)
                {
                    switch (state)
                    {
                        case eManagedButtonState.PointerOver: return ThemedColorKeys.ToggleStripButton_CheckedPointerOverBackground;
                        case eManagedButtonState.Pressed: return ThemedColorKeys.ToggleStripButton_CheckedPressedBackground;
                        default: return ThemedColorKeys.ToggleStripButton_CheckedBackground;
                    }
                }

                switch (state)
                {
                    case eManagedButtonState.PointerOver: return ThemedColorKeys.ToggleStripButton_PointerOverBackground;
                    case eManagedButtonState.Pressed: return ThemedColorKeys.ToggleStripButton_PressedBackground;
                    default: return null;
                }
            }

            private NSAttributedString MakeName (NSColor foreground)
            {
                return new NSAttributedString(m_item?.Name ?? String.Empty, new NSStringAttributes { Font = m_owner.m_font, ForegroundColor = foreground });
            }
        }
    }
}
