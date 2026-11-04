namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using System.ComponentModel;
    using Windows.Foundation;
    using AJut.UX;
    using AJut.UX.PropertyInteraction;
    using DPUtils = AJut.UX.DPUtils<PropertyGridItemRow>;

    // ===========[ PropertyGridItemRow ]=======================================
    // Per-property row presenter used internally by PropertyGrid.
    //
    // DataContext is FlatTreeItem (set by FlatTreeListControl's ListView).
    // The row template binds to FlatTreeItem properties directly for the label.
    // PART_EditorContent.Content is set from code (not via a XAML binding) so
    // that the stale DataContext-propagation timing in WinUI3 cannot override
    // our programmatic assignment with old data.
    //
    // EditorTemplateSelector is resolved on Loaded by walking up the visual
    // tree to find the enclosing PropertyGrid.
    //
    // LabelColumnWidth is read from the enclosing PropertyGrid on Loaded, and pushed by the grid
    // when it changes after that (its label splitter changes it).
    // ApplyLabelColumnWidth() computes the PART_LabelBorder.Width by
    // subtracting FlatTreeItem.IndentWidth and kExpanderColumnWidth.
    //
    // DefaultValueLabelDataTemplate / ModifiedValueLabelDataTemplate are read
    // from the enclosing PropertyGrid on Loaded. PART_LabelContent.ContentTemplate
    // switches between them based on PropertyEditTarget.IsAtDefaultValue.
    //
    // Right-tapping PART_LabelBorder shows a context menu with "Set to default"
    // when the target HasDefaultValue.

    [TemplatePart(Name = nameof(PART_LabelBorder), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_LabelContent), Type = typeof(ContentControl))]
    [TemplatePart(Name = nameof(PART_SubtitleText), Type = typeof(TextBlock))]
    [TemplatePart(Name = nameof(PART_EditorContent), Type = typeof(ContentControl))]
    [TemplatePart(Name = nameof(PART_ListElementHeader), Type = typeof(TextBlock))]
    [TemplatePart(Name = nameof(PART_DeleteButton), Type = typeof(Button))]
    public class PropertyGridItemRow : Control
    {
        // ===========[ Statics ]==========================================
        private const double kExpanderColumnWidth = 18.0;

        // ===========[ Instance fields ]==========================================
        private FlatTreeItem m_flatTreeItem;
        private PropertyEditTarget m_editTarget;

        // Bumped on every DataContext change so a deferred ApplyEditorContent callback can tell
        // whether it is still the most recent one without capturing (and thus pinning) the target.
        private int m_dataContextRevision;

        // ===========[ Construction ]=============================================
        public PropertyGridItemRow ()
        {
            this.DefaultStyleKey = typeof(PropertyGridItemRow);
            this.Loaded += this.OnLoaded;
            this.DataContextChanged += this.OnDataContextChanged;
        }

        // ===========[ Template parts ]==========================================
        private Border PART_LabelBorder { get; set; }
        private ContentControl PART_LabelContent { get; set; }
        private TextBlock PART_SubtitleText { get; set; }
        private ContentControl PART_EditorContent { get; set; }
        private TextBlock PART_ListElementHeader { get; set; }
        private Button PART_DeleteButton { get; set; }

        // ===========[ Dependency Properties ]====================================
        public static readonly DependencyProperty EditorTemplateSelectorProperty = DPUtils.Register(_ => _.EditorTemplateSelector);
        public DataTemplateSelector EditorTemplateSelector
        {
            get => (DataTemplateSelector)this.GetValue(EditorTemplateSelectorProperty);
            set => this.SetValue(EditorTemplateSelectorProperty, value);
        }

        public static readonly DependencyProperty LabelColumnWidthProperty = DPUtils.Register(_ => _.LabelColumnWidth, double.NaN, (d, e) => d.ApplyLabelColumnWidth());
        public double LabelColumnWidth
        {
            get => (double)this.GetValue(LabelColumnWidthProperty);
            set => this.SetValue(LabelColumnWidthProperty, value);
        }

        /// <summary>
        /// Mirrors the enclosing PropertyGrid's IsReadOnly: while true the editor is disabled (button rows and a list's
        /// add button with it), the remove button is hidden and "Set to default" isn't offered
        /// </summary>
        public static readonly DependencyProperty IsGridReadOnlyProperty = DPUtils.Register(_ => _.IsGridReadOnly, false, (d, e) => d.ApplyReadOnly());
        public bool IsGridReadOnly
        {
            get => (bool)this.GetValue(IsGridReadOnlyProperty);
            set => this.SetValue(IsGridReadOnlyProperty, value);
        }

        public static readonly DependencyProperty DefaultValueLabelDataTemplateProperty = DPUtils.Register(_ => _.DefaultValueLabelDataTemplate, (d, e) => d.ApplyLabelTemplate());
        public DataTemplate DefaultValueLabelDataTemplate
        {
            get => (DataTemplate)this.GetValue(DefaultValueLabelDataTemplateProperty);
            set => this.SetValue(DefaultValueLabelDataTemplateProperty, value);
        }

        public static readonly DependencyProperty ModifiedValueLabelDataTemplateProperty = DPUtils.Register(_ => _.ModifiedValueLabelDataTemplate, (d, e) => d.ApplyLabelTemplate());
        public DataTemplate ModifiedValueLabelDataTemplate
        {
            get => (DataTemplate)this.GetValue(ModifiedValueLabelDataTemplateProperty);
            set => this.SetValue(ModifiedValueLabelDataTemplateProperty, value);
        }

        public static readonly DependencyProperty LabelSubtitleStyleProperty = DPUtils.Register(_ => _.LabelSubtitleStyle, (d, e) => d.ApplySubtitle());
        public Style LabelSubtitleStyle
        {
            get => (Style)this.GetValue(LabelSubtitleStyleProperty);
            set => this.SetValue(LabelSubtitleStyleProperty, value);
        }

        public static readonly DependencyProperty PropertyToolTipPlacementProperty = DPUtils.Register(_ => _.PropertyToolTipPlacement, ePropertyToolTipPlacement.PropertyNameOnly, (d, e) => d.ApplyToolTips());
        public ePropertyToolTipPlacement PropertyToolTipPlacement
        {
            get => (ePropertyToolTipPlacement)this.GetValue(PropertyToolTipPlacementProperty);
            set => this.SetValue(PropertyToolTipPlacementProperty, value);
        }

        // ===========[ Label splitter support ]==================================
        /// <summary>
        /// The boundary between this row's label and its editor, as an x in <paramref name="relativeTo"/>'s coordinates
        /// (the middle of the gap between them), and the label column width this row shows. False until the row has a
        /// template and an item.
        /// </summary>
        internal bool TryGetLabelBoundary (UIElement relativeTo, out double boundaryX, out double shownLabelColumnWidth)
        {
            if (this.PART_LabelBorder == null || m_flatTreeItem == null)
            {
                boundaryX = 0.0;
                shownLabelColumnWidth = double.NaN;
                return false;
            }

            var gapMiddle = new Point(this.PART_LabelBorder.ActualWidth + (this.PART_LabelBorder.Margin.Right / 2), 0);
            boundaryX = this.PART_LabelBorder.TransformToVisual(relativeTo).TransformPoint(gapMiddle).X;
            shownLabelColumnWidth = PropertyGridRowRules.ToLabelColumnWidth(this.PART_LabelBorder.ActualWidth, m_flatTreeItem.IndentWidth, kExpanderColumnWidth);
            return true;
        }

        /// <summary>The label column width that would show this row's whole label. False until the row has a template and an item.</summary>
        internal bool TryMeasureFittingLabelColumnWidth (out double labelColumnWidth)
        {
            if (this.PART_LabelBorder?.Child == null || m_flatTreeItem == null)
            {
                labelColumnWidth = double.NaN;
                return false;
            }

            // The label border's width is set, so its own desired width is just that; what its content wants is
            // measured unconstrained instead. The content is then marked for a normal measure again, or it keeps
            // this one when the fit leaves the width as it was.
            UIElement content = this.PART_LabelBorder.Child;
            content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Thickness border = this.PART_LabelBorder.BorderThickness;
            Thickness padding = this.PART_LabelBorder.Padding;
            double labelWidth = content.DesiredSize.Width + border.Left + border.Right + padding.Left + padding.Right;
            content.InvalidateMeasure();

            labelColumnWidth = PropertyGridRowRules.ToLabelColumnWidth(labelWidth, m_flatTreeItem.IndentWidth, kExpanderColumnWidth);
            return true;
        }

        // ===========[ Pointer overrides - drive HoverStates VSM group ]=========
        protected override void OnPointerEntered (PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);
            VisualStateManager.GoToState(this, "Hovered", true);
        }

        protected override void OnPointerExited (PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);
            VisualStateManager.GoToState(this, "NotHovered", true);
        }

        // ===========[ Template application ]====================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_LabelBorder != null)
            {
                this.PART_LabelBorder.RightTapped -= this.OnLabelBorderRightTapped;
            }

            if (this.PART_DeleteButton != null)
            {
                this.PART_DeleteButton.Click -= this.OnDeleteButtonClick;
            }

            this.PART_LabelBorder = this.GetTemplateChild(nameof(PART_LabelBorder)) as Border;
            this.PART_LabelContent = this.GetTemplateChild(nameof(PART_LabelContent)) as ContentControl;
            this.PART_SubtitleText = this.GetTemplateChild(nameof(PART_SubtitleText)) as TextBlock;
            this.PART_EditorContent = this.GetTemplateChild(nameof(PART_EditorContent)) as ContentControl;
            this.PART_ListElementHeader = this.GetTemplateChild(nameof(PART_ListElementHeader)) as TextBlock;
            this.PART_DeleteButton = this.GetTemplateChild(nameof(PART_DeleteButton)) as Button;

            if (this.PART_LabelBorder != null)
            {
                this.PART_LabelBorder.RightTapped += this.OnLabelBorderRightTapped;
            }

            if (this.PART_DeleteButton != null)
            {
                this.PART_DeleteButton.Click += this.OnDeleteButtonClick;
            }

            this.ApplyLabelColumnWidth();
            this.ApplyLabelTemplate();
            this.ApplySubtitle();
            this.ApplyDeleteButton();
            this.ApplyListElementHeader();
            this.ApplyToolTips();
            this.ApplyReadOnly();

            if (m_flatTreeItem != null)
            {
                VisualStateManager.GoToState(this, m_flatTreeItem.IsSelected ? "Selected" : "Normal", false);
            }
        }

        // ===========[ Events ]===================================================
        private void OnLoaded (object sender, RoutedEventArgs e)
        {
            // Walk up the visual tree to find the enclosing PropertyGrid.
            DependencyObject current = VisualTreeHelper.GetParent(this);
            while (current != null)
            {
                if (current is PropertyGrid pg)
                {
                    if (this.EditorTemplateSelector == null)
                    {
                        this.EditorTemplateSelector = pg.ItemTemplateSelector;
                    }

                    if (double.IsNaN(this.LabelColumnWidth))
                    {
                        this.LabelColumnWidth = pg.LabelColumnWidth;
                    }

                    if (this.DefaultValueLabelDataTemplate == null)
                    {
                        this.DefaultValueLabelDataTemplate = pg.DefaultValueLabelDataTemplate;
                    }

                    if (this.ModifiedValueLabelDataTemplate == null)
                    {
                        this.ModifiedValueLabelDataTemplate = pg.ModifiedValueLabelDataTemplate;
                    }

                    if (this.LabelSubtitleStyle == null)
                    {
                        this.LabelSubtitleStyle = pg.LabelSubtitleStyle;
                    }

                    // Tooltip placement and read-only are grid-global - always mirror the enclosing PropertyGrid.
                    this.PropertyToolTipPlacement = pg.PropertyToolTipPlacement;
                    this.IsGridReadOnly = pg.IsReadOnly;

                    // Apply ElementPadding from PropertyGrid as this row's Padding so the
                    // template's {TemplateBinding Padding} insets the content from the edges.
                    this.Padding = pg.ElementPadding;

                    return;
                }

                current = VisualTreeHelper.GetParent(current);
            }
        }

        private void OnDataContextChanged (FrameworkElement sender, DataContextChangedEventArgs e)
        {
            if (m_flatTreeItem != null)
            {
                m_flatTreeItem.IsSelectedChanged -= this.OnIsSelectedChanged;
                m_flatTreeItem.PropertyChanged -= this.OnFlatTreeItemPropertyChanged;
            }

            if (m_editTarget != null)
            {
                m_editTarget.PropertyChanged -= this.OnEditTargetPropertyChanged;
            }

            m_flatTreeItem = e.NewValue as FlatTreeItem;
            m_editTarget = m_flatTreeItem?.Source as PropertyEditTarget;

            VisualStateManager.GoToState(this, "NotHovered", false);

            if (m_flatTreeItem != null)
            {
                m_flatTreeItem.IsSelectedChanged += this.OnIsSelectedChanged;
                m_flatTreeItem.PropertyChanged += this.OnFlatTreeItemPropertyChanged;
                VisualStateManager.GoToState(this, m_flatTreeItem.IsSelected ? "Selected" : "Normal", false);
                this.ApplyLabelColumnWidth();
            }
            else
            {
                VisualStateManager.GoToState(this, "Normal", false);
            }

            if (m_editTarget != null)
            {
                m_editTarget.PropertyChanged += this.OnEditTargetPropertyChanged;
            }

            // Defer to the next dispatcher frame so that:
            // (a) OnApplyTemplate has fired and PART_EditorContent is available, and
            // (b) OnLoaded has fired and EditorTemplateSelector has been resolved from
            //     the enclosing PropertyGrid.
            // Without deferral, DataContextChanged fires before template application on
            // newly created rows, making PART_EditorContent null at call time.
            // Guard with a revision token rather than capturing m_editTarget: capturing the
            // target would let this queued callback hold the target (and through it the source
            // object) alive until the dispatcher pumps. Harmless in a live app, but it makes a
            // disposed grid's source look pinned to a leak probe that GCs before the queue drains.
            int revision = ++m_dataContextRevision;
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (m_dataContextRevision == revision)
                {
                    this.ApplyEditorContent();
                }
            });

            this.ApplyLabelTemplate();
            this.ApplySubtitle();
            this.ApplyListElementHeader();
            this.ApplyToolTips();

            // Whether the row's own property can be set comes with its target, so the editor area is worked out again
            // (and the delete button with it)
            this.ApplyReadOnly();
        }

        private void OnIsSelectedChanged (object sender, System.EventArgs e)
        {
            VisualStateManager.GoToState(this, m_flatTreeItem?.IsSelected == true ? "Selected" : "Normal", false);
        }

        private void OnFlatTreeItemPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FlatTreeItem.IndentWidth))
            {
                this.ApplyLabelColumnWidth();
            }
        }

        private void OnEditTargetPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PropertyEditTarget.IsAtDefaultValue))
            {
                this.ApplyLabelTemplate();
            }
            else if (e.PropertyName == nameof(PropertyEditTarget.ToolTip)
                || e.PropertyName == nameof(PropertyEditTarget.DisplayName))
            {
                this.ApplyToolTips();
            }
            else if (e.PropertyName == nameof(PropertyEditTarget.IsListElementHeaderVisible)
                || e.PropertyName == nameof(PropertyEditTarget.ListElementHeaderText))
            {
                this.ApplyListElementHeader();
            }
        }

        private void OnLabelBorderRightTapped (object sender, RightTappedRoutedEventArgs e)
        {
            if (m_editTarget == null || !PropertyGridRowRules.CanResetToDefault(m_editTarget, this.IsGridReadOnly))
            {
                return;
            }

            var flyout = new MenuFlyout();
            var item = new MenuFlyoutItem { Text = "Set to default" };
            item.Click += this.OnSetToDefaultClicked;
            flyout.Items.Add(item);
            flyout.ShowAt(this.PART_LabelBorder, new FlyoutShowOptions { Position = e.GetPosition(this.PART_LabelBorder) });
            e.Handled = true;
        }

        private void OnSetToDefaultClicked (object sender, RoutedEventArgs e)
        {
            m_editTarget?.ResetToDefault();
        }

        private void OnDeleteButtonClick (object sender, RoutedEventArgs e)
        {
            m_editTarget?.ListElementRemoveCommand?.Execute(null);
        }

        // ===========[ Private helpers ]===========================================
        private void ApplyEditorContent ()
        {
            if (this.PART_EditorContent == null)
            {
                return;
            }

            // Force a full Content cycle: null -> target guarantees ContentControl destroys
            // and recreates the visual tree, so WinUI3 calls SelectTemplate with the new
            // PropertyEditTarget even when switching between same-type properties (where
            // the selector returns the same DataTemplate object and WinUI3 would otherwise
            // reuse the old visual tree with a stale DataContext).
            var target = m_editTarget?.EffectiveEditorTarget;
            this.PART_EditorContent.Content = null;
            this.PART_EditorContent.Content = target;
        }

        private void ApplyLabelColumnWidth ()
        {
            if (this.PART_LabelBorder == null)
            {
                return;
            }

            if (m_flatTreeItem == null)
            {
                this.PART_LabelBorder.Width = double.NaN;
                return;
            }

            this.PART_LabelBorder.Width = PropertyGridRowRules.ComputeLabelWidth(this.LabelColumnWidth, m_flatTreeItem.IndentWidth, kExpanderColumnWidth);
        }

        private void ApplySubtitle ()
        {
            if (this.PART_SubtitleText == null)
            {
                return;
            }

            string subtitle = m_editTarget?.Subtitle;
            bool hasSubtitle = !string.IsNullOrEmpty(subtitle);
            this.PART_SubtitleText.Text = subtitle ?? string.Empty;
            this.PART_SubtitleText.Visibility = hasSubtitle ? Visibility.Visible : Visibility.Collapsed;

            if (this.LabelSubtitleStyle != null)
            {
                this.PART_SubtitleText.Style = this.LabelSubtitleStyle;
            }
        }

        private void ApplyDeleteButton ()
        {
            if (this.PART_DeleteButton == null)
            {
                return;
            }

            this.PART_DeleteButton.Visibility = (m_editTarget != null) && PropertyGridRowRules.CanRemoveFromList(m_editTarget, this.IsGridReadOnly)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // The whole editor area is disabled rather than each editor told, since the editors are the
        // consumer's templates; IsEnabled flows down to every control in it, and a disabled editor looks it.
        // A value row whose property has no setter (a get-only counter) is disabled too, as on the mobile
        // stacks, since nothing typed into it could be kept: an editor that can't be used should look it. A
        // button row and a list's row are left to the grid's read-only alone, since they run a command or add
        // to the list rather than set their property, and so have no setter either.
        private void ApplyReadOnly ()
        {
            if (this.PART_EditorContent != null)
            {
                this.PART_EditorContent.IsEnabled = this.IsEditorAreaEnabled();
            }

            this.ApplyDeleteButton();
        }

        private bool IsEditorAreaEnabled ()
        {
            if (this.IsGridReadOnly)
            {
                return false;
            }

            if (m_editTarget == null)
            {
                return true;
            }

            bool setsItsProperty = (m_editTarget.Editor != PropertyGridRowRules.kButtonEditorKey)
                && !(m_editTarget.EditContext is PropertyGridListContext);
            return !setsItsProperty || !m_editTarget.IsReadOnly;
        }

        private void ApplyListElementHeader ()
        {
            if (this.PART_ListElementHeader == null)
            {
                return;
            }

            bool visible = m_editTarget?.IsListElementHeaderVisible == true;
            this.PART_ListElementHeader.Text = visible ? (m_editTarget.ListElementHeaderText ?? string.Empty) : string.Empty;
            this.PART_ListElementHeader.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }


        private void ApplyToolTips ()
        {
            string text = m_editTarget?.EffectiveToolTip;
            ePropertyToolTipPlacement placement = this.PropertyToolTipPlacement;

            if (this.PART_LabelContent != null)
            {
                ToolTipService.SetToolTip(this.PART_LabelContent, PropertyGridRowRules.ShowsToolTipOnName(placement) ? text : null);
            }

            if (this.PART_EditorContent != null)
            {
                ToolTipService.SetToolTip(this.PART_EditorContent, PropertyGridRowRules.ShowsToolTipOnValue(placement) ? text : null);
            }
        }

        private void ApplyLabelTemplate ()
        {
            if (this.PART_LabelContent == null)
            {
                return;
            }

            // Expandable sub-object rows always get the default-value look; see UsesDefaultValueLabel
            this.PART_LabelContent.ContentTemplate = PropertyGridRowRules.UsesDefaultValueLabel(m_editTarget)
                ? this.DefaultValueLabelDataTemplate
                : this.ModifiedValueLabelDataTemplate;
        }
    }
}
