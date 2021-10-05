namespace AJut.UX.Converters
{
    using System.Windows;

    // Nullness
    public class NumericComparisonConverter<TOutput> : SimpleValueConverter
    {
        public eComparison Comp { get; set; }
        public double To { get; set; }

        public TOutput CompTrueValue { get; set; }
        public TOutput CompFalseValue { get; set; }

        public NumericComparisonConverter (TOutput compTrueDefault, TOutput compFalseDefault)
        {
            this.CompTrueValue = compTrueDefault;
            this.CompFalseValue = compFalseDefault;
        }

        protected override object Convert (object objValue)
        {
            dynamic sourceValue = (dynamic)objValue;
            dynamic toValue = (dynamic)System.Convert.ChangeType(this.To, objValue.GetType());
            switch (this.Comp)
            {
                case eComparison.NotEqual: return sourceValue != toValue;
                case eComparison.Equal: return sourceValue == toValue;
                case eComparison.GreaterThan: return sourceValue > toValue;
                case eComparison.LessThan: return sourceValue < toValue;
                case eComparison.GreaterThanOrEqual: return sourceValue >= toValue;
                case eComparison.LessThanOrEqual: return sourceValue <= toValue;
            }

            return false;
        }

        public enum eComparison
        {
            NotEqual,
            Equal,
            GreaterThan,
            LessThan,
            GreaterThanOrEqual,
            LessThanOrEqual,
        }
    }

    public class NumericComparisonToBoolConverter : NumericComparisonConverter<bool>
    {
        public NumericComparisonToBoolConverter () : base(true, false) { }
    }

    public class NumericComparisonToVisibilityConverter : NumericComparisonConverter<Visibility>
    {
        public NumericComparisonToVisibilityConverter () : base(Visibility.Visible, Visibility.Collapsed) { }
    }
}