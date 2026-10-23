namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using AJut.Storage;
    using AJut.Tree;
    using AJut.TypeManagement;

    // ===========[ DockingManagerBase ]=========================================
    // Everything a docking manager does that never touches a window or a control, shared by every
    // toolkit's DockingManager:
    //  - The display factory: registering panel types and building, adapting and setting up panels.
    //  - The root zone registry: which root zones are registered, which window hosts each, and
    //    finding a leaf zone to dock into, by group or otherwise.
    //  - Tearoff bookkeeping: which root zone each tearoff window hosts, tearing panels and zones off,
    //    and every close path a tearoff window can take (user close, close all, the root window
    //    closing, a drop emptying it, a layout load clearing it), with the zone deregister and
    //    teardown each one needs.
    //  - Hidden panels' tearoff state, for re-showing a panel where it was hidden from.
    //  - Layout save and load orchestration, and auto-save.
    //
    // TWindow is the toolkit's window type and TZone its root DockZone control. The toolkit's
    // DockingManager derives from this and supplies window creation, placement, closing and
    // activation, and the zone control's view model, group id and tree through the abstract hooks.
    // Drag tracking, hit testing and menus stay in the toolkit's manager.
    public abstract class DockingManagerBase<TWindow, TZone> : NotifyPropertyChanged, IDockingManager, IDisposable
        where TWindow : class
        where TZone : class
    {
        private const string kLayoutPanelTypeMessage = "Builds each panel a layout file names from its saved type id, and trimming can remove a panel type, or its constructor, that nothing else uses. Register each panel type with RegisterDisplayFactory<T> to keep it.";

        private readonly Dictionary<Type, DisplayBuilder> m_factory = new Dictionary<Type, DisplayBuilder>();
        private readonly List<TZone> m_rootZones = new List<TZone>();
        private readonly Dictionary<TWindow, List<TZone>> m_zonesByWindow = new Dictionary<TWindow, List<TZone>>();
        private readonly Dictionary<TWindow, DockZoneViewModel> m_tearoffRootZones = new Dictionary<TWindow, DockZoneViewModel>();
        private readonly List<TWindow> m_windowsToCloseSilently = new List<TWindow>();
        private readonly List<TWindow> m_currentlyClosingWindows = new List<TWindow>();
        private bool m_isZoneDragDropUnderway;
        private bool m_isReadyToTrackAutoSave;
        private bool m_isDisposed;

        // ===========[ Setup/Construction/Teardown ]===========================
        /// <summary>
        /// Sets up the manager for <paramref name="rootWindow"/>. With no <paramref name="persistentStorageFilePath"/>, the
        /// layout is kept under the app's data root (<see cref="AppStorage.AppDataRoot"/>), in Layouts, named by
        /// <paramref name="uniqueId"/>.
        /// </summary>
        protected DockingManagerBase (TWindow rootWindow, string uniqueId, string? persistentStorageFilePath, eDockingAutoSaveMethod autoSaveMethod)
        {
            this.RootWindow = rootWindow;
            this.UniqueId = uniqueId;
            this.DockLayoutPersistentStorageFile = persistentStorageFilePath ?? CreateDefaultLayoutPath(uniqueId);
            this.AutoSaveMethod = autoSaveMethod;

            this.UISyncVM = new DockPanelAddRemoveUISync();
            this.UISyncVM.SetManager(this);
        }

        /// <summary>
        /// Force closes every tearoff, releases every hidden panel, lets the toolkit drop its own subscriptions
        /// (<see cref="OnDisposing"/>), tears down every root zone's view model tree, then clears every registry. A second
        /// call does nothing.
        /// </summary>
        public void Dispose ()
        {
            if (m_isDisposed)
            {
                return;
            }

            m_isDisposed = true;
            this.CloseAll(force: true);

            // A hidden panel is out of every zone but still this manager's, waiting to be shown again
            this.UISyncVM.ReleaseHiddenPanels();
            this.OnDisposing();

            foreach (TZone zone in m_rootZones)
            {
                this.OnRootZoneDisposing(zone);

                // Terminal teardown of the view model tree: drop Manager/Parent/UI on every zone view model so a
                // zone whose UI outlives this manager can't re-root the (now disposed) manager. The tree is still
                // intact here, CloseAll(force) above only handled tearoff windows.
                this.GetZoneViewModel(zone)?.Teardown();
            }

            m_rootZones.Clear();
            m_tearoffRootZones.Clear();
            m_windowsToCloseSilently.Clear();
            m_currentlyClosingWindows.Clear();
            m_factory.Clear();

            // CloseAll deliberately keeps the root window's entry so callers can close tearoffs without
            // killing the host. Dispose has no such caller, so clear what remains, emptying each inner list
            // too in case anything still holds one of them.
            foreach (List<TZone> zoneList in m_zonesByWindow.Values)
            {
                zoneList.Clear();
            }

            m_zonesByWindow.Clear();
        }

        // ===========[ Properties ]============================================
        /// <summary>The window the main docking layout lives in; every tearoff is a window besides this one</summary>
        public TWindow RootWindow { get; }

        /// <summary>The id that names this manager's layout file when no path is given</summary>
        public string UniqueId { get; }

        /// <summary>Where <see cref="SaveDockLayoutToPersistentStorage"/> and <see cref="ReloadDockLayoutFromPersistentStorage"/> save and load</summary>
        public string DockLayoutPersistentStorageFile { get; set; }

        /// <summary>When the layout saves itself</summary>
        public eDockingAutoSaveMethod AutoSaveMethod { get; set; }

        /// <summary>True while a layout is being loaded</summary>
        public bool IsLoadingFromLayout { get; private set; }

        /// <summary>True while a zone or panel is being dragged to a new dock location</summary>
        public bool IsZoneDragDropUnderway
        {
            get => m_isZoneDragDropUnderway;
            protected set => this.SetAndRaiseIfChanged(ref m_isZoneDragDropUnderway, value);
        }

        /// <summary>Minimum pixel dimension for dock zone panels during size recalculation. Default: 50.</summary>
        public double MinPanelDimension { get; set; } = 50.0;

        /// <inheritdoc/>
        public DockPanelAddRemoveUISync UISyncVM { get; }

        /// <summary>Every registered root zone, the main window's and each tearoff's</summary>
        protected IReadOnlyList<TZone> RootZones => m_rootZones;

        /// <summary>Every window that has root zones registered, the root window included</summary>
        protected IReadOnlyCollection<TWindow> WindowsWithZones => m_zonesByWindow.Keys;

        // ===========[ Public Interface Methods ]==============================
        /// <inheritdoc/>
        public IEnumerable<DockZoneViewModel> GetAllRoots ()
        {
            return m_rootZones.Select(z => this.GetZoneViewModel(z)!);
        }

        /// <summary>Registers how to build a panel type; with no <paramref name="customFactory"/> its default constructor is used</summary>
        public void RegisterDisplayFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> (Func<T>? customFactory = null, bool singleInstanceOnly = false)
            where T : IDockableDisplayElement
        {
            this.RegisterDisplayFactory<T>(
                new DockPanelRegistrationRules { SingleInstanceOnly = singleInstanceOnly },
                customFactory
            );
        }

        /// <summary>Registers how to build a panel type, and its rules; with no <paramref name="customFactory"/> its default constructor is used</summary>
        public void RegisterDisplayFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> (DockPanelRegistrationRules rules, Func<T>? customFactory = null)
            where T : IDockableDisplayElement
        {
            m_factory[typeof(T)] = new DisplayBuilder(
                rules,
                customFactory != null
                    ? () => customFactory() as IDockableDisplayElement
                    : () => BuildDefaultDisplayFor(typeof(T))
            );

            this.UISyncVM.AddEntry(typeof(T), rules);
        }

        /// <summary>Builds and sets up a new panel of type <typeparamref name="T"/>, without docking it anywhere</summary>
        public T BuildNewDisplayElement<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> () where T : IDockableDisplayElement
            => (T)this.BuildNewDisplayElement(typeof(T));

        /// <inheritdoc/>
        public IDockableDisplayElement BuildNewDisplayElement ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type elementType)
        {
            DockPanelRegistrationRules rules = default;
            IDockableDisplayElement? displayElement;
            if (m_factory.TryGetValue(elementType, out DisplayBuilder? builder))
            {
                displayElement = builder.Builder();
                rules = builder.Rules;
            }
            else
            {
                displayElement = BuildDefaultDisplayFor(elementType);
            }

            return this.SetupAndReturnNew(displayElement, rules)!;
        }

        /// <summary>Removes root zones from the registry, without touching their view models</summary>
        public void DeRegisterRootDockZones (params TZone[] dockZones)
        {
            foreach (TZone zone in dockZones)
            {
                m_rootZones.Remove(zone);
                this.OnRootZoneDeregistered(zone);
                foreach (List<TZone> list in m_zonesByWindow.Values)
                {
                    list.Remove(zone);
                }
            }
        }

        /// <summary>
        /// Closes every tearoff window. Unless <paramref name="force"/>, every docked panel is asked first, and one veto
        /// stops the whole close (returning false) before anything closes.
        /// </summary>
        public bool CloseAll (bool force = false)
        {
            if (!force)
            {
                List<DockingContentAdapterModel> all = m_rootZones
                    .SelectMany(z => this.EnumerateZoneTree(z))
                    .SelectMany(z => this.GetZoneViewModel(z) is DockZoneViewModel vm
                        ? (IEnumerable<DockingContentAdapterModel>)vm.DockedContent
                        : Enumerable.Empty<DockingContentAdapterModel>())
                    .ToList();

                if (!this.CheckPanelsCanCloseForCloseAll(all))
                {
                    return false;
                }
            }

            this.CloseEveryTearoffSilently();
            return true;
        }

        /// <summary>
        /// Flattens every split zone whose child splits the same way, moving the child's children up into it, so a
        /// built or loaded layout never nests a horizontal split directly inside a horizontal split
        /// </summary>
        public void CleanZoneLayoutHierarchies ()
        {
            var toVisit = new Queue<DockZoneViewModel>();
            foreach (DockZoneViewModel? zone in m_rootZones.Select(z => this.GetZoneViewModel(z)))
            {
                if (zone != null)
                {
                    toVisit.Enqueue(zone);
                }
            }

            while (toVisit.Count != 0)
            {
                DockZoneViewModel parentEval = toVisit.Dequeue();
                if (parentEval?.Children == null || parentEval.Children.Count == 0)
                {
                    continue;
                }

                for (int index = 0; index < parentEval.Children.Count; ++index)
                {
                    DockZoneViewModel child = parentEval.Children[index];
                    if (child.Orientation == parentEval.Orientation)
                    {
                        parentEval.RunChildZoneRemoval(child);
                        foreach (DockZoneViewModel grandchild in child.Children.Where(c => c != null).ToList())
                        {
                            parentEval.InsertChild(index++, grandchild);
                            toVisit.Enqueue(grandchild);
                        }
                    }
                    else
                    {
                        toVisit.Enqueue(child);
                    }
                }
            }
        }

        /// <summary>The first leaf zone (one that shows panels rather than splitting) under any root, or null when there is none</summary>
        public DockZoneViewModel? FindFirstAvailableDockZone ()
        {
            foreach (DockZoneViewModel? zone in m_rootZones
                .SelectMany(z => this.EnumerateZoneTree(z))
                .Select(z => this.GetZoneViewModel(z)))
            {
                if (zone != null && zone.Orientation.IsFlagInGroup(eDockOrientation.AnyLeafDisplay))
                {
                    return zone;
                }
            }

            Logger.LogError("DockingManager: tried to find first available dock zone but none exists - manager may be set up incorrectly");
            return null;
        }

        /// <summary>
        /// Find the first available leaf dock zone matching the given group id.
        /// Falls back to <see cref="FindFirstAvailableDockZone"/> if no match is found.
        /// </summary>
        public DockZoneViewModel? FindFirstAvailableDockZoneForGroup (string? groupId)
        {
            if (groupId.IsNotNullOrEmpty())
            {
                foreach (TZone rootZone in m_rootZones)
                {
                    if (this.GetZoneGroupId(rootZone) == groupId)
                    {
                        foreach (DockZoneViewModel zone in TreeTraversal<DockZoneViewModel>.All(this.GetZoneViewModel(rootZone)!))
                        {
                            if (zone.Orientation.IsFlagInGroup(eDockOrientation.AnyLeafDisplay))
                            {
                                return zone;
                            }
                        }
                    }
                }
            }

            return this.FindFirstAvailableDockZone();
        }

        /// <inheritdoc/>
        public void AddPanel (Type panelType) => this.UISyncVM.AddPanel(panelType);

        /// <inheritdoc/>
        public void TogglePanel (Type panelType) => this.UISyncVM.ShowPanel(panelType);

        /// <inheritdoc/>
        public void RemoveOrHidePanel (DockingContentAdapterModel adapter) => this.UISyncVM.CloseOrHidePanel(adapter);

        /// <summary>
        /// Loads the persistent layout, or failing that the fallback layout (which is then saved as the persistent
        /// one), then makes sure every guaranteed-on-start panel exists. Returns false when neither loaded.
        /// </summary>
        [RequiresUnreferencedCode(kLayoutPanelTypeMessage)]
        public bool SetupDefaultAndFallbackTo (string? fallbackLayoutFilePath)
        {
            if (this.ReloadDockLayoutFromPersistentStorage())
            {
                this.UISyncVM.EnforceGuaranteedOnStart();
                m_isReadyToTrackAutoSave = true;
                return true;
            }
            else if (fallbackLayoutFilePath != null && this.LoadDockLayoutFromFile(fallbackLayoutFilePath))
            {
                this.UISyncVM.EnforceGuaranteedOnStart();
                this.SaveDockLayoutToPersistentStorage();
                m_isReadyToTrackAutoSave = true;
                return true;
            }

            // A first run has no layout to load yet, and the layout the user goes on to build is the one to save
            this.UISyncVM.EnforceGuaranteedOnStart();
            m_isReadyToTrackAutoSave = true;
            return false;
        }

        /// <inheritdoc/>
        [RequiresUnreferencedCode(kLayoutPanelTypeMessage)]
        public bool ReloadDockLayoutFromPersistentStorage ()
        {
            try
            {
                return this.LoadDockLayoutFromFile(this.DockLayoutPersistentStorageFile);
            }
            catch (Exception e)
            {
                Logger.LogError($"Docking: Error loading from layout file '{this.DockLayoutPersistentStorageFile ?? "<null>"}'", e);
                return false;
            }
        }

        /// <inheritdoc/>
        [RequiresUnreferencedCode(kLayoutPanelTypeMessage)]
        public bool LoadDockLayoutFromFile (string filePath)
        {
            this.IsLoadingFromLayout = true;
            try
            {
                return this.ResetLayoutFromFile(filePath);
            }
            finally
            {
                this.IsLoadingFromLayout = false;

                // A load replaces the panels wholesale, so count them again rather than tracking each one
                this.UISyncVM.RecountActiveInstances();
            }
        }

        /// <inheritdoc/>
        public bool SaveDockLayoutToPersistentStorage ()
            => this.SaveDockLayoutToFile(this.DockLayoutPersistentStorageFile);

        /// <inheritdoc/>
        public bool SaveDockLayoutToFile (string? filePath = null)
            => this.SaveLayoutToFile(filePath);

        /// <summary>The temp file <see cref="eDockingAutoSaveMethod.AutoSaveToTemp"/> saves to, beside the persistent one</summary>
        public string GenerateDefaultStateStorageAutoSaveTempPath ()
            => this.DockLayoutPersistentStorageFile + "~";

        // ===========[ IDockingManager Implementations ]=======================
        /// <inheritdoc/>
        public DockPanelRegistrationRules? GetPanelRules (Type panelType)
        {
            return m_factory.TryGetValue(panelType, out DisplayBuilder? builder) ? builder.Rules : null;
        }

        /// <inheritdoc/>
        public DockZoneViewModel? FindTargetZoneForGroup (string? groupId)
        {
            return this.FindFirstAvailableDockZoneForGroup(groupId);
        }

        /// <summary>
        /// Captures where a panel's tearoff window is, so a hidden panel can be re-shown there. A panel in the root
        /// window gives null.
        /// </summary>
        public virtual HiddenPanelPlatformState? CaptureHideState (DockingContentAdapterModel adapter)
        {
            TZone? dockZoneUI = adapter.Location?.UI as TZone;
            TWindow? hostWindow = dockZoneUI != null ? this.GetWindowForZone(dockZoneUI) : null;
            bool wasInTearoff = hostWindow != null && hostWindow != this.RootWindow;

            if (!wasInTearoff)
            {
                return null;
            }

            // Kept as creation bounds, since re-showing the panel creates its tearoff from them
            DockWindowBounds bounds = this.ToTearoffCreationBounds(this.GetWindowBounds(hostWindow!));
            return new HiddenPanelPlatformState
            {
                WasInTearoff = true,
                NextDisplayLocationX = bounds.X,
                NextDisplayLocationY = bounds.Y,
                NextDisplayWidth = bounds.Width,
                NextDisplayHeight = bounds.Height,
                TearoffWindowRef = hostWindow!,
            };
        }

        /// <summary>Docks a re-shown panel back into the tearoff it was hidden from, if that window is still open</summary>
        public virtual bool TryRestoreFromHideState (object hideState, DockingContentAdapterModel adapter)
        {
            if (hideState is not HiddenPanelPlatformState state || !state.WasInTearoff)
            {
                return false;
            }

            // Check if the same tearoff window is still alive
            if (state.TearoffWindowRef is TWindow window && m_zonesByWindow.ContainsKey(window))
            {
                if (m_tearoffRootZones.TryGetValue(window, out DockZoneViewModel? tearoffRoot))
                {
                    tearoffRoot.AddDockedContent(adapter);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Closes the tearoff a panel was hidden from, if hiding it left that tearoff empty</summary>
        public virtual void AfterPanelHidden (object hideState)
        {
            if (hideState is not HiddenPanelPlatformState state || !state.WasInTearoff)
            {
                return;
            }

            if (state.TearoffWindowRef is TWindow hostWindow)
            {
                TZone? rootZoneUI = this.GetRootZoneForWindow(hostWindow);
                if (rootZoneUI != null)
                {
                    this.CloseSourceTearoffIfEmpty(hostWindow, rootZoneUI);
                }
            }
        }

        /// <summary>
        /// Creates a tearoff window at the given place and size and docks <paramref name="adapter"/> into it. A negative
        /// <paramref name="x"/> or <paramref name="y"/> takes the root window's.
        /// </summary>
        public bool CreateTearoffForPanel (DockingContentAdapterModel adapter, double x, double y, double width, double height)
        {
            if (x < 0 || y < 0)
            {
                // In creation units, like the bounds this was handed
                DockWindowBounds rootBounds = this.ToTearoffCreationBounds(this.GetWindowBounds(this.RootWindow));
                if (x < 0)
                {
                    x = rootBounds.X;
                }

                if (y < 0)
                {
                    y = rootBounds.Y;
                }
            }

            var newZone = new DockZoneViewModel(this);
            Result<TWindow> windowResult = this.CreateAndStockTearoffWindow(newZone, new DockWindowBounds(x, y, width, height));
            if (windowResult)
            {
                newZone.AddDockedContent(adapter);
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public bool IsTearoffRootThatWouldOrphan (DockZoneViewModel zone)
        {
            if (zone == null)
            {
                return false;
            }

            foreach (DockZoneViewModel rootZone in m_tearoffRootZones.Values)
            {
                if (rootZone == zone)
                {
                    // It's a tearoff root, would removing the last content leave it empty
                    return zone.DockedContent.Count <= 1 && zone.Children.Count == 0;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public void CloseTearoffForRootZone (DockZoneViewModel rootZone)
        {
            foreach (KeyValuePair<TWindow, DockZoneViewModel> tearoff in m_tearoffRootZones)
            {
                if (tearoff.Value == rootZone)
                {
                    this.SilentlyForceCloseTearoffWindow(tearoff.Key);
                    return;
                }
            }
        }

        // ===========[ Toolkit Hooks ]=========================================
        /// <summary>The view model a root zone control shows, or null before it has one</summary>
        protected abstract DockZoneViewModel? GetZoneViewModel (TZone zone);

        /// <summary>The group id a root zone was registered under, which layouts and <see cref="DockPanelRegistrationRules.DefaultGroupId"/> name it by</summary>
        protected abstract string? GetZoneGroupId (TZone zone);

        /// <summary>A zone control and every zone control nested inside it, the zone itself first</summary>
        protected abstract IEnumerable<TZone> EnumerateZoneTree (TZone rootZone);

        /// <summary>
        /// Creates a tearoff window hosting <paramref name="rootZone"/> at <paramref name="bounds"/> (a size in the units
        /// zone render sizes use), shows it, records it with <see cref="RecordTearoffRootZone"/>, and registers its root
        /// zone control with the window
        /// </summary>
        protected abstract Result<TWindow> CreateAndStockTearoffWindow (DockZoneViewModel rootZone, DockWindowBounds bounds);

        /// <summary>Where a window is and how big, in the units the toolkit places windows by</summary>
        protected abstract DockWindowBounds GetWindowBounds (TWindow window);

        /// <summary>Whether the toolkit still tracks this window as open</summary>
        protected abstract bool IsWindowTracked (TWindow window);

        /// <summary>Stops the toolkit tracking a window that is closing</summary>
        protected abstract void StopTrackingWindow (TWindow window);

        /// <summary>Drops every subscription the manager made on a tearoff window and its content, so a closing window pins nothing</summary>
        protected abstract void DetachTearoffSubscriptions (TWindow window);

        /// <summary>Closes a window. Its tearoff subscriptions are already detached by the time this is called.</summary>
        protected abstract void CloseWindow (TWindow window);

        /// <summary>Brings a window to the front</summary>
        protected abstract void ActivateWindow (TWindow window);

        /// <summary>
        /// A tearoff window's place, size and maximized or full screen state, for saving to a layout file. False when the
        /// window is already gone, and it is left out of the file.
        /// </summary>
        protected abstract bool TryCaptureWindowLayoutState (TWindow window, out DockWindowLayoutState state);

        /// <summary>
        /// Converts a window's placement bounds (as <see cref="GetWindowBounds"/> gives them, and as layout files and hidden
        /// panels keep them) into the bounds <see cref="CreateAndStockTearoffWindow"/> takes, for a toolkit whose window
        /// placement and tearoff creation use different units. The same bounds by default.
        /// </summary>
        protected virtual DockWindowBounds ToTearoffCreationBounds (DockWindowBounds placementBounds) => placementBounds;

        /// <summary>Maximizes a tearoff, or makes it full screen, after a layout load created it at its saved bounds</summary>
        protected abstract void ApplyWindowLayoutState (TWindow window, DockWindowLayoutState state);

        /// <summary>
        /// Asks every docked panel whether it can close, for <see cref="CloseAll"/>; false stops the whole close. By default
        /// the first panel to refuse ends the asking.
        /// </summary>
        protected virtual bool CheckPanelsCanCloseForCloseAll (IReadOnlyList<DockingContentAdapterModel> panels)
        {
            foreach (DockingContentAdapterModel adapter in panels)
            {
                if (!adapter.CheckCanClose())
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Called as a root zone leaves the registry, for the toolkit to unset its manager</summary>
        protected virtual void OnRootZoneDeregistered (TZone zone) { }

        /// <summary>Called from <see cref="Dispose"/> after tearoffs close and before root zones are torn down, for the toolkit's own subscriptions</summary>
        protected virtual void OnDisposing () { }

        /// <summary>Called from <see cref="Dispose"/> for each root zone, before its view model tree is torn down</summary>
        protected virtual void OnRootZoneDisposing (TZone zone) { }

        // ===========[ Helpers For The Toolkit ]===============================
        /// <summary>Adds a root zone control to the registry, hosted by <paramref name="window"/></summary>
        protected void RecordRootZone (TWindow window, TZone zone)
        {
            m_rootZones.Add(zone);
            if (!m_zonesByWindow.TryGetValue(window, out List<TZone>? list))
            {
                list = new List<TZone>();
                m_zonesByWindow[window] = list;
            }

            list.Add(zone);
        }

        /// <summary>Records which root zone view model a tearoff window hosts</summary>
        protected void RecordTearoffRootZone (TWindow window, DockZoneViewModel rootZone)
        {
            m_tearoffRootZones[window] = rootZone;
        }

        /// <summary>The root zone controls registered for a window</summary>
        protected bool TryGetZonesForWindow (TWindow window, out IReadOnlyList<TZone> zones)
        {
            if (m_zonesByWindow.TryGetValue(window, out List<TZone>? list))
            {
                zones = list;
                return true;
            }

            zones = Array.Empty<TZone>();
            return false;
        }

        /// <summary>The window whose registered root zones hold <paramref name="zone"/>, or the root window if none does</summary>
        protected TWindow GetWindowForZone (TZone zone)
        {
            foreach (KeyValuePair<TWindow, List<TZone>> windowZones in m_zonesByWindow)
            {
                foreach (TZone rootZone in windowZones.Value)
                {
                    if ((rootZone == zone)
                        || this.EnumerateZoneTree(rootZone).Any(z => z == zone))
                    {
                        return windowZones.Key;
                    }
                }
            }

            return this.RootWindow;
        }

        /// <summary>The first root zone control registered for a window, or null</summary>
        protected TZone? GetRootZoneForWindow (TWindow window)
            => m_zonesByWindow.TryGetValue(window, out List<TZone>? zones) ? zones.FirstOrDefault() : null;

        /// <summary>The first registered root zone's view model, which a layout loads into when it names no known group</summary>
        protected DockZoneViewModel? GetFallbackRootZone ()
            => m_rootZones.FirstOrDefault() is TZone zone ? this.GetZoneViewModel(zone) : null;

        /// <summary>
        /// The view model of the root zone registered under <paramref name="groupId"/>, or null. A root zone with no group
        /// id is saved under the empty string, so the empty string (or null) finds it.
        /// </summary>
        protected DockZoneViewModel? GetRootZoneForGroup (string groupId)
        {
            string wanted = groupId ?? string.Empty;
            TZone? zone = m_rootZones.FirstOrDefault(z => (this.GetZoneGroupId(z) ?? string.Empty) == wanted);
            return zone != null ? this.GetZoneViewModel(zone) : null;
        }

        /// <summary>
        /// Rebuilds <paramref name="zone"/> from a layout file's zone tree, building each panel the file names by its
        /// registered type id (or else its full type name) and handing it the state the file saved for it
        /// </summary>
        [RequiresUnreferencedCode(kLayoutPanelTypeMessage)]
        protected void ApplyZoneLayout (DockZoneViewModel zone, DockZoneSerializationData data)
        {
            zone.StorePassAlongUISize(data.SizeOnParent);
            zone.BuildFromState(data, this.BuildDisplayAdapterFromLayout);
        }

        /// <summary>
        /// Takes a panel out of its zone and docks it alone in a new tearoff window at <paramref name="bounds"/>. The
        /// window is made first, so a window that can't be made leaves the panel where it was.
        /// </summary>
        protected Result<TWindow> DoTearoffCore (IDockableDisplayElement element, DockWindowBounds bounds)
        {
            try
            {
                var newZone = new DockZoneViewModel(this);
                Result<TWindow> windowResult = this.CreateAndStockTearoffWindow(newZone, bounds);
                if (!windowResult)
                {
                    return windowResult;
                }

                DockZoneViewModel sourceLocation = element.DockingAdapter.Location;
                if (!sourceLocation.RemoveDockedContent(element.DockingAdapter))
                {
                    // The panel stays put, so the window made for it goes again
                    this.SilentlyForceCloseTearoffWindow(windowResult.Value);

                    var result = Result<TWindow>.Error("DockingManager: Tear off failed - could not remove content from zone");
                    Logger.LogError(result.GetErrorReport());
                    return result;
                }

                newZone.AddDockedContent(element);
                return windowResult;
            }
            catch (Exception exc)
            {
                Logger.LogError("Window tearoff failed!", exc);
                return Result<TWindow>.Error("DockingManager: Window tearoff failed for unknown reason");
            }
        }

        /// <summary>
        /// Moves a whole zone into a new tearoff window at <paramref name="bounds"/>. A tearoff's root zone gives back
        /// its existing window; the root window's root zone is duplicated, so the root window keeps its slot.
        /// </summary>
        protected Result<TWindow> DoGroupTearoffCore (DockZoneViewModel sourceZone, DockWindowBounds bounds)
        {
            try
            {
                if (sourceZone.Parent != null)
                {
                    sourceZone.UnparentAndDistributeSibling();
                }
                else
                {
                    // Already root of a tearoff window, return its existing window
                    TWindow? existingWindow = m_tearoffRootZones
                        .Where(kv => kv.Value == sourceZone)
                        .Select(kv => kv.Key)
                        .FirstOrDefault();

                    if (existingWindow != null)
                    {
                        return Result<TWindow>.Success(existingWindow);
                    }

                    // Root zone on the main window, duplicate so the main zone keeps its slot
                    sourceZone = sourceZone.DuplicateAndClear();
                }

                return this.CreateAndStockTearoffWindow(sourceZone, bounds);
            }
            catch (Exception exc)
            {
                Logger.LogError("Window group tearoff failed!", exc);
                return Result<TWindow>.Error("DockingManager: Window tearoff failed for unknown reason");
            }
        }

        /// <summary>
        /// Commits a drop: <paramref name="sourceZone"/> goes into <paramref name="targetZone"/> in
        /// <paramref name="direction"/>, the drag's source tearoff closes, and <paramref name="targetWindow"/> (captured
        /// before the drop restructured the tree) is brought to the front
        /// </summary>
        protected void CommitDrop (DockZoneViewModel targetZone, DockZoneViewModel sourceZone, eDockInsertionDirection direction, TWindow dragSourceWindow, TWindow? targetWindow)
        {
            targetZone.DropAddSiblingIntoDock(sourceZone, direction);

            // Always close the tearoff after a successful drop. For tab-add drops the source zone is emptied; for
            // directional splits the source view model is transplanted into the root hierarchy so DockedContent stays
            // non-empty, but the tearoff window is no longer needed in either case.
            this.SilentlyForceCloseTearoffWindow(dragSourceWindow);

            // When the drag source closes, the OS activates whichever window it considers next, often the root
            // window rather than the drop target. Activate the target so it stays in front.
            if (targetWindow != null)
            {
                this.ActivateWindow(targetWindow);
            }
        }

        /// <summary>
        /// Puts a canceled drag back where it began: <paramref name="tornOffRoot"/>, the root zone of the tearoff the drag
        /// made, is dropped where <paramref name="origin"/> says it came from, the way a drop would (which closes
        /// <paramref name="dragSourceWindow"/>), then gets its tab position, selection and split sizes back. False, with the
        /// tearoff left where it is, when the place it came from is gone.
        /// </summary>
        protected bool ReturnCanceledDrag (DockDragOrigin origin, DockZoneViewModel tornOffRoot, TWindow dragSourceWindow)
        {
            if (!origin.TryFindReturn(out DockZoneViewModel target, out eDockInsertionDirection direction))
            {
                return false;
            }

            TWindow? targetWindow = target.UI is TZone targetZone ? this.GetWindowForZone(targetZone) : null;
            this.CommitDrop(target, tornOffRoot, direction, dragSourceWindow, targetWindow);
            origin.RestoreAfterReturn(tornOffRoot);
            return true;
        }

        /// <summary>
        /// The user asked a tearoff window to close (its window close or its own close button). Runs the close, and
        /// returns false when a panel vetoed it, or the window is already closing.
        /// </summary>
        protected bool DoTearoffWindowClose (TWindow window)
        {
            if (m_windowsToCloseSilently.Contains(window) || m_currentlyClosingWindows.Contains(window))
            {
                return false;
            }

            m_currentlyClosingWindows.Add(window);
            try
            {
                if (!m_tearoffRootZones.TryGetValue(window, out DockZoneViewModel? root))
                {
                    return false;
                }

                if (root.IsActivelyAttemptingClose)
                {
                    return false;
                }

                // Route HideDontClose panels through RemoveOrHidePanel so they become hidden (not destroyed) and the
                // UISyncVM toggle state stays correct. Mark this window as closing silently first so AfterPanelHidden
                // won't try to re-close it when the last hideable panel is removed.
                m_windowsToCloseSilently.Add(window);

                // Snapshot the view model subtree NOW: RequestCloseAllAndClear below dismantles the tree (children get
                // detached, Parent nulled), so every zone must be captured up front to sever its Manager/Parent
                // afterward. A walk from root would only reach the root by then.
                List<DockZoneViewModel> closingZoneVMs = TreeTraversal<DockZoneViewModel>.All(root).ToList();

                List<DockingContentAdapterModel> allAdapters = closingZoneVMs.SelectMany(z => z.DockedContent).ToList();
                List<DockingContentAdapterModel> hideable = allAdapters.Where(a => a.HideDontClose).ToList();
                foreach (DockingContentAdapterModel adapter in hideable)
                {
                    this.UISyncVM.CloseOrHidePanel(adapter);
                }

                // Close remaining (non-hideable) panels normally
                Result<List<DockingContentAdapterModel>> closeResult = root.RequestCloseAllAndClear();
                if (closeResult.HasErrors)
                {
                    return false;
                }

                // Drop the tearoff's root zone(s) from the registry too. Without this a closed tearoff's zone (and
                // everything referenced through it) stays rooted by the manager for its whole life, growing the
                // registry one entry per tearoff open/close cycle.
                if (m_zonesByWindow.TryGetValue(window, out List<TZone>? closingZones))
                {
                    this.DeRegisterRootDockZones(closingZones.ToArray());
                }

                // Terminal teardown of the (now dismantled) view model subtree captured above. Each zone is childless
                // at this point, so Teardown just severs its Manager/Parent/UI; the content was already closed and
                // disposed by RequestCloseAllAndClear.
                foreach (DockZoneViewModel vm in closingZoneVMs)
                {
                    vm.Teardown();
                }

                m_tearoffRootZones.Remove(window);
                m_zonesByWindow.Remove(window);
                this.StopTrackingWindow(window);
                this.DetachTearoffSubscriptions(window);
                if (this.RootWindow != null)
                {
                    this.ActivateWindow(this.RootWindow);
                }

                this.UISyncVM.RecountActiveInstances();
                return true;
            }
            finally
            {
                m_currentlyClosingWindows.Remove(window);
                m_windowsToCloseSilently.Remove(window);
            }
        }

        /// <summary>
        /// A tearoff's own close button: runs <see cref="DoTearoffWindowClose"/>, then closes the window itself, which
        /// the window close path leaves to the OS
        /// </summary>
        protected void CloseTearoffWindowOnRequest (TWindow window)
        {
            if (this.DoTearoffWindowClose(window))
            {
                // Mark as silent so the window's own closing handler won't re-enter and cancel, then close
                m_windowsToCloseSilently.Add(window);
                try
                {
                    this.DetachTearoffSubscriptions(window);
                    this.CloseWindow(window);
                }
                finally
                {
                    m_windowsToCloseSilently.Remove(window);
                }
            }
        }

        /// <summary>Unconditionally closes and deregisters a tearoff window (after a successful drop, say)</summary>
        protected void SilentlyForceCloseTearoffWindow (TWindow sourceWindow)
        {
            // A window already partway through closing (a user close hiding its last panel, say) is left to that close,
            // which still has its tree to finish with
            if (!this.IsWindowTracked(sourceWindow)
                || (sourceWindow == this.RootWindow)
                || m_windowsToCloseSilently.Contains(sourceWindow)
                || m_currentlyClosingWindows.Contains(sourceWindow))
            {
                return;
            }

            m_windowsToCloseSilently.Add(sourceWindow);
            try
            {
                // Deregister the tearoff's zone(s) AND tear down their view model trees (see DoTearoffWindowClose). The
                // trees are still intact here, this path closes no content first.
                if (m_zonesByWindow.TryGetValue(sourceWindow, out List<TZone>? closingZones))
                {
                    this.DeRegisterAndTeardownRootZones(closingZones.ToArray());
                }

                m_tearoffRootZones.Remove(sourceWindow);
                m_zonesByWindow.Remove(sourceWindow);
                this.StopTrackingWindow(sourceWindow);
                this.DetachTearoffSubscriptions(sourceWindow);
                this.CloseWindow(sourceWindow);
            }
            finally
            {
                m_windowsToCloseSilently.Remove(sourceWindow);
            }

            this.UISyncVM.RecountActiveInstances();
        }

        /// <summary>
        /// The root window closed: force closes every tearoff so none outlives the app, skipping the panels' close
        /// veto
        /// </summary>
        protected void CloseTearoffsForRootWindowClosed ()
        {
            this.CloseEveryTearoffSilently();
        }

        /// <summary>Before a layout loads: closes every tearoff, then force closes and clears every root zone</summary>
        protected void ClearForLoadingFromState ()
        {
            this.CloseEveryTearoffSilently();
            foreach (TZone root in m_rootZones)
            {
                this.GetZoneViewModel(root)?.ForceCloseAllAndClear();
            }
        }

        /// <summary>
        /// Clears the layout and rebuilds it from a layout file: each root group's zones, then a tearoff window per saved
        /// tearoff. <see cref="IsLoadingFromLayout"/> is true throughout. False (and logged) when the file can't be read.
        /// </summary>
        [RequiresUnreferencedCode(kLayoutPanelTypeMessage)]
        protected virtual bool ResetLayoutFromFile (string filePath)
        {
            try
            {
                Logger.LogInfo($"Docking: Loading state from '{filePath ?? "<null>"}'");
                using Stream? stream = DockLayoutFile.OpenRead(filePath);
                if (stream == null)
                {
                    return false;
                }

                bool result = this.ResetLayoutFromStream(stream);
                Logger.LogInfo($"Docking: Load state complete, result={result}");
                return result;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Writes the root window's zones by group, and every open tearoff, to a layout file</summary>
        protected virtual bool SaveLayoutToFile (string? filePath)
        {
            var contents = new DockLayoutFile.Contents
            {
                Core = this.BuildCoreLayout(),
                Ancillary = this.BuildTearoffLayouts().ToArray(),
            };

            return DockLayoutFile.Write(filePath, contents);
        }

        /// <summary>Saves the layout per <see cref="AutoSaveMethod"/>, unless a layout is loading or the startup load has not finished</summary>
        protected void TriggerLayoutAutoSave ()
        {
            if (this.IsLoadingFromLayout || !m_isReadyToTrackAutoSave)
            {
                return;
            }

            if (this.AutoSaveMethod == eDockingAutoSaveMethod.AutoSaveOnAllChanges)
            {
                this.SaveDockLayoutToPersistentStorage();
            }
            else if (this.AutoSaveMethod == eDockingAutoSaveMethod.AutoSaveToTemp)
            {
                this.SaveDockLayoutToFile(this.GenerateDefaultStateStorageAutoSaveTempPath());
            }
        }

        // ===========[ Helper Methods ]========================================
        private List<TWindow> GetTearoffWindows ()
            => m_zonesByWindow.Keys.Where(w => w != this.RootWindow).ToList();

        // Closes every tearoff without asking its panels, deregistering its zones and tearing down their trees. Every
        // path that closes all tearoffs at once (CloseAll, the root window closing, a layout load) comes through here,
        // so none of them leaves a closed tearoff's zone registered for zone search to find.
        private void CloseEveryTearoffSilently ()
        {
            List<TWindow> tearoffs = this.GetTearoffWindows();
            m_windowsToCloseSilently.AddRange(tearoffs);
            foreach (TWindow window in tearoffs)
            {
                // The trees are still intact here, nothing closed them first
                if (m_zonesByWindow.TryGetValue(window, out List<TZone>? closingZones))
                {
                    this.DeRegisterAndTeardownRootZones(closingZones.ToArray());
                }

                m_tearoffRootZones.Remove(window);
                m_zonesByWindow.Remove(window);
                this.StopTrackingWindow(window);
                this.DetachTearoffSubscriptions(window);
                this.CloseWindow(window);
            }

            m_windowsToCloseSilently.Clear();
            if (tearoffs.Count > 0)
            {
                this.UISyncVM.RecountActiveInstances();
            }
        }

        // Deregister AND permanently tear down a set of closing tearoff root zones: drops them from the registry, then
        // severs Manager/Parent/UI on every zone view model in their subtrees so a surviving zone (one whose UI outlives
        // it, say) can't keep this manager rooted once it is disposed. Use this ONLY for genuine closes: re-homing a zone
        // to another manager must call plain DeRegisterRootDockZones, since its tree is about to move. The trees must
        // still be intact (no prior ForceClose/RequestClose) so Teardown can walk them; DoTearoffWindowClose, which
        // closes first, tears down separately.
        private void DeRegisterAndTeardownRootZones (params TZone[] closingZones)
        {
            List<DockZoneViewModel> closingZoneVMs = closingZones
                .Select(z => z != null ? this.GetZoneViewModel(z) : null)
                .Where(vm => vm != null)
                .Select(vm => vm!)
                .ToList();

            this.DeRegisterRootDockZones(closingZones);

            foreach (DockZoneViewModel vm in closingZoneVMs)
            {
                // A post-drop close (SilentlyForceCloseTearoffWindow) can hand over a source whose view model was
                // transplanted into the live drop target by a directional split drop, so it now has a parent. Tearing
                // that down would destroy the drop target, so only tear down genuine abandoned roots (a real tearoff
                // root is parentless, and so is an emptied tab-add source).
                if (vm.Parent == null)
                {
                    vm.Teardown();
                }
            }
        }

        [RequiresUnreferencedCode(kLayoutPanelTypeMessage)]
        private bool ResetLayoutFromStream (Stream stream)
        {
            try
            {
                DockZoneViewModel? defaultLoadZone = this.GetFallbackRootZone();
                if (defaultLoadZone == null)
                {
                    Logger.LogError("Dock Loading Error: No root zones are registered");
                    return false;
                }

                DockLayoutFile.Contents? contents = DockLayoutFile.Read(stream);
                if (contents == null)
                {
                    return false;
                }

                this.ClearForLoadingFromState();

                if (contents.Core != null)
                {
                    foreach (string groupId in contents.Core.ZoneInfoByRoot.Keys)
                    {
                        DockZoneViewModel? zone = this.GetRootZoneForGroup(groupId);
                        if (zone == null)
                        {
                            Logger.LogError($"Dock Loading Error: No zone for group id '{groupId}' - falling back to default zone");
                            zone = defaultLoadZone;
                        }

                        this.ApplyZoneLayout(zone, contents.Core.ZoneInfoByRoot[groupId]);
                    }

                    foreach (DockLayoutFile.TearoffLayout tearoff in contents.Ancillary ?? Array.Empty<DockLayoutFile.TearoffLayout>())
                    {
                        if (tearoff.State == null)
                        {
                            continue;
                        }

                        var zone = new DockZoneViewModel(this);
                        this.ApplyZoneLayout(zone, tearoff.State);

                        // The file keeps placement bounds; creation may take other units
                        var bounds = new DockWindowBounds(tearoff.WindowX, tearoff.WindowY, tearoff.WindowWidth, tearoff.WindowHeight);
                        Result<TWindow> windowResult = this.CreateAndStockTearoffWindow(zone, this.ToTearoffCreationBounds(bounds));
                        if (windowResult.HasErrors)
                        {
                            Logger.LogError("Dock Loading Error: Failed to create ancillary window from state");
                            Logger.LogError(windowResult.GetErrorReport());
                            continue;
                        }

                        if (tearoff.IsFullscreen || tearoff.IsMaximized)
                        {
                            this.ApplyWindowLayoutState(windowResult.Value, new DockWindowLayoutState(bounds, tearoff.IsMaximized, tearoff.IsFullscreen));
                        }
                    }
                }

                this.CleanZoneLayoutHierarchies();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in docking serialization", ex);
                return false;
            }
        }

        // Builds a panel a layout file names, by its registered type id or else its full type name, and hands it
        // the state the file saved for it
        [RequiresUnreferencedCode(kLayoutPanelTypeMessage)]
        private DockingContentAdapterModel? BuildDisplayAdapterFromLayout (DockDisplaySerializationData displayData)
        {
            IDockableDisplayElement? display = null;
            if (TypeIdRegistrar.TryGetType(displayData.TypeId, out Type type))
            {
                display = this.BuildNewDisplayElement(type);
            }
            else
            {
                type = Type.GetType(displayData.TypeId)!;
                if (type != null)
                {
                    display = this.BuildNewDisplayElement(type);
                }
            }

            if (display == null)
            {
                Logger.LogError($"Dock Loading Error: Cannot find type for id '{displayData.TypeId}'");
                return null;
            }

            if (displayData.State != null)
            {
                display.ApplyState(displayData.State);
            }

            return display.DockingAdapter;
        }

        private DockLayoutFile.CoreLayout BuildCoreLayout ()
        {
            var core = new DockLayoutFile.CoreLayout();
            if (m_zonesByWindow.TryGetValue(this.RootWindow, out List<TZone>? rootZones))
            {
                foreach (TZone rootZone in rootZones)
                {
                    string groupId = this.GetZoneGroupId(rootZone) ?? string.Empty;
                    if (!core.ZoneInfoByRoot.TryAdd(groupId, this.GetZoneViewModel(rootZone)!.GenerateSerializationState()))
                    {
                        Logger.LogError($"Docking Serialization: Duplicate root zone group id '{groupId}' - serialization skipped for that zone");
                    }
                }
            }

            return core;
        }

        private IEnumerable<DockLayoutFile.TearoffLayout> BuildTearoffLayouts ()
        {
            // Snapshot the registered windows so the iteration is stable. Skip the root window, and any window already
            // gone (closed, or in teardown)
            foreach (KeyValuePair<TWindow, List<TZone>> windowZones in m_zonesByWindow.ToList())
            {
                if (windowZones.Key == this.RootWindow)
                {
                    continue;
                }

                if (windowZones.Value.Count == 0 || !this.TryCaptureWindowLayoutState(windowZones.Key, out DockWindowLayoutState state))
                {
                    continue;
                }

                yield return new DockLayoutFile.TearoffLayout
                {
                    WindowX = (int)state.Bounds.X,
                    WindowY = (int)state.Bounds.Y,
                    WindowWidth = (int)state.Bounds.Width,
                    WindowHeight = (int)state.Bounds.Height,
                    IsMaximized = state.IsMaximized,
                    IsFullscreen = state.IsFullscreen,
                    State = windowZones.Value.FirstOrDefault() is TZone zone ? this.GetZoneViewModel(zone)?.GenerateSerializationState() : null,
                };
            }
        }

        // Conditionally close a tearoff window only when all docked content has been removed
        private void CloseSourceTearoffIfEmpty (TWindow sourceWindow, TZone sourceDockZone)
        {
            bool hasContent = this.EnumerateZoneTree(sourceDockZone)
                .Any(z => this.GetZoneViewModel(z)?.DockedContent.Count > 0);

            if (!hasContent)
            {
                this.SilentlyForceCloseTearoffWindow(sourceWindow);
            }
        }

        private IDockableDisplayElement? SetupAndReturnNew (IDockableDisplayElement? displayElement, DockPanelRegistrationRules rules = default)
        {
            if (displayElement == null)
            {
                return null;
            }

            var adapter = new DockingContentAdapterModel(this);
            adapter.TitleContent = StringXT.ConvertToFriendlyEn(displayElement.GetType().Name);

            // Apply factory defaults before panel Setup so Setup can override
            if (rules.DefaultIsClosable.HasValue)
            {
                adapter.IsClosable = rules.DefaultIsClosable.Value;
            }

            if (rules.DefaultHideDontClose.HasValue)
            {
                adapter.HideDontClose = rules.DefaultHideDontClose.Value;
            }

            if (rules.DefaultCanTearoff.HasValue)
            {
                adapter.CanTearoff = rules.DefaultCanTearoff.Value;
            }

            displayElement.Setup(adapter);
            displayElement.DockingAdapter.Display = displayElement;
            return displayElement;
        }

        private static string CreateDefaultLayoutPath (string uniqueManagerId)
            => Path.Combine(AppStorage.AppDataRoot!, "Layouts", $"{AJut.IO.PathHelpers.SanitizeFileName(uniqueManagerId)}.layout");

        private static IDockableDisplayElement? BuildDefaultDisplayFor ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type elementType)
            => AJutActivator.CreateInstanceOf(elementType) as IDockableDisplayElement;

        // ===========[ Subclasses/structs ]====================================
        private sealed record DisplayBuilder (DockPanelRegistrationRules Rules, Func<IDockableDisplayElement?> Builder);
    }
}
