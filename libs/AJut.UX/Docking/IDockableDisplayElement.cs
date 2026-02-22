namespace AJut.UX.Docking
{
    public interface IDockableDisplayElement
    {
        DockingContentAdapterModel DockingAdapter { get; }
        void Setup (DockingContentAdapterModel adapter);

        void ApplyState (object state) { }
        object GenerateState () => null;
    }
}
