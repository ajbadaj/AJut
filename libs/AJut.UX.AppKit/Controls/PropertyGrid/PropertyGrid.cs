namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using AJut.Storage;
    using AJut.UX.CodeBinding;
    using AJut.UX.PropertyInteraction;
    using AJut.UX.Theming;
    using AppKit;
    using ObjCRuntime;

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
    // An AppKit view and its managed peer keep each other alive while anything native the view
    // retains holds the view: the native side roots the peer, and the peer roots the native side.
    // This grid natively retains its tree and its label splitter, so nothing they hold may lead back
    // to the grid while it is out of a window, or a grid dropped without Dispose is never collected:
    //  - the label splitter and each row hold the grid weakly;
    //  - the tree's row factory and drag check go through TreeHooks, which holds the grid weakly,
    //    and the grid listens to the tree's events only while it is in a window;
    //  - the tree is only given the rows while the grid is in a window, since every row's target
    //    reaches the grid through the controller following it. Back in a window the grid gives them
    //    back, as the controller has them after catching up, with the selection kept.
    //
    // The controller is reflection based, so the grid passes its RequiresUnreferencedCode and
    // RequiresDynamicCode on to the app: an app that trims is warned where it sets SingleItemSource
    // or ItemsSource, with what to keep, instead of the warning stopping inside AJut. That is the
    // one place an app hands the grid a type to reflect over; everything else the grid does with
    // the controller works on what that source built, and says so in a suppression. Those two take
    // an object, which no annotation can keep members through, so SetSingleItemSource<T> and
    // SetItemsSource<T> are the trim-safe way in: their T carries [DynamicallyAccessedMembers(All)],
    // and the trimmer keeps the shown type's members from the call.
    //
    // The warning is on those setters and not on the class because a type-level annotation also
    // puts it on every member reached by reflection, and the macOS static registrar's generated code
    // reaches every member the grid and its nested views export to Objective-C (each override, such
    // as IsFlipped, Layout or HitTest). A class-level annotation made a trimmed app's build warn
    // once per exported member, burying the one warning that tells the app what to do.
    public partial class PropertyGrid : NSView, IPropertyGridHost
    {
        private const double kExpanderColumnWidth = 18.0;

        private readonly PropertyGridController m_controller;
        private readonly FlatTreeListView m_tree;
        private readonly HashSet<RowView> m_liveRows = new HashSet<RowView>();
        private double m_labelColumnWidth = double.NaN;
        private IEnumerable m_itemsSource;
        private object m_singleItemSource;
        private bool m_isListeningToTree;
        private bool m_isDisposed;

        // The row selected when the grid left its window, selected again when it comes back
        private PropertyEditTarget m_selectedWhenDetached;

        // ===========[ Setup/Construction/Teardown ]===========================
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
        public PropertyGrid ()
        {
            m_controller = new PropertyGridController(this);
            m_controller.PropertyTreeChanged += this.OnControllerPropertyTreeChanged;

            var hooks = new TreeHooks(this);
            m_tree = new FlatTreeListView
            {
                SelectionMode = eFlatTreeSelectionMode.Single,
                IsDragDropReorderEnabled = true,
                CanDragItem = hooks.CanDragRow,
                CanDropItem = PropertyGridController.CanDropItem,
                RowContentFactory = hooks.CreateRowContent,

                // Static, so it holds nothing that leads back to the grid
                DragGhostTextFactory = ReadDragGhostText,
                TranslatesAutoresizingMaskIntoConstraints = false,

                // The WinUI3 grid's rows have no content margin, so its depth bands sit 6 right of the plain tree's
                DragDepthRowOffset = 0.0,

                // The grid's tree has no fill or outline of its own, as WinUI3's PropertyGrid template sets its tree
                ListBackgroundColorKey = null,
                ListBorderColorKey = null,

                // Each row marks its own selection on its label (see RowView)
                DrawsRowSelection = false,
            };
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

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native view whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls: the native view keeps the tree and splitter it has.
        /// None of its managed state survives a dispose, so a call that needs that state can still fail: dispose it only
        /// once AppKit is done with it.
        /// </summary>
        protected PropertyGrid (NativeHandle handle) : base(handle)
        {
        }

        // An NSView's Dispose is also its release, so the grid's teardown (every subscription it made,
        // and the source) runs here, on the managed side going away. To empty a grid that stays in
        // use, set its sources to null instead.
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
        protected override void Dispose (bool disposing)
        {
            if (disposing && !m_isDisposed)
            {
                m_isDisposed = true;
                this.ListenToTree(false);
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
            // a grid that was dropped without Dispose, stop listening to the tree, and take the rows out of
            // it (see the class comment). Back in one, catch up, give the tree the rows again with the
            // selection as it was, then listen again, so putting the rows back raises no selection events
            // of its own.
            if (this.Window != null)
            {
                m_controller.OnAttached();
                this.ShowRootItemsInTree();
                if (m_selectedWhenDetached != null)
                {
                    m_tree.TrySelectItemForSource(m_selectedWhenDetached, scrollIntoView: false);
                    m_selectedWhenDetached = null;
                }

                this.ListenToTree(true);
            }
            else
            {
                m_selectedWhenDetached = this.SelectedTarget;
                this.ListenToTree(false);
                m_controller.OnDetached();
                m_tree.RootItemsSource = null;
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

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
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

            [RequiresUnreferencedCode(PropertyGridTrimming.kMessage)]
            [RequiresDynamicCode(PropertyGridTrimming.kListMessage)]
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

            [RequiresUnreferencedCode(PropertyGridTrimming.kMessage)]
            [RequiresDynamicCode(PropertyGridTrimming.kListMessage)]
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
        public object SelectedSourceObject
        {
            [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
            [UnconditionalSuppressMessage("AOT", "IL3050", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
            get => PropertyGridController.ResolveSourceObject(this.SelectedTarget);
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Shows <paramref name="source"/>, the same as setting <see cref="SingleItemSource"/>, and keeps the members of
        /// <typeparamref name="T"/> the grid reads in an app that trims. SingleItemSource takes an object, so the trimmer
        /// can't tell which type the grid reads, and under full trimming it removes the properties nothing else uses:
        /// their rows never show, even with [DynamicallyAccessedMembers] on the type. A sub-object or list element type
        /// the grid expands isn't covered by T; keep its members with
        /// [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(...))] on the method that calls this.
        /// </summary>
        [RequiresDynamicCode(PropertyGridTrimming.kListMessage)]
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PropertyGridTrimming.kTypeParameterKeepsTheMembers)]
        public void SetSingleItemSource<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> (T source)
        {
            this.SingleItemSource = source;
        }

        /// <summary>
        /// Shows <paramref name="items"/>, the same as setting <see cref="ItemsSource"/>, and keeps the members of
        /// <typeparamref name="T"/> the grid reads in an app that trims, for the same reason as
        /// <see cref="SetSingleItemSource{T}"/>. An item of a type derived from T, and a sub-object or list element type
        /// the grid expands, aren't covered by T; keep their members with
        /// [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(...))] on the method that calls this.
        /// </summary>
        [RequiresDynamicCode(PropertyGridTrimming.kListMessage)]
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PropertyGridTrimming.kTypeParameterKeepsTheMembers)]
        public void SetItemsSource<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> (IEnumerable<T> items)
        {
            this.ItemsSource = items;
        }

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
        // Out of a window the tree is given nothing (see the class comment); coming back gives it the controller's rows
        void IPropertyGridHost.ShowRootItems (IReadOnlyList<IObservableTreeNode> rootItems)
        {
            if (this.Window != null)
            {
                this.ShowRootItemsInTree();
            }
        }

        // ===========[ Helper Methods ]========================================
        // The tree is only rebuilt when it would show something new: a catch-up that rebuilt the rows has given them already
        private void ShowRootItemsInTree ()
        {
            if (!ReferenceEquals(m_tree.RootItemsSource, m_controller.RootItems))
            {
                m_tree.RootItemsSource = m_controller.RootItems;
            }
        }

        private void ListenToTree (bool listen)
        {
            if (listen == m_isListeningToTree)
            {
                return;
            }

            m_isListeningToTree = listen;
            if (listen)
            {
                m_tree.DragDropReorderRequested += this.OnTreeDragDropReorderRequested;
                m_tree.SelectionChanged += this.OnTreeSelectionChanged;
            }
            else
            {
                m_tree.DragDropReorderRequested -= this.OnTreeDragDropReorderRequested;
                m_tree.SelectionChanged -= this.OnTreeSelectionChanged;
            }
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
        private bool CanDragRow (IObservableTreeNode node) => m_controller.CanDragRow(node);

        // A dragged list element is named by its row's name, rather than by the edit target's type
        private static string ReadDragGhostText (FlatTreeItem item) => (item.Source as PropertyEditTarget)?.DisplayName;

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
            var button = new BrushButton { Title = target.EditValue?.ToString() ?? target.DisplayName ?? String.Empty };

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
            // WinUI3's: the count in the row's text, faded, and an add button at least 24 square
            const double kCountOpacity = 0.7;
            const double kAddButtonMinSize = 24.0;

            var row = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: 6.0);
            NSTextField count = NSTextField.CreateLabel(String.Empty);
            count.TextColor = ThemedColors.Get(ThemedColorKeys.PropertyGridRow_LabelForeground);
            count.AlphaValue = (nfloat)kCountOpacity;
            row.Add(count, alignment: eFillStackCrossAlignment.Center);

            if (target.EditContext is PropertyGridListContext listContext)
            {
                string format = this.ListElementCountFormat;
                bindings.BindLabel(count, listContext, nameof(PropertyGridListContext.ElementCount), _ReadCount);
                if (listContext.CanAdd)
                {
                    BrushButton add = CreateGlyphButton("+", "Add element", kAddButtonMinSize);
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

        // The grid's buttons are AJut's own BrushButton, on the Button keys, as the flagships' are themed Buttons; a stock
        // NSButton takes only a tint, and no hover. A glyph button (add, remove) is padded 2 by 4 and kept at least
        // minSize square, as WinUI3's are.
        private static BrushButton CreateGlyphButton (string glyph, string toolTip, double minSize)
        {
            const double kPaddingVertical = 2.0;
            const double kPaddingHorizontal = 4.0;

            var button = new BrushButton
            {
                Title = glyph,
                ToolTip = toolTip,
                Padding = new NSEdgeInsets((nfloat)kPaddingVertical, (nfloat)kPaddingHorizontal, (nfloat)kPaddingVertical, (nfloat)kPaddingHorizontal),
                TranslatesAutoresizingMaskIntoConstraints = false,
            };

            NSLayoutConstraint.ActivateConstraints(new[]
            {
                button.WidthAnchor.ConstraintGreaterThanOrEqualTo((nfloat)minSize),
                button.HeightAnchor.ConstraintGreaterThanOrEqualTo((nfloat)minSize),
            });

            return button;
        }

        // A text button in a row, Set and Unset, padded 3 by 8 as WinUI3's NullableEditor pads them
        private static BrushButton CreateTextButton (string title)
        {
            const double kPaddingVertical = 3.0;
            const double kPaddingHorizontal = 8.0;

            return new BrushButton
            {
                Title = title,
                Padding = new NSEdgeInsets((nfloat)kPaddingVertical, (nfloat)kPaddingHorizontal, (nfloat)kPaddingVertical, (nfloat)kPaddingHorizontal),
            };
        }

        // ===========[ Subclasses/structs ]====================================
        // What the tree calls back into the grid for. The tree is retained by the grid, so these hold the grid weakly; a
        // strong hold would loop through native code where the GC can't see it.
        private sealed class TreeHooks
        {
            private readonly WeakReference<PropertyGrid> m_grid;

            public TreeHooks (PropertyGrid grid)
            {
                m_grid = new WeakReference<PropertyGrid>(grid);
            }

            public bool CanDragRow (IObservableTreeNode node)
            {
                return m_grid.TryGetTarget(out PropertyGrid grid) && grid.CanDragRow(node);
            }

            public NSView CreateRowContent (FlatTreeItem item)
            {
                return m_grid.TryGetTarget(out PropertyGrid grid) ? new RowView(grid, item) : new NSView();
            }
        }
    }
}
