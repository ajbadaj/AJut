namespace AJut.Application
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Controls;

    public class SwitchTemplateDictionary : Dictionary<object, DataTemplate>
    {
    }

    /// <summary>
    /// Allows you to declare a template selector that switches based off of data type.
    /// </summary>
    [DefaultProperty(nameof(RegisteredTemplates))]
    public class SwitchTemplateSelector : DataTemplateSelector
    {
        public SwitchTemplateDictionary RegisteredTemplates { get; } = new SwitchTemplateDictionary();
        public DataTemplate Default { get; set; }

        public override DataTemplate SelectTemplate (object item, DependencyObject container)
        {
            if (item == null)
            {
                return null;
            }

            object key = GetKeyForItem(item);
            if (this.RegisteredTemplates.TryGetValue(key, out DataTemplate template))
            {
                return template;
            }

            return this.Default ?? base.SelectTemplate(item, container);
        }

        protected virtual object GetKeyForItem (object item) => item;
    }
}
