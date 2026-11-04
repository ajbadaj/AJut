namespace TheAJutShowRoom.UI.Controls.Diagnostics
{
    using System.Windows.Controls;

    /// <summary>
    /// Every control the AJut theme styles, in each of its states side by side, so a palette can be judged in one look.
    /// The same sections as the WinUI3, AppKit, Android and UIKit showrooms' Theme Tester. The managed brush buttons hold
    /// pointer over and pressed by overriding CurrentState; what WPF can't hold is said where it would go, with a live
    /// sample to try by hand.
    /// </summary>
    public partial class ThemeTesterDisplay : UserControl
    {
        public ThemeTesterDisplay ()
        {
            this.InitializeComponent();
        }
    }
}
