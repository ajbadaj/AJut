namespace AJut.UX
{
    // =========================================================================================================
    // TODO: Make determination on deprecation
    //
    // This should probably be deprecated, you can just give DataTemplate with keys of the type already and
    //  it will allow you to do this without a template selector, but maybe the default makes this relevant?
    // =========================================================================================================

    /// <summary>
    /// Allows you to declare a template selector that switches based off of data type.
    /// </summary>
    public class TypeTemplateSelector : SwitchTemplateSelector
    {
        protected override object GetKeyForItem (object item) => item.GetType();
    }
}
