namespace AJutShowRoom.StackNavTest
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<ThirdDisplay>;

    public partial class ThirdDisplay : UserControl, IStackNavDisplayControl
    {
        private static int g_uniqueIdMaker = 0;
        public ThirdDisplay ()
        {
            this.UniqueId = g_uniqueIdMaker++;
            this.InitializeComponent();
        }

        private static readonly DependencyPropertyKey UniqueIdPropertyKey = DPUtils.RegisterReadOnly(_ => _.UniqueId);
        public static readonly DependencyProperty UniqueIdProperty = UniqueIdPropertyKey.DependencyProperty;
        public int UniqueId
        {
            get => (int)this.GetValue(UniqueIdProperty);
            protected set => this.SetValue(UniqueIdPropertyKey, value);
        }


        public static readonly DependencyProperty BkgColorProperty = DPUtils.Register(_ => _.BkgColor, CoerceUtils.CoerceColorFrom("#888"));
        public Color BkgColor
        {
            get => (Color)this.GetValue(BkgColorProperty);
            set => this.SetValue(BkgColorProperty, value);
        }

        public void Setup (StackNavAdapter adapter)
        {
            this.NavAdapter = adapter;
            this.NavAdapter.Title = "Third Display";
            this.NavAdapter.PreserveFullAdapterAndControlOnCover = true;
        }

        public static readonly DependencyProperty NavAdapterProperty = DPUtils.Register(_ => _.NavAdapter);
        public StackNavAdapter NavAdapter
        {
            get => (StackNavAdapter)this.GetValue(NavAdapterProperty);
            set => this.SetValue(NavAdapterProperty, value);
        }

        private void NavToNewSecond_OnClick (object sender, RoutedEventArgs e)
        {
            this.NavAdapter.Navigator.GenerateAndPushDisplay<SecondDisplay>(5);
        }
    }
}
