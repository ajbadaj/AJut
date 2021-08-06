namespace AJut.Application.SinglePageApp
{
    public interface IDrawerDisplay
    {
        string Title { get; }
    }

    public interface IManagerReactiveDrawerDisplay : IDrawerDisplay
    {
        void Setup (PageManager pageManager);
    }
}
