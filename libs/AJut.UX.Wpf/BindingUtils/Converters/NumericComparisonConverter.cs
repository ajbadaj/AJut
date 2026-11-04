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
            if (!NumericConverterMath.TryRelate(objValue, this.To, out NumericConverterMath.Relation relation))
            {
                return this.CompFalseValue;
            }

            switch (this.Comp)
            {
                case eComparison.NotEqual: return !relation.IsEqual ? this.CompTrueValue : this.CompFalseValue;
                case eComparison.Equal: return relation.IsEqual ? this.CompTrueValue : this.CompFalseValue;
                case eComparison.GreaterThan: return relation.IsGreater ? this.CompTrueValue : this.CompFalseValue;
                case eComparison.LessThan: return relation.IsLess ? this.CompTrueValue : this.CompFalseValue;
                case eComparison.GreaterThanOrEqual: return (relation.IsGreater || relation.IsEqual) ? this.CompTrueValue : this.CompFalseValue;
                case eComparison.LessThanOrEqual: return (relation.IsLess || relation.IsEqual) ? this.CompTrueValue : this.CompFalseValue;
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