namespace AJut.UX
{
    public class SelectionChange<TElement>
    {
        public SelectionChange (bool isClear)
        {
            this.IsClear = isClear;
        }

        public SelectionChange (TElement[] added, TElement[] removed, bool isClear = false)
        {
            this.Added = added ?? System.Array.Empty<TElement>();
            this.Removed = removed ?? System.Array.Empty<TElement>();
            this.IsClear = isClear;
        }

        public TElement[] Added { get; }
        public TElement[] Removed { get; }
        public bool IsClear { get; }
    }
}
