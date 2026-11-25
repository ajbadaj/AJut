namespace AJut.UX.Controls
{
    /// <summary>What a dot in a PageStack's dot strip stands for</summary>
    public enum ePageStackDotKind
    {
        /// <summary>A page inside the dot range, other than the current one</summary>
        Page,

        /// <summary>The page the stack is showing</summary>
        Current,

        /// <summary>The smaller, faded dot before the range: there are more pages back this way</summary>
        BackEdge,

        /// <summary>The smaller, faded dot after the range: there are more pages forward this way</summary>
        ForwardEdge,
    }
}
