namespace AJut.UX.Docking
{
    using System;

    public class ClosedEventArgs : EventArgs
    {
        /// <summary>
        /// True if forced closed by a layout reset or programmatic <see cref="DockZoneViewModel.ForceCloseAllAndClear"/>.
        /// Lets consumers differentiate between normal and forced closure.
        /// </summary>
        public bool IsForForcedClose { get; init; }
    }
}
