namespace AJut.UX.PropertyInteraction
{
    using AJut.Storage;
    using System;

    // ===========[ PropertyGridTypeAliasing ]=======================================
    // Base class for type aliasing in the PropertyGrid auto-generation path.
    // When a property on a source object is tagged with [PGTypeAlias(typeof(MyAliasing))],
    // PropertyEditTarget.GenerateForPropertiesOf wraps the getter and setter with the
    // aliasing instance's converters, and uses AliasType.Name as the editor template key.
    //
    // This lets the property grid display and edit values through a compatible type
    // (the alias) even when the actual property type has no native editor support.
    //
    // Example use case: SKColor on a data class can be aliased to Windows.UI.Color so
    // the ColorEditIngressControl (which binds to Windows.UI.Color) works without any
    // special-casing inside the property grid infrastructure.
    // =============================================================================

    public interface IPropertyGridTypeAliasing
    {
        Type AliasType { get; }
        object? ConvertToAlias(object sourceValue);
        object? ConvertFromAlias(object aliasValue);
    }
    public abstract class PropertyGridTypeAliasing<TSource,TAlias> : IPropertyGridTypeAliasing
    {
        // ===========[ Properties ]================================================

        /// <summary>The type the editor expects. Its Name drives the editor template key lookup.</summary>
        public virtual Type AliasType => typeof(TAlias);

        // ===========[ Public Interface Methods ]==================================

        /// <summary>Convert from the property's actual value to the alias type value (for display/editing).</summary>
        public abstract TAlias? ConvertToAlias (TSource sourceValue);

        /// <summary>Convert from the alias type value back to the property's actual type (after user edits).</summary>
        public abstract TSource? ConvertFromAlias (TAlias aliasValue);

        // ============[ IPropertyGridTypeAliasing Impl ]===========================
        object? IPropertyGridTypeAliasing.ConvertToAlias(object sourceValue)
        {
            if (sourceValue is TSource sourceCasted)
            {
                return this.ConvertToAlias(sourceCasted);
            }

            return default;
        }

        object? IPropertyGridTypeAliasing.ConvertFromAlias(object aliasValue)
        {
            if (aliasValue is TAlias aliasCasted)
            {
                return this.ConvertFromAlias(aliasCasted);
            }

            return default;
        }
    }
}
