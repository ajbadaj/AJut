namespace AJut.Application.Converters
{
    using AJut;
    public class IsTextNullOrEmptyConvter : SimpleValueConverter<string, bool>
    {
        protected override bool Convert (string value) => value.IsNullOrEmpty();
    }
}
