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
    using ObjCRuntime;

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
    //  - A leaf (Single or Tabbed) is laid out the way WinUI3's DockLeafLayout is: a header (the
    //    selected panel's title, and its close button) with its own outline, over the selected
    //    panel's display in a filled, outlined frame whose bottom corners are rounded, and for
    //    Tabbed a tab strip below the frame, inset from the zone's edges, with no fill or outline
    //    of its own.
    //  - An empty zone shows a placeholder while the pointer is over it.
    //
    // A panel's display is the IDockableDisplayElement the adapter carries, which on AppKit has
    // to be an NSView. Titles and tooltips follow the adapter live.
    //
    // The header shows the panel being typed into (focus within the zone, in the key window) in
    // the focused header colors, as WinUI3's and WPF's do. See "Panel focus" below for how.
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
        private readonly NSTrackingArea m_hoverTrackingArea;
        private BindingSet m_layoutBindings;
        private ThemedSplitView m_split;
        private HeaderView m_header;
        private NSTextField m_emptyPlaceholder;
        private DockDropOverlayView m_dropOverlay;
        private List<double> m_pendingChildSizes;
        private bool m_needsPaneSizing;
        private CGSize m_lastLaidOutSize;
        private double m_tabScrollOffset;
        private bool m_isPointerOver;
        private NSWindow m_watchedWindow;
        private IDisposable m_firstResponderWatch;
        private NSObject m_becameKeyWatch;
        private NSObject m_resignedKeyWatch;
        private bool m_isTornDown;

        // ===========[ Setup/Construction/Teardown ]===========================
        public DockZoneView (DockZoneViewModel zone)
        {
            m_zone = zone;
            m_zone.UI = this;
            m_zone.PropertyChanged += this.OnZonePropertyChanged;
            ((INotifyCollectionChanged)m_zone.Children).CollectionChanged += this.OnZoneCollectionChanged;
            ((INotifyCollectionChanged)m_zone.DockedContent).CollectionChanged += this.OnZoneCollectionChanged;

            // A panel takes focus for a press inside it (see TakeFocusForPress), and shows that on its header, not as a ring
            this.FocusRingType = NSFocusRingType.None;

            // For the empty placeholder, which shows only under the pointer. One area for the view's whole life, kept in
            //  step with its visible rect by AppKit, as ManagedButton's is.
            m_hoverTrackingArea = new NSTrackingArea(
                CGRect.Empty,
                NSTrackingAreaOptions.MouseEnteredAndExited | NSTrackingAreaOptions.ActiveInActiveApp | NSTrackingAreaOptions.InVisibleRect,
                this,
                null
            );
            this.AddTrackingArea(m_hoverTrackingArea);

            this.RebuildLayout();
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native view whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls: the native view keeps the views it has. None of its
        /// managed state survives a dispose, so a call that needs that state can still fail: dispose it only once AppKit
        /// is done with it.
        /// </summary>
        protected DockZoneView (NativeHandle handle) : base(handle)
        {
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
            this.WatchWindow(null);
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

        // The window's first responder moved, or the window became or stopped being key: see "Panel focus"
        private void OnWindowFirstResponderChanged (NSObservedChange change)
        {
            this.RefreshPanelFocus();
        }

        private void OnWindowKeyStateChanged (object sender, NSNotificationEventArgs e)
        {
            this.RefreshPanelFocus();
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

            // A zone's size is part of what a layout saves, so a new size goes to the manager, which saves once the change
            //  settles when it auto-saves
            if (this.Bounds.Size != m_lastLaidOutSize)
            {
                m_lastLaidOutSize = this.Bounds.Size;
                (m_zone.Manager as DockingManager)?.OnZoneSizeChanged();
            }

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

        public override void ViewDidMoveToWindow ()
        {
            base.ViewDidMoveToWindow();
            this.WatchWindow(m_isTornDown ? null : this.Window);
            this.RefreshPanelFocus();
        }

        /// <summary>
        /// Only a panel takes focus for itself, from a press (see TakeFocusForPress). A split leaves that to the panels in
        /// it, and an empty zone has nothing to type into.
        /// </summary>
        public override bool AcceptsFirstResponder () => m_header != null;

        /// <summary>
        /// A panel takes focus from a press only, never from Tab: it draws no focus ring, so Tab stopping on it would look
        /// like focus had gone nowhere. Tab moves between the controls inside the panels.
        /// </summary>
        public override bool CanBecomeKeyView => false;

        /// <summary>
        /// A press inside a panel that nothing under it took gives the panel focus and stops there, as WinUI3's does.
        /// Anywhere else it carries on up.
        /// </summary>
        public override void MouseDown (NSEvent theEvent)
        {
            if (!this.TakeFocusForPress())
            {
                base.MouseDown(theEvent);
            }
        }

        /// <summary>Only this view's own area counts: an entered or exited passed up from a view inside it is passed on.</summary>
        public override void MouseEntered (NSEvent theEvent)
        {
            if (theEvent.TrackingArea == m_hoverTrackingArea)
            {
                this.SetPointerOver(true);
            }
            else
            {
                base.MouseEntered(theEvent);
            }
        }

        public override void MouseExited (NSEvent theEvent)
        {
            if (theEvent.TrackingArea == m_hoverTrackingArea)
            {
                this.SetPointerOver(false);
            }
            else
            {
                base.MouseExited(theEvent);
            }
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
            m_header = null;
            m_emptyPlaceholder = null;
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

            // Clearing takes the shown panel's views out of the window, and focus inside them goes with them (the window
            //  takes it back). Whatever had it gets it back once the rebuild has put it back in this zone; failing that, a
            //  panel that held focus keeps it itself, so its header stays focused.
            NSResponder focused = this.HasFocusWithin() ? this.Window.FirstResponder : null;

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

            if ((focused != null) && (m_header != null) && !this.HasFocusWithin())
            {
                bool isBackInZone = (focused is NSView focusedView) && focusedView.IsDescendantOf(this) && focusedView.AcceptsFirstResponder();
                this.Window?.MakeFirstResponder(isBackInZone ? focused : this);
            }

            this.RefreshPanelFocus();
        }

        private NSView BuildSplit ()
        {
            // Each pane draws its own outline, so a divider line between two panes would double it; the WinUI3 and WPF docks
            //  make their splitters transparent too. The divider is still there to drag.
            m_split = new ThemedSplitView(isVertical: m_zone.Orientation == eDockOrientation.Horizontal)
            {
                MinPanelSize = m_zone.Manager?.MinPanelDimension ?? kFallbackMinPanelSize,
                DividerColorKey = null,
            };

            // A split places its panes by frame, so a pane's frame becomes required constraints on it, while the leaf inside
            //  needs room for its header, tabs and panel. Each pane starts at its share of this zone rather than the empty
            //  frame a new view has, or until the split first sizes them those can't both hold and AppKit has to search for
            //  constraints to break (see the tab row in TabStrip). The stored pane sizes still apply on the next layout.
            double minPane = m_zone.Manager?.MinPanelDimension ?? kFallbackMinPanelSize;
            bool isSideBySide = m_zone.Orientation == eDockOrientation.Horizontal;
            int paneCount = Math.Max(1, m_zone.Children.Count);
            CGRect zoneBounds = this.Bounds;
            double paneWidth = Math.Max(minPane, isSideBySide ? (double)zoneBounds.Width / paneCount : (double)zoneBounds.Width);
            double paneHeight = Math.Max(minPane, isSideBySide ? (double)zoneBounds.Height : (double)zoneBounds.Height / paneCount);

            foreach (DockZoneViewModel child in m_zone.Children)
            {
                var childView = new DockZoneView(child)
                {
                    Frame = new CGRect(0, 0, (nfloat)paneWidth, (nfloat)paneHeight),
                };

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
            // Faint, and only while the pointer is over the zone, as on both flagships
            const double kPlaceholderOpacity = 0.25;

            this.BackgroundColorKey = ThemedColorKeys.DockZone_EmptyBackground;
            this.BorderColorKey = ThemedColorKeys.DockZone_EmptyBorder;
            this.BorderThickness = 1.0;

            m_emptyPlaceholder = NSTextField.CreateLabel("Empty");
            m_emptyPlaceholder.TextColor = ThemedColors.Get(ThemedColorKeys.DockZone_EmptyForeground);
            m_emptyPlaceholder.Alignment = NSTextAlignment.Center;
            m_emptyPlaceholder.AlphaValue = (nfloat)kPlaceholderOpacity;
            m_emptyPlaceholder.Hidden = !m_isPointerOver;
            return m_emptyPlaceholder;
        }

        // The leaf, as WinUI3's DockLeafLayout lays it out (see DockLeafLayout.xaml):
        //  - The panel frame, filled and outlined, its bottom corners rounded, holding the shown panel's display. Its top
        //    sits under the header, which covers the frame's top corners, so only the bottom ones show rounded. The
        //    outline is the frame layer's border, which a layer draws over its sublayers, and the frame clips the display
        //    to its rounded corners.
        //  - The header over the frame's top, with its own outline all around, so the line between it and the display is
        //    the header's bottom edge.
        //  - For Tabbed, the tab strip below the frame, inset from the zone's sides and bottom, overlapping the frame's
        //    bottom edge so the selected tab can cover it and join the panel above. The strip has no fill or outline: its
        //    gaps show whatever is behind the zone.
        private NSView BuildLeaf ()
        {
            // WinUI3's themed PanelCornerRadius is 0,0,8,8 (the top two are under the header here), and its content border
            //  pads the display by 2. Its tab strip's margin (5,-2,5,5) and padding (2,0,2,2) together put the tabs 7 in
            //  from the zone's sides and bottom.
            const double kPanelCornerRadius = 8.0;
            const double kDisplayPadding = 2.0;
            const double kTabStripInset = 7.0;

            int selected = Math.Clamp(m_zone.SelectedIndex, 0, m_zone.DockedContent.Count - 1);
            DockingContentAdapterModel shown = m_zone.DockedContent[selected];
            var leaf = new NSView();

            var panel = new ThemedView
            {
                BackgroundColorKey = ThemedColorKeys.DockZone_PanelBackground,
                BorderColorKey = ThemedColorKeys.DockZone_PanelBorder,
                BorderThickness = kEdgeThickness,
                CornerRadius = kPanelCornerRadius,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };

            if (panel.Layer != null)
            {
                panel.Layer.MasksToBounds = true;
            }

            leaf.AddSubview(panel);

            var displayHost = new NSView { TranslatesAutoresizingMaskIntoConstraints = false };
            panel.AddSubview(displayHost);
            double displayInset = kEdgeThickness + kDisplayPadding;
            var constraints = new List<NSLayoutConstraint>
            {
                panel.TopAnchor.ConstraintEqualTo(leaf.TopAnchor),
                panel.LeadingAnchor.ConstraintEqualTo(leaf.LeadingAnchor),
                panel.TrailingAnchor.ConstraintEqualTo(leaf.TrailingAnchor),
                displayHost.TopAnchor.ConstraintEqualTo(panel.TopAnchor, (nfloat)(kHeaderHeight + kDisplayPadding)),
                displayHost.BottomAnchor.ConstraintEqualTo(panel.BottomAnchor, (nfloat)(-displayInset)),
                displayHost.LeadingAnchor.ConstraintEqualTo(panel.LeadingAnchor, (nfloat)displayInset),
                displayHost.TrailingAnchor.ConstraintEqualTo(panel.TrailingAnchor, (nfloat)(-displayInset)),
            };

            if (shown.Display is NSView display)
            {
                display.RemoveFromSuperview();
                display.TranslatesAutoresizingMaskIntoConstraints = false;
                displayHost.AddSubview(display);
                constraints.Add(display.TopAnchor.ConstraintEqualTo(displayHost.TopAnchor));
                constraints.Add(display.BottomAnchor.ConstraintEqualTo(displayHost.BottomAnchor));
                constraints.Add(display.LeadingAnchor.ConstraintEqualTo(displayHost.LeadingAnchor));
                constraints.Add(display.TrailingAnchor.ConstraintEqualTo(displayHost.TrailingAnchor));
            }

            m_header = new HeaderView(this, shown) { TranslatesAutoresizingMaskIntoConstraints = false };
            leaf.AddSubview(m_header);
            constraints.Add(m_header.TopAnchor.ConstraintEqualTo(leaf.TopAnchor));
            constraints.Add(m_header.LeadingAnchor.ConstraintEqualTo(leaf.LeadingAnchor));
            constraints.Add(m_header.TrailingAnchor.ConstraintEqualTo(leaf.TrailingAnchor));
            constraints.Add(m_header.HeightAnchor.ConstraintEqualTo((nfloat)kHeaderHeight));

            if (m_zone.DockedContent.Count > 1)
            {
                var tabs = new TabStrip(this, selected);
                leaf.AddSubview(tabs);
                constraints.Add(tabs.TopAnchor.ConstraintEqualTo(panel.BottomAnchor, (nfloat)(-kEdgeThickness)));
                constraints.Add(tabs.BottomAnchor.ConstraintEqualTo(leaf.BottomAnchor, (nfloat)(-kTabStripInset)));
                constraints.Add(tabs.LeadingAnchor.ConstraintEqualTo(leaf.LeadingAnchor, (nfloat)kTabStripInset));
                constraints.Add(tabs.TrailingAnchor.ConstraintEqualTo(leaf.TrailingAnchor, (nfloat)(-kTabStripInset)));
            }
            else
            {
                constraints.Add(panel.BottomAnchor.ConstraintEqualTo(leaf.BottomAnchor));
            }

            NSLayoutConstraint.ActivateConstraints(constraints.ToArray());
            return leaf;
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

        private void SetPointerOver (bool isPointerOver)
        {
            m_isPointerOver = isPointerOver;
            if (m_emptyPlaceholder != null)
            {
                m_emptyPlaceholder.Hidden = !isPointerOver;
            }
        }

        // ------ Panel focus
        // A panel's header shows it is the one being typed into while focus is within its zone and its window is the key
        // window, which is WinUI3's rule (DockZone.RefreshPanelFocus, there with the window being active) and WPF's
        // (IsKeyboardFocusWithin).
        //
        // AppKit has no focus-within, and no event when focus moves into or out of a view. What it has is the window's
        // first responder, which is key-value observable, and NSWindow's became-key and resigned-key notifications. So
        // each zone watches the window it is in for both, and works out again whether the first responder is itself or a
        // view inside it (a text field being edited has its window's field editor as first responder, and the field
        // editor sits inside the field, so that counts). The watches move with the zone from window to window, and go
        // when it leaves one or is torn down; each holds this zone until it is let go of.
        // https://developer.apple.com/documentation/appkit/nswindow/firstresponder
        //
        // A press only moves focus when it lands on a view that takes it (a field, a button), so a press on a panel's
        // header, tabs, labels or empty space would leave focus, and the highlight, in another panel. A leaf zone takes
        // focus itself for such a press, as WinUI3's does; anything under the press that takes focus has already taken it
        // by then, since AppKit makes the clicked view first responder before it gets the press.

        // Gives this zone focus for a press inside its panel, unless focus is already within it, and says whether the
        // press was inside a panel at all. A split zone leaves this to the panels inside it, and an empty zone has nothing
        // to type into.
        private bool TakeFocusForPress ()
        {
            if (m_header == null)
            {
                return false;
            }

            if (!this.HasFocusWithin())
            {
                this.Window?.MakeFirstResponder(this);
            }

            return true;
        }

        // NSView.IsDescendantOf is true for the view itself, so this zone being first responder counts
        private bool HasFocusWithin () => (this.Window?.FirstResponder is NSView focused) && focused.IsDescendantOf(this);

        private void RefreshPanelFocus ()
        {
            if (m_header != null)
            {
                m_header.IsPanelFocused = (m_watchedWindow != null) && m_watchedWindow.IsKeyWindow && this.HasFocusWithin();
            }
        }

        private void WatchWindow (NSWindow window)
        {
            if (window == m_watchedWindow)
            {
                return;
            }

            m_firstResponderWatch?.Dispose();
            m_firstResponderWatch = null;
            m_becameKeyWatch?.Dispose();
            m_becameKeyWatch = null;
            m_resignedKeyWatch?.Dispose();
            m_resignedKeyWatch = null;

            m_watchedWindow = window;
            if (window != null)
            {
                m_firstResponderWatch = window.AddObserver("firstResponder", NSKeyValueObservingOptions.New, this.OnWindowFirstResponderChanged);
                m_becameKeyWatch = NSWindow.Notifications.ObserveDidBecomeKey(window, this.OnWindowKeyStateChanged);
                m_resignedKeyWatch = NSWindow.Notifications.ObserveDidResignKey(window, this.OnWindowKeyStateChanged);
            }
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
        // anywhere else on it and dragging past the threshold starts a header drag. It has an outline all around, as
        // WinUI3's header bar does, so its bottom edge is the line between it and the panel's display.
        private sealed class HeaderView : ThemedView
        {
            private readonly DockZoneView m_owner;
            private readonly NSTextField m_title;
            private readonly DockGlyphButton m_close;
            private bool m_isPanelFocused;
            private bool m_isPressPending;
            private CGPoint m_pressInZone;
            private CGPoint m_pressOnScreen;

            public HeaderView (DockZoneView owner, DockingContentAdapterModel shown)
            {
                m_owner = owner;
                this.BackgroundColorKey = ThemedColorKeys.DockZone_HeaderBackground;
                this.BorderColorKey = ThemedColorKeys.DockZone_PanelBorder;
                this.BorderThickness = kEdgeThickness;

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

                m_title = NSTextField.CreateLabel(String.Empty);
                m_title.LineBreakMode = NSLineBreakMode.TruncatingTail;
                m_title.TextColor = ThemedColors.Get(ThemedColorKeys.DockZone_HeaderForeground);
                owner.m_layoutBindings.BindLabel(m_title, shown, nameof(DockingContentAdapterModel.TitleContent), _ReadTitle);
                row.Add(m_title, grows: true, alignment: eFillStackCrossAlignment.Center);

                if (shown.IsClosable)
                {
                    m_close = new DockGlyphButton(DockGlyphButton.eGlyph.Close)
                    {
                        ToolTip = shown.HideDontClose ? "Hide" : "Close",
                    };
                    m_close.TranslatesAutoresizingMaskIntoConstraints = false;
                    NSLayoutConstraint.ActivateConstraints(new[]
                    {
                        m_close.WidthAnchor.ConstraintEqualTo((nfloat)kCloseButtonSize),
                        m_close.HeightAnchor.ConstraintEqualTo((nfloat)kCloseButtonSize),
                    });
                    owner.m_layoutBindings.BindCommand(m_close, new PanelAction(shown, owner.ClosePanel));
                    row.Add(m_close, alignment: eFillStackCrossAlignment.Center);
                }

                this.Menu = owner.BuildPanelMenu(shown);

                string _ReadTitle () => owner.ReadTitle(shown);
            }

            // For a peer the runtime makes over an existing native header, which has no owner to tell
            private HeaderView (NativeHandle handle) : base(handle)
            {
            }

            /// <summary>Shows the panel as the one being typed into: the focused fill, with the title and close X in the text that reads on it</summary>
            public bool IsPanelFocused
            {
                get => m_isPanelFocused;
                set
                {
                    if (m_isPanelFocused == value)
                    {
                        return;
                    }

                    m_isPanelFocused = value;
                    string textKey = value ? ThemedColorKeys.DockZone_HeaderFocusedForeground : ThemedColorKeys.DockZone_HeaderForeground;
                    this.BackgroundColorKey = value ? ThemedColorKeys.DockZone_HeaderFocusedBackground : ThemedColorKeys.DockZone_HeaderBackground;
                    m_title.TextColor = ThemedColors.Get(textKey);
                    if (m_close != null)
                    {
                        m_close.GlyphColorKey = textKey;
                    }
                }
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
                m_owner.TakeFocusForPress();
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

        // The tabs of a Tabbed zone, along the bottom, drawn the way WinUI3 draws them. The strip overlaps the panel frame's
        // bottom edge by that edge's thickness, so the selected tab, in the panel's fill, full height and with no top edge,
        // covers the edge and reads as part of the panel above it. The other tabs sit just below the edge, which is their
        // top, a little shorter, with their title and close X faint; the one under the pointer reaches the strip's bottom
        // and its title brightens.
        //
        // Tabs keep their own widths. When they don't all fit, the strip scrolls sideways: a scroll button shows at each
        // end (disabled once there is nothing further that way), and the wheel or a sideways trackpad swipe scrolls it too.
        // The offset lives on the zone view, since the strip is rebuilt on every selection change, and each new strip
        // brings the selected tab into view.
        //
        // The strip takes the mouse for its tabs (a tab's close button takes its own). A click selects, and a middle click
        // closes. A press that moves past the threshold is decided once, by the shared rule: along the strip it reorders,
        // up or down out of it tears the tab's panel off, when the panel allows it. While a reorder runs, a ghost of the
        // tab follows the pointer along the strip, the tab itself fades a little where it is, and a line shows where it
        // will land; the reorder itself is the VM's, on release.
        private sealed class TabStrip : ThemedView
        {
            private const double kScrollStep = 80.0;
            private const double kDragInsertLineWidth = 2.0;

            private readonly DockZoneView m_owner;
            private readonly int m_selectedIndex;
            private readonly ThemedView m_clip;
            private readonly FillStack m_tabs;
            private readonly DockGlyphButton m_scrollBack;
            private readonly DockGlyphButton m_scrollForward;
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

                // The tabs are kTabHeight below the panel; the strip's top edge's worth overlaps the panel
                this.HeightAnchor.ConstraintEqualTo((nfloat)(kTabHeight + kEdgeThickness)).Active = true;

                // Placed by frame in Layout, back to front: the tabs clipped to the space between the scroll buttons, then
                //  the scroll buttons
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

                m_scrollBack = this.MakeScrollButton(DockGlyphButton.eGlyph.ScrollBack, "Scroll tabs back", this.ScrollBack);
                m_scrollForward = this.MakeScrollButton(DockGlyphButton.eGlyph.ScrollForward, "Scroll tabs forward", this.ScrollForward);

                // The row of tabs is placed by frame (in Layout), so its frame is turned into required constraints, and the
                // tabs inside it are held by required ones of their own (insets, the close button, the gaps). Left at the
                // empty frame it is made with until the first Layout, the two can't both hold, and AppKit has to pick
                // constraints to break: a costly search through the window's constraints, which on .NET calls into every
                // managed view it compares and grew with the tab count until a click took seconds. So it starts at the size
                // its tabs want.
                double rowWidth = 0.0;
                foreach (NSView tab in m_tabs.Children)
                {
                    rowWidth += (tab is TabView tabView) ? tabView.MeasureWidth() : 0.0;
                }

                var rowFrame = new CGRect(0, 0, (nfloat)rowWidth, (nfloat)(kTabHeight + kEdgeThickness));
                m_clip.Frame = rowFrame;
                m_tabs.Frame = rowFrame;
            }

            // For a peer the runtime makes over an existing native strip, which has no owner to tell
            private TabStrip (NativeHandle handle) : base(handle)
            {
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

                // 1. Each tab is as wide as it wants to be, and the tabs sit edge to edge. A tab works its width out from its
                //    title (TabView.MeasureWidth) rather than being asked its FittingSize: that is a whole constraint solve of
                //    the tab, AppKit runs this layout several times a pass, and with every tab asked each time the docking
                //    page slowed with the square of its tab count, nearly two seconds a click at seven tabs.
                m_tabWidths.Clear();
                double contentWidth = 0.0;
                foreach (NSView tab in m_tabs.Children)
                {
                    double tabWidth = (tab is TabView tabView) ? tabView.MeasureWidth() : (double)tab.FittingSize.Width;
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

                // 3. Place the clipped tabs and the scroll buttons, which sit below the panel's edge
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
                m_owner.TakeFocusForPress();
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

            private DockGlyphButton MakeScrollButton (DockGlyphButton.eGlyph arrow, string toolTip, Action scroll)
            {
                var button = new DockGlyphButton(arrow)
                {
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

            // A copy of the dragged tab that follows the pointer along the strip, the line where it will land, and the tab
            // itself faded where it is, as WinUI3's Dragging state fades it
            private void BeginReorderFeedback ()
            {
                const double kDragGhostOpacity = 0.85;

                DockingContentAdapterModel adapter = m_owner.m_zone.DockedContent[m_dragFromIndex];
                double ghostWidth = (m_dragFromIndex < m_tabWidths.Count) ? m_tabWidths[m_dragFromIndex] : kTabHeight;
                this.SetTabDragged(m_dragFromIndex, true);
                m_dragGhost = new ThemedView
                {
                    BackgroundColorKey = ThemedColorKeys.DockZone_DragGhostBackground,
                    BorderColorKey = ThemedColorKeys.DockZone_DragGhostBorder,
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
                if (m_isDragging)
                {
                    this.SetTabDragged(m_dragFromIndex, false);
                }

                m_dragGhost?.RemoveFromSuperview();
                m_dragGhost = null;
                m_dragInsertLine?.RemoveFromSuperview();
                m_dragInsertLine = null;
                m_dragFromIndex = -1;
                m_dragTargetIndex = -1;
                m_isDragging = false;
            }

            private void SetTabDragged (int index, bool isDragged)
            {
                const double kDraggedTabOpacity = 0.9;

                NSView tab = (index >= 0) ? m_tabs.Children.ElementAtOrDefault(index) : null;
                if (tab != null)
                {
                    tab.AlphaValue = (nfloat)(isDragged ? kDraggedTabOpacity : 1.0);
                }
            }
        }

        // The dock's small glyph buttons, drawn the way WinUI3's are: the tab strip's scroll arrows (DockTabScrollButton)
        // and the header's and tabs' close X (DockCloseButton). Clear at rest, the hover or pressed fill with its corners
        // rounded 2, and faded while disabled (a scroll arrow with nowhere further to go). A scroll arrow is a small solid
        // triangle in the dock's text color, as WinUI3 shows the characters U+25C2 and U+25B8; the X is stroked in the text
        // beside it, which is the focused header text while its panel is focused. A GeometryButton would fill in the button
        // face color and trace in the common border, which all but vanishes on the dark palette.
        private sealed class DockGlyphButton : ManagedButton
        {
            private readonly eGlyph m_glyph;
            private string m_glyphColorKey;

            public DockGlyphButton (eGlyph glyph)
            {
                m_glyph = glyph;
                m_glyphColorKey = (glyph == eGlyph.Close) ? ThemedColorKeys.DockZone_HeaderForeground : ThemedColorKeys.DockTabScrollButton_Glyph;
            }

            // For a peer the runtime makes over an existing native button
            private DockGlyphButton (NativeHandle handle) : base(handle)
            {
            }

            /// <summary>The <see cref="ThemedColors"/> key the glyph is drawn in: the text beside it for the X, the scroll arrow key for the arrows</summary>
            public string GlyphColorKey
            {
                get => m_glyphColorKey;
                set
                {
                    m_glyphColorKey = value;
                    this.NeedsDisplay = true;
                }
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                const double kCornerRadius = 2.0;
                const double kDisabledOpacity = 0.4;

                CGRect bounds = this.Bounds;
                eManagedButtonState state = this.CurrentState;
                bool isClose = m_glyph == eGlyph.Close;

                // 1. The fill, only under the pointer or pressed
                string fillKey = null;
                if (state == eManagedButtonState.PointerOver)
                {
                    fillKey = isClose ? ThemedColorKeys.DockCloseButton_HoverBackground : ThemedColorKeys.DockTabScrollButton_HoverBackground;
                }
                else if (state == eManagedButtonState.Pressed)
                {
                    fillKey = isClose ? ThemedColorKeys.DockCloseButton_PressedBackground : ThemedColorKeys.DockTabScrollButton_PressedBackground;
                }

                if (fillKey != null)
                {
                    ThemedColors.Get(fillKey).SetFill();
                    NSBezierPath.FromRoundedRect(bounds, (nfloat)kCornerRadius, (nfloat)kCornerRadius).Fill();
                }

                // 2. The glyph
                NSColor glyphColor = ThemedColors.Get(m_glyphColorKey);
                if (state == eManagedButtonState.Disabled)
                {
                    glyphColor = glyphColor.ColorWithAlphaComponent((nfloat)kDisabledOpacity);
                }

                double centerX = (double)bounds.Width / 2.0;
                double centerY = (double)bounds.Height / 2.0;
                if (isClose)
                {
                    DrawCloseX(centerX, centerY, glyphColor);
                }
                else
                {
                    DrawScrollArrow(centerX, centerY, m_glyph == eGlyph.ScrollBack, glyphColor);
                }
            }

            private static void DrawCloseX (double centerX, double centerY, NSColor color)
            {
                const double kHalfSpan = 4.0;
                const double kLineWidth = 1.5;

                var cross = new NSBezierPath { LineWidth = (nfloat)kLineWidth };
                cross.MoveTo(new CGPoint(centerX - kHalfSpan, centerY - kHalfSpan));
                cross.LineTo(new CGPoint(centerX + kHalfSpan, centerY + kHalfSpan));
                cross.MoveTo(new CGPoint(centerX + kHalfSpan, centerY - kHalfSpan));
                cross.LineTo(new CGPoint(centerX - kHalfSpan, centerY + kHalfSpan));
                color.SetStroke();
                cross.Stroke();
            }

            // Points the way it scrolls (the view is flipped, so +y is down)
            private static void DrawScrollArrow (double centerX, double centerY, bool pointsBack, NSColor color)
            {
                const double kArrowWidth = 4.0;
                const double kArrowHalfHeight = 3.5;

                double tipX = pointsBack ? centerX - (kArrowWidth / 2.0) : centerX + (kArrowWidth / 2.0);
                double baseX = pointsBack ? centerX + (kArrowWidth / 2.0) : centerX - (kArrowWidth / 2.0);
                var arrow = new NSBezierPath();
                arrow.MoveTo(new CGPoint(baseX, centerY - kArrowHalfHeight));
                arrow.LineTo(new CGPoint(tipX, centerY));
                arrow.LineTo(new CGPoint(baseX, centerY + kArrowHalfHeight));
                arrow.ClosePath();
                color.SetFill();
                arrow.Fill();
            }

            public enum eGlyph
            {
                /// <summary>The arrow at the strip's start, pointing back</summary>
                ScrollBack,

                /// <summary>The arrow at the strip's end, pointing forward</summary>
                ScrollForward,

                /// <summary>The X that closes a panel</summary>
                Close,
            }
        }

        // One tab: its title and, for a closable panel, an X that closes it, on a face drawn with the edges the strip
        // asks for. The strip takes the mouse; this makes the tab something VoiceOver can find and pick, as one of a set
        // of radio buttons named by the tab's title (its text field is left out, so the title isn't read twice, and its
        // close button is the one child it shows).
        private sealed class TabView : NSView
        {
            // Unselected tabs sit this much short of the strip's bottom, with their title and close X this faint; the fill
            //  and edges stay full. Under the pointer any tab takes the hover fill, the muted primary that says it can be
            //  dragged off, inset this much inside its face, and an unselected one reaches the strip's bottom with its
            //  label left as faint. Both flagships show the same.
            private const double kUnselectedBottomInset = 2.0;
            private const double kUnselectedLabelOpacity = 0.35;
            private const double kHoverFillInset = 1.0;
            private const double kTabCloseSize = 16.0;
            private const double kTabCloseGap = 2.0;

            private readonly DockZoneView m_owner;
            private readonly int m_index;
            private readonly bool m_isSelected;
            private readonly NSTextField m_title;
            private readonly DockGlyphButton m_close;
            private readonly ThemedView m_hoverFill;
            private readonly NSLayoutConstraint m_faceBottom;
            private readonly NSTrackingArea m_hoverTrackingArea;

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
                    TranslatesAutoresizingMaskIntoConstraints = false,
                };

                this.AddSubview(face);
                m_faceBottom = face.BottomAnchor.ConstraintEqualTo(this.BottomAnchor, (nfloat)(isSelected ? 0.0 : -kUnselectedBottomInset));
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    face.TopAnchor.ConstraintEqualTo(this.TopAnchor, (nfloat)(isSelected ? 0.0 : kEdgeThickness)),
                    m_faceBottom,
                    face.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                    face.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                });

                // Under the title and X, so they draw over it
                m_hoverFill = new ThemedView
                {
                    BackgroundColorKey = ThemedColorKeys.DockZone_TabPointerOverBackground,
                    Hidden = true,
                    TranslatesAutoresizingMaskIntoConstraints = false,
                };

                face.AddSubview(m_hoverFill);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    m_hoverFill.TopAnchor.ConstraintEqualTo(face.TopAnchor, (nfloat)kHoverFillInset),
                    m_hoverFill.BottomAnchor.ConstraintEqualTo(face.BottomAnchor, (nfloat)(-kHoverFillInset)),
                    m_hoverFill.LeadingAnchor.ConstraintEqualTo(face.LeadingAnchor, (nfloat)kHoverFillInset),
                    m_hoverFill.TrailingAnchor.ConstraintEqualTo(face.TrailingAnchor, (nfloat)(-kHoverFillInset)),
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
                    m_close = new DockGlyphButton(DockGlyphButton.eGlyph.Close)
                    {
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

                // One area for the tab's life, as ManagedButton's is
                m_hoverTrackingArea = new NSTrackingArea(
                    CGRect.Empty,
                    NSTrackingAreaOptions.MouseEnteredAndExited | NSTrackingAreaOptions.ActiveInActiveApp | NSTrackingAreaOptions.InVisibleRect,
                    this,
                    null
                );
                this.AddTrackingArea(m_hoverTrackingArea);

                if (!isSelected)
                {
                    m_title.AlphaValue = (nfloat)kUnselectedLabelOpacity;
                    if (m_close != null)
                    {
                        m_close.AlphaValue = (nfloat)kUnselectedLabelOpacity;
                    }
                }

                string _ReadTitle () => owner.ReadTitle(adapter);
            }

            // For a peer the runtime makes over an existing native tab, which has no owner to tell
            private TabView (NativeHandle handle) : base(handle)
            {
            }

            /// <summary>
            /// The width this tab wants: the title's inset, the title, then the close button and its gaps (or the title's
            /// inset again), the same chain its constraints lay out, worked out without a constraint solve
            /// </summary>
            public double MeasureWidth ()
            {
                double titleWidth = Math.Max(0.0, (double)m_title.IntrinsicContentSize.Width);
                double trailing = (m_close != null) ? (kTabCloseGap + kTabCloseSize + kTabCloseGap) : kTabTitleInset;
                return kTabTitleInset + titleWidth + trailing;
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

            public override void MouseEntered (NSEvent theEvent)
            {
                if (theEvent.TrackingArea == m_hoverTrackingArea)
                {
                    this.ShowPointerOver(true);
                }
                else
                {
                    base.MouseEntered(theEvent);
                }
            }

            public override void MouseExited (NSEvent theEvent)
            {
                if (theEvent.TrackingArea == m_hoverTrackingArea)
                {
                    this.ShowPointerOver(false);
                }
                else
                {
                    base.MouseExited(theEvent);
                }
            }

            // The selected tab is always at full height; an unselected one reaches the strip's bottom under the pointer
            private void ShowPointerOver (bool isPointerOver)
            {
                m_hoverFill.Hidden = !isPointerOver;
                if (!m_isSelected)
                {
                    m_faceBottom.Constant = (nfloat)(isPointerOver ? 0.0 : -kUnselectedBottomInset);
                }
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
