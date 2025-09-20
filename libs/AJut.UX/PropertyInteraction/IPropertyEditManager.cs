namespace AJut.UX.PropertyInteraction
{
    using System.Collections.Generic;

    public interface IPropertyEditManager
    {
        /// <summary>
        /// Generate the edit targets for this manager (note: use <see cref="PropertyEditTarget.GenerateForPropertiesOf"/> to generate via reflection if you just want to add a few on top of existing)
        /// </summary>
        IEnumerable<PropertyEditTarget> GenerateEditTargets () => PropertyEditTarget.GenerateForPropertiesOf(this);
    }
}
