namespace AJut.UX.Converters
{
    using System.Windows;
    using AJut.UX.Converters;

    public class ThicknessFromNumberConverter : SimpleValueConverter<object, Thickness>
    {
        public Thickness MultiplyMask { get; set; }

        protected override Thickness Convert (object value)
        {
            double target = (double)System.Convert.ChangeType(value, typeof(double));
            return new Thickness
            {
                Left = this.MultiplyMask.Left * target,
                Top = this.MultiplyMask.Top * target,
                Right = this.MultiplyMask.Right * target,
                Bottom = this.MultiplyMask.Bottom * target,
            };
        }
    }
}
