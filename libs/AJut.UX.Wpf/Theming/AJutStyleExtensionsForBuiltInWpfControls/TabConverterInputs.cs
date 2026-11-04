namespace AJut.UX.Theming.AJutStyleExtensionsForBuiltInWpfControls
{
    using System.Windows;

    /// <summary>What the tab control and tab item converters share about the values their MultiBindings hand them.</summary>
    internal static class TabConverterInputs
    {
        /// <summary>
        /// Whether any of <paramref name="values"/> is DependencyProperty.UnsetValue, which is what a binding gives while it
        /// has nothing to bind to yet: a TabItem's binding to the TabControl around it, say, while the TabItem is measured
        /// before it is in one. That is a binding that has not settled rather than a wiring mistake, so a converter returns
        /// its default for it quietly, and works the value out when the binding settles and hands it a real one. A value of
        /// the wrong type is still a wiring mistake, and still asserts.
        /// </summary>
        public static bool IsAnyUnset (object[] values)
        {
            foreach (object value in values)
            {
                if (value == DependencyProperty.UnsetValue)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
