namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using System;
    using DPUtils = AJut.UX.DPUtils<DockLeafLayout>;

    // ===========[ DockLeafLayout ]=============================================
    // WinUI3-specific: no WPF equivalent.
    // Leaf layout for DockZone (Single and Tabbed orientations). Defines the
    // 3-row structure — header bar / content panel / tab strip — entirely in
    // XAML so that all visual properties (backgrounds, borders, corner radius,
    // padding) live in the template rather than in DockZone code-behind.
    //
    // Tab strip overlap: this.PART_TabStripWrapper uses Margin="0,-1,0,0" so its
    // top border coincides with the content panel's bottom edge. Grid does NOT
    // clip its children, so this -1px offset is visible (unlike inside a
    // ScrollViewer). The content panel suppresses its bottom border when tabbed
    // so only the tab strip's top border marks that boundary.
    //
    // Template parts:
    //   this.PART_HeaderBar        — Border hosting the panel title / close button
    //   this.PART_ContentBorder    — Border framing the docked content presenter
    //   this.PART_ContentPresenter — ContentPresenter for PanelContent
    //   this.PART_TabStripWrapper  — Border hosting the tab navigation row
    //   this.PART_TabNavPresenter  — ContentPresenter for TabNavContent
    //
    // WinUI3 TemplateBinding does not backfill values set before template
    // application, so all visual DPs push their values directly to template
    // parts in both OnApplyTemplate (data-before-template) and each DP change
    // handler (template-before-data).

    [TemplatePart(Name = nameof(PART_HeaderBar), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_ContentBorder), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_ContentPresenter), Type = typeof(ContentPresenter))]
    [TemplatePart(Name = nameof(PART_TabStripWrapper), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_TabNavPresenter), Type = typeof(ContentPresenter))]
    public sealed class DockLeafLayout : Control
    {
        // ===========[ Construction ]=========================================
        public DockLeafLayout()
        {
            this.DefaultStyleKey = typeof(DockLeafLayout);
        }

        // ===========[ Header pointer events ]================================
        // Forwarded from this.PART_HeaderBar so DockZone can subscribe before the
        // template is applied, without coupling to the internal Border reference.
        public event PointerEventHandler HeaderPointerPressed;
        public event PointerEventHandler HeaderPointerMoved;
        public event PointerEventHandler HeaderPointerReleased;
        public event PointerEventHandler HeaderPointerCaptureLost;

        // ===========[ Template Properties ]======================

        public Border PART_HeaderBar { get; private set; }
        public Border PART_ContentBorder { get; private set; }
        public ContentPresenter PART_ContentPresenter { get; private set; }
        public Border PART_TabStripWrapper { get; private set; }
        public ContentPresenter PART_TabNavPresenter { get; private set; }

        // ===========[ Dependency Properties — Styling ]======================

        public static readonly DependencyProperty PanelBackgroundProperty = DPUtils.Register(_ => _.PanelBackground, (d, e) => d.ApplyBackground());
        public Brush PanelBackground
        {
            get => (Brush)this.GetValue(PanelBackgroundProperty);
            set => this.SetValue(PanelBackgroundProperty, value);
        }

        public static readonly DependencyProperty PanelBorderBrushProperty = DPUtils.Register(_ => _.PanelBorderBrush, (d, e) => d.ApplyBorderBrush());
        public Brush PanelBorderBrush
        {
            get => (Brush)this.GetValue(PanelBorderBrushProperty);
            set => this.SetValue(PanelBorderBrushProperty, value);
        }

        public static readonly DependencyProperty PanelBorderThicknessProperty = DPUtils.Register(_ => _.PanelBorderThickness, (d, e) => d.ApplyBorderThickness());
        public Thickness PanelBorderThickness
        {
            get => (Thickness)this.GetValue(PanelBorderThicknessProperty);
            set => this.SetValue(PanelBorderThicknessProperty, value);
        }

        public static readonly DependencyProperty PanelCornerRadiusProperty = DPUtils.Register(_ => _.PanelCornerRadius, (d,e)=>d.OnPanelCornerRadiusChanged(e));
        public CornerRadius PanelCornerRadius
        {
            get => (CornerRadius)this.GetValue(PanelCornerRadiusProperty);
            set => this.SetValue(PanelCornerRadiusProperty, value);
        }
        private void OnPanelCornerRadiusChanged(DependencyPropertyChangedEventArgs<CornerRadius> e)
        {
            if (this.PART_ContentBorder != null)
            {
                this.PART_ContentBorder.CornerRadius = e.NewValue;
            }
        }


        public static readonly DependencyProperty TabStripBackgroundProperty = DPUtils.Register(_ => _.TabStripBackground, (d,e)=>d.OnTabStripBackgroundChanged(e));
        public Brush TabStripBackground
        {
            get => (Brush)this.GetValue(TabStripBackgroundProperty);
            set => this.SetValue(TabStripBackgroundProperty, value);
        }

        private void OnTabStripBackgroundChanged(DependencyPropertyChangedEventArgs<Brush> e)
        {
            //if (this.PART_TabStripWrapper != null)
            //{
            //    this.PART_TabStripWrapper.Background = e.NewValue;
            //}
        }

        // ===========[ Dependency Properties — Content ]======================

        public static readonly DependencyProperty HeaderContentProperty = DPUtils.Register(_ => _.HeaderContent, (d,e)=>d.OnHeaderContentChanged(e));
        public UIElement HeaderContent
        {
            get => (UIElement)this.GetValue(HeaderContentProperty);
            set => this.SetValue(HeaderContentProperty, value);
        }
        private void OnHeaderContentChanged(DependencyPropertyChangedEventArgs<UIElement> e)
        {
            if (this.PART_HeaderBar != null)
            {
                this.PART_HeaderBar.Child = e.NewValue;
            }
        }

        public static readonly DependencyProperty PanelContentProperty = DPUtils.Register(_ => _.PanelContent, (d,e)=>d.OnPanelContentChanged(e));
        public UIElement PanelContent
        {
            get => (UIElement)this.GetValue(PanelContentProperty);
            set => this.SetValue(PanelContentProperty, value);
        }
        private void OnPanelContentChanged(DependencyPropertyChangedEventArgs<UIElement> e)
        {
            if (this.PART_ContentPresenter != null)
            {
                this.PART_ContentPresenter.Content = e.NewValue;
            }
        }

        public static readonly DependencyProperty TabNavContentProperty = DPUtils.Register(
            _ => _.TabNavContent, (d, e) => { if (d.PART_TabNavPresenter != null) d.PART_TabNavPresenter.Content = e.NewValue; });
        public UIElement TabNavContent
        {
            get => (UIElement)this.GetValue(TabNavContentProperty);
            set => this.SetValue(TabNavContentProperty, value);
        }

        // ===========[ Dependency Properties — State ]========================

        public static readonly DependencyProperty IsTabbedProperty = DPUtils.Register(
            _ => _.IsTabbed, (d, e) => d.ApplyTabbedState());
        public bool IsTabbed
        {
            get => (bool)this.GetValue(IsTabbedProperty);
            set => this.SetValue(IsTabbedProperty, value);
        }

        // ===========[ Template application ]=================================
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            this.PART_HeaderBar = (Border)this.GetTemplateChild(nameof(this.PART_HeaderBar));
            this.PART_ContentBorder = (Border)this.GetTemplateChild(nameof(this.PART_ContentBorder));
            this.PART_ContentPresenter = (ContentPresenter)this.GetTemplateChild(nameof(this.PART_ContentPresenter));
            this.PART_TabStripWrapper = (Border)this.GetTemplateChild(nameof(this.PART_TabStripWrapper));
            this.PART_TabNavPresenter = (ContentPresenter)this.GetTemplateChild(nameof(this.PART_TabNavPresenter));

            if (this.PART_HeaderBar != null)
            {
                // Forward pointer events so DockZone can wire header-drag without
                // depending on the internal template structure.
                this.PART_HeaderBar.PointerPressed += (s, e) => this.HeaderPointerPressed?.Invoke(s, e);
                this.PART_HeaderBar.PointerMoved += (s, e) => this.HeaderPointerMoved?.Invoke(s, e);
                this.PART_HeaderBar.PointerReleased += (s, e) => this.HeaderPointerReleased?.Invoke(s, e);
                this.PART_HeaderBar.PointerCaptureLost += (s, e) => this.HeaderPointerCaptureLost?.Invoke(s, e);
            }

            // Push current DP values — WinUI3 TemplateBinding does not backfill
            // values set before template application, so we push them manually.
            this.ApplyAll();
        }

        // ===========[ Apply helpers ]=========================================
        private void ApplyAll()
        {
            try
            {
                this.ApplyBackground();
                this.ApplyBorderBrush();
                this.ApplyBorderThickness();

                if (this.PART_ContentBorder != null)
                {
                    this.PART_ContentBorder.CornerRadius = this.PanelCornerRadius;
                }
                if (this.PART_HeaderBar != null && this.HeaderContent != null)
                {
                    this.PART_HeaderBar.Child = this.HeaderContent;
                }
                if (this.PART_ContentPresenter != null && this.PanelContent != null)
                {
                    this.PART_ContentPresenter.Content = this.PanelContent;
                }
                if (this.PART_TabNavPresenter != null && this.TabNavContent != null)
                {
                    this.PART_TabNavPresenter.Content = this.TabNavContent;
                }

                this.ApplyTabbedState();
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }

        private void ApplyBackground()
        {
            
        }

        private void ApplyBorderBrush()
        {
            //if (this.PART_HeaderBar != null)
            //{
            //    this.PART_HeaderBar.BorderBrush = this.PanelBorderBrush;
            //}
            //if (this.PART_ContentBorder != null)
            //{
            //    this.PART_ContentBorder.BorderBrush = this.PanelBorderBrush;
            //}
            //if (this.PART_TabStripWrapper != null)
            //{
            //    this.PART_TabStripWrapper.BorderBrush = this.PanelBorderBrush;
            //}
        }

        private void ApplyBorderThickness()
        {
            if (this.PART_ContentBorder == null)
            {
                return;
            }

            var bt = this.PanelBorderThickness;
            // When tabbed, suppress the content border's bottom so the tab strip's
            // top border is the sole visual boundary between content and tabs.
            //this.PART_ContentBorder.BorderThickness = this.IsTabbed
            //    ? new Thickness(bt.Left, bt.Top, bt.Right, 0)
            //    : bt;
        }

        private void ApplyTabbedState()
        {
            if (this.PART_TabStripWrapper != null)
            {
                this.PART_TabStripWrapper.Visibility = this.IsTabbed
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            // Re-apply border thickness: the bottom edge rule changes with IsTabbed.
            this.ApplyBorderThickness();
        }
    }
}
