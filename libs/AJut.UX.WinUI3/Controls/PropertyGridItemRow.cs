namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using System.ComponentModel;
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
    // LabelColumnWidth is read from the enclosing PropertyGrid on Loaded.
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

                    // Tooltip placement is grid-global - always mirror the enclosing PropertyGrid.
                    this.PropertyToolTipPlacement = pg.PropertyToolTipPlacement;

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
            this.ApplyDeleteButton();
            this.ApplyListElementHeader();
            this.ApplyToolTips();
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
            if (m_editTarget == null || !m_editTarget.HasDefaultValue || m_editTarget.IsExpandable)
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

            // Force a full Content cycle: null → target guarantees ContentControl destroys
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

            if (double.IsNaN(this.LabelColumnWidth) || m_flatTreeItem == null)
            {
                this.PART_LabelBorder.Width = double.NaN;
                return;
            }

            double available = this.LabelColumnWidth - m_flatTreeItem.IndentWidth - kExpanderColumnWidth;
            this.PART_LabelBorder.Width = available > 0 ? available : 0;
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

            this.PART_DeleteButton.Visibility = (m_editTarget?.CanRemoveFromList == true)
                ? Visibility.Visible
                : Visibility.Collapsed;
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
                bool showOnName = placement == ePropertyToolTipPlacement.PropertyNameOnly
                    || placement == ePropertyToolTipPlacement.PropertyNameAndValue;
                ToolTipService.SetToolTip(this.PART_LabelContent, showOnName ? text : null);
            }

            if (this.PART_EditorContent != null)
            {
                bool showOnValue = placement == ePropertyToolTipPlacement.ValueOnly
                    || placement == ePropertyToolTipPlacement.PropertyNameAndValue;
                ToolTipService.SetToolTip(this.PART_EditorContent, showOnValue ? text : null);
            }
        }

        private void ApplyLabelTemplate ()
        {
            if (this.PART_LabelContent == null)
            {
                return;
            }

            // Expandable parent nodes (complex reference-type sub-objects) always use the
            // default-value template - their "value" is the object reference which is never
            // meaningfully compared against a default, and the bold/modified look is misleading.
            bool isAtDefault = (m_editTarget?.IsAtDefaultValue ?? true) || (m_editTarget?.IsExpandable == true);
            this.PART_LabelContent.ContentTemplate = isAtDefault
                ? this.DefaultValueLabelDataTemplate
                : this.ModifiedValueLabelDataTemplate;
        }
    }
}
