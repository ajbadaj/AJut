namespace AJut.UX.PropertyInteraction
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    public interface IPropertyEditManager
    {
        private const string kDefaultTargetsJustification = "Only PropertyGridManager calls this, and using one carries RequiresUnreferencedCode and RequiresDynamicCode; an implementation that builds its own targets needs neither.";

        /// <summary>
        /// Generate the edit targets for this manager (note: use <see cref="PropertyEditTarget.GenerateForPropertiesOf"/> to generate via reflection if you just want to add a few on top of existing)
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = kDefaultTargetsJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = kDefaultTargetsJustification)]
        IEnumerable<PropertyEditTarget> GenerateEditTargets () => PropertyEditTarget.GenerateForPropertiesOf(this);
    }
}
