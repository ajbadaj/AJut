namespace AJut.UX
{
    using AJut.TypeManagement;
    using Microsoft.UI.Xaml;
    using Windows.Foundation;

    /// <summary>
    /// Registers <see cref="TypeMetadataExtensionRegistrar"/> defaults for types known to AJut.UX.WinUI3.
    /// Call <see cref="Apply"/> once at application startup. Safe to call multiple times -- subsequent
    /// calls are no-ops.
    /// </summary>
    public static class WinUITypeMetadataExtensionDefaults
    {
        private static bool m_applied;

        public static void Apply ()
        {
            if (m_applied)
            {
                return;
            }

            m_applied = true;

            // 1. Apply UX defaults first
            UXTypeMetadataExtensionDefaults.Apply();

            // 2. WinUI3-specific type registrations

            // GridLength: only Value and GridUnitType are meaningful to expose
            TypeMetadataExtensionRegistrar.For<GridLength>()
                .Hide(nameof(GridLength.IsAbsolute), nameof(GridLength.IsAuto), nameof(GridLength.IsStar))
                .SetMemberOrder(nameof(GridLength.Value), 0)
                .SetMemberOrder(nameof(GridLength.GridUnitType), 1);

            // Thickness: expose the four sides in reading order
            TypeMetadataExtensionRegistrar.For<Thickness>()
                .SetMemberOrder(nameof(Thickness.Left), 0)
                .SetMemberOrder(nameof(Thickness.Top), 1)
                .SetMemberOrder(nameof(Thickness.Right), 2)
                .SetMemberOrder(nameof(Thickness.Bottom), 3);

            // Rect: hide computed edge properties, expose position then size
            TypeMetadataExtensionRegistrar.For<Rect>()
                .Hide(nameof(Rect.IsEmpty), nameof(Rect.Left), nameof(Rect.Top),
                      nameof(Rect.Right), nameof(Rect.Bottom))
                .SetMemberOrder(nameof(Rect.X), 0)
                .SetMemberOrder(nameof(Rect.Y), 1)
                .SetMemberOrder(nameof(Rect.Width), 2)
                .SetMemberOrder(nameof(Rect.Height), 3);

            // Point: X before Y
            TypeMetadataExtensionRegistrar.For<Point>()
                .SetMemberOrder(nameof(Point.X), 0)
                .SetMemberOrder(nameof(Point.Y), 1);

            // Size: Width before Height
            TypeMetadataExtensionRegistrar.For<Size>()
                .SetMemberOrder(nameof(Size.Width), 0)
                .SetMemberOrder(nameof(Size.Height), 1);
        }
    }
}
