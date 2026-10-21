namespace AJutShowRoomWinUI.UI.Samples
{
    using System;
    using System.ComponentModel;
    using AJut;

    /// <summary>
    /// The editor modes the ToggleStrip enum repro and the PropertyGrid sample object select between.
    /// </summary>
    public enum eEditorMode { Text, Number, Color }

    /// <summary>
    /// A plain enum for the single-select EnumToggleStrip demo.
    /// </summary>
    public enum eDisplayMode
    {
        Compact,
        Standard,
        Comfortable,
        Wide,
    }

    /// <summary>
    /// A [Flags] enum for the multi-select EnumToggleStrip demo, where the toggles OR into the selection.
    /// </summary>
    [Flags]
    public enum eToppings
    {
        None      = 0,
        Cheese    = 1 << 0,
        Mushrooms = 1 << 1,
        Olives    = 1 << 2,
        Pepperoni = 1 << 3,
        Pineapple = 1 << 4,
    }

    /// <summary>
    /// Shows both ways to keep a member out of an auto-built strip: [Browsable(false)] and [ExcludeFromSelection].
    /// </summary>
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
