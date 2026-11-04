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
    using Android.Content;
    using Android.Content.Res;
    using Android.Runtime;
    using Android.Util;
    using Android.Views;
    using Android.Widget;

    // ===========[ PropertyGrid ]===============================================
    // Reflection-based property editor: give it an object (or several) and it shows a row per
    // public property, each with an editor picked by the property's editor key. Complex
    // sub-objects are expandable rows whose children are their properties.
    //
    // This is the Android view only. Everything that never touches a view (building the rows from
    // the source, following its edits, the PG* attributes, ShowIf/HideIf, list editing, teardown,
    // selection by source object, reordering list elements) is AJut.UX's PropertyGridController,
    // the same one the WinUI3, WPF, AppKit, and UIKit grids run on. The rows are a FlatTreeListView.
    //
    // A phone is too narrow for a label column beside the editors, so each row puts the property's
    // name above its editor, and the editor takes the row's width. There is no LabelColumnWidth and
    // no label splitter here, which only mean something with a label column. A compact editor (a
    // switch, a color swatch) sits at the end of the name's line instead, as a settings screen sets
    // it out (see PropertyGridEditorRegistry's placements). The rows are a plain list on the grid's
    // own background, as Android's settings screens are, where iOS sets them in a grouped card.
    //
    // Editors come from Editors (a PropertyGridEditorRegistry), the Android counterpart of
    // PropertyGridTemplateSelector. Nullable, Button, and List rows have built-in editors; call
    // Editors.RegisterStandardEditors() for strings, booleans, numbers, colors, and enums as well.
    //
    // "Set to default" is a row's long press menu. A list element that can be dragged lifts to drag
    // on a long press instead. A phone has no hover tooltips, so a property's tooltip text is what
    // TalkBack reads as the hint on its name, its editor, or both, following PropertyToolTipPlacement.
    //
    // Out of a window the grid lets go of its source (the controller drops the source to grid
    // subscription) and gives its tree no rows, so a long lived source never keeps a grid that was
    // dropped without Dispose; back in one it catches up and gives the rows back, with the selection
    // kept. The tree is the grid's own child, so its hooks back into the grid hold nothing the view
    // hierarchy doesn't already.
    //
    // The controller is reflection based, so the grid passes its RequiresUnreferencedCode and
    // RequiresDynamicCode on to the app, as the AppKit and UIKit grids do: on the SingleItemSource
    // and ItemsSource setters, with SetSingleItemSource<T> and SetItemsSource<T> as the trim-safe way in.
    [Register("ajut.ux.controls.PropertyGrid")]
    public partial class PropertyGrid : FrameLayout, IPropertyGridHost
    {
        private PropertyGridController m_controller = null!;
        private FlatTreeListView m_tree = null!;
        private IEnumerable? m_itemsSource;
        private object? m_singleItemSource;
        private bool m_isListeningToTree;
        private bool m_isDisposed;

        // The row selected when the grid left its window, selected again when it comes back
        private PropertyEditTarget? m_selectedWhenDetached;

        // ===========[ Setup/Construction/Teardown ]===========================
        public PropertyGrid (Context context) : base(context)
        {
            this.Initialize(context, null);
        }

        public PropertyGrid (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            this.Initialize(context, attrs);
        }

        public PropertyGrid (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.Initialize(context, attrs);
        }

        protected PropertyGrid (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
        private void Initialize (Context context, IAttributeSet? attrs)
        {
            m_controller = new PropertyGridController(this);
            m_controller.PropertyTreeChanged += this.OnControllerPropertyTreeChanged;

            m_tree = new FlatTreeListView(context)
            {
                SelectionMode = eFlatTreeSelectionMode.Single,
                IsDragDropReorderEnabled = true,
                TreeDepthIndentSize = MobileLook.PropertyGrid.TreeDepthIndentSize,
                CanDragItem = this.CanDragRow,
                CanDropItem = PropertyGridController.CanDropItem,
                RowContentFactory = this.CreateRowContent,
                RowMenuFactory = this.FillRowMenu,
                Look = new FlatTreeListView.RowLook(
                    SelectionFill: null,
                    SelectionAccent: RowColors.SelectionAccent,
                    PressedFill: RowColors.PressedFill,
                    PressedBorder: RowColors.PressedBorder
                ),
            };

            this.AddView(m_tree, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutPropertyGrid);
                this.IsReadOnly = values.GetBoolean(Resource.Styleable.AJutPropertyGrid_ajutIsReadOnly, false);
                this.Enabled = values.GetBoolean(Resource.Styleable.AJutPropertyGrid_android_enabled, true);
                values.Recycle();
            }
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
        protected override void Dispose (bool disposing)
        {
            if (disposing && !m_isDisposed && (m_controller != null))
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
        public event EventHandler<EventArgs>? PropertyTreeChanged;

        /// <summary>Raised whenever the selected row changes</summary>
        public event EventHandler<EventArgs>? SelectedTargetChanged;

        /// <summary>
        /// Raised when the source object behind the selected row changes. Moving between two rows inside the same list
        /// element's subtree resolves to the same element, so it does not fire then.
        /// </summary>
        public event EventHandler<EventArgs>? SelectedSourceObjectChanged;

        // In a window: catch up, give the tree the rows again with the selection as it was, then listen again, so putting
        // the rows back raises no selection events of its own (see the class comment)
        protected override void OnAttachedToWindow ()
        {
            base.OnAttachedToWindow();

            // A peer made again for a Java view that outlived its disposed one has none of the grid's state
            if (m_isDisposed || (m_controller == null))
            {
                return;
            }

            m_controller.OnAttached();
            this.ShowRootItemsInTree();
            if (m_selectedWhenDetached != null)
            {
                m_tree.TrySelectItemForSource(m_selectedWhenDetached, scrollIntoView: false);
                m_selectedWhenDetached = null;
            }

            this.ListenToTree(true);
        }

        // Out of a window: drop the source to grid subscription, stop listening to the tree, and take the rows out of it.
        // The selection is read first, while the tree still has it.
        protected override void OnDetachedFromWindow ()
        {
            if (m_isDisposed || (m_controller == null))
            {
                base.OnDetachedFromWindow();
                return;
            }

            m_selectedWhenDetached = this.SelectedTarget;
            this.ListenToTree(false);
            m_controller.OnDetached();
            m_tree.RootItemsSource = null;
            base.OnDetachedFromWindow();
        }

        private void OnControllerPropertyTreeChanged (object? sender, EventArgs e)
        {
            this.PropertyTreeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnTreeSelectionChanged (object? sender, SelectionChange<FlatTreeItem> e)
        {
            this.SelectedTargetChanged?.Invoke(this, EventArgs.Empty);
            if (m_controller.OnSelectedTargetChanged(this.SelectedTarget))
            {
                this.SelectedSourceObjectChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
        private void OnTreeDragDropReorderRequested (object? sender, FlatTreeReorderEventArgs e)
        {
            PropertyGridController.TryHandleReorder(e);
        }

        // ===========[ Properties ]============================================
        /// <summary>One object to edit, or null. Setting it clears <see cref="ItemsSource"/>.</summary>
        [AllowNull]
        public object SingleItemSource
        {
            get => m_singleItemSource!;

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

        /// <summary>Several objects to edit together, or null. An observable collection is followed. Setting it clears <see cref="SingleItemSource"/>.</summary>
        [AllowNull]
        public IEnumerable ItemsSource
        {
            get => m_itemsSource!;

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
        /// expands, collapses, selects, or scrolls, and each editor shows its own disabled look. A ViewGroup's Enabled
        /// never reaches the editors in its rows, so the grid passes it on itself. Changing it builds the shown rows again.
        /// </summary>
        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;
                if (m_tree != null)
                {
                    m_tree.Enabled = value;
                }
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

        /// <summary>dp of indent per depth</summary>
        public double TreeDepthIndentSize
        {
            get => m_tree.TreeDepthIndentSize;
            set => m_tree.TreeDepthIndentSize = value;
        }

        /// <summary>Space between rows, in dp</summary>
        public double RowSpacing
        {
            get => m_tree.RowSpacing;
            set => m_tree.RowSpacing = value;
        }

        /// <summary>A fixed row height in dp, or NaN (the default) for rows sized to their name and editor</summary>
        public double FixedRowHeight
        {
            get => m_tree.FixedRowHeight;
            set => m_tree.FixedRowHeight = value;
        }

        /// <summary>Which of a row's name and editor TalkBack reads the property's tooltip text on, as its hint; on its name by default</summary>
        public ePropertyToolTipPlacement PropertyToolTipPlacement { get; set; } = ePropertyToolTipPlacement.PropertyNameOnly;

        /// <summary>The element count text on a list row; {0} is the count</summary>
        public string ListElementCountFormat { get; set; } = "Elements ({0})";

        /// <summary>The row selected now, or null. Setting it selects that row, expanding its ancestors, and scrolls to it.</summary>
        public PropertyEditTarget? SelectedTarget
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
        public object? SelectedSourceObject
        {
            [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
            [UnconditionalSuppressMessage("AOT", "IL3050", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
            get => PropertyGridController.ResolveSourceObject(this.SelectedTarget!);
        }

        // A row takes no edits, runs no button, and offers no add, remove, or reset while the grid is read-only or disabled
        private bool IsLocked => this.IsReadOnly || !this.Enabled;

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
        public bool TrySelectSourceObject (object? sourceObject)
        {
            if (sourceObject == null)
            {
                return false;
            }

            PropertyEditTarget? match = m_controller.FindTargetForSourceObject(sourceObject);
            return (match != null) && m_tree.TrySelectItemForSource(match, scrollIntoView: true);
        }

        // ===========[ IPropertyGridHost Implementations ]=====================
        // Out of a window the tree is given nothing (see the class comment); coming back gives it the controller's rows
        void IPropertyGridHost.ShowRootItems (IReadOnlyList<IObservableTreeNode>? rootItems)
        {
            if (this.IsAttachedToWindow)
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

        private View CreateRowContent (FlatTreeItem item) => new RowView(this, item);

        // "Set to default", where it applies. A row that can be dragged has none, since its long press lifts it to drag.
        private void FillRowMenu (FlatTreeItem item, IMenu menu)
        {
            if ((item.Source is not PropertyEditTarget target)
                || !PropertyGridRowRules.CanResetToDefault(target, this.IsLocked)
                || this.CanDragRow(target))
            {
                return;
            }

            menu.Add("Set to default")!.SetOnMenuItemClickListener(new ResetAction(target));
        }

        // The editor for a target: a registered one wins, then a built-in one, then Default. A button row or a list header
        // has no setter of its own, so those follow the grid's read-only (or disabled) state alone, and value editors get
        // CanEdit's answer.
        internal View? CreateEditor (PropertyEditTarget target, BindingSet bindings, out ePropertyEditorPlacement placement)
        {
            Context context = this.Context!;
            bool isReadOnly = !PropertyGridRowRules.CanEdit(target, this.IsLocked);
            string key = PropertyGridRowRules.GetEditorKey(target);
            placement = this.Editors.GetPlacement(key);
            if (this.Editors.TryGet(key, out PropertyEditorFactory? factory))
            {
                return factory(context, target, bindings, isReadOnly);
            }

            switch (PropertyGridRowRules.GetBuiltInEditor(target, this.Editors.IsRegistered))
            {
                case eBuiltInPropertyEditor.Nullable:
                    return new NullableEditorView(this, target, bindings, isReadOnly);

                case eBuiltInPropertyEditor.Button:
                    placement = ePropertyEditorPlacement.BelowLabel;
                    return CreateButtonEditor(context, target, bindings, this.IsLocked);

                case eBuiltInPropertyEditor.List:
                    placement = ePropertyEditorPlacement.BesideLabel;
                    return this.CreateListEditor(context, target, bindings);
            }

            return this.Editors.Default?.Invoke(context, target, bindings, isReadOnly);
        }

        private static View CreateButtonEditor (Context context, PropertyEditTarget target, BindingSet bindings, bool isLocked)
        {
            // Themed, the same face as a stock button beside the grid; unthemed, the stock button
            var button = new Button(context) { Text = target.EditValue?.ToString() ?? target.DisplayName ?? string.Empty };
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

        private View CreateListEditor (Context context, PropertyEditTarget target, BindingSet bindings)
        {
            var row = new LinearLayout(context) { Orientation = Android.Widget.Orientation.Horizontal };
            row.SetGravity(GravityFlags.CenterVertical);

            TextView count = RowView.CreateListText(context);
            row.AddView(count);
            if (target.EditContext is PropertyGridListContext listContext)
            {
                string format = this.ListElementCountFormat;
                bindings.BindLabel(count, listContext, nameof(PropertyGridListContext.ElementCount), _ReadCount);
                if (listContext.CanAdd)
                {
                    Button add = CreateAddButton(context);
                    if (this.IsLocked)
                    {
                        add.Enabled = false;
                    }
                    else
                    {
                        bindings.BindCommand(add, listContext.AddCommand);
                    }

                    row.AddView(add);
                }

                string _ReadCount () => string.Format(format, listContext.ElementCount);
            }

            return row;
        }

        // A list's add button: a plus in an outlined circle, in the accent, centered in a full touch target, as iOS draws
        // its add button. A bare "+" in a borderless button read as more text beside the element count. Disabled, the
        // plus and the circle both dim.
        private static Button CreateAddButton (Context context)
        {
            int touchSize = RowView.ToPixels(context, MobileLook.PropertyGrid.RemoveButtonSize);
            int diameter = RowView.ToPixels(context, MobileLook.PropertyGrid.AddButtonDiameter);
            int outline = Math.Max(1, RowView.ToPixels(context, MobileLook.PropertyGrid.AddButtonOutlineThickness));
            int inset = Math.Max(0, (touchSize - diameter) / 2);

            var colors = new ColorStateList(
                new[] { new[] { -Android.Resource.Attribute.StateEnabled }, Array.Empty<int>() },
                new[] { ThemeColors.Resolve(context, RowColors.ActionDisabled).ToAndroidColor().ToArgb(), ThemeColors.Resolve(context, RowColors.Action).ToAndroidColor().ToArgb() }
            );

            var circle = new Android.Graphics.Drawables.GradientDrawable();
            circle.SetShape(Android.Graphics.Drawables.ShapeType.Oval);
            circle.SetColor(Android.Graphics.Color.Transparent);
            circle.SetStroke(outline, colors);

            // The ripple fills the circle. With no mask layer a RippleDrawable is masked by what its content draws, which
            // here is only the outline, so a solid circle of the same size is its mask; a mask's color never shows.
            var mask = new Android.Graphics.Drawables.GradientDrawable();
            mask.SetShape(Android.Graphics.Drawables.ShapeType.Oval);
            mask.SetColor(Android.Graphics.Color.White);
            var ripple = new Android.Graphics.Drawables.RippleDrawable(
                colors.WithAlpha((int)Math.Round(byte.MaxValue * MobileLook.PropertyGrid.AddButtonRippleOpacity)),
                new Android.Graphics.Drawables.InsetDrawable(circle, inset),
                new Android.Graphics.Drawables.InsetDrawable(mask, inset)
            );

            var add = new Button(context)
            {
                Text = "+",
                ContentDescription = "Add",
                Background = ripple,
                Gravity = GravityFlags.Center,
                StateListAnimator = null,
            };

            add.SetTextColor(colors);
            add.SetTextSize(ComplexUnitType.Sp, MobileLook.PropertyGrid.AddButtonGlyphSize);
            add.SetIncludeFontPadding(false);
            add.SetPadding(0, 0, 0, 0);
            add.SetMinWidth(0);
            add.SetMinHeight(0);
            add.SetMinimumWidth(touchSize);
            add.SetMinimumHeight(touchSize);
            add.LayoutParameters = new LinearLayout.LayoutParams(touchSize, touchSize);
            return add;
        }

        // ===========[ Subclasses/structs ]====================================
        // A menu item's click handler, holding only the target it resets
        private sealed class ResetAction : Java.Lang.Object, IMenuItemOnMenuItemClickListener
        {
            private readonly PropertyEditTarget m_target;

            public ResetAction (PropertyEditTarget target)
            {
                m_target = target;
            }

            public bool OnMenuItemClick (IMenuItem item)
            {
                m_target.ResetToDefault();
                return true;
            }
        }
    }
}
