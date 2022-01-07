namespace AJut.UX.Docking
{
    using AJut.TypeManagement;

    public interface IDockableDisplayElement
    {
        DockingContentAdapterModel DockingAdapter { get; }
        void Setup (DockingContentAdapterModel adapter);

        void ApplyState (object state) { }
        object GenerateState () => null;
    }

}
