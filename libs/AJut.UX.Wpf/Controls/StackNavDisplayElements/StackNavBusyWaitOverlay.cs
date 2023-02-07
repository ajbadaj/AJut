namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using DPUtils = AJut.UX.DPUtils<StackNavBusyWaitOverlay>;

    /// <summary>
    /// The standard stack nav busy wait overlay
    /// </summary>
    public class StackNavBusyWaitOverlay : Control
    {
        static StackNavBusyWaitOverlay ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(StackNavBusyWaitOverlay), new FrameworkPropertyMetadata(typeof(StackNavBusyWaitOverlay)));
        }

        public static readonly DependencyProperty MessageContentProperty = DPUtils.Register(_ => _.MessageContent);
        public object MessageContent
        {
            get => this.GetValue(MessageContentProperty);
            set => this.SetValue(MessageContentProperty, value);
        }

        public static readonly DependencyProperty MessageContentTemplateProperty = DPUtils.Register(_ => _.MessageContentTemplate);
        public DataTemplate MessageContentTemplate
        {
            get => (DataTemplate)this.GetValue(MessageContentTemplateProperty);
            set => this.SetValue(MessageContentTemplateProperty, value);
        }

        public static readonly DependencyProperty MessageContentTemplateSelectorProperty = DPUtils.Register(_ => _.MessageContentTemplateSelector);
        public DataTemplateSelector MessageContentTemplateSelector
        {
            get => (DataTemplateSelector)this.GetValue(MessageContentTemplateSelectorProperty);
            set => this.SetValue(MessageContentTemplateSelectorProperty, value);
        }

        public static readonly DependencyProperty OverlayColumnMarginWidthProperty = DPUtils.Register(_ => _.OverlayColumnMarginWidth);
        public GridLength OverlayColumnMarginWidth
        {
            get => (GridLength)this.GetValue(OverlayColumnMarginWidthProperty);
            set => this.SetValue(OverlayColumnMarginWidthProperty, value);
        }

        public static readonly DependencyProperty OverlayRowMarginHeightProperty = DPUtils.Register(_ => _.OverlayRowMarginHeight);
        public GridLength OverlayRowMarginHeight
        {
            get => (GridLength)this.GetValue(OverlayRowMarginHeightProperty);
            set => this.SetValue(OverlayRowMarginHeightProperty, value);
        }

        public static readonly DependencyProperty OverlayWidthProperty = DPUtils.Register(_ => _.OverlayWidth, GridLength.Auto);
        public GridLength OverlayWidth
        {
            get => (GridLength)this.GetValue(OverlayWidthProperty);
            set => this.SetValue(OverlayWidthProperty, value);
        }

        public static readonly DependencyProperty OverlayHeightProperty = DPUtils.Register(_ => _.OverlayHeight, GridLength.Auto);
        public GridLength OverlayHeight
        {
            get => (GridLength)this.GetValue(OverlayHeightProperty);
            set => this.SetValue(OverlayHeightProperty, value);
        }

    }
}
