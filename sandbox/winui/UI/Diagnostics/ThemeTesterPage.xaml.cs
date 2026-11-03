namespace AJutShowRoomWinUI.UI.Diagnostics
{
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// Every control the AJut theme styles, in each of its states side by side, so a palette can be judged in one look.
    /// The same sections as the AppKit, Android and UIKit showrooms' Theme Tester; states that need the pointer or focus
    /// are held with <see cref="PinnedVisualState"/>.
    /// </summary>
    public sealed partial class ThemeTesterPage : Page
    {
        public ThemeTesterPage ()
        {
            this.InitializeComponent();
        }

        // ===========[ Properties ]===============================================
        public string[] StripItems { get; } = new[] { "One", "Two", "Three" };

        public string[] ManyItems { get; } = new[] { "January", "February", "March", "April", "May", "June", "July" };
    }
}
