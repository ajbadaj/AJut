namespace AJut.UX
{
    using AJut.TypeManagement;

    /// <summary>
    /// Registers <see cref="TypeMetadataExtensionRegistrar"/> defaults for types known to AJut.UX.
    /// Call <see cref="Apply"/> once at application startup (or let a platform-level defaults class
    /// call it as part of its chain). Safe to call multiple times -- subsequent calls are no-ops.
    /// </summary>
    public static class UXTypeMetadataExtensionDefaults
    {
        private static bool m_applied;

        public static void Apply ()
        {
            if (m_applied)
            {
                return;
            }

            m_applied = true;

            // 1. Apply Core defaults first
            TypeMetadataExtensionDefaults.Apply();

            // UX-level type registrations go here as needed.
        }
    }
}
