namespace AJut.UX.Converters
{
    /// <summary>The operation an arithmetic converter applies, see <see cref="NumericConverterMath.Apply"/></summary>
    public enum eArithmeticOperation
    {
        /// <summary>The value plus the operand</summary>
        Add,

        /// <summary>The value minus the operand</summary>
        Subtract,

        /// <summary>The value times the operand</summary>
        Multiply,

        /// <summary>The value divided by the operand, or the value as it was when the operand is 0</summary>
        Divide,
    }
}
