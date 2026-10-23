namespace AJut.UX.CodeBinding
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Turns a target's edited value back into a source value, or refuses to. Returning false writes nothing to the
    /// source, which is how a two-way binding rejects text that does not parse, say, without throwing.
    /// </summary>
    public delegate bool TryConvertBack<TTarget, TSource> (TTarget target, out TSource source);

    /// <summary>
    /// The conversion between a source property and the target it is bound to: what a XAML converter does, for the
    /// toolkits that have no XAML. Convert runs on every push to the target; ConvertBack runs on every user edit and
    /// may refuse it.
    /// </summary>
    public sealed class BindingConversion<TSource, TTarget>
    {
        public BindingConversion (Func<TSource, TTarget> convert, TryConvertBack<TTarget, TSource>? convertBack = null)
        {
            this.Convert = convert;
            this.ConvertBack = convertBack;
        }

        /// <summary>Source value to the value the target shows.</summary>
        public Func<TSource, TTarget> Convert { get; }

        /// <summary>
        /// The target's edited value back to a source value. Null makes the binding one-way in practice: the target
        /// still shows the source, and its edits are not written back.
        /// </summary>
        public TryConvertBack<TTarget, TSource>? ConvertBack { get; }

        /// <summary>
        /// Decides whether the target already shows a value, in which case a push leaves the target alone (so an echo
        /// of the user's own edit does not move a caret or restart an animation). Defaults to
        /// <see cref="EqualityComparer{T}.Default"/>; see <see cref="BindingComparers.Tolerance"/> for doubles.
        /// </summary>
        public IEqualityComparer<TTarget> TargetComparer { get; init; } = EqualityComparer<TTarget>.Default;
    }
}
