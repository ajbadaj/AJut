namespace AJut.Application.StackNav.Model
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
