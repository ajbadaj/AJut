namespace AJutShowRoom.StackNavTest
{
    using System.Windows.Controls;
    using AJut.UX;

    public partial class FirstDisplayDrawer : UserControl, IStackNavDrawerDisplay
    {
        public FirstDisplayDrawer ()
        {
            this.InitializeComponent();
        }

        public string Title => "First Display (Custom)";
    }
}
