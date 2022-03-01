namespace AJut.UX.Controls
{
    using AJut.UX.PropertyInteraction;
    public class PropertyGridTemplateSelector : SwitchTemplateSelector
    {
        protected override object GetKeyForItem (object item) => ((PropertyEditTarget)item).Editor ?? "__Invalid";
    }
}
