namespace AJutShowRoomAppKit.UI.Diagnostics
{
    using System;
    using System.Collections.ObjectModel;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using AJut;
    using AJut.Storage;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using AppKit;
    using CoreGraphics;

    // Whether a control is collected once it leaves its window and is dropped without Dispose: a PropertyGrid, a
    // FlatTreeListView, a PopupMenuButton, an EditableTextBlock mid edit, a spinning BusyWait overlay, an Svg, a
    // SimpleWrapPanel, a PathSelectionControl, a BumpStack, an AutoGrid, or a UIElementRepeater (and its container) whose
    // collection outlives it. Each probe shows a fresh control in this page's window with its rows on screen, takes it
    // out, drops it, and then collects with pauses between passes, so AppKit has let go of the views and any deferred work
    // has run before the last pass looks. An AppKit view that something it retains holds back is never collected, which is
    // what this catches. The WindowManager probe sets one up on this window with a tool window tracked, closes the tool
    // window, and disposes the manager, which then has to be collected.
    public sealed class LeakProbePage : ShowroomPage
    {
        private const double kProbeWidth = 320.0;
        private const double kProbeHeight = 200.0;

        private readonly NSView m_probeArea;
        private readonly NSButton m_probeGrid;
        private readonly NSButton m_probeTree;
        private readonly NSButton m_probePopupMenuButton;
        private readonly NSButton m_probeEditableTextBlock;
        private readonly NSButton m_probeBusyWait;
        private readonly NSButton m_probeSvg;
        private readonly NSButton m_probeWrapPanel;
        private readonly NSButton m_probePathSelection;
        private readonly NSButton m_probeBumpStack;
        private readonly NSButton m_probeWindowManager;
        private readonly NSButton m_probeAutoGrid;
        private readonly NSButton m_probeRepeater;

        // Outlives every repeater probed, as a view model's collection outlives its view
        private readonly ObservableCollection<NSView> m_repeatedViews = new ObservableCollection<NSView>
        {
            new NSView(),
            new NSView(),
            new NSView(),
        };

        private WeakReference m_probedRepeaterContainer;
        private readonly NSTextField m_result;
        private bool m_isProbing;

