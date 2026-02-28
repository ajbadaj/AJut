namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX.PropertyInteraction;

    public class PropertyGridTemplateSelector : SwitchTemplateSelector
    {
        // Built-in DataTemplate singleton: lazily created the first time a "Nullable"
        // editor key is encountered and no consumer-registered template is present.
        private static DataTemplate s_builtInNullableTemplate;

        protected override object GetKeyForItem (object item) => ((PropertyEditTarget)item).Editor ?? "__Invalid";

        public override DataTemplate SelectTemplate (object item, DependencyObject container)
        {
            if (item is PropertyEditTarget { Editor: "Nullable" }
                && !this.RegisteredTemplates.ContainsKey("Nullable"))
            {
                return GetOrCreateBuiltInNullableTemplate();
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
    }
}
