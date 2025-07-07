namespace AJut.UX.PropertyInteraction
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using AJut.Storage;

    public interface IPropertyGrid
    {
        public IEnumerable ItemsSource { get; }
        public object SingleItemSource { get; }
    }

    public class PropertyGridManager : IDisposable
    {
        private readonly IPropertyGrid m_propertyGrid;
        public PropertyGridManager(IPropertyGrid propertyGrid)
        {
            m_propertyGrid = propertyGrid;
        }

        public ObservableFlatTreeStore<PropertyEditTarget> Items { get; } = new ObservableFlatTreeStore<PropertyEditTarget>();

        public void Dispose ()
        {
            this.Items.Clear();
        }

        public void RebuildEditTargets()
        {
            this.Items.Clear();
            if (m_propertyGrid.ItemsSource == null && m_propertyGrid.SingleItemSource == null)
            {
                return;
            }

            IEnumerable sourceItems;
            if (m_propertyGrid.SingleItemSource != null)
            {
                sourceItems = Enumerable.Repeat(m_propertyGrid.SingleItemSource, 1);
            }
            else
            {
                sourceItems = m_propertyGrid.ItemsSource;
            }

            if (sourceItems == null)
            {
                return;
            }

            Dictionary<int, PropertyEditTarget> editTargets = new Dictionary<int, PropertyEditTarget>();
            foreach (object item in sourceItems)
            {
                if (item is IPropertyEditManager propManager)
                {
                    propManager.GenerateEditTargets().ForEach(_Add);
                }
                else
                {
                    PropertyEditTarget.GenerateForPropertiesOf(item).ForEach(_Add);
                }
            }

            var root = new PropertyEditTarget("$_root_", () => null, null);
            foreach (PropertyEditTarget target in editTargets.Values)
            {
                target.Setup();
                root.AddChild(target);
            }

            this.Items.RootNode = root;

            void _Add (PropertyEditTarget _target)
            {
                int _id = _target.GetHashCode();
                if (editTargets.TryGetValue(_id, out PropertyEditTarget _found))
                {
                    _found.TakeOn(_target);
                    return;
                }

                editTargets.Add(_id, _target);
            }
        }
    }
}
