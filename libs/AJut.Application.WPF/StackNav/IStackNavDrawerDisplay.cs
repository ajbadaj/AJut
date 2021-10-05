namespace AJut.Application
{
    public interface IStackNavDrawerDisplay
    {
        string Title { get; }
    }

    public interface IStackNavFlowControllerReactiveDrawerDisplay : IStackNavDrawerDisplay
    {
        void Setup (StackNavFlowController pageManager);
    }
}
