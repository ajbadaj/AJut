namespace AJut.Storage
{
    using System;

    public class BaselineStratumModificationEventArgs
    {
        internal BaselineStratumModificationEventArgs (Guid itemId, string propertyName, object oldData, object newValue, bool wasPropertyRemoved)
        {
            this.ItemId = itemId;
            this.PropertyName = propertyName;
            this.OldData = oldData;
            this.NewData = newValue;
            this.WasPropertyRemoved = wasPropertyRemoved;
        }

        public Guid ItemId { get; }
        public string PropertyName { get; }
        public bool WasPropertyRemoved { get; }
        public object OldData { get; }
        public object NewData { get; }
    }

    public class OverrideStratumModificationEventArgs
    {
        internal OverrideStratumModificationEventArgs (int layerIndex, Guid itemId, string propertyName, object oldData, object newValue, bool wasPropertyRemoved)
        {
            this.LayerIndex = layerIndex;
            this.ItemId = itemId;
            this.PropertyName = propertyName;
            this.OldData = oldData;
            this.NewData = newValue;
            this.WasPropertyRemoved = wasPropertyRemoved;
        }

        public Guid ItemId { get; }
        public string PropertyName { get; }
        public bool WasPropertyRemoved { get; }
        public int LayerIndex { get; }
        public object OldData { get; }
        public object NewData { get; }
    }
}
