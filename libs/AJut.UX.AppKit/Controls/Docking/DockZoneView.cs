namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using AJut.UX.CodeBinding;
    using AJut.UX.Docking;
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

        private readonly DockZoneViewModel m_zone;
        private readonly List<DockZoneView> m_childViews = new List<DockZoneView>();
        private BindingSet m_layoutBindings;
        private ThemedSplitView m_split;
        private DockDropOverlayView m_dropOverlay;
        private List<double> m_pendingChildSizes;
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
            if (m_pendingChildSizes == null || m_split == null || m_split.ArrangedSubviews.Length < 2)
            {
                return;
            }

            // Each size is a child's length along the split; a divider sits after each child but the last
            double position = 0.0;
            int dividerCount = Math.Min(m_pendingChildSizes.Count, m_split.ArrangedSubviews.Length) - 1;
            for (int i = 0; i < dividerCount; ++i)
            {
                position += m_pendingChildSizes[i];
                m_split.SetPositionOfDivider((nfloat)position, i);
                position += m_split.Thickness;
            }

            m_pendingChildSizes = null;
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

        // The tabs of a Tabbed zone, along the bottom. Click selects. A press that moves past the
        // threshold is decided once, by the shared rule: along the strip it reorders, committed through
        // the VM on release; up or down out of it tears the tab's panel off, when the panel allows it.
        private sealed class TabStrip : ThemedView
        {
            private readonly DockZoneView m_owner;
            private readonly FillStack m_tabs;
            private int m_dragFromIndex = -1;
            private CGPoint m_dragStart;
            private bool m_isDragging;

            public TabStrip (DockZoneView owner, int selectedIndex)
            {
                m_owner = owner;
                this.BackgroundColorKey = ThemedColorKeys.DockZone_TabStripBackground;
                this.TranslatesAutoresizingMaskIntoConstraints = false;
                this.HeightAnchor.ConstraintEqualTo((nfloat)kTabHeight).Active = true;

                m_tabs = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: 1.0);
                m_tabs.TranslatesAutoresizingMaskIntoConstraints = false;
                this.AddSubview(m_tabs);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    m_tabs.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                    m_tabs.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                    m_tabs.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                    m_tabs.TrailingAnchor.ConstraintLessThanOrEqualTo(this.TrailingAnchor),
                });

                for (int i = 0; i < owner.m_zone.DockedContent.Count; ++i)
                {
                    DockingContentAdapterModel adapter = owner.m_zone.DockedContent[i];
                    var tab = new TabView(owner, i, i == selectedIndex)
                    {
                        BackgroundColorKey = i == selectedIndex ? ThemedColorKeys.DockZone_SelectedTabBackground : ThemedColorKeys.DockZone_TabBackground,
                        BorderColorKey = ThemedColorKeys.DockZone_PanelBorder,
                        BorderThickness = 1.0,
                    };

                    NSTextField title = NSTextField.CreateLabel(String.Empty);
                    title.TextColor = ThemedColors.Get(ThemedColorKeys.DockZone_HeaderForeground);
                    owner.m_layoutBindings.BindLabel(title, adapter, nameof(DockingContentAdapterModel.TitleContent), _ReadTitle);
                    title.TranslatesAutoresizingMaskIntoConstraints = false;
                    tab.AddSubview(title);
                    tab.Title = title;
                    NSLayoutConstraint.ActivateConstraints(new[]
                    {
                        title.CenterYAnchor.ConstraintEqualTo(tab.CenterYAnchor),
                        title.LeadingAnchor.ConstraintEqualTo(tab.LeadingAnchor, (nfloat)8),
                        title.TrailingAnchor.ConstraintEqualTo(tab.TrailingAnchor, (nfloat)(-8)),
                    });

                    tab.ToolTip = adapter.TooltipContent?.ToString();
                    tab.Menu = owner.BuildPanelMenu(adapter);
                    m_tabs.Add(tab);

                    string _ReadTitle () => owner.ReadTitle(adapter);
                }
            }

            public override void MouseDown (NSEvent theEvent)
            {
                m_dragFromIndex = this.TabIndexAt(theEvent);
                m_dragStart = this.ConvertPointFromView(theEvent.LocationInWindow, null);
                m_isDragging = false;
            }

            public override void MouseDragged (NSEvent theEvent)
            {
                if ((m_dragFromIndex < 0)
                    || (m_dragFromIndex >= m_owner.m_zone.DockedContent.Count)
                    || m_isDragging)
                {
                    return;
                }

                CGPoint now = this.ConvertPointFromView(theEvent.LocationInWindow, null);
                DockingContentAdapterModel tab = m_owner.m_zone.DockedContent[m_dragFromIndex];
                switch (DockDragRules.DecideTabDrag(tab, (double)(now.X - m_dragStart.X), (double)(now.Y - m_dragStart.Y), m_owner.DragThreshold))
                {
                    case eTabDragKind.Reorder:
                        m_isDragging = true;
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
                int index = this.TabIndexAt(theEvent);
                if (m_dragFromIndex >= 0 && index >= 0)
                {
                    if (m_isDragging)
                    {
                        m_owner.ReorderTab(m_dragFromIndex, index);
                    }
                    else
                    {
                        m_owner.SelectTab(index);
                    }
                }

                m_dragFromIndex = -1;
                m_isDragging = false;
            }

            // Which tab is under the pointer horizontally; past the last tab counts as the last
            private int TabIndexAt (NSEvent theEvent)
            {
                CGPoint point = m_tabs.ConvertPointFromView(theEvent.LocationInWindow, null);
                int index = 0;
                foreach (NSView tab in m_tabs.Children)
                {
                    if (point.X < tab.Frame.Right)
                    {
                        return index;
                    }

                    ++index;
                }

                return index - 1;
            }
        }

        // One tab. The strip handles the mouse; this only makes the tab something VoiceOver can find and
        // pick, as one of a set of radio buttons named by the tab's title (its text field is ignored, so
        // the title is not read twice).
        private sealed class TabView : ThemedView
        {
            private readonly DockZoneView m_owner;
            private readonly int m_index;
            private readonly bool m_isSelected;

            public TabView (DockZoneView owner, int index, bool isSelected)
            {
                m_owner = owner;
                m_index = index;
                m_isSelected = isSelected;
            }

            public NSTextField Title { get; set; }

            public override bool AccessibilityElement => true;
            public override string AccessibilityRole => NSAccessibilityRoles.RadioButtonRole;
            public override string AccessibilityLabel => this.Title?.StringValue;
            public override bool AccessibilitySelected => m_isSelected;
            public override NSObject[] AccessibilityChildren => null;

            public override bool AccessibilityPerformPress ()
            {
                m_owner.SelectTab(m_index);
                return true;
            }
        }
    }
}