        public LeakProbePage ()
            : base("Leak Probe", "Shows a control in this window with its rows on screen, takes it out, drops it without Dispose, and says whether it was collected. Collected is the pass; still alive means something it holds leads back to it.")
        {
            m_probeGrid = new NSButton { Title = "Probe a PropertyGrid" };
            m_probeGrid.BezelStyle = NSBezelStyle.Rounded;
            m_probeGrid.Activated += this.OnProbeGridActivated;
            this.AddDemo(m_probeGrid, eFillStackCrossAlignment.Start);

            m_probeTree = new NSButton { Title = "Probe a FlatTreeListView" };
            m_probeTree.BezelStyle = NSBezelStyle.Rounded;
            m_probeTree.Activated += this.OnProbeTreeActivated;
            this.AddDemo(m_probeTree, eFillStackCrossAlignment.Start);

            m_probePopupMenuButton = new NSButton { Title = "Probe a PopupMenuButton" };
            m_probePopupMenuButton.BezelStyle = NSBezelStyle.Rounded;
            m_probePopupMenuButton.Activated += this.OnProbePopupMenuButtonActivated;
            this.AddDemo(m_probePopupMenuButton, eFillStackCrossAlignment.Start);

            m_probeEditableTextBlock = new NSButton { Title = "Probe an EditableTextBlock, mid edit" };
            m_probeEditableTextBlock.BezelStyle = NSBezelStyle.Rounded;
            m_probeEditableTextBlock.Activated += this.OnProbeEditableTextBlockActivated;
            this.AddDemo(m_probeEditableTextBlock, eFillStackCrossAlignment.Start);

            m_probeBusyWait = new NSButton { Title = "Probe a spinning BusyWait overlay" };
            m_probeBusyWait.BezelStyle = NSBezelStyle.Rounded;
            m_probeBusyWait.Activated += this.OnProbeBusyWaitActivated;
            this.AddDemo(m_probeBusyWait, eFillStackCrossAlignment.Start);

            m_probeSvg = new NSButton { Title = "Probe an Svg" };
            m_probeSvg.BezelStyle = NSBezelStyle.Rounded;
            m_probeSvg.Activated += this.OnProbeSvgActivated;
            this.AddDemo(m_probeSvg, eFillStackCrossAlignment.Start);

            m_probeWrapPanel = new NSButton { Title = "Probe a SimpleWrapPanel" };
            m_probeWrapPanel.BezelStyle = NSBezelStyle.Rounded;
            m_probeWrapPanel.Activated += this.OnProbeWrapPanelActivated;
            this.AddDemo(m_probeWrapPanel, eFillStackCrossAlignment.Start);

            m_probePathSelection = new NSButton { Title = "Probe a PathSelectionControl, with both buttons" };
            m_probePathSelection.BezelStyle = NSBezelStyle.Rounded;
            m_probePathSelection.Activated += this.OnProbePathSelectionActivated;
            this.AddDemo(m_probePathSelection, eFillStackCrossAlignment.Start);

            m_probeBumpStack = new NSButton { Title = "Probe a BumpStack, scrolling" };
            m_probeBumpStack.BezelStyle = NSBezelStyle.Rounded;
            m_probeBumpStack.Activated += this.OnProbeBumpStackActivated;
            this.AddDemo(m_probeBumpStack, eFillStackCrossAlignment.Start);

            m_probeWindowManager = new NSButton { Title = "Probe a WindowManager" };
            m_probeWindowManager.BezelStyle = NSBezelStyle.Rounded;
            m_probeWindowManager.Activated += this.OnProbeWindowManagerActivated;
            this.AddDemo(m_probeWindowManager, eFillStackCrossAlignment.Start);

            m_probeAutoGrid = new NSButton { Title = "Probe an AutoGrid" };
            m_probeAutoGrid.BezelStyle = NSBezelStyle.Rounded;
            m_probeAutoGrid.Activated += this.OnProbeAutoGridActivated;
            this.AddDemo(m_probeAutoGrid, eFillStackCrossAlignment.Start);

            m_probeRepeater = new NSButton { Title = "Probe a UIElementRepeater, its collection kept" };
            m_probeRepeater.BezelStyle = NSBezelStyle.Rounded;
            m_probeRepeater.Activated += this.OnProbeRepeaterActivated;
            this.AddDemo(m_probeRepeater, eFillStackCrossAlignment.Start);

            m_result = ShowroomStyle.Body("Nothing probed yet");
            this.AddDemo(m_result);

            // Where a probed control is shown while it is in the window
            m_probeArea = new NSView { TranslatesAutoresizingMaskIntoConstraints = false };
            m_probeArea.WidthAnchor.ConstraintEqualTo((nfloat)kProbeWidth).Active = true;
            m_probeArea.HeightAnchor.ConstraintEqualTo((nfloat)kProbeHeight).Active = true;
            this.AddDemo(m_probeArea, eFillStackCrossAlignment.Start);
        }

        protected override void OnTeardown ()
        {
            m_probeGrid.Activated -= this.OnProbeGridActivated;
            m_probeTree.Activated -= this.OnProbeTreeActivated;
            m_probePopupMenuButton.Activated -= this.OnProbePopupMenuButtonActivated;
            m_probeEditableTextBlock.Activated -= this.OnProbeEditableTextBlockActivated;
            m_probeBusyWait.Activated -= this.OnProbeBusyWaitActivated;
            m_probeSvg.Activated -= this.OnProbeSvgActivated;
            m_probeWrapPanel.Activated -= this.OnProbeWrapPanelActivated;
            m_probePathSelection.Activated -= this.OnProbePathSelectionActivated;
            m_probeBumpStack.Activated -= this.OnProbeBumpStackActivated;
            m_probeWindowManager.Activated -= this.OnProbeWindowManagerActivated;
            m_probeAutoGrid.Activated -= this.OnProbeAutoGridActivated;
            m_probeRepeater.Activated -= this.OnProbeRepeaterActivated;
        }

        // ===========[ Events ]================================================
        private void OnProbeGridActivated (object sender, EventArgs e)
        {
            _ = this.ProbeAsync("PropertyGrid", MakeGrid);
        }

        private void OnProbeTreeActivated (object sender, EventArgs e)
        {
            _ = this.ProbeAsync("FlatTreeListView", MakeTree);
        }

        private void OnProbePopupMenuButtonActivated (object sender, EventArgs e)
        {
            _ = this.ProbeAsync("PopupMenuButton", MakePopupMenuButton);
        }

        private void OnProbeEditableTextBlockActivated (object sender, EventArgs e)
        {
            _ = this.ProbeAsync("EditableTextBlock", MakeEditableTextBlock);
        }

