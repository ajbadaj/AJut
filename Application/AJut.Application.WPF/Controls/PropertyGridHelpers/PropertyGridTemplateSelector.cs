namespace AJut.Application.Controls
{
    public class PropertyGridTemplateSelector : SwitchTemplateSelector
    {
        protected override object GetKeyForItem (object item) => ((PropertyEditTarget)item).Editor ?? "__Invalid";
    }
}
