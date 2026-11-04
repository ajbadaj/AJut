namespace AJut.UX.Controls
{
    /// <summary>
    /// The container a <see cref="RepeaterChildSync{TChild}"/> places its children in, as each toolkit puts a child view in
    /// a container view. The repeated children lead the container's children, so index 0 is the container's first child.
    /// </summary>
    public interface IRepeaterChildHost<TChild> where TChild : class
    {
        /// <summary>Puts <paramref name="child"/>, which is not in the container, at <paramref name="index"/> among its children</summary>
        void InsertChild (int index, TChild child);

        /// <summary>Takes <paramref name="child"/>, which is in the container, out of it</summary>
        void RemoveChild (TChild child);
    }
}
