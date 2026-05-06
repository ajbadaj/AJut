namespace AJut.Text.AJson
{
    using System;

    /// <summary>
    /// Opts a type into AJson source-generator emission. The Roslyn generator that ships with
    /// AJut.Core emits a compile-time serialization helper for any type carrying this attribute,
    /// and routes JsonHelper.BuildJsonForObject / BuildObjectForJson through that helper at
    /// runtime instead of the reflection path. Two wins: throughput (no per-call reflection,
    /// no boxing of value-typed properties) and trimming compatibility (the emitted code
    /// references properties by name, so the IL trimmer keeps them).
    /// </summary>
    /// <remarks>
    /// This is not a "this type can be serialized" marker - the reflection path handles all
    /// serializable types regardless. It is a "compile a fast path for this type" marker.
    /// Behavior is identical with or without it; what changes is which path the runtime
    /// dispatches through.
    ///
    /// Two valid forms:
    ///   - Per-type: place [OptimizeAJson] directly on a class or struct you control.
    ///   - Per-assembly: place [assembly: OptimizeAJson(typeof(SomeType))] in your project.
    ///     The generator opts in every public type in the marker type's assembly. Use case:
    ///     a referenced library full of pure-data types you want optimized in bulk, including
    ///     libraries you do not control.
    ///
    /// The generator emits external static helper classes (not partial methods on the target),
    /// so target types do not need to be partial and foreign-assembly types are supported.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly,
                    AllowMultiple = true,
                    Inherited = false)]
    public sealed class OptimizeAJsonAttribute : Attribute
    {
        /// <summary>
        /// Per-type form. Used as [OptimizeAJson] on a class or struct.
        /// </summary>
        public OptimizeAJsonAttribute ()
        {
        }

        /// <summary>
        /// Assembly-level form. Used as [assembly: OptimizeAJson(typeof(SomeMarker))] - opts in
        /// every public type in the marker's assembly. Marker can be any public type in the
        /// target assembly; it is not used at runtime, only as a compile-time pointer at the
        /// assembly the generator should walk.
        /// </summary>
        public OptimizeAJsonAttribute (Type assemblyMarker)
        {
            this.AssemblyMarker = assemblyMarker;
        }

        /// <summary>
        /// The marker Type from the target assembly when the assembly-level form is used.
        /// Null for the per-type form.
        /// </summary>
        public Type AssemblyMarker { get; }
    }
}
