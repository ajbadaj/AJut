namespace TheAJutShowRoom.UI.Controls
{
    using System.Windows.Controls;
    using DPUtils = AJut.UX.DPUtils<WindowChromeButtonStripExample>;

    public partial class WindowChromeButtonStripExample : UserControl
    {
        public WindowChromeButtonStripExample()
        {
            this.InitializeComponent();
        }


        public string CodeText =>
@"
<ajut:WindowChromeButtonStrip AllowMaximizeRestore=""False""
                              AllowFullscreen=""False""
                              MinimizeToolTip=""This will **actually** minimize this application""
                              CloseToolTip=""This will **actually** close this application"" />".TrimStart('\r').TrimStart('\n');

    }
}
