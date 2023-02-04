namespace AJut.Storage
{
    using System;

    internal class StratabaseChangeEventArgs : EventArgs
    {
        public Guid ItemId { get; init; }
        public string PropertyName { get; init; }
        public int LayerIndex { get; init; } = -1;
        public bool IsBaseline => this.LayerIndex == -1;
    }
}
