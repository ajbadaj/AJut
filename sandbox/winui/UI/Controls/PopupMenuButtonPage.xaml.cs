namespace AJutShowRoomWinUI.UI.Controls
{
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The PopupMenuButton demos: two buttons, each with its own flyout items.
    /// </summary>
    public sealed partial class PopupMenuButtonPage : Page
    {
        public PopupMenuButtonPage ()
        {
            this.InitializeComponent();
        }

        public string DemoCode { get; } = @"<ajut:PopupMenuButton>
    <ajut:PopupMenuButton.ButtonContent>
        <TextBlock Text=""Actions"" VerticalAlignment=""Center""/>
    </ajut:PopupMenuButton.ButtonContent>
    <ajut:PopupMenuButton.MenuItems>
        <MenuFlyoutItem Text=""New File""/>
        <MenuFlyoutItem Text=""Open...""/>
        <MenuFlyoutSeparator/>
        <MenuFlyoutItem Text=""Exit""/>
    </ajut:PopupMenuButton.MenuItems>
</ajut:PopupMenuButton>";
    }
}
