namespace AJutShowRoomAppKit.UI.Utilities
{
    using System;
    using System.Collections.Specialized;
    using System.Linq;
    using AJut.UX;
    using AJut.UX.Controls;
    using AppKit;
    using CoreGraphics;

    // A WindowManager built around the showroom's window while this page is up, with tool windows it opens and tracks.
    // The strip picks how the tool windows show and hide with the showroom window, and the readout keeps the activation
    // order, front to back. Leaving the page closes the tool windows and disposes the manager.
    public sealed class WindowManagerPage : ShowroomPage
    {
        private const double kToolWindowWidth = 260.0;
        private const double kToolWindowHeight = 120.0;
        private const double kToolWindowStagger = 28.0;

        private readonly EnumToggleStrip m_coordination;
        private readonly BrushButton m_openToolWindow;
        private readonly BrushButton m_hideToolWindows;
        private readonly BrushButton m_showToolWindows;
        private readonly BrushButton m_closeToolWindows;
        private readonly NSTextField m_order = ShowroomStyle.Understated(String.Empty);
        private WindowManager m_manager;
        private int m_toolWindowCount;

        public WindowManagerPage ()
            : base("WindowManager", "Tracks a root window and its child windows: the order they were last active in, front to back, and whether the children show and hide with the root. The other stacks' showrooms run theirs behind the scenes; here it is out in the open, built around this window while the page is up.")
        {
            m_coordination = new EnumToggleStrip { SelectedItem = eChildWindowDisplayCoordinationStyle.Manually };
            m_coordination.SelectedItemChanged += this.OnCoordinationChanged;
            this.AddLabeledDemo("ShowHideChildrenWhen", m_coordination);
            this.AddText("RootActivationDeactivation: switch to another app and the tool windows go; come back to this window and they return. RootMinimizationRestoration: minimize this window to the Dock and they go with it, restore it and they return.");

            m_openToolWindow = MakeButton("Open a tool window");
            m_openToolWindow.Activated += this.OnOpenToolWindowActivated;
            this.AddDemo(m_openToolWindow, eFillStackCrossAlignment.Start);

            m_hideToolWindows = MakeButton("Hide all tool windows");
            m_hideToolWindows.Activated += this.OnHideToolWindowsActivated;
            this.AddDemo(m_hideToolWindows, eFillStackCrossAlignment.Start);

            m_showToolWindows = MakeButton("Show all tool windows");
            m_showToolWindows.Activated += this.OnShowToolWindowsActivated;
            this.AddDemo(m_showToolWindows, eFillStackCrossAlignment.Start);

            m_closeToolWindows = MakeButton("Close all tool windows");
            m_closeToolWindows.Activated += this.OnCloseToolWindowsActivated;
            this.AddDemo(m_closeToolWindows, eFillStackCrossAlignment.Start);

            this.AddLabeledDemo("Front to back", m_order);
            this.RefreshOrder();

            this.AddCode(
@"var windows = new WindowManager(mainWindow)
{
    ShowHideChildrenWhen = eChildWindowDisplayCoordinationStyle.RootActivationDeactivation,
};
windows.Track(toolWindow);   // shows it, brings it to the front, and gives it the root's appearance
windows.ForEach(false, w => ...); // the children, most recently active first
windows.Dispose();           // when the app is done with it"
            );
        }

        protected override void OnTeardown ()
        {
            m_coordination.SelectedItemChanged -= this.OnCoordinationChanged;
            m_openToolWindow.Activated -= this.OnOpenToolWindowActivated;
            m_hideToolWindows.Activated -= this.OnHideToolWindowsActivated;
            m_showToolWindows.Activated -= this.OnShowToolWindowsActivated;
            m_closeToolWindows.Activated -= this.OnCloseToolWindowsActivated;
            this.ReleaseManager();
        }

        // ===========[ Events ]================================================
        private void OnCoordinationChanged (object sender, EventArgs e)
        {
            if ((m_manager != null) && (m_coordination.SelectedItem is eChildWindowDisplayCoordinationStyle style))
            {
                m_manager.ShowHideChildrenWhen = style;
            }
        }

        private void OnOpenToolWindowActivated (object sender, EventArgs e)
        {
            WindowManager manager = this.EnsureManager();
            if (manager == null)
            {
                return;
            }

            ++m_toolWindowCount;
            var toolWindow = new ThemedWindow(new CGRect(0, 0, (nfloat)kToolWindowWidth, (nfloat)kToolWindowHeight))
            {
                Title = $"Tool window {m_toolWindowCount}",
            };

            // A window made from C# is owned by its managed object; letting AppKit release it on close as well would free
            //  it twice
            toolWindow.ReleaseWhenClosed(false);
            toolWindow.WindowContents = ShowroomStyle.Body($"Tool window {m_toolWindowCount}. Click between it and the showroom: the order below follows.");

            // Staggered down and right from the showroom window's top left, so each new one is easy to see
            CGRect root = manager.Root.Frame;
            double offset = kToolWindowStagger * m_toolWindowCount;
            toolWindow.SetFrameTopLeftPoint(new CGPoint(root.X + (nfloat)offset, root.Bottom - (nfloat)offset));
            manager.Track(toolWindow);
        }

        private void OnHideToolWindowsActivated (object sender, EventArgs e) => m_manager?.HideAllWindows();
        private void OnShowToolWindowsActivated (object sender, EventArgs e) => m_manager?.ShowAllWindows();
        private void OnCloseToolWindowsActivated (object sender, EventArgs e) => m_manager?.CloseAllChildWindows();

        private void OnOrderChanged (object sender, NotifyCollectionChangedEventArgs e) => this.RefreshOrder();

        // ===========[ Helper Methods ]========================================
        // Built on first use rather than up front, since the page has no window until it is shown
        private WindowManager EnsureManager ()
        {
            if ((m_manager == null) && (this.Window != null))
            {
                m_manager = new WindowManager(this.Window);
                if (m_coordination.SelectedItem is eChildWindowDisplayCoordinationStyle style)
                {
                    m_manager.ShowHideChildrenWhen = style;
                }

                ((INotifyCollectionChanged)m_manager).CollectionChanged += this.OnOrderChanged;
                this.RefreshOrder();
            }

            return m_manager;
        }

        private void ReleaseManager ()
        {
            if (m_manager == null)
            {
                return;
            }

            ((INotifyCollectionChanged)m_manager).CollectionChanged -= this.OnOrderChanged;
            m_manager.CloseAllChildWindows();
            m_manager.Dispose();
            m_manager = null;
        }

        private void RefreshOrder ()
        {
            m_order.StringValue = (m_manager == null)
                ? "(no tool windows yet)"
                : String.Join(", ", m_manager.Select(this.NameOf));
        }

        private string NameOf (NSWindow window) => (window == m_manager?.Root) ? "Showroom" : window.Title;

        private static BrushButton MakeButton (string title) => new BrushButton { Title = title };
    }
}
