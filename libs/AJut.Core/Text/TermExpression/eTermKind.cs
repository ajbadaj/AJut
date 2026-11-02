namespace AJut.Text
{
    /// <summary>
    /// What a <see cref="TermValue"/> is. A list holds one kind, and lists don't nest. Whoever types a formula never writes
    /// one of these: the host declares the kind of every value it supplies (<see cref="TermDeclarations"/>) and the kind it
    /// wants back, and the parse works out the rest.
    /// </summary>
    public enum eTermKind : byte
    {
        /// <summary>
        /// A whole number, held exactly up to 2^53 either way. An int is accepted anywhere a float is wanted.
        /// </summary>
        Int,

        /// <summary>
        /// A number that can have a fraction (a double)
        /// </summary>
        Float,

        /// <summary>
        /// True or false
        /// </summary>
        Bool,

        /// <summary>
        /// Text
        /// </summary>
        String,

        /// <summary>
        /// A list of whole numbers, like a set of scores
        /// </summary>
        IntList,

        /// <summary>
        /// A list of numbers that can have fractions
        /// </summary>
        FloatList,

        /// <summary>
        /// A list of true or false values, like a comparison applied to every item of a list
        /// </summary>
        BoolList,

        /// <summary>
        /// A list of texts
        /// </summary>
        StringList,
    }
}
