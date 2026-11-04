namespace AJut.UX.Controls
{
    using System;
    using System.Windows;
    using AJut.UX.PropertyInteraction;

    public class PropertyGridTemplateSelector : SwitchTemplateSelector
    {
        // The built-in row templates, loaded the first time a row needs one that the grid has no template of its own
        //  registered for, and shared by every grid after that
        private static readonly Lazy<PropertyGridBuiltInTemplates> g_builtInTemplates = new Lazy<PropertyGridBuiltInTemplates>(() => new PropertyGridBuiltInTemplates());

        protected override object GetKeyForItem (object item) => PropertyGridRowRules.GetEditorKey((PropertyEditTarget)item);

        public override DataTemplate SelectTemplate (object item, DependencyObject container)
        {
            if (item is PropertyEditTarget target)
            {
                switch (PropertyGridRowRules.GetBuiltInEditor(target, this.RegisteredTemplates.ContainsKey))
                {
                    case eBuiltInPropertyEditor.Nullable: return g_builtInTemplates.Value.NullableTemplate;
                    case eBuiltInPropertyEditor.Button: return g_builtInTemplates.Value.ButtonTemplate;
                    case eBuiltInPropertyEditor.List: return g_builtInTemplates.Value.ListTemplate;
                }
            }

            return base.SelectTemplate(item, container);
        }
    }
}
