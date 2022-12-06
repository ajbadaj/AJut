using AJut.UX.Converters;

namespace AJut.UX.Theming.TabControlAJutStyling
{
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;

    public class TabControlHeaderPanelMarginBuilder : SimpleMultiValueConverter<Thickness>
    {
        // 0: CornerRadius
        // 0: BorderThickness
        protected override Thickness Convert(object[] values)
        {
            Debug.Assert(values.Length == 3, "Error: TabStripPlacementMarginConverter requires 3 elements; [0] Dock placement, [1]: CornerRadius, [2]: BorderThickness");
            if (!(values[0] is Dock placement))
            {
                if (values[0] == DependencyProperty.UnsetValue)
                {
                    placement = Dock.Top;
                }
                else
                {
                    throw new InvalidSetupException("Error: TabStripPlacementMarginConverter requires 3 elements; [0] Dock placement, [1]: CornerRadius, [2]: BorderThickness");
                }
            }

            if (!(values[1] is CornerRadius cornerRadius))
            {
                if (values[1] == DependencyProperty.UnsetValue)
                {
                    cornerRadius = new CornerRadius();
                }
                else
                {
                    throw new InvalidSetupException("Error: TabStripPlacementMarginConverter requires 3 elements; [0] Dock placement, [1]: CornerRadius, [2]: BorderThickness");
                }
            }

            if (!(values[2] is Thickness borderThickness))
            {
                if (values[2] == DependencyProperty.UnsetValue)
                {
                    borderThickness = new Thickness();
                }
                else
                {
                    throw new InvalidSetupException("Error: TabStripPlacementMarginConverter requires 3 elements; [0] Dock placement, [1]: CornerRadius, [2]: BorderThickness");
                }
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
