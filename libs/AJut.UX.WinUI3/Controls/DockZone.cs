namespace AJut.UX.Controls
{
    using AJut.Tree;
    using AJut.UX.Docking;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using Windows.Foundation;
    using APUtils = AJut.UX.APUtils<DockZone>;
    using DPUtils = AJut.UX.DPUtils<DockZone>;

    // ===========[ DockZone ]====================================================
    // WinUI3 docking zone control. Backed by a DockZoneViewModel which drives
    // the layout: Empty / Single / Tabbed / Horizontal-split / Vertical-split.
    //
    // Implements IDockZoneUI so DockZoneViewModel can be platform-agnostic:
    //   RenderSize       -> ActualWidth / ActualHeight
    //   SetTargetSizeAsync -> schedules SetTargetSize on the DispatcherQueue
    //
    // The template contains only PART_Root (a Grid). All structural children
    // (DockLeafLayout, child DockZones, DockZoneSplitters) are built in code-behind
    // via RebuildLayout() whenever the ViewModel's Orientation or DockedContent changes.
    //
    // Leaf layout (Single / Tabbed orientations) is delegated to DockLeafLayout, a
    // proper WinUI3 Control with its own ControlTemplate in DockLeafLayout.xaml. This
    // gives the 3-row structure (header / content / tab strip) a real XAML home so
    // theming, border overlap, and padding live in markup rather than code.
    //
    // Drag-and-drop (tearoff, zone reorder) is deferred to a later session.
    //
    // Template part:
    //   PART_Root - Grid populated by code-behind

    [TemplatePart(Name = nameof(PART_Root), Type = typeof(Grid))]
    public sealed class DockZone : Control, IDockZoneUI
    {
        // ===========[ Fields ]===============================================
        private Grid m_dropOverlay;
        private DockDropInsertionDriverWidget m_overlayLeft, m_overlayTop, m_overlayRight, m_overlayBottom, m_overlayCenter;
        private readonly ObservableCollection<DockZone> m_childZones = new();

        // Header-drag threshold state (reset at each RebuildLayout)
        private bool m_headerDragPending;
        private Point m_headerDragLocalPressPt;
        private Point m_headerDragScreenPressPt;
        private DockZone m_headerDragZone;   // zone to pass to InitiateDrag

        // Tab strip state - valid during BuildTabNavContent lifetime
        private StackPanel m_tabPanel;
        private ScrollViewer m_tabScrollViewer;
        private DockTabScrollButton m_tabLeftScrollBtn;
        private DockTabScrollButton m_tabRightScrollBtn;
        private int m_tabReorderTargetIdx = -1;

        // ===========[ Construction ]=========================================
        static DockZone()
        {
            // Allow DockingManager.FindFirstAvailableDockZone / CloseAll to walk the zone tree.
            TreeTraversal<DockZone>.SetupDefaults(
                z => (z?.ViewModel == null || z.ViewModel.Orientation == eDockOrientation.Tabbed)
                    ? Enumerable.Empty<DockZone>()
                    : (IEnumerable<DockZone>)z.ChildZones,
                z => z.ViewModel?.Parent?.UI as DockZone
            );
        }

        public DockZone()
        {
            this.DefaultStyleKey = typeof(DockZone);
            this.ChildZones = new ReadOnlyObservableCollection<DockZone>(m_childZones);
            m_childZones.CollectionChanged += OnChildZonesCollectionChanged;

            // Create a detached ViewModel (no manager). DockingManager will
            // replace this once the zone is registered.
            this.ViewModel = new DockZoneViewModel(manager: null);
        }

        // ===========[ IDockZoneUI ]==========================================

        DockZoneSize IDockZoneUI.RenderSize => new DockZoneSize(this.ActualWidth, this.ActualHeight);

        void IDockZoneUI.SetTargetSizeAsync(List<double> sizes)
        {
            this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                if (this.XamlRoot == null) { return; }
                this.SetTargetSize(sizes);
            });
        }

        // ===========[ Template ]=============================================
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            this.PART_Root = this.GetTemplateChild(nameof(this.PART_Root)) as Grid;
            this.BuildDropOverlay();
            this.RebuildLayout();
        }
        public Grid PART_Root { get; private set; }

        // ===========[ Dependency Properties - ViewModel / Manager ]=========

        public static readonly DependencyProperty ViewModelProperty = DPUtils.Register(_ => _.ViewModel, (d, e) => d.OnViewModelChanged(e));
        public DockZoneViewModel ViewModel
        {
            get => (DockZoneViewModel)this.GetValue(ViewModelProperty);
            set => this.SetValue(ViewModelProperty, value);
        }

        private IDockingManager m_manager;
        public IDockingManager Manager
        {
            get => m_manager;
            internal set
            {
                if (m_manager != value)
                {
                    m_manager = value;
                    if (this.ViewModel != null && this.ViewModel.Manager == null)
                    {
                        this.ViewModel.Manager = m_manager;
                    }

                    this.OnResetIsSetup();
                }
            }
        }

        // ===========[ Dependency Properties - State ]========================

        // IsSetup: true when both ViewModel and Manager are non-null.
        public static readonly DependencyProperty IsSetupProperty = DPUtils.Register(_ => _.IsSetup);
        public bool IsSetup
        {
            get => (bool)this.GetValue(IsSetupProperty);
            private set => this.SetValue(IsSetupProperty, value);
        }

        // IsEmpty: true when ViewModel is null or Orientation == Empty.
        public static readonly DependencyProperty IsEmptyProperty = DPUtils.Register(_ => _.IsEmpty);
        public bool IsEmpty
        {
            get => (bool)this.GetValue(IsEmptyProperty);
            private set => this.SetValue(IsEmptyProperty, value);
        }

        public ReadOnlyObservableCollection<DockZone> ChildZones { get; }

        // ===========[ Attached Properties - GroupId ]========================

        // Identifies a root zone within a DockingManager (used for serialization mapping).
        // Mirrors WPF DockZone.GroupId. Set explicitly or auto-assigned from Name in RegisterRootDockZones.
        public static readonly DependencyProperty GroupIdProperty = APUtils.Register(GetGroupId, SetGroupId);
        public static string GetGroupId(DependencyObject obj) => (string)obj.GetValue(GroupIdProperty);
        public static void SetGroupId(DependencyObject obj, string value) => obj.SetValue(GroupIdProperty, value);

        // ===========[ Dependency Properties - Drag Feedback ]================

        // IsDirectDropTarget: true while a drag-search is hovering over this zone.
        // DockingManager sets/clears this; triggers the drop overlay to show/hide.
        public static readonly DependencyProperty IsDirectDropTargetProperty = DPUtils.Register(
            _ => _.IsDirectDropTarget,
            (d, e) => d.OnIsDirectDropTargetChanged(e)
        );
        public bool IsDirectDropTarget
        {
            get => (bool)this.GetValue(IsDirectDropTargetProperty);
            internal set => this.SetValue(IsDirectDropTargetProperty, value);
        }

        // SeparationSize: pixel width/height of the DockZoneSplitter columns/rows.
        public static readonly DependencyProperty SeparationSizeProperty = DPUtils.Register(_ => _.SeparationSize, 4.0);
        public double SeparationSize
        {
            get => (double)this.GetValue(SeparationSizeProperty);
            set => this.SetValue(SeparationSizeProperty, value);
        }

        // ===========[ Layout Building ]======================================

        private void RebuildLayout()
        {
            if (this.PART_Root == null)
            {
                return;
            }

            // WinUI3 does not auto-detach UIElements from their old parent when a new
            // parent tries to claim them (unlike WPF). Display UIElements are reused
            // across rebuilds, so if a DockLeafLayout's ContentPresenter still holds the
            // display element when we try to reparent it to a NEW ContentPresenter, WinUI3
            // throws a WinRT COM exception. Null out PanelContent on all outgoing leaf
            // layouts BEFORE the Children.Clear() so the display element is detached from
            // its old ContentPresenter while the layout is still properly in the visual tree.
            foreach (var oldLeaf in this.PART_Root.Children.OfType<DockLeafLayout>().ToList())
            {
                oldLeaf.PanelContent = null;
            }

            this.PART_Root.Children.Clear();
            this.PART_Root.RowDefinitions.Clear();
            this.PART_Root.ColumnDefinitions.Clear();

            if (this.ViewModel == null)
            {
                return;
            }

            switch (this.ViewModel.Orientation)
            {
                case eDockOrientation.Empty:
                    this.BuildEmptyLayout();
                    break;

                case eDockOrientation.Single:
                    this.BuildLeafLayout(tabbed: false);
                    break;

                case eDockOrientation.Tabbed:
                    this.BuildLeafLayout(tabbed: true);
                    break;

                case eDockOrientation.Horizontal:
                case eDockOrientation.Vertical:
                    this.BuildSplitLayout();
                    break;
            }

            // Always re-add drop overlay last so it renders above all content
            if (m_dropOverlay != null)
            {
                this.PART_Root.Children.Add(m_dropOverlay);
            }
        }

        private void BuildEmptyLayout()
        {
            var placeholder = new TextBlock
            {
                Text = "Empty",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(20),
                FontSize = 18,
                Opacity = 0.25,
            };
            this.PART_Root.Children.Add(placeholder);
        }

        private void BuildLeafLayout(bool tabbed)
        {
            // Cancel any in-progress header drag (the old leaf layout is being replaced).
            m_headerDragPending = false;

            int selectedIndex = this.ViewModel.SelectedIndex;
            var selectedAdapter = (selectedIndex >= 0 && selectedIndex < this.ViewModel.DockedContent.Count)
                ? this.ViewModel.DockedContent[selectedIndex]
                : this.ViewModel.DockedContent.FirstOrDefault();

            // Header content UIElement: title text + optional close button.
            // Foreground is NOT set here - it inherits from DockLeafLayout.Foreground (set via Style).
            var headerText = new TextBlock
            {
                Text = selectedAdapter?.TitleContent?.ToString() ?? string.Empty,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 4, 6, 4),
            };

            var dockingManager = m_manager as DockingManager;
            var hostWindow = dockingManager?.GetWindowForZone(this);

            // Show the panel-level close button only when this zone is a non-root panel
            // inside a tearoff window. On the root window the OS chrome handles closure.
            // On the root zone of a tearoff the title bar's X button handles closure;
            // adding another X here would be confusing and redundant.
            bool isNonRootInTearoff = dockingManager != null
                && hostWindow != null
                && hostWindow != dockingManager.RootWindow
                && dockingManager.GetRootDockZoneForWindow(hostWindow) != this;

            UIElement headerContent;
            if (isNonRootInTearoff && dockingManager?.ShowPanelClose == true && selectedAdapter != null)
            {
                var headerCloseBtn = new DockCloseButton
                {
                    Padding = new Thickness(6, 3, 6, 3),
                    Margin = new Thickness(2, 2, 2, 2),
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Tag = selectedAdapter,
                };
                headerCloseBtn.Click += this.OnHeaderPanelCloseBtnClicked;

                var hGrid = new Grid();
                hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(headerText, 0);
                Grid.SetColumn(headerCloseBtn, 1);
                hGrid.Children.Add(headerText);
                hGrid.Children.Add(headerCloseBtn);
                headerContent = hGrid;
            }
            else
            {
                headerContent = headerText;
            }

            // WinUI3 does not auto-detach UIElements from their old parent (unlike WPF).
            // If the display element is still hosted in a ContentPresenter from the tearoff
            // that triggered this drop, explicitly detach it before claiming it here.
            var displayElement = selectedAdapter?.Display as UIElement;
            if (displayElement != null)
            {
                if (VisualTreeHelper.GetParent(displayElement) is ContentPresenter existingCp)
                {
                    existingCp.Content = null;
                }
            }

            // DockLeafLayout owns the 3-row visual structure (header / content / tab strip).
            // All visual DPs are pushed directly - WinUI3 TemplateBinding does not backfill.
            var leaf = new DockLeafLayout
            {
                IsTabbed = tabbed,
                HeaderContent = headerContent,
                PanelContent = displayElement,
            };

            if (tabbed)
            {
                leaf.TabNavContent = this.BuildTabNavContent(selectedAdapter);
            }

            // Forward header pointer events to this DockZone's handlers.
            // DockLeafLayout passes this.PART_HeaderBar (a Border) as the event sender,
            // so existing handler casts - (Border)sender - remain valid.
            leaf.HeaderPointerPressed += this.OnHeaderPointerPressed;
            leaf.HeaderPointerMoved += this.OnHeaderPointerMoved;
            leaf.HeaderPointerReleased += this.OnHeaderPointerReleased;
            leaf.HeaderPointerCaptureLost += this.OnHeaderPointerCaptureLost;

            this.PART_Root.Children.Add(leaf);
        }

        // Returns the tab-navigation Grid (left scroll btn + ScrollViewer + right scroll btn).
        // The outer wrapper Border (background, top border, padding) lives in DockLeafLayout's
        // this.PART_TabStripWrapper template part - only the inner content is built here.
        //
        // All per-tab drag logic lives in DockTabItem (events surfaced as high-level named
        // handlers). No anonymous closures - fields m_tab* hold the current strip objects
        // so named handlers can reference them without captures.
        private FrameworkElement BuildTabNavContent(DockingContentAdapterModel selectedAdapter)
        {
            m_tabPanel = new StackPanel { Orientation = Orientation.Horizontal };
            m_tabReorderTargetIdx = -1;
            var dockingManager = m_manager as DockingManager;

            for (int i = 0; i < this.ViewModel.DockedContent.Count; i++)
            {
                var adapter = this.ViewModel.DockedContent[i];
                bool isSelected = adapter == selectedAdapter;

                // Tab border logic:
                //   Left  : first tab and selected tab get a left border (outer edge + selected outline)
                //   Top   : never - the open top visually connects to the content panel above
                //   Right : every tab except those immediately left of a selected tab (selected provides its own left)
                //   Bottom: always - visible outline at the bottom of the tab strip
                bool nextIsSelected = (i + 1 < this.ViewModel.DockedContent.Count)
                    && (this.ViewModel.DockedContent[i + 1] == selectedAdapter);

                int leftBorder = (i == 0 || isSelected) ? 1 : 0;
                int rightBorder = nextIsSelected ? 0 : 1;

                var tabItem = new DockTabItem
                {
                    Index = i,
                    IsSelected = isSelected,
                    BorderThickness = new Thickness(leftBorder, 0, rightBorder, 1),
                    Content = this.BuildTabContent(adapter, i, dockingManager),
                    DragThresholdPixels = dockingManager?.DragThresholdPixels ?? DockTabItem.kDefaultDragThresholdPixels,
                };

                tabItem.TabSelectionRequested        += this.OnTabSelectionRequested;
                tabItem.TabMiddleClickCloseRequested += this.OnTabMiddleClickCloseRequested;
                tabItem.TabTearoffDragInitiated      += this.OnTabTearoffDragInitiated;
                tabItem.TabReorderDragMoved          += this.OnTabReorderDragMoved;
                tabItem.TabReorderDropped            += this.OnTabReorderDropped;
                tabItem.TabDragCancelled             += this.OnTabDragCancelled;

                m_tabPanel.Children.Add(tabItem);
            }

            // Trailing spacer: when fully scrolled right, the right scroll button disappears.
            // Without a spacer the cursor lands on the last tab's X button. The spacer adds
            // dead empty space so there is nothing clickable under the vanishing right button.
            m_tabPanel.Children.Add(new Border { Width = 32 });

            m_tabScrollViewer = new ScrollViewer
            {
                HorizontalScrollMode = ScrollMode.Enabled,
                VerticalScrollMode = ScrollMode.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = m_tabPanel,
            };

            m_tabLeftScrollBtn = new DockTabScrollButton
            {
                Content = "◂",
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            m_tabLeftScrollBtn.Click += this.OnTabScrollLeft;

            m_tabRightScrollBtn = new DockTabScrollButton
            {
                Content = "▸",
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            m_tabRightScrollBtn.Click += this.OnTabScrollRight;

            m_tabScrollViewer.ViewChanged   += this.OnTabScrollViewerViewChanged;
            m_tabPanel.SizeChanged          += this.OnTabPanelSizeChanged;
            m_tabScrollViewer.SizeChanged   += this.OnTabScrollViewerSizeChanged;

            var navGrid = new Grid();
            navGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            navGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            navGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(m_tabLeftScrollBtn, 0);
            Grid.SetColumn(m_tabScrollViewer, 1);
            Grid.SetColumn(m_tabRightScrollBtn, 2);
            navGrid.Children.Add(m_tabLeftScrollBtn);
            navGrid.Children.Add(m_tabScrollViewer);
            navGrid.Children.Add(m_tabRightScrollBtn);

            // Return only the inner nav grid. The outer wrapper (background, top border, padding)
            // is this.PART_TabStripWrapper in DockLeafLayout's ControlTemplate - see DockLeafLayout.xaml.
            return navGrid;
        }

        // Builds the content UIElement for a single tab (label + optional close button).
        private UIElement BuildTabContent(DockingContentAdapterModel adapter, int index, DockingManager dockingManager)
        {
            var tabLabel = new TextBlock
            {
                Text = adapter.TitleContent?.ToString() ?? $"Tab {index + 1}",
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 3, 6, 3),
            };

            if (dockingManager?.ShowTabHeaderClose != true)
            {
                return tabLabel;
            }

            var tabCloseBtn = new DockCloseButton
            {
                Padding = new Thickness(3, 1, 3, 1),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 10,
                Margin = new Thickness(2),
                Tag = adapter,
            };

            // Stop PointerPressed from bubbling to DockTabItem's OnPointerPressed so the
            // drag-start handler is not triggered when clicking the close button.
            tabCloseBtn.AddHandler(
                UIElement.PointerPressedEvent,
                (PointerEventHandler)SuppressPointerPressed,
                handledEventsToo: false
            );
            tabCloseBtn.Click += this.OnTabCloseBtnClicked;

            var tabInner = new StackPanel { Orientation = Orientation.Horizontal };
            tabInner.Children.Add(tabLabel);
            tabInner.Children.Add(tabCloseBtn);
            return tabInner;
        }

        // Returns the index of the tab whose x-range contains cursorX within the panel.
        // Only counts DockTabItem children; the trailing spacer Border is skipped.
        private static int FindTabIndexAtX(StackPanel panel, double cursorX)
        {
            double accum = 0;
            int realIndex = 0;
            for (int i = 0; i < panel.Children.Count; i++)
            {
                if (panel.Children[i] is not DockTabItem child)
                {
                    continue;
                }

                accum += child.ActualWidth;
                ++realIndex;

                if (cursorX < accum)
                {
                    return realIndex - 1;
                }
            }

            return Math.Max(0, realIndex - 1);
        }

        // ===========[ Tab Strip Event Handlers ]================================

        private void OnTabSelectionRequested(object sender, int tabIndex)
        {
            if (this.ViewModel != null
                && tabIndex >= 0
                && tabIndex < this.ViewModel.DockedContent.Count)
            {
                this.ViewModel.SelectedIndex = tabIndex;
            }
        }

        private void OnTabMiddleClickCloseRequested(object sender, int tabIndex)
        {
            if ((m_manager as DockingManager)?.AllowMiddleMouseClose != true)
            {
                return;
            }

            var adapter = this.ViewModel?.DockedContent.ElementAtOrDefault(tabIndex);
            adapter?.Location?.RequestCloseAndRemoveDockedContent(adapter);
        }

        private async void OnTabTearoffDragInitiated(object sender, EventArgs e)
        {
            var tabItem = (DockTabItem)sender;
            var adapter = this.ViewModel?.DockedContent.ElementAtOrDefault(tabItem.Index);
            var mgr = m_manager as DockingManager;
            if (mgr != null && adapter?.Display != null)
            {
                await mgr.InitiateDragForContent(adapter.Display);
            }
        }

        private void OnTabReorderDragMoved(object sender, PointerRoutedEventArgs e)
        {
            if (m_tabPanel == null)
            {
                return;
            }

            double cursorX = e.GetCurrentPoint(m_tabPanel).Position.X;
            m_tabReorderTargetIdx = FindTabIndexAtX(m_tabPanel, cursorX);
        }

        private void OnTabReorderDropped(object sender, int sourceIndex)
        {
            int targetIdx = m_tabReorderTargetIdx;
            m_tabReorderTargetIdx = -1;

            if (targetIdx >= 0
                && targetIdx != sourceIndex
                && targetIdx < this.ViewModel?.DockedContent.Count)
            {
                this.ViewModel.SwapDockedContentOrder(sourceIndex, targetIdx);
                this.ViewModel.SelectedIndex = targetIdx;
            }
        }

        private void OnTabDragCancelled(object sender, EventArgs e)
        {
            m_tabReorderTargetIdx = -1;
        }

        private void OnTabCloseBtnClicked(object sender, RoutedEventArgs e)
        {
            if (sender is DockCloseButton btn && btn.Tag is DockingContentAdapterModel adapter)
            {
                adapter.Location?.RequestCloseAndRemoveDockedContent(adapter);
            }
        }

        private void OnHeaderPanelCloseBtnClicked(object sender, RoutedEventArgs e)
        {
            if (sender is DockCloseButton btn && btn.Tag is DockingContentAdapterModel adapter)
            {
                adapter.Location?.RequestCloseAndRemoveDockedContent(adapter);
            }
        }

        private static void SuppressPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void OnTabScrollLeft(object sender, RoutedEventArgs e)
        {
            m_tabScrollViewer?.ChangeView(m_tabScrollViewer.HorizontalOffset - 80, null, null);
        }

        private void OnTabScrollRight(object sender, RoutedEventArgs e)
        {
            m_tabScrollViewer?.ChangeView(m_tabScrollViewer.HorizontalOffset + 80, null, null);
        }

        private void OnTabScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            this.UpdateTabScrollButtons();
        }

        private void OnTabPanelSizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.UpdateTabScrollButtons();
        }

        private void OnTabScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.UpdateTabScrollButtons();
        }

        private void UpdateTabScrollButtons()
        {
            if (m_tabPanel == null || m_tabScrollViewer == null
                || m_tabLeftScrollBtn == null || m_tabRightScrollBtn == null)
            {
                return;
            }

            double offset = m_tabScrollViewer.HorizontalOffset;
            bool needsScroll = m_tabPanel.ActualWidth > m_tabScrollViewer.ActualWidth;
            m_tabLeftScrollBtn.Visibility = (needsScroll && offset > 0)
                ? Visibility.Visible : Visibility.Collapsed;
            m_tabRightScrollBtn.Visibility = (needsScroll && offset < m_tabScrollViewer.ScrollableWidth)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BuildSplitLayout()
        {
            bool isHorizontal = this.ViewModel.Orientation == eDockOrientation.Horizontal;
            double separationSize = this.SeparationSize;

            for (int i = 0; i < m_childZones.Count; i++)
            {
                var child = m_childZones[i];

                if (isHorizontal)
                {
                    this.PART_Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    Grid.SetColumn(child, i * 2);
                    this.PART_Root.Children.Add(child);

                    if (i < m_childZones.Count - 1)
                    {
                        this.PART_Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(separationSize) });
                        var splitter = new DockZoneSplitter();
                        splitter.Setup(this.PART_Root, i, eDockOrientation.Horizontal);
                        Grid.SetColumn(splitter, i * 2 + 1);
                        this.PART_Root.Children.Add(splitter);
                    }
                }
                else // Vertical
                {
                    this.PART_Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    Grid.SetRow(child, i * 2);
                    this.PART_Root.Children.Add(child);

                    if (i < m_childZones.Count - 1)
                    {
                        this.PART_Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(separationSize) });
                        var splitter = new DockZoneSplitter();
                        splitter.Setup(this.PART_Root, i, eDockOrientation.Vertical);
                        Grid.SetRow(splitter, i * 2 + 1);
                        this.PART_Root.Children.Add(splitter);
                    }
                }
            }

            // After a layout pass, apply stored pass-along sizes (e.g. from serialization).
            this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                if (this.XamlRoot == null) { return; }
                this.HandleNewChildZonesAdded(m_childZones);
            });
        }

        private void HandleNewChildZonesAdded(IEnumerable<DockZone> added)
        {
            if (added == null || !added.Any() || this.PART_Root == null || this.ViewModel == null)
            {
                return;
            }

            // Only applies when zones have stored pass-along sizes from serialization.
            // If no sizes are stored and the root has no actual dimensions yet, skip.
            DockZoneSize rootSize = ((IDockZoneUI)this).RenderSize;

            var sizes = this.ChildZones
                .Select(z => z.ViewModel.TakePassAlongUISize(out DockZoneSize stored) ? stored : ((IDockZoneUI)z).RenderSize)
                .ToList();

            if (this.ViewModel.Orientation == eDockOrientation.Horizontal)
            {
                double full = sizes.Sum(s => s.Width);
                if (full > 0 && rootSize.Width > 0)
                {
                    var pixelSizes = sizes.Select(s => rootSize.Width * (s.Width / full)).ToList();
                    this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                    {
                        if (this.XamlRoot == null) { return; }
                        this.SetTargetSize(pixelSizes);
                    });
                }
            }
            else if (this.ViewModel.Orientation == eDockOrientation.Vertical)
            {
                double full = sizes.Sum(s => s.Height);
                if (full > 0 && rootSize.Height > 0)
                {
                    var pixelSizes = sizes.Select(s => rootSize.Height * (s.Height / full)).ToList();
                    this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                    {
                        if (this.XamlRoot == null) { return; }
                        this.SetTargetSize(pixelSizes);
                    });
                }
            }
        }

        internal void SetTargetSize(List<double> sizes)
        {
            if (this.PART_Root == null || this.ViewModel == null)
            {
                return;
            }

            bool isHorizontal = this.ViewModel.Orientation == eDockOrientation.Horizontal;
            int count = Math.Min(m_childZones.Count, sizes.Count);

            for (int i = 0; i < count; i++)
            {
                int defIndex = i * 2;
                if (isHorizontal && defIndex < this.PART_Root.ColumnDefinitions.Count)
                {
                    this.PART_Root.ColumnDefinitions[defIndex].Width = new GridLength(sizes[i], GridUnitType.Star);
                }
                else if (!isHorizontal && defIndex < this.PART_Root.RowDefinitions.Count)
                {
                    this.PART_Root.RowDefinitions[defIndex].Height = new GridLength(sizes[i], GridUnitType.Star);
                }
            }
        }

        // ===========[ ViewModel Change Handling ]============================

        private void OnViewModelChanged(DependencyPropertyChangedEventArgs<DockZoneViewModel> e)
        {
            if (e.HasOldValue)
            {
                if (e.OldValue.UI == (IDockZoneUI)this)
                {
                    e.OldValue.UI = null;
                }

                ((INotifyCollectionChanged)e.OldValue.Children).CollectionChanged -= OnViewModelChildrenChanged;
                ((INotifyCollectionChanged)e.OldValue.DockedContent).CollectionChanged -= OnViewModelDockedContentChanged;
                e.OldValue.PropertyChanged -= OnViewModelPropertyChanged;

                // Remove any child zones that were created for the old ViewModel
                var oldChildren = m_childZones.ToList();
                m_childZones.Clear();
                foreach (var rm in oldChildren)
                {
                    rm.ViewModel?.DestroyUIReference();
                }
            }

            if (e.HasNewValue)
            {
                this.Manager = e.NewValue.Manager;
                e.NewValue.UI = this;

                ((INotifyCollectionChanged)e.NewValue.Children).CollectionChanged -= OnViewModelChildrenChanged;
                ((INotifyCollectionChanged)e.NewValue.Children).CollectionChanged += OnViewModelChildrenChanged;
                ((INotifyCollectionChanged)e.NewValue.DockedContent).CollectionChanged -= OnViewModelDockedContentChanged;
                ((INotifyCollectionChanged)e.NewValue.DockedContent).CollectionChanged += OnViewModelDockedContentChanged;
                e.NewValue.PropertyChanged -= OnViewModelPropertyChanged;
                e.NewValue.PropertyChanged += OnViewModelPropertyChanged;

                // Populate initial child zones for existing split hierarchies
                int idx = 0;
                foreach (DockZoneViewModel child in e.NewValue.Children)
                {
                    m_childZones.Insert(idx++, new DockZone { ViewModel = child });
                }
            }
            else
            {
                this.Manager = null;
            }

            this.IsEmpty = this.ViewModel == null || this.ViewModel.Orientation == eDockOrientation.Empty;
            this.OnResetIsSetup();
            this.RebuildLayout();
        }

        private void OnViewModelChildrenChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                var toRemove = m_childZones.Where(c => e.OldItems.Contains(c.ViewModel)).ToList();
                foreach (var rm in toRemove)
                {
                    m_childZones.Remove(rm);
                    rm.ViewModel?.DestroyUIReference();
                }
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                var copy = m_childZones.ToList();
                m_childZones.Clear();
                foreach (var rm in copy)
                {
                    rm.ViewModel?.DestroyUIReference();
                }
            }

            if (e.NewItems != null)
            {
                int index = e.NewStartingIndex;
                foreach (DockZoneViewModel child in e.NewItems)
                {
                    m_childZones.Insert(index++, new DockZone { ViewModel = child });
                }
            }
        }

        private void OnViewModelDockedContentChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            this.RebuildLayout();
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            this.IsEmpty = this.ViewModel == null || this.ViewModel.Orientation == eDockOrientation.Empty;

            if (e.PropertyName == nameof(DockZoneViewModel.Orientation)
                || e.PropertyName == nameof(DockZoneViewModel.SelectedIndex))
            {
                this.RebuildLayout();
            }
        }

        private void OnChildZonesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // When split-layout children change at runtime, rebuild the split structure.
            if (this.PART_Root != null && this.ViewModel != null
                && (this.ViewModel.Orientation & eDockOrientation.AnySplitOrientation) != 0)
            {
                this.PART_Root.Children.Clear();
                this.PART_Root.RowDefinitions.Clear();
                this.PART_Root.ColumnDefinitions.Clear();
                this.BuildSplitLayout();

                // Re-add overlay above all split content
                if (m_dropOverlay != null)
                {
                    this.PART_Root.Children.Add(m_dropOverlay);
                }
            }
        }

        private void OnResetIsSetup()
        {
            this.IsSetup = this.ViewModel != null && this.Manager != null;
        }

        // Called by DockingManager after a zone is fully registered in m_dockZoneMapping
        // so that GetWindowForZone resolves correctly during layout (fixes close-button
        // visibility and header-drag suppression for tearoff root zones).
        internal void TriggerLayoutRebuild() => this.RebuildLayout();

        // ===========[ Drop Overlay ]=========================================

        private void BuildDropOverlay()
        {
            // 3×3 grid (84×60 px) centered over the zone - shown when IsDirectDropTarget=true
            //   Col layout: 20 | 44 | 20
            //   Row layout: 19 | 22 | 19
            //   [1,0]=Left  [0,1]=Top  [1,2]=Right  [2,1]=Bottom  [1,1]=Center(AddToTabbed)
            m_overlayLeft = new DockDropInsertionDriverWidget { Direction = eDockInsertionDirection.Left, InsertionZone = this };
            m_overlayTop = new DockDropInsertionDriverWidget { Direction = eDockInsertionDirection.Top, InsertionZone = this };
            m_overlayRight = new DockDropInsertionDriverWidget { Direction = eDockInsertionDirection.Right, InsertionZone = this };
            m_overlayBottom = new DockDropInsertionDriverWidget { Direction = eDockInsertionDirection.Bottom, InsertionZone = this };
            m_overlayCenter = new DockDropInsertionDriverWidget { Direction = eDockInsertionDirection.AddToTabbedDisplay, InsertionZone = this };
            var left = m_overlayLeft;
            var top = m_overlayTop;
            var right = m_overlayRight;
            var bottom = m_overlayBottom;
            var center = m_overlayCenter;

            var grid = new Grid
            {
                Width = 84,
                Height = 60,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
            };

            // Span all rows/columns so the grid centers over the full DockZone
            Grid.SetRowSpan(grid, 99);
            Grid.SetColumnSpan(grid, 99);

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(19) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(19) });

            Grid.SetRow(left, 1); Grid.SetColumn(left, 0);
            Grid.SetRow(top, 0); Grid.SetColumn(top, 1);
            Grid.SetRow(right, 1); Grid.SetColumn(right, 2);
            Grid.SetRow(bottom, 2); Grid.SetColumn(bottom, 1);
            Grid.SetRow(center, 1); Grid.SetColumn(center, 1);

            grid.Children.Add(left);
            grid.Children.Add(top);
            grid.Children.Add(right);
            grid.Children.Add(bottom);
            grid.Children.Add(center);

            m_dropOverlay = grid;
        }

        private void OnIsDirectDropTargetChanged(DependencyPropertyChangedEventArgs<bool> e)
        {
            if (m_dropOverlay == null)
            {
                return;
            }

            if (!e.NewValue)
            {
                m_dropOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            // Empty zones can only accept content to fill them; they can't be split.
            // Leaf zones (Single/Tabbed) support both directional splits and tab-insertion.
            // Split zones (Horizontal/Vertical) support only directional splits - they cannot
            // accept direct tab content alongside their child zones.
            // The center "+" stays visible for all orientations but is disabled (50% opacity)
            // for split zones so users can see it exists without being able to misuse it.
            var orientation = this.ViewModel?.Orientation ?? eDockOrientation.Empty;
            bool canSplit      = orientation != eDockOrientation.Empty;
            bool isLeaf        = orientation == eDockOrientation.Single || orientation == eDockOrientation.Tabbed;
            bool centerEnabled = !canSplit || isLeaf;
            var splitVis = canSplit ? Visibility.Visible : Visibility.Collapsed;
            m_overlayLeft.Visibility   = splitVis;
            m_overlayTop.Visibility    = splitVis;
            m_overlayRight.Visibility  = splitVis;
            m_overlayBottom.Visibility = splitVis;
            m_overlayCenter.Visibility = Visibility.Visible;
            m_overlayCenter.IsEnabled  = centerEnabled;
            m_overlayCenter.Opacity    = centerEnabled ? 1.0 : 0.5;

            m_dropOverlay.Visibility = Visibility.Visible;
        }

        // ===========[ Header drag (threshold-based) ]========================

        // On pointer-press: record the press point and determine which zone to drag.
        // Actual drag is not initiated until PointerMoved crosses DragThresholdPixels.
        private void OnHeaderPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var manager = m_manager as DockingManager;
            if (manager == null)
            {
                return;
            }

            var hostWindow = manager.GetWindowForZone(this);

            if (hostWindow != manager.RootWindow)
            {
                // This zone lives inside a tearoff window.
                var rootZone = manager.GetRootDockZoneForWindow(hostWindow);

                if (rootZone == this)
                {
                    // This IS the root zone of the tearoff. The title bar handles all
                    // window-level drag; suppress the panel header entirely.
                    return;
                }

                // Non-root panel in a tearoff. If the tearoff root is a split zone
                // (Horizontal/Vertical) this zone can be torn off independently - the
                // remaining sibling(s) keep the root non-empty, which is required.
                // If the root is a leaf (Single/Tabbed), tearing off would empty it,
                // which is not allowed; drag the whole tearoff window instead.
                bool rootIsSplit = rootZone?.ViewModel?.Orientation
                    .IsFlagInGroup(eDockOrientation.AnySplitOrientation) == true;
                m_headerDragZone = rootIsSplit ? this : rootZone;
            }
            else
            {
                // Root window - dragging tears this zone off.
                m_headerDragZone = this;
            }

            var border = (Border)sender;
            border.CapturePointer(e.Pointer);
            m_headerDragLocalPressPt = e.GetCurrentPoint(border).Position;
            manager.TryGetCursorScreenPos(out m_headerDragScreenPressPt);
            m_headerDragPending = true;
            e.Handled = true;
        }

        // On pointer-move: initiate drag once threshold is crossed.
        private void OnHeaderPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!m_headerDragPending)
            {
                return;
            }

            var border = (Border)sender;
            var cur = e.GetCurrentPoint(border).Position;
            double dx = cur.X - m_headerDragLocalPressPt.X;
            double dy = cur.Y - m_headerDragLocalPressPt.Y;

            var manager = m_manager as DockingManager;
            double threshold = manager?.DragThresholdPixels ?? 8.0;

            if (dx * dx + dy * dy > threshold * threshold)
            {
                m_headerDragPending = false;
                border.ReleasePointerCapture(e.Pointer);
                e.Handled = true;

                if (manager == null || m_headerDragZone == null)
                {
                    return;
                }

                // Pass the screen press position so the window appears with the cursor
                // at the same relative position. The header is inside the DockZone which
                // sits at row 1 (after the 30 px custom title bar) of the tearoff window,
                // so add 30 to the Y component - but only when we're tearing off from the
                // root window (creating a brand-new tearoff). For moves of an existing
                // tearoff (m_headerDragZone != this) the tearoff window already exists
                // and we don't have a reliable offset, so fall back to default centering.
                Point? windowOffset = (m_headerDragZone == this)
                    ? new Point(m_headerDragLocalPressPt.X, 30.0 + m_headerDragLocalPressPt.Y)
                    : (Point?)null;

                _ = manager.InitiateDrag(m_headerDragZone, m_headerDragScreenPressPt, windowOffset);
            }
        }

        private void OnHeaderPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            m_headerDragPending = false;
            ((Border)sender).ReleasePointerCapture(e.Pointer);
        }

        private void OnHeaderPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            m_headerDragPending = false;
        }
    }
}
