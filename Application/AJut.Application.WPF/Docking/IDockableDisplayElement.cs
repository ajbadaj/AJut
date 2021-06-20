namespace AJut.Application.Docking
{
    using AJut.TypeManagement;

    public interface IDockableDisplayElement
    {
        DockingContentAdapterModel DockingAdapter { get; }
        void Setup (DockingContentAdapterModel adapter);

        void ApplyState (object state) { }
        object GenerateState () => TypeIdRegistrar.GetTypeIdFor(this.GetType()) ?? this.GetType().FullName;
    }

}
