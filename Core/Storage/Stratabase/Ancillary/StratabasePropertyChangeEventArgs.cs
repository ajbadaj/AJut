namespace AJut.Storage
{
    using System;

    internal class StratabasePropertyChangeEventArgs : EventArgs
    {
        public StratabasePropertyChangeEventArgs (Guid itemId, int layerIndex, string property, object oldData, object newData)
        {
            this.ItemId = itemId;
            this.PropertyName = property;
            this.LayerIndex = layerIndex;
            this.IsBaseline = false;

            this.OldData = oldData;
            this.NewData = newData;
        }

        public StratabasePropertyChangeEventArgs (Guid itemId, string property, object oldData, object newData)
        {
            this.ItemId = itemId;
            this.PropertyName = property;
            this.LayerIndex = -1;
            this.IsBaseline = true;

            this.OldData = oldData;
            this.NewData = newData;
        }

        public Guid ItemId { get; }
        public string PropertyName { get; }
        public int LayerIndex { get; }
        public bool IsBaseline { get; }

        public object OldData { get; }
        public object NewData { get; }
    }
}
