namespace AJut.UX.Theming.TabControlAJutStyling
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX;
    using AJut.UX.Converters;


    public class TabItemRootMarginGenerator : SimpleMultiValueConverter<Thickness>
    {
        protected override Thickness Convert (object[] values)
        {
            const int kNumConverterElements = 7;
            Debug.Assert(values.Length == kNumConverterElements, $"Error: {nameof(TabItemBorderThicknessConverter)} requires {kNumConverterElements} elements, got {values.Length}; [0] TabItem --- [1] TabControl --- [2]: TabItem.BorderThickness --- [3]: TabItem.IsSelected");

            int tab = 0;
            if (!(values[tab++] is TabItem tabItem))
            {
                Debug.Fail($"Error: {nameof(TabItemBorderThicknessConverter)} requires {kNumConverterElements} elements - element [{tab}] was incorrect; [0] TabItem --- [1] TabControl --- [2]: TabItem.BorderThickness --- [3]: TabItem.IsSelected");
                return new Thickness(0);
            }

            if (!(values[tab++] is Thickness tabItemBorderThickness))
            {
                Debug.Fail($"Error: {nameof(TabItemBorderThicknessConverter)} requires {kNumConverterElements} elements - element [{tab}] was incorrect; [0] TabItem --- [1] TabControl --- [2]: TabItem.BorderThickness --- [3]: TabItem.IsSelected");
                return new Thickness(0);
            }

            if (!(values[tab++] is bool isSelected))
            {
                Debug.Fail($"Error: {nameof(TabItemBorderThicknessConverter)} requires {kNumConverterElements} elements - element [{tab}] was incorrect; [0] TabItem --- [1] TabControl --- [2]: TabItem.BorderThickness --- [3]: TabItem.IsSelected");
                return new Thickness(0);
            }


            if (!(values[tab++] is TabControl tabControl))
            {
                Debug.Fail($"Error: {nameof(TabItemBorderThicknessConverter)} requires {kNumConverterElements} elements - element [{tab}] was incorrect; [0] TabItem --- [1] TabControl --- [2]: TabItem.BorderThickness --- [3]: TabItem.IsSelected");
                return new Thickness(0);
            }

            if (!(values[tab++] is Thickness tabControlBorderThickness))
            {
                Debug.Fail($"Error: {nameof(TabItemBorderThicknessConverter)} requires {kNumConverterElements} elements - element [{tab}] was incorrect; [0] TabItem --- [1] TabControl --- [2]: TabItem.BorderThickness --- [3]: TabItem.IsSelected");
                return new Thickness(0);
            }

            if (!(values[tab++] is Dock tabStripPlacement))
            {
                Debug.Fail($"Error: {nameof(TabItemBorderThicknessConverter)} requires {kNumConverterElements} elements - element [{tab}] was incorrect; [0] TabItem --- [1] TabControl --- [2]: TabItem.BorderThickness --- [3]: TabItem.IsSelected");
                return new Thickness(0);
            }

            if (!(values[tab++] is int unselectedPad))
            {
                Debug.Fail($"Error: {nameof(TabItemBorderThicknessConverter)} requires {kNumConverterElements} elements - element [{tab}] was incorrect; [0] TabItem --- [1] TabControl --- [2]: TabItem.BorderThickness --- [3]: TabItem.IsSelected");
                return new Thickness(0);
            }



            // Ok... going to assume that the TabItem's BorderThickness is more of what you would call a guideline than an actual
            //  border thickness request. We'll use that as source info, but override depending on what side it's on, and what the
            //  TabControl owner's BorderThickness is for that side

            int index = tabControl.Items.IndexOf(tabItem);
            bool isFirst = index == 0;
            bool isLast = tabControl.Items.Count == index + 1;
            bool isMiddle = !isFirst && !isLast;

            switch (tabStripPlacement)
            {
                // Left headers are top justified
                case Dock.Left:
                    return new Thickness
                    {
                        Left = isSelected ? 0 : unselectedPad,
                        Top = !isFirst && isSelected ? -tabItemBorderThickness.Top : 0,
                        Right = _SelectOrSubtractAttachPointMargin(tabItemBorderThickness.Right, tabControlBorderThickness.Right),
                        Bottom = 0
                    };

                // Top headers are left justified
                case Dock.Top:
                    return new Thickness
                    {
                        Left = !isFirst && isSelected ? -tabItemBorderThickness.Left : 0,
                        Top = isSelected ? 0 : unselectedPad,
                        Right = 0,
                        Bottom = _SelectOrSubtractAttachPointMargin(tabItemBorderThickness.Bottom, tabControlBorderThickness.Bottom),
                    };

                // Right headers are top justified
                case Dock.Right:
                    return new Thickness
                    {
                        Left = _SelectOrSubtractAttachPointMargin(tabItemBorderThickness.Left, tabControlBorderThickness.Left),
                        Top = !isFirst && isSelected ? -tabItemBorderThickness.Top : 0,
                        Right = isSelected ? 0 : unselectedPad,
                        Bottom = 0
                    };

                case Dock.Bottom:
                    return new Thickness
                    {
                        Left = !isFirst && isSelected ? -tabItemBorderThickness.Left : 0,
                        Top = _SelectOrSubtractAttachPointMargin(tabItemBorderThickness.Top, tabControlBorderThickness.Top),
                        Right = 0,
                        Bottom = isSelected ? 0 : unselectedPad,
                    };

                default:
                    throw new NonExistantEnumException<Dock>((int)tabControl.TabStripPlacement);
            }

            double _SelectOrSubtractAttachPointMargin (double _tabItemElement, double _tabControlElement)
            {
                //if (_tabControlElement > _tabItemElement)
                //{
                //    return -(_tabControlElement - _tabItemElement);
                //}

                return 0;
            }
        }
    }
}
