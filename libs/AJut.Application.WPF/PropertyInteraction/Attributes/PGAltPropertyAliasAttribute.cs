namespace AJut.Application.PropertyInteraction
{
    using System;

    /// <summary>
    /// <see cref="PropertyGrid"/> attr: What additional property changes should this property recache on when property change is triggered
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class PGAltPropertyAliasAttribute : Attribute
    {
        public PGAltPropertyAliasAttribute (params string[] recacheOn)
        {
            this.AltPropertyAliases = recacheOn;
        }

        public string[] AltPropertyAliases { get; set; }
    }
}
