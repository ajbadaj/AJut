namespace TheAJutShowRoom.UI.Controls
{
    using System;
    using System.ComponentModel;
    using AJut;

    public enum eDisplayMode
    {
        Compact,
        Standard,
        Comfortable,
        Wide,
    }

    [Flags]
    public enum eToppings
    {
        None        = 0,
        Cheese      = 1 << 0,
        Mushrooms   = 1 << 1,
        Olives      = 1 << 2,
        Pepperoni   = 1 << 3,
        Pineapple   = 1 << 4,
    }

    public enum eExclusionDemo
    {
        Visible1,
        [Browsable(false)]
        HiddenViaBrowsable,
        Visible2,
        [ExcludeFromSelection]
        HiddenViaAjutAttr,
        Visible3,
    }
}
