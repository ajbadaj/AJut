namespace AJut.UX.Converters
{
    using System;
    using AJut.UX.PropertyInteraction;

    /// <summary>
    /// Maps an <see cref="ePropertyToolTipPlacement"/> to a bool suitable for ToolTipService.IsEnabled.
    /// Pass ConverterParameter "Value" to test the value editor, anything else tests the property name.
    /// </summary>
    public class PropertyToolTipPlacementToBoolConverter : SimpleValueConverter<ePropertyToolTipPlacement, bool>
    {
        protected override bool Convert (ePropertyToolTipPlacement value, object parameter)
        {
            bool forValue = parameter is string text && text.Equals("Value", StringComparison.OrdinalIgnoreCase);
            if (forValue)
            {
                return value == ePropertyToolTipPlacement.ValueOnly
                    || value == ePropertyToolTipPlacement.PropertyNameAndValue;
            }

            return value == ePropertyToolTipPlacement.PropertyNameOnly
                || value == ePropertyToolTipPlacement.PropertyNameAndValue;
        }
    }
}
