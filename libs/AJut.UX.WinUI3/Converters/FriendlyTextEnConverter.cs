namespace AJut.UX.Converters
{
    public class FriendlyTextEnConverter : SimpleValueConverter<object, string>
    {
        protected override string Convert (object value) => value?.ToString().ConvertToFriendlyEn() ?? string.Empty;
    }
}