namespace AJutShowRoomAppKit.UI.Pages
{
    using System;
    using AJut.UX.Controls;
    using AppKit;

    // One area: its own page list on the left (title, blurb, the pages, then what the other
    // stacks have here), and the selected page on the right. The same nesting as the WinUI3
    // showroom's area pages.
    public sealed class AreaView : FillStack, IShowroomPage
    {
        private readonly ShowroomArea m_area;
        private readonly NavColumn m_nav;
        private readonly PageHost m_pageHost;

        public AreaView (ShowroomArea area)
            : base(NSUserInterfaceLayoutOrientation.Horizontal)
        {
            m_area = area;

            m_nav = new NavColumn();
            m_nav.AddHeader(ShowroomStyle.Heading(area.Title));
            m_nav.AddHeader(ShowroomStyle.Understated(area.Blurb));
            foreach (ShowroomPageEntry page in area.Pages)
            {
                m_nav.AddItem(page.Title);
            }

            if (area.Pages.Count == 0)
            {
                m_nav.AddHeader(ShowroomStyle.Understated("Nothing here on AppKit yet."));
            }

            m_nav.AddFooter(ShowroomStyle.Understated(area.OtherStacksNote));
            m_nav.SelectionChanged += this.OnNavSelectionChanged;

            m_pageHost = new PageHost();
            this.Add(m_nav);
            this.Add(ShowroomStyle.VerticalDivider());
            this.Add(m_pageHost, grows: true);

            m_nav.Select(0);
        }

        public void Teardown ()
        {
            m_nav.SelectionChanged -= this.OnNavSelectionChanged;
            m_nav.Teardown();
            m_pageHost.Clear();
        }

        private void OnNavSelectionChanged (object sender, EventArgs e)
        {
            m_pageHost.Show(m_area.Pages[m_nav.SelectedIndex].Create());
        }
    }
}
