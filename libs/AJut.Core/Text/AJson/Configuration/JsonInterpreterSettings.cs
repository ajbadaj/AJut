namespace AJut.Text.AJson
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Numerics;
    using AJut;
    using AJut.Text;
    using AJut.TypeManagement;

    /// <summary>
    /// A delegate used to construct an instance from a json value (given interpreter settings).
    /// V2 adds the owning Json so error reporting goes through Json.Errors instead of throwing.
    /// </summary>
    public delegate object JsonToObjectConstructor (Type fullTarget, JsonValue value, JsonInterpreterSettings settings, Json owner);

    /// <summary>
    /// Settings used to determine how to interpret json and create object instances. V2 typo-fix
    /// of V1's JsonInterpretterSettings (double-t) - V2 uses JsonInterpreterSettings.
    /// </summary>
    public class JsonInterpreterSettings
    {
        /// <summary>
        /// The Tuple types a read builds through their constructor. A Tuple has nothing settable, so the member walk writes its
        /// items and only a constructor can build one back.
        /// </summary>
        private static readonly Type[] kTupleTypes =
        {
            typeof(Tuple<>), typeof(Tuple<,>), typeof(Tuple<,,>), typeof(Tuple<,,,>),
            typeof(Tuple<,,,,>), typeof(Tuple<,,,,,>), typeof(Tuple<,,,,,,>), typeof(Tuple<,,,,,,,>),
        };

        /// <summary>
        /// A Tuple's eighth constructor argument (index 7) is the tuple holding the rest of its items, written under the key
        /// <see cref="kTupleRestKey"/> rather than as Item8
        /// </summary>
        private const int kTupleRestIndex = 7;
        private const string kTupleRestKey = "Rest";

        private readonly Dictionary<Type, JsonToObjectConstructor> m_customConstructors = new Dictionary<Type, JsonToObjectConstructor>();

        // ===========================[ Construction ]===============================
        public JsonInterpreterSettings (StringParser stringParser = null)
        {
            this.StringParser = stringParser ?? new StringParser();
            m_customConstructors.Add(typeof(KeyValuePair<,>), _CreateKeyValuePairFor);
            m_customConstructors.Add(typeof(Vector2), _CreateVector2);
            foreach (Type tupleType in kTupleTypes)
            {
                m_customConstructors.Add(tupleType, _CreateTupleFor);
            }

            // KVP construction goes through Type.GetConstructor on a generic KeyValuePair<,> the
            // trimmer can't statically verify; KeyValuePair<,> is a closed system shape and the
            // ctor is always present, so the suppression is safe in practice.
            [UnconditionalSuppressMessage("Trimming", "IL2075",
                Justification = "KeyValuePair<,> ctor is intrinsic and always preserved; the FindBaseTypeOrInterface return is a constructed KeyValuePair<,> by definition of the call site.")]
            [UnconditionalSuppressMessage("Trimming", "IL2067",
                Justification = "KVP element types come from generic arguments of the KeyValuePair<,> the consumer requested - keeping members of those is the consumer's responsibility per AJson reflection-path contract.")]
            object _CreateKeyValuePairFor (Type fullTarget, JsonValue json, JsonInterpreterSettings settings, Json owner)
            {
                Type kvpType = fullTarget.FindBaseTypeOrInterface(typeof(KeyValuePair<,>));
                Type[] genericTypes = kvpType.GetGenericArguments();

                if (!json.IsDocument)
                {
                    owner?.AddError($"KeyValuePair source must be a document - got {(json.IsArray ? "array" : "value")}");
                    return kvpType.GetConstructor(genericTypes).Invoke(new object[] { _DefaultFor(genericTypes[0]), _DefaultFor(genericTypes[1]) });
                }

                JsonDocument doc = (JsonDocument)json;
                Type keyType = null;
                if (doc.TryGetValue(JsonDocument.kKVPKeyTypeIndicator, out string keyTypeId))
                {
                    JsonHelper.TryGetTypeForTypeId(keyTypeId, out keyType);
                }

                Type valueType = null;
                if (doc.TryGetValue(JsonDocument.kKVPValueTypeIndicator, out string valueTypeId))
                {
                    JsonHelper.TryGetTypeForTypeId(valueTypeId, out valueType);
                }

                keyType = keyType ?? genericTypes[0];
                valueType = valueType ?? genericTypes[1];

                JsonValue keyJson = doc.ValueFor("Key");
                JsonValue valueJson = doc.ValueFor("Value");

                // V2 Phase C fold-in: missing-value contract is now errors-or-default rather than throw.
                if (keyJson == null)
                {
                    owner?.AddError("KeyValuePair source missing 'Key' field");
                }
                if (valueJson == null)
                {
                    owner?.AddError("KeyValuePair source missing 'Value' field");
                }

                object keyObj = keyJson != null
                    ? JsonHelper.BuildObjectForJson(keyType, keyJson, settings, owner)
                    : _DefaultFor(keyType);

                object valueObj = valueJson != null
                    ? JsonHelper.BuildObjectForJson(valueType, valueJson, settings, owner)
                    : _DefaultFor(valueType);

                return kvpType.GetConstructor(genericTypes).Invoke(new[] { keyObj, valueObj });
            }

            // A Tuple is read from the document of items the member walk writes for it, Item1 to Item7 and then Rest, each
            //  read as its type argument and passed to the constructor. A missing item is its type's default.
            [UnconditionalSuppressMessage("Trimming", "IL2075",
                Justification = "Tuple<> constructors are intrinsic and always preserved; the type is a constructed Tuple<> by definition of the registration.")]
            [UnconditionalSuppressMessage("Trimming", "IL2072",
                Justification = "Tuple item types come from the generic arguments of the Tuple<> the consumer requested - keeping members of those is the consumer's responsibility per AJson reflection-path contract.")]
            object _CreateTupleFor (Type fullTarget, JsonValue json, JsonInterpreterSettings settings, Json owner)
            {
                Type[] itemTypes = fullTarget.GetGenericArguments();
                object[] items = new object[itemTypes.Length];
                JsonDocument document = json as JsonDocument;
                if (document == null)
                {
                    owner?.AddError($"A {fullTarget.Name} is read from a document of its items, and this is {(json.IsArray ? "an array" : "a value")}");
                }

                for (int index = 0; index < itemTypes.Length; ++index)
                {
                    string key = (index == kTupleRestIndex) ? kTupleRestKey : $"Item{index + 1}";
                    JsonValue itemJson = document?.ValueFor(key);
                    items[index] = itemJson != null
                        ? JsonHelper.BuildObjectForJson(itemTypes[index], itemJson, settings, owner)
                        : _DefaultFor(itemTypes[index]);
                }

                return fullTarget.GetConstructor(itemTypes).Invoke(items);
            }

            object _CreateVector2 (Type fullTarget, JsonValue json, JsonInterpreterSettings settings, Json owner)
            {
                // A Vector2 is written as a document of its public fields, { "X": 0.25, "Y": 0.5 }, the
                //  way any other type's members are, and the member fill that runs after this reads it
                if (json.IsDocument)
                {
                    return Vector2.Zero;
                }

                // Text written before that is an angle-bracket string, "<0.25,0.5>", with invariant
                //  components or, from older versions, the writing machine's culture, which reads back
                //  with the current culture as before. A comma-decimal culture's old text (<0,5,1,25>)
                //  splits into four parts and was never readable, so it is reported along with anything
                //  else that does not parse.
                string[] xystrs = (json.StringValue ?? String.Empty)
                    .Trim('<', '>')
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                // The old read used float.TryParse's own defaults, which allow thousands grouping
                const NumberStyles kOldTextStyles = NumberStyles.Float | NumberStyles.AllowThousands;
                if (_TryReadVector2(xystrs, NumberStyles.Float, CultureInfo.InvariantCulture, out Vector2 found)
                    || _TryReadVector2(xystrs, kOldTextStyles, CultureInfo.CurrentCulture, out found))
                {
                    return found;
                }

                owner?.AddError($"Could not read '{json.StringValue}' as a Vector2, the value is left at zero");
                return Vector2.Zero;
            }

            static bool _TryReadVector2 (string[] parts, NumberStyles styles, CultureInfo culture, out Vector2 vector)
            {
                if (parts.Length == 2
                    && float.TryParse(parts[0], styles, culture, out float x)
                    && float.TryParse(parts[1], styles, culture, out float y))
                {
                    vector = new Vector2(x, y);
                    return true;
                }

                vector = Vector2.Zero;
                return false;
            }

            static object _DefaultFor ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;
        }

        // ===========================[ Properties ]===============================
        public static JsonInterpreterSettings Default { get; set; } = new JsonInterpreterSettings();

        public StringParser StringParser { get; }

        /// <summary>
        /// How DateTime text that is not round-trip ISO 8601 is read: text written before AJson
        /// wrote ISO 8601, or by something else. The default, AssumeUniversal, matches how the old
        /// writer wrote it (UTC, with no offset).
        /// </summary>
        public DateTimeStyles DefaultDateTimeParseStyle { get; set; } = DateTimeStyles.AssumeUniversal;

        // ===========================[ Public Interface Methods ]===============================

        public void Add (Type t, JsonToObjectConstructor constructor)
        {
            m_customConstructors.Add(t, constructor);
        }

        /// <summary>
        /// Register a strongly-typed factory for a target type that does not have a parameterless
        /// constructor (or otherwise needs custom translation from a json value to an instance).
        /// The factory is consulted before the parameterless-constructor + property-assignment
        /// path runs.
        /// </summary>
        public void RegisterCustomConstructor<T> (Func<JsonValue, T> ctor)
        {
            m_customConstructors[typeof(T)] = (fullTarget, json, settings, owner) => ctor(json);
        }

        /// <summary>
        /// Construct an instance of the given type from the passed in json. Owner Json (if any)
        /// receives error reports from delegate constructors instead of throwing.
        /// </summary>
        /// <remarks>
        /// In order: a constructor registered with <see cref="RegisterCustomConstructor{T}"/> or <see cref="Add"/>, then the
        /// type's constructor route (see <see cref="AJsonConstructorAttribute"/>), then a parameterless constructor.
        /// </remarks>
        public object ConstructInstanceFor ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type, JsonValue jsonValue, Json owner = null)
        {
            return this.ConstructInstanceFor(type, jsonValue, owner, out _);
        }

        /// <summary>
        /// Builds an instance exactly as <see cref="ConstructInstanceFor(Type, JsonValue, Json)"/> does, and also reports the json
        /// keys a constructor route took as arguments, so the property fill that follows can leave them alone instead of
        /// overwriting what the constructor did with those values
        /// </summary>
        /// <param name="type">The type to build</param>
        /// <param name="jsonValue">The json the instance is built from</param>
        /// <param name="owner">Receives errors, if given</param>
        /// <param name="keysConsumedByConstructor">The json keys a constructor route took as arguments, which the property fill after
        /// construction must leave alone; null when the instance was built any other way</param>
        internal object ConstructInstanceFor ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type, JsonValue jsonValue, Json owner, out IReadOnlySet<string> keysConsumedByConstructor)
        {
            keysConsumedByConstructor = null;
            if (this.TryConstructWithCustomConstructor(type, jsonValue, owner, out object custom))
            {
                return custom;
            }

            if (AJsonConstructorRoute.TryConstruct(type, jsonValue, this, owner, out object routed, out keysConsumedByConstructor))
            {
                return routed;
            }

            if (type == typeof(string))
            {
                return String.Empty;
            }

            return AJutActivator.CreateInstanceOf(type);
        }

        /// <summary>
        /// Builds <paramref name="type"/> with a constructor registered for it through <see cref="RegisterCustomConstructor{T}"/>
        /// or <see cref="Add"/>, if there is one. Generated readers call this before building the type themselves, so a registered
        /// constructor wins on both paths.
        /// </summary>
        /// <remarks>
        /// A registered constructor also wins over one marked [AJsonConstructor]. The first time that happens for a type, it is
        /// logged as a warning.
        /// </remarks>
        /// <returns>True if a registered constructor matched the type and built <paramref name="instance"/>, false otherwise</returns>
        public bool TryConstructWithCustomConstructor (Type type, JsonValue jsonValue, Json owner, out object instance)
        {
            JsonToObjectConstructor constructor = this.TryGetCustomConstructorFor(type);
            if (constructor == null)
            {
                instance = null;
                return false;
            }

            instance = constructor(type, jsonValue, this, owner);
            AJsonConstructorRoute.NoteCustomConstructorWon(type);
            return true;
        }

        /// <summary>
        /// Whether a constructor is registered for <paramref name="type"/>, through <see cref="RegisterCustomConstructor{T}"/>,
        /// <see cref="Add"/> or as one of AJson's own (KeyValuePair, Tuple, Vector2's old text)
        /// </summary>
        internal bool HasCustomConstructorFor (Type type) => this.TryGetCustomConstructorFor(type) != null;

        // ===========================[ Helper Methods ]===============================
        /// <summary>
        /// The constructor registered for <paramref name="type"/> exactly, or for the generic type it is made from
        /// </summary>
        private JsonToObjectConstructor TryGetCustomConstructorFor (Type type)
        {
            if (m_customConstructors.TryGetValue(type, out JsonToObjectConstructor exact))
            {
                return exact;
            }

            if (type.IsGenericType
                && m_customConstructors.TryGetValue(type.GetGenericTypeDefinition(), out JsonToObjectConstructor byDefinition))
            {
                return byDefinition;
            }

            return null;
        }
    }
}
