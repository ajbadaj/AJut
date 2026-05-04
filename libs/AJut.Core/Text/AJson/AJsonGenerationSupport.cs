namespace AJut.Text.AJson
{
    using System;

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
        /// constructed object or null if the wrapper is malformed / type id cannot be resolved.
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

            JsonValue payload = wrapper.ValueFor(JsonDocument.kRuntimeTypeEvalValue);
            if (payload == null)
            {
                return null;
            }

            if (!JsonHelper.TryGetTypeForTypeId(runtimeTypeId, out Type runtimeType))
            {
                owner?.AddError($"Runtime type id '{runtimeTypeId}' could not be resolved");
                return null;
            }

            return JsonHelper.BuildObjectForJson(runtimeType, payload, settings);
        }
    }
}
