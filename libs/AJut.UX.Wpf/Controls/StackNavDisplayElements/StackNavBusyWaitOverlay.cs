namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using DPUtils = AJut.UX.DPUtils<StackNavBusyWaitOverlay>;

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
    }
}
