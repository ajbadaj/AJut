namespace AJut.Text.AJson
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// The kind of json a <see cref="JsonValueConverter"/> writes
    /// </summary>
    public enum eJsonValueShape
    {
        /// <summary>
        /// A single value: a string, a number, or a bool
        /// </summary>
        Value,

        /// <summary>
        /// A document, { "key": value, ... }
        /// </summary>
        Document,

        /// <summary>
        /// An array, [ value, ... ]
        /// </summary>
        Array,
    }

    /// <summary>
    /// How one type is written to json and read back, used in place of AJson's walk of the type's public members. Register
    /// one with <see cref="JsonHelper.RegisterConverter"/>, and it is used on the reflection path and by generated serializers
    /// alike. For a type written as a single value, derive from <see cref="JsonScalarConverter"/>.
    /// </summary>
    /// <remarks>
    /// A string maker registered with <see cref="JsonBuilderSettings.SetCustomJsonManager"/>, or a constructor registered with
    /// <see cref="JsonInterpreterSettings.RegisterCustomConstructor{T}"/>, wins over a converter for the settings it was
    /// registered on.
    /// </remarks>
    public abstract class JsonValueConverter
    {
        // ===========================[ Setup/Construction/Teardown ]===========================
        /// <param name="targetType">The type converted. An open generic type (typeof(Tuple&lt;,&gt;)) covers every type made from it.</param>
        /// <param name="writtenShape">The kind of json <see cref="Write"/> produces</param>
        protected JsonValueConverter (Type targetType, eJsonValueShape writtenShape)
        {
            this.TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
            this.WrittenShape = writtenShape;
        }

        // ===========================[ Properties ]===========================
        /// <summary>
        /// The type converted, or the open generic type whose every closed form is converted
        /// </summary>
        public Type TargetType { get; }

        /// <summary>
        /// The kind of json <see cref="Write"/> produces. The builder has to know this before the value is written, to decide
        /// whether the value starts out as a single value or holds off for a document or an array.
        /// </summary>
        public eJsonValueShape WrittenShape { get; }

        // ===========================[ Public Interface Methods ]===========================
        /// <summary>
        /// Writes <paramref name="instance"/> into <paramref name="target"/>, the builder for wherever it goes: a property's
        /// value, an array item, or the root. Use <see cref="StartDocument"/> and <see cref="StartArray"/> to begin a document
        /// or an array.
        /// </summary>
        public abstract void Write (object instance, JsonBuilder target);

        /// <summary>
        /// Builds an instance from <paramref name="json"/>. Report what cannot be read to <paramref name="owner"/> (it can be
        /// null) rather than throwing, and return a default.
        /// </summary>
        /// <param name="fullTarget">The type being read, which for an open generic converter is the closed type</param>
        /// <param name="json">The json to read</param>
        /// <param name="settings">The settings the read is using</param>
        /// <param name="owner">Receives errors, if given</param>
        public abstract object Read (Type fullTarget, JsonValue json, JsonInterpreterSettings settings, Json owner);

        /// <summary>
        /// Whether <see cref="Read"/> handles <paramref name="json"/>. When it does not, the json is read the way it would be
        /// with no converter, which is how text written before a type had a converter still reads.
        /// </summary>
        public virtual bool CanRead (JsonValue json) => true;

        // ===========================[ Helper Methods ]===========================
        /// <summary>
        /// Starts the document to write into: <paramref name="target"/> itself when it is already an array's document item
        /// </summary>
        protected static JsonBuilder StartDocument (JsonBuilder target)
        {
            return (target.IsArrayItem && target.IsDocument) ? target : target.StartDocument();
        }

        /// <summary>
        /// Starts the array to write into
        /// </summary>
        protected static JsonBuilder StartArray (JsonBuilder target) => target.StartArray();
    }

    /// <summary>
    /// A <see cref="JsonValueConverter"/> for a type written as one value: its text, quoted or not
    /// </summary>
    public abstract class JsonScalarConverter : JsonValueConverter
    {
        // ===========================[ Setup/Construction/Teardown ]===========================
        /// <param name="targetType">The type converted</param>
        /// <param name="isUsuallyQuoted">True to write the text as a json string, false to write it bare, as json writes a number</param>
        protected JsonScalarConverter (Type targetType, bool isUsuallyQuoted) : base(targetType, eJsonValueShape.Value)
        {
            this.IsUsuallyQuoted = isUsuallyQuoted;
            this.StringMaker = this.ToJsonText;
        }

        // ===========================[ Properties ]===========================
        /// <summary>
        /// True when the text is written as a json string, false when it is written bare, as a number is
        /// </summary>
        public bool IsUsuallyQuoted { get; }

        /// <summary>
        /// <see cref="ToJsonText"/> as a string maker, made once
        /// </summary>
        internal JsonStringMaker StringMaker { get; }

        // ===========================[ Public Interface Methods ]===========================
        /// <summary>
        /// The text <paramref name="instance"/> is written as. Format it culture-invariant, so any machine can read it back.
        /// </summary>
        public abstract string ToJsonText (object instance);

        /// <summary>
        /// Reads <paramref name="text"/>, written by <see cref="ToJsonText"/> or by an older version of it
        /// </summary>
        /// <returns>True if the text was read into <paramref name="value"/>, false if it could not be</returns>
        public abstract bool TryReadJsonText (string text, Type fullTarget, JsonInterpreterSettings settings, out object value);

        public sealed override void Write (object instance, JsonBuilder target)
        {
            JsonHelper.ApplySimpleValue(target, this.ToJsonText(instance), this.IsUsuallyQuoted);
        }

        /// <summary>
        /// Text that cannot be read is reported to <paramref name="owner"/>, and the value is left at its default
        /// </summary>
        public sealed override object Read (Type fullTarget, JsonValue json, JsonInterpreterSettings settings, Json owner)
        {
            if (this.TryReadJsonText(json.StringValue, fullTarget, settings, out object value))
            {
                return value;
            }

            owner?.AddError($"Could not read '{json.StringValue}' as a {fullTarget.Name}, the value is left at its default");
            return this.GetValueForUnreadableText(fullTarget);
        }

        /// <summary>
        /// Only a single value is read as text. A document or an array is read the way it would be with no converter.
        /// </summary>
        public override bool CanRead (JsonValue json) => json.IsValue;

        // ===========================[ Helper Methods ]===========================
        /// <summary>
        /// What text that cannot be read reads as: the type's default unless overridden
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2067",
            Justification = "Only a value type is made this way, as its all-zero default, which runs no constructor for the trimmer to keep.")]
        protected virtual object GetValueForUnreadableText (Type fullTarget)
        {
            return fullTarget.IsValueType ? RuntimeHelpers.GetUninitializedObject(fullTarget) : null;
        }
    }
}
