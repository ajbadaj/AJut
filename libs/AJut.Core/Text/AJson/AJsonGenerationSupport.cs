namespace AJut.Text.AJson
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;

    /// <summary>
    /// Public helper surface that the AJson source generator emits calls into. Lives here rather
    /// than as private helpers inside JsonHelper because the generated code is in the consumer's
    /// assembly - it cannot reach internals.
    /// </summary>
    /// <remarks>
    /// Hand-written code can use these helpers too, but they exist primarily to keep the emitted
    /// output compact and to centralize the document-startup ceremony / runtime-type-eval wrapper
    /// shape so future tweaks land in one place.
    /// </remarks>
    public static class AJsonGenerationSupport
    {
        /// <summary>
        /// What the reflection path needs kept of a type it reads: the same as JsonHelper.BuildObjectForJson asks for
        /// </summary>
        private const DynamicallyAccessedMemberTypes kReadRequirements
            = DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicFields
            | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor;

        /// <summary>
        /// Generated writers call this first. Promotes the builder to a document if needed and
        /// writes the type-id header per the active builder settings. Returns the document
        /// builder the writer should append properties to.
        /// </summary>
        public static JsonBuilder StartGeneratedDocument (JsonBuilder target, Type runtimeType)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            JsonBuilder doc = target.IsArrayItem ? target : target.StartDocument();

            if (JsonHelper.TryGetTypeIdForType(doc.BuilderSettings.TypeIdToWrite, runtimeType, out string typeId))
            {
                doc.AddProperty(JsonDocument.kTypeIndicator, typeId);
            }

            return doc;
        }

        /// <summary>
        /// Generated readers call this to read a property's value. Same as <see cref="JsonHelper.BuildObjectForJson{T}(JsonValue, JsonInterpreterSettings)"/>,
        /// except that errors from the read, including any nested inside it, go to <paramref name="owner"/> the way they do on the
        /// reflection path, rather than being dropped.
        /// </summary>
        public static T ReadValue<[DynamicallyAccessedMembers(kReadRequirements)] T> (JsonValue value, JsonInterpreterSettings settings, Json owner)
        {
            return (T)JsonHelper.BuildObjectForJson(typeof(T), value, settings, owner);
        }

        /// <summary>
        /// Generated readers call this to read a property whose type implements IParsable of itself. A single value with no
        /// constructor registered for the type and no converter registered for it is read with the type's own TryParse, called
        /// directly, which keeps working under trimming where the reflection path's lookup of it may not. Anything else is
        /// read the way <see cref="ReadValue{T}"/> reads it.
        /// </summary>
        public static T ReadParsable<[DynamicallyAccessedMembers(kReadRequirements)] T> (JsonValue value, JsonInterpreterSettings settings, Json owner)
            where T : IParsable<T>
        {
            settings = settings ?? JsonInterpreterSettings.Default;
            bool isReadByTryParse = value != null
                && value.IsValue
                && !settings.HasCustomConstructorFor(typeof(T))
                && !JsonHelper.IsRegisteredConverterFor(typeof(T));

            if (!isReadByTryParse)
            {
                return ReadValue<T>(value, settings, owner);
            }

            if (T.TryParse(value.StringValue, CultureInfo.InvariantCulture, out T parsed))
            {
                return parsed;
            }

            owner?.AddError($"Could not read '{value.StringValue}' as a {typeof(T).Name}, the value is left at its default");
            return default;
        }

        /// <summary>
        /// Generated readers call this for a get-only collection property or readonly collection field, since there is no
        /// setting a new one: the collection already there is cleared, then filled from <paramref name="value"/>. A collection
        /// that is null or read-only, or json that is not an array, is reported to <paramref name="owner"/>, the same as on the
        /// reflection path.
        /// </summary>
        public static void FillGetOnlyCollection (object collection, string jsonKey, JsonValue value, JsonInterpreterSettings settings, Json owner)
        {
            JsonHelper.FillCollectionInPlace(collection, jsonKey, value, settings, owner);
        }

        /// <summary>
        /// Generated writers call this for properties carrying [JsonRuntimeTypeEval]. Wraps the
        /// payload in a small document carrying __type + __value, matching the V1/V2 wrapper
        /// shape so polymorphic round-trip stays consistent across opt-in modes.
        /// Returns the value-side builder the writer should fill with the payload, or null
        /// if no type id could be resolved (writer should skip the property entirely in that
        /// case to match the reflection path).
        /// </summary>
        public static JsonBuilder StartRuntimeTypeEvalProperty (JsonBuilder docBuilder, string propertyKey, eTypeIdInfo typeWriteTarget, Type runtimeType)
        {
            if (docBuilder == null)
            {
                throw new ArgumentNullException(nameof(docBuilder));
            }

            if (!JsonHelper.TryGetTypeIdForType(typeWriteTarget, runtimeType, out string runtimeTypeId))
            {
                return null;
            }

            JsonBuilder wrapperDoc = docBuilder.StartProperty(propertyKey).StartDocument();
            wrapperDoc.AddProperty(JsonDocument.kTypeIndicator, runtimeTypeId);
            return wrapperDoc.StartProperty(JsonDocument.kRuntimeTypeEvalValue);
        }

        /// <summary>
        /// Generated readers call this for properties carrying [JsonRuntimeTypeEval]. Unwraps the
        /// wrapper document and constructs an instance of the runtime-resolved type. Returns the
        /// constructed object, or null if the value is not a wrapper document or its type id is
        /// missing or cannot be resolved.
        /// </summary>
        public static object ReadRuntimeTypeEvalProperty (JsonValue propertyValue, JsonInterpreterSettings settings, Json owner)
        {
            if (!(propertyValue is JsonDocument wrapper))
            {
                return null;
            }

            if (!wrapper.TryGetValue(JsonDocument.kTypeIndicator, out string runtimeTypeId))
            {
                return null;
            }

            if (!JsonHelper.TryGetTypeForTypeId(runtimeTypeId, out Type runtimeType))
            {
                owner?.AddError($"Runtime type id '{runtimeTypeId}' could not be resolved");
                return null;
            }

            // A wrapper with no __value held an object with nothing to write: a type with no data
            //  members (text written before those kept their document), or one whose builder
            //  settings wrote no type id inside the payload. The wrapper's type id is then the whole
            //  value, so build from the wrapper itself, the same fallback the reflection reader uses.
            JsonValue payload = wrapper.ValueFor(JsonDocument.kRuntimeTypeEvalValue) ?? wrapper;
            return JsonHelper.BuildObjectForJson(runtimeType, payload, settings);
        }
    }
}
