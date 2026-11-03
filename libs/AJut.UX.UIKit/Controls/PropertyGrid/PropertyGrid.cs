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
    using CoreGraphics;
    using ObjCRuntime;
    using UIKit;

    // ===========[ PropertyGrid ]===============================================
    // Reflection-based property editor: give it an object (or several) and it shows a row per
    // public property, each with an editor picked by the property's editor key. Complex
    // sub-objects are expandable rows whose children are their properties.
    //
    // This is the UIKit view only. Everything that never touches a view (building the rows from
    // the source, following its edits, the PG* attributes, ShowIf/HideIf, list editing, teardown,
    // selection by source object, reordering list elements) is AJut.UX's PropertyGridController,
    // the same one the WinUI3, WPF, and AppKit grids run on. The rows are a FlatTreeListView in an
    // inset grouped table, the card a settings form sits in.
    //
    // A phone is too narrow for a label column beside the editors, so each row puts the property's
    // name above its editor, and the editor takes the row's width. There is no LabelColumnWidth and
    // no label splitter here, which only mean something with a label column. A compact editor (a
    // switch, a color swatch) sits at the end of the name's line instead, as a settings form sets it
    // out (see PropertyGridEditorRegistry's placements).
    //
    // Editors come from Editors (a PropertyGridEditorRegistry), the UIKit counterpart of
    // PropertyGridTemplateSelector. Nullable, Button, and List rows have built-in editors; call
    // Editors.RegisterStandardEditors() for strings, booleans, numbers, colors, and enums as well.
    //
    // "Set to default" is a row's long press menu. A list element that can be dragged lifts to drag
    // on a long press instead. A phone has no tooltips, so a property's tooltip text is read by
    // VoiceOver as the hint on its name, its editor, or both, following PropertyToolTipPlacement.
    //
    // A UIKit view and its managed peer form a cycle the GC can't break when anything native the
    // view retains holds the view: the native side keeps the peer alive, and the peer keeps the
    // native side alive. The tree is retained by this grid, so nothing the tree holds may lead back
    // to the grid while the grid is out of a window, or a grid dropped without Dispose is never
    // collected:
    //  - the row factory, drag check, and menu factory go through TreeHooks, which holds the grid
    //    weakly;
    //  - the grid listens to the tree's events only while it is in a window;
    //  - the tree is only given the rows while the grid is in a window, since every row's target
    //    reaches the grid through the controller following it. Back in a window the grid gives them
    //    back, as the controller has them after catching up, with the selection kept.
    //
    // The controller is reflection based, so the grid passes its RequiresUnreferencedCode and
    // RequiresDynamicCode on to the app, as the AppKit grid does: on the SingleItemSource and
    // ItemsSource setters, with SetSingleItemSource<T> and SetItemsSource<T> as the trim-safe way in.
    public partial class PropertyGrid : UIView, IPropertyGridHost
    {
        private PropertyGridController m_controller;
        private FlatTreeListView m_tree;
        private IEnumerable m_itemsSource;
        private object m_singleItemSource;
        private bool m_isListeningToTree;
        private bool m_isDisposed;

        // The row selected when the grid left its window, selected again when it comes back
        private PropertyEditTarget m_selectedWhenDetached;

        // ===========[ Setup/Construction/Teardown ]===========================
        public PropertyGrid () : base(CGRect.Empty)
        {
            this.Setup();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native view.</summary>
        protected PropertyGrid (NativeHandle handle) : base(handle)
        {
            this.Setup();
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
        private void Setup ()
        {
            m_controller = new PropertyGridController(this);
            m_controller.PropertyTreeChanged += this.OnControllerPropertyTreeChanged;

            var hooks = new TreeHooks(this);
            m_tree = new FlatTreeListView(UITableViewStyle.InsetGrouped)
            {
                TranslatesAutoresizingMaskIntoConstraints = false,
                SelectionMode = eFlatTreeSelectionMode.Single,
                IsDragDropReorderEnabled = true,
                TreeDepthIndentSize = MobileLook.PropertyGrid.TreeDepthIndentSize,
                CanDragItem = hooks.CanDragRow,
                CanDropItem = PropertyGridController.CanDropItem,
                RowContentFactory = hooks.CreateRowContent,
                RowMenuFactory = hooks.CreateRowMenu,
            };

            m_tree.Look = new FlatTreeListView.RowLook(
                SelectionFill: null,
                SelectionAccent: MobileLook.PropertyGrid.SelectionAccent,
                PressedFill: MobileLook.PropertyGrid.PressedFill,
                PressedBorder: MobileLook.PropertyGrid.PressedBorder
            );

            this.AddSubview(m_tree);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_tree.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_tree.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_tree.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_tree.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
            });
        }

        // An NSObject's Dispose is also its release, so the grid's teardown (every subscription it made, and the source)
        // runs here, on the managed side going away. To empty a grid that stays in use, set its sources to null instead.
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
        /// Raised when the source object behind the selected row changes. Moving between two rows inside the same list
        /// element's subtree resolves to the same element, so it does not fire then.
        /// </summary>
        public event EventHandler<EventArgs> SelectedSourceObjectChanged;

        public override void MovedToWindow ()
        {
            base.MovedToWindow();

            // Out of a window, drop the source to grid subscription so a long lived source does not keep a grid that was
            // dropped without Dispose, stop listening to the tree, and take the rows out of it (see the class comment).
            // Back in one, catch up, give the tree the rows again with the selection as it was, then listen again, so
            // putting the rows back raises no selection events of its own.
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
        /// elements show no remove button and can't be dragged to reorder, and nothing offers "Set to default". Rows still
        /// expand, collapse, and select. Changing it builds the shown rows again.
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

                // Every row reads it as it is built, which is when its editors are made and its commands bound, so the
                // shown rows are built again rather than patched
                m_controller.IsReadOnly = value;
                m_tree.ReloadRowContent();
            }
        }

        /// <summary>
        /// Whether the grid takes touches. False is <see cref="IsReadOnly"/> and more: nothing is edited, and no row
        /// expands, collapses, selects, or scrolls, and each editor shows its own disabled look. A UIView has no Enabled to
        /// hand down to the editors in its rows, so the grid passes it on itself. Changing it builds the shown rows again.
        /// </summary>
        public bool Enabled
        {
            get => m_tree.Enabled;
            set => m_tree.Enabled = value;
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

        /// <summary>A fixed row height, or NaN (the default) for rows sized to their name and editor</summary>
        public double FixedRowHeight
        {
            get => m_tree.FixedRowHeight;
            set => m_tree.FixedRowHeight = value;
        }

        /// <summary>Which of a row's name and editor VoiceOver reads the property's tooltip text on, as its hint; on its name by default</summary>
        public ePropertyToolTipPlacement PropertyToolTipPlacement { get; set; } = ePropertyToolTipPlacement.PropertyNameOnly;

        /// <summary>The element count text on a list row; {0} is the count</summary>
        public string ListElementCountFormat { get; set; } = "Elements ({0})";

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
        /// can't tell which type the grid reads, and under full trimming it removes the properties nothing else uses: their
        /// rows never show, even with [DynamicallyAccessedMembers] on the type. A sub-object or list element type the grid
        /// expands isn't covered by T; keep its members with
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
        /// <see cref="SetSingleItemSource{T}"/>. An item of a type derived from T, and a sub-object or list element type the
        /// grid expands, aren't covered by T; keep their members with
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
            return (match != null) && m_tree.TrySelectItemForSource(match, scrollIntoView: true);
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
        // A row takes no edits, runs no button, and offers no add, remove, or reset while the grid is read-only or disabled
        private bool IsLocked => this.IsReadOnly || !this.Enabled;

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

        // "Set to default", where it applies. A row that can be dragged has none, since its long press lifts it to drag.
        private UIMenu CreateRowMenu (FlatTreeItem item)
        {
            if ((item.Source is not PropertyEditTarget target)
                || !PropertyGridRowRules.CanResetToDefault(target, this.IsLocked)
                || this.CanDragRow(target))
            {
                return null;
            }

            UIAction setToDefault = UIAction.Create(
                "Set to default",
                UIImage.GetSystemImage("arrow.uturn.backward"),
                null,
                new ResetAction(target).Run
            );
            return UIMenu.Create(new UIMenuElement[] { setToDefault });
        }

        // The editor for a target: a registered one wins, then a built-in one, then Default. A button row or a list header
        // has no setter of its own, so those follow the grid's read-only (or disabled) state alone, and value editors get
        // CanEdit's answer.
        internal UIView CreateEditor (PropertyEditTarget target, BindingSet bindings, out ePropertyEditorPlacement placement)
        {
            bool isReadOnly = !PropertyGridRowRules.CanEdit(target, this.IsLocked);
            string key = PropertyGridRowRules.GetEditorKey(target);
            placement = this.Editors.GetPlacement(key);
            if (this.Editors.TryGet(key, out PropertyEditorFactory factory))
            {
                return factory(target, bindings, isReadOnly);
            }

            switch (PropertyGridRowRules.GetBuiltInEditor(target, this.Editors.IsRegistered))
            {
                case eBuiltInPropertyEditor.Nullable:
                    return new NullableEditorView(this, target, bindings, isReadOnly);

                case eBuiltInPropertyEditor.Button:
                    placement = ePropertyEditorPlacement.BelowLabel;
                    return CreateButtonEditor(target, bindings, this.IsLocked);

                case eBuiltInPropertyEditor.List:
                    placement = ePropertyEditorPlacement.BesideLabel;
                    return this.CreateListEditor(target, bindings);
            }

            return this.Editors.Default?.Invoke(target, bindings, isReadOnly);
        }

        private static UIView CreateButtonEditor (PropertyEditTarget target, BindingSet bindings, bool isLocked)
        {
            // Themed, the same face as a plain button beside the grid; unthemed, the system's gray button
            UIButtonConfiguration configuration = UIButtonConfiguration.GrayButtonConfiguration;
            configuration.Title = target.EditValue?.ToString() ?? target.DisplayName ?? string.Empty;
            var button = new UIButton(CGRect.Empty) { Configuration = configuration };
            StockControlTint.ApplyToOwnButton(button);

            // A bound command enables its button whenever it can execute, so a read-only or disabled grid leaves it
            // unbound and the button disabled
            if (isLocked)
            {
                button.Enabled = false;
            }
            else if (target.EditContext is System.Windows.Input.ICommand command)
            {
                bindings.BindCommand(button, command);
            }

            return button;
        }

        private UIView CreateListEditor (PropertyEditTarget target, BindingSet bindings)
        {
            const float kSpacing = 8f;

            var row = new UIStackView
            {
                Axis = UILayoutConstraintAxis.Horizontal,
                Alignment = UIStackViewAlignment.Center,
                Spacing = kSpacing,
            };

            var count = new UILabel
            {
                Font = UIFont.SystemFontOfSize(MobileLook.PropertyGrid.ListElementHeaderFontSize),
                TextColor = RowColors.ListElementHeader,
                Alpha = MobileLook.PropertyGrid.ListElementHeaderOpacity,
            };

            row.AddArrangedSubview(count);
            if (target.EditContext is PropertyGridListContext listContext)
            {
                string format = this.ListElementCountFormat;
                bindings.BindLabel(count, listContext, nameof(PropertyGridListContext.ElementCount), _ReadCount);
                if (listContext.CanAdd)
                {
                    var add = UIButton.FromType(UIButtonType.ContactAdd);
                    add.AccessibilityLabel = "Add";
                    if (this.IsLocked)
                    {
                        add.Enabled = false;
                    }
                    else
                    {
                        bindings.BindCommand(add, listContext.AddCommand);
                    }

                    row.AddArrangedSubview(add);
                }

                string _ReadCount () => string.Format(format, listContext.ElementCount);
            }

            return row;
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

            public UIView CreateRowContent (FlatTreeItem item)
            {
                return m_grid.TryGetTarget(out PropertyGrid grid) ? new RowView(grid, item) : new UIView();
            }

            public UIMenu CreateRowMenu (FlatTreeItem item)
            {
                return m_grid.TryGetTarget(out PropertyGrid grid) ? grid.CreateRowMenu(item) : null;
            }
        }

        // A menu action's handler, holding only the target it resets
        private sealed class ResetAction
        {
            private readonly PropertyEditTarget m_target;

            public ResetAction (PropertyEditTarget target)
            {
                m_target = target;
            }

            public void Run (UIAction action)
            {
                m_target.ResetToDefault();
            }
        }
    }
}
