namespace AJut.UX.PropertyInteraction
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Windows.Input;
    using AJut.Storage;
    using AJut.TypeManagement;

    /// <summary>
    /// Context object for a list property in the PropertyGrid. Carries the add/remove/reorder
    /// capabilities and element count. Set as EditContext on the list parent PropertyEditTarget.
    /// </summary>
    public class PropertyGridListContext : NotifyPropertyChanged
    {
        // ===========[ Instance fields ]==========================================
        private readonly object m_sourceItem;
        private readonly PropertyInfo m_property;
        private readonly PGListAttribute m_attribute;
        private readonly Action m_rebuildChildren;
        private int m_elementCount;

        // ===========[ Construction ]=============================================
        public PropertyGridListContext (
            object sourceItem,
            PropertyInfo property,
            Type elementType,
            PGListAttribute attribute,
            Action rebuildChildren)
        {
            m_sourceItem = sourceItem;
            m_property = property;
            m_attribute = attribute;
            m_rebuildChildren = rebuildChildren;

            this.ElementType = elementType;
            this.CanAdd = attribute.CanAdd;
            this.CanRemove = attribute.CanRemove;
            this.CanReorder = attribute.CanReorder && _IsIndexedCollection();

            this.AddCommand = new ActionCommand(this.AddElement);
            this.RefreshElementCount();
        }

        // ===========[ Properties ]==========================================

        public Type ElementType { get; }
        public bool CanAdd { get; }
        public bool CanRemove { get; }
        public bool CanReorder { get; }
        public ICommand AddCommand { get; }

        public int ElementCount
        {
            get => m_elementCount;
            private set => this.SetAndRaiseIfChanged(ref m_elementCount, value);
        }

        // ===========[ Public Interface Methods ]==========================================

        public void AddElement ()
        {
            if (!this.CanAdd)
            {
                return;
            }

            object collection = m_property.GetValue(m_sourceItem);
            if (collection == null)
            {
                return;
            }

            // 1. Try custom add method
            if (!string.IsNullOrEmpty(m_attribute.AddMethodName))
            {
                MethodInfo method = m_sourceItem.GetType().GetMethod(
                    m_attribute.AddMethodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
                );

                if (method != null)
                {
                    method.Invoke(method.IsStatic ? null : m_sourceItem, null);
                    this.OnCollectionModified();
                    return;
                }

                Logger.LogError($"PGList: AddMethodName '{m_attribute.AddMethodName}' not found on '{m_sourceItem.GetType().Name}'");
                return;
            }

            // 2. Auto-add for arrays (create new array, copy, set)
            if (m_property.PropertyType.IsArray)
            {
                _AddToArray(collection);
                return;
            }

            // 3. Auto-add for IList (List<T>, etc.)
            if (collection is IList list)
            {
                object newElement = AJutActivator.CreateInstanceOf(this.ElementType);
                list.Add(newElement);
                this.OnCollectionModified();
                return;
            }

            // 4. Try generic ICollection<T>.Add via reflection
            Type genericCollectionType = _FindGenericInterface(collection.GetType(), typeof(ICollection<>));
            if (genericCollectionType != null)
            {
                MethodInfo addMethod = genericCollectionType.GetMethod("Add");
                if (addMethod != null)
                {
                    object newElement = AJutActivator.CreateInstanceOf(this.ElementType);
                    addMethod.Invoke(collection, new[] { newElement });
                    this.OnCollectionModified();
                    return;
                }
            }

            Logger.LogError($"PGList: Cannot auto-add to collection type '{collection.GetType().Name}' - provide an AddMethodName");
        }

        public void RemoveElementAt (int index)
        {
            if (!this.CanRemove)
            {
                return;
            }

            object collection = m_property.GetValue(m_sourceItem);
            if (collection == null)
            {
                return;
            }

            // 1. Try custom remove method
            if (!string.IsNullOrEmpty(m_attribute.RemoveMethodName))
            {
                _InvokeCustomRemove(collection, index);
                return;
            }

            // 2. Auto-remove for arrays
            if (m_property.PropertyType.IsArray)
            {
                _RemoveFromArray(collection, index);
                return;
            }

            // 3. Auto-remove for IList
            if (collection is IList list && index >= 0 && index < list.Count)
            {
                list.RemoveAt(index);
                this.OnCollectionModified();
                return;
            }

            Logger.LogError($"PGList: Cannot auto-remove from collection type '{collection.GetType().Name}' - provide a RemoveMethodName");
        }

        public void MoveElement (int fromIndex, int toIndex)
        {
            if (!this.CanReorder)
            {
                return;
            }

            object collection = m_property.GetValue(m_sourceItem);
            if (collection == null)
            {
                return;
            }

            // Validate custom predicate
            if (!string.IsNullOrEmpty(m_attribute.AcceptReorderMethodName))
            {
                if (!_EvaluateReorderPredicate(fromIndex, toIndex))
                {
                    return;
                }
            }

            // Arrays: create new array with element moved
            if (m_property.PropertyType.IsArray)
            {
                _MoveInArray(collection, fromIndex, toIndex);
                return;
            }

            // IList: remove and re-insert
            if (collection is IList list)
            {
                if (fromIndex < 0 || fromIndex >= list.Count || toIndex < 0 || toIndex >= list.Count)
                {
                    return;
                }

                object item = list[fromIndex];
                list.RemoveAt(fromIndex);
                list.Insert(toIndex, item);
                this.OnCollectionModified();
                return;
            }

            Logger.LogError($"PGList: Cannot reorder collection type '{collection.GetType().Name}'");
        }

        public ICommand CreateRemoveCommand (int index)
        {
            int capturedIndex = index;
            return new ActionCommand(() => this.RemoveElementAt(capturedIndex));
        }

        public void RefreshElementCount ()
        {
            object collection = m_property.GetValue(m_sourceItem);
            this.ElementCount = _GetCount(collection);
        }

        public object GetCollection () => m_property.GetValue(m_sourceItem);

        /// Called when the backing collection is replaced externally (not through
        /// Add/Remove/Reorder). Re-reads the collection from the source property
        /// and rebuilds the child targets to match.
        public void OnExternalCollectionReplaced ()
        {
            this.OnCollectionModified();
        }

        // ===========[ Private helpers ]==========================================

        private void OnCollectionModified ()
        {
            this.RefreshElementCount();
            m_rebuildChildren?.Invoke();
        }

        private bool _IsIndexedCollection ()
        {
            Type propType = m_property.PropertyType;
            if (propType.IsArray)
            {
                return true;
            }

            if (typeof(IList).IsAssignableFrom(propType))
            {
                return true;
            }

            return false;
        }

        private void _AddToArray (object currentArray)
        {
            Array arr = (Array)currentArray;
            int newLength = arr.Length + 1;
            Array newArr = Array.CreateInstance(this.ElementType, newLength);
            Array.Copy(arr, newArr, arr.Length);
            newArr.SetValue(AJutActivator.CreateInstanceOf(this.ElementType), arr.Length);
            m_property.SetValue(m_sourceItem, newArr);
            this.OnCollectionModified();
        }

        private void _RemoveFromArray (object currentArray, int index)
        {
            Array arr = (Array)currentArray;
            if (index < 0 || index >= arr.Length)
            {
                return;
            }

            int newLength = arr.Length - 1;
            Array newArr = Array.CreateInstance(this.ElementType, newLength);

            // Copy before index
            if (index > 0)
            {
                Array.Copy(arr, 0, newArr, 0, index);
            }

            // Copy after index
            if (index < arr.Length - 1)
            {
                Array.Copy(arr, index + 1, newArr, index, arr.Length - index - 1);
            }

            m_property.SetValue(m_sourceItem, newArr);
            this.OnCollectionModified();
        }

        private void _MoveInArray (object currentArray, int fromIndex, int toIndex)
        {
            Array arr = (Array)currentArray;
            if (fromIndex < 0 || fromIndex >= arr.Length || toIndex < 0 || toIndex >= arr.Length)
            {
                return;
            }

            // Convert to list, move, convert back
            var list = new List<object>(arr.Length);
            for (int i = 0; i < arr.Length; ++i)
            {
                list.Add(arr.GetValue(i));
            }

            object item = list[fromIndex];
            list.RemoveAt(fromIndex);
            list.Insert(toIndex, item);

            Array newArr = Array.CreateInstance(this.ElementType, arr.Length);
            for (int i = 0; i < list.Count; ++i)
            {
                newArr.SetValue(list[i], i);
            }

            m_property.SetValue(m_sourceItem, newArr);
            this.OnCollectionModified();
        }

        private void _InvokeCustomRemove (object collection, int index)
        {
            Type sourceType = m_sourceItem.GetType();

            // Try (int index) signature first
            MethodInfo method = sourceType.GetMethod(
                m_attribute.RemoveMethodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                null,
                new[] { typeof(int) },
                null
            );

            if (method != null)
            {
                method.Invoke(method.IsStatic ? null : m_sourceItem, new object[] { index });
                this.OnCollectionModified();
                return;
            }

            // Try (object element) signature
            if (collection is IList list && index >= 0 && index < list.Count)
            {
                object element = list[index];
                method = sourceType.GetMethod(
                    m_attribute.RemoveMethodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
                );

                if (method != null)
                {
                    method.Invoke(method.IsStatic ? null : m_sourceItem, new[] { element });
                    this.OnCollectionModified();
                    return;
                }
            }

            Logger.LogError($"PGList: RemoveMethodName '{m_attribute.RemoveMethodName}' not found on '{sourceType.Name}'");
        }

        private bool _EvaluateReorderPredicate (int fromIndex, int toIndex)
        {
            MethodInfo method = m_sourceItem.GetType().GetMethod(
                m_attribute.AcceptReorderMethodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(int) },
                null
            );

            if (method != null)
            {
                object result = method.Invoke(method.IsStatic ? null : m_sourceItem, new object[] { fromIndex, toIndex });
                return result is bool b && b;
            }

            Logger.LogError($"PGList: AcceptReorderMethodName '{m_attribute.AcceptReorderMethodName}' not found on '{m_sourceItem.GetType().Name}'");
            return false;
        }

        private static int _GetCount (object collection)
        {
            if (collection == null)
            {
                return 0;
            }

            if (collection is ICollection c)
            {
                return c.Count;
            }

            if (collection is Array arr)
            {
                return arr.Length;
            }

            // Fallback: enumerate
            int count = 0;
            foreach (object _ in (IEnumerable)collection)
            {
                ++count;
            }

            return count;
        }

        private static Type _FindGenericInterface (Type type, Type genericInterfaceDefinition)
        {
            foreach (Type iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == genericInterfaceDefinition)
                {
                    return iface;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Context for a list element child target. Carries the remove command and
    /// the element's index within the parent list.
    /// </summary>
    public class PropertyGridListElementContext
    {
        public PropertyGridListElementContext (PropertyGridListContext parentListContext, int index)
        {
            this.ParentListContext = parentListContext;
            this.Index = index;
            this.RemoveCommand = parentListContext.CanRemove
                ? parentListContext.CreateRemoveCommand(index)
                : null;
        }

        public PropertyGridListContext ParentListContext { get; }
        public int Index { get; }
        public ICommand RemoveCommand { get; }
        public bool CanRemove => this.RemoveCommand != null;
    }
}
