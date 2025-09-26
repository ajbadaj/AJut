namespace AJut.Storage
{
    using System.Collections.Generic;


    /// <summary>
    /// Enables automated <see cref="List{T}"/> preperation for recycled elements provided by the <see cref="MemLibrary"/>
    /// </summary>
    public struct MemLibraryListConcierge<T> : IMemLibraryConcierge<List<T>>
    {
        public int Capacity { get; set; }

        /// <summary>
        /// Checks out from the <see cref="MemLibrary"/> on your behalf (see <see cref="MemLibrary.Checkout{TValue, TConfig}(TConfig)")/>
        /// </summary>
        /// <returns></returns>
        public MemLibrary.ManagedShelfItem<List<T>> CheckoutFromMemLibrary()
        {
            return MemLibrary.Checkout<List<T>, MemLibraryListConcierge<T>>(this);
        }

        /// <summary>
        /// Takes from the <see cref="MemLibrary"/> on your behalf (see <see cref="MemLibrary.Take{TValue, TConfig}(TConfig)")
        /// </summary>
        public List<T> TakeFromMemLibrary()
        {
            return MemLibrary.Take<List<T>, MemLibraryListConcierge<T>>(this);
        }

        public void Prepare(List<T> value)
        {
            if (this.Capacity > 0 &&
                this.Capacity > value.Capacity)
            {
                value.Capacity = this.Capacity;
            }

            value.Clear();
        }
    }
}
