namespace AJut.UX.CodeBinding
{
    using System;
    using System.Collections.Generic;

    /// <summary>Ready made comparers for <see cref="BindingConversion{TSource, TTarget}.TargetComparer"/>.</summary>
    public static class BindingComparers
    {
        /// <summary>
        /// Treats two doubles within <paramref name="epsilon"/> of each other as equal, for targets that hand back a
        /// value a hair off the one they were given (a slider snapping to its own step math, a stepper's float
        /// round trip). It is opt in rather than the default on purpose: a default tolerance would silently swallow a
        /// small change made to a model double from code.
        /// </summary>
        public static IEqualityComparer<double> Tolerance (double epsilon) => new ToleranceComparer(epsilon);

        // ===========[ Subclasses/structs ]===================================
        // Equals is all a binding asks of it. A tolerance cannot give a hash that agrees with Equals (a ~ b and
        // b ~ c do not make a ~ c), so it hashes everything the same, which keeps it correct, if slow, in a set.
        private sealed class ToleranceComparer : IEqualityComparer<double>
        {
            private readonly double m_epsilon;

            public ToleranceComparer (double epsilon)
            {
                m_epsilon = epsilon;
            }

            // Equal values first: NaN and NaN, or infinity and itself, are the same value but their difference is NaN
            public bool Equals (double x, double y) => x.Equals(y) || (Math.Abs(x - y) <= m_epsilon);

            public int GetHashCode (double obj) => 0;
        }
    }
}
