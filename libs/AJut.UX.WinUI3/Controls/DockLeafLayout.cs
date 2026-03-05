namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using System;
    using Windows.Foundation;
    using DPUtils = AJut.UX.DPUtils<DockLeafLayout>;

    // ===========[ DockLeafLayout ]=============================================
    // WinUI3-specific: no WPF equivalent.
    // Leaf layout for DockZone (Single and Tabbed orientations). Defines the
    // 3-row structure - header bar / content panel / tab strip - entirely in
    // XAML so that all visual properties (backgrounds, borders, corner radius,
    // padding) live in the template rather than in DockZone code-behind.
    //
    // Template parts:
    //   PART_HeaderBar        - Border hosting the panel title / close button
    //   PART_ContentBorder    - Border framing the docked content presenter
    //   PART_ContentPresenter - ContentPresenter for PanelContent
    //   PART_TabStripWrapper  - Border hosting the tab navigation row
    //   PART_TabNavPresenter  - ContentPresenter for TabNavContent
    //
    // TemplateBinding handles PanelBackground, PanelBorderBrush,
    // PanelBorderThickness and TabStripBackground reactively after template
    // application.  PanelCornerRadius has no TemplateBinding path to the
    // nested PART_ContentBorder, so it is pushed from code in OnApplyTemplate
    // and its DP change handler.  Content DPs (HeaderContent, PanelContent,
    // TabNavContent) are pushed from code because they target .Child/.Content
    // properties rather than DPs exposed for binding.

    [TemplatePart(Name = nameof(PART_HeaderBar), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_ContentBorder), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_ContentPresenter), Type = typeof(ContentPresenter))]
    [TemplatePart(Name = nameof(PART_TabStripWrapper), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_TabNavPresenter), Type = typeof(ContentPresenter))]
    public sealed class DockLeafLayout : Control
    {
        // ===========[ Construction ]=========================================
        public DockLeafLayout ()
        {
            this.DefaultStyleKey = typeof(DockLeafLayout);
        }

        // ===========[ Header pointer events ]================================
        // Forwarded from PART_HeaderBar so DockZone can subscribe before the
        // template is applied, without coupling to the internal Border reference.
        public event PointerEventHandler HeaderPointerPressed;
        public event PointerEventHandler HeaderPointerMoved;
        public event PointerEventHandler HeaderPointerReleased;
        public event PointerEventHandler HeaderPointerCaptureLost;
        public event EventHandler<RightTappedRoutedEventArgs> HeaderRightTapped;

        // ===========[ Template Properties ]===================================
        public Border PART_HeaderBar { get; private set; }
        public Border PART_ContentBorder { get; private set; }
        public ContentPresenter PART_ContentPresenter { get; private set; }
        public Border PART_TabStripWrapper { get; private set; }
        public ContentPresenter PART_TabNavPresenter { get; private set; }

        // ===========[ Dependency Properties - Styling ]======================
        // PanelBackground / PanelBorderBrush / PanelBorderThickness / TabStripBackground:
        // no DP change callbacks - TemplateBinding in DockLeafLayout.xaml keeps
        // the template parts in sync after template application.

        public static readonly DependencyProperty PanelBackgroundProperty = DPUtils.Register(_ => _.PanelBackground);
        public Brush PanelBackground
        {
            get => (Brush)this.GetValue(PanelBackgroundProperty);
            set => this.SetValue(PanelBackgroundProperty, value);
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

        // PanelCornerRadius: pushed from code - no TemplateBinding path to the
        // nested PART_ContentBorder's CornerRadius in WinUI3.
        public static readonly DependencyProperty PanelCornerRadiusProperty = DPUtils.Register(_ => _.PanelCornerRadius, (d, e) => d.OnPanelCornerRadiusChanged(e));
        public CornerRadius PanelCornerRadius
        {
            get => (CornerRadius)this.GetValue(PanelCornerRadiusProperty);
            set => this.SetValue(PanelCornerRadiusProperty, value);
        }
        private void OnPanelCornerRadiusChanged (DependencyPropertyChangedEventArgs<CornerRadius> e)
        {
            if (this.PART_ContentBorder != null)
            {
                this.PART_ContentBorder.CornerRadius = e.NewValue;
            }
        }

        public static readonly DependencyProperty TabStripBackgroundProperty = DPUtils.Register(_ => _.TabStripBackground);
        public Brush TabStripBackground
        {
            get => (Brush)this.GetValue(TabStripBackgroundProperty);
            set => this.SetValue(TabStripBackgroundProperty, value);
        }

        // ===========[ Dependency Properties - Content ]======================

        public static readonly DependencyProperty HeaderContentProperty = DPUtils.Register(_ => _.HeaderContent, (d, e) => d.OnHeaderContentChanged(e));
        public UIElement HeaderContent
        {
            get => (UIElement)this.GetValue(HeaderContentProperty);
            set => this.SetValue(HeaderContentProperty, value);
        }
        private void OnHeaderContentChanged (DependencyPropertyChangedEventArgs<UIElement> e)
        {
            if (this.PART_HeaderBar != null)
            {
                this.PART_HeaderBar.Child = e.NewValue;
            }
        }

        public static readonly DependencyProperty PanelContentProperty = DPUtils.Register(_ => _.PanelContent, (d, e) => d.OnPanelContentChanged(e));
        public UIElement PanelContent
        {
            get => (UIElement)this.GetValue(PanelContentProperty);
            set => this.SetValue(PanelContentProperty, value);
        }
        private void OnPanelContentChanged (DependencyPropertyChangedEventArgs<UIElement> e)
        {
            if (this.PART_ContentPresenter != null)
            {
                this.PART_ContentPresenter.Content = e.NewValue;
            }
        }

        public static readonly DependencyProperty TabNavContentProperty = DPUtils.Register(_ => _.TabNavContent, (d, e) => d.OnTabNavContentChanged(e));
        public UIElement TabNavContent
        {
            get => (UIElement)this.GetValue(TabNavContentProperty);
            set => this.SetValue(TabNavContentProperty, value);
        }

        private void OnTabNavContentChanged(DependencyPropertyChangedEventArgs<UIElement> e)
        {
            if (this.PART_TabNavPresenter != null)
            {
                this.PART_TabNavPresenter.Content = e.NewValue;
            }
        }


        // ===========[ Dependency Properties - State ]========================

        public static readonly DependencyProperty IsTabbedProperty = DPUtils.Register(_ => _.IsTabbed, (d, e) => d.ApplyTabbedState());
        public bool IsTabbed
        {
            get => (bool)this.GetValue(IsTabbedProperty);
            set => this.SetValue(IsTabbedProperty, value);
        }

        // ===========[ Template application ]=================================
        protected override void OnApplyTemplate ()
        {
            // Unhook old header bar events
            if (this.PART_HeaderBar != null)
            {
                this.PART_HeaderBar.PointerPressed -= this.OnHeaderBarPointerPressed;
                this.PART_HeaderBar.PointerMoved -= this.OnHeaderBarPointerMoved;
                this.PART_HeaderBar.PointerReleased -= this.OnHeaderBarPointerReleased;
                this.PART_HeaderBar.PointerCaptureLost -= this.OnHeaderBarPointerCaptureLost;
                this.PART_HeaderBar.RightTapped -= this.OnHeaderBarRightTapped;
            }

            base.OnApplyTemplate();

            this.PART_HeaderBar = (Border)this.GetTemplateChild(nameof(this.PART_HeaderBar));
            this.PART_ContentBorder = (Border)this.GetTemplateChild(nameof(this.PART_ContentBorder));
            this.PART_ContentPresenter = (ContentPresenter)this.GetTemplateChild(nameof(this.PART_ContentPresenter));
            this.PART_TabStripWrapper = (Border)this.GetTemplateChild(nameof(this.PART_TabStripWrapper));
            this.PART_TabNavPresenter = (ContentPresenter)this.GetTemplateChild(nameof(this.PART_TabNavPresenter));

            // Forward pointer events so DockZone can wire header-drag without
            // depending on the internal template structure.
            if (this.PART_HeaderBar != null)
            {
                this.PART_HeaderBar.PointerPressed += this.OnHeaderBarPointerPressed;
                this.PART_HeaderBar.PointerMoved += this.OnHeaderBarPointerMoved;
                this.PART_HeaderBar.PointerReleased += this.OnHeaderBarPointerReleased;
                this.PART_HeaderBar.PointerCaptureLost += this.OnHeaderBarPointerCaptureLost;
                this.PART_HeaderBar.RightTapped += this.OnHeaderBarRightTapped;
            }

            // Push current DP values that can't be covered by TemplateBinding.
            // WinUI3 TemplateBinding does not backfill values set before template
            // application, so PanelCornerRadius and content DPs need a manual push.
            // PanelBackground / PanelBorderBrush / PanelBorderThickness / TabStripBackground
            // are covered by TemplateBinding in the XAML template.
            this.ApplyAll();
        }

        // ===========[ Apply helpers ]=========================================
        private void ApplyAll ()
        {
            try
            {
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
            catch (Exception ex)
            {
                Logger.LogError("Error in DockLeafLayout::ApplyAll", ex);
            }
        }

        private void ApplyTabbedState ()
        {
            if (this.PART_TabStripWrapper != null)
            {
                this.PART_TabStripWrapper.Visibility = this.IsTabbed
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        // ===========[ Header Bar Event Forwarding ]============================
        // Named handlers forwarding pointer events from PART_HeaderBar to the
        // public events so DockZone can subscribe without coupling to the Border.

        private void OnHeaderBarPointerPressed (object sender, PointerRoutedEventArgs e)
        {
            this.HeaderPointerPressed?.Invoke(sender, e);
        }

        private void OnHeaderBarPointerMoved (object sender, PointerRoutedEventArgs e)
        {
            this.HeaderPointerMoved?.Invoke(sender, e);
        }

        private void OnHeaderBarPointerReleased (object sender, PointerRoutedEventArgs e)
        {
            this.HeaderPointerReleased?.Invoke(sender, e);
        }

        private void OnHeaderBarPointerCaptureLost (object sender, PointerRoutedEventArgs e)
        {
            this.HeaderPointerCaptureLost?.Invoke(sender, e);
        }

        private void OnHeaderBarRightTapped (object sender, RightTappedRoutedEventArgs e)
        {
            this.HeaderRightTapped?.Invoke(sender, e);
        }
    }
}
