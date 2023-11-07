namespace AJut.Storage
{
    using System;

    public interface IStrataPropertyAccess : IDisposable
    {
        event EventHandler<EventArgs> IsBaselineSetChanged;
        event EventHandler<EventArgs> IsSetChanged;
        event EventHandler<EventArgs> ValueChanged;

        string PropertyName { get; }
        bool IsBaselineSet { get; }
        bool IsSet { get; }
        int ActiveLayerIndex { get; }
        bool IsActiveLayerBaseline => this.ActiveLayerIndex == Stratabase.kActiveLayerBaseline;
    }
}
