namespace AJut.UX.Controls
{
    using AJut.Storage;
    using AJut.Tree;
    using AJut.UX.PropertyInteraction;
    using Microsoft.UI.Input;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Windows.Foundation;
    using DPUtils = AJut.UX.DPUtils<PropertyGrid>;

    // ===========[ PropertyGrid ]===============================================
    // Reflection-based property editor. Given a C# object (or view model),
    // discovers its public properties and renders each with a type-appropriate
    // editor via ItemTemplateSelector.
    //
    // Consumers register editor templates on PropertyGridTemplateSelector keyed by
    // PropertyEditTarget.Editor (the property type name or [PGEditor] override).
    //
    // Complex reference-type properties whose value is non-null are shown as
    // expandable tree nodes whose children are the sub-object's properties.
    // Uses FlatTreeListControl internally for tree expansion/indentation.
    //
    // The label splitter: with the pointer on a row's label within PropertyGridRowRules.kLabelSplitterHitDistance
    // of the boundary between the label and its editor, a line shows on it. Dragging it sets
    // LabelColumnWidth, and double-clicking it fits LabelColumnWidth to the labels on screen.
    //
    // Template parts:
    //   PART_TreeList       - the inner FlatTreeListControl that renders property rows
    //   PART_LabelSplitter  - the splitter's line and hit area, over the rows

    [TemplatePart(Name = nameof(PART_TreeList), Type = typeof(FlatTreeListControl))]
    [TemplatePart(Name = nameof(PART_LabelSplitter), Type = typeof(FrameworkElement))]
    public class PropertyGrid : Control, IPropertyGridHost, IDisposable
    {
        // ===========[ Instance fields ]==========================================
        // Every rule that never touches a view (rebuilding, mirroring source edits, teardown, selection
        // by source object, list reordering) lives in the shared controller; this control is the view
        private readonly PropertyGridController m_controller;
        private readonly PointerEventHandler m_anyPointerMovedHandler;
        private FlatTreeListControl PART_TreeList;
        private FrameworkElement PART_LabelSplitter;

        // The label splitter, in PART_TreeList's coordinates: where the boundary under the pointer is,
        // the label column width that boundary stands for, and the drag in progress
        private double m_labelSplitterBoundaryX;
        private double m_labelSplitterColumnWidth = double.NaN;
        private double m_labelSplitterDragStartX;
        private double m_labelSplitterDragStartBoundaryX;
        private double m_labelSplitterDragStartWidth;
        private bool m_isDraggingLabelSplitter;

        // ===========[ Construction ]=============================================
        public PropertyGrid ()
        {
            this.DefaultStyleKey = typeof(PropertyGrid);
            m_controller = new PropertyGridController(this);
            m_controller.PropertyTreeChanged += this.OnControllerPropertyTreeChanged;

            // Editors mark the pointer moves over them handled, and the splitter has to see every move
            // over the rows to know when the pointer nears a label boundary
            m_anyPointerMovedHandler = new PointerEventHandler(this.OnAnyPointerMoved);
            this.AddHandler(PointerMovedEvent, m_anyPointerMovedHandler, true);

            // Drop the source -> grid PropertyChanged subscription whenever the grid leaves the
            // visual tree, and restore it when it comes back. Without this a long-lived source
            // object pins the whole grid after a consumer simply drops it (without Dispose or
            // nulling the source).
            this.Loaded += this.OnLoaded;
            this.Unloaded += this.OnUnloaded;
        }

        public void Dispose ()
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.SelectionChanged -= this.OnTreeListSelectionChanged;
                this.PART_TreeList.DragDropReorderRequested -= this.OnDragDropReorderRequested;
            }

