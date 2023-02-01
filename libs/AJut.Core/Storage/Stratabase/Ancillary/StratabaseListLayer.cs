namespace AJut.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class StratabaseListElementsChangedEventArgs : EventArgs
    {
        public Guid ObjectId { get; init; }
        public string PropertyName { get; init; }
        public bool WasBaseline => this.Layer == -1;
        public int Layer { get; init; }
        public int ElementIndex { get; init; }
        public object Element { get; init; }
        public bool WasElementAdded { get; init; }
        public bool WasElementRemoved => !this.WasElementAdded;
    }
}
