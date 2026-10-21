namespace AJutShowRoomWinUI.UI.Diagnostics
{
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// Hosts the native crash filter check. The check itself is its own control and carries its own description and
    /// probes; this page only gives it a place in the Debug area.
    /// </summary>
    public sealed partial class CrashFilterPage : Page
    {
        public CrashFilterPage ()
        {
            this.InitializeComponent();
        }
    }
}