        private void OnProbeBusyWaitActivated (object sender, EventArgs e)
        {
            _ = this.ProbeAsync("BusyWait", MakeBusyWait);
        }

        private void OnProbeSvgActivated (object sender, EventArgs e)
        {
            _ = this.ProbeAsync("Svg", MakeSvg);
        }

        private void OnProbeWrapPanelActivated (object sender, EventArgs e)
        {
            _ = this.ProbeAsync("SimpleWrapPanel", MakeWrapPanel);
        }

        private void OnProbePathSelectionActivated (object sender, EventArgs e)
        {
            _ = this.ProbeAsync("PathSelectionControl", MakePathSelectionControl);
        }

        private void OnProbeBumpStackActivated (object sender, EventArgs e)
        {
            _ = this.ProbeAsync("BumpStack", MakeBumpStack);
        }

        private void OnProbeWindowManagerActivated (object sender, EventArgs e)
        {
            if (!m_isProbing && (this.Window != null))
            {
                m_isProbing = true;
                _ = this.CollectAndReportAsync("WindowManager", this.SetUpThenDisposeWindowManager());
            }
        }

        private void OnProbeAutoGridActivated (object sender, EventArgs e)
        {
            _ = this.ProbeAsync("AutoGrid", MakeAutoGrid);
        }

        private void OnProbeRepeaterActivated (object sender, EventArgs e)
        {
            _ = this.ProbeRepeaterAsync();
        }

        // ===========[ Helper Methods ]========================================
        private async Task ProbeAsync (string name, Func<NSView> make)
        {
            if (m_isProbing)
            {
                return;
            }

            m_isProbing = true;
            await this.CollectAndReportAsync(name, this.ShowThenDrop(make));
        }

