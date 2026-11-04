namespace AJutShowRoomAndroid
{
    using AJut.UX.CodeBinding;
    using Android.Views;

    /// <summary>
    /// One page of the showroom. A page names its layout, and when it has wiring to do (a model to bind, a list
    /// to fill) does it in <see cref="Bind"/>. Everything bound through the binder it is handed is undone when the
    /// page is left; anything else it hooks it undoes in <see cref="Unbind"/>.
    /// </summary>
    public abstract class ShowroomPage
    {
        // ===========[ Properties ]===================================
        public abstract string Title { get; }

        public abstract int LayoutId { get; }

        /// <summary>The raw resource holding this page's own layout, shown under it as its markup, or 0 for none.</summary>
        public virtual int MarkupSourceId => 0;

        // ===========[ Public Interface Methods ]===================================
        public virtual void Bind (View root, MainActivity host, BindingSet binder)
        {
        }

        public virtual void Unbind ()
        {
        }
    }

    /// <summary>A demo page that is all layout: its markup is the whole demo.</summary>
    public sealed class DemoPage : ShowroomPage
    {
        private readonly string m_title;
        private readonly int m_layoutId;
        private readonly int m_markupSourceId;

        public DemoPage (string title, int layoutId, int markupSourceId)
        {
            m_title = title;
            m_layoutId = layoutId;
            m_markupSourceId = markupSourceId;
        }

        public override string Title => m_title;

        public override int LayoutId => m_layoutId;

        public override int MarkupSourceId => m_markupSourceId;
    }
}
