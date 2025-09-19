namespace AJut.UX.Converters
{
    using System.Collections;
    using System.Linq;

    public class ContainsConverter : SimpleValueConverter<IEnumerable, bool>
    {
        public object Target { get; set; }
        protected override bool Convert (IEnumerable value) => value.OfType<object>().Any(o => o == this.Target);
    }
}