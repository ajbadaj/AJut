namespace AJutShowRoomWinUI.UI.Controls
{
    using AJutShowRoomWinUI.UI.Samples;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// EnumToggleStrip bound to a plain enum, a [Flags] enum, an enum with members excluded two ways, and a plain
    /// enum laid out vertically.
    /// </summary>
    public sealed partial class EnumToggleStripPage : Page
    {
        public EnumToggleStripPage ()
        {
            this.InitializeComponent();
        }

        public eDisplayMode DisplayMode { get; set; } = eDisplayMode.Standard;
        public eToppings Toppings { get; set; } = eToppings.Cheese | eToppings.Pepperoni;
        public eExclusionDemo ExclusionPick { get; set; } = eExclusionDemo.Visible1;
        public eDisplayMode VerticalDisplayMode { get; set; } = eDisplayMode.Comfortable;

        public string DemoCode { get; } = @"<ajut:EnumToggleStrip SelectedItem=""{x:Bind DisplayMode, Mode=TwoWay}""/>
<ajut:EnumToggleStrip AllowMultiSelect=""True"" AllowNoSelection=""True""
                      SelectedItem=""{x:Bind Toppings, Mode=TwoWay}""/>
<ajut:EnumToggleStrip SelectedItem=""{x:Bind ExclusionPick, Mode=TwoWay}""/>
<ajut:EnumToggleStrip Orientation=""Vertical""
                      SelectedItem=""{x:Bind VerticalDisplayMode, Mode=TwoWay}""/>

// The bound property picks the enum type, and the strip builds its options from it
public eDisplayMode DisplayMode { get; set; } = eDisplayMode.Standard;
public eToppings Toppings { get; set; } = eToppings.Cheese | eToppings.Pepperoni;
public eExclusionDemo ExclusionPick { get; set; } = eExclusionDemo.Visible1;
public eDisplayMode VerticalDisplayMode { get; set; } = eDisplayMode.Comfortable;

[Flags]
public enum eToppings { None = 0, Cheese = 1 << 0, Mushrooms = 1 << 1, Olives = 1 << 2, Pepperoni = 1 << 3, Pineapple = 1 << 4 }

public enum eExclusionDemo
{
    Visible1,
    [Browsable(false)] HiddenViaBrowsable,
    Visible2,
    [ExcludeFromSelection] HiddenViaAjutAttr,
    Visible3,
}";
    }
}
