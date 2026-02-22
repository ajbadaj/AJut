namespace AJut.UX.Controls
{
    using AJut.UX.PropertyInteraction;

    // ===========[ PropertyGridTemplateSelector ]===============================
    // DataTemplateSelector for PropertyGrid rows. Routes each PropertyEditTarget
    // to a DataTemplate keyed by PropertyEditTarget.Editor (a type-name string).
    // Consumers (e.g. CallFamiliar) register templates against editor names:
    //   selector.RegisteredTemplates["string"]  = myStringEditorTemplate;
    //   selector.RegisteredTemplates["Single"]  = myFloatEditorTemplate;

    public class PropertyGridTemplateSelector : SwitchDataTemplateSelector
    {
        protected override object GetKeyForItem (object item)
            => ((PropertyEditTarget)item).Editor ?? "__Invalid";
    }
}
