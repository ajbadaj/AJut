namespace AJut.Storage
{
    using System;

    /// <summary>
    /// Use this as a way to adapt property values stored in the stratabase to a converted, adapted value. You might store
    /// a string in the stratabase but want that adapted to a color by the adapter. Most commonly I expect this as a reference
    /// adapter where a property is stored as a <see cref="Guid"/> in the stratabase for a "Sub object" and the adapter would
    /// interpret this into some kind of strata property access based model.
    /// </summary>
    /// <typeparam name="TStrataValue">The type of value stored in the <see cref="Stratabase"/></typeparam>
    /// <typeparam name=" TAdaptedValue">The type of value adapted to</typeparam>
    public class StrataPropertyAdapter<TStrataValue, TAdaptedValue> : IDisposable
    {
        public delegate TAdaptedValue ConvertAccessToOutput (Stratabase sb, TStrataValue accessValue);

        private readonly ConvertAccessToOutput m_factory;
        private Lazy<TAdaptedValue> m_instanceStorage;

        public StrataPropertyAdapter (Stratabase sb, Guid itemId, string propertyName, ConvertAccessToOutput factory)
            : this(sb.GeneratePropertyAccess<TStrataValue>(itemId, propertyName), factory)
        {
        }

        public StrataPropertyAdapter (StrataPropertyValueAccess<TStrataValue> access, ConvertAccessToOutput factory)
        {
            this.Access = access;
            this.Access.ValueChanged += this.OnAccessValueChanged;
            m_factory = factory;
            this.Reset();
        }

        private void OnAccessValueChanged (object sender, EventArgs e)
        {
            this.Reset();
        }

        public void Dispose ()
        {
            this.Access.ValueChanged -= this.OnAccessValueChanged;
            this.Access.Dispose();
            this.Access = null;
            this.ClearInstanceStorage();
            m_instanceStorage = null;
        }

        public event EventHandler<EventArgs> ValueChanged;

        public TAdaptedValue Value => this.Access.IsSet ? m_instanceStorage.Value : default;
        public StrataPropertyValueAccess<TStrataValue> Access { get; private set; }

        private void ClearInstanceStorage ()
        {
            if (m_instanceStorage?.IsValueCreated == true && m_instanceStorage.Value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private void Reset()
        {
            this.ClearInstanceStorage();
            m_instanceStorage = new Lazy<TAdaptedValue>(_Constructor);
            this.ValueChanged?.Invoke(this, EventArgs.Empty);

            TAdaptedValue _Constructor()
            {
                return m_factory(this.Access.ODAM.SB, this.Access.GetValue());
            }
        }
    }
}
