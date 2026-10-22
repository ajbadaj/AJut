namespace AJut.UX.Controls
{
    /// <summary>
    /// What a trimmed app is told about the AppKit property grid, which runs on AJut.UX's reflection based
    /// PropertyGridController. The text is the controller's own (AJut.UX's PropertyGridTrimming, which is internal to that
    /// assembly), so a consumer sees the same advice at their PropertyGrid as AJut.UX gives at the controller.
    /// </summary>
    internal static class PropertyGridTrimming
    {
        /// <summary>Why the property grid's reflection needs the members it shows kept</summary>
        public const string kMessage = "The property grid finds and edits the properties of the objects it shows through reflection, and trimming removes a property nothing else uses, so it never shows. Keep a shown type's members with [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] on it.";

        /// <summary>Why the property grid's list editing needs dynamic code</summary>
        public const string kListMessage = "The property grid's list editor makes arrays of an element type known only at runtime, which a NativeAOT build can only do for an array type the app uses elsewhere.";
    }
}
