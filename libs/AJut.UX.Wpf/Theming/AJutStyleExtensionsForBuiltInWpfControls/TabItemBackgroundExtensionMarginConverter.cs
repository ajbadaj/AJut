namespace AJut.UX.Theming.AJutStyleExtensionsForBuiltInWpfControls
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX;
    using AJut.UX.Converters;

    public class TabItemBackgroundExtensionMarginConverter : SimpleMultiValueConverter<Thickness>
    {
        private bool ValidateAndExtractFromValues (object[] values, out TabItem tabItem, out bool isSelected, out TabControl tabControl, out Thickness tabControlBorderThickness, out Dock tabStripPlacement)
        {
            // Root assertions
            const int kNumConverterElements = 5;
            string kOrderIndicatorText = $"[0]: TabItem --- [1]: TabItem.IsSelected --- [2]: TabControl --- [3] TabControl.BorderThickness --- [4] TabControl.TabStripPlacement";
            Debug.Assert(values.Length == kNumConverterElements, $"Error: {this.GetType().Name} requires {kNumConverterElements} elements, got {values.Length}; {kOrderIndicatorText}");

            // Defaults for failure
            tabItem = default;
            isSelected = default;
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
            tabControl = found_tabControl;
            tabControlBorderThickness = found_tabControlBorderThickness;
            tabStripPlacement = found_tabStripPlacement;
            return true;
        }

        protected override Thickness Convert (object[] values)
        {
            if (!ValidateAndExtractFromValues(values, out TabItem tabItem, out bool isSelected, out TabControl tabControl, out Thickness tabControlBorderThickness, out Dock tabStripPlacement))
            {
                return new Thickness(0);
            }

            switch (tabStripPlacement)
            {
                case Dock.Left:
                    return new Thickness
                    {
                        Left = 0,
                        Top = 0,
                        Right = isSelected ? -tabControlBorderThickness.Left : 0,
                        Bottom = 0,
                    };

                // Top headers are left justified
                case Dock.Top:
                    return new Thickness
                    {
                        Left = 0,
                        Top = 0,
                        Right = 0,
                        Bottom = isSelected ? -tabControlBorderThickness.Top : 0
                    };


                // Right headers are top justified
                case Dock.Right:
                    return new Thickness
                    {
                        Left = isSelected ? -tabControlBorderThickness.Right : 0,
                        Top = 0,
                        Right = 0,
                        Bottom = 0,
                    };

                // Bottom headers are left justified
                case Dock.Bottom:
                    return new Thickness
                    {
                        Left = 0,
                        Top = isSelected ? -tabControlBorderThickness.Bottom : 0,
                        Right = 0,
                        Bottom = 0,
                    };

                default:
                    throw new NonExistantEnumException<Dock>((int)tabControl.TabStripPlacement);
            }
        }
    }
}
