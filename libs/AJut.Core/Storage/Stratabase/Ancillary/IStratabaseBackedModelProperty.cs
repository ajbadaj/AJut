namespace AJut.Storage
{
    using System;

    public interface IStrataBackedModelProperty : IDisposable
    {
        IStrataPropertyAccess Access { get; }
        string Name { get; }
        public event EventHandler<EventArgs> ValueChanged;
    }
}
