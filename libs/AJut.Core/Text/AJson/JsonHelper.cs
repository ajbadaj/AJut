namespace AJut.Text.AJson
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using AJut;
    using AJut.IO;
    using AJut.TypeManagement;

    /// <summary>
    /// Public entry point for V2 AJson - parsing, building, and POCO conversion.
    /// </summary>
    public static class JsonHelper
    {
        private static JsonBuilderSettings g_defaultBuilderSettings = new JsonBuilderSettings();

        // Per-type reflection cache. Bounded by Type identity (assembly-bounded), no leak risk.
        // ConcurrentDictionary chosen for thread-safety in case AJson is called concurrently
        // (Call Familiar wire-message hot path may serialize on multiple threads).
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> g_propertyCacheReadable
            = new ConcurrentDictionary<Type, PropertyInfo[]>();
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> g_propertyCacheWritable
            = new ConcurrentDictionary<Type, PropertyInfo[]>();

        // ===============================[ Type ID Registration ]===========================
        public static void RegisterTypeId<T> (string id)
        {
            TypeIdRegistrar.RegisterTypeId<T>(id);
        }

        public static void RegisterTypeId (string id, Type type)
        {
            TypeIdRegistrar.RegisterTypeId(id, type);
        }

        // ===============================[ Parse Entry Points ]===========================
        public static Json ParseText (string jsonText, ParserRules rules = null)
        {
            if (jsonText == null)
            {
                Json failed = new Json();
                failed.AddError("Null source text provided");
                return failed;
            }
            return JsonReader.Parse(jsonText.AsSpan(), rules);
        }

        public static Json ParseText (ReadOnlySpan<char> jsonText, ParserRules rules = null)
        {
            return JsonReader.Parse(jsonText, rules);
        }

        public static Json ParseFile (string filePath, ParserRules rules = null)
        {
            if (PathHelpers.IsValidAsPath(filePath) && File.Exists(filePath))
            {
                return ParseText(File.ReadAllText(filePath), rules);
            }

            Json failed = new Json();
            failed.AddError($"File path '{filePath ?? "<null>"}' does not exist on disk, or is an invalid path");
            return failed;
        }

        public static Json ParseFile (Stream jsonFileStream, ParserRules rules = null)
        {
            // leaveOpen=true - the caller still owns the stream after we're done reading.
            using (StreamReader reader = new StreamReader(jsonFileStream, System.Text.Encoding.UTF8, true, 1024, leaveOpen: true))
            {
                return ParseText(reader.ReadToEnd(), rules);
            }
        }

        // ===============================[ Build Entry Points ]===========================
        public static JsonBuilder MakeRootBuilder (JsonBuilderSettings settings = null)
        {
            return new JsonBuilder(settings ?? g_defaultBuilderSettings);
        }

        internal static JsonBuilder MakeValueBuilder (object value, JsonBuilderSettings settings = null)
        {
            return new JsonBuilder(settings, value);
        }

        public static Json BuildJsonForObject (object instance, JsonBuilderSettings settings = null)
        {
            JsonBuilder output = MakeRootBuilder(settings);
            if (instance != null)
            {
                FillOutJsonBuilderForObject(instance, output);
            }
            return output.Finalize();
        }

        public static Json BuildJsonForObject<T> (T instance, JsonBuilderSettings settings = null)
        {
            return BuildJsonForObject((object)instance, settings);
        }

        // ===============================[ Object-from-Json Entry Points ]===========================
        public static T BuildObjectForJson<T> (Json sourceJson, JsonInterpreterSettings settings = null)
        {
            return (T)BuildObjectForJson(typeof(T), sourceJson, settings);
        }

        public static T BuildObjectForJson<T> (JsonValue sourceJsonValue, JsonInterpreterSettings settings = null)
        {
            return (T)BuildObjectForJson(typeof(T), sourceJsonValue, settings);
        }

        public static List<T> BuildObjectListForJson<T> (JsonArray sourceJsonArray, JsonInterpreterSettings settings = null)
        {
            List<T> list = new List<T>(sourceJsonArray.Count);
            foreach (JsonValue value in sourceJsonArray)
            {
                list.Add(BuildObjectForJson<T>(value, settings));
            }
            return list;
        }

        public static object BuildObjectForTypedJson (Json sourceJson, JsonInterpreterSettings settings = null)
        {
            return sourceJson.HasErrors ? null : BuildObjectForTypedJson(sourceJson.Data, settings);
        }

        public static object BuildObjectForTypedJson (JsonValue sourceJson, JsonInterpreterSettings settings = null)
        {
            if (sourceJson is JsonDocument doc
                && doc.ValueFor(JsonDocument.kTypeIndicator) is JsonValue typeValue
                && !String.IsNullOrEmpty(typeValue.StringValue)
                && TypeIdRegistrar.TryGetType(typeValue.StringValue, out Type objectType))
            {
                return BuildObjectForJson(objectType, sourceJson, settings);
            }

            return null;
        }

        public static object BuildObjectForJson (Type type, Json sourceJson, JsonInterpreterSettings settings = null)
        {
            if (sourceJson.HasErrors)
            {
                return null;
            }

            return BuildObjectForJson(type, sourceJson.Data, settings, sourceJson);
        }

        public static object BuildObjectForJson (Type type, JsonValue sourceJsonValue, JsonInterpreterSettings settings = null)
        {
            return BuildObjectForJson(type, sourceJsonValue, settings, owner: null);
        }

        // ===============================[ Object-from-Json Implementation ]===========================
        private static object BuildObjectForJson (Type type, JsonValue sourceJsonValue, JsonInterpreterSettings settings, Json owner)
        {
            if (typeof(JsonValue).IsAssignableFrom(type))
            {
                return sourceJsonValue;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return BuildObjectForJson(type.GenericTypeArguments[0], sourceJsonValue, settings, owner);
            }

            settings = settings ?? JsonInterpreterSettings.Default;
            object outputInstance = null;

            if (sourceJsonValue == null)
            {
                return null;
            }

            if (sourceJsonValue.IsDocument)
            {
                JsonDocument docVersion = (JsonDocument)sourceJsonValue;
                string typeIndicator = docVersion.ValueFor(JsonDocument.kTypeIndicator)?.StringValue;
                if (typeIndicator != null)
                {
                    if (TryGetTypeForTypeId(typeIndicator, out Type targetType))
                    {
                        outputInstance = AJutActivator.CreateInstanceOf(targetType);
                    }
                    else
                    {
                        owner?.AddError($"Target type provided '{typeIndicator}' could not be translated, skipping");
                    }
                }
            }
            else if (sourceJsonValue.IsArray)
            {
                JsonArray array = (JsonArray)sourceJsonValue;
                if (type.IsArray)
                {
                    outputInstance = AJutActivator.CreateInstanceOfArray(type, array.Count);
                }
            }

            if (outputInstance == null)
            {
                outputInstance = settings.ConstructInstanceFor(type, sourceJsonValue, owner);
            }

            FillOutObjectWithJson(ref outputInstance, type, sourceJsonValue, settings, owner);
            return outputInstance;
        }

        public static void FillOutObjectWithJson (ref object targetItem, Type targetType, JsonValue sourceJsonValue, JsonInterpreterSettings settings = null)
        {
            FillOutObjectWithJson(ref targetItem, targetType, sourceJsonValue, settings, owner: null);
        }

        private static void FillOutObjectWithJson (ref object targetItem, Type targetType, JsonValue sourceJsonValue, JsonInterpreterSettings settings, Json owner)
        {
            if (targetItem != null)
            {
                targetType = targetItem.GetType();
            }

            settings = settings ?? JsonInterpreterSettings.Default;

            if (sourceJsonValue.IsValue)
            {
                Type nullableElementType = targetType.TargetsSameTypeAs(typeof(Nullable<>)) ? targetType.GenericTypeArguments[0] : null;
                Type effectiveType = nullableElementType ?? targetType;

                if (settings.StringParser.CanConvert(effectiveType))
                {
                    object parsedValue = settings.StringParser.Convert(sourceJsonValue.StringValue, effectiveType);
                    if (nullableElementType != null)
                    {
                        ConstructorInfo nullableCtor = typeof(Nullable<>).MakeGenericType(nullableElementType).GetConstructor(new[] { nullableElementType });
                        targetItem = nullableCtor.Invoke(new[] { parsedValue });
                    }
                    else
                    {
                        targetItem = parsedValue;
                    }
                }

                return;
            }

            if (sourceJsonValue.IsArray)
            {
                bool isArray = false, isList = false, isDictionary = false;
                Type elementType = null;
                MethodInfo dictionaryAdd = null;

                if (targetType.IsArray)
                {
                    isArray = true;
                    elementType = targetType.GetElementType();
                }
                else if (targetType.FindBaseTypeOrInterface(typeof(IList<>)) is Type listType)
                {
                    isList = true;
                    elementType = listType.GetGenericArguments()[0];
                }
                else if (targetType.FindBaseTypeOrInterface(typeof(IDictionary<,>)) is Type dictionaryType)
                {
                    isDictionary = true;
                    Type[] generics = dictionaryType.GetGenericArguments();
                    elementType = typeof(KeyValuePair<,>).MakeGenericType(generics[0], generics[1]);

                    Type collectionType = typeof(ICollection<>).MakeGenericType(elementType);
                    dictionaryAdd = collectionType.GetMethod("Add", new[] { elementType });
                    Debug.Assert(dictionaryAdd != null, $"Could not find add method for dictionary of type {targetType}");
                }

                JsonArray sourceCasted = (JsonArray)sourceJsonValue;
                for (int index = 0; index < sourceCasted.Count; ++index)
                {
                    JsonValue element = sourceCasted[index];
                    object built = BuildObjectForJson(elementType, element, settings, owner);

                    if (isArray)
                    {
                        ((IList)targetItem)[index] = built;
                    }
                    else if (isList)
                    {
                        ((IList)targetItem).Insert(index, built);
                    }
                    else if (isDictionary)
                    {
                        dictionaryAdd.Invoke(targetItem, new object[] { built });
                    }
                }
            }

            if (sourceJsonValue.IsDocument)
            {
                JsonDocument sourceCasted = (JsonDocument)sourceJsonValue;
                PropertyInfo[] allProperties = GetCachedWritableProperties(targetType);

                foreach (KeyValuePair<string, JsonValue> kvp in sourceCasted)
                {
                    if (kvp.Key == JsonDocument.kTypeIndicator)
                    {
                        continue;
                    }

                    PropertyInfo propToSet = FindPropertyForKey(allProperties, kvp.Key);
                    if (propToSet != null)
                    {
                        object newPropValue = BuildObjectForJson(propToSet.PropertyType, kvp.Value, settings, owner);
                        propToSet.SetValue(targetItem, newPropValue);
                    }
                }
            }
        }

        // ===============================[ Object-to-Json Implementation ]===========================
        public static void FillOutJsonBuilderForObject (object source, JsonBuilder target)
        {
            if (source == null)
            {
                return;
            }

            Type sourceType = source.GetType();

            // Simple value path.
            if (TryGetSimpleStringValue(target.BuilderSettings, sourceType, source, out bool isUsuallyQuoted, out string value))
            {
                target.IsValueUsualQuoteTarget = isUsuallyQuoted;
                ApplySimpleValue(target, value);
                return;
            }

            // Array / IEnumerable path.
            if (typeof(IEnumerable).IsAssignableFrom(sourceType))
            {
                JsonBuilder array = target.StartArray();
                IEnumerable enumerableValue = (IEnumerable)source;
                foreach (object arrayItemObj in enumerableValue)
                {
                    if (arrayItemObj == null)
                    {
                        if (sourceType.IsArray)
                        {
                            // Preserve element order on arrays - emit a stand-in for the null slot.
                            Type elementType = sourceType.GetElementType();
                            if (typeof(IEnumerable).IsAssignableFrom(elementType))
                            {
                                JsonBuilder emptyChildArr = array.StartArray();
                                emptyChildArr.End();
                            }
                            else if (elementType.IsSimpleType() || target.BuilderSettings.TryGetJsonValueStringMakerFor(elementType) != null)
                            {
                                JsonBuilder item = array.AddArrayItem(String.Empty);
                                item.IsValueUsualQuoteTarget = true;
                            }
                            else
                            {
                                JsonBuilder document = array.StartDocument();
                                document.End();
                            }
                        }
                        continue;
                    }

                    if (TryGetSimpleStringValue(target.BuilderSettings, arrayItemObj.GetType(), arrayItemObj, out bool itemIsQuoted, out string itemString))
                    {
                        JsonBuilder item = array.AddArrayItem(itemString);
                        item.IsValueUsualQuoteTarget = itemIsQuoted;
                    }
                    else if (arrayItemObj is IEnumerable)
                    {
                        FillOutJsonBuilderForObject(arrayItemObj, array);
                    }
                    else
                    {
                        JsonBuilder arrayItem = array.StartDocument();
                        FillOutJsonBuilderForObject(arrayItemObj, arrayItem);
                    }
                }

                target.End();
                return;
            }

            // KeyValuePair special case (only kicks in when KVP type-id flags are set in settings).
            if (target.BuilderSettings.HasAnyKVPTypeIdWriteInstructions
                && sourceType.IsGenericType
                && typeof(KeyValuePair<,>) == sourceType.GetGenericTypeDefinition())
            {
                if (!target.IsArrayItem)
                {
                    target = target.StartDocument();
                }

                PropertyInfo keyProp = sourceType.GetProperty("Key");
                object keyObj = keyProp.GetValue(source);
                if (TryGetTypeIdForType(target.BuilderSettings.KeyValuePairKeyTypeIdToWrite, keyObj?.GetType() ?? sourceType.GenericTypeArguments[0], out string keyTypeId))
                {
                    target.AddProperty(JsonDocument.kKVPKeyTypeIndicator, keyTypeId);
                }

                PropertyInfo valueProp = sourceType.GetProperty("Value");
                object valueObj = valueProp.GetValue(source);
                if (TryGetTypeIdForType(target.BuilderSettings.KeyValuePairValueTypeIdToWrite, valueObj?.GetType() ?? sourceType.GenericTypeArguments[1], out string valueTypeId))
                {
                    target.AddProperty(JsonDocument.kKVPValueTypeIndicator, valueTypeId);
                }

                ApplyDocumentProperty(target, source, keyProp);
                ApplyDocumentProperty(target, source, valueProp);
                return;
            }

            // Document path.
            PropertyInfo[] allProperties = GetCachedReadableProperties(
                sourceType,
                requiresSet: source.GetType().IsSimpleType() || !target.BuilderSettings.UseReadonlyObjectProperties
            );

            if (allProperties.Length == 0 && target.Parent != null)
            {
                target.Parent.Children.Remove(target);
                return;
            }

            if (!target.IsArrayItem)
            {
                target = target.StartDocument();
            }

            if (TryGetTypeIdForType(target.BuilderSettings.TypeIdToWrite, sourceType, out string typeId))
            {
                target.AddProperty(JsonDocument.kTypeIndicator, typeId);
            }

            foreach (PropertyInfo prop in allProperties)
            {
                ApplyDocumentProperty(target, source, prop);
            }
        }

        // ===============================[ Internal Helpers ]===========================
        private static void ApplyDocumentProperty (JsonBuilder target, object propSource, PropertyInfo propInfo)
        {
            string key = propInfo.Name;
            object sourceValue = propInfo.GetValue(propSource);

            if (sourceValue == null)
            {
                return;
            }

            if (TryGetSimpleStringValue(target.BuilderSettings, propInfo.PropertyType, sourceValue, out bool isUsuallyQuoted, out string simpleStringValue))
            {
                target.AddProperty(key, sourceValue, isUsuallyQuoted);
            }
            else
            {
                JsonBuilder propertyBuilder = target.StartProperty(key);
                FillOutJsonBuilderForObject(sourceValue, propertyBuilder);
            }
        }

        private static bool TryGetSimpleStringValue (JsonBuilderSettings settings, Type type, object instance, out bool isUsuallyQuoted, out string stringValue)
        {
            isUsuallyQuoted = false;
            JsonStringMaker maker = settings.TryGetJsonValueStringMakerFor(type);
            if (maker != null)
            {
                stringValue = maker(instance);
                if (type == typeof(string) || type == typeof(char) || type.IsEnum)
                {
                    isUsuallyQuoted = true;
                }
                else if (type.IsNumericType())
                {
                    isUsuallyQuoted = false;
                }
                else if (type == typeof(bool))
                {
                    isUsuallyQuoted = false;
                }
                else
                {
                    isUsuallyQuoted = true;
                }

                return true;
            }

            stringValue = null;
            return false;
        }

        private static void ApplySimpleValue (JsonBuilder target, string rawValue)
        {
            string escaped = rawValue == null ? null : rawValue.Replace("\"", "\\\"");
            if (target.IsValue)
            {
                target.Value = escaped;
            }
            else
            {
                target.DocumentKVPValue = new JsonBuilder(target);
                target.DocumentKVPValue.IsValueUsualQuoteTarget = target.IsValueUsualQuoteTarget;
                target.DocumentKVPValue.Value = escaped;
            }
        }

        // ===============================[ Type Id Helpers ]===========================
        internal static bool TryGetTypeIdForType (eTypeIdInfo typeWriteSettings, Type type, out string foundTypeId)
        {
            if (type == null)
            {
                foundTypeId = null;
                return false;
            }

            if (typeWriteSettings != eTypeIdInfo.None)
            {
                if (typeWriteSettings.HasFlag(eTypeIdInfo.TypeIdAttributed))
                {
                    string typeId = TypeIdRegistrar.GetTypeIdFor(type);
                    if (typeId != null)
                    {
                        foundTypeId = typeId;
                        return true;
                    }
                }
                if (typeWriteSettings.HasFlag(eTypeIdInfo.SystemTypeName))
                {
                    foundTypeId = type.Name;
                    return true;
                }
                else if (typeWriteSettings.HasFlag(eTypeIdInfo.FullyQualifiedSystemType))
                {
                    foundTypeId = type.AssemblyQualifiedName;
                    return true;
                }
            }

            foundTypeId = null;
            return false;
        }

        internal static bool TryGetTypeForTypeId (string typeIndicator, out Type foundType)
        {
            if (typeIndicator == null)
            {
                foundType = null;
                return false;
            }

            if (TypeIdRegistrar.TryGetType(typeIndicator, out foundType))
            {
                return true;
            }

            foundType = Type.GetType(typeIndicator);
            return foundType != null;
        }

        // ===============================[ Reflection Cache ]===========================
        private static PropertyInfo[] GetCachedReadableProperties (Type type, bool requiresSet)
        {
            ConcurrentDictionary<Type, PropertyInfo[]> cache = requiresSet ? g_propertyCacheWritable : g_propertyCacheReadable;
            return cache.GetOrAdd(type, t => ComputeProperties(t, requiresGet: true, requiresSet: requiresSet));
        }

        private static PropertyInfo[] GetCachedWritableProperties (Type type)
        {
            return g_propertyCacheWritable.GetOrAdd(type, t => ComputeProperties(t, requiresGet: false, requiresSet: true));
        }

        private static PropertyInfo[] ComputeProperties (Type targetType, bool requiresGet, bool requiresSet)
        {
            return TypeMetadataExtensionRegistrar
                .GetOrderedProperties(targetType, BindingFlags.Public | BindingFlags.Instance)
                .Where(prop => (!requiresGet || prop.GetGetMethod() != null)
                            && (!requiresSet || prop.GetSetMethod() != null)
                            && !TypeMetadataExtensionRegistrar.IsHidden(prop))
                .ToArray();
        }

        private static PropertyInfo FindPropertyForKey (PropertyInfo[] props, string key)
        {
            for (int i = 0; i < props.Length; ++i)
            {
                if (props[i].Name == key)
                {
                    return props[i];
                }
            }
            return null;
        }
    }
}
