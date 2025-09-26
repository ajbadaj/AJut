namespace AJut.Storage
{
    /// <summary>
    /// Enables automated memory preperation for recycled elements provided by the <see cref="MemLibrary"/>
    /// </summary>
    public interface IMemLibraryConcierge<T> where T : new()
    {
        void Prepare(T value);
    }
}
