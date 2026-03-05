namespace AJut.UX.Docking
{
    using System;

    /// <summary>
    /// A custom context menu entry for a docked panel's header right-click menu.
    /// Set <see cref="IsSeparator"/> to true for a visual divider (other fields ignored).
    /// </summary>
    public record struct DockPanelMenuOption
    {
        /// <summary>When true, this entry renders as a menu separator. Title and Action are ignored.</summary>
        public bool IsSeparator { get; init; }

        /// <summary>Display text for the menu item.</summary>
        public string Title { get; init; }

        /// <summary>Callback invoked when the menu item is clicked. Receives the adapter of the panel whose header was right-clicked.</summary>
        public Action<DockingContentAdapterModel> Action { get; init; }
    }
}
