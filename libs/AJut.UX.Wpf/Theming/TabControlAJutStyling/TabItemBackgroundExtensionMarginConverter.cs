namespace AJut.UX.Theming.TabControlAJutStyling
{
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX;
    using AJut.UX.Converters;

    public class TabItemBackgroundExtensionMarginConverter : SimpleMultiValueConverter<Thickness>
    {
        protected override Thickness Convert (object[] values)
        {
            const int kNumConverterElements = 5;
            Debug.Assert(values.Length == kNumConverterElements, $"Error: {nameof(TabItemBackgroundExtensionMarginConverter)} requires {kNumConverterElements} elements, got {values.Length}; [0] TabItem --- [1] TabItem.IsSelected --- [2] TabControl --- [3]: TabControl.BorderThickness --- [4] TabControl.TabStripPlacement");

            if (!(values[0] is TabItem tabItem))
            {
                Debug.Fail($"Error: {nameof(TabItemBackgroundExtensionMarginConverter)} requires {kNumConverterElements} elements - element [0] was incorrect; [0] TabItem --- [1] TabItem.IsSelected --- [2] TabControl --- [3]: TabControl.BorderThickness --- [4] TabControl.TabStripPlacement");
                return new Thickness(0);
            }

            if (!(values[1] is bool isSelected))
            {
                Debug.Fail($"Error: {nameof(TabItemBackgroundExtensionMarginConverter)} requires {kNumConverterElements} elements - element [1] was incorrect; [0] TabItem --- [1] TabItem.IsSelected --- [2] TabControl --- [3]: TabControl.BorderThickness --- [4] TabControl.TabStripPlacement");
                return new Thickness(0);
            }

            if (!(values[2] is TabControl tabControl))
            {
                Debug.Fail($"Error: {nameof(TabItemBackgroundExtensionMarginConverter)} requires {kNumConverterElements} elements - element [2] was incorrect; [0] TabItem --- [1] TabItem.IsSelected --- [2] TabControl --- [3]: TabControl.BorderThickness --- [4] TabControl.TabStripPlacement");
                return new Thickness(0);
            }

            if (!(values[3] is Thickness tabControlBorderThickness))
            {
                Debug.Fail($"Error: {nameof(TabItemBackgroundExtensionMarginConverter)} requires {kNumConverterElements} elements - element [3] was incorrect; [0] TabItem --- [1] TabItem.IsSelected --- [2] TabControl --- [3]: TabControl.BorderThickness --- [4] TabControl.TabStripPlacement");
                return new Thickness(0);
            }

            if (!(values[4] is Dock tabStripPlacement))
            {
                Debug.Fail($"Error: {nameof(TabItemBackgroundExtensionMarginConverter)} requires {kNumConverterElements} elements - element [4] was incorrect; [0] TabItem --- [1] TabItem.IsSelected --- [2] TabControl --- [3]: TabControl.BorderThickness --- [4] TabControl.TabStripPlacement");
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
                        Bottom = isSelected ? -tabControlBorderThickness.Bottom : 0
                    };


                // Right headers are top justified
                case Dock.Right:
                    return new Thickness
                    {
                        Left = isSelected ? -tabControlBorderThickness.Left : 0,
                        Top = 0,
                        Right = 0,
                        Bottom = 0,
                    };

                // Bottom headers are left justified
                case Dock.Bottom:
                    return new Thickness
                    {
                        Left = 0,
                        Top = isSelected ? -tabControlBorderThickness.Top : 0,
                        Right = 0,
                        Bottom = 0,
                    };

                default:
                    throw new NonExistantEnumException<Dock>((int)tabControl.TabStripPlacement);
            }
        }
    }
}
