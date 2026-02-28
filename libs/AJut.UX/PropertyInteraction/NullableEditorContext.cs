namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// EditContext payload set on a PropertyEditTarget whose property type is Nullable&lt;T&gt;.
    /// NullableEditor reads this to know what inner editor template key to use and what
    /// type to instantiate as the default value when the user clicks [Set].
    /// </summary>
    public class NullableEditorContext
    {
        public NullableEditorContext(string innerEditorKey, Type innerType)
        {
            this.InnerEditorKey = innerEditorKey;
            this.InnerType = innerType;
        }

        /// <summary>
        /// The editor template key for the underlying non-nullable type
        /// (e.g. "Single" for float?, "Int32" for int?).
        /// </summary>
        public string InnerEditorKey { get; }

        /// <summary>The underlying non-nullable type (e.g. typeof(float) for float?).</summary>
        public Type InnerType { get; }
    }
}
