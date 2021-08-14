namespace AJut.Application.Controls
{
    using AJut.Application.PropertyInteraction;
    public class PropertyGridTemplateSelector : SwitchTemplateSelector
    {
        protected override object GetKeyForItem (object item) => ((PropertyEditTarget)item).Editor ?? "__Invalid";
    }
}
