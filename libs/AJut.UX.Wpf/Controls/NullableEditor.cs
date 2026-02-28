namespace AJut.UX.Controls
{
    using System;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using AJut.UX.PropertyInteraction;
    using DPUtils = DPUtils<NullableEditor>;

    // ===========[ NullableEditor ]=============================================
    // Property grid editor for Nullable<T> properties.
    //
    // Receives a PropertyEditTarget (Editor="Nullable", EditContext=NullableEditorContext)
    // as DataContext. Displays the inner T editor with a "Not set" overlay when the value
    // is null. Set/Unset buttons let the user toggle between null and the default T value.
    //
    // The inner editor template is resolved by walking up the visual tree to the enclosing
    // PropertyGrid and borrowing its ItemTemplateSelector.
    //
    // Template parts:
    //   PART_InnerContent  - ContentPresenter for the inner T editor
    //   PART_NullOverlay   - overlay panel (darkening + "Not set" text + [Set] button)
    //   PART_SetButton     - [Set] button inside the overlay
    //   PART_UnsetButton   - [Unset] button shown when value is non-null

    [TemplatePart(Name = nameof(PART_InnerContent), Type = typeof(ContentPresenter))]
    [TemplatePart(Name = nameof(PART_NullOverlay), Type = typeof(UIElement))]
    [TemplatePart(Name = nameof(PART_SetButton), Type = typeof(Button))]
    [TemplatePart(Name = nameof(PART_UnsetButton), Type = typeof(Button))]
    public class NullableEditor : Control
    {
        // ===========[ Instance fields ]==========================================
        private PropertyEditTarget m_outerTarget;
        private ContentPresenter PART_InnerContent { get; set; }
        private UIElement PART_NullOverlay { get; set; }
        private Button PART_SetButton { get; set; }
        private Button PART_UnsetButton { get; set; }

        // ===========[ Construction ]=============================================
        static NullableEditor ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NullableEditor), new FrameworkPropertyMetadata(typeof(NullableEditor)));
        }

        public NullableEditor ()
        {
            this.DataContextChanged += this.OnDataContextChanged;
            this.Loaded += this.OnLoaded;
        }

        // ===========[ Dependency Properties ]====================================
        public static readonly DependencyProperty ProxyTargetProperty = DPUtils.Register(_ => _.ProxyTarget);
        public PropertyEditTarget ProxyTarget
        {
            get => (PropertyEditTarget)this.GetValue(ProxyTargetProperty);
            private set => this.SetValue(ProxyTargetProperty, value);
        }

        public static readonly DependencyProperty InnerEditorTemplateSelectorProperty = DPUtils.Register(_ => _.InnerEditorTemplateSelector);
        public DataTemplateSelector InnerEditorTemplateSelector
        {
            get => (DataTemplateSelector)this.GetValue(InnerEditorTemplateSelectorProperty);
            private set => this.SetValue(InnerEditorTemplateSelectorProperty, value);
        }

        // ===========[ Template application ]====================================
        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_SetButton != null)
            {
                this.PART_SetButton.Click -= this.OnSetClicked;
            }

            if (this.PART_UnsetButton != null)
            {
                this.PART_UnsetButton.Click -= this.OnUnsetClicked;
            }

            this.PART_InnerContent = this.GetTemplateChild(nameof(PART_InnerContent)) as ContentPresenter;
            this.PART_NullOverlay = this.GetTemplateChild(nameof(PART_NullOverlay)) as UIElement;
            this.PART_SetButton = this.GetTemplateChild(nameof(PART_SetButton)) as Button;
            this.PART_UnsetButton = this.GetTemplateChild(nameof(PART_UnsetButton)) as Button;

            if (this.PART_SetButton != null)
            {
                this.PART_SetButton.Click += this.OnSetClicked;
            }

            if (this.PART_UnsetButton != null)
            {
                this.PART_UnsetButton.Click += this.OnUnsetClicked;
            }

            this.UpdateNullableState(false);
        }

        // ===========[ Events ]===================================================
        private void OnDataContextChanged (object sender, DependencyPropertyChangedEventArgs e)
        {
            if (m_outerTarget != null)
            {
                m_outerTarget.PropertyChanged -= this.OnOuterTargetPropertyChanged;
            }

            m_outerTarget = e.NewValue as PropertyEditTarget;

            if (m_outerTarget != null)
            {
                m_outerTarget.PropertyChanged += this.OnOuterTargetPropertyChanged;

                var context = m_outerTarget.EditContext as NullableEditorContext;
                this.ProxyTarget = BuildProxyTarget(m_outerTarget, context);
                this.InnerEditorTemplateSelector = this.FindParentPropertyGrid()?.ItemTemplateSelector;
            }
            else
            {
                this.ProxyTarget = null;
            }

            this.UpdateNullableState(false);
        }

        private void OnLoaded (object sender, RoutedEventArgs e)
        {
            // Retry in case FindParentPropertyGrid() returned null during DataContextChanged
            // (DataContextChanged can fire before the element is fully in the live visual tree).
            if (this.InnerEditorTemplateSelector == null && m_outerTarget != null)
            {
                this.InnerEditorTemplateSelector = this.FindParentPropertyGrid()?.ItemTemplateSelector;
            }
        }

        private void OnOuterTargetPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PropertyEditTarget.EditValue))
            {
                // ForceRaise instead of RecacheEditValue: when outer goes null→0 and back,
                // the proxy's cached value stays at 0 both times (getter returns default(T)
                // when outer is null). SetAndRaiseIfChanged would see no change and the inner
                // editor (NumericEditor) would not refresh. Force-raising always updates it.
                this.ProxyTarget?.ForceRaiseEditValueChanged();
                this.UpdateNullableState(true);
            }
        }

        private void OnSetClicked (object sender, RoutedEventArgs e)
        {
            if (m_outerTarget?.EditContext is NullableEditorContext ctx && ctx.InnerType != null)
            {
                m_outerTarget.EditValue = ctx.InnerType.IsValueType
                    ? Activator.CreateInstance(ctx.InnerType)
                    : null;
            }
        }

        private void OnUnsetClicked (object sender, RoutedEventArgs e)
        {
            if (m_outerTarget != null)
            {
                m_outerTarget.EditValue = null;
            }
        }

        // ===========[ Private helpers ]===========================================
        private void UpdateNullableState (bool useTransitions)
        {
            bool isNull = m_outerTarget?.EditValue == null;
            VisualStateManager.GoToState(this, isNull ? "IsNull" : "HasValue", useTransitions);
        }

        private PropertyGrid FindParentPropertyGrid ()
        {
            DependencyObject current = VisualTreeHelper.GetParent(this);
            while (current != null)
            {
                if (current is PropertyGrid pg)
                {
                    return pg;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static PropertyEditTarget BuildProxyTarget (PropertyEditTarget outer, NullableEditorContext context)
        {
            Type innerType = context?.InnerType ?? typeof(object);
            string editorKey = context?.InnerEditorKey ?? innerType.Name;

            var proxy = new PropertyEditTarget(
                outer.PropertyPathTarget,
                () => outer.EditValue ?? (innerType.IsValueType ? Activator.CreateInstance(innerType) : null),
                (v) => outer.EditValue = v
            )
            {
                Editor = editorKey,
                DisplayName = outer.DisplayName,
            };

            proxy.Setup();
            return proxy;
        }
    }
}
