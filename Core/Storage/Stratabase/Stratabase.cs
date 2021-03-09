namespace AJut.Storage
{
    using AJut.Text.AJson;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public delegate bool ObjectEqualityTester (object o1, object o2);

    /// <summary>
    /// A layered storage container of property values &amp; overrides. Property groups, referenced in comments as objects, are specified with a unified
    /// <see cref="Guid"/> id. On those objects are properties set by name. A baseline layer contains all properties, and an override layer, from 0-<see cref="OverrideLayerCount"/>
    /// overrides those values. For instance, you might store an object with id {294FBA5B-BDB6-4CA1-86E7-8C552C5F3608}, and a property "Count" that is an int;
    /// you could then override Count for that object in an override layer, and the top most value (from highest override layer index to baseline) would be the
    /// effective value. To simplify this somewhat, access flyweights are available with <see cref="StrataPropertyValueAccess{TProperty}"/> and <see cref="StrataPropertyListAccess{TElement}"/>.
    /// </summary>
    public sealed class Stratabase
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

        private Stratabase (StratabaseDataModel data)
        {
            this.OverrideLayerCount = data.OverrideLayers.Length;
            m_baselineStorageLayer = StratabaseDataModel.FromStratumData(this, data.BaselineData);
            m_overrideStorageLayers = data.OverrideLayers.Select(layer => StratabaseDataModel.FromStratumData(this, layer)).ToArray();
        }

        /// <summary>
        /// Constructs a <see cref="Json"/> instance that represents this instance
        /// </summary>
        public Json SerializeToJson () => this.SerializeToJson(true, -1);
        public Json SerializeToJson (bool includeBaseline = true, params int[] overrideLayersToInclude)
        {
            var output = new StratabaseDataModel(this, includeBaseline, overrideLayersToInclude);
            return JsonHelper.BuildJsonForObject(output, StratabaseDataModel.kJsonSettings);
        }

        /// <summary>
        /// Constructs a <see cref="Stratabase"/> from the json previously serialized by a stratabase
        /// </summary>
        public static Stratabase DeserializeFromJson (Json json)
        {
            var data = JsonHelper.BuildObjectForJson<StratabaseDataModel>(json);
            if (data == null)
            {
                return null;
            }

            return new Stratabase(data);
        }

        // =================================[ Events ]=========================================

        public event EventHandler<BaselineStratumModificationEventArgs> BaselineDataChanged;
        public event EventHandler<OverrideStratumModificationEventArgs> OverrideDataChanged;

        // ==========================[ Public Properties ]=====================================

        public int OverrideLayerCount { get; }

        public ObjectEqualityTester ValueEqualityTester { get; set; } = (o1, o2) => o1?.Equals(o2) ?? false;

        // ====================================================================================
        // ===========================[ Public Methods ]=======================================
        // ====================================================================================

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

        public void ClearAll (bool notifyOfRemovals = true)
        {
            foreach (var odam in m_objectAccess.Values.ToList())
            {
                odam.ClearAll(notifyOfRemovals);
            }
        }

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

        public void SetObjectWithProperties<T> (Guid objectId, T source)
        {
            Type sourceType = source.GetType();
            PropertyInfo[] allSettableProperties = sourceType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty)
                .Where(p => !(p.GetAttributes<StrataIgnoreAttribute>().FirstOrDefault()?.WhenOutput ?? false))
                .ToArray();

            ObjectDataAccessManager odam = this.GetAccessManager(objectId);
            if (odam == null)
            {
                return;
            }
            
            foreach (PropertyInfo settableProperty in allSettableProperties)
            {
                if (odam.SearchForFirstOverrideLayerIndexSet(this.OverrideLayerCount - 1, settableProperty.Name, out int overrideLayerIndex))
                {
                    _SetPropWithStratum(settableProperty, objectId, m_overrideStorageLayers[overrideLayerIndex]);
                }
                else if (odam.HasBaselineValueSet(settableProperty.Name))
                {
                    _SetPropWithStratum(settableProperty, objectId, m_baselineStorageLayer);
                }
            }

            void _SetPropWithStratum(PropertyInfo _propToSet, Guid _objId, Stratum _layer)
            {
                if (_layer.TryGetValue(objectId, out PseudoPropertyBag storedPropBag)
                    && storedPropBag.TryGetValue(_propToSet.Name, out object storedPropValue))
                {
                    // Go through this BONKERS process to determine if it's list access backed data and set it that way
                    if (typeof(IEnumerable).IsAssignableFrom(_propToSet.PropertyType))
                    {
                        // Is it ListAccess backed?
                        Type propValueType = storedPropValue.GetType();
                        if (propValueType.IsGenericType && propValueType.GetGenericTypeDefinition() == typeof(ObservableCollectionX<>)
                            && propValueType.GenericTypeArguments[0].IsGenericType 
                            && propValueType.GenericTypeArguments[0].GetGenericTypeDefinition() == typeof(StratabaseListInsertion<>))
                        {
                            // Ok it is, now use reflection to generate a StrataPropertyListAccess
                            Type itemType = propValueType.GenericTypeArguments[0].GenericTypeArguments[0];
                            var listAccess = (IDisposable)this.InvokeTemplatedMethod(
                                nameof(GenerateListPropertyAccess), 
                                itemType, _objId, _propToSet.Name
                            );

                            // If we succeeded in making a list access, then grab the cache and try to set the value
                            if (listAccess != null)
                            {
                                IList elements = listAccess.GetComplexPropertyValue<IList>(nameof(StrataPropertyListAccess<int>.Elements));
                                if (elements != null)
                                {
                                    // Here's the tricky bit, we either are setting an array - or - we're setting something we assume
                                    //  activator.create instance can make for us, is an IList, which we can then add the items to.
                                    if (_propToSet.PropertyType.IsArray)
                                    {
                                        Array final = Array.CreateInstance(_propToSet.PropertyType.GenericTypeArguments[0], elements.Count);
                                        for (int index = 0; index < elements.Count; ++index)
                                        {
                                            final.SetValue(elements[index], index);
                                        }

                                        _propToSet.SetValue(source, final);
                                    }
                                    else if (typeof(IList).IsAssignableFrom(_propToSet.PropertyType))
                                    {
                                        IList final = (IList)Activator.CreateInstance(_propToSet.PropertyType);
                                        foreach(object obj in elements)
                                        {
                                            final.Add(obj);
                                        }

                                        _propToSet.SetValue(source, final);
                                    }
                                }

                                // Don't forget to dispose of hte list access
                                listAccess.Dispose();
                                return;
                            }
                        }
                    }

                    // Otherwise, try and see if the type is assignable, and assign it if it is
                    if (_propToSet.PropertyType.IsAssignableFrom(storedPropValue.GetType()))
                    {
                        _propToSet.SetValue(source, storedPropValue);
                    }
                }
            }
        }

        public void SetObjectWithProperties<T> (T source)
        {
            if (source == null)
            {
                return;
            }

            if (!ObjectEvaluation.DetermineObjectId(source, out Guid id))
            {
                throw new ArgumentException($"Object of type {source.GetType().Name} passed in to {nameof(SetObjectWithProperties)} was not assigned a StratabaseId in it's class layout, nor passed in with a guid.");
            }

            this.SetObjectWithProperties(id, source);
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

        public bool TryGetBaselinePropertyValue<T> (Guid id, string property, out T value)
        {
            value = default;
            return this.GetAccessManager(id)?.TryGetBaselineValue(property, out value) == true;
        }

        public bool TryGetBaselinePropertyValue (Guid id, string property, out object value)
        {
            return TryGetBaselinePropertyValue<object>(id, property, out value);
        }

        public bool TryGetOverridePropertyValue<T> (int layer, Guid id, string property, out T value)
        {
            value = default;
            return this.GetAccessManager(id)?.TryGetOverrideValue(layer, property, out value) == true;
        }

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

        private void SetBaselineData (ObjectEvaluation objectEvaluation)
        {
            ObjectDataAccessManager accessManager = EnsureDataAccess(objectEvaluation.Id);
            foreach (KeyValuePair<string, object> property in objectEvaluation.ValueStorage)
            {
                accessManager.SetBaselineValue(property.Key, property.Value);
            }

            //#error using it
            foreach (ObjectEvaluation ancillary in objectEvaluation.AncillaryElements)
            {
                this.SetBaselineData(ancillary);
            }

            foreach (KeyValuePair<string, ObjectEvaluation> childObject in objectEvaluation.ChildObjectStorage)
            {
                accessManager.SetBaselineValue(childObject.Key, childObject.Value.Id);
                this.SetBaselineData(childObject.Value);
            }

            foreach (KeyValuePair<string,ObjectEvaluation.ListInfo> listProp in objectEvaluation.InsertGenerationLists)
            {
                if ((listProp.Value.Elements?.Length ?? 0) == 0)
                {
                    continue;
                }

                Type elementType = listProp.Value.ElementType ?? listProp.Value.Elements[0].GetType();
                Type listAccessType = typeof(StrataPropertyListAccess<>).MakeGenericType(elementType);

                // In order to keep some kind of strong access of the method (for refactoring & compiling errors), using object
                //  as a sort of unknown sinceit's just to get nameof.
                string methodName = nameof(StrataPropertyListAccess<object>.GenerateStorageForBaselineListAccess);
                object storage = ReflectionXT.RunStaticMethod(listAccessType, methodName, (object)(listProp.Value.Elements));
                this.SetBaselinePropertyValue(objectEvaluation.Id, listProp.Key, storage);
            }
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

        internal class ObjectEvaluation
        {
            private ObjectEvaluation (Guid id, Dictionary<string, object> simpleProps, Dictionary<string, ObjectEvaluation> validSubObjects, Dictionary<string, ListInfo> insertGenerationLists, List<ObjectEvaluation> ancillaryElements)
            {
                this.Id = id;
                this.ValueStorage = simpleProps;
                this.ChildObjectStorage = validSubObjects;
                this.InsertGenerationLists = insertGenerationLists;
                this.AncillaryElements = ancillaryElements;
            }

            public Guid Id { get; }
            public Dictionary<string, object> ValueStorage { get; }
            public Dictionary<string, ObjectEvaluation> ChildObjectStorage { get; }
            public Dictionary<string, ListInfo> InsertGenerationLists { get; }
            public List<ObjectEvaluation> AncillaryElements { get; }

            public static ObjectEvaluation Generate (Guid? foundId, string idPropertyName, object source, string parentPropertyChain = null)
            {
                if (source == null)
                {
                    return null;
                }

                Type sourceType = source.GetType();
                if (sourceType.IsSimpleType())
                {
                    return null;
                }

                var allProperties = DeterminePropertiesFor(sourceType);

                Dictionary<string, object> simpleProperties = new Dictionary<string, object>();
                Dictionary<string, ObjectEvaluation> subObjects = new Dictionary<string, ObjectEvaluation>();
                Dictionary<string, ListInfo> insertGenerationLists = new Dictionary<string, ListInfo>();
                List<ObjectEvaluation> ancillaryElements = new List<ObjectEvaluation>();

                string propertyNamePrefix = parentPropertyChain == null ? String.Empty : $"{parentPropertyChain}.";

                var classIdAttr = sourceType.GetAttributes<StratabaseIdAttribute>().FirstOrDefault();
                foreach (PropertyInfo prop in allProperties)
                {
                    if (!foundId.HasValue && classIdAttr != null && 
                            (prop.Name == classIdAttr.PropertyName || prop.Name == idPropertyName))
                    {
                        ThrowIfTypeIsNotGuid(sourceType, prop);
                        foundId = (Guid)prop.GetValue(source);
                        continue;
                    }

                    var propIdAttr = prop.GetAttributes<StratabaseIdAttribute>().FirstOrDefault();
                    if (!foundId.HasValue && propIdAttr != null && propIdAttr.IsClassDefault)
                    {
                        ThrowIfTypeIsNotGuid(sourceType, prop);
                        foundId = (Guid)prop.GetValue(source);
                        continue;
                    }

                    Lazy<object> lazyInstanceGenerator = new Lazy<object>(() => prop.GetValue(source));
                    string subObjectName = propertyNamePrefix + prop.Name;

                    // Got a sub object over here...
                    if (!prop.PropertyType.IsSimpleType())
                    {
                        if (prop.PropertyType.IsArray || (typeof(IList).IsAssignableFrom(prop.PropertyType) && prop.PropertyType.GenericTypeArguments.Length == 1))
                        {
                            IList elementValues = lazyInstanceGenerator.Value as IList;
                            if (elementValues == null)
                            {
                                continue;
                            }

                            StrataListConfigAttribute listConfigAttr = prop.GetAttributes<StrataListConfigAttribute>().FirstOrDefault() ?? StrataListConfigAttribute.Default;

                            // ========= Reference lists =======================
                            if (listConfigAttr.BuildReferenceList)
                            {
                                if (listConfigAttr.Config == eStrataListConfig.StoreListDirectly)
                                {
                                    throw new ArgumentException($"List {prop.Name} was marked up with [StrataListConfig] to indicate reference list generation and direct list storage, which is incompatible.");
                                }

                                List<object> elementIds = new List<object>();
                                int index = 0;
                                foreach (object element in elementValues)
                                {
                                    var elementEval = ObjectEvaluation.Generate(null, null, element);
                                    if (listConfigAttr.Config == eStrataListConfig.GenerateInsertOverrides)
                                    {
                                        if (elementEval == null)
                                        {
                                            throw new ArgumentException($"Error - StratabaseId on class {element.GetType().Name} from list {subObjectName} could not determine id. Please markup list with [StrataListConfig] attribute or element class with [StratabaseId] attribute.");
                                        }

                                        ancillaryElements.Add(elementEval);
                                        elementIds.Add(elementEval.Id);
                                    }
                                    else if (listConfigAttr.Config == eStrataListConfig.GenerateIndexedSubProperties)
                                    {
                                        string name = $"{subObjectName}[{index++}]";
                                        if (elementEval != null)
                                        {
                                            subObjects.Add(name, elementEval);
                                        }
                                        else
                                        {
                                            simpleProperties.Add(name, element);
                                        }
                                    }
                                }

                                if (elementIds.Count > 0 && listConfigAttr.Config == eStrataListConfig.GenerateInsertOverrides)
                                {
                                    insertGenerationLists.Add(subObjectName, new ListInfo(elementIds.ToArray(), typeof(Guid)));
                                }
                            }
                            // ========= Whatever is in 'em lists ==============
                            else
                            {
                                if (listConfigAttr.Config == eStrataListConfig.GenerateInsertOverrides)
                                {
                                    insertGenerationLists.Add(subObjectName, new ListInfo(elementValues.OfType<object>().ToArray(), listConfigAttr.ElementType));
                                }
                                else if (listConfigAttr.Config == eStrataListConfig.StoreListDirectly)
                                {
                                    // Otherwise store as just list
                                    simpleProperties.Add(subObjectName, elementValues);
                                }
                                else if (listConfigAttr.Config == eStrataListConfig.GenerateIndexedSubProperties)
                                {
                                    string idPropName = null;
                                    if (propIdAttr != null && !propIdAttr.IsClassDefault)
                                    {
                                        idPropName = propIdAttr.PropertyName;
                                    }

                                    int index = 0;
                                    foreach (object item in elementValues)
                                    {
                                        string name = $"{subObjectName}[{index++}]";
                                        var result = ObjectEvaluation.Generate(null, idPropName, item, name);
                                        if (result != null)
                                        {
                                            subObjects.Add(name, result);
                                        }
                                        else
                                        {
                                            simpleProperties.Add(name, item);
                                        }
                                    }
                                }
                            }

                            continue;
                        }

                        if (lazyInstanceGenerator.Value == null)
                        {
                            continue;
                        }

                        // Gerenate subobject or subobject info
                        bool isReference = prop.GetAttributes<StratabaseReferenceAttribute>().Any();
                        ObjectEvaluation subObjectEval = ObjectEvaluation.Generate(null, null, lazyInstanceGenerator.Value, isReference ? null : subObjectName);

                        // Nothing was generated, and if we're here we have a value, so instead put hte value in for simple properties
                        if (subObjectEval == null)
                        {
                            simpleProperties.Add(subObjectName, lazyInstanceGenerator.Value);
                            continue;
                        }

                        // We generated a full on sub object
                        if (isReference && subObjectEval.Id != Guid.Empty)
                        {
                            simpleProperties.Add(subObjectName, subObjectEval.Id);
                            subObjects.Add(subObjectName, subObjectEval);
                            continue;
                        }

                        // We generate subobject property info, add that info to this things info
                        simpleProperties.AddEach(subObjectEval.ValueStorage);
                        subObjects.AddEach(subObjectEval.ChildObjectStorage);
                    }

                    if (lazyInstanceGenerator.Value != null)
                    {
                        simpleProperties.Add(subObjectName, lazyInstanceGenerator.Value);
                    }
                }

                if (foundId.HasValue)
                {
                    return new ObjectEvaluation(foundId.Value, simpleProperties, subObjects, insertGenerationLists, ancillaryElements);
                }
                else if (parentPropertyChain != null)
                {
                    return new ObjectEvaluation(Guid.Empty, simpleProperties, subObjects, insertGenerationLists, ancillaryElements);
                }

                return null;

            }

            private static void ThrowIfTypeIsNotGuid(Type sourceType, PropertyInfo prop)
            {
                if (prop.PropertyType != typeof(Guid))
                {
                    throw new ArgumentException($"Error - StratabaseId on class {sourceType.Name} identified property {prop.Name} as the object's identifier, but {prop.Name} is not a guid. [StratabaseId] attribute must be used to identify a property of type guid.");
                }
            }

            public static PropertyInfo[] DeterminePropertiesFor(Type sourceType)
            {
                return sourceType
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty)
                        .Where(p => !(p.GetAttributes<StrataIgnoreAttribute>().FirstOrDefault()?.WhenInput ?? false))
                        .ToArray();
            }

            public static bool DetermineObjectId (object source, out Guid id)
            {
                Type sourceType = source.GetType();
                var classIdAttr = sourceType.GetAttributes<StratabaseIdAttribute>().FirstOrDefault();
                var allProperties = DeterminePropertiesFor(sourceType);
                foreach (PropertyInfo prop in allProperties)
                {
                    if (classIdAttr != null && prop.Name == classIdAttr.PropertyName)
                    {
                        ThrowIfTypeIsNotGuid(sourceType, prop);
                        id = (Guid)prop.GetValue(source);
                        return true;
                    }

                    var propIdAttr = prop.GetAttributes<StratabaseIdAttribute>().FirstOrDefault();
                    if (propIdAttr != null && propIdAttr.IsClassDefault)
                    {
                        ThrowIfTypeIsNotGuid(sourceType, prop);
                        id = (Guid)prop.GetValue(source);
                        return true;
                    }
                }

                id = Guid.Empty;
                return false;
            }

            public class ListInfo
            {
                public ListInfo (object[] elements, Type type = null)
                {
                    this.Elements = elements;
                    this.ElementType = type;
                }
                public Type ElementType { get; }
                public object[] Elements { get; }
            }
        }

        private class StratabaseDataModel
        {
            public static JsonBuilder.Settings kJsonSettings = new JsonBuilder.Settings
            {
                KeyValuePairValueTypeIdToWrite = eTypeIdInfo.Any
            };

            public Dictionary<Guid, Dictionary<string, object>> BaselineData { get; set; }
            public Dictionary<Guid, Dictionary<string, object>>[] OverrideLayers { get; set; }

            public StratabaseDataModel() { }
            public StratabaseDataModel(Stratabase sb, bool includeBaseline, int[] layersToInclude)
            {
                this.BaselineData = includeBaseline ? _StratumToSaveData(sb.m_baselineStorageLayer) : null;

                bool includeAllOverrideLayers = layersToInclude.Length == 1 && layersToInclude[0] == -1;
                this.OverrideLayers = new Dictionary<Guid, Dictionary<string, object>>[sb.OverrideLayerCount];
                for (int stratumIndex = 0; stratumIndex < sb.OverrideLayerCount; ++stratumIndex)
                {
                    this.OverrideLayers[stratumIndex] = (includeAllOverrideLayers || layersToInclude.Contains(stratumIndex))
                            ? _StratumToSaveData(sb.m_overrideStorageLayers[stratumIndex])
                            : null;
                }
                sb.m_overrideStorageLayers.Select(_StratumToSaveData).ToArray();

                Dictionary<Guid, Dictionary<string, object>> _StratumToSaveData(Stratum _s)
                {
                    var output = new Dictionary<Guid, Dictionary<string, object>>();
                    foreach (KeyValuePair<Guid,PseudoPropertyBag> data in _s)
                    {
                        output.Add(data.Key, data.Value.m_storage);
                    }

                    return output;
                }
            }

            // This doesn't **need** to be here, but I like having it next to it's opposite above
            public static Stratum FromStratumData (Stratabase sb, Dictionary<Guid, Dictionary<string, object>> stratumData)
            {
                var output = new Stratum();
                if (stratumData != null)
                {
                    foreach (KeyValuePair<Guid, Dictionary<string, object>> data in stratumData)
                    {
                        output.Add(data.Key, new PseudoPropertyBag(sb, data.Value));
                    }
                }

                return output;
            }
        }
    }
}
