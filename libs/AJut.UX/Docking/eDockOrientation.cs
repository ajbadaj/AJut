namespace AJut.UX.Docking
{
    using System;

    [Flags]
    public enum eDockOrientation
    {
        /// <summary>An empty zone, contains nothing.</summary>
        Empty = 0b0000,

        /// <summary>Split: anterior zone on the left, posterior on the right.</summary>
        Horizontal = 0b0001,

        /// <summary>Split: anterior zone on top, posterior below.</summary>
        Vertical = 0b0010,

        /// <summary>Leaf: single element to display.</summary>
        Single = 0b0100,

        /// <summary>Leaf: more than one element to display (shown as tabs).</summary>
        Tabbed = 0b1000,

        /// <summary>Either horizontal or vertical - zone contains two sub-zones.</summary>
        AnySplitOrientation = Horizontal | Vertical,

        /// <summary>Leaf orientations that contain displayable elements.</summary>
        AnyLeafDisplay = Single | Tabbed | Empty,
    }
}
