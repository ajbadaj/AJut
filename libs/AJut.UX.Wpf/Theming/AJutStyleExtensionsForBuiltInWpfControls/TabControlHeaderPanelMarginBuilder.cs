namespace AJut.UX.Theming.AJutStyleExtensionsForBuiltInWpfControls
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX.Converters;

    public class TabControlHeaderPanelMarginBuilder : SimpleMultiValueConverter<Thickness>
    {
        private bool ValidateAndExtractFromValues (object[] values, out Dock placement, out CornerRadius cornerRadius, out Thickness borderThickness)
        {
            // Root assertions
            const int kNumConverterElements = 3;
            string kOrderIndicatorText = $"[0] TabControl.TabStripPlacement --- [1]: TabControl.CornerRadius (likely via BorderXTA) --- [2]: TabControl.BorderThickness";
            Debug.Assert(values.Length == kNumConverterElements, $"Error: {this.GetType().Name} requires {kNumConverterElements} elements, got {values.Length}; {kOrderIndicatorText}");
            int debugEvalIndex = 0;

            // Defaults for failure
            placement = default;
            cornerRadius = default;
            borderThickness = default;

            // Individual assertions
            if (!(values[debugEvalIndex++] is Dock found_placement))
            {
                Debug.Fail($"Error: {this.GetType().Name} requires {kNumConverterElements} elements - element [{debugEvalIndex - 1}] was incorrect; {kOrderIndicatorText}");
                return false;
            }

            if (!(values[debugEvalIndex++] is CornerRadius found_cornerRadius))
            {
                Debug.Fail($"Error: {this.GetType().Name} requires {kNumConverterElements} elements - element [{debugEvalIndex - 1}] was incorrect; {kOrderIndicatorText}");
                return false;
            }

            if (!(values[debugEvalIndex++] is Thickness found_borderThickness))
            {
                Debug.Fail($"Error: {this.GetType().Name} requires {kNumConverterElements} elements - element [{debugEvalIndex - 1}] was incorrect; {kOrderIndicatorText}");
                return false;
            }

            // Success
            placement = found_placement;
            cornerRadius = found_cornerRadius;
            borderThickness = found_borderThickness;
            return true;
        }

        protected override Thickness Convert (object[] values)
        {
            if (!ValidateAndExtractFromValues(values, out Dock placement, out CornerRadius cornerRadius, out Thickness borderThickness))
            {
                return new Thickness(0);
            }

            switch (placement)
            {
                // Left headers are top justified
                case Dock.Left:
                    return new Thickness
                    {
                        Left = 0,
                        Top = cornerRadius.TopLeft + borderThickness.Top,
                        Right = 0,
                        Bottom = 0
                    };

                // Top headers are left justified
                case Dock.Top:
                    return new Thickness
                    {
                        Left = cornerRadius.TopLeft + borderThickness.Left,
                        Top = 0,
                        Right = 0,
                        Bottom = 0
                    };

                // Right headers are top justified
                case Dock.Right:
                    return new Thickness
                    {
                        Left = 0,
                        Top = cornerRadius.TopLeft + borderThickness.Top,
                        Right = 0,
                        Bottom = 0
                    };

                // Bottom headers are left justified
                case Dock.Bottom:
                    return new Thickness
                    {
                        Left = cornerRadius.BottomLeft + borderThickness.Left,
                        Top = 0,
                        Right = 0,
                        Bottom = 0
                    };

                default:
                    throw new NonExistantEnumException<Dock>((int)placement);
            }
        }
    }
}
