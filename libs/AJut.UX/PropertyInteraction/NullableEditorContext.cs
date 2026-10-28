namespace AJut.UX.PropertyInteraction
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// EditContext payload set on a PropertyEditTarget whose property type is Nullable&lt;T&gt;.
    /// NullableEditor reads this to know what inner editor template key to use and what
    /// type to instantiate as the default value when the user clicks [Set].
    /// </summary>
    [RequiresUnreferencedCode(PropertyGridTrimming.kMessage)]
    [RequiresDynamicCode(PropertyGridTrimming.kListMessage)]
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

        /// <summary>
        /// Builds the target a nullable editor hands its inner editor: it reads the outer target's value, or the
        /// inner type's default while the value is null, and writes straight through to the outer target. Its
        /// Editor is the inner editor key, so the inner editor is chosen the way a non-nullable one would be.
        /// </summary>
        public static PropertyEditTarget BuildProxyTarget (PropertyEditTarget outer, NullableEditorContext? context)
        {
            Type innerType = context?.InnerType ?? typeof(object);
            string editorKey = context?.InnerEditorKey ?? innerType.Name;

            var proxy = new PropertyEditTarget(
                outer.PropertyPathTarget,
                () => outer.EditValue ?? (innerType.IsValueType ? Activator.CreateInstance(innerType) : null),
                (v) => outer.EditValue = v
            )
            {
                Editor = editorKey,
                DisplayName = outer.DisplayName,
            };

            proxy.Setup();
            return proxy;
        }
    }
}
