namespace AJut.Text.AJson
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using AJut;
    using AJut.IO;
    using AJut.TypeManagement;

    // The reflection path uses Type.GetProperties / Type.GetFields / Activator.CreateInstance
    // throughout. Each Type-taking entry point carries a DynamicallyAccessedMembers annotation
    // so the IL trimmer keeps the relevant members. The kReflectionRequirements constant
    // (PublicProperties + PublicFields + PublicParameterlessConstructor) is the minimum set the
    // reflection path needs - properties and fields to walk, parameterless ctor to construct.
    // Source-generator-handled types do not need these; they pre-emit explicit code referencing
    // each member by name.

    /// <summary>
    /// Public entry point for V2 AJson - parsing, building, and POCO conversion.
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// The AJson text format this AJson writes into its version marker
        /// (<see cref="JsonDocument.kAJsonVersionIndicator"/>). 2 is the first: strings escaped to
        /// the JSON spec, DateTimes as round-trip ISO 8601, and public fields written as well as
        /// properties, which puts the System.Numerics vectors and matrices down as documents of
        /// their components. Text with no marker reads as 0.
        /// </summary>
        public const int kCurrentAJsonVersion = 2;

        private static JsonBuilderSettings g_defaultBuilderSettings = new JsonBuilderSettings();

        // Per-type reflection caches. Bounded by Type identity (assembly-bounded), no leak risk.
        // ConcurrentDictionary chosen for thread-safety in case AJson is called concurrently.
        // The lists depend on TypeMetadataExtensionRegistrar state as well as on the Type, so the
        // registrar invalidates them whenever a registration changes (InvalidateMemberCachesFor).
        // The member sets are built from the member lists, which is why they are two caches: a
        // GetOrAdd factory that asked its own cache for the same type would recurse.
        private static readonly ConcurrentDictionary<Type, DataMember[]> g_memberCache
            = new ConcurrentDictionary<Type, DataMember[]>();
        private static readonly ConcurrentDictionary<Type, MemberSet> g_memberSetCache
            = new ConcurrentDictionary<Type, MemberSet>();

        // ===============================[ AJson Version ]===========================
        /// <summary>
        /// Whether text written from a <see cref="Json"/> carries the AJson version marker, unless
        /// the build's <see cref="JsonBuilderSettings.WriteAJsonVersion"/> or the json's own
        /// <see cref="Json.WriteAJsonVersion"/> says otherwise. On by default.
        /// </summary>
        public static bool WriteAJsonVersion { get; set; } = true;

        /// <summary>
        /// The default for <see cref="ParserRules.WarnIfAJsonVersionBelow"/>: a read of a root
        /// document whose AJson version is below this logs a warning. 0, the default, never warns,
        /// since a reader cannot know whether the writer had the marker on.
        /// </summary>
        public static int WarnIfAJsonVersionBelow { get; set; } = 0;

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

        // ===============================[ Data Classification ]===========================
        // What json node a clr object maps to when built - a scalar value, an array, or a document.
        // Same three-way split FillOutJsonBuilderForObject walks (simple value, then IEnumerable,
        // then document), pulled out so the value-builder can decide up front whether to start as a
        // value or hold off for StartDocument / StartArray.
        public static bool IsValueData (object value, JsonBuilderSettings settings = null)
        {
            if (value == null)
            {
                return false;
            }

            settings = settings ?? g_defaultBuilderSettings;
            return settings.TryGetJsonValueStringMakerFor(value.GetType()) != null;
        }

        public static bool IsArrayData (object value, JsonBuilderSettings settings = null)
        {
            return value != null
                && !IsValueData(value, settings)
                && value is IEnumerable;
        }

        public static bool IsDocumentData (object value, JsonBuilderSettings settings = null)
        {
            return value != null
                && !IsValueData(value, settings)
                && !IsArrayData(value, settings);
        }

        // ===============================[ Object-from-Json Entry Points ]===========================
        private const DynamicallyAccessedMemberTypes kReflectionRequirements
            = DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicFields
            | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor;

        public static T BuildObjectForJson<[DynamicallyAccessedMembers(kReflectionRequirements)] T> (Json sourceJson, JsonInterpreterSettings settings = null)
        {
            return (T)BuildObjectForJson(typeof(T), sourceJson, settings);
        }

        public static T BuildObjectForJson<[DynamicallyAccessedMembers(kReflectionRequirements)] T> (JsonValue sourceJsonValue, JsonInterpreterSettings settings = null)
        {
            return (T)BuildObjectForJson(typeof(T), sourceJsonValue, settings);
        }

        public static List<T> BuildObjectListForJson<[DynamicallyAccessedMembers(kReflectionRequirements)] T> (JsonArray sourceJsonArray, JsonInterpreterSettings settings = null)
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

        public static object BuildObjectForJson ([DynamicallyAccessedMembers(kReflectionRequirements)] Type type, Json sourceJson, JsonInterpreterSettings settings = null)
        {
            if (sourceJson.HasErrors)
            {
                return null;
            }

            return BuildObjectForJson(type, sourceJson.Data, settings, sourceJson);
        }

        public static object BuildObjectForJson ([DynamicallyAccessedMembers(kReflectionRequirements)] Type type, JsonValue sourceJsonValue, JsonInterpreterSettings settings = null)
        {
            return BuildObjectForJson(type, sourceJsonValue, settings, owner: null);
        }

        // ===============================[ Object-from-Json Implementation ]===========================
        internal static object BuildObjectForJson ([DynamicallyAccessedMembers(kReflectionRequirements)] Type type, JsonValue sourceJsonValue, JsonInterpreterSettings settings, Json owner)
        {
            // No resolvable target type. Happens for a polymorphic (object-typed) value that carries
            //  no __type discriminator - a boxed array is the usual culprit. Degrade gracefully: log
            //  it on the owning Json and hand back null rather than dereferencing a null type below.
            if (type == null)
            {
                owner?.AddError($"Could not resolve a target type for a json value (polymorphic value with no '{JsonDocument.kTypeIndicator}' discriminator). Skipping, value left null.");
                return null;
            }

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

            // Class-level [JsonPropertyAsSelf] elevation - the json data we have is the inner
            //  property's content rather than a doc with a named entry. Build an instance of the
            //  outer type, build the inner value via the property type, set, return. Backward-compat
            //  read of the legacy non-elevated shape (a doc that still carries the inner as a named
            //  entry) routes through the same path by pulling that entry's value first.
            JsonPropertyAsSelfAttribute elevatedAttr = type.GetCustomAttribute<JsonPropertyAsSelfAttribute>(inherit: true);
            if (elevatedAttr != null)
            {
                PropertyInfo elevatedProp = type.GetProperty(elevatedAttr.PropertyName, BindingFlags.Public | BindingFlags.Instance);
                if (elevatedProp != null)
                {
                    JsonValue elevatedSource = sourceJsonValue;
                    if (sourceJsonValue is JsonDocument legacyDoc)
                    {
                        JsonValue legacyMatch = legacyDoc.ValueFor(elevatedAttr.PropertyName);
                        if (legacyMatch != null)
                        {
                            elevatedSource = legacyMatch;
                        }
                    }

                    object outerInstance = AJutActivator.CreateInstanceOf(type);
                    object innerValue = BuildObjectForJson(elevatedProp.PropertyType, elevatedSource, settings, owner);
                    elevatedProp.SetValue(outerInstance, innerValue);
                    return outerInstance;
                }
            }

            // Resolve the concrete target type up front - the dispatch check needs the runtime
            //  type, which comes from the doc's __type indicator when present.
            Type concreteType = type;
            if (sourceJsonValue.IsDocument)
            {
                JsonDocument docVersion = (JsonDocument)sourceJsonValue;
                string typeIndicator = docVersion.ValueFor(JsonDocument.kTypeIndicator)?.StringValue;
                if (typeIndicator != null)
                {
                    if (TryGetTypeForTypeId(typeIndicator, out Type targetType))
                    {
                        concreteType = targetType;
                    }
                    else
                    {
                        owner?.AddError($"Target type provided '{typeIndicator}' could not be translated, skipping");
                    }
                }
            }

            // Source-gen fast path. If a generated reader is registered for the concrete type, it
            //  handles construction and population in one call - we are done.
            if (AJsonGeneratedDispatch.TryGet(concreteType, out AJsonGeneratedSerializer generated))
            {
                return generated.Reader(sourceJsonValue, settings, owner);
            }

            IReadOnlySet<string> keysConsumedByConstructor = null;
            if (sourceJsonValue.IsDocument && concreteType != type)
            {
                outputInstance = settings.ConstructInstanceFor(concreteType, sourceJsonValue, owner, out keysConsumedByConstructor);
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
                outputInstance = settings.ConstructInstanceFor(type, sourceJsonValue, owner, out keysConsumedByConstructor);
            }

            FillOutObjectWithJson(ref outputInstance, type, sourceJsonValue, settings, owner, keysConsumedByConstructor);
            return outputInstance;
        }

        public static void FillOutObjectWithJson (ref object targetItem, [DynamicallyAccessedMembers(kReflectionRequirements)] Type targetType, JsonValue sourceJsonValue, JsonInterpreterSettings settings = null)
        {
            FillOutObjectWithJson(ref targetItem, targetType, sourceJsonValue, settings, owner: null);
        }

        private static void FillOutObjectWithJson (ref object targetItem, [DynamicallyAccessedMembers(kReflectionRequirements)] Type targetType, JsonValue sourceJsonValue, JsonInterpreterSettings settings, Json owner, IReadOnlySet<string> keysConsumedByConstructor = null)
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

                // The tree already holds the string unescaped. StringParser's default string entry
                //  undoes the quote-only escape the legacy tree still carries, which would corrupt a
                //  string that really contains a backslash before a quote.
                if (effectiveType == typeof(string))
                {
                    targetItem = sourceJsonValue.StringValue;
                    return;
                }

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

                // No element type resolved - the target (usually a bare object) is not an array,
                //  list, or dictionary, so there is nothing to deserialize the elements into. Report
                //  it and null the value rather than feeding a null element type into the per-item
                //  build below, which would dereference null. This is the boxed-array-in-object case:
                //  the array was serialized with no __type discriminator and can't be resolved back.
                if (elementType == null)
                {
                    owner?.AddError($"Cannot interpret a json array as target type '{targetType}' - it is not an array, list, or dictionary. This usually means a polymorphic (object-typed) value held an array serialized with no '{JsonDocument.kTypeIndicator}' discriminator. Skipping, value left null.");
                    targetItem = null;
                    return;
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
                DataMember[] membersToSet = GetMemberSet(targetType).ReadInto;

                foreach (KeyValuePair<string, JsonValue> kvp in sourceCasted)
                {
                    if (kvp.Key == JsonDocument.kTypeIndicator)
                    {
                        continue;
                    }

                    // A key the constructor took as an argument is done: setting its member again would replace whatever the
                    //  constructor did with the value, which the generated reader never does either
                    if (keysConsumedByConstructor != null && keysConsumedByConstructor.Contains(kvp.Key))
                    {
                        continue;
                    }

                    DataMember memberToSet = FindMemberForKey(membersToSet, kvp.Key);
                    if (memberToSet == null)
                    {
                        continue;
                    }

                    object newMemberValue = null;

                    // [JsonRuntimeTypeEval] read path: the value side is a small doc carrying the
                    //  payload's runtime type id and the actual content. Resolve the type, build
                    //  against that concrete type, then set.
                    JsonRuntimeTypeEvalAttribute runtimeTypeEval = memberToSet.Info.GetCustomAttribute<JsonRuntimeTypeEvalAttribute>(inherit: false);
                    if (runtimeTypeEval != null && kvp.Value.IsDocument)
                    {
                        JsonDocument runtimeDoc = (JsonDocument)kvp.Value;
                        if (runtimeDoc.TryGetValue(JsonDocument.kTypeIndicator, out string runtimeTypeId)
                            && runtimeDoc.ValueFor(JsonDocument.kRuntimeTypeEvalValue) is JsonValue wrappedValue
                            && TryGetTypeForTypeId(runtimeTypeId, out Type runtimeType))
                        {
                            newMemberValue = BuildObjectForJson(runtimeType, wrappedValue, settings, owner);
                        }
                    }

                    if (newMemberValue == null)
                    {
                        newMemberValue = BuildObjectForJson(memberToSet.Type, kvp.Value, settings, owner);
                    }

                    memberToSet.SetValue(targetItem, newMemberValue);
                }
            }
        }

        // ===============================[ Object-to-Json Implementation ]===========================
        // The reflection branch reads property metadata off source.GetType(). The trimmer cannot
        // statically verify which members of the runtime type are needed - that depends on what
        // the caller actually passes in. Consumers that want trim safety should opt their types
        // into [OptimizeAJson] (the source-gen path short-circuits to generated code before any
        // reflection runs) or annotate their consumer-side type holder with
        // [DynamicallyAccessedMembers].
        [UnconditionalSuppressMessage("Trimming", "IL2075",
            Justification = "Reflection-path serializer; trim-safe path is [OptimizeAJson] on the consumer type or DynamicallyAccessedMembers on the holder.")]
        public static void FillOutJsonBuilderForObject (object source, JsonBuilder target)
        {
            if (source == null)
            {
                return;
            }

            Type sourceType = source.GetType();

            // Source-gen fast path. The generated writer handles document-startup, type-id header,
            //  and per-property writes in a single explicit call, with no reflection on the property
            //  loop. Value-typed properties are still boxed: each one goes through
            //  JsonBuilder.AddProperty, which takes an object.
            if (AJsonGeneratedDispatch.TryGet(sourceType, out AJsonGeneratedSerializer generated))
            {
                generated.Writer(source, target);
                return;
            }

            // Class-level [JsonPropertyAsSelf] elevation - swap the source for the named property's
            //  value and continue down the normal path. Null elevated value falls out as a no-op
            //  (matches the null-property omit policy the outer property handler applies).
            JsonPropertyAsSelfAttribute elevatedAttr = sourceType.GetCustomAttribute<JsonPropertyAsSelfAttribute>(inherit: true);
            if (elevatedAttr != null)
            {
                PropertyInfo elevatedProp = sourceType.GetProperty(elevatedAttr.PropertyName, BindingFlags.Public | BindingFlags.Instance);
                if (elevatedProp != null)
                {
                    source = elevatedProp.GetValue(source);
                    if (source == null)
                    {
                        return;
                    }
                    sourceType = source.GetType();
                }
            }

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

                ApplyDocumentMember(target, source, new DataMember(keyProp));
                ApplyDocumentMember(target, source, new DataMember(valueProp));
                return;
            }

            // Document path.
            MemberSet members = GetMemberSet(sourceType);
            DataMember[] membersToWrite = (sourceType.IsSimpleType() || !target.BuilderSettings.UseReadonlyObjectProperties)
                ? members.WithGetterAndSetter
                : members.WithGetter;

            // A document with nothing to write is dropped from its parent, unless it carries a type
            //  id. For a type with no data members the type id is the whole value: an empty marker
            //  type in an interface-typed property or a list has to come back as an instance of that
            //  type, not vanish and read back as null (or shift the list).
            eTypeIdInfo typeIdToWrite = target.BuilderSettings.TypeIdToWrite;
            bool hasTypeId = TryGetTypeIdForType(typeIdToWrite, sourceType, out string typeId);
            if (membersToWrite.Length == 0 && !hasTypeId && target.Parent != null)
            {
                target.Parent.Children.Remove(target);
                return;
            }

            if (!target.IsArrayItem)
            {
                target = target.StartDocument();
            }

            if (hasTypeId)
            {
                target.AddProperty(JsonDocument.kTypeIndicator, typeId);
            }

            foreach (DataMember member in membersToWrite)
            {
                ApplyDocumentMember(target, source, member);
            }
        }

        // ===============================[ Internal Helpers ]===========================
        private static void ApplyDocumentMember (JsonBuilder target, object memberSource, DataMember member)
        {
            string key = member.JsonKey;
            object sourceValue = member.GetValue(memberSource);

            if (sourceValue == null)
            {
                return;
            }

            // [JsonOmitIfDefault] - skip the member if the value matches a default. Three sources
            //  in priority order: per-attribute explicit, settings-registered equivalent, then the
            //  type's zero value.
            JsonOmitIfDefaultAttribute omitAttr = member.Info.GetCustomAttribute<JsonOmitIfDefaultAttribute>(inherit: true);
            if (omitAttr != null && IsConsideredDefault(target.BuilderSettings, member.Type, sourceValue, omitAttr))
            {
                return;
            }

            // [JsonRuntimeTypeEval] - wrap the value in a type-id-bearing doc so the read path can
            //  recover the concrete runtime type for the member (typically an object/interface).
            JsonRuntimeTypeEvalAttribute runtimeTypeEval = member.Info.GetCustomAttribute<JsonRuntimeTypeEvalAttribute>(inherit: false);
            if (runtimeTypeEval != null
                && TryGetTypeIdForType(runtimeTypeEval.TypeWriteTarget, sourceValue.GetType(), out string runtimeTypeId))
            {
                JsonBuilder wrapperDoc = target.StartProperty(key).StartDocument();
                wrapperDoc.AddProperty(JsonDocument.kTypeIndicator, runtimeTypeId);
                JsonBuilder valueBuilder = wrapperDoc.StartProperty(JsonDocument.kRuntimeTypeEvalValue);
                FillOutJsonBuilderForObject(sourceValue, valueBuilder);
                return;
            }

            if (TryGetSimpleStringValue(target.BuilderSettings, member.Type, sourceValue, out bool isUsuallyQuoted, out string simpleStringValue))
            {
                target.AddProperty(key, sourceValue, isUsuallyQuoted);
            }
            else
            {
                JsonBuilder propertyBuilder = target.StartProperty(key);
                FillOutJsonBuilderForObject(sourceValue, propertyBuilder);
            }
        }

        private static bool IsConsideredDefault (JsonBuilderSettings settings, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type propertyType, object value, JsonOmitIfDefaultAttribute attr)
        {
            if (value == null)
            {
                return true;
            }

            // Per-attribute explicit default. Attribute arguments for enum values get stored as
            //  the underlying integer, so coerce both sides through the property type before
            //  comparing - otherwise an `eFoo.Center` argument never equals the boxed enum value.
            if (attr.HasExplicitDefault)
            {
                object attrDefault = attr.ExplicitDefault;
                if (attrDefault != null && propertyType.IsEnum && attrDefault.GetType() != propertyType)
                {
                    attrDefault = Enum.ToObject(propertyType, attrDefault);
                }
                // A number of another width (2.5 given for a float) is converted to the value's own type, the same cast the
                //  generated writer emits, or the two would disagree about whether to omit it
                else if (attrDefault != null
                    && (attrDefault.GetType() != value.GetType())
                    && (attrDefault is IConvertible)
                    && (value.GetType().IsPrimitive || (value is decimal)))
                {
                    attrDefault = Convert.ChangeType(attrDefault, value.GetType(), System.Globalization.CultureInfo.InvariantCulture);
                }
                return Equals(value, attrDefault);
            }

            // Settings-level explicit default - lets non-const-expressible types (Vector2, Guid, etc.)
            //  participate by registering an instance to compare against.
            if (settings.TryGetDefaultEquivalent(propertyType, out object registeredDefault))
            {
                return Equals(value, registeredDefault);
            }

            // Zero-value default for value types. Reference types reach here only when value is
            //  non-null (already returned true above) so they are non-default by definition.
            if (propertyType.IsValueType)
            {
                return Equals(value, Activator.CreateInstance(propertyType));
            }

            return false;
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

        // The value goes into the tree as it is. Escaping belongs to the text: JsonWriter escapes
        //  it on the way out and JsonReader unescapes it on the way in.
        private static void ApplySimpleValue (JsonBuilder target, string rawValue)
        {
            if (target.IsValue)
            {
                target.Value = rawValue;
            }
            else
            {
                target.DocumentKVPValue = new JsonBuilder(target);
                target.DocumentKVPValue.IsValueUsualQuoteTarget = target.IsValueUsualQuoteTarget;
                target.DocumentKVPValue.Value = rawValue;
            }
        }

        // ===============================[ Type Id Helpers ]===========================
        public static bool TryGetTypeIdForType (eTypeIdInfo typeWriteSettings, Type type, out string foundTypeId)
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

        // The Type.GetType(string) call is inherently dynamic and the trimmer cannot statically
        // verify the target. Consumers that rely on this fallback (rather than registering with
        // TypeIdRegistrar up front) need to ensure their target types are kept by the trimmer
        // via DynamicDependency or DynamicallyAccessedMembers - a runtime concern, not solvable
        // here.
        [UnconditionalSuppressMessage("Trimming", "IL2057",
            Justification = "Fallback for type ids not registered with TypeIdRegistrar - consumers using this path must keep target types alive themselves.")]
        public static bool TryGetTypeForTypeId (string typeIndicator, out Type foundType)
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
            if (foundType != null)
            {
                return true;
            }

            // The id may be an assembly-qualified name whose assembly cannot bind, but whose type was
            //  registered by full name - the source generator auto-registers an opted-in assembly's
            //  enums that way (a hard typeof reference, so trim / ReadyToRun safe). Retry the registrar
            //  with the assembly identity stripped off. No enum gate here: this only resolves what was
            //  explicitly registered, never an arbitrary name.
            string typeFullName = FallbackTypeResolver.ExtractTypeFullName(typeIndicator);
            if (typeFullName != typeIndicator && TypeIdRegistrar.TryGetType(typeFullName, out Type fullNameMatch))
            {
                foundType = fullNameMatch;
                return true;
            }

            // Last resort: still unbound. Try a name-only match across the assemblies the registrar was
            //  asked to track. Deliberately scoped to enums: auto-resolving an arbitrary class by name
            //  from wire input is a deserialization gadget risk, whereas an enum carries no payload. A
            //  class/struct that must round-trip has to be registered or carry a [TypeId].
            Type nameMatched = FallbackTypeResolver.ResolveByName(typeIndicator, TypeIdRegistrar.TrackedAssemblies);
            if (nameMatched != null && nameMatched.IsEnum)
            {
                foundType = nameMatched;
                return true;
            }

            foundType = null;
            return false;
        }

        // ===============================[ Reflection Cache ]===========================
        /// <summary>
        /// Drops the cached member lists for <paramref name="type"/> and every type derived from
        /// it. A cached list bakes in the <see cref="TypeMetadataExtensionRegistrar"/> hide and order
        /// state from when it was built, and a derived type's list carries its bases' members, so a
        /// registration on a base has to reach the derived types too. Called by the registrar.
        /// </summary>
        internal static void InvalidateMemberCachesFor (Type type)
        {
            RemoveTypeAndDerived(g_memberCache, type);
            RemoveTypeAndDerived(g_memberSetCache, type);
        }

        /// <summary>
        /// Drops every cached member list. Called by the registrar when a change can affect any
        /// type (clearing all registrations, or changing the default member ordering).
        /// </summary>
        internal static void ClearMemberCaches ()
        {
            g_memberCache.Clear();
            g_memberSetCache.Clear();
        }

        /// <summary>
        /// The public properties and fields AJson reads and writes for <paramref name="type"/>, in member order: every public
        /// instance property but an indexer, and every public instance field but one a property stands in front of, leaving
        /// out anything hidden through <see cref="TypeMetadataExtensionRegistrar"/> or marked [JsonIgnore]. The constructor
        /// route matches its parameters against the same list.
        /// </summary>
        internal static DataMember[] GetDataMembers (Type type)
        {
            return g_memberCache.GetOrAdd(type, static t => ComputeDataMembers(t));
        }

        private static MemberSet GetMemberSet (Type type)
        {
            return g_memberSetCache.GetOrAdd(type, static t => new MemberSet(GetDataMembers(t)));
        }

        private static void RemoveTypeAndDerived<TValue> (ConcurrentDictionary<Type, TValue> cache, Type type)
        {
            foreach (Type cachedType in cache.Keys)
            {
                if (type.IsAssignableFrom(cachedType))
                {
                    cache.TryRemove(cachedType, out _);
                }
            }
        }

        private static DataMember[] ComputeDataMembers (Type targetType)
        {
            MemberInfo[] orderedMembers = TypeMetadataExtensionRegistrar.GetOrderedPropertiesAndFields(targetType).ToArray();
            string[] propertyNames = orderedMembers
                .OfType<PropertyInfo>()
                .Where(prop => prop.GetIndexParameters().Length == 0)
                .Select(prop => prop.Name)
                .ToArray();

            List<DataMember> output = new List<DataMember>(orderedMembers.Length);
            foreach (MemberInfo member in orderedMembers)
            {
                if (TypeMetadataExtensionRegistrar.IsHidden(member)
                    || member.GetCustomAttribute<JsonIgnoreAttribute>(inherit: true) != null)
                {
                    continue;
                }

                if (member is PropertyInfo property)
                {
                    // An indexer is a property with parameters, and has no value to read or write without an index. Reading
                    //  one with no index throws, which is what writing any type with an indexer used to do, the
                    //  System.Numerics vectors included.
                    if (property.GetIndexParameters().Length == 0)
                    {
                        output.Add(new DataMember(property));
                    }
                }
                else if (member is FieldInfo field && !IsStorageForAProperty(field, propertyNames))
                {
                    output.Add(new DataMember(field));
                }
            }

            return output.ToArray();
        }

        private static bool IsStorageForAProperty (FieldInfo field, string[] propertyNames)
        {
            // A public field with a property of the same name in front of it, ignoring a leading underscore and case, is that
            //  property's storage. The property stands for the value, so the field is not written a second time. The WinUI3
            //  Rect, Point and Size keep their values that way (public float _x behind public double X). A field that matches
            //  no property is data in its own right, like the X, Y, Z and W of the System.Numerics vectors.
            string name = field.Name.TrimStart('_');
            foreach (string propertyName in propertyNames)
            {
                if (String.Equals(propertyName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static DataMember FindMemberForKey (DataMember[] members, string key)
        {
            for (int i = 0; i < members.Length; ++i)
            {
                if (members[i].JsonKey == key)
                {
                    return members[i];
                }
            }
            return null;
        }

        // ===============================[ Subclasses/structs ]===========================
        /// <summary>
        /// A public property or field that AJson reads and writes, so the walk treats both the same way
        /// </summary>
        internal sealed class DataMember
        {
            private readonly PropertyInfo m_property;
            private readonly FieldInfo m_field;

            public DataMember (PropertyInfo property)
            {
                m_property = property;
                this.Info = property;
                this.Type = property.PropertyType;
                this.CanGet = property.GetGetMethod() != null;
                this.CanSet = property.GetSetMethod() != null;
                this.JsonKey = KeyFor(property);
            }

            public DataMember (FieldInfo field)
            {
                m_field = field;
                this.Info = field;
                this.Type = field.FieldType;
                this.CanGet = true;

                // A readonly field can only be set by its own type's constructor, so it counts as get-only
                this.CanSet = !field.IsInitOnly;
                this.JsonKey = KeyFor(field);
            }

            /// <summary>
            /// The property or field itself, for its attributes
            /// </summary>
            public MemberInfo Info { get; }

            public Type Type { get; }

            /// <summary>
            /// The key it is written and read under: its name, or its <see cref="JsonPropertyAliasAttribute"/>
            /// </summary>
            public string JsonKey { get; }

            /// <summary>
            /// True for a field, or a property with a public getter
            /// </summary>
            public bool CanGet { get; }

            /// <summary>
            /// True for a field that is not readonly, or a property with a public setter (init-only included)
            /// </summary>
            public bool CanSet { get; }

            public object GetValue (object source) => m_property != null ? m_property.GetValue(source) : m_field.GetValue(source);

            public void SetValue (object target, object value)
            {
                if (m_property != null)
                {
                    m_property.SetValue(target, value);
                }
                else
                {
                    m_field.SetValue(target, value);
                }
            }

            private static string KeyFor (MemberInfo member)
            {
                JsonPropertyAliasAttribute alias = member.GetCustomAttribute<JsonPropertyAliasAttribute>(inherit: true);
                return alias?.PropertyName ?? member.Name;
            }
        }

        /// <summary>
        /// One type's members, split the ways reading and writing use them
        /// </summary>
        private sealed class MemberSet
        {
            public MemberSet (DataMember[] members)
            {
                this.ReadInto = members.Where(m => m.CanSet).ToArray();
                this.WithGetter = members.Where(m => m.CanGet).ToArray();
                this.WithGetterAndSetter = members.Where(m => m.CanGet && m.CanSet).ToArray();
            }

            /// <summary>
            /// The members a document's keys are read into
            /// </summary>
            public DataMember[] ReadInto { get; }

            /// <summary>
            /// What is written when get-only members are written too: every member with a getter
            /// </summary>
            public DataMember[] WithGetter { get; }

            /// <summary>
            /// What is written when get-only members are not
            /// </summary>
            public DataMember[] WithGetterAndSetter { get; }
        }
    }
}
