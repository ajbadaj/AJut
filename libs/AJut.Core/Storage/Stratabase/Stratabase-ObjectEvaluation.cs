namespace AJut.Storage
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using AJut.TypeManagement;

    public sealed partial class Stratabase
    {
        // ===========================[ Utility Methods ]======================================
        private void SetBaselineData (ObjectEvaluation objectEvaluation)
        {
            ObjectDataAccessManager accessManager = EnsureDataAccess(objectEvaluation.Id);
            foreach (KeyValuePair<string, object> property in objectEvaluation.ValueStorage)
            {
                accessManager.SetBaselineValue(property.Key, property.Value);
            }

            foreach (ObjectEvaluation ancillary in objectEvaluation.AncillaryElements)
            {
                this.SetBaselineData(ancillary);
            }

            foreach (KeyValuePair<string, ObjectEvaluation> childObject in objectEvaluation.ChildObjectStorage)
            {
                accessManager.SetBaselineValue(childObject.Key, childObject.Value.Id);
                this.SetBaselineData(childObject.Value);
            }

            foreach (KeyValuePair<string, ObjectEvaluation.ListInfo> listProp in objectEvaluation.InsertGenerationLists)
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

        // ===========================[ Utility Classes ]======================================

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

                string foundIdPropertyForObject = sourceType.GetAttributes<StratabaseIdAttribute>().FirstOrDefault()?.PropertyName ?? idPropertyName;
                var allProperties = DeterminePropertiesFor(sourceType, foundIdPropertyForObject);

                Dictionary<string, object> simpleProperties = new Dictionary<string, object>();
                Dictionary<string, ObjectEvaluation> subObjects = new Dictionary<string, ObjectEvaluation>();
                Dictionary<string, ListInfo> insertGenerationLists = new Dictionary<string, ListInfo>();
                List<ObjectEvaluation> ancillaryElements = new List<ObjectEvaluation>();

                string propertyNamePrefix = parentPropertyChain == null ? String.Empty : $"{parentPropertyChain}.";

                var typeIdAttr = sourceType.GetAttributes<TypeIdAttribute>().FirstOrDefault();
                if (typeIdAttr != null)
                {
                    simpleProperties.Add(propertyNamePrefix + kTypeIdStorage, typeIdAttr.Id);
                }

                foreach (PropertyInfo prop in allProperties)
                {
                    // If we haven't found the id yet, check to see if this property is the id
                    if (!foundId.HasValue)
                    {
                        var propIdAttr = prop.GetAttributes<StratabaseIdAttribute>().FirstOrDefault();
                        if (prop.Name == foundIdPropertyForObject || propIdAttr != null && propIdAttr.IsClassDefault)
                        {
                            ThrowIfTypeIsNotGuid(sourceType, prop);
                            foundId = (Guid)prop.GetValue(source);
                            continue;
                        }
                    }

                    Lazy<object> lazyInstanceGenerator = new Lazy<object>(() => prop.GetValue(source));
                    string subObjectName = propertyNamePrefix + prop.Name;

                    // Got a sub object over here...
                    if (!prop.PropertyType.IsSimpleType())
                    {
                        // ===========================[ Struct Evaluations ]========================
                        if (prop.PropertyType.IsValueType)
                        {
                            // Structs are usually stored directly, but if they are tagged like this then their properties are stored as dot elements (thing.prop1)
                            if (prop.IsTaggedWithAttribute<StrataStoreAsDotElementsAttribute>() || prop.PropertyType.IsTaggedWithAttribute<StrataStoreAsDotElementsAttribute>())
                            {
                                var dotEvalResult = ObjectEvaluation.Generate(null, null, lazyInstanceGenerator.Value, subObjectName);
                                if (dotEvalResult != null)
                                {
                                    simpleProperties.AddEach(dotEvalResult.ValueStorage);
                                    subObjects.AddEach(dotEvalResult.ChildObjectStorage);
                                }

                                continue;
                            }
                            else
                            {
                                simpleProperties.Add(subObjectName, lazyInstanceGenerator.Value);
                                continue;
                            }
                        }

                        // ===========================[ List Evaluations ]========================
                        if (EvaluateListCompatibility(prop, out StrataListConfigAttribute listConfigAttr))
                        {
                            IList elementValues = lazyInstanceGenerator.Value as IList;
                            if (elementValues == null)
                            {
                                continue;
                            }

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
                                    if (listConfigAttr.Config == eStrataListConfig.ObservableElementManagement)
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

                                if (elementIds.Count > 0 && listConfigAttr.Config == eStrataListConfig.ObservableElementManagement)
                                {
                                    insertGenerationLists.Add(subObjectName, new ListInfo(elementIds.ToArray(), typeof(Guid)));
                                }
                            }
                            // ========= Whatever is in 'em lists ==============
                            else
                            {
                                if (listConfigAttr.Config == eStrataListConfig.ObservableElementManagement)
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
                                    var propIdAttr = prop.GetAttributes<StratabaseIdAttribute>().FirstOrDefault();
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

                        // ===========================[ Subobject Evaluations ]========================
                        // Gerenate subobject or subobject info
                        bool isReference = EvaluateIfUsesReferenceIndirection(prop);
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
                    else if (lazyInstanceGenerator.Value != null)
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

            private static void ThrowIfTypeIsNotGuid (Type sourceType, PropertyInfo prop)
            {
                if (prop.PropertyType != typeof(Guid))
                {
                    throw new ArgumentException($"Error - StratabaseId on class {sourceType.Name} identified property {prop.Name} as the object's identifier, but {prop.Name} is not a guid. [StratabaseId] attribute must be used to identify a property of type guid.");
                }
            }

            public static bool EvaluateListCompatibility (PropertyInfo prop, out StrataListConfigAttribute listConfig)
            {
                if (!prop.PropertyType.IsSimpleType()
                    && (prop.PropertyType.IsArray || (typeof(IList).IsAssignableFrom(prop.PropertyType) && prop.PropertyType.GenericTypeArguments.Length == 1)))
                {
                    listConfig = prop.GetAttributes<StrataListConfigAttribute>().FirstOrDefault() ?? StrataListConfigAttribute.Default;
                    return true;
                }

                listConfig = null;
                return false;
            }

            public static bool EvaluateIfUsesReferenceIndirection (PropertyInfo prop)
            {
                return prop.GetAttributes<StratabaseReferenceAttribute>().Any();
            }

            public static PropertyInfo[] DeterminePropertiesFor (Type sourceType, string classIdProperty = null)
            {
                return sourceType
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetField)
                        .Where(_IsPropertyValid)
                        .ToArray();

                bool _IsPropertyValid (PropertyInfo _prop)
                {
                    if (_prop.GetAttributes<StrataIgnoreAttribute>().FirstOrDefault()?.WhenInput ?? false)
                    {
                        return false;
                    }

                    if (_prop.SetMethod == null)
                    {
                        if (sourceType.IsAnonymous() || _prop.Name == classIdProperty || _prop.IsTaggedWithAttribute<StrataIncludeReadonlyAttribute>())
                        {
                            return true;
                        }

                        return false;
                    }

                    return true;
                }
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
    }
}
