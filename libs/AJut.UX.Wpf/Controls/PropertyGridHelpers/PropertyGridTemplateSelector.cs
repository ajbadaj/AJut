namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using AJut.UX.AttachedProperties;
    using AJut.UX.Converters;
    using AJut.UX.PropertyInteraction;

    public class PropertyGridTemplateSelector : SwitchTemplateSelector
    {
        // Built-in DataTemplate singletons: lazily created the first time the
        // editor key is encountered and no consumer-registered template is present.
        private static DataTemplate s_builtInNullableTemplate;
        private static DataTemplate s_builtInButtonTemplate;
        private static DataTemplate s_builtInListTemplate;

        protected override object GetKeyForItem (object item) => PropertyGridRowRules.GetEditorKey((PropertyEditTarget)item);

        public override DataTemplate SelectTemplate (object item, DependencyObject container)
        {
            if (item is PropertyEditTarget target)
            {
                switch (PropertyGridRowRules.GetBuiltInEditor(target, this.RegisteredTemplates.ContainsKey))
                {
                    case eBuiltInPropertyEditor.Nullable: return GetOrCreateBuiltInNullableTemplate();
                    case eBuiltInPropertyEditor.Button: return GetOrCreateBuiltInButtonTemplate();
                    case eBuiltInPropertyEditor.List: return GetOrCreateBuiltInListTemplate();
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

                // A read-only grid (Edit.IsReadOnly, inherited) runs no button rows. Button styles, AJut's themed
                // ones included, don't follow Edit.IsReadOnly, so the button binds it itself.
                buttonFactory.SetBinding(Button.IsEnabledProperty, new Binding
                {
                    RelativeSource = RelativeSource.Self,
                    Path = new PropertyPath(Edit.IsReadOnlyProperty),
                    Converter = new BooleanInverseConverter(),
                });
                buttonFactory.SetValue(Button.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
                buttonFactory.SetValue(Button.VerticalAlignmentProperty, VerticalAlignment.Center);
                dt.VisualTree = buttonFactory;
#pragma warning restore CS0618
                dt.Seal();
                s_builtInButtonTemplate = dt;
            }

            return s_builtInButtonTemplate;
        }

        private static DataTemplate GetOrCreateBuiltInListTemplate ()
        {
            if (s_builtInListTemplate == null)
            {
                var dt = new DataTemplate { DataType = typeof(PropertyEditTarget) };
#pragma warning disable CS0618
                dt.VisualTree = new FrameworkElementFactory(typeof(PropertyGridListEditor));
#pragma warning restore CS0618
                dt.Seal();
                s_builtInListTemplate = dt;
            }

            return s_builtInListTemplate;
        }
    }
}
