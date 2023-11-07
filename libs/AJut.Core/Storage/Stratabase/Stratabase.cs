﻿namespace AJut.Storage
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography.X509Certificates;
    using System.Xml.Linq;
    using AJut.TypeManagement;

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
        public const string kTypeIdStorage = "__type";

        /// <summary>
        /// Used with <see cref="ObjectDataAccessManager.TryFindActiveLayer(int, string, out int)"/>, index that indicates the baseline layer
        /// </summary>
        internal const int kActiveLayerBaseline = -1;

        /// <summary>
        /// Used with <see cref="ObjectDataAccessManager.TryFindActiveLayer(int, string, out int)"/>, index that indicates that there is no layer set
        /// </summary>
        internal const int kActiveLayerNotSet = -2;

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
            return this.EnsureDataAccess(id).ObliteratePropertyStorageInBaseline(property);
        }

        /// <summary>
        /// Clear a property set on an override layer.
        /// </summary>
        /// <returns>True if the property was found and cleared, false otherwise.</returns>
        public bool ClearPropertyOverride (int layer, Guid id, string property)
        {
            return this.EnsureDataAccess(id).ObliteratePropertyStorageInLayer(layer, property);
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
            string typeId = TypeIdRegistrar.GetTypeIdFor(data.GetType());
            if (typeId != null)
            {
                this.SetBaselinePropertyValue(objectEvaluation.Id, kTypeIdStorage, typeId);
            }

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

            if (this.TryGetTypeForItem(targetId, out Type properTargetPropType))
            {
                targetPropType = properTargetPropType;
            }

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

            // If there is a type id for a sub object, then pre-allocate the sub-object to make set easier
            string[] typeIdKeys = baseline.Keys.Where(k => k.EndsWith("." + kTypeIdStorage)).ToArray();
            foreach (string typeIdKey in baseline.Keys.Where(k => k.EndsWith("." + kTypeIdStorage)))
            {
                if (baseline.TryGetValue(typeIdKey, out string typeId))
                {
                    string remainingPropPath = typeIdKey.Substring(0, typeIdKey.Length - (kTypeIdStorage.Length + 1));
                    PropertyInfo targetProp = source.GetComplexProperty(remainingPropPath, out object childTarget);
                    targetProp.SetValue(childTarget, AJutActivator.CreateInstanceOf(typeId));
                }
            }

            foreach (string propPath in baseline.Keys)
            {
                if (kTypeIdStorage == propPath || !odam.SearchForFirstSetValue(m_overrideStorageLayers.Length - 1, propPath, out object storedPropValue))
                {
                    continue;
                }

                PropertyInfo targetProp = source.GetComplexProperty(propPath, out object target, ensureSubObjectPath: true);
                if (targetProp == null || targetProp.SetMethod == null)
                {
                    continue;
                }

                var ignoreAttr = targetProp.GetAttributes<StrataIgnoreAttribute>().FirstOrDefault();
                if (ignoreAttr?.WhenOutput ?? false)
                {
                    continue;
                }

                // Do references differently
                if (storedPropValue is Guid targetId && targetProp.IsTaggedWithAttribute<StratabaseReferenceAttribute>())
                {
                    try
                    {
                        targetProp.SetValue(target, this.CreateAndSetObjectWith(targetProp.PropertyType, targetId));
                    }
                    catch
                    {
                        Debug.Assert(true, $"Setting property {targetProp.Name} with stored value {storedPropValue} failed and threw an exception");
                    }
                }

                // Go through this BONKERS process to determine if it's list access backed data and set it that way
                else if (targetProp.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(targetProp.PropertyType))
                {
                    var strataListConfig = targetProp.GetAttributes<StrataListConfigAttribute>().FirstOrDefault();

                    // Is it ListAccess backed?
                    Type storedType = storedPropValue.GetType();
                    if (strataListConfig != null
                        && storedType.IsGenericType && storedType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        // Ok it is, now use reflection to generate a StrataPropertyListAccess
                        Type itemType = storedType.GenericTypeArguments[0];
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

                                    try
                                    {
                                        targetProp.SetValueExtended(source, propPath, target, final);
                                    }
                                    catch
                                    {
                                        Debug.Assert(true, $"Setting property {targetProp.Name} with stored value {storedPropValue} failed and threw an exception");
                                    }
                                }
                                else if (typeof(IList).IsAssignableFrom(targetProp.PropertyType))
                                {
                                    Type elementType = targetProp.PropertyType.GenericTypeArguments[0];
                                    IList final = (IList)Activator.CreateInstance(targetProp.PropertyType);
                                    foreach (object obj in elements)
                                    {
                                        final.Add(_ConvertElementToOutputElement(elementType, obj));
                                    }

                                    try
                                    {
                                        targetProp.SetValueExtended(source, propPath, target, final);
                                    }
                                    catch
                                    {
                                        Debug.Assert(true, $"Setting property {targetProp.Name} with stored value {storedPropValue} failed and threw an exception");
                                    }
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
                    try
                    {
                        targetProp.SetValueExtended(source, propPath, target, storedPropValue);
                    }
                    catch
                    {
                        Debug.Assert(true, $"Setting property {targetProp.Name} with stored value {storedPropValue} failed and threw an exception");
                    }
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

        // ----------------- List Element Misc -----------------

        public int GetElementCount (Guid id, string property)
        {
            ObjectDataAccessManager odam = this.EnsureDataAccess(id);
            if (odam.TryFindActiveLayer(property, out int activeLayer))
            {
                if (activeLayer == kActiveLayerBaseline)
                {
                    return odam.GetElementCountFromBaseline(property);
                }
                else
                {
                    return odam.GetElementCountFromOverrideLayer(activeLayer, property);
                }
            }

            return -1;
        }

        public int GetElementCountInBaseline (Guid id, string property)
        {
            return this.EnsureDataAccess(id).GetElementCountFromBaseline(property);
        }

        public int GetElementCountInOverrideLayer (int layer, Guid id, string property)
        {
            return this.EnsureDataAccess(id).GetElementCountFromOverrideLayer(layer, property);
        }

        /// <summary>
        /// It may be that inserting directly into a list fails if it's the first insertion and the element is null. In cases like that and others, 
        /// separately establishing the list element type can fix problems proactively if you know you might run into them.
        /// </summary>
        /// <typeparam name="TElement">The element type to establish</typeparam>
        /// <param name="id">The object's id</param>
        /// <param name="property">The property name</param>
        /// <param name="clearIfIncorrectElementsExist">If the list has already been established and the elements are of a different type, then should the list be cleared first?</param>
        /// <returns>bool indicating if the list type is correct (can be false if <paramref name="clearIfIncorrectElementsExist"/> is <c>false</c> and elements of a different type are already established).</returns>
        public bool EstablishListElementType<TElement> (Guid id, string property, bool clearIfIncorrectElementsExist = true)
        {
            return this.EstablishListElementType(id, property, typeof(TElement), clearIfIncorrectElementsExist);
        }

        /// <summary>
        /// It may be that inserting directly into a list fails if it's the first insertion and the element is null. In cases like that and others, 
        /// separately establishing the list element type can fix problems proactively if you know you might run into them.
        /// </summary>
        /// <param name="id">The object's id</param>
        /// <param name="property">The property name</param>
        /// <param name="elementType">The element type to establish</param>
        /// <param name="clearIfIncorrectElementsExist">If the list has already been established and the elements are of a different type, then should the list be cleared first?</param>
        /// <returns>bool indicating if the list type is correct (can be false if <paramref name="clearIfIncorrectElementsExist"/> is <c>false</c> and elements of a different type are already established).</returns>
        public bool EstablishListElementType (Guid id, string property, Type elementType, bool clearIfIncorrectElementsExist = true)
        {
            return EnsureDataAccess(id).EstablishListElementType(property, elementType, clearIfIncorrectElementsExist);
        }

        // ----------------- Insert Element Value -----------------

        public bool InsertElementIntoBaselineList (Guid id, string property, int elementIndex, object newElement)
        {
            return this.EnsureDataAccess(id).InsertElementIntoBaselineList(property, elementIndex, newElement);
        }

        public bool InsertElementIntoOverrideList (int layer, Guid id, string property, int elementIndex, object newElement)
        {
            return EnsureDataAccess(id).InsertElementIntoOverrideLayerList(layer, property, elementIndex, newElement);
        }

        public bool RemoveElementFromBaselineList (Guid id, string property, int elementIndex)
        {
            return EnsureDataAccess(id).RemoveElementFromBaselineList(property, elementIndex);
        }

        public bool RemoveElementFromOverrideList (int layer, Guid id, string property, int elementIndex)
        {
            return EnsureDataAccess(id).RemoveElementFromOverrideLayerList(layer, property, elementIndex);
        }

        public void RemoveAllElementsFromBaselineList (Guid id, string property)
        {
            EnsureDataAccess(id).RemoveAllElementsInBaselineList(property);
        }

        public void RemoveAllElementsFromOverrideList (int layer, Guid id, string property)
        {
            EnsureDataAccess(id).RemoveAllElementsInOverrideLayerList(layer, property);
        }

        public void ResetListLayerByCopyingElements (int layerCopyFrom, int layerCopyTo, Guid id, string propertyName)
        {
            EnsureDataAccess(id).ResetListLayerByCopyingElements(layerCopyFrom, layerCopyTo, propertyName);
        }

        public void ResetListLayerByCopyingElementsFromBaseline (int layerCopyTo, Guid id, string propertyName)
        {
            EnsureDataAccess(id).ResetListLayerByCopyingElementsFromBaseline(layerCopyTo, propertyName);
        }

        public void ResetBaselineByCopyingElementsFrom (int overrideLayerToCopyFrom, Guid id, string propertyName)
        {
            EnsureDataAccess(id).ResetBaselineByCopyingElementsFrom(overrideLayerToCopyFrom, propertyName);
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

        // ----------------- Get Element Value -----------------

        /// <summary>
        /// Try and extract a baseline property value for a given object id (typed)
        /// </summary>
        public bool TryGetBaselineElementValue<T> (Guid id, string property, int elementIndex, out T value)
        {
            value = default;
            return this.GetAccessManager(id)?.TryGetBaselineElementValue(property, elementIndex, out value) == true;
        }

        /// <summary>
        /// Try and extract a baseline property value for a given object id (untyped)
        /// </summary>
        public bool TryGetBaselinePropertyElementValue (Guid id, string property, int elementIndex, out object value)
        {
            return TryGetBaselineElementValue<object>(id, property, elementIndex, out value);
        }

        /// <summary>
        /// Try and extract an override property value for a given object id (typed)
        /// </summary>
        public bool TryGetOverridePropertyElementValue<T> (int layer, Guid id, string property, int elementIndex, out T value)
        {
            value = default;
            return this.GetAccessManager(id)?.TryGetOverrideElementValue(layer, property, elementIndex, out value) == true;
        }

        /// <summary>
        /// Try and extract an override property value for a given object id (untyped)
        /// </summary>
        public bool TryGetOverridePropertyElementValue (int layer, Guid id, string property, int elementIndex, out object value)
        {
            return TryGetOverridePropertyElementValue<object>(layer, id, property, elementIndex, out value);
        }

        // ------- Get Typing Info ----------

        public bool TryGetTypeIdForItem (Guid id, out string typeId)
        {
            return this.TryGetBaselinePropertyValue(id, kTypeIdStorage, out typeId);
        }

        public bool TryGetTypeForItem (Guid id, out Type targetType)
        {
            targetType = null;
            return this.TryGetTypeIdForItem(id, out string typeId)
                && TypeIdRegistrar.TryGetType(typeId, out targetType);
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
                this.BaselineDataChanged?.Invoke(this, new BaselineStratumModificationEventArgs(e.ItemId, e.PropertyName, e.OldValue, e.NewValue, false));
            }
            else
            {
                this.OverrideDataChanged?.Invoke(this, new OverrideStratumModificationEventArgs(e.LayerIndex, e.ItemId, e.PropertyName, e.OldValue, e.NewValue, false));
            }
        }

        private void Odam_LayerDataRemoved (object sender, StratabasePropertyChangeEventArgs e)
        {
            if (e.IsBaseline)
            {
                this.BaselineDataChanged?.Invoke(this, new BaselineStratumModificationEventArgs(e.ItemId, e.PropertyName, e.OldValue, null, true));
            }
            else
            {
                this.OverrideDataChanged?.Invoke(this, new OverrideStratumModificationEventArgs(e.LayerIndex, e.ItemId, e.PropertyName, e.OldValue, null, true));
            }
        }

        // ===========================[ Utility Classes ]======================================

        /// <summary>
        /// Manager of all changes and interactions, focused on a particular object.
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
            internal event EventHandler<StratabaseListElementsChangedEventArgs> LayerListElementsChanged;
            internal event EventHandler<StratabaseChangeEventArgs> LayerListElementsCleared;

            public Guid Id { get; }
            public Stratabase SB { get; }

            /// <summary>
            /// Determines the active layer set, if any
            /// </summary>
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
                    activeLayer = kActiveLayerBaseline;
                    return true;
                }

                activeLayer = kActiveLayerNotSet;
                return false;
            }

            public bool ObliteratePropertyStorageInBaseline (string propertyName)
            {
                if (this.SB.GetBaselinePropertyBagFor(this.Id).PullValueOut(propertyName, out object oldValue))
                {
                    this.LayerDataRemoved?.Invoke(this,
                        new StratabasePropertyChangeEventArgs
                        {
                            ItemId = this.Id,
                            PropertyName = propertyName,
                            OldValue = oldValue,
                            NewValue = null
                        }
                    );
                    return true;
                }

                return false;
            }

            public Result<object> ObliteratePropertyStorageInLayer (int overrideLayer, string propertyName)
            {
                if (this.SB.GetOverridePropertyBagFor(overrideLayer, this.Id).PullValueOut(propertyName, out object oldValue))
                {
                    this.LayerDataRemoved?.Invoke(this,
                        new StratabasePropertyChangeEventArgs
                        {
                            ItemId = this.Id,
                            LayerIndex = overrideLayer,
                            PropertyName = propertyName,
                            OldValue = oldValue,
                            NewValue = null
                        }
                    );
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
                    this.LayerDataSet?.Invoke(this, 
                        new StratabasePropertyChangeEventArgs
                        {
                            ItemId = this.Id,
                            PropertyName = property,
                            OldValue = oldValue,
                            NewValue = newValue
                        }
                    );
                    return true;
                }

                return false;
            }

            public bool SetOverrideValue (int overrideLayerIndex, string property, object newValue)
            {
                if (this.SB.GetOverridePropertyBagFor(overrideLayerIndex, this.Id).SetValue(property, newValue, out object oldValue))
                {
                    this.LayerDataSet?.Invoke(this,
                        new StratabasePropertyChangeEventArgs
                        {
                            ItemId = this.Id,
                            LayerIndex = overrideLayerIndex,
                            PropertyName = property,
                            OldValue = oldValue,
                            NewValue = newValue
                        }
                    );
                    return true;
                }

                return false;
            }

            // ------------- Element Management: Insert / Remove ----------------

            public bool EstablishListElementType(string propertyName, Type elementType, bool clearIfIncorrectElementsExist)
            {
                bool soFarSoGood = true;
                if (!this.SB.GetBaselinePropertyBagFor(this.Id).EnsureListElementsOfType(propertyName, elementType, clearIfIncorrectElementsExist))
                {
                    soFarSoGood = false;
                }
                for (int overrideLayerIndex = 0; overrideLayerIndex < this.SB.OverrideLayerCount; ++overrideLayerIndex)
                {
                    if (!this.SB.GetOverridePropertyBagFor(overrideLayerIndex, this.Id).EnsureListElementsOfType(propertyName, elementType, clearIfIncorrectElementsExist))
                    {
                        soFarSoGood = false;
                    }
                }

                return soFarSoGood;
            }

            public bool InsertElementIntoBaselineList (string propertyName, int elementIndex, object newElement)
            {
                if (this.SB.GetBaselinePropertyBagFor(this.Id).InsertElement(propertyName, elementIndex, newElement))
                {
                    this.LayerListElementsChanged?.Invoke(this,
                        new StratabaseListElementsChangedEventArgs
                        {
                            ItemId = this.Id,
                            PropertyName = propertyName,
                            ElementIndex = elementIndex,
                            Element = newElement,
                            WasElementAdded = true,
                        }
                    );

                    return true;
                }

                return false;
            }

            public bool InsertElementIntoOverrideLayerList (int overrideLayerIndex, string propertyName, int elementIndex, object newElement)
            {
                if (this.SB.GetOverridePropertyBagFor(overrideLayerIndex, this.Id).InsertElement(propertyName, elementIndex, newElement))
                {
                    this.LayerListElementsChanged?.Invoke(this,
                        new StratabaseListElementsChangedEventArgs
                        {
                            ItemId = this.Id,
                            PropertyName = propertyName,
                            LayerIndex = overrideLayerIndex,
                            ElementIndex = elementIndex,
                            Element = newElement,
                            WasElementAdded = true,
                        }
                    );

                    return true;
                }

                return false;
            }

            public bool AddElementIntoBaselineList (string propertyName, object newElement)
            {
                if (this.SB.GetBaselinePropertyBagFor(this.Id).AddElement(propertyName, newElement, out int elementIndex))
                {
                    this.LayerListElementsChanged?.Invoke(this,
                        new StratabaseListElementsChangedEventArgs
                        {
                            ItemId = this.Id,
                            PropertyName = propertyName,
                            ElementIndex = elementIndex,
                            Element = newElement,
                            WasElementAdded = true,
                        }
                    );

                    return true;
                }

                return false;
            }

            public bool AddElementIntoOverrideList (int overrideLayerIndex, string propertyName, object newElement)
            {
                if (this.SB.GetOverridePropertyBagFor(overrideLayerIndex, this.Id).AddElement(propertyName, newElement, out int elementIndex))
                {
                    this.LayerListElementsChanged?.Invoke(this,
                        new StratabaseListElementsChangedEventArgs
                        {
                            ItemId = this.Id,
                            PropertyName = propertyName,
                            LayerIndex = overrideLayerIndex,
                            ElementIndex = elementIndex,
                            Element = newElement,
                            WasElementAdded = true,
                        }
                    );

                    return true;
                }

                return false;
            }

            public bool RemoveElementFromBaselineList (string propertyName, int elementIndex)
            {
                if (this.SB.GetBaselinePropertyBagFor(this.Id).RemoveElementAt(propertyName, elementIndex, out object removedElement))
                {
                    this.LayerListElementsChanged?.Invoke(this,
                        new StratabaseListElementsChangedEventArgs
                        {
                            ItemId = this.Id,
                            PropertyName = propertyName,
                            ElementIndex = elementIndex,
                            Element = removedElement,
                            WasElementAdded = false,
                        }
                    );

                    return true;
                }

                return false;
            }

            public bool RemoveElementFromOverrideLayerList (int overrideLayerIndex, string propertyName, int elementIndex)
            {
                if (this.SB.GetOverridePropertyBagFor(overrideLayerIndex, this.Id).RemoveElementAt(propertyName, elementIndex, out object removedElement))
                {
                    this.LayerListElementsChanged?.Invoke(this,
                        new StratabaseListElementsChangedEventArgs
                        {
                            ItemId = this.Id,
                            PropertyName = propertyName,
                            LayerIndex = overrideLayerIndex,
                            ElementIndex = elementIndex,
                            Element = removedElement,
                            WasElementAdded = false,
                        }
                    );

                    return true;
                }

                return false;
            }

            public void RemoveAllElementsInBaselineList (string propertyName)
            {
                this.SB.GetBaselinePropertyBagFor(this.Id).ClearListElements(propertyName);
                this.LayerListElementsCleared?.Invoke(this,
                    new StratabaseChangeEventArgs
                    {
                        ItemId = this.Id,
                        PropertyName = propertyName,
                    }
                );
            }

            public void RemoveAllElementsInOverrideLayerList (int overrideLayerIndex, string propertyName)
            {
                this.SB.GetOverridePropertyBagFor(overrideLayerIndex, this.Id).ClearListElements(propertyName);
                this.LayerListElementsCleared?.Invoke(this,
                    new StratabaseChangeEventArgs
                    {
                        ItemId = this.Id,
                        LayerIndex = overrideLayerIndex,
                        PropertyName = propertyName,
                    }
                );
            }

            public void ResetListLayerByCopyingElements (int overrideLayerToCopyFrom, int overrideLayerToCopyTo, string propertyName)
            {
                this.RemoveAllElementsInOverrideLayerList(overrideLayerToCopyTo, propertyName);
                this.SB.GetOverridePropertyBagFor(overrideLayerToCopyFrom, this.Id).CopyListElementsTo(propertyName, this.SB.GetOverridePropertyBagFor(overrideLayerToCopyTo, this.Id));
            }

            public void ResetListLayerByCopyingElementsFromBaseline (int overrideLayerToCopyTo, string propertyName)
            {
                this.RemoveAllElementsInOverrideLayerList(overrideLayerToCopyTo, propertyName);
                this.SB.GetBaselinePropertyBagFor(this.Id).CopyListElementsTo(propertyName, this.SB.GetOverridePropertyBagFor(overrideLayerToCopyTo, this.Id));
            }

            public void ResetBaselineByCopyingElementsFrom (int overrideLayerToCopyFrom, string propertyName)
            {
                this.RemoveAllElementsInBaselineList(propertyName);
                this.SB.GetOverridePropertyBagFor(overrideLayerToCopyFrom, this.Id).CopyListElementsTo(propertyName, this.SB.GetBaselinePropertyBagFor(this.Id));
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

            public int GetElementCountFromBaseline (string property)
            {
                var propBag = this.SB.GetBaselinePropertyBagFor(this.Id);
                if (propBag != null && propBag.TryGetValue(property, out IList list))
                {
                    return list.Count;
                }

                return -1;
            }

            public int GetElementCountFromOverrideLayer (int layerIndex, string property)
            {
                var propBag = this.SB.GetOverridePropertyBagFor(layerIndex, this.Id);
                if (propBag != null && propBag.TryGetValue(property, out IList list))
                {
                    return list.Count;
                }

                return -1;
            }

            public bool TryGetBaselineElementValue<T> (string property, int elementIndex, out T value)
            {
                var propBag = this.SB.GetBaselinePropertyBagFor(this.Id);
                if (propBag != null && propBag.TryGetElementValue<T>(property, elementIndex, out value))
                {
                    return true;
                }

                value = default;
                return false;
            }


            public bool TryGetOverrideElementValue<T> (int layerIndex, string property, int elementIndex, out T value)
            {
                if (this.SB.m_overrideStorageLayers[layerIndex].TryGetValue(this.Id, out PseudoPropertyBag propertyBag)
                    && propertyBag.TryGetElementValue(property, elementIndex, out value))
                {
                    return true;
                }

                value = default;
                return false;
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
                    this.LayerDataRemoved?.Invoke(this,
                        new StratabasePropertyChangeEventArgs
                        {
                            ItemId = this.Id,
                            PropertyName = String.Empty,
                        }
                    );
                }
            }

            /// <summary>
            /// Handles with a flyweight has been disposed. Removing access points may mean this instance could
            /// remove itself
            /// </summary>
            public void HandleAccessWithdrawn ()
            {
                if (this.LayerDataSet == null && this.LayerDataRemoved == null)
                {
                    this.SB.OnObjectDataAccessManagerRemoved(this);
                }
            }
        }

        internal class PseudoPropertyBag
        {
            private readonly Stratabase m_stratabase;
            internal readonly Dictionary<string, object> m_storage;
            private readonly Dictionary<string, Type> m_propertyListElementTypeRestriction = new Dictionary<string, Type>();

            public PseudoPropertyBag (Stratabase stratabase)
            {
                m_stratabase = stratabase;
                m_storage = new Dictionary<string, object>();
            }

            public PseudoPropertyBag (Stratabase stratabase, Dictionary<string, object> storage)
            {
                m_stratabase = stratabase;
                m_storage = storage;
                foreach (KeyValuePair<string,object> kvps in m_storage.Where(kvp => kvp.Value is IList))
                {
                    m_propertyListElementTypeRestriction[kvps.Key] = kvps.Value.GetType().GetGenericArguments()[0];
                }
            }

            // ==========================[ Properties ]===================================
            public IEnumerable<string> Keys => m_storage.Keys;

            // ==========================[ Retrieval Methods ]=============================
            public bool TryGetValue (string propertyName, out object value) => this.m_storage.TryGetValue(propertyName, out value);
            public bool TryGetValue<T> (string propertyName, out T value)
            {
                if (this.TryGetValue(propertyName, out object objValue))
                {
                    value = (T)objValue;
                    return true;
                }

                value = default;
                return false;
            }

            public bool TryGetElementValue (string propertyName, int elementIndex, out object value)
            {
                if (m_storage.TryGetValue(propertyName, out object listObj) && listObj is IList list)
                {
                    value = list[elementIndex];
                    return true;
                }

                value = null;
                return false;
            }

            public bool TryGetElementValue<T> (string propertyName, int elementIndex, out T value)
            {
                if (m_storage.TryGetValue(propertyName, out object listObj) && listObj is IList list)
                {
                    value = (T)list[elementIndex];
                    return true;
                }

                value = default;
                return false;
            }

            public bool PullValueOut (string propertyName, out object oldValue)
            {
                if (m_storage.TryGetValue(propertyName, out oldValue))
                {
                    return m_storage.Remove(propertyName);
                }

                return false;
            }

            // ==========================[ Modifier Methods ]=============================

            public bool SetValue (string propertyName, object newValue, out object oldValue)
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

            public bool EnsureListElementsOfType(string propertyName, Type targetElementType, bool clearIfIncorrectElementsExist)
            {
                if (!m_propertyListElementTypeRestriction.TryGetValue(propertyName, out Type currentElementType))
                {
                    // It's fully non-established, establish it and move on
                    m_propertyListElementTypeRestriction[propertyName] = targetElementType;
                    return true;
                }

                if (currentElementType == targetElementType)
                {
                    // It's already correct, no need to do more
                    return true;
                }

                if (m_storage[propertyName] is IList listTracker)
                {
                    // There's no need to safely get the first element type since this code controls what it will be exactly
                    if (currentElementType == null)
                    {
                        currentElementType = listTracker.GetType().GenericTypeArguments[0];
                    }

                    if (currentElementType == targetElementType)
                    {
                        // Everything is right!
                        return true;
                    }

                    Logger.LogInfo($"{nameof(Stratabase)}::{propertyName} - attempting to ensure list elements of type {targetElementType.Name} - existing storage had a different element type {currentElementType.Name} instead!");

                    if (listTracker.Count == 0)
                    {
                        // It wan't right - but that's ok because it was empty anyway
                        m_storage.Remove(propertyName);
                        _DoListCreation();
                        return true;
                    }

                    if (currentElementType.IsAssignableTo(targetElementType))
                    {
                        // It wasn't right - but it's still ok because the element type was of one castable to the new type
                        Logger.LogInfo($"{nameof(Stratabase)}::{propertyName} - target type is assignable from existing type - so reallocating the list and keeping the elements");
                        m_storage.Remove(propertyName);
                        IList newList = _DoListCreation();
                        newList.AddEach(listTracker);
                        return true;
                    }

                    if (clearIfIncorrectElementsExist)
                    {
                        // Everything is not right, but we got the go ahead to wipe it all out and start over
                        m_storage.Remove(propertyName);
                        _DoListCreation();
                        return true;
                    }
                    else
                    {
                        // Everything is not right, and we can't start over :(
                        return false;
                    }
                }
                else
                {
                    Logger.LogInfo($"{nameof(Stratabase)}::{propertyName} - attempting to ensure list elements of type {targetElementType.Name} - existing storage was not a list at all! Overwriting to be a list.");
                    m_storage.Remove(propertyName);
                    _DoListCreation();
                    return true;
                }

                IList _DoListCreation ()
                {
                    Type _finalListType = typeof(List<>).MakeGenericType(targetElementType);
                    IList _listTracker = (IList)AJutActivator.CreateInstanceOf(_finalListType);
                    m_storage.Add(propertyName, _listTracker);
                    return _listTracker;
                }
            }

            public bool InsertElement (string propertyName, int elementIndex, object newElement)
            {
                if (!m_storage.TryGetValue(propertyName, out object listTrackerObj) || listTrackerObj is not IList listTracker)
                {
                    Type elementType = null;
                    if (m_propertyListElementTypeRestriction.TryGetValue(propertyName, out Type restrictedType))
                    {
                        elementType = restrictedType;
                    }
                    else if (newElement != null)
                    {
                        elementType = newElement.GetType();
                        m_propertyListElementTypeRestriction[propertyName] = elementType;
                    }

                    // We couldn't establish a valid element type
                    if (elementType == null)
                    {
                        return false;
                    }

                    var finalListType = typeof(List<>).MakeGenericType(elementType);
                    listTracker = (IList)AJutActivator.CreateInstanceOf(finalListType);
                    m_storage.Add(propertyName, listTracker);
                }

                if (elementIndex == -1)
                {
                    elementIndex = listTracker.Count - 1;
                }

                if (listTracker.Count < elementIndex || elementIndex < 0)
                {
                    return false;
                }

                listTracker.Insert(elementIndex, newElement);
                return true;
            }

            public bool AddElement (string propertyName, object newElement, out int newElementIndex)
            {
                if (!m_storage.TryGetValue(propertyName, out object listTrackerObj) || !(listTrackerObj is IList listTracker))
                {
                    if (newElement == null)
                    {
                        newElementIndex = -1;
                        return false;
                    }

                    Type elementType = newElement.GetType();
                    var finalListType = typeof(List<>).MakeGenericType(elementType);
                    listTracker = (IList)AJutActivator.CreateInstanceOf(finalListType);
                    m_storage.Add(propertyName, listTracker);
                }

                newElementIndex = listTracker.Count;
                listTracker.Insert(newElementIndex, newElement);
                return true;
            }

            public bool RemoveElementAt (string propertyName, int elementIndex, out object element)
            {
                if (m_storage.TryGetValue(propertyName, out object listTrackerObj) && listTrackerObj is IList listTracker)
                {
                    element = m_storage[propertyName];
                    listTracker.RemoveAt(elementIndex);
                    return true;
                }

                element = null;
                return false;
            }

            public void ClearListElements (string propertyName)
            {
                if (m_storage.TryGetValue(propertyName, out object listTrackerObj) && listTrackerObj is IList listTracker)
                {
                    listTracker.Clear();
                }
            }

            public void CopyListElementsTo (string propertyName, PseudoPropertyBag sourceBag)
            {
                if (sourceBag.m_storage.TryGetValue(propertyName, out object sourceListObj) && sourceListObj is IList listTracker
                    && m_storage.TryGetValue(propertyName, out object destListObj) && destListObj is IList destListTracker)
                {
                    destListTracker.AddEach(listTracker);
                }
            }

            // ==========================[ Search Methods Methods ]=============================

            public bool ContainsKey (string property) => m_storage.ContainsKey(property);

        }

        private class Stratum : Dictionary<Guid, PseudoPropertyBag> { }
    }
}
