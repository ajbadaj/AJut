namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using AJut.Storage;
    using AJut.UX.CodeBinding;
    using AJut.UX.PropertyInteraction;
    using AppKit;

    // ===========[ PropertyGrid ]===============================================
    // Reflection-based property editor: give it an object (or several) and it shows a row per
    // public property, each with an editor picked by the property's editor key. Complex
    // sub-objects are expandable rows whose children are their properties.
    //
    // This is the AppKit view only. Everything that never touches a view (building the rows from
    // the source, following its edits, the PG* attributes, ShowIf/HideIf, list editing, teardown,
    // selection by source object, reordering list elements) is AJut.UX's PropertyGridController,
    // the same one the WinUI3 and WPF grids run on. The rows are a FlatTreeListView.
    //
    // Editors come from Editors (a PropertyGridEditorRegistry), the AppKit counterpart of
    // PropertyGridTemplateSelector. Nullable, Button and List rows have built-in editors; call
    // Editors.RegisterStandardEditors() for strings, booleans, numbers and enums as well.
    //
    // The label splitter (PropertyGrid.LabelSplitter.cs) sizes the label column the way it does on
    // the other stacks: drag the line over the label and editor boundary, or double-click it to fit.
    //
    // The controller is reflection based, so the grid carries its RequiresUnreferencedCode and
    // RequiresDynamicCode up to here: an app that trims is warned at its own PropertyGrid, with
    // what to keep, instead of the warning stopping inside AJut. The nested row views carry them
    // too, since a type's annotation does not cover the types nested in it.
    [RequiresUnreferencedCode(PropertyGridTrimming.kMessage)]
    [RequiresDynamicCode(PropertyGridTrimming.kListMessage)]
    public partial class PropertyGrid : NSView, IPropertyGridHost
    {
        private const double kExpanderColumnWidth = 18.0;

        private readonly PropertyGridController m_controller;
        private readonly FlatTreeListView m_tree;
        private readonly HashSet<RowView> m_liveRows = new HashSet<RowView>();
        private double m_labelColumnWidth = double.NaN;
        private IEnumerable m_itemsSource;
        private object m_singleItemSource;
        private bool m_isDisposed;

        // ===========[ Setup/Construction/Teardown ]===========================
        public PropertyGrid ()
        {
            m_controller = new PropertyGridController(this);
            m_controller.PropertyTreeChanged += this.OnControllerPropertyTreeChanged;

            m_tree = new FlatTreeListView
            {
                SelectionMode = eFlatTreeSelectionMode.Single,
                IsDragDropReorderEnabled = true,
                CanDragItem = m_controller.CanDragRow,
                CanDropItem = PropertyGridController.CanDropItem,
                RowContentFactory = this.CreateRowContent,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };
            m_tree.DragDropReorderRequested += this.OnTreeDragDropReorderRequested;
            m_tree.SelectionChanged += this.OnTreeSelectionChanged;
            this.AddSubview(m_tree);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_tree.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_tree.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_tree.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_tree.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
            });

            m_labelSplitter = new LabelSplitterView(this);
            this.AddSubview(m_labelSplitter);
            m_labelSplitterLeading = m_labelSplitter.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_labelSplitterLeading,
                m_labelSplitter.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_labelSplitter.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_labelSplitter.WidthAnchor.ConstraintEqualTo((nfloat)PropertyGridRowRules.kLabelSplitterHitDistance),
            });
        }

        // An NSView's Dispose is also its release, so the grid's teardown (every subscription it made,
        // and the source) runs here, on the managed side going away. To empty a grid that stays in
        // use, set its sources to null instead.
        protected override void Dispose (bool disposing)
        {
            if (disposing && !m_isDisposed)
            {
                m_isDisposed = true;
                m_tree.DragDropReorderRequested -= this.OnTreeDragDropReorderRequested;
                m_tree.SelectionChanged -= this.OnTreeSelectionChanged;
                m_controller.PropertyTreeChanged -= this.OnControllerPropertyTreeChanged;
                m_controller.Dispose();
                this.SingleItemSource = null;
                this.ItemsSource = null;
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        /// <summary>Raised whenever any property in the tree is edited through the grid</summary>
        public event EventHandler<EventArgs> PropertyTreeChanged;

        /// <summary>Raised whenever the selected row changes</summary>
        public event EventHandler<EventArgs> SelectedTargetChanged;

        /// <summary>
        /// Raised when the source object behind the selected row changes. Moving between two rows inside the same
        /// list element's subtree resolves to the same element, so it does not fire then.
        /// </summary>
        public event EventHandler<EventArgs> SelectedSourceObjectChanged;

        public override void ViewDidMoveToWindow ()
        {
            base.ViewDidMoveToWindow();

            // Out of a window, drop the source to grid subscription so a long-lived source does not keep
            // a grid that was dropped without Dispose; back in one, restore it and catch up
            if (this.Window != null)
            {
                m_controller.OnAttached();
            }
            else
            {
                m_controller.OnDetached();
            }
        }

        private void OnControllerPropertyTreeChanged (object sender, EventArgs e)
        {
            this.PropertyTreeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnTreeSelectionChanged (object sender, SelectionChange<FlatTreeItem> e)
        {
            this.SelectedTargetChanged?.Invoke(this, EventArgs.Empty);
            if (m_controller.OnSelectedTargetChanged(this.SelectedTarget))
            {
                this.SelectedSourceObjectChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnTreeDragDropReorderRequested (object sender, FlatTreeReorderEventArgs e)
        {
            PropertyGridController.TryHandleReorder(e);
        }

        // ===========[ Properties ]============================================
        public override bool IsFlipped => true;

        /// <summary>One object to edit. Setting it clears <see cref="ItemsSource"/>.</summary>
        public object SingleItemSource
        {
            get => m_singleItemSource;
            set
            {
                if (ReferenceEquals(m_singleItemSource, value))
                {
                    return;
                }

                if (value != null)
                {
                    this.ItemsSource = null;
                }

                m_singleItemSource = value;
                m_controller.OnSingleItemSourceChanged(value);
            }
        }

        /// <summary>Several objects to edit together. An observable collection is followed. Setting it clears <see cref="SingleItemSource"/>.</summary>
        public IEnumerable ItemsSource
        {
            get => m_itemsSource;
            set
            {
                if (ReferenceEquals(m_itemsSource, value))
                {
                    return;
                }

                if (value != null)
                {
                    this.SingleItemSource = null;
                }

                m_itemsSource = value;
                m_controller.OnItemsSourceChanged(value);
            }
        }

        /// <summary>
        /// Makes the whole grid read-only: no editor takes edits, button rows and a list's add button are disabled, list
        /// elements show no remove button and can't be dragged to reorder, and nothing offers "Set to default". Rows
        /// still expand, collapse and select. Changing it builds the shown rows again.
        /// </summary>
        public bool IsReadOnly
        {
            get => m_controller.IsReadOnly;
            set
            {
                if (m_controller.IsReadOnly == value)
                {
                    return;
                }

                // Every row reads it as it is built, which is when its editors are made and its
                // commands bound, so the shown rows are built again rather than patched
                m_controller.IsReadOnly = value;
                m_tree.ReloadRowContent();
            }
        }

        /// <summary>The editor factories, by editor key</summary>
        public PropertyGridEditorRegistry Editors { get; } = new PropertyGridEditorRegistry();

        /// <summary>The manager that builds the rows from the source</summary>
        public PropertyGridManager Manager => m_controller.Manager;

        /// <summary>How deep sub-objects are expanded into rows</summary>
        public int MaxRecursionDepth
        {
            get => m_controller.Manager.MaxRecursionDepth;
            set => m_controller.Manager.MaxRecursionDepth = value;
        }

        /// <summary>Points of indent per depth</summary>
        public double TreeDepthIndentSize
        {
            get => m_tree.TreeDepthIndentSize;
            set => m_tree.TreeDepthIndentSize = value;
        }

        /// <summary>Space between rows, in points</summary>
        public double RowSpacing
        {
            get => m_tree.RowSpacing;
            set => m_tree.RowSpacing = value;
        }

        /// <summary>A fixed row height, or NaN (the default) for rows sized to their editors</summary>
        public double FixedRowHeight
        {
            get => m_tree.FixedRowHeight;
            set => m_tree.FixedRowHeight = value;
        }

        /// <summary>
        /// Width of the label column in points, so every row's editor starts at the same x whatever its depth; NaN
        /// (the default) sizes each label to its text. The label splitter sets it.
        /// </summary>
        public double LabelColumnWidth
        {
            get => m_labelColumnWidth;
            set
            {
                m_labelColumnWidth = value;
                foreach (RowView row in m_liveRows)
                {
                    row.ApplyLabelColumnWidth(value);
                }
            }
        }

        /// <summary>Where each property's tooltip shows; on its name by default</summary>
        public ePropertyToolTipPlacement PropertyToolTipPlacement { get; set; } = ePropertyToolTipPlacement.PropertyNameOnly;

        /// <summary>The element count text on a list row; {0} is the count</summary>
        public string ListElementCountFormat { get; set; } = "Elements ({0})";

        /// <summary>Space around each row's label and editor</summary>
        public NSEdgeInsets ElementPadding { get; set; } = new NSEdgeInsets(2, 3, 2, 3);

        /// <summary>The row selected now, or null. Setting it selects that row, expanding its ancestors, and scrolls to it.</summary>
        public PropertyEditTarget SelectedTarget
        {
            get => m_tree.SelectedItem?.Source as PropertyEditTarget;
            set
            {
                if (value == null)
                {
                    m_tree.SetSelection(null);
                    return;
                }

                m_tree.TrySelectItemForSource(value, scrollIntoView: true);
            }
        }

        /// <summary>The source object (list element) behind the selected row, or null</summary>
        public object SelectedSourceObject => PropertyGridController.ResolveSourceObject(this.SelectedTarget);

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Selects the row for a list element instance, by reference. False when no row shows it.</summary>
        public bool TrySelectSourceObject (object sourceObject)
        {
            if (sourceObject == null)
            {
                return false;
            }

            PropertyEditTarget match = m_controller.FindTargetForSourceObject(sourceObject);
            return match != null && m_tree.TrySelectItemForSource(match, scrollIntoView: true);
        }

        // ===========[ IPropertyGridHost Implementations ]=====================
        void IPropertyGridHost.ShowRootItems (IReadOnlyList<IObservableTreeNode> rootItems)
        {
            m_tree.RootItemsSource = rootItems;
        }

        // ===========[ Helper Methods ]========================================
        private NSView CreateRowContent (FlatTreeItem item)
        {
            return new RowView(this, item);
        }

        // The editor for a target: a registered one wins, then a built-in one, then Default. A
        // button row or a list header has no setter of its own, so those follow the grid's
        // read-only state alone, and value editors get CanEdit's answer.
        internal NSView CreateEditor (PropertyEditTarget target, BindingSet bindings)
        {
            bool isReadOnly = !PropertyGridRowRules.CanEdit(target, this.IsReadOnly);
            string key = PropertyGridRowRules.GetEditorKey(target);
            if (this.Editors.TryGet(key, out PropertyEditorFactory factory))
            {
                return factory(target, bindings, isReadOnly);
            }

            switch (PropertyGridRowRules.GetBuiltInEditor(target, this.Editors.IsRegistered))
            {
                case eBuiltInPropertyEditor.Nullable: return new NullableEditorView(this, target, bindings, isReadOnly);
                case eBuiltInPropertyEditor.Button: return CreateButtonEditor(target, bindings, this.IsReadOnly);
                case eBuiltInPropertyEditor.List: return this.CreateListEditor(target, bindings);
            }

            return this.Editors.Default?.Invoke(target, bindings, isReadOnly);
        }

        private static NSView CreateButtonEditor (PropertyEditTarget target, BindingSet bindings, bool isGridReadOnly)
        {
            var button = new NSButton { Title = target.EditValue?.ToString() ?? target.DisplayName ?? String.Empty };
            button.BezelStyle = NSBezelStyle.Rounded;

            // A bound command enables its button whenever it can execute, so a read-only grid
            // leaves it unbound and the button disabled
            if (isGridReadOnly)
            {
                button.Enabled = false;
            }
            else if (target.EditContext is System.Windows.Input.ICommand command)
            {
                bindings.BindCommand(button, command);
            }

            return button;
        }

        private NSView CreateListEditor (PropertyEditTarget target, BindingSet bindings)
        {
            var row = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: 6.0);
            NSTextField count = NSTextField.CreateLabel(String.Empty);
            row.Add(count, alignment: eFillStackCrossAlignment.Center);

            if (target.EditContext is PropertyGridListContext listContext)
            {
                string format = this.ListElementCountFormat;
                bindings.BindLabel(count, listContext, nameof(PropertyGridListContext.ElementCount), _ReadCount);
                if (listContext.CanAdd)
                {
                    var add = new NSButton { Title = "+" };
                    add.BezelStyle = NSBezelStyle.Rounded;
                    if (this.IsReadOnly)
                    {
                        add.Enabled = false;
                    }
                    else
                    {
                        bindings.BindCommand(add, listContext.AddCommand);
                    }

                    row.Add(add, alignment: eFillStackCrossAlignment.Center);
                }

                string _ReadCount () => String.Format(format, listContext.ElementCount);
            }

            return row;
        }
    }
}
