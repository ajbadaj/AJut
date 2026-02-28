namespace AJut.TypeManagement
{
    /// <summary>
    /// Registers <see cref="TypeMetadataExtensionRegistrar"/> defaults for types known to AJut.Core.
    /// Call <see cref="Apply"/> once at application startup (or let a higher-level defaults class
    /// call it as part of its own chain). Safe to call multiple times -- subsequent calls are no-ops.
    /// </summary>
    public static class TypeMetadataExtensionDefaults
    {
        private static bool m_applied;

        public static void Apply ()
        {
            if (m_applied)
            {
                return;
            }

            m_applied = true;

            // No Core-specific type registrations currently needed.
            // Platform-level defaults (UX / WPF / WinUI3) call this first, then add their own.
        }
    }
}
