namespace AJut.UX.Controls
{
    /// <summary>
    /// What a trimmed app is told about the AppKit property grid, which runs on AJut.UX's reflection based
    /// PropertyGridController. The text is the controller's own (AJut.UX's PropertyGridTrimming, which is internal to that
    /// assembly), so a consumer sees the same advice at their PropertyGrid as AJut.UX gives at the controller.
    /// </summary>
    internal static class PropertyGridTrimming
    {
        /// <summary>
        /// Why the property grid's reflection needs the members it shows kept, and how. [DynamicallyAccessedMembers] on the
        /// shown type itself keeps nothing, since SingleItemSource and ItemsSource take an object; the generic entry points
        /// carry the annotation on their type parameter, which the trimmer follows from the call.
        /// </summary>
        public const string kMessage = "The property grid finds and edits the properties of the objects it shows through reflection, and trimming removes a property nothing else uses, so its row never shows. Show an object through the grid's SetSingleItemSource<T> or SetItemsSource<T>, which keep T's members; SingleItemSource and ItemsSource take an object, so [DynamicallyAccessedMembers] on its type keeps nothing. Keep a sub-object or list element type the grid expands with [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(...))].";

        /// <summary>Why the property grid's list editing needs dynamic code</summary>
        public const string kListMessage = "The property grid's list editor makes arrays of an element type known only at runtime, which a NativeAOT build can only do for an array type the app uses elsewhere.";

        /// <summary>
        /// Why the grid's own members can call into the reflection based controller without warning: the warning is the
        /// app's, at the one place it hands the grid a type to reflect over
        /// </summary>
        public const string kSourceCarriesTheWarning = "The grid only reflects over the source it is given, and setting SingleItemSource or ItemsSource carries the RequiresUnreferencedCode and RequiresDynamicCode warnings for it (SetSingleItemSource<T> and SetItemsSource<T> keep T's members instead). This member reflects over nothing else: it sets the grid up, clears it, or works on rows built from that source.";

        /// <summary>Why the generic entry points can set the object properties without the trim warning</summary>
        public const string kTypeParameterKeepsTheMembers = "T carries [DynamicallyAccessedMembers(All)], so the trimmer keeps the shown type's members from the call; a sub-object or list element type the grid expands is kept by the caller, as the method's summary says.";
    }
}
