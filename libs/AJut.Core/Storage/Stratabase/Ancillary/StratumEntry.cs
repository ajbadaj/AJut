namespace AJut.Storage
{
    using System;
    using AJut.Text.AJson;

    /// <summary>
    /// How an import treats what a <see cref="Stratabase"/> layer already holds
    /// </summary>
    public enum eStratumImportMode
    {
        /// <summary>
        /// Set each imported entry, and leave every other value in the layer as it was
        /// </summary>
        Merge,

        /// <summary>
        /// Set each imported entry, and remove every value in the layer that the import does not name, so the layer ends up
        /// holding exactly the imported entries. Other layers are untouched.
        /// </summary>
        Replace,
    }

    /// <summary>
    /// One value stored in a <see cref="Stratabase"/> layer: the <see cref="PropertyName"/> property of the object
    /// <see cref="ItemId"/>, holding <see cref="Value"/>. An entry does not record which layer it came from, so entries exported
    /// from one layer (<see cref="Stratabase.ExportOverrideLayer"/>, <see cref="Stratabase.ExportBaseline"/>) can be imported into
    /// any layer of any store that uses the same ids (<see cref="Stratabase.ImportIntoOverrideLayer"/>, <see cref="Stratabase.ImportIntoBaseline"/>).
    /// <para>
    /// <see cref="Value"/> is written to json with the type id of its runtime type. A value whose type has no registered type id
    /// (see <see cref="AJut.TypeManagement.TypeIdRegistrar"/>) is written with its assembly qualified type name instead, which only
    /// reads back where that type can be resolved by that name.
    /// </para>
    /// </summary>
    /// <param name="ItemId">The id of the object the value belongs to</param>
    /// <param name="PropertyName">The property the value is stored under</param>
    /// <param name="Value">The stored value</param>
    public readonly record struct StratumEntry (Guid ItemId, string PropertyName, [property: JsonRuntimeTypeEval] object Value);
}
