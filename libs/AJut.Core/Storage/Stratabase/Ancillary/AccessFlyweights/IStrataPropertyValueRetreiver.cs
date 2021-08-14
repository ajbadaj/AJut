namespace AJut.Storage
{
    public interface IStrataPropertyValueRetreiver<out TProperty>
    {
        TProperty GetValue ();
    }
}