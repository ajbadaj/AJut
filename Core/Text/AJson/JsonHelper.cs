namespace AJut.Text.AJson
{
    using AJut;
#if WINDOWS_UWP
    using System.Runtime.Serialization;
#else
    using AJut.IO;
    using System.IO;
#endif
    using AJut.Text;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Diagnostics;
    using AJut.TypeManagement;

    /// <summary>
    /// Utility class that acts as a starting point for parsing and building json, and other related utilities
    /// </summary>
    public static class JsonHelper
    {
        // For specialized trim
        private static readonly char[] kWhitespace = new[] { ' ', '\r', '\n', '\t' };

        private static JsonBuilder.Settings DefaultSettings { get; set; }

        static JsonHelper ()
        {
            DefaultSettings = new JsonBuilder.Settings();
        }

        /// <summary>
        /// Register all <see cref="JsonTypeIdAttribute"/> type ids from the given assembly
        /// </summary>
        /// <param name="assembly">The assembly to search</param>
        /// <param name="forceSearch">Whether or not to search again if the assembly has already been searched (cached by name)</param>
        [Obsolete("JsonTypeId is being deprecated in favor of the more generic TypeId, please use TypeIdRegistrar.RegisterAllTypeIds instead")]
        public static void RegisterAllTypeIds (Assembly assembly, bool forceSearch = false)
        {
            TypeIdRegistrar.RegisterAllTypeIds(assembly, forceSearch);
        }

        public static void RegisterTypeId<T> (string id)
        {
            TypeIdRegistrar.RegisterTypeId<T>(id);
        }

        public static void RegisterTypeId (string id, Type type)
        {
            TypeIdRegistrar.RegisterTypeId(id, type);
        }

        // TODO: Should add support to force commas or not, then just use newline as a text separator

#if !WINDOWS_UWP
        /// <summary>
        /// Parses the passed in file at the given file path, and returns a non-<c>null</c> <see cref="Json"/> instance.
        /// </summary>
        /// <param name="filePath">Path of the file to parse</param>
        /// <param name="rules">Parser rules</param>
        /// <returns>A non-<c>null</c> <see cref="Json"/> instance</returns>
        public static Json ParseFile (string filePath, ParserRules rules = null)
        {
            if (PathHelpers.IsValidAsPath(filePath) && File.Exists(filePath))
            {
                return ParseText(File.ReadAllText(filePath), rules);
            }
            else
            {
                Json jsonOutput = new Json(null);
                jsonOutput.AddError($"File path '{filePath ?? "<null>"}' does not exist on disk, or is an invalid path!");
                return jsonOutput;
            }
        }

        /// <summary>
        /// Parses the text from an existing <see cref="FileTextTracker"/> file, and returns a non-<c>null</c> <see cref="Json"/> instance.
        /// </summary>
        /// <param name="file">A pre-made <see cref="FileTextTracker"/> to parse the contents of. Note: Will act as the returned <see cref="Json"/> instnace's TextTracking.</param>
        /// <param name="rules">Parser rules</param>
        /// <returns>A non-<c>null</c> <see cref="Json"/> instance</returns>
        public static Json ParseFile (FileTextTracker file, ParserRules rules = null)
        {
            return RunParse(file, rules);
        }
#endif

        /// <summary>
        /// Parses the passed in text, and returns a non-<c>null</c> <see cref="Json"/> instance.
        /// </summary>
        /// <param name="jsonText">Some json text.</param>
        /// <param name="rules">Parser rules</param>
        /// <returns>A non-<c>null</c> <see cref="Json"/> instance</returns>
        public static Json ParseText (string jsonText, ParserRules rules = null)
        {
            return RunParse(new TrackedStringManager(jsonText), rules);
        }

        /// <summary>
        /// Parses the text from the passed in <see cref="TrackedStringManager"/>, and returns a non-<c>null</c> <see cref="Json"/> instance.
        /// </summary>
        /// <param name="jsonText">Some json text.</param>
        /// <param name="rules">Parser rules</param>
        /// <returns>A non-<c>null</c> <see cref="Json"/> instance</returns>
        public static Json ParseText (TrackedStringManager source, ParserRules rules = null)
        {
            return RunParse(source, rules);
        }

        private static Json RunParse (TrackedStringManager tracker, ParserRules rules)
        {
            Json output = new Json(tracker);
            try
            {
                if (tracker.Text == null)
                {
                    output.AddError("Null source text provided!");
                    tracker.HasChanges = false;
                    return output;
                }

                JsonTextIndexer indexer = new JsonTextIndexer(tracker.Text, rules);
                // TODO: Handle case where root is document without braces( ie "item : value, item2 : value2")

                int indexOfFirstOpenBracket = indexer.NextAny(0, '{', '[');

                // ============ No Brackets ===========
                if (indexOfFirstOpenBracket == -1)
                {
                    output.Data = new JsonValue(output.TextTracking, 0, indexer.NextAny(0, '}', ']'), false);
                    tracker.HasChanges = false;
                    return output;
                }

                // ============ Has Brackets ===========
                char nextBracket = output.TextTracking.Text[indexOfFirstOpenBracket];
                if (nextBracket == '{')
                {
                    output.Data = new JsonDocument(indexer, output.TextTracking, indexOfFirstOpenBracket, out int endIndex);
                    tracker.HasChanges = false;
                    return output;
                }
                else // if(nextBracket == '[')
                {
                    output.Data = new JsonArray(indexer, output.TextTracking, indexOfFirstOpenBracket, out int endIndex);
                    tracker.HasChanges = false;
                    return output;
                }
            }
            catch (Exception exc)
            {
                output.AddError(exc.ToString());
            }

            return output;
        }

        /// <summary>
        /// Starting point if you are building AJson via a builder.
        /// </summary>
        /// <example>
        /// <see cref="JsonHelper"/>.MakeRootBuilder().StartDocument().AddProperty("test", 2).Finalize(); 
        /// would give you an Json object with a JsonDocument structured like this: { "test": 2 }
        /// </example>
        /// <param name="settings">The builder settings to use while constructing the <see cref="Json"/> (optional, null will give you the default settings)</param>
        /// <returns>A <see cref="JsonBuilder"/> for you to chain construction commands to in order to form <see cref="Json"/>.</returns>
        public static JsonBuilder MakeRootBuilder (JsonBuilder.Settings settings = null)
        {
            return new JsonBuilder(settings ?? DefaultSettings);
        }

        internal static JsonBuilder MakeValueBuilder (object value, JsonBuilder.Settings settings = null)
        {
            return new JsonBuilder(settings, value);
        }

        /// <summary>
        /// Makes a <see cref="JsonBuilder"/> for the given object
        /// </summary>
        /// <param name="instance">The instance to build the <see cref="JsonBuilder"/> from</param>
        /// <param name="settings">The builder settings to use while constructing the <see cref="Json"/> (optional, null will give you the default settings)</param>
        /// <returns>A <see cref="JsonBuilder"/> for you to chain construction commands to in order to form <see cref="Json"/>.</returns>
        public static Json BuildJsonForObject (object instance, JsonBuilder.Settings settings = null)
        {
            JsonBuilder output = MakeRootBuilder(settings);
            if (instance != null)
            {
                FillOutJsonBuilderForObject(instance, output);
            }

            return output.Finalize();
        }

        /// <summary>
        /// Builds an object from the provided <see cref="Json"/>.
        /// </summary>
        /// <typeparam name="T">The type of object to build</typeparam>
        /// <param name="sourceJson">The <see cref="Json"/> used</param>
        /// <param name="settings">Special interpretter settings</param>
        /// <returns>The object built from the <see cref="Json"/></returns>
        public static T BuildObjectForJson<T> (Json sourceJson, JsonInterpretterSettings settings = null)
        {
            return (T)BuildObjectForJson(typeof(T), sourceJson, settings);
        }

        /// <summary>
        /// Builds an object from the provided <see cref="Json"/>.
        /// </summary>
        /// <typeparam name="T">The type of object to build</typeparam>
        /// <param name="sourceJsonValue">The <see cref="JsonValue"/> used</param>
        /// <param name="settings">Special interpretter settings</param>
        /// <returns>The object built from the <see cref="JsonValue"/></returns>
        public static T BuildObjectForJson<T> (JsonValue sourceJsonValue, JsonInterpretterSettings settings = null)
        {
            return (T)BuildObjectForJson(typeof(T), sourceJsonValue, settings);
        }

        /// <summary>
        /// Builds a list of objects from the <see cref="JsonArray"/>
        /// </summary>
        /// <typeparam name="T">The type of object to build</typeparam>
        /// <param name="sourceJsonArray">The <see cref="JsonValue"/> being evaluated</param>
        /// <param name="settings">Special interpretter settings</param>
        /// <returns>A list built with objects parsed from the array</returns>
        public static List<T> BuildObjectListForJson<T> (JsonArray sourceJsonArray, JsonInterpretterSettings settings = null)
        {
            var list = new List<T>();
            foreach (JsonValue value in sourceJsonArray)
            {
                list.Add(BuildObjectForJson<T>(value, settings));
            }

            return list;
        }

        /// <summary>
        /// Build an object for json that has been typed (using the built in <see cref="JsonDocument.kTypeIndicator"/> property)
        /// </summary>
        public static object BuildObjectForTypedJson (Json sourceJson, JsonInterpretterSettings settings = null)
        {
            return sourceJson.HasErrors ? null : BuildObjectForTypedJson(sourceJson.Data, settings);
        }

        /// <summary>
        /// Build an object for json that has been typed (using the built in <see cref="JsonDocument.kTypeIndicator"/> property)
        /// </summary>
        public static object BuildObjectForTypedJson (JsonValue sourceJson, JsonInterpretterSettings settings = null)
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

        /// <summary>
        /// Builds an object from the provided <see cref="Json"/>.
        /// </summary>
        /// <param name="type">The type of object to build</param>
        /// <param name="sourceJson">The <see cref="Json"/> used</param>
        /// <param name="settings">Special interpretter settings</param>
        /// <returns>The object built from the <see cref="Json"/></returns>
        public static object BuildObjectForJson (Type type, Json sourceJson, JsonInterpretterSettings settings = null)
        {
            if (sourceJson.HasErrors)
            {
                return null;
            }

            return BuildObjectForJson(type, sourceJson.Data, settings);
        }

        /// <summary>
        /// Builds an object from the provided <see cref="Json"/>.
        /// </summary>
        /// <param name="type">The type of object to build</param>
        /// <param name="sourceJsonValue">The <see cref="JsonValue"/> used</param>
        /// <param name="settings">Special interpretter settings</param>
        /// <returns>The object built from the <see cref="JsonValue"/></returns>
        public static object BuildObjectForJson (Type type, JsonValue sourceJsonValue, JsonInterpretterSettings settings = null)
        {
            if (typeof(JsonValue).IsAssignableFrom(type))
            {
                return sourceJsonValue;
            }

            settings = settings ?? JsonInterpretterSettings.Default;
            object outputInstance = null;

            // If we're elevating one of the properties of this thing, as itself, then we need to
            //  alter the type we're operating on
            string elevatedPropertyName = type.GetAttributes<JsonPropertyAsSelfAttribute>()?.FirstOrDefault()?.PropertyName;
            if (elevatedPropertyName != null)
            {
                // If we're doing elevation, then we are dealing with an object at this point
                //  we'll need to create the instance, and set the value on the property indicated
                PropertyInfo targetProp = type.GetProperty(elevatedPropertyName, BindingFlags.Public | BindingFlags.Instance);
                if (targetProp != null)
                {
                    outputInstance = Activator.CreateInstance(type);

                    // Basically forward the json value onto building the child, and set value of that to the output instance
                    object childInstance = BuildObjectForJson(targetProp.PropertyType, sourceJsonValue, settings);
                    targetProp.SetValue(outputInstance, childInstance);

                    return outputInstance;
                }
            }

            if (sourceJsonValue.IsDocument)
            {
                JsonDocument docVersion = (JsonDocument)sourceJsonValue;
                string typeIndicator = docVersion.ValueFor(JsonDocument.kTypeIndicator)?.StringValue;
                if (typeIndicator != null)
                {
                    if (TryGetTypeForTypeId(typeIndicator, out Type targetType))
                    {
                        outputInstance = Activator.CreateInstance(targetType);
                    }
                    else
                    {
                        Logger.LogError($"Target type provided {typeIndicator ?? "-null-"} could not be translated, skipping");
                    }
                }
            }
            else if (sourceJsonValue.IsArray)
            {
                var array = (JsonArray)sourceJsonValue;

                // You have to create arrays with an argument
                if (type.IsArray)
                {
                    outputInstance = Activator.CreateInstance(type, array.Count);
                }
            }

            if (outputInstance == null)
            {
                outputInstance = settings.ConstructInstanceFor(type, sourceJsonValue);
            }

            FillOutObjectWithJson(ref outputInstance, sourceJsonValue, settings);
            return outputInstance;
        }

        /// <summary>
        /// Fills out an existing instance with <see cref="JsonValue"/>.
        /// </summary>
        /// <param name="targetItem">The instance to fill out</param>
        /// <param name="sourceJsonValue">The <see cref="JsonValue"/> used</param>
        /// <param name="settings">Special interpretter settings</param>
        public static void FillOutObjectWithJson (ref object targetItem, JsonValue sourceJsonValue, JsonInterpretterSettings settings = null)
        {
            Type targetType = targetItem.GetType();
            settings = settings ?? JsonInterpretterSettings.Default;

            if (sourceJsonValue.IsValue)
            {
                if (settings.StringParser.CanConvert(targetType))
                {
                    targetItem = settings.StringParser.Convert(sourceJsonValue.StringValue, targetType);
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
                    var generics = dictionaryType.GetGenericArguments();
                    elementType = typeof(KeyValuePair<,>).MakeGenericType(generics[0], generics[1]);

                    var collectionType = typeof(ICollection<>).MakeGenericType(elementType);
                    dictionaryAdd = collectionType.GetMethod("Add", new[] { elementType });
                    Debug.Assert(dictionaryAdd != null, $"Could not find add method for dictionary of type {targetType}");
                }

                JsonArray sourceCasted = (JsonArray)sourceJsonValue;
                for (int index = 0; index < sourceCasted.Count; ++index)
                {
                    JsonValue value = sourceCasted[index];
                    object element = BuildObjectForJson(elementType, value, settings);

                    // If it's an array, then we've preallocated it the correct length already
                    if (isArray)
                    {
                        ((IList)targetItem)[index] = element;
                    }
                    else if (isList)
                    {
                        ((IList)targetItem).Insert(index, element);
                    }
                    else if (isDictionary)
                    {
                        dictionaryAdd.Invoke(targetItem, new object[] { element });
                    }
                }
            }
            if (sourceJsonValue.IsDocument)
            {
                JsonDocument sourceCasted = (JsonDocument)sourceJsonValue;
                PropertyInfo[] allProperties = GetPropertiesFrom(targetType, false, true
#if WINDOWS_UWP
                    // Unfortunately, there aren't these kinds of settings in this direciton
                    //  going to say that opt in IS NOT required for this way anyway for now
                    , false
#endif
                    );

                foreach (var kvp in sourceCasted)
                {
                    if (kvp.Key == JsonDocument.kTypeIndicator)
                    {
                        continue;
                    }

                    PropertyInfo propToSet = allProperties.FirstOrDefault(prop => prop.Name == kvp.Key);
                    if (propToSet != null)
                    {
                        object newPropValue = null;

                        // Resolve runtime type evaluation
                        var runtimeTypeEval = propToSet.GetAttributes<JsonRuntimeTypeEvalAttribute>()?.FirstOrDefault();
                        if (runtimeTypeEval != null && kvp.Value.IsDocument)
                        {
                            JsonDocument valueDocCasted = (JsonDocument)kvp.Value;
                            if (valueDocCasted.TryGetValue(JsonDocument.kTypeIndicator, out string typeIdForValue)
                                && valueDocCasted.ValueFor(JsonDocument.kRuntimeTypeEvalValue) is JsonValue foundRuntimeValueIndicator
                                && JsonHelper.TryGetTypeForTypeId(typeIdForValue, out Type foundType))
                            {
                                newPropValue = BuildObjectForJson(foundType, foundRuntimeValueIndicator, settings);
                            }
                        }

                        // If we're not dealing with runtime type evaluation, use the property type
                        if (newPropValue == null)
                        {
                            newPropValue = BuildObjectForJson(propToSet.PropertyType, kvp.Value, settings);
                        }

                        propToSet.SetValue(targetItem, newPropValue);
                    }
                }
            }
        }

        private static void SetValueOnObject (object target, Type targetType, PropertyInfo prop, JsonValue sourceValue)
        {
            if (sourceValue.IsDocument)
            {
                foreach (KeyValuePair<TrackedString, JsonValue> item in (JsonDocument)sourceValue)
                {
                    try
                    {
                        PropertyInfo childProp = targetType.GetProperty(item.Key.StringValue);
                        if (prop != null)
                        {
                            SetValueOnObject(target, targetType, childProp, item.Value);
                        }

                    }
                    catch (Exception exc)
                    {
                        Logger.LogError(exc);
                    }

                }
            }
        }

        /// <summary>
        /// The recursive utility that fills out a <see cref="JsonBuilder"/> based off of the passed in source
        /// </summary>
        /// <param name="source">The object to pull data from</param>
        /// <param name="target">The builder to fill out</param>
        public static void FillOutJsonBuilderForObject (object source, JsonBuilder target)
        {
            Type sourceType = source.GetType();
            bool isUsuallyQuoted;

            // If we're elevating one of the properties of this thing, as itself, then we need to
            //  alter the type we're operating on
            string elevatedPropertyName = sourceType.GetAttributes<JsonPropertyAsSelfAttribute>()?.FirstOrDefault()?.PropertyName;
            if (elevatedPropertyName != null)
            {
                PropertyInfo targetProp = sourceType.GetProperty(elevatedPropertyName, BindingFlags.Public | BindingFlags.Instance);
                if (targetProp != null)
                {
                    source = targetProp.GetValue(source);
                    sourceType = source.GetType();
                }
            }

            // ----------- Handle Simple Values ------------
            if (_CheckForSettingsRegisteredSimpleValue(sourceType, source, out isUsuallyQuoted, out string value))
            {
                target.IsValueUsualQuoteTarget = isUsuallyQuoted;
                _SetValue(value);
                return;
            }

            // ----------- Handle Array ------------
            if (typeof(IEnumerable).IsAssignableFrom(sourceType))
            {
                JsonBuilder array = target.StartArray();
                IEnumerable enumerableValue = (IEnumerable)source;
                if (enumerableValue != null)
                {
                    foreach (object arrayItemObj in enumerableValue)
                    {
                        // =================[ Null Array Item ]===================
                        if (arrayItemObj == null)
                        {
                            // We've got a null element, but bad news it's an array that means we
                            //  *MUST* preserver element order and add an empty element. If it's
                            //  not an array, order doesn't matter, so we'll just skip it.
                            if (sourceType.IsArray)
                            {
                                Type elementType = sourceType.GetElementType();
                                if (typeof(IEnumerable).IsAssignableFrom(elementType))
                                {
                                    var emptyChildArr = array.StartArray();
                                    emptyChildArr.End();
                                }
                                else if (elementType.IsSimpleType() || target.BuilderSettings.TryGetJsonValueStringMakerFor(elementType) != null)
                                {
                                    var item = array.AddArrayItem(String.Empty);
                                    item.IsValueUsualQuoteTarget = true;
                                }
                                else
                                {
                                    var document = array.StartDocument();
                                    document.End();
                                }
                            }

                            continue;
                        }

                        // =================[ Normal Array Item ]===================
                        // Simple array item
                        if (_CheckForSettingsRegisteredSimpleValue(arrayItemObj.GetType(), arrayItemObj, out isUsuallyQuoted, out string stringValue))
                        {
                            var item = array.AddArrayItem(stringValue);
                            item.IsValueUsualQuoteTarget = isUsuallyQuoted;
                        }
                        // Array of array item
                        else if (arrayItemObj is IEnumerable)
                        {
                            FillOutJsonBuilderForObject(arrayItemObj, array);
                        }
                        // Document array item
                        else
                        {
                            JsonBuilder arrayItem = array.StartDocument();
                            FillOutJsonBuilderForObject(arrayItemObj, arrayItem);
                        }
                    }
                }

                target.End();
                return;
            }

            // ----------- Handle KeyValuePair special case -----------
            if (target.BuilderSettings.HasAnyKVPTypeIdWriteInstructions
                && sourceType.IsGenericType 
                && typeof(KeyValuePair<,>) == sourceType.GetGenericTypeDefinition())
            {
                // [Special Case] Array item documents get started when they're added
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

                _HandleApplyProperty(source, keyProp);
                _HandleApplyProperty(source, valueProp);
                return;
            }

            // ----------- Handle Document ------------
            PropertyInfo[] allProperties = GetPropertiesFrom(source.GetType(), true, source.GetType().IsSimpleType() || !target.BuilderSettings.UseReadonlyObjectProperties
#if WINDOWS_UWP
                , target.BuilderSettings.UWP_RequireOptInViaDataMemberAttribute
#endif
            );

            if (allProperties.Length == 0 && target.Parent != null)
            {
                target.Parent.Children.Remove(target);
                return;
            }

            // [Special Case] Array item documents get started when they're added
            if (!target.IsArrayItem)
            {
                target = target.StartDocument();
            }

            // Make sure to write out the type for the instance if requested
            if (TryGetTypeIdForType(target.BuilderSettings.TypeIdToWrite, sourceType, out string typeId))
            {
                target.AddProperty(JsonDocument.kTypeIndicator, typeId);
            }

            foreach (PropertyInfo prop in allProperties)
            {
                _HandleApplyProperty(source, prop);
            }

            // ----------- Handle Value (Simple Type) ------------
            void _HandleApplyProperty (object _propSource, PropertyInfo _propInfo)
            {
                object _sourceValue = _propInfo.GetValue(_propSource);

                if (_sourceValue != null)
                {
                    var runtimeTypeEval = _propInfo.GetAttributes<JsonRuntimeTypeEvalAttribute>()?.FirstOrDefault();
                    if (runtimeTypeEval != null && JsonHelper.TryGetTypeIdForType(runtimeTypeEval.TypeWriteTarget, _sourceValue?.GetType(), out string _foundTypeId))
                    {
                        JsonBuilder propertyObjectBuilder = target.StartProperty(_propInfo.Name).StartDocument();
                        propertyObjectBuilder.AddProperty(JsonDocument.kTypeIndicator, _foundTypeId);
                        JsonBuilder typedValueObjectBuilder = propertyObjectBuilder.StartProperty(JsonDocument.kRuntimeTypeEvalValue);
                        FillOutJsonBuilderForObject(_sourceValue, typedValueObjectBuilder);
                    }
                    else if (_CheckForSettingsRegisteredSimpleValue(_propInfo.PropertyType, _sourceValue, out isUsuallyQuoted, out string simpleStringValue))
                    {
                        var created = target.AddProperty(_propInfo.Name, simpleStringValue);
                        created.IsValueUsualQuoteTarget = isUsuallyQuoted;
                    }
                    else
                    {
                        JsonBuilder propertyBuilder = target.StartProperty(_propInfo.Name);
                        FillOutJsonBuilderForObject(_sourceValue, propertyBuilder);
                    }
                }
            }

            bool _CheckForSettingsRegisteredSimpleValue (Type _type, object _instance, out bool _isUsuallyQuoted, out string simplifiedStringValue)
            {
                _isUsuallyQuoted = false;
                var valueBuilder = target.BuilderSettings.TryGetJsonValueStringMakerFor(_type);
                if (valueBuilder != null)
                {
                    simplifiedStringValue = valueBuilder(_instance);
                    if (_type == typeof(string) || _type == typeof(char)
#if WINDOWS_UWP
                        || _type.IsEnum())
#else
                        || _type.IsEnum)
#endif
                    {
                        _isUsuallyQuoted = true;
                    }

                    return true;
                }

                simplifiedStringValue = null;
                return false;
            }

            void _SetValue (string _value)
            {
                if (target.IsValue)
                {
                    target.Value = _value;
                }
                else
                {
                    target.DocumentKVPValue = new JsonBuilder(target);
                    target.DocumentKVPValue.Value = _value;
                }
            }
        }

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

        internal static void FindInsertStart(string sourceText, ref int targetStart)
        {
            while (kWhitespace.Contains(sourceText[targetStart - 1]))
            {
                --targetStart;
            }
        }

        internal static int EvaluteBegginningTabOffset(JsonValue source, JsonBuilder.Settings settings)
        {
            var capture = RegexHelper.Match(source.StringValue, (source.IsDocument ? "{" : @"\[") + @" *(\s*)").GetMostSpecificCapture();
            if (capture == null)
            {
                Logger.LogInfo("Unable to determine current tabbing due to weirdly formatted start. If the document is formatted unexpectedly, that may lead to serious failures. Also, as a result of this error, tabbing may look funny.");
            }
            else
            {
                // Figure out the current tabbing
                string tabText = capture.Value;

                if (tabText == String.Empty)
                {
                    return 0;
                }
                // If the document seems to use the same tabbing as the settings asked for, then use that
                else if (tabText.Contains(settings.Tabbing))
                {
                    return tabText.NumberOfTimesContained(settings.Tabbing);
                }
                // Otherwise we'll figure out a best guess for tabbing
                else
                {
                    // If the tabbing seems to use tabs, then number of tabs will do
                    if (tabText[0] == '\t')
                    {
                        return tabText.Length;
                    }
                    // If tabbing seems to use some othe sequence, then we'll say every 4
                    //  count as one tab.
                    else
                    {
                        return tabText.Length / 4;
                    }
                }
            }

            // If we couldn't figure anything out anywhere, default to 1
            return 1;
        }

        internal static string TrimUnquotedValue(string jsonText, out int startOffset)
        {
            startOffset = 0;
            if (String.IsNullOrEmpty(jsonText))
            {
                return String.Empty;
            }

            string trimmed = jsonText.TrimStart(kWhitespace);

            // Store the offset as how much was trimmed off
            startOffset = jsonText.Length - trimmed.Length;

            // Note: It's ok to trim end without worrying because the only thing recorded is the start index.
            //          End index is found by using start index + string's length.
            return trimmed.TrimEnd(kWhitespace);
        }

#if WINDOWS_UWP
        private static PropertyInfo[] GetPropertiesFrom(Type targetType, bool requiresGet, bool requiresSet, bool requireOptInViaDataMemberAttribute)
        {
            return targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                .Where(prop => (!requiresGet || prop.HasPublicGetProperty())
                                                            && (!requiresSet || prop.HasPublicSetProperty())
                                                            && !AttributeHelper.HasAny<JsonIgnoreAttribute>(prop.GetCustomAttributes(true))
                                                            && (!requireOptInViaDataMemberAttribute || AttributeHelper.HasAny<DataMemberAttribute>(prop.GetCustomAttributes(true))))
                                                        .ToArray();
        }
#else
        private static PropertyInfo[] GetPropertiesFrom(Type targetType, bool requiresGet, bool requiresSet)
        {
            return targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                .Where(prop => (!requiresGet || prop.GetGetMethod() != null)
                                                            && (!requiresSet || prop.GetSetMethod() != null)
                                                            && !prop.IsTaggedWithAttribute<JsonIgnoreAttribute>())
                                                    .ToArray();
        }
#endif


        internal class IndexTrackingHelper
        {
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
            public bool IsInsideQuotes { get; set; }

            public bool IsValid
            {
                get
                {
                    return this.StartIndex != -1 && this.EndIndex != -1 && this.EndIndex >= this.StartIndex;
                }
            }

            public static implicit operator bool (IndexTrackingHelper h)
            {
                return h.IsValid;
            }

            public void Reset()
            {
                this.StartIndex = -1;
                this.EndIndex = -1;
                this.IsInsideQuotes = false;
            }

            public TrackedString CreateTS(TrackedStringManager tracker)
            {
                string target = tracker.Text.SubstringWithIndices(this.StartIndex, this.EndIndex);
                int startOffset = 0;

                // If it's unquoted text, then we need to trim
                if (!this.IsInsideQuotes)
                {
                    target = JsonHelper.TrimUnquotedValue(target, out startOffset);
                }

                return tracker.Track(this.StartIndex + startOffset, target.Length);
            }
        }
    }
}
