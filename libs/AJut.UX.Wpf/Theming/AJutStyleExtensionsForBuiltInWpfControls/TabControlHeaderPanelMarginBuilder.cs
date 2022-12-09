namespace AJut.UX.Theming.AJutStyleExtensionsForBuiltInWpfControls
{
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX.Converters;

    public class TabControlHeaderPanelMarginBuilder : SimpleMultiValueConverter<Thickness>
    {
        private bool ValidateAndExtractFromValues (object[] values, out Dock tabStripPlacement, out CornerRadius tabControlBorderPlacement, out Thickness tabControlBorderThickness)
        {
            // Root assertions
            const int kNumConverterElements = 3;
            string kOrderIndicatorText = $"[0] TabControl.TabStripPlacement --- [1]: TabControl.CornerRadius (likely via BorderXTA) --- [2]: TabControl.BorderThickness";
            Debug.Assert(values.Length == kNumConverterElements, $"Error: {this.GetType().Name} requires {kNumConverterElements} elements, got {values.Length}; {kOrderIndicatorText}");
            int debugEvalIndex = 0;

            // Defaults for failure
            tabStripPlacement = default;
            tabControlBorderPlacement = default;
            tabControlBorderThickness = default;

            // Individual assertions
            if (!(values[debugEvalIndex++] is Dock found_tabStripPlacement))
            {
                Debug.Fail($"Error: {this.GetType().Name} requires {kNumConverterElements} elements - element [{debugEvalIndex - 1}] was incorrect; {kOrderIndicatorText}");
                return false;
            }

            if (!(values[debugEvalIndex++] is CornerRadius found_tabControlCornerRadius))
            {
                Debug.Fail($"Error: {this.GetType().Name} requires {kNumConverterElements} elements - element [{debugEvalIndex - 1}] was incorrect; {kOrderIndicatorText}");
                return false;
            }

            if (!(values[debugEvalIndex++] is Thickness found_tabControlBorderThickness))
            {
                Debug.Fail($"Error: {this.GetType().Name} requires {kNumConverterElements} elements - element [{debugEvalIndex - 1}] was incorrect; {kOrderIndicatorText}");
                return false;
            }

            // Success
            tabStripPlacement = found_tabStripPlacement;
            tabControlBorderPlacement = found_tabControlCornerRadius;
            tabControlBorderThickness = found_tabControlBorderThickness;
            return true;
        }

        protected override Thickness Convert (object[] values)
        {
            if (!ValidateAndExtractFromValues(values, out Dock tabStripPlacement, out CornerRadius tabControlBorderPlacement, out Thickness tabControlBorderThickness))
            {
                return new Thickness(0);
            }

            /* ***********************************************************************
             * Goal is:
             *  1. Avoid the corner radius
             *  2. Avoid the tab control's border according the docked side & the docked 
             *      side's justification (ie bottom docked is left justified, so avoid 
             *      tab control's left border thickness)
             * **********************************************************************/

            switch (tabStripPlacement)
            {
                // Left headers are top justified
                case Dock.Left:
                    return new Thickness
                    {
                        Left = 0,
                        Top = tabControlBorderPlacement.TopLeft + tabControlBorderThickness.Top,
                        Right = 0,
                        Bottom = 0
                    };

                // Top headers are left justified
                case Dock.Top:
                    return new Thickness
                    {
                        Left = tabControlBorderPlacement.TopLeft + tabControlBorderThickness.Left,
                        Top = 0,
                        Right = 0,
                        Bottom = 0
                    };

                // Right headers are top justified
                case Dock.Right:
                    return new Thickness
                    {
                        Left = 0,
                        Top = tabControlBorderPlacement.TopLeft + tabControlBorderThickness.Top,
                        Right = 0,
                        Bottom = 0
                    };

                // Bottom headers are left justified
                case Dock.Bottom:
                    return new Thickness
                    {
                        Left = tabControlBorderPlacement.BottomLeft + tabControlBorderThickness.Left,
                        Top = 0,
                        Right = 0,
                        Bottom = 0
                    };

                default:
                    throw new NonExistantEnumException<Dock>((int)tabStripPlacement);
            }
        }
    }
}
