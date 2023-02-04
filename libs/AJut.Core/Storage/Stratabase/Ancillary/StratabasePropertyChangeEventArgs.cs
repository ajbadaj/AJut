namespace AJut.Storage
{
    internal class StratabasePropertyChangeEventArgs : StratabaseChangeEventArgs
    {
        public object OldValue { get; init; } = null;
        public object NewValue { get; init; } = null;
    }
}
