namespace AJut.Storage
{
    /// <summary>
    /// Concierge extensions for <see cref="MemLibrary"/> and <see cref="IMemLibraryConcierge{T}"/>
    /// </summary>
    public static class MemLibraryConciergeXT
    {
        public static MemLibrary.ManagedShelfItem<TValue> CheckoutFromMemLibrary<TValue, TConcierge>(this TConcierge concierge)
            where TValue : new()
            where TConcierge : IMemLibraryConcierge<TValue>
        {
            // 2. Delegate to the original, non-boxing static method.
            return MemLibrary.Checkout<TValue, TConcierge>(concierge);
        }

        public static TValue TakeFromMemLibrary<TValue, TConcierge>(this TConcierge concierge)
            where TValue : new()
            where TConcierge : IMemLibraryConcierge<TValue>
        {
            // 2. Delegate to the original, non-boxing static method.
            return MemLibrary.Take<TValue, TConcierge>(concierge);
        }
    }
}
