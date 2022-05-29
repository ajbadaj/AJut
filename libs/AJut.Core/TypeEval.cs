namespace AJut
{
    using System;

    /// <summary>
    /// Utility class for storing and evaluating type equivelence expectations and preferences
    /// </summary>
    public class TypeEval
    {
        /// <summary>
        /// The type we're testing against
        /// </summary>
        public Type SourceType { get; set; }

        /// <summary>
        /// Do we allow base types of the <see cref="SourceType"/>
        /// </summary>
        public bool AllowBaseTypes { get; set; }

        /// <summary>
        /// Construct a type eval
        /// </summary>
        public TypeEval (Type type)
        {
            SourceType = type;
            AllowBaseTypes = false;
        }

        /// <summary>
        /// Construct a type eval
        /// </summary>
        public TypeEval (Type type, bool allowsAncestors)
        {
            SourceType = type;
            AllowBaseTypes = allowsAncestors;
        }

        /// <summary>
        /// Evaluate a type to see if it matches criteria set by this evaluator
        /// </summary>
        public bool Evaluate (Type other)
        {
            return this.AllowBaseTypes ? this.SourceType.IsAssignableFrom(other) : this.SourceType == other;
        }
    }
}
