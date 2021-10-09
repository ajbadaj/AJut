namespace AJut.UX
{
    /// <summary>
    /// The display used in the drawer for a particular <see cref="IStackNavDisplayControl"/>, set in it's <see cref="StackNavAdapter"/>.
    /// </summary>
    public interface IStackNavDrawerDisplay
    {
        /// <summary>
        /// The only required property for a drawer, this gives the title displayed in the drawer's heading
        /// </summary>
        string Title { get; }
    }

    /// <summary>
    /// A <see cref="IStackNavDrawerDisplay"/> drawer that needs awareness of the navigational state or take navigational action via the <see cref="StackNavFlowController"/>.
    /// </summary>
    public interface IStackNavFlowControllerReactiveDrawerDisplay : IStackNavDrawerDisplay
    {
        void Setup (StackNavFlowController pageManager);
    }
}
