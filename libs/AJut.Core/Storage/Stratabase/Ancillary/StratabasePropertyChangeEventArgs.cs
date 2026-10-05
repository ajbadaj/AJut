namespace AJut.Storage
{
    internal class StratabasePropertyChangeEventArgs : StratabaseChangeEventArgs
    {
        public object OldValue { get; init; } = null;
        public object NewValue { get; init; } = null;

        /// <summary>
        /// The change still reaches property access objects, but the <see cref="Stratabase"/> does not raise its store wide
        /// <see cref="Stratabase.BaselineDataChanged"/> / <see cref="Stratabase.OverrideDataChanged"/> for it (an import with notification off)
        /// </summary>
        public bool SuppressStoreEvents { get; init; }
    }
}
