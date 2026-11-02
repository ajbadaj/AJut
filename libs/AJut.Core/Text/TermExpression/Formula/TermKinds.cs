namespace AJut.Text
{
    /// <summary>
    /// What every part of the formula engine needs to know about eTermKind. A list kind sits a fixed distance past
    /// the kind of its items, which is what the arithmetic here relies on.
    /// </summary>
    internal static class TermKinds
    {
        private const int kListOffset = (int)eTermKind.IntList - (int)eTermKind.Int;

        public static bool IsList (eTermKind kind) => kind >= eTermKind.IntList;

        public static eTermKind ItemOf (eTermKind kind) => IsList(kind) ? (eTermKind)((int)kind - kListOffset) : kind;

        public static eTermKind ListOf (eTermKind itemKind) => (eTermKind)((int)ItemOf(itemKind) + kListOffset);

        public static eTermKind WithListness (eTermKind itemKind, bool isList) => isList ? ListOf(itemKind) : ItemOf(itemKind);

        public static bool IsNumber (eTermKind kind) => ItemOf(kind) is eTermKind.Int or eTermKind.Float;

        public static bool IsKnown (eTermKind kind) => kind is >= eTermKind.Int and <= eTermKind.StringList;

        /// <summary>
        /// Whether a value of kind actual can go where wanted is asked for: the same kind, or an int where a float is
        /// wanted
        /// </summary>
        public static bool Accepts (eTermKind wanted, eTermKind actual)
        {
            if (wanted == actual)
            {
                return true;
            }

            return (IsList(wanted) == IsList(actual))
                && (ItemOf(wanted) == eTermKind.Float)
                && (ItemOf(actual) == eTermKind.Int);
        }

        /// <summary>
        /// The kind two values make together where either can stand for the other: the same kind, or an int and a
        /// float making a float. Anything else has no common kind.
        /// </summary>
        public static bool TryUnify (eTermKind left, eTermKind right, out eTermKind unified)
        {
            if (Accepts(left, right))
            {
                unified = left;
                return true;
            }

            if (Accepts(right, left))
            {
                unified = right;
                return true;
            }

            unified = left;
            return false;
        }

        /// <summary>
        /// In the words of whoever types formulas, who never sees Int or String. "is a whole number", "is a list of
        /// text".
        /// </summary>
        public static string Describe (eTermKind kind)
        {
            return kind switch
            {
                eTermKind.Int => "a whole number",
                eTermKind.Float => "a number",
                eTermKind.Bool => "true or false",
                eTermKind.String => "text",
                eTermKind.IntList => "a list of whole numbers",
                eTermKind.FloatList => "a list of numbers",
                eTermKind.BoolList => "a list of true or false values",
                eTermKind.StringList => "a list of text",
                _ => "unknown",
            };
        }

        /// <summary>
        /// For a single value where a list is wanted: "a single whole number"
        /// </summary>
        public static string DescribeSingle (eTermKind kind)
        {
            return kind switch
            {
                eTermKind.Int => "a single whole number",
                eTermKind.Float => "a single number",
                eTermKind.Bool => "a single true or false value",
                eTermKind.String => "a single piece of text",
                _ => Describe(kind),
            };
        }
    }
}
