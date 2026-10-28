namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut;
    using AJut.Tree;
    using AJut.UX.AttachedProperties;
    using AJut.UX.Converters;
    using AJut.UX.PropertyInteraction;
    using AJut.Storage;
    using DPUtils = DPUtils<PropertyGrid>;

    // The label splitter: with the pointer on a row's label within PropertyGridRowRules.kLabelSplitterHitDistance
    // of the boundary between the label and its editor, a line shows on it (PART_LabelSplitter).
    // Dragging it sets LabelColumnWidth, and double-clicking it fits LabelColumnWidth to the labels
    // on screen.
    public class PropertyGrid : Control, IDisposable, IPropertyGridHost
    {
        private const string kPropertyRowPartName = "PART_PropertyRow";
        private const string kPropertyRowLabelPartName = "PART_PropertyRowLabel";

        // Every rule that never touches a view (rebuilding, mirroring source edits, teardown, selection
        // by source object, list reordering) lives in the shared controller; this control is the view
        private readonly PropertyGridController m_controller;
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

        // ===========[ Commands ]=================================================
        /// <summary>
        /// Resets the target PropertyEditTarget (passed as CommandParameter) to its default value.
        /// Sourced from the label ContextMenu in the control template.
        /// </summary>
        public static readonly RoutedUICommand SetPropertyToDefaultCommand = new RoutedUICommand(
            "Set to Default",
            nameof(SetPropertyToDefaultCommand),
            typeof(PropertyGrid)
        );

        static PropertyGrid ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGrid), new FrameworkPropertyMetadata(typeof(PropertyGrid)));
        }

        public PropertyGrid ()
        {
            m_controller = new PropertyGridController(this);
            m_controller.PropertyTreeChanged += this.OnControllerPropertyTreeChanged;
            this.CommandBindings.Add(new CommandBinding(
                SetPropertyToDefaultCommand,
                this.OnSetPropertyToDefaultExecuted,
                this.OnSetPropertyToDefaultCanExecute
            ));

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
            m_controller.PropertyTreeChanged -= this.OnControllerPropertyTreeChanged;
            m_controller.Dispose();
            this.ItemsSource = null;
            this.SingleItemSource = null;
        }

        // ===========[ IPropertyGridHost ]=======================================
        void IPropertyGridHost.ShowRootItems (IReadOnlyList<IObservableTreeNode> rootItems)
        {
            this.PropertyTreeItems = rootItems;
        }

        // ===========[ Template application ]====================================
        public override void OnApplyTemplate ()
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
                this.PART_TreeList.IsDragDropReorderEnabled = true;
                this.PART_TreeList.CanDragItem = m_controller.CanDragRow;
                this.PART_TreeList.CanDropItem = PropertyGridController.CanDropItem;
                this.PART_TreeList.DragDropReorderRequested += this.OnDragDropReorderRequested;

                this.PART_TreeList.SelectionChanged += this.OnTreeListSelectionChanged;

                this.PART_TreeList.DragGhostTemplate = this.DragGhostTemplate;
            }

            this.UnhookLabelSplitter();
            this.PART_LabelSplitter = this.GetTemplateChild(nameof(PART_LabelSplitter)) as FrameworkElement;
            if (this.PART_LabelSplitter != null)
            {
                this.PART_LabelSplitter.MouseLeftButtonDown += this.OnLabelSplitterMouseLeftButtonDown;
                this.PART_LabelSplitter.MouseMove += this.OnLabelSplitterMouseMove;
                this.PART_LabelSplitter.MouseLeftButtonUp += this.OnLabelSplitterMouseLeftButtonUp;
                this.PART_LabelSplitter.LostMouseCapture += this.OnLabelSplitterLostMouseCapture;
            }
        }

        // ===========[ Dependency Properties ]====================================
        public static readonly DependencyProperty ItemsSourceProperty = DPUtils.Register(_ => _.ItemsSource, (d, e) => d.OnItemsSourceChanged(e));
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)this.GetValue(ItemsSourceProperty);
            set => this.SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty SingleItemSourceProperty = DPUtils.Register(_ => _.SingleItemSource, (d, e) => d.OnSingleItemSourceChanged(e));
        public object SingleItemSource
        {
            get => (object)this.GetValue(SingleItemSourceProperty);
            set => this.SetValue(SingleItemSourceProperty, value);
        }

        /// <summary>
        /// The top-level PropertyEditTarget items (children of the hidden $root node).
        /// Bind FlatTreeListControl.RootItemsSource to this so it creates an always-expanded
        /// uber root, making all top-level properties immediately visible while keeping
        /// sub-object children collapsed until the user expands them.
        /// Updated whenever RebuildEditTargets runs.
        /// </summary>
        private static readonly DependencyPropertyKey PropertyTreeItemsPropertyKey = DPUtils.RegisterReadOnly(_ => _.PropertyTreeItems);
        public static readonly DependencyProperty PropertyTreeItemsProperty = PropertyTreeItemsPropertyKey.DependencyProperty;
        public IReadOnlyList<IObservableTreeNode> PropertyTreeItems
        {
            get => (IReadOnlyList<IObservableTreeNode>)this.GetValue(PropertyTreeItemsProperty);
            private set => this.SetValue(PropertyTreeItemsPropertyKey, value);
        }

        public static readonly DependencyProperty ItemTemplateSelectorProperty = DPUtils.Register(_ => _.ItemTemplateSelector);
        public PropertyGridTemplateSelector ItemTemplateSelector
        {
            get => (PropertyGridTemplateSelector)this.GetValue(ItemTemplateSelectorProperty);
            set => this.SetValue(ItemTemplateSelectorProperty, value);
        }

        public static readonly DependencyProperty TextLabelStyleProperty = DPUtils.Register(_ => _.TextLabelStyle);
        public Style TextLabelStyle
        {
            get => (Style)this.GetValue(TextLabelStyleProperty);
            set => this.SetValue(TextLabelStyleProperty, value);
        }

        public static readonly DependencyProperty MaxRecursionDepthProperty = DPUtils.Register(_ => _.MaxRecursionDepth, 5, (d, e) => d.m_controller.Manager.MaxRecursionDepth = e.NewValue);
        public int MaxRecursionDepth
        {
            get => (int)this.GetValue(MaxRecursionDepthProperty);
            set => this.SetValue(MaxRecursionDepthProperty, value);
        }

        public static readonly DependencyProperty TreeDepthIndentSizeProperty = DPUtils.Register(_ => _.TreeDepthIndentSize, 8.0);
        public double TreeDepthIndentSize
        {
            get => (double)this.GetValue(TreeDepthIndentSizeProperty);
            set => this.SetValue(TreeDepthIndentSizeProperty, value);
        }

        public static readonly DependencyProperty RowSpacingProperty = DPUtils.Register(_ => _.RowSpacing, 2.0);
        public double RowSpacing
        {
            get => (double)this.GetValue(RowSpacingProperty);
            set => this.SetValue(RowSpacingProperty, value);
        }

        public static readonly DependencyProperty FixedRowHeightProperty = DPUtils.Register(_ => _.FixedRowHeight, double.NaN);
        public double FixedRowHeight
        {
            get => (double)this.GetValue(FixedRowHeightProperty);
            set => this.SetValue(FixedRowHeightProperty, value);
        }

        /// <summary>
        /// Makes the whole grid read-only, the same as setting <see cref="Edit.IsReadOnlyProperty"/> on it, which it
        /// does: AJut's editors honor that inherited property, list elements can't be removed or dragged to reorder,
        /// and nothing offers "Set to default". False clears the grid's own setting, so a read-only inherited from
        /// further up still applies. The grid's own setting is its Edit.IsReadOnly, so false also clears an
        /// Edit.IsReadOnly set directly on the grid: use one or the other on the grid itself, not both.
        /// </summary>
        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly, false, (d, e) => d.OnIsReadOnlyChanged(e.NewValue));
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }
        private void OnIsReadOnlyChanged (bool newValue)
        {
            if (newValue)
            {
                Edit.SetIsReadOnly(this, true);
            }
            else
            {
                this.ClearValue(Edit.IsReadOnlyProperty);
            }
        }

        /// <summary>
        /// Width of the label column in pixels, so every row's editor starts at the same x whatever its depth; NaN (the
        /// default) sizes each label to its text. The label splitter sets it.
        /// </summary>
        public static readonly DependencyProperty LabelColumnWidthProperty = DPUtils.Register(_ => _.LabelColumnWidth, double.NaN);
        public double LabelColumnWidth
        {
            get => (double)this.GetValue(LabelColumnWidthProperty);
            set => this.SetValue(LabelColumnWidthProperty, value);
        }

        /// <summary>The label splitter's line, shown over the label and editor boundary while the pointer is near it</summary>
        public static readonly DependencyProperty LabelSplitterBrushProperty = DPUtils.Register(_ => _.LabelSplitterBrush);
        public Brush LabelSplitterBrush
        {
            get => (Brush)this.GetValue(LabelSplitterBrushProperty);
            set => this.SetValue(LabelSplitterBrushProperty, value);
        }

        /// <summary>Decides where per-property tooltips render (property name, value, both, or none).</summary>
        public static readonly DependencyProperty PropertyToolTipPlacementProperty = DPUtils.Register(_ => _.PropertyToolTipPlacement, ePropertyToolTipPlacement.PropertyNameOnly);
        public ePropertyToolTipPlacement PropertyToolTipPlacement
        {
            get => (ePropertyToolTipPlacement)this.GetValue(PropertyToolTipPlacementProperty);
            set => this.SetValue(PropertyToolTipPlacementProperty, value);
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

        public static readonly DependencyProperty DragGhostTemplateProperty = DPUtils.Register(_ => _.DragGhostTemplate, (d, e) => d.OnDragGhostTemplateChanged(e));
        public DataTemplate DragGhostTemplate
        {
            get => (DataTemplate)this.GetValue(DragGhostTemplateProperty);
            set => this.SetValue(DragGhostTemplateProperty, value);
        }
        private void OnDragGhostTemplateChanged (DependencyPropertyChangedEventArgs<DataTemplate> e)
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.DragGhostTemplate = e.NewValue;
            }
        }

        // ===========[ Lifecycle - source subscription ]==========================
        // The grid mirrors external edits by subscribing to its source object(s) PropertyChanged.
        // That subscription is a source -> grid strong reference, so a long-lived source pins the
        // grid once it leaves the tree. The controller drops it (and the ItemsSource collection's
        // CollectionChanged) on Unloaded so a dropped grid collects, and restores and refreshes it
        // on Loaded so a re-shown grid stays live.
        private void OnLoaded (object sender, RoutedEventArgs e)
        {
            m_controller.OnAttached();
        }

        private void OnUnloaded (object sender, RoutedEventArgs e)
        {
            m_controller.OnDetached();
        }

        private void OnSingleItemSourceChanged (DependencyPropertyChangedEventArgs<object> e)
        {
            // A single source replaces an items source
            if (e.HasNewValue)
            {
                this.ItemsSource = null;
            }

            m_controller.OnSingleItemSourceChanged(e.NewValue);
        }

        private void OnItemsSourceChanged (DependencyPropertyChangedEventArgs<IEnumerable> e)
        {
            // An items source replaces a single source
            if (e.HasNewValue)
            {
                this.SingleItemSource = null;
            }

            m_controller.OnItemsSourceChanged(e.NewValue);
        }

        private void OnControllerPropertyTreeChanged (object sender, EventArgs e)
        {
            this.PropertyTreeChanged?.Invoke(this, EventArgs.Empty);
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

        // ===========[ Sources ]==================================================
        /// <summary>
        /// Shows <paramref name="source"/>, the same as setting <see cref="SingleItemSource"/>. On the stacks an app can
        /// trim, this generic form keeps the members of <typeparamref name="T"/> the grid reads, where SingleItemSource,
        /// taking an object, can't. WPF doesn't support trimming, so here it is the same call under the same name, for
        /// code that reads the same on every stack.
        /// </summary>
        public void SetSingleItemSource<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> (T source)
        {
            this.SingleItemSource = source;
        }

        /// <summary>
        /// Shows <paramref name="items"/>, the same as setting <see cref="ItemsSource"/>; the generic counterpart of
        /// <see cref="SetSingleItemSource{T}"/>, for the same reason.
        /// </summary>
        public void SetItemsSource<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> (IEnumerable<T> items)
        {
            this.ItemsSource = items;
        }

        // ===========[ Selection ]================================================
        /// <summary>
        /// The PropertyEditTarget behind the currently selected row, or null when nothing is selected.
        /// Setting it selects that row (expanding any collapsed ancestors) and scrolls it into view; set
        /// to null to clear the selection. Returns null until the template has been applied.
        /// </summary>
        public PropertyEditTarget SelectedTarget
        {
            get => this.PART_TreeList?.SelectedItem as PropertyEditTarget;
            set
            {
                if (this.PART_TreeList == null)
                {
                    return;
                }

                if (value == null)
                {
                    this.PART_TreeList.SelectedItems.Clear();
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

        // ===========[ Selection handling ]=======================================
        private void OnTreeListSelectionChanged (object sender, EventArgs<SelectionChange<IObservableTreeNode>> e)
        {
            this.SelectedTargetChanged?.Invoke(this, EventArgs.Empty);
            if (m_controller.OnSelectedTargetChanged(this.SelectedTarget))
            {
                this.SelectedSourceObjectChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // Edit.IsReadOnly is inherited, so it changes here whether the grid's own IsReadOnly set it or an
        // ancestor did; the controller follows the effective value either way
        protected override void OnPropertyChanged (DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == Edit.IsReadOnlyProperty)
            {
                m_controller.IsReadOnly = (bool)e.NewValue;
            }
        }

        private void OnSetPropertyToDefaultExecuted (object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is PropertyEditTarget target)
            {
                target.ResetToDefault();
            }
        }

        private void OnSetPropertyToDefaultCanExecute (object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = e.Parameter is PropertyEditTarget target && PropertyGridRowRules.CanResetToDefault(target, Edit.GetIsReadOnly(this));
        }

        // ===========[ Label splitter ]===========================================
        // A preview sees every move over the grid, even ones an editor handles: shows the splitter over
        // the boundary of the row under the pointer when the pointer is near it, and hides it otherwise
        protected override void OnPreviewMouseMove (MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);
            if (m_isDraggingLabelSplitter || this.PART_TreeList == null || this.PART_LabelSplitter == null)
            {
                return;
            }

            Point pointer = e.GetPosition(this.PART_TreeList);
            if (this.TryGetLabelBoundary(this.FindPropertyRowAt(pointer), out double boundaryX, out double shownWidth)
                && PropertyGridRowRules.IsNearLabelBoundary(pointer.X, boundaryX))
            {
                // A set width is the same boundary on every row, so a drag starts from it
                m_labelSplitterColumnWidth = double.IsNaN(this.LabelColumnWidth) ? shownWidth : this.LabelColumnWidth;
                this.PlaceLabelSplitter(boundaryX);
                this.PART_LabelSplitter.Visibility = Visibility.Visible;
            }
            else
            {
                this.HideLabelSplitter();
            }
        }

        protected override void OnMouseLeave (MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (!m_isDraggingLabelSplitter)
            {
                this.HideLabelSplitter();
            }
        }

        private void OnLabelSplitterMouseLeftButtonDown (object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                this.FitLabelColumnWidthToShownLabels();
                this.HideLabelSplitter();
                e.Handled = true;
                return;
            }

            if (double.IsNaN(m_labelSplitterColumnWidth))
            {
                return;
            }

            m_labelSplitterDragStartX = e.GetPosition(this.PART_TreeList).X;
            m_labelSplitterDragStartBoundaryX = m_labelSplitterBoundaryX;
            m_labelSplitterDragStartWidth = m_labelSplitterColumnWidth;
            m_isDraggingLabelSplitter = this.PART_LabelSplitter.CaptureMouse();
            e.Handled = true;
        }

        private void OnLabelSplitterMouseMove (object sender, MouseEventArgs e)
        {
            if (!m_isDraggingLabelSplitter)
            {
                return;
            }

            // Every move is measured from where the drag started. The line follows the width actually
            // set, which stops at the limits even when the pointer doesn't.
            double pointerDelta = e.GetPosition(this.PART_TreeList).X - m_labelSplitterDragStartX;
            double width = PropertyGridRowRules.ComputeDraggedLabelColumnWidth(m_labelSplitterDragStartWidth, pointerDelta, this.PART_TreeList.ActualWidth);
            this.LabelColumnWidth = width;
            this.PlaceLabelSplitter(m_labelSplitterDragStartBoundaryX + (width - m_labelSplitterDragStartWidth));
        }

        private void OnLabelSplitterMouseLeftButtonUp (object sender, MouseButtonEventArgs e)
        {
            if (m_isDraggingLabelSplitter)
            {
                this.PART_LabelSplitter.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        // A drag that moved has already set its width; a press with no move sets nothing, so a click
        // on the splitter doesn't fix an auto sized column
        private void OnLabelSplitterLostMouseCapture (object sender, MouseEventArgs e)
        {
            m_isDraggingLabelSplitter = false;
        }

        private void FitLabelColumnWidthToShownLabels ()
        {
            var widths = new List<double>();
            foreach (DependencyObject element in TreeTraversal<DependencyObject>.All(this.PART_TreeList, getChildrenMethodOverride: DependencyObjectXT.GetVisualChildren))
            {
                if (element is Grid row && row.Name == kPropertyRowPartName && this.IsRowShown(row) && this.TryMeasureFittingLabelColumnWidth(row, out double width))
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

        // The row template's root (PART_PropertyRow) under a point in PART_TreeList, or null
        private Grid FindPropertyRowAt (Point pointInTreeList)
        {
            for (DependencyObject current = VisualTreeHelper.HitTest(this.PART_TreeList, pointInTreeList)?.VisualHit; current != null && current != this.PART_TreeList; current = current.GetVisualParent())
            {
                if (current is Grid row && row.Name == kPropertyRowPartName)
                {
                    return row;
                }
            }

            return null;
        }

        // The middle of the gap between a row's label and its editor, in PART_TreeList's coordinates,
        // and the label column width the row shows
        private bool TryGetLabelBoundary (Grid row, out double boundaryX, out double shownLabelColumnWidth)
        {
            boundaryX = 0.0;
            shownLabelColumnWidth = double.NaN;
            if (row == null || !(LogicalTreeHelper.FindLogicalNode(row, kPropertyRowLabelPartName) is FrameworkElement label) || !TryGetIndentWidth(row, out double indentWidth))
            {
                return false;
            }

            var gapMiddle = new Point(label.ActualWidth + (label.Margin.Right / 2), 0);
            boundaryX = label.TranslatePoint(gapMiddle, this.PART_TreeList).X;
            shownLabelColumnWidth = PropertyGridRowRules.ToLabelColumnWidth(row.ColumnDefinitions[0].ActualWidth, indentWidth, LabelWidthAfterIndentConverter.kExpanderColumnWidth);
            return true;
        }

        // The label column width that would show a row's whole label. The label column's width is set,
        // so what the label's content wants is measured unconstrained, then marked for a normal measure
        // again, or it keeps this one when the fit leaves the width as it was.
        private bool TryMeasureFittingLabelColumnWidth (Grid row, out double labelColumnWidth)
        {
            labelColumnWidth = double.NaN;
            if (!(LogicalTreeHelper.FindLogicalNode(row, kPropertyRowLabelPartName) is Border label) || label.Child == null || !TryGetIndentWidth(row, out double indentWidth))
            {
                return false;
            }

            label.Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double columnWidth = label.Child.DesiredSize.Width
                + label.Margin.Left + label.Margin.Right
                + label.BorderThickness.Left + label.BorderThickness.Right
                + label.Padding.Left + label.Padding.Right;
            label.Child.InvalidateMeasure();

            labelColumnWidth = PropertyGridRowRules.ToLabelColumnWidth(columnWidth, indentWidth, LabelWidthAfterIndentConverter.kExpanderColumnWidth);
            return true;
        }

        // The row template's DataContext is the PropertyEditTarget; the indent is on the FlatTreeItem
        // its ListBoxItem holds, which the label column's width binding reads the same way
        private static bool TryGetIndentWidth (Grid row, out double indentWidth)
        {
            if (row.GetFirstParentOf<ListBoxItem>()?.DataContext is FlatTreeItem item)
            {
                indentWidth = item.IndentWidth;
                return true;
            }

            indentWidth = 0.0;
            return false;
        }

        // Realized rows include a few just off screen; fitting only counts the ones in view
        private bool IsRowShown (FrameworkElement row)
        {
            Rect bounds = row.TransformToVisual(this.PART_TreeList).TransformBounds(new Rect(0, 0, row.ActualWidth, row.ActualHeight));
            return bounds.Bottom > 0 && bounds.Top < this.PART_TreeList.ActualHeight;
        }

        private void PlaceLabelSplitter (double boundaryX)
        {
            m_labelSplitterBoundaryX = boundaryX;
            this.PART_LabelSplitter.Margin = new Thickness(boundaryX - PropertyGridRowRules.kLabelSplitterHitDistance, 0, 0, 0);
        }

        private void HideLabelSplitter ()
        {
            if (this.PART_LabelSplitter != null)
            {
                this.PART_LabelSplitter.Visibility = Visibility.Collapsed;
            }
        }

        private void UnhookLabelSplitter ()
        {
            if (this.PART_LabelSplitter != null)
            {
                this.PART_LabelSplitter.MouseLeftButtonDown -= this.OnLabelSplitterMouseLeftButtonDown;
                this.PART_LabelSplitter.MouseMove -= this.OnLabelSplitterMouseMove;
                this.PART_LabelSplitter.MouseLeftButtonUp -= this.OnLabelSplitterMouseLeftButtonUp;
                this.PART_LabelSplitter.LostMouseCapture -= this.OnLabelSplitterLostMouseCapture;
            }
        }

        // ===========[ Drag/Drop for list element reordering ]====================
        private void OnDragDropReorderRequested (object sender, FlatTreeReorderEventArgs e)
        {
            PropertyGridController.TryHandleReorder(e);
        }
    }
}
