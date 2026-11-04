namespace AJut.UX
{
    /// <summary>
    /// How child window showing/hiding is coordinated for all windows in a WindowManager
    /// </summary>
    public enum eChildWindowDisplayCoordinationStyle
    {
        /// <summary>
        /// Only allow manual show/hide of windows
        /// </summary>
        Manually,

        /// <summary>
        /// Show/hide all child windows when the root window is activated/deactivated respectively
        /// </summary>
        RootActivationDeactivation,

        /// <summary>
        /// Show/hide all child windows when the root window is restored/minimized respectively
        /// </summary>
        RootMinimizationRestoration,
    }
}
