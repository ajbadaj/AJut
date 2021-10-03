namespace AJut.Application.StackNav
{
    /// <summary>
    /// The interface a control needs to implement in order to be treated as a page in a SinglePageDisplay
    /// </summary>
    public interface IStackNavDisplayControl
    {
        void Setup (StackNavAdapter adapter);
        void SetState (object state) { }
        object GenerateState () => null;
    }
}
