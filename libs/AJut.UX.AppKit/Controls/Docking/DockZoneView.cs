namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using AJut.UX.CodeBinding;
    using AJut.UX.Docking;
    using AJut.UX.PropertyInteraction;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using Foundation;

    // ===========[ DockZoneView ]===============================================
    // The AppKit view of one AJut.UX DockZoneViewModel: the VM owns the dock tree (splits, tabs,
    // closing, drop and reorder rules, serialization) and this view only shows the zone it is
    // given, rebuilding whenever the zone's shape changes.
    //  - A split (Horizontal or Vertical) is a ThemedSplitView of child DockZoneViews. The VM's
    //    Horizontal means side by side, which is NSSplitView's IsVertical (vertical dividers).
    //    When the split's children change, each pane still showing keeps its current size as
    //    its zone's pass-along size, and the rebuilt split gives every pane the size its zone
    //    passes along, the way WinUI3's HandleNewChildZonesAdded does. A layout load and a drop
    //    leave pass-along sizes on the zones they build the same way.
    //  - A leaf (Single or Tabbed) is a header (the selected panel's title, and its close button),
    //    the selected panel's display, and for Tabbed a tab strip along the bottom, as on WinUI3.
    //  - An empty zone shows a placeholder.
    //
    // A panel's display is the IDockableDisplayElement the adapter carries, which on AppKit has
    // to be an NSView. Titles and tooltips follow the adapter live.
    //
    // Dragging is the docking manager's: a header dragged past the threshold, or a tab dragged up
    // or down out of the strip, hands the drag to it, and it tears the zone or panel off into a
    // tearoff window that follows the pointer. While a drag is over this zone the manager shows
    // its drop overlay (DockDropOverlayView). Tabs dragged along the strip reorder; the reorder
    // itself is the VM's.
    public class DockZoneView : ThemedView, IDockZoneUI
    {
        private const double kHeaderHeight = 24.0;
        private const double kTabHeight = 24.0;
        private const double kCloseButtonSize = 20.0;
        private const double kFallbackMinPanelSize = 30.0;
        private const double kEdgeThickness = 1.0;
        private const double kTabTitleInset = 8.0;

        private readonly DockZoneViewModel m_zone;
        private readonly List<DockZoneView> m_childViews = new List<DockZoneView>();
        private BindingSet m_layoutBindings;
        private ThemedSplitView m_split;
        private DockDropOverlayView m_dropOverlay;
        private List<double> m_pendingChildSizes;
        private bool m_needsPaneSizing;
        private double m_tabScrollOffset;
        private bool m_isTornDown;

        // ===========[ Setup/Construction/Teardown ]===========================
        public DockZoneView (DockZoneViewModel zone)
        {
            m_zone = zone;
            m_zone.UI = this;
            m_zone.PropertyChanged += this.OnZonePropertyChanged;
            ((INotifyCollectionChanged)m_zone.Children).CollectionChanged += this.OnZoneCollectionChanged;
            ((INotifyCollectionChanged)m_zone.DockedContent).CollectionChanged += this.OnZoneCollectionChanged;
            this.RebuildLayout();
        }

        /// <summary>
        /// Unhooks this view and its child views from their zones and releases the panels' views. Call when the
        /// dock tree this view shows is going away; the zones themselves are torn down by their manager.
        /// </summary>
        public void Teardown ()
        {
            if (m_isTornDown)
            {
                return;
            }

            m_isTornDown = true;
            m_zone.PropertyChanged -= this.OnZonePropertyChanged;
            ((INotifyCollectionChanged)m_zone.Children).CollectionChanged -= this.OnZoneCollectionChanged;
            ((INotifyCollectionChanged)m_zone.DockedContent).CollectionChanged -= this.OnZoneCollectionChanged;
            this.ClearLayout();
            if (m_zone.UI == this)
            {
                m_zone.UI = null;
            }
        }

        // ===========[ Events ]================================================
        private void OnZonePropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DockZoneViewModel.Orientation) || e.PropertyName == nameof(DockZoneViewModel.SelectedIndex))
            {
                this.RebuildLayout();
            }
        }

        private void OnZoneCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            // A split's children changed: each pane still showing keeps the size it has now, so the rebuilt split can
            //  keep their proportions and make room for a new one
            if (m_split != null)
            {
                foreach (DockZoneView child in m_childViews)
                {
                    if (!child.m_zone.HasPassAlongUISize)
                    {
                        child.m_zone.StorePassAlongUISize(((IDockZoneUI)child).RenderSize);
                    }
                }
            }

            this.RebuildLayout();
        }

        // ===========[ Properties ]============================================
        /// <summary>The zone this view shows</summary>
        public DockZoneViewModel ViewModel => m_zone;

        /// <summary>
        /// The group a root zone is registered under: what a saved layout names it by, and where a panel registered with
        /// that DefaultGroupId docks. Only read from root zones.
        /// </summary>
        public string GroupId { get; set; }

        /// <summary>The zone views nested directly in this one while it shows a split, which the docking manager walks</summary>
        internal IReadOnlyList<DockZoneView> ChildZoneViews => m_childViews;

        // How far a press on the header or a tab moves before it is a drag: the manager's, or the shared default
        private double DragThreshold => (m_zone.Manager as DockingManager)?.DragThreshold ?? DockDragRules.kDefaultDragThreshold;

        // ===========[ IDockZoneUI Implementations ]===========================
        DockZoneSize IDockZoneUI.RenderSize => new DockZoneSize((double)this.Bounds.Width, (double)this.Bounds.Height);

        // The sizes arrive before the split has laid out, so they are kept and applied on the next layout
        void IDockZoneUI.SetTargetSizeAsync (List<double> sizes)
        {
            m_pendingChildSizes = sizes;
            this.NeedsLayout = true;
        }

        // ===========[ Public Interface Methods ]==============================
        public override void Layout ()
        {
            base.Layout();
            if ((m_split == null)
                || (m_split.ArrangedSubviews.Length < 2)
                || (!m_needsPaneSizing && (m_pendingChildSizes == null)))
            {
                return;
            }

            bool isSideBySide = m_zone.Orientation == eDockOrientation.Horizontal;
            double splitLength = isSideBySide ? (double)this.Bounds.Width : (double)this.Bounds.Height;
            if (splitLength <= 0.0)
            {
                return;
            }

            // 1. Every child's pass-along size is taken, even when sizes were asked for directly, so none is left behind
            //  to size some later rebuild
            var passedAlong = new List<double>(m_zone.Children.Count);
            bool anyPassedAlong = false;
            foreach (DockZoneViewModel child in m_zone.Children)
            {
                double length = 0.0;
                if (child.TakePassAlongUISize(out DockZoneSize stored))
                {
                    length = isSideBySide ? stored.Width : stored.Height;
                }

                anyPassedAlong |= length > 0.0;
                passedAlong.Add(length);
            }

            // 2. Sizes asked for directly win. Otherwise the panes keep the proportions they were passed along at, held to
            //  the smallest a panel may be; with nothing passed along they share the split equally.
            List<double> lengths = m_pendingChildSizes;
            if ((lengths == null) && anyPassedAlong)
            {
                double total = passedAlong.Sum();
                lengths = passedAlong.Select(l => splitLength * (l / total)).ToList();
                DockZoneViewModel.EnforceMinPanelDimensions(lengths, splitLength, m_split.MinPanelSize);
            }

            m_split.SetPaneLengths(lengths ?? passedAlong);
            m_pendingChildSizes = null;
            m_needsPaneSizing = false;
        }

        /// <summary>Shows or hides the drop overlay, centered over this zone, while a docking drag is over it</summary>
        internal void ShowDropOverlay (bool isShown)
        {
            m_dropOverlay?.RemoveFromSuperview();
            m_dropOverlay = null;
            if (!isShown || m_isTornDown)
            {
                return;
            }

            m_dropOverlay = new DockDropOverlayView(m_zone.Orientation);
            m_dropOverlay.TranslatesAutoresizingMaskIntoConstraints = false;
            this.AddSubview(m_dropOverlay);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_dropOverlay.WidthAnchor.ConstraintEqualTo((nfloat)DockDragRules.kOverlayWidth),
                m_dropOverlay.HeightAnchor.ConstraintEqualTo((nfloat)DockDragRules.kOverlayHeight),
                m_dropOverlay.CenterXAnchor.ConstraintEqualTo(this.CenterXAnchor),
                m_dropOverlay.CenterYAnchor.ConstraintEqualTo(this.CenterYAnchor),
            });
        }

        /// <summary>The overlay widget at <paramref name="windowPoint"/> (in this view's window), or null when the overlay isn't showing or no widget there takes a drop</summary>
        internal DockDropOverlayView.InsertionWidget FindDropWidget (CGPoint windowPoint)
            => m_dropOverlay?.FindWidgetAt(m_dropOverlay.ConvertPointFromView(windowPoint, null));

        // ===========[ Helper Methods ]========================================
        private void ClearLayout ()
        {
            m_dropOverlay = null;
            m_layoutBindings?.Dispose();
            m_layoutBindings = null;
            foreach (DockZoneView child in m_childViews)
            {
                child.Teardown();
            }

            m_childViews.Clear();
            m_split = null;
            foreach (NSView subview in this.Subviews)
            {
                subview.RemoveFromSuperview();
            }
        }

        private void RebuildLayout ()
        {
            if (m_isTornDown)
            {
                return;
            }

            this.ClearLayout();
            m_layoutBindings = new BindingSet();
            this.BackgroundColorKey = null;
            this.BorderColorKey = null;

            NSView content;
            if ((m_zone.Orientation & eDockOrientation.AnySplitOrientation) != 0)
            {
                content = this.BuildSplit();
            }
            else if (m_zone.DockedContent.Count > 0)
            {
                content = this.BuildLeaf();
            }
            else
            {
                content = this.BuildEmpty();
            }

            content.TranslatesAutoresizingMaskIntoConstraints = false;
            this.AddSubview(content);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                content.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                content.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                content.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                content.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
            });
        }

        private NSView BuildSplit ()
        {
            m_split = new ThemedSplitView(isVertical: m_zone.Orientation == eDockOrientation.Horizontal)
            {
                MinPanelSize = m_zone.Manager?.MinPanelDimension ?? kFallbackMinPanelSize,
            };

            foreach (DockZoneViewModel child in m_zone.Children)
            {
                var childView = new DockZoneView(child);
                m_childViews.Add(childView);
                m_split.AddArrangedSubview(childView);
            }

            // The panes are sized on this view's next layout with a real size, from their zones' pass-along sizes
            m_needsPaneSizing = true;
            this.NeedsLayout = true;
            return m_split;
        }

        private NSView BuildEmpty ()
        {
            this.BackgroundColorKey = ThemedColorKeys.DockZone_EmptyBackground;
            this.BorderColorKey = ThemedColorKeys.DockZone_EmptyBorder;
            this.BorderThickness = 1.0;

            NSTextField label = NSTextField.CreateLabel("Empty");
            label.TextColor = ThemedColors.Get(ThemedColorKeys.DockZone_EmptyForeground);
            label.Alignment = NSTextAlignment.Center;
            return label;
        }

        private NSView BuildLeaf ()
        {
            this.BackgroundColorKey = ThemedColorKeys.DockZone_PanelBackground;
            this.BorderColorKey = ThemedColorKeys.DockZone_PanelBorder;
            this.BorderThickness = 1.0;

            int selected = Math.Clamp(m_zone.SelectedIndex, 0, m_zone.DockedContent.Count - 1);
            DockingContentAdapterModel shown = m_zone.DockedContent[selected];

            var stack = new FillStack(NSUserInterfaceLayoutOrientation.Vertical);
            stack.Add(this.BuildHeader(shown));

            var displayHost = new NSView();
            if (shown.Display is NSView display)
            {
                display.RemoveFromSuperview();
                display.TranslatesAutoresizingMaskIntoConstraints = false;
                displayHost.AddSubview(display);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    display.TopAnchor.ConstraintEqualTo(displayHost.TopAnchor),
                    display.BottomAnchor.ConstraintEqualTo(displayHost.BottomAnchor),
                    display.LeadingAnchor.ConstraintEqualTo(displayHost.LeadingAnchor),
                    display.TrailingAnchor.ConstraintEqualTo(displayHost.TrailingAnchor),
                });
            }

            stack.Add(displayHost, grows: true);
            if (m_zone.DockedContent.Count > 1)
            {
                stack.Add(new TabStrip(this, selected));
            }

            return stack;
        }

        private NSView BuildHeader (DockingContentAdapterModel shown)
        {
            var header = new HeaderView(this, shown);
            header.TranslatesAutoresizingMaskIntoConstraints = false;
            header.HeightAnchor.ConstraintEqualTo((nfloat)kHeaderHeight).Active = true;
            return header;
        }

        private string ReadTitle (DockingContentAdapterModel adapter) => adapter.TitleContent?.ToString() ?? String.Empty;

        private void ClosePanel (DockingContentAdapterModel adapter)
        {
            m_zone.RequestCloseAndRemoveDockedContent(adapter);
        }

        // The panel menu: the panel's own options, then Tear Off where the panel allows it, then Hide
        // or Close where it can be closed, as on WinUI3
        private NSMenu BuildPanelMenu (DockingContentAdapterModel adapter)
        {
            var menu = new NSMenu { AutoEnablesItems = false };
            if (adapter.AdditionalContextMenuItems != null)
            {
                foreach (DockPanelMenuOption option in adapter.AdditionalContextMenuItems)
                {
                    if (option.IsSeparator)
                    {
                        menu.AddItem(NSMenuItem.SeparatorItem);
                    }
                    else
                    {
                        menu.AddItem(this.MakeMenuItem(option.Title, new PanelAction(adapter, option.Action)));
                    }
                }

                if ((adapter.AdditionalContextMenuItems.Count > 0)
                    && (adapter.CanTearoff || adapter.IsClosable))
                {
                    menu.AddItem(NSMenuItem.SeparatorItem);
                }
            }

            if (adapter.CanTearoff && (m_zone.Manager is DockingManager))
            {
                menu.AddItem(this.MakeMenuItem("Tear Off Panel", new PanelAction(adapter, this.TearOffPanel)));
            }

            if (adapter.IsClosable)
            {
                menu.AddItem(this.MakeMenuItem(adapter.HideDontClose ? "Hide Panel" : "Close Panel", new PanelAction(adapter, this.ClosePanel)));
            }

            return menu;
        }

        private NSMenuItem MakeMenuItem (string title, PanelAction action)
        {
            var item = new NSMenuItem(title ?? String.Empty);
            m_layoutBindings.BindCommand(item, action);
            return item;
        }

        private void SelectTab (int index)
        {
            if (index != m_zone.SelectedIndex)
            {
                m_zone.SelectedIndex = index;
            }
        }

        private void ReorderTab (int fromIndex, int toIndex)
        {
            if (DockDragRules.ShouldCommitTabReorder(fromIndex, toIndex, m_zone.DockedContent.Count)
                && m_zone.SwapDockedContentOrder(fromIndex, toIndex))
            {
                m_zone.SelectedIndex = toIndex;
            }
        }

        private void TearOffPanel (DockingContentAdapterModel adapter)
        {
            (m_zone.Manager as DockingManager)?.TearOffPanel(adapter);
        }

        // A header drag past the threshold: the manager decides what it moves and runs the drag. The
        // press is in this zone's own points (top-left origin) and in AppKit screen coordinates.
        private void BeginHeaderDrag (CGPoint pressInZone, CGPoint pressOnScreen)
        {
            (m_zone.Manager as DockingManager)?.DragFromHeader(this, pressInZone, pressOnScreen);
        }

        // A tab dragged up or down out of the strip: the manager tears its panel off and runs the drag
        private void BeginTabTearoff (DockingContentAdapterModel adapter)
        {
            (m_zone.Manager as DockingManager)?.DragPanel(adapter);
        }

        // ===========[ Subclasses/structs ]====================================
        // A menu item's command for one panel; the BindingSet that holds it is replaced on rebuild
        private sealed class PanelAction : System.Windows.Input.ICommand
        {
            private readonly DockingContentAdapterModel m_adapter;
            private readonly Action<DockingContentAdapterModel> m_action;

            public PanelAction (DockingContentAdapterModel adapter, Action<DockingContentAdapterModel> action)
            {
                m_adapter = adapter;
                m_action = action;
            }

            // CanExecute never changes, so nothing is ever raised
            public event EventHandler CanExecuteChanged { add { } remove { } }

            public bool CanExecute (object parameter) => m_action != null;
            public void Execute (object parameter) => m_action?.Invoke(m_adapter);
        }

        // The selected panel's title, its close button, and the panel menu on a right click. Pressing
        // anywhere else on it and dragging past the threshold starts a header drag.
        private sealed class HeaderView : ThemedView
        {
            private readonly DockZoneView m_owner;
            private bool m_isPressPending;
            private CGPoint m_pressInZone;
            private CGPoint m_pressOnScreen;

            public HeaderView (DockZoneView owner, DockingContentAdapterModel shown)
            {
                m_owner = owner;
                this.BackgroundColorKey = ThemedColorKeys.DockZone_HeaderBackground;

                var row = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: 6.0)
                {
                    Padding = new NSEdgeInsets(0, 8, 0, 4),
                };
                row.TranslatesAutoresizingMaskIntoConstraints = false;
                this.AddSubview(row);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    row.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                    row.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                    row.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                    row.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                });

                NSTextField title = NSTextField.CreateLabel(String.Empty);
                title.LineBreakMode = NSLineBreakMode.TruncatingTail;
                title.TextColor = ThemedColors.Get(ThemedColorKeys.DockZone_HeaderForeground);
                owner.m_layoutBindings.BindLabel(title, shown, nameof(DockingContentAdapterModel.TitleContent), _ReadTitle);
                row.Add(title, grows: true, alignment: eFillStackCrossAlignment.Center);

                if (shown.IsClosable)
                {
                    var close = new GeometryButton
                    {
                        GeometryMarkup = "M6,6 L14,14 M14,6 L6,14",
                        GeometryStretch = AJut.UX.Helpers.ePathStretch.None,
                        TraceThickness = 1.5,
                        ToolTip = shown.HideDontClose ? "Hide" : "Close",
                    };
                    close.TranslatesAutoresizingMaskIntoConstraints = false;
                    NSLayoutConstraint.ActivateConstraints(new[]
                    {
                        close.WidthAnchor.ConstraintEqualTo((nfloat)kCloseButtonSize),
                        close.HeightAnchor.ConstraintEqualTo((nfloat)kCloseButtonSize),
                    });
                    owner.m_layoutBindings.BindCommand(close, new PanelAction(shown, owner.ClosePanel));
                    row.Add(close, alignment: eFillStackCrossAlignment.Center);
                }

                this.Menu = owner.BuildPanelMenu(shown);

                string _ReadTitle () => owner.ReadTitle(shown);
            }

            // The title is a label, which would take the press itself; the header takes it instead so the title is
            // something to drag by. The close button still takes its own.
            public override NSView HitTest (CGPoint point)
            {
                NSView hit = base.HitTest(point);
                return hit is NSTextField ? this : hit;
            }

            public override void MouseDown (NSEvent theEvent)
            {
                m_pressInZone = m_owner.ConvertPointFromView(theEvent.LocationInWindow, null);
                m_pressOnScreen = this.Window?.ConvertPointToScreen(theEvent.LocationInWindow) ?? CGPoint.Empty;
                m_isPressPending = this.Window != null;
            }

            public override void MouseDragged (NSEvent theEvent)
            {
                if (!m_isPressPending)
                {
                    return;
                }

                CGPoint now = m_owner.ConvertPointFromView(theEvent.LocationInWindow, null);
                double dx = (double)(now.X - m_pressInZone.X);
                double dy = (double)(now.Y - m_pressInZone.Y);
                if (DockDragRules.HasMovedPastThreshold(dx, dy, m_owner.DragThreshold))
                {
                    m_isPressPending = false;
                    m_owner.BeginHeaderDrag(m_pressInZone, m_pressOnScreen);
                }
            }

            public override void MouseUp (NSEvent theEvent)
            {
                m_isPressPending = false;
            }
        }

        // The tabs of a Tabbed zone, along the bottom, drawn the way WinUI3 draws them. The selected tab is in the panel's
        // fill, full height and with no top edge, over the line the strip draws along its top, so it reads as part of the
        // panel above it. The other tabs sit below that line, a little shorter, and dimmed.
        //
        // Tabs keep their own widths. When they don't all fit, the strip scrolls sideways: a scroll button shows at each
        // end (disabled once there is nothing further that way), and the wheel or a sideways trackpad swipe scrolls it too.
        // The offset lives on the zone view, since the strip is rebuilt on every selection change, and each new strip
        // brings the selected tab into view.
        //
        // The strip takes the mouse for its tabs (a tab's close button takes its own). A click selects, and a middle click
        // closes. A press that moves past the threshold is decided once, by the shared rule: along the strip it reorders,
        // up or down out of it tears the tab's panel off, when the panel allows it. While a reorder runs, a ghost of the
        // tab follows the pointer along the strip and a line shows where it will land; the reorder itself is the VM's, on
        // release.
        private sealed class TabStrip : ThemedView
        {
            private const double kScrollStep = 80.0;
            private const double kDragInsertLineWidth = 2.0;

            private readonly DockZoneView m_owner;
            private readonly int m_selectedIndex;
            private readonly ThemedView m_topLine;
            private readonly ThemedView m_clip;
            private readonly FillStack m_tabs;
            private readonly GeometryButton m_scrollBack;
            private readonly GeometryButton m_scrollForward;
            private readonly List<double> m_tabWidths = new List<double>();
            private ThemedView m_dragGhost;
            private ThemedView m_dragInsertLine;
            private int m_dragFromIndex = -1;
            private int m_dragTargetIndex = -1;
            private CGPoint m_dragStart;
            private bool m_isDragging;
            private bool m_needsSelectedTabInView = true;

            public TabStrip (DockZoneView owner, int selectedIndex)
            {
                m_owner = owner;
                m_selectedIndex = selectedIndex;
                this.BackgroundColorKey = ThemedColorKeys.DockZone_TabStripBackground;
                this.TranslatesAutoresizingMaskIntoConstraints = false;
                this.HeightAnchor.ConstraintEqualTo((nfloat)kTabHeight).Active = true;

                // Placed by frame in Layout, back to front: the line along the top, the tabs clipped to the space between
                //  the scroll buttons (the selected tab covers the line), then the scroll buttons
                m_topLine = new ThemedView { BackgroundColorKey = ThemedColorKeys.DockZone_PanelBorder };
                this.AddSubview(m_topLine);

                m_clip = new ThemedView();
                if (m_clip.Layer != null)
                {
                    m_clip.Layer.MasksToBounds = true;
                }

                this.AddSubview(m_clip);

                m_tabs = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal);
                m_clip.AddSubview(m_tabs);

                // Edges, as WinUI3 gives each tab: a left edge on the first tab and the selected one, a right edge on every
                //  tab but the one just before the selected one (the selected tab draws that edge itself), and a bottom
                //  edge on all of them. None gets a top edge.
                bool showsClose = (owner.m_zone.Manager as DockingManager)?.ShowTabHeaderClose ?? true;
                for (int i = 0; i < owner.m_zone.DockedContent.Count; ++i)
                {
                    bool isSelected = i == selectedIndex;
                    m_tabs.Add(new TabView(owner, i, isSelected, hasLeftEdge: (i == 0) || isSelected, hasRightEdge: (i + 1) != selectedIndex, showsClose));
                }

                m_scrollBack = this.MakeScrollButton("M10,5 L6,10 L10,15", "Scroll tabs back", this.ScrollBack);
                m_scrollForward = this.MakeScrollButton("M6,5 L10,10 L6,15", "Scroll tabs forward", this.ScrollForward);
            }

            public override void Layout ()
            {
                const double kScrollButtonWidth = 16.0;

                base.Layout();
                double width = (double)this.Bounds.Width;
                double height = (double)this.Bounds.Height;
                if (width <= 0.0)
                {
                    return;
                }

                m_topLine.Frame = new CGRect(0, 0, (nfloat)width, (nfloat)kEdgeThickness);

                // 1. Each tab is as wide as it wants to be, and the tabs sit edge to edge
                m_tabWidths.Clear();
                double contentWidth = 0.0;
                foreach (NSView tab in m_tabs.Children)
                {
                    double tabWidth = (double)tab.FittingSize.Width;
                    m_tabWidths.Add(tabWidth);
                    contentWidth += tabWidth;
                }

                // 2. Scroll when they don't fit, bringing the selected tab into view on the first layout after a rebuild
                bool isScrolling = contentWidth > width;
                double buttonWidth = isScrolling ? kScrollButtonWidth : 0.0;
                double viewport = Math.Max(0.0, width - (2 * buttonWidth));
                double maxOffset = Math.Max(0.0, contentWidth - viewport);
                double offset = isScrolling ? m_owner.m_tabScrollOffset : 0.0;
                if (m_needsSelectedTabInView && isScrolling && (m_selectedIndex < m_tabWidths.Count))
                {
                    double selectedStart = 0.0;
                    for (int i = 0; i < m_selectedIndex; ++i)
                    {
                        selectedStart += m_tabWidths[i];
                    }

                    double selectedEnd = selectedStart + m_tabWidths[m_selectedIndex];
                    if (selectedStart < offset)
                    {
                        offset = selectedStart;
                    }
                    else if (selectedEnd > offset + viewport)
                    {
                        offset = selectedEnd - viewport;
                    }
                }

                m_needsSelectedTabInView = false;
                offset = Math.Clamp(offset, 0.0, maxOffset);
                m_owner.m_tabScrollOffset = offset;

                // 3. Place the clipped tabs and the scroll buttons, which sit under the top line
                m_clip.Frame = new CGRect((nfloat)buttonWidth, 0, (nfloat)viewport, (nfloat)height);
                m_tabs.Frame = new CGRect((nfloat)(-offset), 0, (nfloat)contentWidth, (nfloat)height);

                double buttonHeight = Math.Max(0.0, height - kEdgeThickness);
                m_scrollBack.Frame = new CGRect(0, (nfloat)kEdgeThickness, (nfloat)buttonWidth, (nfloat)buttonHeight);
                m_scrollForward.Frame = new CGRect((nfloat)(width - buttonWidth), (nfloat)kEdgeThickness, (nfloat)buttonWidth, (nfloat)buttonHeight);
                m_scrollBack.Hidden = !isScrolling;
                m_scrollForward.Hidden = !isScrolling;
                m_scrollBack.Enabled = offset > 0.0;
                m_scrollForward.Enabled = offset < maxOffset;
            }

            public override void ScrollWheel (NSEvent theEvent)
            {
                // A mouse wheel only ever reports up and down, a trackpad swipe can go sideways; the larger of the two
                //  scrolls the strip. A line based wheel reports its delta in lines, a trackpad in points.
                const double kWheelLineDistance = 10.0;

                double deltaX = (double)theEvent.ScrollingDeltaX;
                double deltaY = (double)theEvent.ScrollingDeltaY;
                double delta = Math.Abs(deltaX) >= Math.Abs(deltaY) ? deltaX : deltaY;
                if (!theEvent.HasPreciseScrollingDeltas)
                {
                    delta *= kWheelLineDistance;
                }

                if (m_scrollBack.Hidden || (delta == 0.0))
                {
                    base.ScrollWheel(theEvent);
                    return;
                }

                this.ScrollBy(-delta);
            }

            public override void MouseDown (NSEvent theEvent)
            {
                m_dragFromIndex = this.TabIndexAt(theEvent, clampToLast: false);
                m_dragStart = this.ConvertPointFromView(theEvent.LocationInWindow, null);
                m_isDragging = false;
            }

            public override void MouseDragged (NSEvent theEvent)
            {
                if ((m_dragFromIndex < 0) || (m_dragFromIndex >= m_owner.m_zone.DockedContent.Count))
                {
                    return;
                }

                if (m_isDragging)
                {
                    this.UpdateReorderFeedback(theEvent);
                    return;
                }

                CGPoint now = this.ConvertPointFromView(theEvent.LocationInWindow, null);
                DockingContentAdapterModel tab = m_owner.m_zone.DockedContent[m_dragFromIndex];
                switch (DockDragRules.DecideTabDrag(tab, (double)(now.X - m_dragStart.X), (double)(now.Y - m_dragStart.Y), m_owner.DragThreshold))
                {
                    case eTabDragKind.Reorder:
                        m_isDragging = true;
                        this.BeginReorderFeedback();
                        this.UpdateReorderFeedback(theEvent);
                        break;

                    case eTabDragKind.Tearoff:
                        // The drag runs to its end inside this call, and this strip is usually rebuilt away by then
                        m_dragFromIndex = -1;
                        m_owner.BeginTabTearoff(tab);
                        break;
                }
            }

            public override void MouseUp (NSEvent theEvent)
            {
                int fromIndex = m_dragFromIndex;
                int targetIndex = m_dragTargetIndex;
                bool wasDragging = m_isDragging;
                this.EndReorderFeedback();
                if (fromIndex < 0)
                {
                    return;
                }

                if (wasDragging)
                {
                    m_owner.ReorderTab(fromIndex, targetIndex);
                }
                else if (this.TabIndexAt(theEvent, clampToLast: false) == fromIndex)
                {
                    m_owner.SelectTab(fromIndex);
                }
            }

            public override void OtherMouseUp (NSEvent theEvent)
            {
                const long kMiddleMouseButton = 2;

                if ((theEvent.ButtonNumber != kMiddleMouseButton)
                    || !((m_owner.m_zone.Manager as DockingManager)?.AllowMiddleMouseClose ?? true))
                {
                    base.OtherMouseUp(theEvent);
                    return;
                }

                int index = this.TabIndexAt(theEvent, clampToLast: false);
                if ((index >= 0) && (index < m_owner.m_zone.DockedContent.Count))
                {
                    DockingContentAdapterModel adapter = m_owner.m_zone.DockedContent[index];
                    if (adapter.IsClosable)
                    {
                        m_owner.ClosePanel(adapter);
                    }
                }
            }

            private GeometryButton MakeScrollButton (string markup, string toolTip, Action scroll)
            {
                var button = new GeometryButton
                {
                    GeometryMarkup = markup,
                    GeometryStretch = AJut.UX.Helpers.ePathStretch.None,
                    TraceThickness = 1.5,
                    ToolTip = toolTip,
                    Hidden = true,
                };

                m_owner.m_layoutBindings.BindCommand(button, new ActionCommand(scroll));
                this.AddSubview(button);
                return button;
            }

            private void ScrollBack () => this.ScrollBy(-kScrollStep);
            private void ScrollForward () => this.ScrollBy(kScrollStep);

            // How far the tabs can go is only known in Layout, which clamps the offset
            private void ScrollBy (double distance)
            {
                m_owner.m_tabScrollOffset = Math.Max(0.0, m_owner.m_tabScrollOffset + distance);
                this.NeedsLayout = true;
            }

            // The tab under the pointer, by the shared rule, in the tabs' own (scrolled) coordinates. Past either end is
            // the end tab when clamped, or no tab (-1).
            private int TabIndexAt (NSEvent theEvent, bool clampToLast)
            {
                double x = (double)m_tabs.ConvertPointFromView(theEvent.LocationInWindow, null).X;
                if (m_tabWidths.Count == 0)
                {
                    return -1;
                }

                if (!clampToLast && ((x < 0.0) || (x >= m_tabWidths.Sum())))
                {
                    return -1;
                }

                return DockDragRules.FindTabIndexAtX(m_tabWidths, x);
            }

            // A copy of the dragged tab that follows the pointer along the strip, and the line where it will land
            private void BeginReorderFeedback ()
            {
                const double kDragGhostOpacity = 0.85;

                DockingContentAdapterModel adapter = m_owner.m_zone.DockedContent[m_dragFromIndex];
                double ghostWidth = (m_dragFromIndex < m_tabWidths.Count) ? m_tabWidths[m_dragFromIndex] : kTabHeight;
                m_dragGhost = new ThemedView
                {
                    BackgroundColorKey = ThemedColorKeys.DockZone_TabBackground,
                    BorderColorKey = ThemedColorKeys.DockZone_PanelBorder,
                    BorderThickness = kEdgeThickness,
                    AlphaValue = (nfloat)kDragGhostOpacity,
                    Frame = new CGRect(0, 0, (nfloat)ghostWidth, this.Bounds.Height),
                };

                NSTextField title = NSTextField.CreateLabel(m_owner.ReadTitle(adapter));
                title.TextColor = ThemedColors.Get(ThemedColorKeys.DockZone_HeaderForeground);
                title.LineBreakMode = NSLineBreakMode.TruncatingTail;
                title.TranslatesAutoresizingMaskIntoConstraints = false;
                m_dragGhost.AddSubview(title);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    title.CenterYAnchor.ConstraintEqualTo(m_dragGhost.CenterYAnchor),
                    title.LeadingAnchor.ConstraintEqualTo(m_dragGhost.LeadingAnchor, (nfloat)kTabTitleInset),
                    title.TrailingAnchor.ConstraintLessThanOrEqualTo(m_dragGhost.TrailingAnchor, (nfloat)(-kTabTitleInset)),
                });

                m_dragInsertLine = new ThemedView
                {
                    BackgroundColorKey = ThemedColorKeys.DockZone_DragInsertLine,
                    Hidden = true,
                    Frame = new CGRect(0, 0, (nfloat)kDragInsertLineWidth, this.Bounds.Height),
                };

                this.AddSubview(m_dragInsertLine);
                this.AddSubview(m_dragGhost);
                m_dragTargetIndex = m_dragFromIndex;
            }

            private void UpdateReorderFeedback (NSEvent theEvent)
            {
                if ((m_dragGhost == null) || (m_dragInsertLine == null))
                {
                    return;
                }

                // The ghost stays in the strip and only moves along it, centered on the pointer
                double pointerX = (double)this.ConvertPointFromView(theEvent.LocationInWindow, null).X;
                double ghostWidth = (double)m_dragGhost.Frame.Width;
                double maxGhostX = Math.Max(0.0, (double)this.Bounds.Width - ghostWidth);
                m_dragGhost.SetFrameOrigin(new CGPoint((nfloat)Math.Clamp(pointerX - (ghostWidth / 2.0), 0.0, maxGhostX), 0));

                m_dragTargetIndex = this.TabIndexAt(theEvent, clampToLast: true);
                double? lineX = DockDragRules.FindInsertLineX(m_tabWidths, m_dragFromIndex, m_dragTargetIndex);
                if (lineX == null)
                {
                    m_dragInsertLine.Hidden = true;
                    return;
                }

                // From the tabs' coordinates to the strip's, centered on the boundary and kept over the visible tabs
                double clipStart = (double)m_clip.Frame.X;
                double clipEnd = Math.Max(clipStart, clipStart + (double)m_clip.Frame.Width - kDragInsertLineWidth);
                double stripX = clipStart + (double)m_tabs.Frame.X + lineX.Value - (kDragInsertLineWidth / 2.0);
                m_dragInsertLine.SetFrameOrigin(new CGPoint((nfloat)Math.Clamp(stripX, clipStart, clipEnd), 0));
                m_dragInsertLine.Hidden = false;
            }

            private void EndReorderFeedback ()
            {
                m_dragGhost?.RemoveFromSuperview();
                m_dragGhost = null;
                m_dragInsertLine?.RemoveFromSuperview();
                m_dragInsertLine = null;
                m_dragFromIndex = -1;
                m_dragTargetIndex = -1;
                m_isDragging = false;
            }
        }

        // One tab: its title and, for a closable panel, an X that closes it, on a face drawn with the edges the strip
        // asks for. The strip takes the mouse; this makes the tab something VoiceOver can find and pick, as one of a set
        // of radio buttons named by the tab's title (its text field is left out, so the title isn't read twice, and its
        // close button is the one child it shows).
        private sealed class TabView : NSView
        {
            // Unselected tabs sit this much short of the strip's bottom, and this faint. WinUI3 dims them further (0.35)
            //  and brightens one under the pointer; this has no hover look, so it stays readable at rest instead.
            private const double kUnselectedBottomInset = 2.0;
            private const double kUnselectedOpacity = 0.5;
            private const double kTabCloseSize = 16.0;
            private const double kTabCloseGap = 2.0;

            private readonly DockZoneView m_owner;
            private readonly int m_index;
            private readonly bool m_isSelected;
            private readonly NSTextField m_title;
            private readonly GeometryButton m_close;

            public TabView (DockZoneView owner, int index, bool isSelected, bool hasLeftEdge, bool hasRightEdge, bool showsClose)
            {
                m_owner = owner;
                m_index = index;
                m_isSelected = isSelected;

                DockingContentAdapterModel adapter = owner.m_zone.DockedContent[index];
                this.ToolTip = adapter.TooltipContent?.ToString();
                this.Menu = owner.BuildPanelMenu(adapter);

                var face = new ThemedView
                {
                    BackgroundColorKey = isSelected ? ThemedColorKeys.DockZone_SelectedTabBackground : ThemedColorKeys.DockZone_TabBackground,
                    AlphaValue = isSelected ? (nfloat)1.0 : (nfloat)kUnselectedOpacity,
                    TranslatesAutoresizingMaskIntoConstraints = false,
                };

                this.AddSubview(face);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    face.TopAnchor.ConstraintEqualTo(this.TopAnchor, (nfloat)(isSelected ? 0.0 : kEdgeThickness)),
                    face.BottomAnchor.ConstraintEqualTo(this.BottomAnchor, (nfloat)(isSelected ? 0.0 : -kUnselectedBottomInset)),
                    face.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                    face.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                });

                m_title = NSTextField.CreateLabel(String.Empty);
                m_title.TextColor = ThemedColors.Get(ThemedColorKeys.DockZone_HeaderForeground);
                m_title.TranslatesAutoresizingMaskIntoConstraints = false;
                owner.m_layoutBindings.BindLabel(m_title, adapter, nameof(DockingContentAdapterModel.TitleContent), _ReadTitle);
                face.AddSubview(m_title);

                var constraints = new List<NSLayoutConstraint>
                {
                    m_title.CenterYAnchor.ConstraintEqualTo(face.CenterYAnchor),
                    m_title.LeadingAnchor.ConstraintEqualTo(face.LeadingAnchor, (nfloat)kTabTitleInset),
                };

                if (showsClose && adapter.IsClosable)
                {
                    m_close = new GeometryButton
                    {
                        GeometryMarkup = "M4,4 L12,12 M12,4 L4,12",
                        GeometryStretch = AJut.UX.Helpers.ePathStretch.None,
                        TraceThickness = 1.5,
                        ToolTip = adapter.HideDontClose ? "Hide" : "Close",
                        TranslatesAutoresizingMaskIntoConstraints = false,
                    };

                    owner.m_layoutBindings.BindCommand(m_close, new PanelAction(adapter, owner.ClosePanel));
                    face.AddSubview(m_close);
                    constraints.Add(m_close.WidthAnchor.ConstraintEqualTo((nfloat)kTabCloseSize));
                    constraints.Add(m_close.HeightAnchor.ConstraintEqualTo((nfloat)kTabCloseSize));
                    constraints.Add(m_close.CenterYAnchor.ConstraintEqualTo(face.CenterYAnchor));
                    constraints.Add(m_close.LeadingAnchor.ConstraintEqualTo(m_title.TrailingAnchor, (nfloat)kTabCloseGap));
                    constraints.Add(m_close.TrailingAnchor.ConstraintEqualTo(face.TrailingAnchor, (nfloat)(-kTabCloseGap)));
                }
                else
                {
                    constraints.Add(m_title.TrailingAnchor.ConstraintEqualTo(face.TrailingAnchor, (nfloat)(-kTabTitleInset)));
                }

                NSLayoutConstraint.ActivateConstraints(constraints.ToArray());

                if (hasLeftEdge)
                {
                    AddEdge(face, eTabEdge.Left);
                }

                if (hasRightEdge)
                {
                    AddEdge(face, eTabEdge.Right);
                }

                AddEdge(face, eTabEdge.Bottom);

                string _ReadTitle () => owner.ReadTitle(adapter);
            }

            public override bool IsFlipped => true;

            public override bool AccessibilityElement => true;
            public override string AccessibilityRole => NSAccessibilityRoles.RadioButtonRole;
            public override string AccessibilityLabel => m_title.StringValue;
            public override bool AccessibilitySelected => m_isSelected;
            public override NSObject[] AccessibilityChildren => (m_close != null) ? new NSObject[] { m_close } : null;

            public override bool AccessibilityPerformPress ()
            {
                m_owner.SelectTab(m_index);
                return true;
            }

            // A line in the border color along one edge of the face, standing in for WinUI3's per-side border thickness
            private static void AddEdge (ThemedView face, eTabEdge side)
            {
                var edge = new ThemedView
                {
                    BackgroundColorKey = ThemedColorKeys.DockZone_PanelBorder,
                    TranslatesAutoresizingMaskIntoConstraints = false,
                };

                face.AddSubview(edge);
                if (side == eTabEdge.Bottom)
                {
                    NSLayoutConstraint.ActivateConstraints(new[]
                    {
                        edge.LeadingAnchor.ConstraintEqualTo(face.LeadingAnchor),
                        edge.TrailingAnchor.ConstraintEqualTo(face.TrailingAnchor),
                        edge.BottomAnchor.ConstraintEqualTo(face.BottomAnchor),
                        edge.HeightAnchor.ConstraintEqualTo((nfloat)kEdgeThickness),
                    });

                    return;
                }

                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    edge.TopAnchor.ConstraintEqualTo(face.TopAnchor),
                    edge.BottomAnchor.ConstraintEqualTo(face.BottomAnchor),
                    (side == eTabEdge.Left)
                        ? edge.LeadingAnchor.ConstraintEqualTo(face.LeadingAnchor)
                        : edge.TrailingAnchor.ConstraintEqualTo(face.TrailingAnchor),
                    edge.WidthAnchor.ConstraintEqualTo((nfloat)kEdgeThickness),
                });
            }

            private enum eTabEdge { Left, Right, Bottom }
        }
    }
}
