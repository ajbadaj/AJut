namespace AJut.UX.Docking
{
    using System;

    public class ClosedEventArgs : EventArgs
    {
        /// <summary>
        /// A docked element is forced closed either by layout reset or by api user's programatic calling of <see cref="DockZoneViewModel.ForceCloseAllAndClear"/>. Noted
        /// incase api end user would like to differentiate between normal closing and forced closing to better tailor notification, etc.
        /// </summary>
        public bool IsForForcedClose { get; init; }
    }
}
