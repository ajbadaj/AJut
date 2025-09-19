namespace AJut.UX.Converters
{
    using AJut;
    using Microsoft.UI.Xaml;

    public class IsTextNullOrEmptyConverter : SimpleValueConverter<string, bool>
    {
        protected override bool Convert (string value) => value.IsNullOrEmpty();
    }

    public class IsTextNullOrEmptyToVisibilityConverter : SimpleValueConverter<string, Visibility>
    {
        public Visibility WhenHasText { get; set; } = Visibility.Visible;
        public Visibility WhenNullOrEmpty { get; set; } = Visibility.Collapsed;
        protected override Visibility Convert (string value) => value.IsNullOrEmpty() ? this.WhenHasText : this.WhenNullOrEmpty;
    }
}
