namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using AJut.UX.PropertyInteraction;

    public class PropertyGridTemplateSelector : SwitchTemplateSelector
    {
        // Built-in DataTemplate singletons: lazily created the first time the
        // editor key is encountered and no consumer-registered template is present.
        private static DataTemplate s_builtInNullableTemplate;
        private static DataTemplate s_builtInButtonTemplate;

        protected override object GetKeyForItem (object item) => ((PropertyEditTarget)item).Editor ?? "__Invalid";

        public override DataTemplate SelectTemplate (object item, DependencyObject container)
        {
            if (item is PropertyEditTarget target)
            {
                if (target.Editor == "Nullable" && !this.RegisteredTemplates.ContainsKey("Nullable"))
                {
                    return GetOrCreateBuiltInNullableTemplate();
                }

                if (target.Editor == "Button" && !this.RegisteredTemplates.ContainsKey("Button"))
                {
                    return GetOrCreateBuiltInButtonTemplate();
                }
            }

            return base.SelectTemplate(item, container);
        }

        private static DataTemplate GetOrCreateBuiltInNullableTemplate ()
        {
            if (s_builtInNullableTemplate == null)
            {
                var dt = new DataTemplate { DataType = typeof(PropertyEditTarget) };
#pragma warning disable CS0618 // FrameworkElementFactory is the only way to create DataTemplates in code
                dt.VisualTree = new FrameworkElementFactory(typeof(NullableEditor));
#pragma warning restore CS0618
                dt.Seal();
                s_builtInNullableTemplate = dt;
            }

            return s_builtInNullableTemplate;
        }

        private static DataTemplate GetOrCreateBuiltInButtonTemplate ()
        {
            if (s_builtInButtonTemplate == null)
            {
                var dt = new DataTemplate { DataType = typeof(PropertyEditTarget) };
#pragma warning disable CS0618
                var buttonFactory = new FrameworkElementFactory(typeof(Button));
                buttonFactory.SetBinding(Button.ContentProperty, new Binding("EditValue"));
                buttonFactory.SetBinding(Button.CommandProperty, new Binding("EditContext"));
                buttonFactory.SetValue(Button.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
                buttonFactory.SetValue(Button.VerticalAlignmentProperty, VerticalAlignment.Center);
                dt.VisualTree = buttonFactory;
#pragma warning restore CS0618
                dt.Seal();
                s_builtInButtonTemplate = dt;
            }

            return s_builtInButtonTemplate;
        }
    }
}
