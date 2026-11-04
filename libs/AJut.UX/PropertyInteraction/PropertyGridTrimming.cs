namespace AJut.UX.PropertyInteraction
{
    /// <summary>
    /// What a trimmed or ahead of time compiled app is told about the property grid, which reads and writes the objects it
    /// shows through reflection
    /// </summary>
    internal static class PropertyGridTrimming
    {
        /// <summary>
        /// Why the property grid's reflection needs the members it shows kept, and how. [DynamicallyAccessedMembers] on the
        /// shown type itself keeps nothing, since the grid takes the object as object; the generic entry points carry the
        /// annotation on their type parameter, which the trimmer follows from the call.
        /// </summary>
        public const string kMessage = "The property grid finds and edits the properties of the objects it shows through reflection, and trimming removes a property nothing else uses, so its row never shows. Show an object through the grid's SetSingleItemSource<T> or SetItemsSource<T>, which keep T's members; SingleItemSource and ItemsSource take an object, so [DynamicallyAccessedMembers] on its type keeps nothing. Keep a sub-object or list element type the grid expands with [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(...))].";

        /// <summary>Why the property grid's list editing needs dynamic code</summary>
        public const string kListMessage = "The property grid's list editor makes arrays of an element type known only at runtime, which a NativeAOT build can only do for an array type the app uses elsewhere.";
    }
}
