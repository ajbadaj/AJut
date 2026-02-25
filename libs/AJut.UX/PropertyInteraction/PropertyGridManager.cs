namespace AJut.UX.PropertyInteraction
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.Storage;

    public interface IPropertyGrid
    {
        public IEnumerable ItemsSource { get; }
        public object SingleItemSource { get; }
    }

    public class PropertyGridManager : IDisposable
    {
        private readonly IPropertyGrid m_propertyGrid;

        public PropertyGridManager (IPropertyGrid propertyGrid)
        {
            m_propertyGrid = propertyGrid;
        }

        /// <summary>
        /// The hidden root node whose children are the top-level property edit targets.
        /// Bind a FlatTreeListControl to this with IncludeRoot=false to get a proper
        /// expandable tree view of all properties (including sub-object properties).
        /// </summary>
        public PropertyEditTarget RootNode { get; private set; }

        /// <summary>
        /// Legacy flat-store access. The store's RootNode is the same as <see cref="RootNode"/>;
        /// IncludeRoot is false so the store contains only top-level items.
        /// </summary>
        public ObservableFlatTreeStore<PropertyEditTarget> Items { get; } = new ObservableFlatTreeStore<PropertyEditTarget>();

        public void Dispose ()
        {
            this.Items.Clear();
            this.RootNode = null;
        }

        public void RebuildEditTargets ()
        {
            this.Items.Clear();
            this.RootNode = null;

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

            var root = new PropertyEditTarget("$_root_", () => null, null);
            var editTargets = new Dictionary<int, PropertyEditTarget>();

            foreach (object item in sourceItems)
            {
                IEnumerable<PropertyEditTarget> targets;
                if (item is IPropertyEditManager propManager)
                {
                    targets = propManager.GenerateEditTargets();
                }
                else
                {
                    targets = PropertyEditTarget.GenerateForPropertiesOf(item);
                }

                foreach (PropertyEditTarget target in targets)
                {
                    _Add(target);
                }
            }

            foreach (PropertyEditTarget target in editTargets.Values)
            {
                target.Setup();
                root.InsertChild(root.Children.Count, target);
            }

            this.RootNode = root;
            this.Items.IncludeRoot = false;
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
