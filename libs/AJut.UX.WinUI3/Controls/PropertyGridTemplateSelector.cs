namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Markup;
    using AJut;
    using AJut.UX.PropertyInteraction;

    // ===========[ PropertyGridTemplateSelector ]===============================
    // DataTemplateSelector for PropertyGrid rows. Routes each PropertyEditTarget
    // to a DataTemplate keyed by PropertyEditTarget.Editor (a type-name string).
    // Consumers register templates against editor names:
    //   selector.RegisteredTemplates["string"]  = myStringEditorTemplate;
    //   selector.RegisteredTemplates["Single"]  = myFloatEditorTemplate;
    //
    // "Nullable" editor key is handled automatically with a built-in NullableEditor
    // template unless the consumer overrides it via RegisteredTemplates["Nullable"].

    public class PropertyGridTemplateSelector : SwitchDataTemplateSelector
    {
        private static DataTemplate s_builtInNullableTemplate;
        private static DataTemplate s_builtInButtonTemplate;

        protected override object GetKeyForItem (object item)
            => ((PropertyEditTarget)item).Editor ?? "__Invalid";

        protected override DataTemplate SelectTemplateCore (object item)
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

            return base.SelectTemplateCore(item);
        }

        protected override DataTemplate SelectTemplateCore (object item, DependencyObject container)
        {
            return this.SelectTemplateCore(item);
        }

        private static DataTemplate GetOrCreateBuiltInNullableTemplate ()
        {
            if (s_builtInNullableTemplate != null)
            {
                return s_builtInNullableTemplate;
            }

            try
            {
                s_builtInNullableTemplate = (DataTemplate)XamlReader.Load(
                    "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                    "xmlns:local=\"using:AJut.UX.Controls\">" +
                    "<local:NullableEditor/>" +
                    "</DataTemplate>"
                );
            }
            catch (System.Exception ex)
            {
                Logger.LogError("[WARNING] NullableEditor built-in template could not be created via XamlReader. " +
                    "Register selector.RegisteredTemplates[\"Nullable\"] manually.", ex);
            }

            return s_builtInNullableTemplate;
        }

        private static DataTemplate GetOrCreateBuiltInButtonTemplate ()
        {
            if (s_builtInButtonTemplate != null)
            {
                return s_builtInButtonTemplate;
            }

            try
            {
                s_builtInButtonTemplate = (DataTemplate)XamlReader.Load(
                    "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
                    "<Button Content=\"{Binding EditValue}\" Command=\"{Binding EditContext}\" " +
                    "HorizontalAlignment=\"Stretch\" VerticalAlignment=\"Center\"/>" +
                    "</DataTemplate>"
                );
            }
            catch (System.Exception ex)
            {
                Logger.LogError("[WARNING] Button built-in template could not be created via XamlReader. " +
                    "Register selector.RegisteredTemplates[\"Button\"] manually.", ex);
            }

            return s_builtInButtonTemplate;
        }
    }
}
