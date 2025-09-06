// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AJutShowRoomWinUI
{
    using AJut.UX;
    using AJut.UX.Theming;
    using Microsoft.UI.Xaml;


    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow (WindowManager manager, AppThemeManager themeManager)
        {
            manager.Setup(this);
            themeManager.ApplyTheme(this);
            this.InitializeComponent();
        }
    }
}
