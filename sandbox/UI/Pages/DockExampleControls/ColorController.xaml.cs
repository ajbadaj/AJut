namespace TheAJutShowRoom.UI.Pages.DockExampleControls
{
    using System.Windows.Controls;
    using AJut.UX.Docking;

    public partial class ColorController : UserControl, IDockableDisplayElement
    {
        public ColorController ()
        {
            this.InitializeComponent();
        }

        public DockingContentAdapterModel? DockingAdapter { get; private set; }

        public void Setup (DockingContentAdapterModel adapter)
        {
            this.DockingAdapter = adapter;
            //this.DockingAdapter.TitleContent = "Color Controller";
        }
    }
}
