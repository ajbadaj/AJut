namespace AJut.Storage
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public delegate bool ObjectEqualityTester (object o1, object o2);

    /// <summary>
    /// A layered storage container of property values. Property groups (simply called "objects") are unified under a single <see cref="Guid"/> id. The property
    /// data for each object is stored in one baseline layer, and a variable number (set at construction time) of override layers (from 0-<see cref="OverrideLayerCount"/>).
    /// The highest stored value, which is to say the largest index of override layer, down to baseline (effectively index -1) is the effective value for a given property.
    /// To simplify access, flyweights are available with <see cref="IStrataPropertyAccess"/> instances - in addition property adapters are also available for automatic conversion
    /// of storage value to used value (for instance maybe you store a string, but you use an enum).
    /// </summary>
    public sealed partial class Stratabase
    {
        private readonly Stratum m_baselineStorageLayer;
        private readonly Stratum[] m_overrideStorageLayers;
        private readonly Dictionary<Guid, ObjectDataAccessManager> m_objectAccess = new Dictionary<Guid, ObjectDataAccessManager>();

        // ===============================[ Construction ]=====================================

        /// <summary>
        /// Construct a stratabase with the number of override layers specified. The number of override layers cannot be 
        /// changed after construction. Result will be a stratabase with 1 baseline laye and {<paramref name="overrideLayerCount"/>} 
        /// override layers
        /// </summary>
        public Stratabase (int overrideLayerCount)
        {
            this.OverrideLayerCount = overrideLayerCount;
            m_overrideStorageLayers = new Stratum[overrideLayerCount];

            m_baselineStorageLayer = new Stratum();
            for (int layer = 0; layer < overrideLayerCount; ++layer)
            {
                m_overrideStorageLayers[layer] = new Stratum();
            }
        }

        // =================================[ Events ]=========================================

        /// <summary>
        /// Indicates that data in the baseline layer has changed
        /// </summary>
        public event EventHandler<BaselineStratumModificationEventArgs> BaselineDataChanged;

        /// <summary>
        /// Indicates that data in the override layer has changed
        /// </summary>
        public event EventHandler<OverrideStratumModificationEventArgs> OverrideDataChanged;

        // ==========================[ Public Properties ]=====================================

        /// <summary>
        /// How many override layers does this <see cref="Stratabase"/> bave
        /// </summary>
        public int OverrideLayerCount { get; }

        /// <summary>
        /// The function that the <see cref="Stratabase"/> uses to determine property equality, can be reset at any time,
        /// though only future changes will use new equality tester.
        /// </summary>
        public ObjectEqualityTester ValueEqualityTester { get; set; } = (o1, o2) => o1?.Equals(o2) ?? false;

        // ====================================================================================
        // ===========================[ Public Methods ]=======================================
        // ====================================================================================

        /// <summary>
        /// Check to see if the <see cref="Stratabase"/> is currently tracking an object with the given id
        /// </summary>
        public bool Contains (Guid id)
        {
            if (m_baselineStorageLayer.ContainsKey(id))
            {
                return true;
            }

            for (int overrideLayerIndex = 0; overrideLayerIndex < m_overrideStorageLayers.Length; ++overrideLayerIndex)
            {
                if (m_overrideStorageLayers[overrideLayerIndex].ContainsKey(id))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Looks up all property names registered at the baseline layer for a given target
        /// </summary>
        public IEnumerable<string> GetAllBaselinePropertiesFor (Guid targetId)
        {
            if (m_baselineStorageLayer.TryGetValue(targetId, out PseudoPropertyBag propbag))
            {
                return propbag.m_storage.Keys;
            }

            return Enumerable.Empty<string>();
        }

        // ----------------- Clear -----------------

        /// <summary>
        /// Clear the entire <see cref="Stratabase"/>, optionally notify of the removals
        /// </summary>
        public void ClearAll (bool notifyOfRemovals = true)
        {
            foreach (var odam in m_objectAccess.Values.ToList())
            {
                odam.ClearAll(notifyOfRemovals);
            }
        }

        /// <summary>
        /// Clear the <see cref="Stratabase"/> of all properties associated to the given <paramref name="id"/>, optionally notify of the removals
        /// </summary>
        public void ClearAllFor (Guid id, bool notifyOfRemovals = true)
        {
            this.GetAccessManager(id)?.ClearAll(notifyOfRemovals);
        }

        /// <summary>
        /// Clear a property set on the baseline.
        /// </summary>
        /// <returns>True if the property was found and cleared, false otherwise.</returns>
        public bool ClearPropertyBaseline (Guid id, string property)
        {
            return this.EnsureDataAccess(id).ClearBaselinePropertyValue(property);
        }

        /// <summary>
        /// Clear a property set on an override layer.
        /// </summary>
        /// <returns>True if the property was found and cleared, false otherwise.</returns>
        public bool ClearPropertyOverride (int layer, Guid id, string property)
        {
            return this.EnsureDataAccess(id).ClearOverridePropertyValue(layer, property);
        }

        // ----------------- Set From Properties of Object -----------------

        /// <summary>
        /// Extract the properties of <paramref name="data"/> and set baseline values with the result. Id must be specified by the object via
        /// the <see cref="StratabaseIdAttribute"/> or the operation will fail.
        /// </summary>
        public bool SetBaselineFromPropertiesOf (object data)
        {
            return this.SetBaselineFromPropertiesOf(null, data);
        }

        /// <summary>
        /// Extract the properties of <paramref name="data"/> and set baseline values with the result. Id must be specified in the parameter or must be 
        /// specified by the object via the <see cref="StratabaseIdAttribute"/> or the operation will fail.
        /// </summary>
        public bool SetBaselineFromPropertiesOf (Guid? id, object data)
        {
            var objectEvaluation = ObjectEvaluation.Generate(id, null, data);
            if (objectEvaluation == null)
            {
                return false;
            }

            this.SetBaselineData(objectEvaluation);
            return true;
        }

        /// <summary>
        /// Pull properties from the Stratabase from the object with the matching <paramref name="objectId"/> and set the passed in object's properties with the highest order stratabase values
        /// </summary>
        public void SetObjectWithProperties<T> (Guid objectId, ref T source)
        {
            object objCasted = source;
            SetObjectWithProperties(objectId, ref objCasted);
        }

        private object CreateAndSetObjectWith (Type targetPropType, Guid targetId)
        {
            object childObj = null;
            var constructor = targetPropType.GetConstructors().Where(c => c.IsTaggedWithAttribute<StratabaseIdConstructorAttribute>() && c.GetParameters().Length == 1 && c.GetParameters()[0].ParameterType == typeof(Guid)).FirstOrDefault();
            if (constructor != null)
            {
                childObj = constructor.Invoke(new object[] { targetId });
            }
            else
            {
                childObj = Activator.CreateInstance(targetPropType);

                // Resolve setting the id
                var classId = targetPropType.GetAttributes<StratabaseIdAttribute>().FirstOrDefault();
                if (classId != null)
                {
                    _SetIdIfPossible(childObj.GetType().GetProperty(classId.PropertyName));
                }
                else
                {
                    _SetIdIfPossible(targetPropType.GetProperties().FirstOrDefault(p => p.IsTaggedWithAttribute<StratabaseIdAttribute>()));
                }

                void _SetIdIfPossible (PropertyInfo _idProp)
                {
                    if (_idProp?.CanWrite ?? false)
                    {
                        _idProp.SetValue(childObj, targetId);
                    }
                }
            }

            this.SetObjectWithProperties(targetId, ref childObj);
            return childObj;
        }

        /// <summary>
        /// Pull properties from the Stratabase from the object with the matching <paramref name="objectId"/> and set the passed in object's properties with the highest order stratabase values
        /// </summary>
        public void SetObjectWithProperties (Guid objectId, ref object source)
        {
            ObjectDataAccessManager odam = this.GetAccessManager(objectId);
            if (odam == null)
            {
                return;
            }

            var baseline = this.GetBaselinePropertyBagFor(objectId);
            foreach (string propPath in baseline.Keys)
            {
                if (!odam.SearchForFirstSetValue(m_overrideStorageLayers.Length - 1, propPath, out object storedPropValue))
                {
                    continue;
                }

                PropertyInfo targetProp = source.GetComplexProperty(propPath, out object target);

                // Do references differently
                if (storedPropValue is Guid targetId && targetProp.IsTaggedWithAttribute<StratabaseReferenceAttribute>())
                {
                    targetProp.SetValue(target, this.CreateAndSetObjectWith(targetProp.PropertyType, targetId));
                }

                // Go through this BONKERS process to determine if it's list access backed data and set it that way
                else if (targetProp.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(targetProp.PropertyType))
                {
                    var strataListConfig = targetProp.GetAttributes<StrataListConfigAttribute>().FirstOrDefault();
                    
                    // Is it ListAccess backed?
                    Type storedType = storedPropValue.GetType();
                    if (strataListConfig != null
                        && storedType.IsGenericType && storedType.GetGenericTypeDefinition() == typeof(ObservableCollectionX<>)
                        && storedType.GenericTypeArguments[0].IsGenericType
                        && storedType.GenericTypeArguments[0].GetGenericTypeDefinition() == typeof(StratabaseListInsertion<>))
                    {
                        // Ok it is, now use reflection to generate a StrataPropertyListAccess
                        Type itemType = storedType.GenericTypeArguments[0].GenericTypeArguments[0];
                        var listAccess = (IDisposable)this.InvokeTemplatedMethod(
                            nameof(GenerateListPropertyAccess),
                            itemType, objectId, propPath
                        );

                        // If we succeeded in making a list access, then grab the cache and try to set the value
                        if (listAccess != null)
                        {
                            IList elements = listAccess.GetComplexPropertyValue<IList>(nameof(StrataPropertyListAccess<int>.Elements));
                            if (elements != null)
                            {
                                // Here's the tricky bit, we either are setting an array - or - we're setting something we assume
                                //  activator.create instance can make for us, is an IList, which we can then add the items to.
                                if (targetProp.PropertyType.IsArray)
                                {
                                    Type elementType = targetProp.PropertyType.GenericTypeArguments[0];
                                    Array final = Array.CreateInstance(elementType, elements.Count);
                                    for (int index = 0; index < elements.Count; ++index)
                                    {
                                        final.SetValue(_ConvertElementToOutputElement(elementType, elements[index]), index);
                                    }

                                    targetProp.SetValueExtended(source, propPath, target, final);
                                }
                                else if (typeof(IList).IsAssignableFrom(targetProp.PropertyType))
                                {
                                    Type elementType = targetProp.PropertyType.GenericTypeArguments[0];
                                    IList final = (IList)Activator.CreateInstance(targetProp.PropertyType);
                                    foreach (object obj in elements)
                                    {
                                        final.Add(_ConvertElementToOutputElement(elementType, obj));
                                    }

                                    targetProp.SetValueExtended(source, propPath, target, final);
                                }

                                // We ...might... be dealing with list references, in which case we need to convert guid back to final form.
                                object _ConvertElementToOutputElement (Type _elementType, object _element)
                                {
                                    if (_element.GetType() == typeof(Guid) && strataListConfig.BuildReferenceList)
                                    {
                                        return this.CreateAndSetObjectWith(_elementType, (Guid)_element);
                                    }
                                    
                                    return _element;
                                }
                            }

                            // Don't forget to dispose of hte list access
                            listAccess.Dispose();
                            return;
                        }
                    }
                }
                else
                {
                    targetProp.SetValueExtended(source, propPath, target, storedPropValue);
                }
            }
        }

        /// <summary>
        /// Determine the object id as you would serializtion from the passed in object, then pull properties from the Stratabase with the matching id and set the passed in object's properties with the highest order stratabase values.
        /// </summary>
        public void SetObjectWithProperties<T> (ref T source)
        {
            if (source == null)
            {
                return;
            }

            if (!ObjectEvaluation.DetermineObjectId(source, out Guid id))
            {
                throw new ArgumentException($"Object of type {source.GetType().Name} passed in to {nameof(SetObjectWithProperties)} was not assigned a StratabaseId in it's class layout, nor passed in with a guid.");
            }

            this.SetObjectWithProperties(id, ref source);
        }

        // ----------------- Set Property Value -----------------

        /// <summary>
        /// Directly set an object (indicated by id) property (indicated by property name) on the baseline layer with the value specified.
        /// </summary>
        public bool SetBaselinePropertyValue (Guid id, string property, object value)
        {
            return EnsureDataAccess(id).SetBaselineValue(property, value);
        }

        /// <summary>
        /// Directly set an object (indicated by id) property (indicated by property name) on the override layer (0-<see cref="OverrideLayerCount"/>) with the value specified.
        /// </summary>
        public bool SetOverridePropertyValue (int layer, Guid id, string property, object value)
        {
            if (!m_objectAccess.TryGetValue(id, out ObjectDataAccessManager accessManager))
            {
                accessManager = new ObjectDataAccessManager(this, id);
            }

            return accessManager.SetOverrideValue(layer, property, value);
        }

        // ----------------- Get Property Value -----------------

        /// <summary>
        /// Try and extract a baseline property value for a given object id (typed)
        /// </summary>
        public bool TryGetBaselinePropertyValue<T> (Guid id, string property, out T value)
        {
            value = default;
            return this.GetAccessManager(id)?.TryGetBaselineValue(property, out value) == true;
        }

        /// <summary>
        /// Try and extract a baseline property value for a given object id (untyped)
        /// </summary>
        public bool TryGetBaselinePropertyValue (Guid id, string property, out object value)
        {
            return TryGetBaselinePropertyValue<object>(id, property, out value);
        }

        /// <summary>
        /// Try and extract an override property value for a given object id (typed)
        /// </summary>
        public bool TryGetOverridePropertyValue<T> (int layer, Guid id, string property, out T value)
        {
            value = default;
            return this.GetAccessManager(id)?.TryGetOverrideValue(layer, property, out value) == true;
        }

        /// <summary>
        /// Try and extract an override property value for a given object id (untyped)
        /// </summary>
        public bool TryGetOverridePropertyValue (int layer, Guid id, string property, out object value)
        {
            return TryGetOverridePropertyValue<object>(layer, id, property, out value);
        }

        // ------- Generate Property Access ----------

        /// <summary>
        /// Generate a property acces for the given object (id) and property (propertyName)
        /// </summary>
        public StrataPropertyValueAccess<TProperty> GeneratePropertyAccess<TProperty> (Guid id, string propertyName)
        {
            return EnsureDataAccess(id).GeneratePropertyValueAccess<TProperty>(propertyName);
        }

        /// <summary>
        /// Generate a property acces for a list for the given object (id) and property (propertyName). If you want lists stored directly in each layer, do not use <see cref="StrataPropertyListAccess{TElement}"/>.
        /// If you want a singluar cohesive list where insertions can be specified on any layer, the only way of doing so is by using the <see cref="StrataPropertyListAccess{TElement}"/>.
        /// </summary>
        public StrataPropertyListAccess<TElement> GenerateListPropertyAccess<TElement> (Guid id, string propertyName)
        {
            return EnsureDataAccess(id).GenerateListPropertyAccess<TElement>(propertyName);
        }

        // ===========================[ Utility Methods ]======================================

        internal ObjectDataAccessManager GetAccessManager (Guid id)
        {
            return m_objectAccess.TryGetValue(id, out ObjectDataAccessManager accessManager) ? accessManager : null;
        }

        internal ObjectDataAccessManager EnsureDataAccess (Guid id)
        {
            if (!m_objectAccess.TryGetValue(id, out ObjectDataAccessManager accessManager))
            {
                accessManager = new ObjectDataAccessManager(this, id);
            }

            return accessManager;
        }

        internal PseudoPropertyBag GetBaselinePropertyBagFor (Guid id)
        {
            if (!m_baselineStorageLayer.TryGetValue(id, out PseudoPropertyBag propertyBag))
            {
                propertyBag = new PseudoPropertyBag(this);
                m_baselineStorageLayer.Add(id, propertyBag);
            }

            return propertyBag;
        }

        internal PseudoPropertyBag GetOverridePropertyBagFor (int overrideLayer, Guid id)
        {
            if (!m_overrideStorageLayers[overrideLayer].TryGetValue(id, out PseudoPropertyBag propertyBag))
            {
                propertyBag = new PseudoPropertyBag(this);
                m_overrideStorageLayers[overrideLayer].Add(id, propertyBag);
            }

            return propertyBag;
        }

        private void OnObjectDataAccessManagerAdded (ObjectDataAccessManager odam)
        {
            m_objectAccess.Add(odam.Id, odam);
            odam.LayerDataRemoved -= this.Odam_LayerDataRemoved;
            odam.LayerDataRemoved += this.Odam_LayerDataRemoved;
            
            odam.LayerDataSet -= this.Odam_LayerDataSet;
            odam.LayerDataSet += this.Odam_LayerDataSet;
        }

        private void OnObjectDataAccessManagerRemoved (ObjectDataAccessManager odam)
        {
            m_objectAccess.Remove(odam.Id);
            odam.LayerDataRemoved -= this.Odam_LayerDataRemoved;
            odam.LayerDataSet -= this.Odam_LayerDataSet;
        }

        private void Odam_LayerDataSet (object sender, StratabasePropertyChangeEventArgs e)
        {
            if (e.IsBaseline)
            {
                this.BaselineDataChanged?.Invoke(this, new BaselineStratumModificationEventArgs(e.ItemId, e.PropertyName, e.OldData, e.NewData, false));
            }
            else
            {
                this.OverrideDataChanged?.Invoke(this, new OverrideStratumModificationEventArgs(e.LayerIndex, e.ItemId, e.PropertyName, e.OldData, e.NewData, false));
            }
        }

        private void Odam_LayerDataRemoved (object sender, StratabasePropertyChangeEventArgs e)
        {
            if (e.IsBaseline)
            {
                this.BaselineDataChanged?.Invoke(this, new BaselineStratumModificationEventArgs(e.ItemId, e.PropertyName, e.OldData, null, true));
            }
            else
            {
                this.OverrideDataChanged?.Invoke(this, new OverrideStratumModificationEventArgs(e.LayerIndex, e.ItemId, e.PropertyName, e.OldData, null, true));
            }
        }

        // ===========================[ Utility Classes ]======================================

        /// <summary>
        /// ALL CHANGES GO THROUGH HERE! This ensures proper routing of events. Stores properties for a given object (indicated by <see cref="Id"/>).
        /// </summary>
        internal class ObjectDataAccessManager
        {
            public ObjectDataAccessManager (Stratabase stratabase, Guid id)
            {
                this.SB = stratabase;
                this.Id = id;

                this.SB.OnObjectDataAccessManagerAdded(this);
            }

            internal event EventHandler<StratabasePropertyChangeEventArgs> LayerDataSet;
            internal event EventHandler<StratabasePropertyChangeEventArgs> LayerDataRemoved;
            
            public Guid Id { get; }
            public Stratabase SB { get; }

            /// <summary>
            /// Determines the active layer set, if any
            /// </summary>
            /// <param name="startingTestLayerIndex">What override layer to start looking at, or -1 for baseline</param>
            /// <param name="propertyName">The property to look for</param>
            /// <param name="activeLayer">The active layer found or -1 for Baseline (or not set of return is false)</param>
            /// <returns>True if the active layer is found, false if the property is not set</returns>
            public bool TryFindActiveLayer (string propertyName, out int activeLayer)
            {
                return this.TryFindActiveLayer(this.SB.OverrideLayerCount - 1, propertyName, out activeLayer);
            }

            /// <summary>
            /// Determines the active layer set, if any
            /// </summary>
            /// <param name="startingTestLayerIndex">What override layer to start looking at, or -1 for baseline</param>
            /// <param name="propertyName">The property to look for</param>
            /// <param name="activeLayer">The active layer found or -1 for Baseline (or not set of return is false)</param>
            /// <returns>True if the active layer is found, false if the property is not set</returns>
            public bool TryFindActiveLayer (int startingTestLayerIndex, string propertyName, out int activeLayer)
            {
                if (this.SearchForFirstOverrideLayerIndexSet(startingTestLayerIndex, propertyName, out activeLayer))
                {
                    return true;
                }
                else if (this.HasBaselineValueSet(propertyName))
                {
                    activeLayer = -1;
                    return true;
                }

                activeLayer = -2;
                return false;
            }

            public bool ClearBaselinePropertyValue (string property)
            {
                if (this.SB.GetBaselinePropertyBagFor(this.Id).RemoveValue(property, out object oldValue))
                {
                    this.LayerDataRemoved?.Invoke(this, new StratabasePropertyChangeEventArgs(this.Id, property, oldValue, null));
                    return true;
                }

                return false;
            }

            public bool ClearOverridePropertyValue (int overrideLayer, string property)
            {
                if (this.SB.GetOverridePropertyBagFor(overrideLayer, this.Id).RemoveValue(property, out object oldValue))
                {
                    this.LayerDataRemoved?.Invoke(this, new StratabasePropertyChangeEventArgs(this.Id, overrideLayer, property, oldValue, null));
                    return true;
                }

                return false;
            }

            // ------------- Generate PA ----------------

            public StrataPropertyValueAccess<TProp> GeneratePropertyValueAccess<TProp> (string propertyName)
            {
                return new StrataPropertyValueAccess<TProp>(this, propertyName);
            }

            public StrataPropertyListAccess<TElement> GenerateListPropertyAccess<TElement> (string propertyName)
            {
                return new StrataPropertyListAccess<TElement>(this, propertyName);
            }

            // ------------- Set Value ----------------

            public bool SetBaselineValue (string property, object newValue)
            {
                if (this.SB.GetBaselinePropertyBagFor(this.Id).SetValue(property, newValue, out object oldValue))
                {
                    this.LayerDataSet?.Invoke(this, new StratabasePropertyChangeEventArgs(this.Id, property, oldValue, newValue));
                    return true;
                }

                return false;
            }

            public bool SetOverrideValue (int overrideLayerIndex, string property, object newValue)
            {
                if (this.SB.GetOverridePropertyBagFor(overrideLayerIndex, this.Id).SetValue(property, newValue, out object oldValue))
                {
                    this.LayerDataSet?.Invoke(this, new StratabasePropertyChangeEventArgs(this.Id, overrideLayerIndex, property,oldValue, newValue));
                    return true;
                }

                return false;
            }

            // ------------- Get Value ----------------

            public bool TryGetBaselineValue<T> (string property, out T value)
            {
                var propBag = this.SB.GetBaselinePropertyBagFor(this.Id);
                if (propBag != null && propBag.TryGetValue(property, out object v))
                {
                    value = (T)v;
                    return true;
                }

                value = default;
                return false;
            }

            public bool SearchForFirstSetValue<T> (int layerStartIndex, string property, out T value)
            {
                int layer = layerStartIndex;
                while (layer >= 0)
                {
                    if (this.TryGetOverrideValue(layer--, property, out value))
                    {
                        return true;
                    }
                }
                
                if (this.TryGetBaselineValue(property, out value))
                {
                    return true;
                }

                value = default;
                return false;
            }

            public bool SearchForFirstOverrideLayerIndexSet (int layerStartIndex, string property, out int overrideLayerIndex)
            {
                int layer = layerStartIndex;
                while (layer >= 0)
                {
                    if (this.HasOverrideValueSet(layer, property))
                    {
                        overrideLayerIndex = layer;
                        return true;
                    }

                    --layer;
                }

                overrideLayerIndex = -2;
                return false;
            }

            public bool TryGetOverrideValue<T> (int layerIndex, string property, out T value)
            {
                if (this.SB.m_overrideStorageLayers[layerIndex].TryGetValue(this.Id, out PseudoPropertyBag propertyBag)
                    && propertyBag.TryGetValue(property, out object v))
                {
                    value = (T)v;
                    return true;
                }

                value = default;
                return false;
            }

            public bool HasBaselineValueSet (string property)
            {
                return this.SB.m_baselineStorageLayer.TryGetValue(this.Id, out PseudoPropertyBag propertyBag)
                        && propertyBag.ContainsKey(property);
            }

            public bool HasOverrideValueSet (int layerIndex, string property)
            {
                return this.SB.m_overrideStorageLayers[layerIndex].TryGetValue(this.Id, out PseudoPropertyBag propertyBag)
                        && propertyBag.ContainsKey(property);
            }

            public void ClearAll (bool notifyOfRemovals = true)
            {
                this.SB.m_baselineStorageLayer.Remove(this.Id);
                foreach (Stratum stratum in this.SB.m_overrideStorageLayers)
                {
                    stratum.Remove(this.Id);
                }

                // ===================================================================================================
                // Note: Remember, don't remove this manager from parent as the events are still potentially hooked
                //          up the property access flyweights, and unhooking them would be a futile operation.
                // ===================================================================================================

                if (notifyOfRemovals)
                {
                    this.LayerDataRemoved?.Invoke(this, new StratabasePropertyChangeEventArgs(this.Id, String.Empty, null, null));
                }
            }

            /// <summary>
            /// Handles with a flyweight has been disposed. Removing access points may mean this instance could
            /// remove itself
            /// </summary>
            public void HandleAccessWithdrawn ()
            {
                if(this.LayerDataSet == null && this.LayerDataRemoved == null)
                {
                    this.SB.OnObjectDataAccessManagerRemoved(this);
                }
            }
        }

        internal class PseudoPropertyBag
        {
            private readonly Stratabase m_stratabase;
            internal readonly Dictionary<string, object> m_storage;

            public PseudoPropertyBag (Stratabase stratabase)
            {
                m_stratabase = stratabase;
                m_storage = new Dictionary<string, object>();
            }

            public PseudoPropertyBag (Stratabase stratabase, Dictionary<string, object> storage)
            {
                m_stratabase = stratabase;
                m_storage = storage;
            }

            public bool TryGetValue (string propertyName, out object value) => this.m_storage.TryGetValue(propertyName, out value);
            
            public IEnumerable<string> Keys => m_storage.Keys;

            internal bool SetValue (string propertyName, object newValue, out object oldValue)
            {
                if (!m_storage.TryGetValue(propertyName, out oldValue))
                {
                    m_storage.Add(propertyName, newValue);
                    return true;
                }
                else if (!m_stratabase.ValueEqualityTester(oldValue, newValue))
                {
                    m_storage[propertyName] = newValue;
                    return true;
                }

                return false;
            }

            internal bool RemoveValue (string propertyName, out object oldValue)
            {
                if (m_storage.TryGetValue(propertyName, out oldValue))
                {
                    return m_storage.Remove(propertyName);
                }

                return false;
            }

            internal bool ContainsKey (string property) => m_storage.ContainsKey(property);
        }

        private class Stratum : Dictionary<Guid, PseudoPropertyBag> { }
    }
}