        private async Task CollectAndReportAsync (string name, WeakReference dropped)
        {
            const int kPasses = 4;
            const int kPauseMilliseconds = 150;

            m_result.StringValue = $"{name}: probing";
            for (int pass = 0; pass < kPasses; ++pass)
            {
                // A pause lets the run loop drain its autorelease pool and AppKit let go of the views
                await Task.Delay(kPauseMilliseconds);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            m_result.StringValue = dropped.IsAlive ? $"{name}: still alive" : $"{name}: collected";
            m_isProbing = false;
        }

        // Kept out of line, so no reference to the control is left behind in the caller while it collects
        [MethodImpl(MethodImplOptions.NoInlining)]
        private WeakReference ShowThenDrop (Func<NSView> make)
        {
            NSView probed = make();
            probed.Frame = m_probeArea.Bounds;
            m_probeArea.AddSubview(probed);
            probed.LayoutSubtreeIfNeeded();
            probed.Display();

            var dropped = new WeakReference(probed);
            probed.RemoveFromSuperview();
            return dropped;
        }

        private static NSView MakeGrid ()
        {
            var grid = new PropertyGrid();
            grid.Editors.RegisterStandardEditors();
            grid.SetSingleItemSource(new ProbeSample());
            return grid;
        }

        // The repeater follows the page's collection while it is in the window, and a collection still followed would
        //  hold the container (its handler reaches the container, not the repeater), so the container is the one to watch
        private async Task ProbeRepeaterAsync ()
        {
            await this.ProbeAsync("UIElementRepeater", this.MakeRepeater);
            bool isContainerAlive = m_probedRepeaterContainer?.IsAlive ?? false;
            m_result.StringValue += isContainerAlive ? ", its container still alive" : ", its container collected";
            m_probedRepeaterContainer = null;
        }

        private NSView MakeRepeater ()
        {
            var container = new AutoGrid { FixedColumnCount = 3 };
            m_probedRepeaterContainer = new WeakReference(container);
            return new UIElementRepeater { Container = container, DisplayChildren = m_repeatedViews };
        }

        // Resizers on both axes, so the view that draws and drags them, which holds the grid, is part of what is probed
        private static NSView MakeAutoGrid ()
        {
            var grid = new AutoGrid { FixedColumnCount = 2, AddColumnResizers = true, AddRowResizers = true };
            for (int index = 0; index < 4; ++index)
            {
                grid.AddSubview(new NSView());
            }

            return grid;
        }

        private static NSView MakeTree ()
        {
            return new FlatTreeListView
            {
                StartItemsExpanded = true,
                Root = new ProbeNode("Root", new ProbeNode("First", new ProbeNode("Inner")), new ProbeNode("Second")),
            };
        }

        private static NSView MakePopupMenuButton ()
        {
            var button = new PopupMenuButton { ButtonContent = "Probe" };
            var more = new PopupMenuItem("More");
            more.Items.Add(new PopupMenuItem("Inner"));
            button.MenuItems.Add(new PopupMenuItem("First"));
            button.MenuItems.Add(PopupMenuItem.CreateSeparator());
            button.MenuItems.Add(more);
            return button;
        }

        // Editing as it goes into the window, so the field has the focus and the window's field editor while it is shown,
        //  and leaving the window ends the edit
        private static NSView MakeEditableTextBlock ()
        {
            return new EditableTextBlock { Text = "Probe", IsEditing = true };
        }

        private static NSView MakeBusyWait ()
        {
            return new BusyWait { Text = "Probe", IsOverlay = true };
        }

        private static NSView MakeSvg ()
        {
            const string kProbeSvg = @"<svg width=""24"" height=""24"" viewBox=""0 0 24 24"" xmlns=""http://www.w3.org/2000/svg""><g fill=""#FFC107"" stroke=""#E65100""><circle cx=""12"" cy=""12"" r=""10""/></g></svg>";
            return new Svg { Source = SvgParsing.ParseText(kProbeSvg), Stretch = ePathStretch.Uniform };
        }

        // Laid out by the probe at its width, so it wraps
        private static NSView MakeWrapPanel ()
        {
            var panel = new SimpleWrapPanel();
            foreach (string text in new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight" })
            {
                panel.AddSubview(new NSButton { Title = text, BezelStyle = NSBezelStyle.Rounded });
            }

            return panel;
        }

        // A folder that is there, so the field, the show in Finder button and the browse button are all up
        private static NSView MakePathSelectionControl ()
        {
            return new PathSelectionControl
            {
                PathType = ePathType.Folder,
                IsOpenInExplorerButtonAllowed = true,
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            };
        }

        // More children than fit, so the bump buttons show and the content has scrolled. It is laid out at the probe area's
        //  size first, since a stack with no size has nowhere to scroll.
        private static NSView MakeBumpStack ()
        {
            const double kScrolledOffset = 40.0;

            var stack = new BumpStack { Frame = new CGRect(0, 0, (nfloat)kProbeWidth, (nfloat)kProbeHeight) };
            foreach (string text in new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten" })
            {
                stack.Children.Add(new NSButton { Title = text, BezelStyle = NSBezelStyle.Rounded });
            }

            stack.LayoutSubtreeIfNeeded();
            stack.ScrollBy(kScrolledOffset);
            return stack;
        }

        // Kept out of line, so no reference to the manager is left behind in the caller while it collects. The tool
        //  window closes before the manager is disposed, the way an app's tool windows go before its root.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private WeakReference SetUpThenDisposeWindowManager ()
        {
            const double kToolWindowSize = 160.0;

            var manager = new WindowManager(this.Window);
            var toolWindow = new ThemedWindow(new CGRect(0, 0, (nfloat)kToolWindowSize, (nfloat)kToolWindowSize)) { Title = "Probe" };
            toolWindow.ReleaseWhenClosed(false);
            manager.Track(toolWindow);
            toolWindow.Close();
            manager.Dispose();
            return new WeakReference(manager);
        }

        // ===========[ Subclasses/structs ]====================================
        // What the probed grid shows: a few kinds of row
        private sealed class ProbeSample : NotifyPropertyChanged
        {
            private string m_name = "Probe";
            private bool m_isOn = true;
            private int m_count = 3;

            public string Name
            {
                get => m_name;
                set => this.SetAndRaiseIfChanged(ref m_name, value);
            }

            public bool IsOn
            {
                get => m_isOn;
                set => this.SetAndRaiseIfChanged(ref m_isOn, value);
            }

            public int Count
            {
                get => m_count;
                set => this.SetAndRaiseIfChanged(ref m_count, value);
            }
        }

        // A named node for the probed tree
        private sealed class ProbeNode : ObservableTreeNode<ProbeNode>
        {
            public ProbeNode (string name, params ProbeNode[] children)
            {
                this.Name = name;
                this.CanHaveChildren = children.Length > 0;
                foreach (ProbeNode child in children)
                {
                    this.InsertChild(this.Children.Count, child);
                }
            }

            public string Name { get; }

            public override string ToString () => this.Name;
        }
    }
}
