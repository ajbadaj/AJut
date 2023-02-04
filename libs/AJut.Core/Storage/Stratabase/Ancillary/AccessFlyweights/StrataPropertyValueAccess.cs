namespace AJut.Storage
{
    /// <summary>
    /// Caches and tracks access information to make a simplified utility set for accessing an object's property stored in a <see cref="Stratabase"/>
    /// </summary>
    public class StrataPropertyValueAccess<TProperty> : StrataPropertyAccessBase<TProperty>, IStrataPropertyValueRetreiver<TProperty>, IStrataPropertyValueManipulator<TProperty>
    {
        // Invalid constructor
        internal protected StrataPropertyValueAccess () : base() { }

        internal StrataPropertyValueAccess (Stratabase.ObjectDataAccessManager owner, string propertyName)
            : base(owner, propertyName)
        {
        }

        public static StrataPropertyValueAccess<TProperty> Invalid { get; } = new InvalidStrataPropertyValueAccess();

        // ===============================[ Interface Methods ]=======================================

        public TProperty GetValue ()
        {
            if (!this.IsSet)
            {
                return default;
            }

            return this.SearchForFirstSetValue(this.ActiveLayerIndex, out TProperty value) ? value : default;
        }

        public bool SetBaselineValue (TProperty value) => this.ODAM.SetBaselineValue(this.PropertyName, value);
        public bool SetOverrideValue (int layerIndex, TProperty value) => this.ODAM.SetOverrideValue(layerIndex, this.PropertyName, value);

        public void ClearBaselineValue () => this.ODAM.ObliteratePropertyStorageInBaseline(this.PropertyName);
        public void ClearOverrideValue (int layerIndex) => this.ODAM.ObliteratePropertyStorageInLayer(layerIndex, this.PropertyName);

        private class InvalidStrataPropertyValueAccess : StrataPropertyValueAccess<TProperty> { }
    }
}
