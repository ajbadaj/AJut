namespace AJut.Application.StackNav
{
    public interface IDrawerDisplay
    {
        string Title { get; }
    }

    public interface IManagerReactiveDrawerDisplay : IDrawerDisplay
    {
        void Setup (StackNavOperationsManager pageManager);
    }
}
