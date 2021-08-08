namespace AJut.Application.SinglePageDisplay
{
    /// <summary>
    /// The interface a control needs to implement in order to be treated as a page in a SinglePageDisplay
    /// </summary>
    public interface IPageDisplayControl
    {
        void Setup (PageAdapterModel adapter);
        void SetState (object state) { }
        object GenerateState () => null;
    }
}
