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
            m_factory = factory;
            this.Reset();
        }

        public void Dispose ()
        {
            m_instanceStorage = null;
            this.Access.Dispose();
            this.Access = null;
        }

        public TAdaptedValue Value => m_instanceStorage.Value;
        public StrataPropertyValueAccess<TStrataValue> Access { get; private set; }

        private void Reset()
        {
            m_instanceStorage = new Lazy<TAdaptedValue>(_Constructor);

            TAdaptedValue _Constructor()
            {
                return m_factory(this.Access.ODAM.SB, this.Access.GetValue());
            }
        }
    }
}
