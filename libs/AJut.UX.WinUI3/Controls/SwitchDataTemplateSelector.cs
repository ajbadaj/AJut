namespace AJut.UX.Controls
{
    using System.Collections.Generic;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    // ===========[ SwitchDataTemplateSelector ]================================
    // WinUI3 port of WPF SwitchTemplateSelector. Holds a dictionary of
    // DataTemplates keyed by arbitrary object (typically a string or type).
    // Subclasses override GetKeyForItem to determine the lookup key from an item.

    public class SwitchDataTemplateDictionary : Dictionary<object, DataTemplate> { }

    public class SwitchDataTemplateSelector : DataTemplateSelector
    {
        public SwitchDataTemplateDictionary RegisteredTemplates { get; } = new SwitchDataTemplateDictionary();
        public DataTemplate Default { get; set; }

        protected override DataTemplate SelectTemplateCore (object item)
        {
            if (item == null)
            {
                return null;
            }

            object key = this.GetKeyForItem(item);
            if (this.RegisteredTemplates.TryGetValue(key, out DataTemplate template))
            {
                return template;
            }

            return this.Default;
        }

        protected override DataTemplate SelectTemplateCore (object item, DependencyObject container)
        {
            return this.SelectTemplateCore(item);
        }

        protected virtual object GetKeyForItem (object item) => item;
    }
}
