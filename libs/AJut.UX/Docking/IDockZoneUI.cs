namespace AJut.UX.Docking
{
    using System.Collections.Generic;

    /// <summary>
    /// Abstracts the UI control that hosts a <see cref="DockZoneViewModel"/>, allowing the ViewModel
    /// to be platform-agnostic (shared between WPF and WinUI3).
    /// </summary>
    public interface IDockZoneUI
    {
        /// <summary>The rendered/actual size of this zone's UI element.</summary>
        DockZoneSize RenderSize { get; }

        /// <summary>
        /// Apply proportional pixel sizes to the zone's child columns or rows. Implementations are
        /// responsible for scheduling this on the UI thread if needed.
        /// </summary>
        void SetTargetSizeAsync (List<double> sizes);
    }
}
