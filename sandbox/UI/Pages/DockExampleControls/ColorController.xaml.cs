namespace TheAJutShowRoom.UI.Pages.DockExampleControls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using AJut.UX.Docking;

    using DPUtils = AJut.UX.DPUtils<ColorController>;

    public partial class ColorController : UserControl, IDockableDisplayElement
    {
        public ColorController (DockingFrameworkOverviewPage owner)
        {
            this.Owner = owner;
            this.InitializeComponent();
        }

        public DockingFrameworkOverviewPage Owner { get; }



        public static readonly DependencyProperty NewBkgProperty = DPUtils.Register(_ => _.NewBkg, (d,e)=>d.Owner.Background = new SolidColorBrush(e.NewValue));
        public Color NewBkg
        {
            get => (Color)this.GetValue(NewBkgProperty);
            set => this.SetValue(NewBkgProperty, value);
        }



        public DockingContentAdapterModel? DockingAdapter { get; private set; }

        public void Setup (DockingContentAdapterModel adapter)
        {
            this.DockingAdapter = adapter;
            //this.DockingAdapter.TitleContent = "Color Controller";
        }
    }
}
