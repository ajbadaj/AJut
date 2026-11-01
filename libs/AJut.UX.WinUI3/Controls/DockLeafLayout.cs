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
    //   PART_HeaderBar        - Border behind the panel title / close button, taking the header's pointer events
    //   PART_HeaderPresenter  - ContentPresenter for HeaderContent, inside PART_HeaderBar
    //   PART_ContentBorder    - Border framing the docked content presenter
    //   PART_ContentPresenter - ContentPresenter for PanelContent
    //   PART_TabStripWrapper  - Border hosting the tab navigation row
    //   PART_TabNavPresenter  - ContentPresenter for TabNavContent
    //
    // TemplateBinding handles PanelBackground, PanelBorderBrush,
    // PanelBorderThickness and TabStripBackground reactively after template
    // application.  The header's background and text color follow the
    // PanelFocusStates visual states instead (HeaderBackground and Foreground, or
    // HeaderFocusedBackground and HeaderFocusedForeground while IsPanelFocused).
    // PanelCornerRadius has no TemplateBinding path to the
    // nested PART_ContentBorder, so it is pushed from code in OnApplyTemplate
    // and its DP change handler.  Content DPs (HeaderContent, PanelContent,
    // TabNavContent) are pushed from code because they target .Content
    // properties rather than DPs exposed for binding.

    [TemplatePart(Name = nameof(PART_HeaderBar), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_HeaderPresenter), Type = typeof(ContentPresenter))]
    [TemplatePart(Name = nameof(PART_ContentBorder), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_ContentPresenter), Type = typeof(ContentPresenter))]
    [TemplatePart(Name = nameof(PART_TabStripWrapper), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_TabNavPresenter), Type = typeof(ContentPresenter))]
    [TemplateVisualState(Name = "PanelUnfocused", GroupName = "PanelFocusStates")]
    [TemplateVisualState(Name = "PanelFocused", GroupName = "PanelFocusStates")]
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
        public ContentPresenter PART_HeaderPresenter { get; private set; }
        public Border PART_ContentBorder { get; private set; }
        public ContentPresenter PART_ContentPresenter { get; private set; }
        public Border PART_TabStripWrapper { get; private set; }
        public ContentPresenter PART_TabNavPresenter { get; private set; }

        // ===========[ Dependency Properties - Styling ]======================
        // HeaderBackground / HeaderFocusedBackground / HeaderFocusedForeground / PanelBackground / PanelBorderBrush /
        // PanelBorderThickness / TabStripBackground: no DP change callbacks - TemplateBinding, or the PanelFocusStates
        // setters' bindings, in DockLeafLayout.xaml keep the template parts in sync after template application.

        public static readonly DependencyProperty HeaderBackgroundProperty = DPUtils.Register(_ => _.HeaderBackground);
        /// <summary>
        /// The background of the header bar above the panel, which holds the panel's title. It sits a level apart from
        /// PanelBackground, the way the unselected tabs do, so the header reads as the panel's frame rather than its content.
        /// </summary>
        public Brush HeaderBackground
        {
            get => (Brush)this.GetValue(HeaderBackgroundProperty);
            set => this.SetValue(HeaderBackgroundProperty, value);
        }

        public static readonly DependencyProperty HeaderFocusedBackgroundProperty = DPUtils.Register(_ => _.HeaderFocusedBackground);
        /// <summary>
        /// The header bar's background while <see cref="IsPanelFocused"/>, marking the panel being typed into
        /// </summary>
        public Brush HeaderFocusedBackground
        {
            get => (Brush)this.GetValue(HeaderFocusedBackgroundProperty);
            set => this.SetValue(HeaderFocusedBackgroundProperty, value);
        }

        public static readonly DependencyProperty HeaderFocusedForegroundProperty = DPUtils.Register(_ => _.HeaderFocusedForeground);
        /// <summary>
        /// The header's text color while <see cref="IsPanelFocused"/>, on <see cref="HeaderFocusedBackground"/>
        /// </summary>
        public Brush HeaderFocusedForeground
        {
            get => (Brush)this.GetValue(HeaderFocusedForegroundProperty);
            set => this.SetValue(HeaderFocusedForegroundProperty, value);
        }

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
            if (this.PART_HeaderPresenter != null)
            {
                this.PART_HeaderPresenter.Content = e.NewValue;
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

        public static readonly DependencyProperty IsPanelFocusedProperty = DPUtils.Register(_ => _.IsPanelFocused, (d, e) => d.ApplyPanelFocusState());
        /// <summary>
        /// Is this the panel being typed into: keyboard focus is within it and its window is active. The header shows it
        /// on <see cref="HeaderFocusedBackground"/> and <see cref="HeaderFocusedForeground"/>. Set by the DockZone that
        /// hosts this layout.
        /// </summary>
        public bool IsPanelFocused
        {
            get => (bool)this.GetValue(IsPanelFocusedProperty);
            set => this.SetValue(IsPanelFocusedProperty, value);
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
            this.PART_HeaderPresenter = (ContentPresenter)this.GetTemplateChild(nameof(this.PART_HeaderPresenter));
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
                if (this.PART_HeaderPresenter != null && this.HeaderContent != null)
                {
                    this.PART_HeaderPresenter.Content = this.HeaderContent;
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
                this.ApplyPanelFocusState();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in DockLeafLayout::ApplyAll", ex);
            }
        }

        // The header's background and text color are set only by these states (their setters bind to the brush DPs), not
        // by TemplateBinding as well, which would overwrite the state's value whenever the template re-applied
        private void ApplyPanelFocusState ()
        {
            VisualStateManager.GoToState(this, this.IsPanelFocused ? "PanelFocused" : "PanelUnfocused", false);
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
