namespace AJut.Storage
{
    internal class StratabaseListElementsChangedEventArgs : StratabaseChangeEventArgs
    {
        public int ElementIndex { get; init; }
        public object Element { get; init; }
        public bool WasElementAdded { get; init; }
        public bool WasElementRemoved => !this.WasElementAdded;
    }
}
