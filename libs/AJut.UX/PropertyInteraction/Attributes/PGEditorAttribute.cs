namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// <see cref="PropertyGrid"/> attr: What editor should this property be displayed with?
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple=false, Inherited=true)]
    public class PGEditorAttribute : Attribute
    {
        public PGEditorAttribute (string editor)
        {
            this.Editor = editor;
        }

        public string Editor { get; set; }
    }
}
