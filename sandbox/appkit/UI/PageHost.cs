namespace AJutShowRoomAppKit.UI
{
    using AppKit;

    /// <summary>A page that holds subscriptions or other hooks, and has to let go of them when it is navigated away from</summary>
    public interface IShowroomPage
    {
        void Teardown ();
    }

    // Shows one page at a time, filling its bounds. The page being replaced is torn down if it
    // has anything to tear down.
    public sealed class PageHost : NSView
    {
        private NSView m_current;

        public override bool IsFlipped => true;

        public NSView Current => m_current;

        public void Show (NSView page)
        {
            if (m_current != null)
            {
                (m_current as IShowroomPage)?.Teardown();
                m_current.RemoveFromSuperview();
            }

            m_current = page;
            if (page == null)
            {
                return;
            }

            page.TranslatesAutoresizingMaskIntoConstraints = false;
            this.AddSubview(page);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                page.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                page.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                page.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                page.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
            });
        }

        public void Clear () => this.Show(null);
    }
}
