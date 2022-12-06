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


    public class TabItemBorderThicknessConverter : SimpleMultiValueConverter<Thickness>
    {
        protected override Thickness Convert (object[] values)
        {
            const int kNumConverterElements = 5;
            Debug.Assert(values.Length == 5, $"Error: {nameof(TabItemBorderThicknessConverter)} requires {kNumConverterElements} elements, got {values.Length}; [0] TabItem --- [1] TabControl --- [2]: TabItem.BorderThickness --- [3]: TabItem.IsSelected --- [4]: TabControl.BorderThickness");

            if (!(values[0] is TabItem tabItem))
            {
                Debug.Fail($"Error: {nameof(TabItemBorderThicknessConverter)} requires {kNumConverterElements} elements - element [0] was incorrect; [0] TabItem --- [1] TabControl --- [2]: TabItem.BorderThickness --- [3]: TabItem.IsSelected --- [4]: TabControl.BorderThickness");
                return new Thickness(0);
            }

            if (!(values[1] is TabControl tabControl))
            {
                Debug.Fail($"Error: {nameof(TabItemBorderThicknessConverter)} requires {kNumConverterElements} elements - element [1] was incorrect; [0] TabItem --- [1] TabControl --- [2]: TabItem.BorderThickness --- [3]: TabItem.IsSelected --- [4]: TabControl.BorderThickness");
                return new Thickness(0);
            }

            if (!(values[2] is Thickness tabItemBorderThickness))
            {
                Debug.Fail($"Error: {nameof(TabItemBorderThicknessConverter)} requires {kNumConverterElements} elements - element [2] was incorrect; [0] TabItem --- [1] TabControl --- [2]: TabItem.BorderThickness --- [3]: TabItem.IsSelected --- [4]: TabControl.BorderThickness");
                return new Thickness(0);
            }

            if (!(values[3] is bool isSelected))
            {
                Debug.Fail($"Error: {nameof(TabItemBorderThicknessConverter)} requires {kNumConverterElements} elements - element [3] was incorrect; [0] TabItem --- [1] TabControl --- [2]: TabItem.BorderThickness --- [3]: TabItem.IsSelected --- [4]: TabControl.BorderThickness");
                return new Thickness(0);
            }

            if (!(values[4] is Thickness tabControlBorderThickness))
            {
                Debug.Fail($"Error: {nameof(TabItemBorderThicknessConverter)} requires {kNumConverterElements} elements - element [4] was incorrect; [0] TabItem --- [1] TabControl --- [2]: TabItem.BorderThickness --- [3]: TabItem.IsSelected --- [4]: TabControl.BorderThickness");
                return new Thickness(0);
            }




            // Ok... going to assume that the TabItem's BorderThickness is more of what you would call a guideline than an actual
            //  border thickness request. We'll use that as source info, but override depending on what side it's on, and what the
            //  TabControl owner's BorderThickness is for that side

            int index = tabControl.Items.IndexOf(tabItem);
            bool isFirst = index == 0;
            bool isLast = tabControl.Items.Count == index + 1;
            bool isMiddle = !isFirst && !isLast;

            switch (tabControl.TabStripPlacement)
            {
                // Left headers are top justified
                case Dock.Left:
                    return new Thickness
                    {
                        Left = isSelected ? tabControlBorderThickness.Right : tabItemBorderThickness.Left,
                        Top = isSelected ? tabControlBorderThickness.Right : (isFirst ? tabItemBorderThickness.Left : 0),
                        Right = 0,
                        Bottom = isSelected ? tabControlBorderThickness.Right : tabItemBorderThickness.Bottom
                    };

                // Top headers are left justified
                case Dock.Top:
                    return new Thickness
                    {
                        Left = isSelected ? tabControlBorderThickness.Top : (isFirst ? tabItemBorderThickness.Left : 0),
                        Top = isSelected ? tabControlBorderThickness.Top : tabItemBorderThickness.Bottom,
                        Right = isSelected ? tabControlBorderThickness.Top : tabItemBorderThickness.Right,
                        Bottom = 0,
                    };

                // Right headers are top justified
                case Dock.Right:
                    return new Thickness
                    {
                        Left = 0,
                        Top = isSelected ? tabControlBorderThickness.Right : (isFirst ? tabItemBorderThickness.Left : 0),
                        Right = isSelected ? tabControlBorderThickness.Right : tabItemBorderThickness.Right,
                        Bottom = isSelected ? tabControlBorderThickness.Right : tabItemBorderThickness.Bottom,
                        //Left = 0,
                        //Top = isFirst ? tabControlBorderThickness.Right : tabItemBorderThickness.Top,
                        //Right = tabControlBorderThickness.Right,
                        //Bottom = isLast ? tabControlBorderThickness.Right : tabItemBorderThickness.Bottom,
                    };

                // Bottom headers are left justified
                case Dock.Bottom:
                    return new Thickness
                    {
                        Left = isSelected ? tabControlBorderThickness.Bottom : (isFirst ? tabItemBorderThickness.Left : 0),
                        Top = 0,
                        Right = isSelected ? tabControlBorderThickness.Bottom : tabItemBorderThickness.Right,
                        Bottom = isSelected ? tabControlBorderThickness.Bottom : tabItemBorderThickness.Bottom,
                    };

                default:
                    throw new NonExistantEnumException<Dock>((int)tabControl.TabStripPlacement);
            }
        }
    }
}