            this.UnhookLabelSplitter();
            this.RemoveHandler(PointerMovedEvent, m_anyPointerMovedHandler);
            m_controller.PropertyTreeChanged -= this.OnControllerPropertyTreeChanged;
            m_controller.Dispose();
            this.SingleItemSource = null;
            this.ItemsSource = null;
        }

        public PropertyGridManager Manager => m_controller.Manager;

        // ===========[ IPropertyGridHost ]=======================================
        void IPropertyGridHost.ShowRootItems (IReadOnlyList<IObservableTreeNode> rootItems)
        {
            this.PropertyTreeItems = rootItems;

            // PropertyTreeItems is not TemplateBind'd to the inner FlatTreeListControl (WinUI3
            // TemplateBinding does not backfill a value set before the template applies), so it is
            // pushed by hand here, and again in OnApplyTemplate for the opposite ordering
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.RootItemsSource = rootItems;
            }
        }

        // ===========[ IPropertyGrid ]===========================================
        IEnumerable IPropertyGrid.ItemsSource => this.ItemsSource;
        object IPropertyGrid.SingleItemSource => this.SingleItemSource;

        // ===========[ Dependency Properties ]====================================
        public static readonly DependencyProperty ItemsSourceProperty = DPUtils.Register(_ => _.ItemsSource, (d, e) => d.OnItemsSourceChanged(e.OldValue, e.NewValue));
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)this.GetValue(ItemsSourceProperty);
            set => this.SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty SingleItemSourceProperty = DPUtils.Register(_ => _.SingleItemSource, (d, e) => d.OnSingleItemSourceChanged(e.OldValue, e.NewValue));
        public object SingleItemSource
        {
            get => this.GetValue(SingleItemSourceProperty);
            set => this.SetValue(SingleItemSourceProperty, value);
        }

        public static readonly DependencyProperty ItemTemplateSelectorProperty = DPUtils.Register(_ => _.ItemTemplateSelector);
        public PropertyGridTemplateSelector ItemTemplateSelector
        {
            get => (PropertyGridTemplateSelector)this.GetValue(ItemTemplateSelectorProperty);
            set => this.SetValue(ItemTemplateSelectorProperty, value);
        }

        public static readonly DependencyProperty RowTemplateProperty = DPUtils.Register(_ => _.RowTemplate, (d, e) => d.ApplyRowTemplate());
        public DataTemplate RowTemplate
        {
            get => (DataTemplate)this.GetValue(RowTemplateProperty);
            set => this.SetValue(RowTemplateProperty, value);
        }

        /// <summary>
        /// Makes the whole grid read-only: every row's editor is disabled (button rows and a list's add button with it),
        /// list elements can't be removed or dragged to reorder, and nothing offers "Set to default". Rows still expand
        /// and collapse. WinUI3 has no inherited properties, so the grid pushes this to its rows itself.
        /// </summary>
        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly, false, (d, e) => d.OnIsReadOnlyChanged(e.NewValue));
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }
        private void OnIsReadOnlyChanged (bool newValue)
        {
            m_controller.IsReadOnly = newValue;
            this.PushToRealizedRows();
        }

        public static readonly DependencyProperty MaxRecursionDepthProperty = DPUtils.Register(_ => _.MaxRecursionDepth, 5, (d, e) => d.m_controller.Manager.MaxRecursionDepth = (int)e.NewValue);
        public int MaxRecursionDepth
        {
            get => (int)this.GetValue(MaxRecursionDepthProperty);
            set => this.SetValue(MaxRecursionDepthProperty, value);
        }

        public static readonly DependencyProperty TreeDepthIndentSizeProperty = DPUtils.Register(_ => _.TreeDepthIndentSize, 16.0, (d, e) => d.OnTreeDepthIndentSizeChanged(e.NewValue));
        public double TreeDepthIndentSize
        {
            get => (double)this.GetValue(TreeDepthIndentSizeProperty);
            set => this.SetValue(TreeDepthIndentSizeProperty, value);
        }
        private void OnTreeDepthIndentSizeChanged (double newValue)
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.TreeDepthIndentSize = newValue;
            }
        }

        public static readonly DependencyProperty RowSpacingProperty = DPUtils.Register(_ => _.RowSpacing, 2.0, (d, e) => d.OnRowSpacingChanged(e.NewValue));
        public double RowSpacing
        {
            get => (double)this.GetValue(RowSpacingProperty);
            set => this.SetValue(RowSpacingProperty, value);
        }
        private void OnRowSpacingChanged (double newValue)
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.RowSpacing = newValue;
            }
        }

        public static readonly DependencyProperty FixedRowHeightProperty = DPUtils.Register(_ => _.FixedRowHeight, double.NaN, (d, e) => d.OnFixedRowHeightChanged(e.NewValue));
        public double FixedRowHeight
        {
            get => (double)this.GetValue(FixedRowHeightProperty);
            set => this.SetValue(FixedRowHeightProperty, value);
        }
        private void OnFixedRowHeightChanged (double newValue)
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.FixedRowHeight = newValue;
            }
        }

        /// <summary>
        /// Width of the label column in pixels, so every row's editor starts at the same x whatever its depth; NaN (the
        /// default) sizes each label to its text. The label splitter sets it.
        /// </summary>
        public static readonly DependencyProperty LabelColumnWidthProperty = DPUtils.Register(_ => _.LabelColumnWidth, double.NaN, (d, e) => d.PushToRealizedRows());
        public double LabelColumnWidth
        {
            get => (double)this.GetValue(LabelColumnWidthProperty);
            set => this.SetValue(LabelColumnWidthProperty, value);
        }

        // Decides where per-property tooltips render (property name, value, both, or none).
        // PropertyGridItemRow reads this on load and applies the tooltip text in code.
        public static readonly DependencyProperty PropertyToolTipPlacementProperty = DPUtils.Register(_ => _.PropertyToolTipPlacement, ePropertyToolTipPlacement.PropertyNameOnly);
        public ePropertyToolTipPlacement PropertyToolTipPlacement
        {
            get => (ePropertyToolTipPlacement)this.GetValue(PropertyToolTipPlacementProperty);
            set => this.SetValue(PropertyToolTipPlacementProperty, value);
        }

        /// <summary>
        /// DataTemplate for the property label when the value equals the default.
        /// DataContext is the PropertyEditTarget. Defaults to a plain TextBlock showing DisplayName.
        /// </summary>
        public static readonly DependencyProperty DefaultValueLabelDataTemplateProperty = DPUtils.Register(_ => _.DefaultValueLabelDataTemplate);
        public DataTemplate DefaultValueLabelDataTemplate
        {
            get => (DataTemplate)this.GetValue(DefaultValueLabelDataTemplateProperty);
            set => this.SetValue(DefaultValueLabelDataTemplateProperty, value);
        }

        /// <summary>
        /// DataTemplate for the property label when the value differs from the default.
        /// DataContext is the PropertyEditTarget. Defaults to a bold TextBlock showing DisplayName.
        /// </summary>
        public static readonly DependencyProperty ModifiedValueLabelDataTemplateProperty = DPUtils.Register(_ => _.ModifiedValueLabelDataTemplate);
        public DataTemplate ModifiedValueLabelDataTemplate
        {
            get => (DataTemplate)this.GetValue(ModifiedValueLabelDataTemplateProperty);
            set => this.SetValue(ModifiedValueLabelDataTemplateProperty, value);
        }

        /// <summary>
        /// Style applied to the subtitle TextBlock in each property row. TargetType must be TextBlock.
        /// Defaults to smaller, italic, 0.85 opacity text.
        /// </summary>
        public static readonly DependencyProperty LabelSubtitleStyleProperty = DPUtils.Register(_ => _.LabelSubtitleStyle);
        public Style LabelSubtitleStyle
        {
            get => (Style)this.GetValue(LabelSubtitleStyleProperty);
            set => this.SetValue(LabelSubtitleStyleProperty, value);
        }

        public static readonly DependencyProperty InsertionLineXOffsetProperty = DPUtils.Register(_ => _.InsertionLineXOffset, 0.0);
        public double InsertionLineXOffset
        {
            get => (double)this.GetValue(InsertionLineXOffsetProperty);
            set => this.SetValue(InsertionLineXOffsetProperty, value);
        }

        // Padding applied to each PropertyGridItemRow (insets label + editor from the row edges).
        // Read by PropertyGridItemRow.OnLoaded and applied as Padding so the template's
        // {TemplateBinding Padding} drives the inner content grid's Margin.
        public static readonly DependencyProperty ElementPaddingProperty = DPUtils.Register(_ => _.ElementPadding, new Thickness(3, 2, 3, 2));
        public Thickness ElementPadding
        {
            get => (Thickness)this.GetValue(ElementPaddingProperty);
            set => this.SetValue(ElementPaddingProperty, value);
        }

        public static readonly DependencyProperty DragGhostTemplateProperty = DPUtils.Register(_ => _.DragGhostTemplate, (d, e) => d.OnDragGhostTemplateChanged(e.NewValue));
        public DataTemplate DragGhostTemplate
        {
            get => (DataTemplate)this.GetValue(DragGhostTemplateProperty);
            set => this.SetValue(DragGhostTemplateProperty, value);
        }

        // Container style for each ListView row inside the inner FlatTreeListControl.
        // Defaults (via Style Setter) to PropertyGrid_ListViewItemStyle - a minimal no-chrome
        // template that suppresses full-row selection highlight so PropertyGridItemRow's own
        // label-only selection indicator is the only visual. Override to customise container
        // padding or add hover/press visuals without touching the PropertyGrid template.
        public static readonly DependencyProperty ListViewItemContainerStyleProperty = DPUtils.Register(_ => _.ListViewItemContainerStyle, (d, e) => d.OnListViewItemContainerStyleChanged(e.NewValue));
        public Style ListViewItemContainerStyle
        {
            get => (Style)this.GetValue(ListViewItemContainerStyleProperty);
            set => this.SetValue(ListViewItemContainerStyleProperty, value);
        }
        private void OnListViewItemContainerStyleChanged (Style newValue)
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.ListViewItemContainerStyle = newValue;
            }
        }
        private void OnDragGhostTemplateChanged (DataTemplate newValue)
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.DragGhostTemplate = newValue;
            }
        }

        /// <summary>
        /// Format string for the element count display in list editors.
        /// {0} is replaced with the element count. Default: "Elements ({0})".
        /// </summary>
        public static readonly DependencyProperty ListElementCountFormatProperty = DPUtils.Register(_ => _.ListElementCountFormat, "Elements ({0})");
        public string ListElementCountFormat
        {
            get => (string)this.GetValue(ListElementCountFormatProperty);
            set => this.SetValue(ListElementCountFormatProperty, value);
        }

        /// <summary>
        /// The top-level PropertyEditTarget items (children of the hidden $root node).
        /// Updated whenever RebuildEditTargets runs. Pushed to PART_TreeList.RootItemsSource
        /// in code rather than via TemplateBinding (see PropertyGrid.xaml for why).
        /// </summary>
        public static readonly DependencyProperty PropertyTreeItemsProperty = DPUtils.Register(_ => _.PropertyTreeItems);
        public IReadOnlyList<IObservableTreeNode> PropertyTreeItems
        {
            get => (IReadOnlyList<IObservableTreeNode>)this.GetValue(PropertyTreeItemsProperty);
            private set => this.SetValue(PropertyTreeItemsProperty, value);
        }

        // ===========[ Events ]===================================================
        /// <summary>
        /// Fires whenever any property in the displayed tree is edited via the property grid.
        /// Subscribe to detect any change and refresh displays that mirror the source object.
        /// </summary>
        public event EventHandler PropertyTreeChanged;

        /// <summary>
        /// Fires whenever the selected row changes - by user click or programmatic selection. Read
        /// <see cref="SelectedTarget"/> / <see cref="SelectedSourceObject"/> in the handler.
        /// </summary>
        public event EventHandler SelectedTargetChanged;

        /// <summary>
        /// Fires when the source object behind the selected row changes. Moving the selection between two
        /// rows inside the same list element's subtree resolves to the same element, so this does NOT fire
        /// in that case - only when the resolved <see cref="SelectedSourceObject"/> actually changes.
        /// </summary>
        public event EventHandler SelectedSourceObjectChanged;

        // ===========[ Selection ]================================================
        /// <summary>
        /// The PropertyEditTarget behind the currently selected row, or null when nothing is selected.
        /// Setting it selects that row (expanding any collapsed ancestors) and scrolls it into view; set
        /// to null to clear the selection. Returns null until the template has been applied.
        /// </summary>
        public PropertyEditTarget SelectedTarget
        {
            get => this.PART_TreeList?.SelectedItem?.Source as PropertyEditTarget;
            set
            {
                if (this.PART_TreeList == null)
                {
                    return;
                }

                if (value == null)
                {
                    this.PART_TreeList.SetSelection(null);
                    return;
                }

                this.PART_TreeList.TrySelectItemForSource(value, scrollIntoView: true);
            }
        }

        /// <summary>
        /// The source object behind the selected row, resolved by walking up from the selected row to the
        /// nearest list-element row and returning its element instance. Null when nothing is selected or
        /// the selection sits outside any list element. Selecting any row inside an element's subtree
        /// (the element row itself or any of its sub-property rows) resolves to that same element.
        /// </summary>
        public object SelectedSourceObject => PropertyGridController.ResolveSourceObject(this.SelectedTarget);

        /// <summary>
        /// Selects the row representing <paramref name="sourceObject"/> (matched by reference), expanding
        /// any collapsed ancestors and scrolling it into view. Returns false when no row represents the
        /// instance. Only list-element rows carry a source object, so this targets list elements.
        /// </summary>
        public bool TrySelectSourceObject (object sourceObject)
        {
            if (sourceObject == null || this.PART_TreeList == null)
            {
                return false;
            }

            PropertyEditTarget match = m_controller.FindTargetForSourceObject(sourceObject);
            if (match == null)
            {
                return false;
            }

            return this.PART_TreeList.TrySelectItemForSource(match, scrollIntoView: true);
        }

        // ===========[ Template application ]====================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.SelectionChanged -= this.OnTreeListSelectionChanged;
                this.PART_TreeList.DragDropReorderRequested -= this.OnDragDropReorderRequested;
            }

            this.PART_TreeList = this.GetTemplateChild(nameof(PART_TreeList)) as FlatTreeListControl;
            if (this.PART_TreeList != null)
            {
                // WinUI3 TemplateBinding does not read the current source DP value at
                // binding-establishment time - it only reacts to post-establishment changes.
                // RebuildEditTargets() commonly runs before the template is applied (e.g. when
                // the PropertyGrid lives in a tab that isn't yet selected), so push manually.
                this.PART_TreeList.RootItemsSource = this.PropertyTreeItems;
                this.PART_TreeList.TreeDepthIndentSize = this.TreeDepthIndentSize;
                this.PART_TreeList.RowSpacing = this.RowSpacing;
                this.PART_TreeList.FixedRowHeight = this.FixedRowHeight;
                this.PART_TreeList.ListViewItemContainerStyle = this.ListViewItemContainerStyle;

                // Enable drag/drop reordering for list elements
                this.PART_TreeList.IsDragDropReorderEnabled = true;
                this.PART_TreeList.CanDragItem = m_controller.CanDragRow;
                this.PART_TreeList.CanDropItem = PropertyGridController.CanDropItem;
                this.PART_TreeList.DragDropReorderRequested += this.OnDragDropReorderRequested;

                this.PART_TreeList.SelectionChanged -= this.OnTreeListSelectionChanged;
                this.PART_TreeList.SelectionChanged += this.OnTreeListSelectionChanged;

                // Suppress list add/remove/reorder animations in the PropertyGrid
                this.PART_TreeList.SuppressItemTransitions = true;

                // Forward drag ghost template (push manually - WinUI3 TemplateBinding doesn't backfill)
                this.PART_TreeList.DragGhostTemplate = this.DragGhostTemplate;
            }

            this.UnhookLabelSplitter();
            this.PART_LabelSplitter = this.GetTemplateChild(nameof(PART_LabelSplitter)) as FrameworkElement;
            if (this.PART_LabelSplitter != null)
            {
                this.PART_LabelSplitter.PointerPressed += this.OnLabelSplitterPointerPressed;
                this.PART_LabelSplitter.PointerMoved += this.OnLabelSplitterPointerMoved;
                this.PART_LabelSplitter.PointerReleased += this.OnLabelSplitterPointerReleased;
                this.PART_LabelSplitter.PointerCaptureLost += this.OnLabelSplitterPointerCaptureLost;
                this.PART_LabelSplitter.DoubleTapped += this.OnLabelSplitterDoubleTapped;
            }

            this.ApplyRowTemplate();
        }

        // ===========[ Lifecycle - source subscription ]==========================
        // The grid mirrors external edits by subscribing to its source object(s) PropertyChanged.
        // That subscription is a source -> grid strong reference, so a long-lived source pins the
        // grid once it leaves the tree. The controller drops it on Unloaded so a dropped grid
        // collects, and restores and refreshes it on Loaded so a re-shown grid stays live.
        private void OnLoaded (object sender, RoutedEventArgs e)
        {
            m_controller.OnAttached();
        }

        private void OnUnloaded (object sender, RoutedEventArgs e)
        {
            m_controller.OnDetached();
        }

        // ===========[ Property change handlers ]=================================
        private void OnSingleItemSourceChanged (object oldValue, object newValue)
        {
            // A single source replaces an items source
            if (newValue != null)
            {
                this.ItemsSource = null;
            }

            m_controller.OnSingleItemSourceChanged(newValue);
        }

        private void OnItemsSourceChanged (IEnumerable oldValue, IEnumerable newValue)
        {
            m_controller.OnItemsSourceChanged(newValue);
        }

        private void OnControllerPropertyTreeChanged (object sender, EventArgs e)
        {
            this.PropertyTreeChanged?.Invoke(this, EventArgs.Empty);
        }

        // ===========[ Selection handling ]=======================================
        private void OnTreeListSelectionChanged (object sender, SelectionChange<FlatTreeItem> e)
        {
            this.SelectedTargetChanged?.Invoke(this, EventArgs.Empty);
            if (m_controller.OnSelectedTargetChanged(this.SelectedTarget))
            {
                this.SelectedSourceObjectChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ApplyRowTemplate ()
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.ItemTemplate = this.RowTemplate;
            }
        }

        // Rows read the grid's settings when they load (PropertyGridItemRow.OnLoaded); a setting that
        // changes after that is pushed to every row already realized here
        private void PushToRealizedRows ()
        {
            foreach (PropertyGridItemRow row in this.FindRealizedRows())
            {
                row.IsGridReadOnly = this.IsReadOnly;
                row.LabelColumnWidth = this.LabelColumnWidth;
            }
        }

        private IEnumerable<PropertyGridItemRow> FindRealizedRows ()
        {
            if (this.PART_TreeList == null)
            {
                return Enumerable.Empty<PropertyGridItemRow>();
            }

            return TreeTraversal<DependencyObject>.All(this.PART_TreeList, getChildrenMethodOverride: DependencyObjectXT.GetVisualChildren).OfType<PropertyGridItemRow>();
        }

        // ===========[ Label splitter ]===========================================
        protected override void OnPointerExited (PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);
            if (!m_isDraggingLabelSplitter)
            {
                this.HideLabelSplitter();
            }
        }

        // Every move over the grid, handled or not: shows the splitter over the boundary of the row under
        // the pointer when the pointer is near it, and hides it otherwise
        private void OnAnyPointerMoved (object sender, PointerRoutedEventArgs e)
        {
            if (m_isDraggingLabelSplitter || this.PART_TreeList == null || this.PART_LabelSplitter == null)
            {
                return;
            }

            double pointerX = e.GetCurrentPoint(this.PART_TreeList).Position.X;
            PropertyGridItemRow row = VisualTreeHelper.FindElementsInHostCoordinates(e.GetCurrentPoint(null).Position, this.PART_TreeList).OfType<PropertyGridItemRow>().FirstOrDefault();
            if (row != null
                && row.TryGetLabelBoundary(this.PART_TreeList, out double boundaryX, out double shownWidth)
                && PropertyGridRowRules.IsNearLabelBoundary(pointerX, boundaryX))
            {
                // A set width is the same boundary on every row, so a drag starts from it rather than from
                // a row whose label is held wider by its minimum
                m_labelSplitterColumnWidth = double.IsNaN(this.LabelColumnWidth) ? shownWidth : this.LabelColumnWidth;
                this.ShowLabelSplitter(boundaryX);
            }
            else
            {
                this.HideLabelSplitter();
            }
        }

        private void OnLabelSplitterPointerPressed (object sender, PointerRoutedEventArgs e)
        {
            if (double.IsNaN(m_labelSplitterColumnWidth))
            {
                return;
            }

            m_labelSplitterDragStartX = e.GetCurrentPoint(this.PART_TreeList).Position.X;
            m_labelSplitterDragStartBoundaryX = m_labelSplitterBoundaryX;
            m_labelSplitterDragStartWidth = m_labelSplitterColumnWidth;
            m_isDraggingLabelSplitter = this.PART_LabelSplitter.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void OnLabelSplitterPointerMoved (object sender, PointerRoutedEventArgs e)
        {
            if (!m_isDraggingLabelSplitter)
            {
                return;
            }

            // Every move is measured from where the drag started. The line follows the width actually
            // set, which stops at the limits even when the pointer doesn't.
            double pointerDelta = e.GetCurrentPoint(this.PART_TreeList).Position.X - m_labelSplitterDragStartX;
            double width = PropertyGridRowRules.ComputeDraggedLabelColumnWidth(m_labelSplitterDragStartWidth, pointerDelta, this.PART_TreeList.ActualWidth);
            this.LabelColumnWidth = width;
            this.PlaceLabelSplitter(m_labelSplitterDragStartBoundaryX + (width - m_labelSplitterDragStartWidth));
            e.Handled = true;
        }

        private void OnLabelSplitterPointerReleased (object sender, PointerRoutedEventArgs e)
        {
            if (m_isDraggingLabelSplitter)
            {
                this.PART_LabelSplitter.ReleasePointerCapture(e.Pointer);
                this.EndLabelSplitterDrag();
                e.Handled = true;
            }
        }

        private void OnLabelSplitterPointerCaptureLost (object sender, PointerRoutedEventArgs e)
        {
            this.EndLabelSplitterDrag();
        }

        private void OnLabelSplitterDoubleTapped (object sender, DoubleTappedRoutedEventArgs e)
        {
            this.FitLabelColumnWidthToShownLabels();
            this.HideLabelSplitter();
            e.Handled = true;
        }

        // A drag that moved has already set its width, and the line already sits on the new boundary; a
        // press with no move sets nothing, so a click on the splitter doesn't fix an auto sized column.
        // The next pointer move reads the boundary and width afresh.
        private void EndLabelSplitterDrag ()
        {
            m_isDraggingLabelSplitter = false;
        }

        private void FitLabelColumnWidthToShownLabels ()
        {
            var widths = new List<double>();
            foreach (PropertyGridItemRow row in this.FindRealizedRows())
            {
                if (this.IsRowShown(row) && row.TryMeasureFittingLabelColumnWidth(out double width))
                {
                    widths.Add(width);
                }
            }

            double fit = PropertyGridRowRules.FitLabelColumnWidth(widths, this.PART_TreeList.ActualWidth);
            if (!double.IsNaN(fit))
            {
                this.LabelColumnWidth = fit;
            }
        }

        // Realized rows include a few just off screen, kept ready for scrolling; fitting only counts the ones in view
        private bool IsRowShown (PropertyGridItemRow row)
        {
            Rect bounds = row.TransformToVisual(this.PART_TreeList).TransformBounds(new Rect(0, 0, row.ActualWidth, row.ActualHeight));
            return bounds.Bottom > 0 && bounds.Top < this.PART_TreeList.ActualHeight;
        }

        private void ShowLabelSplitter (double boundaryX)
        {
            this.PlaceLabelSplitter(boundaryX);
            this.PART_LabelSplitter.Visibility = Visibility.Visible;
            this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        }

        private void PlaceLabelSplitter (double boundaryX)
        {
            m_labelSplitterBoundaryX = boundaryX;
            this.PART_LabelSplitter.Margin = new Thickness(boundaryX - PropertyGridRowRules.kLabelSplitterHitDistance, 0, 0, 0);
        }

        private void HideLabelSplitter ()
        {
            if (this.PART_LabelSplitter != null && this.PART_LabelSplitter.Visibility == Visibility.Visible)
            {
                this.PART_LabelSplitter.Visibility = Visibility.Collapsed;
                this.ProtectedCursor = null;
            }
        }

        private void UnhookLabelSplitter ()
        {
            if (this.PART_LabelSplitter != null)
            {
                this.PART_LabelSplitter.PointerPressed -= this.OnLabelSplitterPointerPressed;
                this.PART_LabelSplitter.PointerMoved -= this.OnLabelSplitterPointerMoved;
                this.PART_LabelSplitter.PointerReleased -= this.OnLabelSplitterPointerReleased;
                this.PART_LabelSplitter.PointerCaptureLost -= this.OnLabelSplitterPointerCaptureLost;
                this.PART_LabelSplitter.DoubleTapped -= this.OnLabelSplitterDoubleTapped;
            }
        }

        // ===========[ Drag/Drop for list element reordering ]====================
        private void OnDragDropReorderRequested (object sender, FlatTreeReorderEventArgs e)
        {
            PropertyGridController.TryHandleReorder(e);
        }
    }
}
