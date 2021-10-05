namespace AJut.UX
{
    public class SelectionChange<TElement>
    {
        public SelectionChange (bool isClear)
        {
            this.IsClear = isClear;
        }

        public SelectionChange (TElement[] added, TElement [] removed, bool isClear = false)
        {
            this.Added = added ?? new TElement[0];
            this.Removed = removed ?? new TElement[0];
            this.IsClear = isClear;
        }

        /// <summary>
        /// The added items - guranteed to be set unless it's a clear, in which case it will be null.
        /// </summary>
        public TElement[] Added { get; }

        /// <summary>
        /// The removed items - guranteed to be set unless it's a clear, in which case it will be null.
        /// </summary>
        public TElement[] Removed { get; }

        /// <summary>
        /// Was the selection cleared
        /// </summary>
        public bool IsClear { get; }
    }
}
