namespace AJut.UX.Theming.AJutStyleExtensionsForBuiltInWpfControls
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX;
    using AJut.UX.Converters;


    public class TabItemBorderThicknessConverter : SimpleMultiValueConverter<Thickness>
    {
        private bool ValidateAndExtractFromValues (object[] values, out TabItem tabItem, out bool isSelected, out Thickness tabItemBorderThickness, out TabControl tabControl, out Thickness tabControlBorderThickness, out Dock tabStripPlacement)
        {
            // Root assertions
            const int kNumConverterElements = 6;
            const string kOrderIndicatorText = "[0]: TabItem --- [1]: TabItem.BorderThickness --- [2]: TabItem.IsSelected --- [3]: TabControl --- [4]: TabControl.BorderThickness --- [5]: TabControl.TabStripPlacement";
            Debug.Assert(values.Length == kNumConverterElements, $"Error: {this.GetType().Name} requires {kNumConverterElements} elements, got {values.Length}; {kOrderIndicatorText}");

            // Defaults for failure
            tabItem = default;
            isSelected = default;
            tabItemBorderThickness = default;
            tabControl = default;
            tabControlBorderThickness = default;
            tabStripPlacement = default;

            // Individual assertions
            int debugEvalIndex = 0;
            if (!(values[debugEvalIndex++] is TabItem found_tabItem))
            {
                Debug.Fail($"Error: {this.GetType().Name} requires {kNumConverterElements} elements - element [{debugEvalIndex - 1}] was incorrect; {kOrderIndicatorText}");
                return false;
            }
            if (!(values[debugEvalIndex++] is bool found_isSelected))
            {
                Debug.Fail($"Error: {this.GetType().Name} requires {kNumConverterElements} elements - element [{debugEvalIndex - 1}] was incorrect; {kOrderIndicatorText}");
                return false;
            }
            if (!(values[debugEvalIndex++] is Thickness found_tabItemBorderThickness))
            {
                Debug.Fail($"Error: {this.GetType().Name} requires {kNumConverterElements} elements - element [{debugEvalIndex - 1}] was incorrect; {kOrderIndicatorText}");
                return false;
            }
            if (!(values[debugEvalIndex++] is TabControl found_tabControl))
            {
                Debug.Fail($"Error: {this.GetType().Name} requires {kNumConverterElements} elements - element [{debugEvalIndex - 1}] was incorrect; {kOrderIndicatorText}");
                return false;
            }
            if (!(values[debugEvalIndex++] is Thickness found_tabControlBorderThickness))
            {
                Debug.Fail($"Error: {this.GetType().Name} requires {kNumConverterElements} elements - element [{debugEvalIndex - 1}] was incorrect; {kOrderIndicatorText}");
                return false;
            }
            if (!(values[debugEvalIndex++] is Dock found_tabStripPlacement))
            {
                Debug.Fail($"Error: {this.GetType().Name} requires {kNumConverterElements} elements - element [{debugEvalIndex - 1}] was incorrect; {kOrderIndicatorText}");
                return false;
            }

            // Success
            tabItem = found_tabItem;
            isSelected = found_isSelected;
            tabItemBorderThickness = found_tabItemBorderThickness;
            tabControl = found_tabControl;
            tabControlBorderThickness = found_tabControlBorderThickness;
            tabStripPlacement = found_tabStripPlacement;
            return true;
        }

        protected override Thickness Convert (object[] values)
        {
            if (!ValidateAndExtractFromValues(values, out TabItem tabItem, out bool isSelected, out Thickness tabItemBorderThickness, out TabControl tabControl, out Thickness tabControlBorderThickness, out Dock tabStripPlacement))
            {
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
                        Left = isSelected ? tabControlBorderThickness.Left : tabItemBorderThickness.Left,
                        Top = isSelected ? tabControlBorderThickness.Left : (isFirst ? tabItemBorderThickness.Left : 0),
                        Right = 0,
                        Bottom = isSelected ? tabControlBorderThickness.Left : tabItemBorderThickness.Bottom
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
