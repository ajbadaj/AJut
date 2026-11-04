namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;

    /// <summary>
    /// What a toolkit's panel add/remove toolbar gives the <see cref="DockPanelToolbarController"/>: somewhere to show
    /// the buttons, and a way to refresh their checked state
    /// </summary>
    public interface IDockPanelToolbarHost
    {
        /// <summary>Replaces the toolbar's buttons with one per entry in <paramref name="buttons"/>, in order</summary>
        void ShowButtons (IReadOnlyList<DockPanelToolbarController.Button> buttons);

        /// <summary>Re-reads every toggle button's checked state from its <see cref="DockPanelToolbarController.Button.IsChecked"/></summary>
        void RefreshButtonStates ();
    }

    // ===========[ DockPanelToolbarController ]=================================
    // Everything a panel add/remove toolbar does that never touches a view, shared by every toolkit's
    // toolbar: following the docking manager's UISyncVM, working out which panel types get a button
    // (a show/hide toggle for a single instance type, an add button for the rest, none for a type
    // hidden from the toolbar), rebuilding the buttons when the registered types change and refreshing
    // their checked state when a panel shows or hides, and turning a click into the add or toggle.
    // The toolkit's toolbar owns the button views.
    public sealed class DockPanelToolbarController
    {
        private readonly IDockPanelToolbarHost m_host;
        private IDockingManager? m_dockingManager;
        private DockPanelAddRemoveUISync? m_subscribedSync;
        private IReadOnlyList<Button> m_buttons = Array.Empty<Button>();

        // Set while the view is out of its window; a manager set meanwhile is followed once the view comes back
        private bool m_isDetached;

        // ===========[ Setup/Construction/Teardown ]===========================
        public DockPanelToolbarController (IDockPanelToolbarHost host)
        {
            m_host = host;
        }

        // ===========[ Events ]================================================
        private void OnEntriesChanged (object? sender, NotifyCollectionChangedEventArgs e)
        {
            this.RebuildButtons();
        }

        private void OnPanelStateChanged (object? sender, DockPanelAddRemoveUISync.PanelStateChangedEventArgs e)
        {
            if (e.IsStructuralChange)
            {
                this.RebuildButtons();
            }
            else
            {
                m_host.RefreshButtonStates();
            }
        }

        // ===========[ Properties ]============================================
        /// <summary>The manager whose panels the toolbar adds and toggles. Setting it follows the new manager's UISyncVM (once the view is in a window) and rebuilds the buttons.</summary>
        public IDockingManager? DockingManager
        {
            get => m_dockingManager;
            set
            {
                m_dockingManager = value;
                this.FollowSync(m_isDetached ? null : value?.UISyncVM);
                this.RebuildButtons();
            }
        }

        /// <summary>The buttons the toolbar shows now</summary>
        public IReadOnlyList<Button> Buttons => m_buttons;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Rebuilds the buttons from the manager's panel types and hands them to the toolbar</summary>
        public void RebuildButtons ()
        {
            var buttons = new List<Button>();
            if (m_subscribedSync != null)
            {
                foreach (DockPanelTypeEntry entry in m_subscribedSync.PanelTypeEntries)
                {
                    if (!entry.IsHiddenFromToolbar)
                    {
                        buttons.Add(new Button(entry));
                    }
                }
            }

            m_buttons = buttons;
            m_host.ShowButtons(m_buttons);
        }

        /// <summary>Call when the toolbar's view comes (back) into a window: follows the manager again and rebuilds, catching up on anything registered while it was away</summary>
        public void OnAttached ()
        {
            m_isDetached = false;
            this.FollowSync(m_dockingManager?.UISyncVM);
            this.RebuildButtons();
        }

        /// <summary>
        /// Call when the toolbar's view leaves its window: stops following the manager's UISyncVM, so a toolbar dropped
        /// from its window is not kept alive by a manager that outlives it
        /// </summary>
        public void OnDetached ()
        {
            m_isDetached = true;
            this.FollowSync(null);
        }

        /// <summary>A button was clicked: a toggle shows or hides its panel, an add button adds one</summary>
        public void OnButtonActivated (Button button)
        {
            if (button.IsToggle)
            {
                m_subscribedSync?.RequestSetToggleState(button.Entry.PanelType);
            }
            else
            {
                m_subscribedSync?.RequestAdd(button.Entry.PanelType);
            }
        }

        // ===========[ Helper Methods ]========================================
        private void FollowSync (DockPanelAddRemoveUISync? sync)
        {
            if (m_subscribedSync != null)
            {
                ((INotifyCollectionChanged)m_subscribedSync.PanelTypeEntries).CollectionChanged -= this.OnEntriesChanged;
                m_subscribedSync.PanelStateChanged -= this.OnPanelStateChanged;
                m_subscribedSync = null;
            }

            if (sync != null)
            {
                m_subscribedSync = sync;
                ((INotifyCollectionChanged)m_subscribedSync.PanelTypeEntries).CollectionChanged += this.OnEntriesChanged;
                m_subscribedSync.PanelStateChanged += this.OnPanelStateChanged;
            }
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>One toolbar button: a show/hide toggle for a single instance panel type, an add button otherwise</summary>
        public sealed class Button
        {
            internal Button (DockPanelTypeEntry entry)
            {
                this.Entry = entry;
            }

            /// <summary>The panel type this button adds or toggles</summary>
            public DockPanelTypeEntry Entry { get; }

            /// <summary>True for a show/hide toggle (a single instance panel type), false for an add button</summary>
            public bool IsToggle => this.Entry.IsSingleInstance;

            /// <summary>What the button and its tooltip say: the type's display name, or its type name when it has none</summary>
            public string Label => this.Entry.DisplayName ?? this.Entry.PanelType.Name;

            /// <summary>Whether a toggle shows as on: its panel is showing</summary>
            public bool IsChecked => this.Entry.HasActiveInstance;
        }
    }
}
