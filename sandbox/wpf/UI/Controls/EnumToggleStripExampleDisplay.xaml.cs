namespace TheAJutShowRoom.UI.Controls
{
    using System.Windows;
    using System.Windows.Controls;

    using DPUtils = AJut.UX.DPUtils<EnumToggleStripExampleDisplay>;

    public partial class EnumToggleStripExampleDisplay : UserControl
    {
        public EnumToggleStripExampleDisplay ()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty DisplayModeProperty = DPUtils.Register(_ => _.DisplayMode, eDisplayMode.Standard);
        public eDisplayMode DisplayMode
        {
            get => (eDisplayMode)this.GetValue(DisplayModeProperty);
            set => this.SetValue(DisplayModeProperty, value);
        }

        public static readonly DependencyProperty ToppingsProperty = DPUtils.Register(_ => _.Toppings, eToppings.Cheese | eToppings.Pepperoni);
        public eToppings Toppings
        {
            get => (eToppings)this.GetValue(ToppingsProperty);
            set => this.SetValue(ToppingsProperty, value);
        }

        public static readonly DependencyProperty ExclusionPickProperty = DPUtils.Register(_ => _.ExclusionPick, eExclusionDemo.Visible1);
        public eExclusionDemo ExclusionPick
        {
            get => (eExclusionDemo)this.GetValue(ExclusionPickProperty);
            set => this.SetValue(ExclusionPickProperty, value);
        }
    }
}
