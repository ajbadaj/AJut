namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    /// <summary>
    /// Shared state broker for panel add/remove/show/hide UI. Contains the shared orchestration
    /// logic for AddPanel/TogglePanel/RemoveOrHidePanel/ShowHiddenPanel so that both WinUI3 and
    /// WPF DockingManagers can delegate to it instead of duplicating the logic. Platform-specific
    /// operations (tearoff capture/restore, zone lookup) are handled via <see cref="IDockingManager"/>
    /// hook methods.
    /// </summary>
    public class DockPanelAddRemoveUISync : NotifyPropertyChanged
    {
        // ===========[ Instance Fields ]===================================
        private readonly ObservableCollection<DockPanelTypeEntry> m_entries = new();
        private readonly List<HiddenPanelRecord> m_hiddenPanels = new();
        private IDockingManager m_manager;

        // ===========[ Construction ]===================================
        public DockPanelAddRemoveUISync ()
        {
            this.PanelTypeEntries = new ReadOnlyObservableCollection<DockPanelTypeEntry>(m_entries);
        }

        // ===========[ Events ]===================================

        /// <summary>
        /// Raised whenever a panel's visible/hidden state changes. UI elements (toolbar
        /// buttons, menu items) subscribe to this to keep their checked/toggle state current.
        /// </summary>
        public event EventHandler<PanelStateChangedEventArgs> PanelStateChanged;

        // ===========[ Properties ]===================================

        /// <summary>All registered panel type entries (observable).</summary>
        public ReadOnlyObservableCollection<DockPanelTypeEntry> PanelTypeEntries { get; }

        // ===========[ Panel Orchestration - Shared Logic ]===================================

        /// <summary>
        /// Create and dock a new panel. For single-instance types: re-shows if hidden, blocks if
        /// already active. For multi-instance types: always creates a new instance.
        /// </summary>
        public void AddPanel (Type panelType)
        {
            if (m_manager == null)
            {
                return;
            }

            DockPanelRegistrationRules? maybeRules = m_manager.GetPanelRules(panelType);
            if (maybeRules == null)
            {
                Logger.LogError($"DockPanelAddRemoveUISync.AddPanel: no factory registered for {panelType.Name}");
                return;
            }

            DockPanelRegistrationRules rules = maybeRules.Value;

            // 1. For single-instance types, check if there's a hidden instance to re-show
            if (rules.SingleInstanceOnly)
            {
                if (m_hiddenPanels.FirstOrDefault(h => h.PanelType == panelType) != null)
                {
                    this.ShowHiddenPanel(panelType);
                    return;
                }

                // Check if an active instance already exists
                bool alreadyExists = m_manager.EnumerateAdapters().Any(a => a.Display?.GetType() == panelType);
                if (alreadyExists)
                {
                    return;
                }
            }

            // 2. Build and dock the new panel
            IDockableDisplayElement display = m_manager.BuildNewDisplayElement(panelType);
            if (display == null)
            {
                return;
            }

            // Single-instance panels are always hidden (not closed) so the toggle can re-show them
            if (rules.SingleInstanceOnly)
            {
                display.DockingAdapter.HideDontClose = true;
            }

            DockZoneViewModel targetZone = m_manager.FindTargetZoneForGroup(rules.DefaultGroupId);
            if (targetZone != null)
            {
                targetZone.AddDockedContent(display.DockingAdapter);
                this.NotifyInstanceAdded(panelType, display.DockingAdapter);
            }
        }

        /// <summary>
        /// For single-instance types: show if hidden, hide if visible.
        /// For multi-instance types: equivalent to <see cref="AddPanel"/>.
        /// </summary>
        public void ShowPanel (Type panelType)
        {
            if (m_manager == null)
            {
                return;
            }

            DockPanelRegistrationRules? maybeRules = m_manager.GetPanelRules(panelType);
            if (maybeRules == null)
            {
                Logger.LogError($"DockPanelAddRemoveUISync.ShowPanel: no factory registered for {panelType.Name}");
                return;
            }

            if (!maybeRules.Value.SingleInstanceOnly)
            {
                // Multi-instance types: toggle is just add
                this.AddPanel(panelType);
                return;
            }

            // Single-instance: if hidden, re-show; if visible, hide/remove
            this.ShowHiddenPanel(panelType);
        }

        /// <summary>
        /// Close or hide a panel. When <see cref="DockingContentAdapterModel.HideDontClose"/> is true,
        /// the panel is hidden (preserving its display for later re-show) and tearoff state is captured.
        /// Otherwise the panel is closed normally.
        /// </summary>
        public void CloseOrHidePanel (DockingContentAdapterModel adapter)
        {
            if (adapter == null || m_manager == null)
            {
                return;
            }

            Type panelType = adapter.Display?.GetType();
            if (adapter.HideDontClose)
            {
                // 1. Capture tearoff state before removing
                HiddenPanelPlatformState hideState = m_manager.CaptureHideState(adapter);
                DockZoneViewModel location = adapter.Location;

                // 2. Remove from zone (may destroy the zone and collapse the split)
                if (location != null)
                {
                    location.RemoveDockedContent(adapter);
                }

                m_hiddenPanels.Add(new HiddenPanelRecord
                {
                    PanelType = panelType,
                    Display = adapter.Display,
                    Adapter = adapter,
                    PlatformHideState = hideState,
                });

                if (panelType != null)
                {
                    this.NotifyInstanceRemoved(panelType, adapter);
                }

                // 3. Platform cleanup (close empty tearoff, etc.)
                m_manager.AfterPanelHidden(hideState);
            }
            else
            {
                // Close normally
                if (adapter.Close())
                {
                    adapter.Location?.RemoveDockedContent(adapter);
                    if (panelType != null)
                    {
                        this.NotifyInstanceRemoved(panelType, adapter);
                    }
                }
            }
        }

        /// <summary>
        /// After a layout load, ensure all panel types with <see cref="DockPanelRegistrationRules.GuaranteedOnStart"/>
        /// have at least one active instance.
        /// </summary>
        public void EnforceGuaranteedOnStart ()
        {
            if (m_manager == null)
            {
                return;
            }

            foreach (DockPanelTypeEntry entry in m_entries)
            {
                if (!entry.Rules.GuaranteedOnStart)
                {
                    continue;
                }

                Type panelType = entry.PanelType;
                bool exists = m_manager.EnumerateAdapters().Any(a => a.Display?.GetType() == panelType);
                if (!exists)
                {
                    this.AddPanel(panelType);
                }
            }
        }

        // ===========[ Show Hidden Panel ]===================================

        private void ShowHiddenPanel (Type panelType)
        {
            DockingContentAdapterModel adapter;
            HiddenPanelRecord? hidden = m_hiddenPanels.FirstOrDefault(h => h.PanelType == panelType);
            if (hidden != null)
            {
                m_hiddenPanels.Remove(hidden);
                adapter = hidden.Adapter;
            }
            else
            {
                IDockableDisplayElement display = m_manager.BuildNewDisplayElement(panelType);
                adapter = display.DockingAdapter;
            }

            // 1. Try to restore to same tearoff window if it's still alive
            if (hidden?.PlatformHideState != null
                && m_manager.TryRestoreFromHideState(hidden.PlatformHideState, hidden.Adapter))
            {
                this.NotifyInstanceAdded(hidden.PanelType, hidden.Adapter);
                return;
            }


            double x = -1;
            double y = -1;
            double widgth = 500;
            double height = 500;

            // Look for last remembered location/size
            if (hidden?.PlatformHideState is HiddenPanelPlatformState platformState && platformState.WasInTearoff)
            {
                x = platformState.NextDisplayLocationX;
                y = platformState.NextDisplayLocationY;
                widgth = platformState.NextDisplayWidth;
                height = platformState.NextDisplayHeight;
            }
            // No last remembered, consult the rules
            else if (m_manager.GetPanelRules(panelType) is DockPanelRegistrationRules maybeRules)
            {
                widgth = Math.Max(100, maybeRules.SpawnWidth);
                height = Math.Max(100, maybeRules.SpawnHeight);
            }

            m_manager.CreateTearoffForPanel(adapter, x, y, widgth, height);
            this.NotifyInstanceAdded(panelType, adapter);
        }

        // ===========[ Public Interface - UI Requests ]===================================

        /// <summary>
        /// Called by toolbar toggle buttons and menu items. Delegates to <see cref="ShowPanel"/>.
        /// </summary>
        /// <returns>
        /// The final toggle state
        /// </returns>
        public bool RequestSetToggleState (Type panelType)
        { 
            if (m_manager.EnumerateAdapters().FirstOrDefault(adapter => adapter.Display?.GetType() == panelType) is DockingContentAdapterModel adapterToClose)
            {
                this.CloseOrHidePanel(adapterToClose);
                return false;
            }

            this.ShowPanel(panelType);
            return true;
        }

        /// <summary>
        /// Called by toolbar add buttons. Delegates to <see cref="AddPanel"/>.
        /// </summary>
        public void RequestAdd (Type panelType)
        {
            this.AddPanel(panelType);
        }

        /// <summary>
        /// Called when a panel should be closed or hidden. Delegates to <see cref="CloseOrHidePanel"/>.
        /// </summary>
        public void RequestRemoveOrHide (DockingContentAdapterModel adapter)
        {
            this.CloseOrHidePanel(adapter);
        }

        /// <summary>
        /// Returns descriptors for building a "View" menu or similar. Each entry that is
        /// not hidden from UI gets a descriptor with its current toggle state. Single-instance
        /// types appear as toggle items (checked = visible); multi-instance types appear as
        /// simple add actions with "Add " prefix.
        /// </summary>
        public IReadOnlyList<PanelMenuDescriptor> GenerateMenuDescriptors ()
        {
            var result = new List<PanelMenuDescriptor>();
            foreach (DockPanelTypeEntry entry in m_entries)
            {
                if (entry.IsHiddenFromUI)
                {
                    continue;
                }

                result.Add(new PanelMenuDescriptor
                {
                    PanelType = entry.PanelType,
                    DisplayName = entry.DisplayName ?? StringXT.ConvertToFriendlyEn(entry.PanelType.Name),
                    IconPath = entry.IconPath,
                    IsToggle = entry.IsSingleInstance,
                    IsChecked = entry.HasActiveInstance,
                });
            }

            return result;
        }

        // ===========[ Configuration ]===================================

        /// <summary>
        /// Set a custom display name and/or icon path for a panel type. Call after
        /// <c>RegisterDisplayFactory</c> but before the toolbar or menu is created.
        /// </summary>
        public void RegisterPanelDisplayOverride<T> (string displayName, string iconPath = null)
            where T : IDockableDisplayElement
        {
            DockPanelTypeEntry entry = this.FindEntry(typeof(T));
            if (entry == null)
            {
                return;
            }

            if (displayName != null)
            {
                entry.DisplayName = displayName;
            }

            entry.IconPath = iconPath;
        }

        /// <summary>
        /// Exclude a panel type from toolbar buttons and menu items entirely.
        /// </summary>
        public void HidePanelDisplayActions<T> () where T : IDockableDisplayElement
        {
            DockPanelTypeEntry entry = this.FindEntry(typeof(T));
            if (entry != null)
            {
                entry.IsHiddenFromUI = true;
            }
        }

        /// <summary>Find the entry for a given panel type. Returns null if not registered.</summary>
        public DockPanelTypeEntry FindEntry (Type panelType)
        {
            return m_entries.FirstOrDefault(e => e.PanelType == panelType);
        }

        // ===========[ Manager Integration ]===================================

        /// <summary>
        /// Called by DockingManager during construction to establish the two-way link.
        /// </summary>
        public void SetManager (IDockingManager manager)
        {
            m_manager = manager;
        }

        /// <summary>Register a new panel type entry.</summary>
        public void AddEntry (Type panelType, DockPanelRegistrationRules rules)
        {
            if (m_entries.Any(e => e.PanelType == panelType))
            {
                return;
            }

            m_entries.Add(new DockPanelTypeEntry(panelType, rules));
        }

        /// <summary>Called when a panel instance is docked (externally or by shared logic).</summary>
        public void NotifyInstanceAdded (Type panelType, DockingContentAdapterModel adapter)
        {
            DockPanelTypeEntry entry = this.FindEntry(panelType);
            if (entry != null)
            {
                ++entry.ActiveInstanceCount;
            }

            this.PanelStateChanged?.Invoke(this, new PanelStateChangedEventArgs(panelType, entry));
        }

        /// <summary>Called when a panel instance is removed or hidden.</summary>
        public void NotifyInstanceRemoved (Type panelType, DockingContentAdapterModel adapter)
        {
            DockPanelTypeEntry entry = this.FindEntry(panelType);
            if (entry != null)
            {
                entry.ActiveInstanceCount = Math.Max(0, entry.ActiveInstanceCount - 1);
            }

            this.PanelStateChanged?.Invoke(this, new PanelStateChangedEventArgs(panelType, entry));
        }

        /// <summary>Reset all counts (e.g. before a layout reload).</summary>
        public void ResetAllCounts ()
        {
            foreach (DockPanelTypeEntry entry in m_entries)
            {
                entry.ActiveInstanceCount = 0;
            }
        }

        // ===========[ Sub-classes ]===================================

        public class PanelStateChangedEventArgs : EventArgs
        {
            public PanelStateChangedEventArgs (Type panelType, DockPanelTypeEntry entry)
            {
                this.PanelType = panelType;
                this.Entry = entry;
            }

            public Type PanelType { get; }
            public DockPanelTypeEntry Entry { get; }
        }

        /// <summary>
        /// Descriptor for building a platform-specific menu item. Returned by
        /// <see cref="GenerateMenuDescriptors"/>.
        /// </summary>
        public class PanelMenuDescriptor
        {
            public Type PanelType { get; init; }
            public string DisplayName { get; init; }
            public string IconPath { get; init; }
            public bool IsToggle { get; init; }
            public bool IsChecked { get; init; }
        }

        /// <summary>
        /// Record for a hidden panel. Stores the display, adapter, and platform-specific tearoff
        /// state needed to restore the panel later (always as a tearoff window).
        /// </summary>
        public class HiddenPanelRecord
        {
            public Type PanelType { get; init; }
            public IDockableDisplayElement Display { get; init; }
            public DockingContentAdapterModel Adapter { get; init; }
            public object PlatformHideState { get; init; }
        }
    }

    /// <summary>
    /// Platform-agnostic base for hide-state captured by <see cref="IDockingManager.CaptureHideState"/>.
    /// Platform DockingManagers create instances of this to store tearoff info.
    /// </summary>
    public class HiddenPanelPlatformState
    {
        public bool WasInTearoff { get; init; }
        public double NextDisplayLocationX { get; init; }
        public double NextDisplayLocationY { get; init; }
        public double NextDisplayWidth { get; init; }
        public double NextDisplayHeight { get; init; }

        /// <summary>Platform-specific tearoff window reference.</summary>
        public object TearoffWindowRef { get; init; }
    }
}
