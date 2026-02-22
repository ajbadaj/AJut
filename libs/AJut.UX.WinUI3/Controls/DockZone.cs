namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using AJut.Tree;
    using AJut.UX.Docking;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using APUtils = AJut.UX.APUtils<DockZone>;
    using DPUtils = AJut.UX.DPUtils<DockZone>;

    using Windows.Foundation;

    // ===========[ DockZone ]====================================================
    // WinUI3 docking zone control. Backed by a DockZoneViewModel which drives
    // the layout: Empty / Single / Tabbed / Horizontal-split / Vertical-split.
    //
    // Implements IDockZoneUI so DockZoneViewModel can be platform-agnostic:
    //   RenderSize       → ActualWidth / ActualHeight
    //   SetTargetSizeAsync → schedules SetTargetSize on the DispatcherQueue
    //
    // The template contains only PART_Root (a Grid). All layout elements
    // (headers, content presenters, tab strips, child DockZones, DockZoneSplitters)
    // are built entirely in code-behind via RebuildLayout() whenever the
    // ViewModel's Orientation or DockedContent changes.
    //
    // Drag-and-drop (tearoff, zone reorder) is deferred to a later session.
    //
    // Template part:
    //   PART_Root — Grid populated by code-behind

    [TemplatePart(Name = nameof(PART_Root), Type = typeof(Grid))]
    public sealed class DockZone : Control, IDockZoneUI
    {
        // ===========[ Fields ]===============================================
        private Grid PART_Root;
        private Grid m_dropOverlay;
        private DockDropInsertionDriverWidget m_overlayLeft, m_overlayTop, m_overlayRight, m_overlayBottom, m_overlayCenter;
        private readonly ObservableCollection<DockZone> m_childZones = new();

        // Header-drag threshold state (reset at each RebuildLayout)
        private bool m_headerDragPending;
        private Point m_headerDragLocalPressPt;
        private Point m_headerDragScreenPressPt;
        private DockZone m_headerDragZone;   // zone to pass to InitiateDrag

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
            this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => this.SetTargetSize(sizes));
        }

        // ===========[ Template ]=============================================
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            PART_Root = this.GetTemplateChild(nameof(PART_Root)) as Grid;
            this.BuildDropOverlay();
            this.RebuildLayout();
        }

        // ===========[ Dependency Properties — ViewModel / Manager ]=========

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

        // ===========[ Dependency Properties — State ]========================

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

        // ===========[ Dependency Properties — Styling ]======================

        public static readonly DependencyProperty PanelBackgroundProperty = DPUtils.Register(_ => _.PanelBackground);
        public Brush PanelBackground
        {
            get => (Brush)this.GetValue(PanelBackgroundProperty);
            set => this.SetValue(PanelBackgroundProperty, value);
        }

        public static readonly DependencyProperty PanelForegroundProperty = DPUtils.Register(_ => _.PanelForeground);
        public Brush PanelForeground
        {
            get => (Brush)this.GetValue(PanelForegroundProperty);
            set => this.SetValue(PanelForegroundProperty, value);
        }

        public static readonly DependencyProperty PanelBorderBrushProperty = DPUtils.Register(_ => _.PanelBorderBrush);
        public Brush PanelBorderBrush
        {
            get => (Brush)this.GetValue(PanelBorderBrushProperty);
            set => this.SetValue(PanelBorderBrushProperty, value);
        }

        public static readonly DependencyProperty PanelBorderThicknessProperty = DPUtils.Register(_ => _.PanelBorderThickness);
        public Thickness PanelBorderThickness
        {
            get => (Thickness)this.GetValue(PanelBorderThicknessProperty);
            set => this.SetValue(PanelBorderThicknessProperty, value);
        }

        public static readonly DependencyProperty PanelCornerRadiusProperty = DPUtils.Register(_ => _.PanelCornerRadius);
        public CornerRadius PanelCornerRadius
        {
            get => (CornerRadius)this.GetValue(PanelCornerRadiusProperty);
            set => this.SetValue(PanelCornerRadiusProperty, value);
        }

        // Background for the tab strip row — typically darker than PanelBackground.
        public static readonly DependencyProperty TabStripBackgroundProperty = DPUtils.Register(_ => _.TabStripBackground);
        public Brush TabStripBackground
        {
            get => (Brush)this.GetValue(TabStripBackgroundProperty);
            set => this.SetValue(TabStripBackgroundProperty, value);
        }

        // ===========[ Attached Properties — GroupId ]========================

        // Identifies a root zone within a DockingManager (used for serialization mapping).
        // Mirrors WPF DockZone.GroupId. Set explicitly or auto-assigned from Name in RegisterRootDockZones.
        public static readonly DependencyProperty GroupIdProperty = APUtils.Register(GetGroupId, SetGroupId);
        public static string GetGroupId(DependencyObject obj) => (string)obj.GetValue(GroupIdProperty);
        public static void SetGroupId(DependencyObject obj, string value) => obj.SetValue(GroupIdProperty, value);

        // ===========[ Dependency Properties — Drag Feedback ]================

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
            if (PART_Root == null)
            {
                return;
            }

            PART_Root.Children.Clear();
            PART_Root.RowDefinitions.Clear();
            PART_Root.ColumnDefinitions.Clear();

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
                PART_Root.Children.Add(m_dropOverlay);
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
            PART_Root.Children.Add(placeholder);
        }

        private void BuildLeafLayout(bool tabbed)
        {
            // Row layout:
            //   0 (Auto)  — header bar (title of the selected panel)
            //   1 (*)     — content presenter
            //   2 (Auto)  — tab strip (tabbed mode only)

            // Cancel any in-progress header drag (the old header border is being replaced).
            m_headerDragPending = false;

            PART_Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            PART_Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            if (tabbed)
            {
                PART_Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            int selectedIndex = this.ViewModel.SelectedIndex;
            var selectedAdapter = (selectedIndex >= 0 && selectedIndex < this.ViewModel.DockedContent.Count)
                ? this.ViewModel.DockedContent[selectedIndex]
                : this.ViewModel.DockedContent.FirstOrDefault();

            // Header bar
            var headerText = new TextBlock
            {
                Text = selectedAdapter?.TitleContent?.ToString() ?? string.Empty,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 4, 6, 4),
            };
            if (this.PanelForeground != null)
            {
                headerText.Foreground = this.PanelForeground;
            }

            var dockingManager = m_manager as DockingManager;
            var hostWindow     = dockingManager?.GetWindowForZone(this);

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
                var headerCloseBtn = new Button
                {
                    Content = "✕",
                    Padding = new Thickness(6, 3, 6, 3),
                    VerticalAlignment = VerticalAlignment.Stretch,
                    BorderThickness = new Thickness(0),
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                    Foreground = this.PanelForeground,
                };
                var capturedAdapter = selectedAdapter;
                headerCloseBtn.Click += (s, e) =>
                {
                    capturedAdapter.Location?.RequestCloseAndRemoveDockedContent(capturedAdapter);
                };

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

            var headerBorder = new Border
            {
                Background = this.PanelBackground,
                BorderBrush = this.PanelBorderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = headerContent,
            };
            headerBorder.PointerPressed      += this.OnHeaderPointerPressed;
            headerBorder.PointerMoved        += this.OnHeaderPointerMoved;
            headerBorder.PointerReleased     += this.OnHeaderPointerReleased;
            headerBorder.PointerCaptureLost  += this.OnHeaderPointerCaptureLost;
            Grid.SetRow(headerBorder, 0);
            PART_Root.Children.Add(headerBorder);

            // Content presenter
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

            var contentPresenter = new ContentPresenter
            {
                Content = displayElement,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            var contentBorder = new Border
            {
                Background = this.PanelBackground,
                BorderBrush = this.PanelBorderBrush,
                BorderThickness = this.PanelBorderThickness,
                CornerRadius = this.PanelCornerRadius,
                Child = contentPresenter,
            };
            Grid.SetRow(contentBorder, 1);
            PART_Root.Children.Add(contentBorder);

            // Tab strip (tabbed mode only)
            if (tabbed)
            {
                var tabStrip = this.BuildTabStrip(selectedAdapter);
                Grid.SetRow(tabStrip, 2);
                PART_Root.Children.Add(tabStrip);
            }
        }

        private FrameworkElement BuildTabStrip(DockingContentAdapterModel selectedAdapter)
        {
            var tabPanel       = new StackPanel { Orientation = Orientation.Horizontal };
            var dockingManager = m_manager as DockingManager;

            for (int i = 0; i < this.ViewModel.DockedContent.Count; i++)
            {
                var adapter      = this.ViewModel.DockedContent[i];
                int capturedIndex = i;
                bool isSelected   = adapter == selectedAdapter;

                var tabLabel = new TextBlock
                {
                    Text              = adapter.TitleContent?.ToString() ?? $"Tab {i + 1}",
                    Foreground        = this.PanelForeground,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding           = new Thickness(6, 3, 6, 3),
                };

                UIElement tabContent;
                if (dockingManager?.ShowTabHeaderClose == true)
                {
                    var tabCloseBtn = new Button
                    {
                        Content           = "✕",
                        Padding           = new Thickness(3, 1, 3, 1),
                        VerticalAlignment = VerticalAlignment.Center,
                        BorderThickness   = new Thickness(0),
                        Background        = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                        Foreground        = this.PanelForeground,
                        FontSize          = 10,
                    };
                    tabCloseBtn.Click += (s, e) =>
                    {
                        adapter.Location?.RequestCloseAndRemoveDockedContent(adapter);
                    };
                    // Stop PointerPressed from bubbling to tabItem so the drag-start
                    // handler ignores clicks that land on the close button.
                    tabCloseBtn.AddHandler(
                        UIElement.PointerPressedEvent,
                        (PointerEventHandler)((s2, e2) => e2.Handled = true),
                        handledEventsToo: false);

                    var tabInner = new StackPanel { Orientation = Orientation.Horizontal };
                    tabInner.Children.Add(tabLabel);
                    tabInner.Children.Add(tabCloseBtn);
                    tabContent = tabInner;
                }
                else
                {
                    tabContent = tabLabel;
                }

                // Issue 7: per-tab left/right border; selected tab has no bottom border
                // and a slight negative top margin so it visually merges with the content.
                var tabItem = new Border
                {
                    Background      = this.PanelBackground,
                    Opacity         = isSelected ? 1.0 : 0.6,
                    BorderBrush     = this.PanelBorderBrush,
                    BorderThickness = new Thickness(1, 0, 1, 0),
                    Margin          = new Thickness(i == 0 ? 0 : 2, isSelected ? -1 : 0, 0, 0),
                    Child           = tabContent,
                };

                // Per-tab drag state — closures capture these per-iteration
                bool   isPressedForDrag  = false;
                bool   isDragModeDecided = false;
                bool   isReorderDrag     = false;
                int    reorderTargetIdx  = capturedIndex;
                Point  dragStartPt       = default;
                Point  screenPressPt     = default;

                tabItem.PointerPressed += (s, e) =>
                {
                    // Middle-click closes the panel
                    if (dockingManager?.AllowMiddleMouseClose == true
                        && e.GetCurrentPoint(tabItem).Properties.IsMiddleButtonPressed)
                    {
                        e.Handled = true;
                        adapter.Location?.RequestCloseAndRemoveDockedContent(adapter);
                        return;
                    }

                    tabItem.CapturePointer(e.Pointer);
                    dragStartPt   = e.GetCurrentPoint(tabItem).Position;
                    dockingManager?.TryGetCursorScreenPos(out screenPressPt);
                    isPressedForDrag  = true;
                    isDragModeDecided = false;
                    isReorderDrag     = false;
                    reorderTargetIdx  = capturedIndex;
                };

                tabItem.PointerMoved += (s, e) =>
                {
                    if (!isPressedForDrag)
                    {
                        return;
                    }

                    var curPt = e.GetCurrentPoint(tabItem).Position;
                    double dx = curPt.X - dragStartPt.X;
                    double dy = curPt.Y - dragStartPt.Y;

                    var mgr = m_manager as DockingManager;
                    double threshold = mgr?.DragThresholdPixels ?? 8.0;

                    if (!isDragModeDecided)
                    {
                        if (dx * dx + dy * dy < threshold * threshold)
                        {
                            return;  // Below threshold — not yet decided
                        }

                        // Decide mode based on dominant direction.
                        isDragModeDecided = true;
                        isReorderDrag     = Math.Abs(dx) > Math.Abs(dy);

                        if (!isReorderDrag)
                        {
                            // Vertical → tearoff mode: initiate immediately
                            isPressedForDrag  = false;
                            isDragModeDecided = false;
                            tabItem.ReleasePointerCapture(e.Pointer);
                            e.Handled = true;

                            if (mgr != null && adapter.Display != null)
                            {
                                _ = mgr.InitiateDragForContent(adapter.Display, screenPressPt);
                            }

                            return;
                        }
                    }

                    // Reorder mode: track which slot the cursor is over
                    if (isReorderDrag)
                    {
                        double cursorInPanel = e.GetCurrentPoint(tabPanel).Position.X;
                        reorderTargetIdx = FindTabIndexAtX(tabPanel, cursorInPanel);
                    }
                };

                tabItem.PointerReleased += (s, e) =>
                {
                    if (isPressedForDrag)
                    {
                        if (isDragModeDecided && isReorderDrag
                            && reorderTargetIdx != capturedIndex
                            && reorderTargetIdx >= 0
                            && reorderTargetIdx < this.ViewModel.DockedContent.Count)
                        {
                            // Execute reorder — triggers RebuildLayout which clears all tab items
                            this.ViewModel.SwapDockedContentOrder(capturedIndex, reorderTargetIdx);
                        }
                        else if (!isDragModeDecided)
                        {
                            // Quick click without threshold → switch tab
                            this.ViewModel.SelectedIndex = capturedIndex;
                        }
                    }

                    isPressedForDrag  = false;
                    isDragModeDecided = false;
                    isReorderDrag     = false;
                };

                tabItem.PointerCaptureLost += (s, e) =>
                {
                    isPressedForDrag  = false;
                    isDragModeDecided = false;
                    isReorderDrag     = false;
                };

                tabPanel.Children.Add(tabItem);
            }

            // Issue 4: wrap tab panel in a ScrollViewer with left/right nav buttons.
            // Buttons appear only when the tabs overflow the available width, and are
            // individually hidden when already at the start or end of the scroll range.
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollMode          = ScrollMode.Enabled,
                VerticalScrollMode            = ScrollMode.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Disabled,
                Content                       = tabPanel,
            };

            var leftScrollBtn = new Button
            {
                Content           = "◂",
                Padding           = new Thickness(4, 2, 4, 2),
                BorderThickness   = new Thickness(0),
                Background        = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                Foreground        = this.PanelForeground,
                Visibility        = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            leftScrollBtn.Click += (s, e) =>
                scrollViewer.ChangeView(scrollViewer.HorizontalOffset - 80, null, null);

            var rightScrollBtn = new Button
            {
                Content           = "▸",
                Padding           = new Thickness(4, 2, 4, 2),
                BorderThickness   = new Thickness(0),
                Background        = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                Foreground        = this.PanelForeground,
                Visibility        = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            rightScrollBtn.Click += (s, e) =>
                scrollViewer.ChangeView(scrollViewer.HorizontalOffset + 80, null, null);

            void UpdateScrollButtons(double offset)
            {
                bool needsScroll = tabPanel.ActualWidth > scrollViewer.ActualWidth;
                leftScrollBtn.Visibility  = needsScroll && offset > 0
                    ? Visibility.Visible : Visibility.Collapsed;
                rightScrollBtn.Visibility = needsScroll && offset < scrollViewer.ScrollableWidth
                    ? Visibility.Visible : Visibility.Collapsed;
            }

            scrollViewer.ViewChanged        += (s, e) => UpdateScrollButtons(scrollViewer.HorizontalOffset);
            tabPanel.SizeChanged            += (s, e) => UpdateScrollButtons(scrollViewer.HorizontalOffset);
            scrollViewer.SizeChanged        += (s, e) => UpdateScrollButtons(scrollViewer.HorizontalOffset);

            var navGrid = new Grid();
            navGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            navGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            navGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(leftScrollBtn,  0);
            Grid.SetColumn(scrollViewer,   1);
            Grid.SetColumn(rightScrollBtn, 2);
            navGrid.Children.Add(leftScrollBtn);
            navGrid.Children.Add(scrollViewer);
            navGrid.Children.Add(rightScrollBtn);

            // Issue 7: slight negative top margin so the selected tab visually overlaps
            // the content border's bottom edge, creating a unified "tab is part of panel" look.
            return new Border
            {
                Background      = this.TabStripBackground,
                BorderBrush     = this.PanelBorderBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding         = new Thickness(4, 1, 4, 2),
                Margin          = new Thickness(0, -3, 0, 0),
                Child           = navGrid,
            };
        }

        // Returns the index of the tab whose x-range contains cursorX within the panel.
        private static int FindTabIndexAtX(StackPanel panel, double cursorX)
        {
            double accum = 0;
            for (int i = 0; i < panel.Children.Count; i++)
            {
                if (panel.Children[i] is FrameworkElement child)
                {
                    accum += child.ActualWidth;
                }

                if (cursorX < accum)
                {
                    return i;
                }
            }

            return Math.Max(0, panel.Children.Count - 1);
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
                    PART_Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    Grid.SetColumn(child, i * 2);
                    PART_Root.Children.Add(child);

                    if (i < m_childZones.Count - 1)
                    {
                        PART_Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(separationSize) });
                        var splitter = new DockZoneSplitter();
                        splitter.Setup(PART_Root, i, eDockOrientation.Horizontal);
                        Grid.SetColumn(splitter, i * 2 + 1);
                        PART_Root.Children.Add(splitter);
                    }
                }
                else // Vertical
                {
                    PART_Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    Grid.SetRow(child, i * 2);
                    PART_Root.Children.Add(child);

                    if (i < m_childZones.Count - 1)
                    {
                        PART_Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(separationSize) });
                        var splitter = new DockZoneSplitter();
                        splitter.Setup(PART_Root, i, eDockOrientation.Vertical);
                        Grid.SetRow(splitter, i * 2 + 1);
                        PART_Root.Children.Add(splitter);
                    }
                }
            }

            // After a layout pass, apply stored pass-along sizes (e.g. from serialization).
            this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => this.HandleNewChildZonesAdded(m_childZones));
        }

        private void HandleNewChildZonesAdded(IEnumerable<DockZone> added)
        {
            if (added == null || !added.Any() || PART_Root == null || this.ViewModel == null)
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
                    this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => this.SetTargetSize(pixelSizes));
                }
            }
            else if (this.ViewModel.Orientation == eDockOrientation.Vertical)
            {
                double full = sizes.Sum(s => s.Height);
                if (full > 0 && rootSize.Height > 0)
                {
                    var pixelSizes = sizes.Select(s => rootSize.Height * (s.Height / full)).ToList();
                    this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => this.SetTargetSize(pixelSizes));
                }
            }
        }

        internal void SetTargetSize(List<double> sizes)
        {
            if (PART_Root == null || this.ViewModel == null)
            {
                return;
            }

            bool isHorizontal = this.ViewModel.Orientation == eDockOrientation.Horizontal;
            int count = Math.Min(m_childZones.Count, sizes.Count);

            for (int i = 0; i < count; i++)
            {
                int defIndex = i * 2;
                if (isHorizontal && defIndex < PART_Root.ColumnDefinitions.Count)
                {
                    PART_Root.ColumnDefinitions[defIndex].Width = new GridLength(sizes[i], GridUnitType.Star);
                }
                else if (!isHorizontal && defIndex < PART_Root.RowDefinitions.Count)
                {
                    PART_Root.RowDefinitions[defIndex].Height = new GridLength(sizes[i], GridUnitType.Star);
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
            if (PART_Root != null && this.ViewModel != null
                && (this.ViewModel.Orientation & eDockOrientation.AnySplitOrientation) != 0)
            {
                PART_Root.Children.Clear();
                PART_Root.RowDefinitions.Clear();
                PART_Root.ColumnDefinitions.Clear();
                this.BuildSplitLayout();

                // Re-add overlay above all split content
                if (m_dropOverlay != null)
                {
                    PART_Root.Children.Add(m_dropOverlay);
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
            // 3×3 grid (84×60 px) centered over the zone — shown when IsDirectDropTarget=true
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
            // All other orientations support directional splits as well as tab-insertion.
            bool canSplit = this.ViewModel?.Orientation != eDockOrientation.Empty;
            var splitVis = canSplit ? Visibility.Visible : Visibility.Collapsed;
            m_overlayLeft.Visibility = splitVis;
            m_overlayTop.Visibility = splitVis;
            m_overlayRight.Visibility = splitVis;
            m_overlayBottom.Visibility = splitVis;
            m_overlayCenter.Visibility = Visibility.Visible;

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

                // Non-root panel in a tearoff — dragging moves the whole tearoff window.
                m_headerDragZone = rootZone;
            }
            else
            {
                // Root window — dragging tears this zone off.
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
                // so add 30 to the Y component — but only when we're tearing off from the
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
