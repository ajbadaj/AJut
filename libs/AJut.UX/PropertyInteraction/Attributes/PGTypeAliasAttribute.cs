namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// PropertyGrid attr: specifies a type aliasing class to use when auto-generating a
    /// PropertyEditTarget for this property via PropertyEditTarget.GenerateForPropertiesOf.
    ///
    /// The aliasing class (must derive from PropertyGridTypeAliasing) controls:
    ///   - Which editor template is selected (via AliasType.Name)
    ///   - How to convert the actual property value to the alias type (for display)
    ///   - How to convert the alias type value back to the actual type (after editing)
    ///
    /// Example:
    ///   [PGTypeAlias(typeof(SKColorToWinColorAliasing))]
    ///   public SKColor MyColor { get; set; }
    ///
    /// This routes the property to the "Color" editor template instead of "SKColor",
    /// and wraps get/set with the aliasing class's converters transparently.
    ///
    /// Note: this attribute is ignored when [PGEditor] or Nullable unwrapping also applies.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class PGTypeAliasAttribute : Attribute
    {
        public PGTypeAliasAttribute (Type aliasingType)
        {
            this.AliasingType = aliasingType;
        }

        /// <summary>A concrete type deriving from PropertyGridTypeAliasing that performs the conversion.</summary>
        public Type AliasingType { get; }
    }
}
