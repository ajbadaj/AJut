// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AJutShowRoomWinUI
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using AJut.Text.AJson;
    using System.Threading.Tasks;
    using AJut;
    using AJut.Storage;
    using AJut.Tree;
    using AJut.TypeManagement;
    using AJut.UX;
    using AJut.UX.Docking;
    using AJut.UX.PropertyInteraction;
    using AJut.UX.Theming;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Windows.Storage;
    using Windows.UI;
    using System.ComponentModel;

    public sealed partial class MainWindow : Window
    {
        private const string kThemeColorsSettingsKey = "_Hidden_theme_colors";
        private readonly ObservableCollection<string> m_themeColorResolver = new ObservableCollection<string>();
        private int m_selectionChangedReproCount;

        // ===========[ Dock Zone test state ]============================================
        private DockingManager m_dockingManager;
        private bool m_dockZoneInitialized = false;
        private bool m_useCaseInitialized = false;

        public MainWindow (WindowManager manager, AppThemeManager themeManager)
        {
            this.Application = App.Instance;
            manager.Setup(this);
            themeManager.ApplyTheme(this);
            this.AppWindow.SetIcon("Assets/app.ico");
            this.InitializeComponent();
            this.Root.SetupFor(this);
            this.TestPropertyGrid.PropertyTreeChanged += this.TestPropertyGrid_OnPropertyTreeChanged;
            this.TestPropertyGrid.SelectedSourceObjectChanged += this.TestPropertyGrid_OnSelectedSourceObjectChanged;
            this.SetPGSource(m_alphaObj);

            // ToggleStrip demo items
            this.ToggleStripSingle.ItemsSource = new[] { "Option A", "Option B", "Option C" };
            this.ToggleStripMulti.ItemsSource = new[] { "Bold", "Italic", "Underline", "Strikethrough" };
            this.ToggleStripFreeform.ItemsSource = new[] { "First", "Second", "Third" };
            this.ToggleStripCustom.ItemsSource = new[] { "Red", "Green", "Blue" };
            this.ToggleStripUniform.ItemsSource = new[] { "S", "Medium", "Extra Large" };
            this.ToggleStripOverflow.ItemsSource = new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven" };
            this.ToggleStripScroll.ItemsSource = new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven" };

            // ToggleStrip enum bug repro
            this.ToggleStripEnumBugRepro.ItemsSource = Enum.GetValues<eEditorMode>();

            // ToggleStrip SelectionChanged deselect repros (multi-select + single-select-with-none, shared counter)
            this.ToggleStripSelectionChangedRepro.ItemsSource = new[] { "One", "Two", "Three", "Four" };
            this.ToggleStripSingleSelectNoneRepro.ItemsSource = new[] { "One", "Two", "Three" };

            this.StockBumpStackDemos();

            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(800, 1000));

            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.TryGetValue(kThemeColorsSettingsKey, out object themeColorsObj) && themeColorsObj is string[] strs)
            {
                m_themeColorResolver.AddEach(strs);
            }
            else
            {
                m_themeColorResolver.Add("SystemAccentColor");
                m_themeColorResolver.Add("TabViewItemHeaderBackground");
                m_themeColorResolver.Add("TabViewItemHeaderBackgroundPointerOver");
                this.SaveThemeColors();
            }

            if (this.ThemeColorsList != null)
            {
                this.ThemeColorsList.ItemsSource = m_themeColorResolver;
            }
            else
            {
                Logger.LogError("ThemeColorList is null");
            }
        }


        private void StockBumpStackDemos ()
        {
            string[] words = { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten" };
            for (int i = 0; i < words.Length; ++i)
            {
                this.BumpStackHorizontal.Children.Add(MakeBumpChip(words[i], i, vertical: false));
                this.BumpStackClip.Children.Add(MakeBumpChip(words[i], i, vertical: false));
            }

            for (int i = 0; i < 8; ++i)
            {
                this.BumpStackVertical.Children.Add(MakeBumpChip($"Row {i + 1}", i, vertical: true));
            }
        }

        private static Border MakeBumpChip (string text, int index, bool vertical)
        {
            byte tint = (byte)(70 + ((index * 22) % 150));
            return new Border
            {
                Width = vertical ? 130 : 70,
                Margin = new Thickness(3),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(0x55, tint, 0x90, 0xC8)),
                Child = new TextBlock
                {
                    Text = text,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 4),
                },
            };
        }

        public App Application { get; }

        // ===========[ ToggleStrip enum bug repro ]================================
        public eEditorMode SelectedEditorMode { get; set; } = eEditorMode.Text;

        // ===========[ EnumToggleStrip showcase ]====================================
        public eDisplayMode DisplayMode { get; set; } = eDisplayMode.Standard;
        public eToppings Toppings { get; set; } = eToppings.Cheese | eToppings.Pepperoni;
        public eExclusionDemo ExclusionPick { get; set; } = eExclusionDemo.Visible1;

        private void EnumBugRepro_OnSetNumber (object sender, RoutedEventArgs e)
        {
            this.SelectedEditorMode = eEditorMode.Number;
            this.ToggleStripEnumBugRepro.SelectedItem = eEditorMode.Number;
            this.EnumBugReproStatus.Text = $"Set to Number. SelectedItem = {this.ToggleStripEnumBugRepro.SelectedItem}";
        }

        private void EnumBugRepro_OnSetColor (object sender, RoutedEventArgs e)
        {
            this.SelectedEditorMode = eEditorMode.Color;
            this.ToggleStripEnumBugRepro.SelectedItem = eEditorMode.Color;
            this.EnumBugReproStatus.Text = $"Set to Color. SelectedItem = {this.ToggleStripEnumBugRepro.SelectedItem}";
        }

        private void EnumBugRepro_OnSetText (object sender, RoutedEventArgs e)
        {
            this.SelectedEditorMode = eEditorMode.Text;
            this.ToggleStripEnumBugRepro.SelectedItem = eEditorMode.Text;
            this.EnumBugReproStatus.Text = $"Set to Text. SelectedItem = {this.ToggleStripEnumBugRepro.SelectedItem}";
        }

        private void ToggleStripSelectionChangedRepro_OnSelectionChanged (object sender, AJut.UX.Controls.ToggleStrip.ToggleStripSelectionChangedEventArgs e)
        {
            ++m_selectionChangedReproCount;
            string current = string.Join(", ", e.CurrentSelection.OfType<object>());
            this.ToggleStripSelectionChangedStatus.Text = $"SelectionChanged fired {m_selectionChangedReproCount} times - current: [{current}]";
        }

        private void AddThemeColor(string themeColor)
        {
            while (m_themeColorResolver.Contains(themeColor))
            {
                m_themeColorResolver.Remove(themeColor);
            }

            m_themeColorResolver.Insert(0, themeColor);
            this.ThemeColorsList.SelectedIndex = 0;
            while (m_themeColorResolver.Count > 10)
            {
                m_themeColorResolver.RemoveAt(10);
            }

            this.SaveThemeColors();
        }

        private void SaveThemeColors()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[kThemeColorsSettingsKey] = m_themeColorResolver.ToArray();
        }

        private async void ThemeResolver_OnClick(object sender, RoutedEventArgs e)
        {
            if (await _DebugWriteBrushInfo(this.SpecificThemeText.Text))
            {
                this.SpecificThemeText.Text = "";
            }

            async Task<bool> _DebugWriteBrushInfo(string resourceName)
            {
                string debugText;
                bool result;

                if (this.Root.TryFindThemedResource(resourceName, out object obj))
                {
                    result = true;
                    string value;
                    if (obj is SolidColorBrush scb)
                    {
                        value = CoerceUtils.GetSmallestHexString(scb.Color);
                        this.ColorPicker.Color = scb.Color;
                    }
                    else if (obj is Color color)
                    {
                        value = CoerceUtils.GetSmallestHexString(color);
                        this.ColorPicker.Color = color;
                    }
                    else
                    {
                        value = obj.ToString();
                    }
                    
                    debugText = $"Resolved resource '{resourceName}' of type '{obj.GetType().FullName}' - value: {value}";
                }
                else
                {
                    result = false;
                    debugText = $"FAILED to resolve '{resourceName}'";
                }

                Logger.LogInfo(debugText);
                if (result)
                {
                    this.AddThemeColor(resourceName);
                }

                ContentDialog myDialog = new ContentDialog
                {
                    Title = "Resource Search Results",
                    Content = debugText,
                    CloseButtonText = "OK"
                };

                myDialog.XamlRoot = this.SpecificThemeText.XamlRoot;
                await myDialog.ShowAsync();
                return result;
            }
        }

        private void ThemeColorsList_OnDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe && fe.DataContext is string context)
            {
                this.SpecificThemeText.Text = context;
            }
        }

        // ===========[ Flat Tree / Property Grid properties ]============================

        public ShowRoomTreeNode TreeRoot { get; } = ShowRoomTreeNode.Build();
        public ShowRoomTreeNode DragDropTreeRoot { get; } = ShowRoomTreeNode.BuildDragDropDemo();

        // Two independent source trees of the same shape - one rendered Forward, one Reversed -
        // so we can eyeball the layers-panel ordering and confirm drag-drop InsertIndex stays source-space.
        public ShowRoomTreeNode SiblingOrderForwardRoot { get; } = ShowRoomTreeNode.BuildSiblingOrderDemo();
        public ShowRoomTreeNode SiblingOrderReversedRoot { get; } = ShowRoomTreeNode.BuildSiblingOrderDemo();

        private void SiblingOrderTree_OnReorder (object sender, FlatTreeReorderEventArgs e)
        {
            var control = sender as AJut.UX.Controls.FlatTreeListControl;
            string parentName = e.TargetParent is ShowRoomTreeNode parent ? parent.NodeName : "(null)";
            string items = string.Join(", ", e.Items.OfType<ShowRoomTreeNode>().Select(n => n.NodeName));
            string line = $"[{DateTime.Now:HH:mm:ss}] -> '{parentName}' InsertIndex={e.InsertIndex} (source) | items=[{items}]" + Environment.NewLine;

            TextBox log = control == this.SiblingOrderReversedTree
                ? this.SiblingOrderReversedLog
                : this.SiblingOrderForwardLog;
            log.Text = line + log.Text;
        }

        // ===========[ SetSelection sandbox scenario ]===========================
        // Exercises FlatTreeListControl.SetSelection on an Extended-mode tree.
        // Expected: all requested rows highlight (4px accent strip), and
        // SelectedItems.Count matches the requested set.
        private void SetSelectionScattered_OnClick (object sender, RoutedEventArgs e)
        {
            var items = this.DragDropTree.Items
                .Where(i => i.Source is ShowRoomTreeNode node
                    && (node.NodeName == "Camera" || node.NodeName == "Textures" || node.NodeName == "GameManager"))
                .ToArray();
            this.DragDropTree.SetSelection(items);
        }

        private void SetSelectionScenesDescendants_OnClick (object sender, RoutedEventArgs e)
        {
            // Select every descendant of the "Scenes" node - expected to be several rows.
            var scenes = this.DragDropTree.Items
                .FirstOrDefault(i => i.Source is ShowRoomTreeNode node && node.NodeName == "Scenes");
            if (scenes == null)
            {
                return;
            }

            var toSelect = this.DragDropTree.Items
                .Where(i => i.Parent == scenes || (i.Parent != null && i.Parent.Parent == scenes))
                .ToArray();
            this.DragDropTree.SetSelection(toSelect);
        }

        private void SetSelectionEmpty_OnClick (object sender, RoutedEventArgs e)
        {
            this.DragDropTree.SetSelection(Array.Empty<FlatTreeItem>());
        }

        private void DragDropTree_OnSelectionChanged (object sender, SelectionChange<FlatTreeItem> e)
        {
            if (this.SetSelectionStatus != null)
            {
                this.SetSelectionStatus.Text = $"SelectedItems.Count = {this.DragDropTree.SelectedItems.Count}";
            }
        }

        // Source swap test objects.
        // ShowRoomAlpha (5 props) → ShowRoomBeta (2 props) replicates an external scenario:
        //   - same float property names (X, Y) but very different values so mismatch is obvious
        //   - count difference (5→2) forces WinUI3 container recycling
        //   - Alpha has String+Bool rows that vanish in Beta; if they persist, the bug is present
        private readonly ShowRoomAlpha m_alphaObj = new ShowRoomAlpha();
        private readonly ShowRoomBeta m_betaObj = new ShowRoomBeta();
        private readonly ShowRoomTester m_complexObj = new ShowRoomTester();
        // Second instance of the SAME type as m_complexObj. Used to test the property grid's ephemeral
        // expansion memory: expand a sub-object / group on Complex A, switch to Complex B, and the same
        // tree pathway should stay expanded so the two objects can be compared.
        private readonly ShowRoomTester m_complexObj2 = new ShowRoomTester { Name = "Complex B - same type, compare expansion" };
        // Delegating edit manager: wraps an inner object that carries a [PGButton] and surfaces it
        // itself, since the grid only auto-harvests buttons off the source it's handed. See
        // ButtonDelegationDemo.cs.
        private readonly ButtonDelegationEditManager m_delegatedButtonsObj = new ButtonDelegationEditManager();
        // Mixed ordering: [MemberOrder] properties + a [PGMemberOrder] button - exposes that the two
        // attributes don't sort on a shared axis (see MixedOrderButtonSource).
        private readonly MixedOrderButtonSource m_mixedOrderObj = new MixedOrderButtonSource();
        // Same mix, but built by hand (manual PETs) - positions every row via MemberSortOrder.
        private readonly ManualMixedOrderManager m_manualMixedOrderObj = new ManualMixedOrderManager();
        private readonly WrappedModeMatrixSource m_wrappedMatrixObj = new WrappedModeMatrixSource();
        private object m_currentPGTestObj;

        private void SetPGSource (object obj)
        {
            m_currentPGTestObj = obj;
            this.TestPropertyGrid.SingleItemSource = obj;
            this.ResetJsonDisplay();

            // Swapping the source rebuilds the tree and drops any selection - reset the readout to match.
            this.PGSelectionReadout.Text = "(none)";
        }

        private void PGSource_OnAlphaClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_alphaObj);
        private void PGSource_OnBetaClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_betaObj);
        private void PGSource_OnComplexClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_complexObj);
        private void PGSource_OnComplexBClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_complexObj2);
        private void PGSource_OnDelegatedButtonsClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_delegatedButtonsObj);
        private void PGSource_OnMixedOrderClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_mixedOrderObj);
        private void PGSource_OnManualMixedOrderClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_manualMixedOrderObj);
        private void PGSource_OnWrappedMatrixClicked (object sender, RoutedEventArgs e)
        {
            this.SetPGSource(m_wrappedMatrixObj);
            this.AttachMatrixDiagnostics();
        }

        // Directly poke the wrapper's Value - simulates the consumer scenario where an
        // enum-behind-a-wrapper changes without the outer object's INPC firing, and
        // several PGShowIf-gated rows have to re-evaluate in response.
        private void PGMatrix_SetModeA (object sender, RoutedEventArgs e) => this.CycleMatrixMode(eMatrixMode.A);
        private void PGMatrix_SetModeB (object sender, RoutedEventArgs e) => this.CycleMatrixMode(eMatrixMode.B);
        private void PGMatrix_SetModeC (object sender, RoutedEventArgs e) => this.CycleMatrixMode(eMatrixMode.C);

        // Used only by the matrix-mode diagnostic counters below so we can see whether
        // the chain wrapper -> OnElevatedSubObjectPropertyChanged -> SourceCommitted ->
        // PropertyGrid.OnAnyTargetPropertyChanged -> UpdateConditionalVisibility actually fires.
        private int m_matrixWrapperInpcCount;
        private int m_matrixTreeChangedCount;
        private bool m_matrixDiagnosticsAttached;

        private void AttachMatrixDiagnostics ()
        {
            if (m_matrixDiagnosticsAttached)
            {
                return;
            }

            m_matrixDiagnosticsAttached = true;
            m_wrappedMatrixObj.Mode.PropertyChanged += this.MatrixWrapper_OnPropertyChanged;
            // PropertyTreeChanged fires from PropertyGrid.OnAnyTargetPropertyChanged on any
            // SourceCommitted. If clicks increment the wrapper counter but not this one,
            // the wrapper INPC is reaching the wrapper but the elevated-subobject-to-
            // SourceCommitted bridge isn't firing or isn't being heard.
            this.TestPropertyGrid.PropertyTreeChanged += this.MatrixDiag_OnPropertyTreeChanged;
        }

        private void MatrixWrapper_OnPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            ++m_matrixWrapperInpcCount;
            this.UpdateMatrixDiagnosticLabel();
        }

        private void MatrixDiag_OnPropertyTreeChanged (object sender, EventArgs e)
        {
            ++m_matrixTreeChangedCount;
            this.UpdateMatrixDiagnosticLabel();
        }

        private void CycleMatrixMode (eMatrixMode mode)
        {
            m_wrappedMatrixObj.Mode.Value = mode;
            this.UpdateMatrixDiagnosticLabel();
        }

        private void UpdateMatrixDiagnosticLabel ()
        {
            if (this.PGMatrixDiagnostic == null)
            {
                return;
            }

            string liveChildren = "(null)";
            var root = this.TestPropertyGrid?.Manager?.RootNode;
            if (root != null)
            {
                liveChildren = string.Join(", ",
                    root.Children.OfType<PropertyEditTarget>().Select(t => t.PropertyPathTarget));
            }

            this.PGMatrixDiagnostic.Text = string.Format(
                "Mode.Value={0}  INPC={1}  TreeChanged={2}  root.Children=[{3}]",
                m_wrappedMatrixObj.Mode.Value,
                m_matrixWrapperInpcCount,
                m_matrixTreeChangedCount,
                liveChildren
            );
        }

        private void PGSource_OnSetElevatedClicked (object sender, RoutedEventArgs e)
        {
            // Externally change elevated property values on the complex object,
            // then re-set the source to trigger RecacheEditValue cascade.
            // Before the fix, the elevated child editors would show stale values.
            Random rng = new Random();
            m_complexObj.Value = rng.Next(1, 555);
            m_complexObj.SubObjWithElevation.SubObjValue = rng.Next(1, 9999);
            m_complexObj.WrappedFloat.Value = (float)Math.Round(rng.NextDouble() * 100.0, 1);
            this.SetPGSource(m_complexObj);
        }


        private void PGSource_OnNoneClicked(object sender, RoutedEventArgs e)
        {
            this.SetPGSource(null);
        }
        private void TestPropertyGrid_OnPropertyTreeChanged(object sender, EventArgs e)
        {
            this.ResetJsonDisplay();
        }

        // ------ Selection-surface tester wiring ------
        private void TestPropertyGrid_OnSelectedSourceObjectChanged (object sender, EventArgs e)
        {
            this.UpdatePGSelectionReadout();
        }

        private void UpdatePGSelectionReadout ()
        {
            object selected = this.TestPropertyGrid.SelectedSourceObject;
            this.PGSelectionReadout.Text = selected is ShowRoomSubObject sub
                ? $"ShowRoomSubObject (SubObjValue={sub.SubObjValue})"
                : selected?.ToString() ?? "(none)";
        }

        private void PGSelect_OnRandomElementClicked (object sender, RoutedEventArgs e)
        {
            // Complex A/B carry ListWithElevation (complex elements with identity). Selecting one by
            // reference drives the programmatic selection path - the one that hit the Single-mode
            // SelectedItems COMException, and (when the list node is collapsed) the expand-then-select path.
            if (this.TestPropertyGrid.SingleItemSource is ShowRoomTester tester && tester.ListWithElevation.Count > 0)
            {
                ShowRoomSubObject element = tester.ListWithElevation[new Random().Next(0, tester.ListWithElevation.Count)];
                if (!this.TestPropertyGrid.TrySelectSourceObject(element))
                {
                    this.PGSelectionReadout.Text = "(could not find a row for that element)";
                }
            }
            else
            {
                this.PGSelectionReadout.Text = "(switch to Complex A or B - it has the element list)";
            }
        }

        private void PGSelect_OnClearClicked (object sender, RoutedEventArgs e)
        {
            this.TestPropertyGrid.SelectedTarget = null;
        }
        private void ResetJsonDisplay()
        {
            if (m_currentPGTestObj == null)
            {
                return;
            }

            try
            {
                Json json = JsonHelper.BuildJsonForObject(m_currentPGTestObj);
                this.PropertyGridJsonDisplay.Text = json.HasErrors ? json.GetErrorReport() : json.ToString();
            }
            catch (Exception ex)
            {
                this.PropertyGridJsonDisplay.Text = $"[Serialization error]\n{ex.Message}";
            }
        }

        // ===========[ Dock Zone tab ]====================================================

        private void DockZoneTab_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (m_dockZoneInitialized)
            {
                return;
            }

            m_dockZoneInitialized = true;

            m_dockingManager = new DockingManager(this, "ShowRoomDock");
            m_dockingManager.FixedTearoffWindowTitle = "Show Room";
            m_dockingManager.RegisterMainWindowRootDockZones(this.TestDockZone);
            m_dockingManager.RegisterDisplayFactory<ShowRoomPanel>();
            m_dockingManager.RegisterDisplayFactory<SingleInstanceShowRoomPanel>(new DockPanelRegistrationRules {
                SingleInstanceOnly = true,
                GuaranteedOnStart = true,
                SpawnWidth = 200,
                SpawnHeight = 500,
            });
            m_dockingManager.RegisterDisplayFactory<HiddenInfraShowRoomPanel>(new DockPanelRegistrationRules {
                SingleInstanceOnly = true,
                IsHiddenFromToolbar = true,
                IsHiddenFromMenu = true,
            });

            m_dockingManager.UISyncVM.RegisterPanelDisplayOverride<SingleInstanceShowRoomPanel>("Single Inst", "Assets/PenguinExample.png");
            this.DockToolbar.DockingManager = m_dockingManager;
            m_dockingManager.ManageMenu(this.ViewMenu);

            var zone = m_dockingManager.FindFirstAvailableDockZone();
            if (zone != null)
            {
                var panelA = m_dockingManager.DockNewPanel<ShowRoomPanel>(zone);
                panelA.Label = "Panel A";
                panelA.DockingAdapter.TitleContent = "Panel A";

                var panelB = m_dockingManager.DockNewPanel<ShowRoomPanel>(zone);
                panelB.Label = "Panel B";
                panelB.DockingAdapter.TitleContent = "Panel B";

                // Hidden infra panel - always present in layout, never in menu/toolbar
                m_dockingManager.DockNewPanel<HiddenInfraShowRoomPanel>(zone);
            }
        }

        // ===========[ Use-Case Leak Test tab ]==========================================

        private void UseCaseTab_OnLoaded (object sender, RoutedEventArgs e)
        {
            if (m_useCaseInitialized)
            {
                return;
            }

            m_useCaseInitialized = true;

            // Pass this window through as the nav parameter so the editor's DockingManager roots to
            // the real root window (the editor lives inside this Frame, like a host editor page).
            this.UseCaseFrame.Navigate(typeof(UseCaseTest.UseCaseLandingPage), this);
        }

        private void DockZone_OnSaveLayoutClicked(object sender, RoutedEventArgs e)
        {
            m_dockingManager?.SaveDockLayoutToPersistentStorage();
        }

        private void DockZone_OnLoadLayoutClicked(object sender, RoutedEventArgs e)
        {
            m_dockingManager?.ReloadDockLayoutFromPersistentStorage();
        }

        private void DockZone_OnSetAllValuesTestClicked(object sender, RoutedEventArgs e)
        {
            Random rng = new Random(DateTime.Now.Second);
            double newValue = rng.NextDouble();
            foreach (var display in m_dockingManager.EnumerateDisplays())
            {
                if (display is ShowRoomPanel panel)
                {
                    panel.Value = newValue;
                }
            }
        }

        // ===========[ Leak Probe ]======================================================

        private void LeakProbe_OnDockingManagerClicked (object sender, RoutedEventArgs e)
        {
            bool collected = LeakProbe_BuildAndDisposeDockingManager(this);
            this.LeakProbe_AppendOutput("Probe DockingManager", collected
                ? "PASS - DockingManager was collected after Dispose"
                : "FAIL - DockingManager survived Dispose + GC (something is still pinning it)");
        }

        private void LeakProbe_OnWindowManagerClicked (object sender, RoutedEventArgs e)
        {
            bool collected = LeakProbe_BuildAndDisposeWindowManager(this);
            this.LeakProbe_AppendOutput("Probe WindowManager", collected
                ? "PASS - WindowManager was collected after Dispose"
                : "FAIL - WindowManager survived Dispose + GC (something is still pinning it)");
        }

        private void LeakProbe_OnCycleClicked (object sender, RoutedEventArgs e)
        {
            int passes = 0;
            for (int i = 0; i < 5; ++i)
            {
                if (LeakProbe_BuildAndDisposeDockingManager(this))
                {
                    ++passes;
                }
            }

            this.LeakProbe_AppendOutput("Probe x5 (open/close cycles)", passes == 5
                ? "PASS - all 5 cycles collected cleanly"
                : $"FAIL - only {passes}/5 cycles collected (later iterations should still pass if the first one does)");
        }

        // Build a real DockingManager against the live root window, dispose it, drop the
        // strong ref, and check the WeakReference. Static so the local `manager` variable
        // is the only place a strong ref lives - the JIT sometimes keeps locals from the
        // enclosing instance method alive longer than expected.
        private static bool LeakProbe_BuildAndDisposeDockingManager (Window root)
        {
            WeakReference weak = LeakProbe_BuildAndDisposeDockingManager_Inner(root);
            return LeakProbe_ConfirmCollected(weak);
        }

        private static WeakReference LeakProbe_BuildAndDisposeDockingManager_Inner (Window root)
        {
            var manager = new DockingManager(root, "leak-probe-" + Guid.NewGuid().ToString("N"));
            var weak = new WeakReference(manager);
            manager.Dispose();
            return weak;
        }

        private static bool LeakProbe_BuildAndDisposeWindowManager (Window root)
        {
            WeakReference weak = LeakProbe_BuildAndDisposeWindowManager_Inner(root);
            return LeakProbe_ConfirmCollected(weak);
        }

        private static WeakReference LeakProbe_BuildAndDisposeWindowManager_Inner (Window root)
        {
            var manager = new WindowManager();
            manager.Setup(root);
            var weak = new WeakReference(manager);
            manager.Dispose();
            return weak;
        }

        private static bool LeakProbe_ConfirmCollected (WeakReference weak)
        {
            // Two pass GC + finalizer wait. Single pass usually does it but second pass
            // catches anything that pulled itself off the finalizer queue on the way out.
            for (int i = 0; i < 2; ++i)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            return !weak.IsAlive;
        }

        // ===========[ Leak Probe - Docking host scenario ]==============================
        // Models the ARC-56 residual: an editor whose layout is a DockingManager lives inside
        // a transient child Window's Frame, while the manager's RootWindow is the long-lived
        // main window. After the child window closes and the manager is disposed, the dock
        // zone, docked panels, and anything bound to the editor page must all collect. The
        // main-window variant is the control case - hosting the same graph in the main window
        // tree, which the prior m_dockZoneMapping fix already collects cleanly.

        private async void LeakProbe_OnChildWindowDockingClicked (object sender, RoutedEventArgs e)
        {
            const int kCycles = 4;
            var button = sender as Button;
            if (button != null) { button.IsEnabled = false; }
            try
            {
                var weaks = new List<DockingLeakProbeWeaks>();
                for (int i = 0; i < kCycles; ++i)
                {
                    var probe = LeakProbe_BuildChildWindowDocking(this);

                    // Wait until the page actually loads so the dock layout realizes (template ->
                    // drop overlay + DockLeafLayout). Without this we may measure a window that
                    // closed before it ever built anything - a vacuous pass. builtLeaves in the
                    // report proves the structure was really there.
                    await LeakProbe_WaitForLoaded(probe.EditorPage, 3000);
                    await Task.Delay(200);

                    int leaves = LeakProbe_CountDescendants<AJut.UX.Controls.DockLeafLayout>(probe.RootZone);

                    // Hold a watch handle to the page so we can wait for its real unload after
                    // teardown nulls every probe-held strong ref.
                    FrameworkElement pageWatch = probe.EditorPage;

                    var weak = LeakProbe_TeardownDockingProbe(probe);
                    weak.BuiltLeaves = leaves;
                    weaks.Add(weak);
                    probe = null;

                    // Deterministic read: wait for WinUI to actually unload the page (it releases
                    // native / container refs during the Unloaded pass), then let deferred cleanup
                    // run, before we collect and measure.
                    await LeakProbe_WaitForUnloaded(pageWatch, 3000);
                    pageWatch = null;
                    await Task.Delay(150);
                }

                await LeakProbe_SettleAndCollectAsync();
                this.LeakProbe_AppendOutput("Dock leak - child window", LeakProbe_ReportSurvivors(weaks));
            }
            finally
            {
                if (button != null) { button.IsEnabled = true; }
            }
        }

        private async void LeakProbe_OnMainWindowDockingClicked (object sender, RoutedEventArgs e)
        {
            const int kCycles = 4;
            var button = sender as Button;
            if (button != null) { button.IsEnabled = false; }
            try
            {
                var weaks = new List<DockingLeakProbeWeaks>();
                for (int i = 0; i < kCycles; ++i)
                {
                    var probe = this.LeakProbe_BuildMainWindowDocking();

                    await LeakProbe_WaitForLoaded(probe.EditorPage, 3000);
                    await Task.Delay(200);

                    int leaves = LeakProbe_CountDescendants<AJut.UX.Controls.DockLeafLayout>(probe.RootZone);

                    FrameworkElement pageWatch = probe.EditorPage;

                    var weak = LeakProbe_TeardownDockingProbe(probe);
                    weak.BuiltLeaves = leaves;
                    weaks.Add(weak);
                    probe = null;

                    await LeakProbe_WaitForUnloaded(pageWatch, 3000);
                    pageWatch = null;
                    await Task.Delay(150);
                }

                await LeakProbe_SettleAndCollectAsync();
                this.LeakProbe_AppendOutput("Dock leak - main window (control)", LeakProbe_ReportSurvivors(weaks));
            }
            finally
            {
                if (button != null) { button.IsEnabled = true; }
            }
        }

        // Deterministic mechanism check for the drop-overlay back-ref fix. The native-peer pin that
        // makes the leak bite cannot be reproduced headless, but the FIX is exact and testable: after
        // DockingManager.Dispose, every DockDropInsertionDriverWidget must have had its InsertionZone
        // back-ref nulled and been detached from the zone's outer grid. This builds a real docked graph
        // (so templates apply and the 5 widgets per zone exist), grabs them while live, disposes, and
        // asserts the sever. Fails before the fix (InsertionZone still points at the zone), passes after.
        private async void LeakProbe_OnDropOverlaySeveredClicked (object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null) { button.IsEnabled = false; }
            try
            {
                var probe = this.LeakProbe_BuildMainWindowDocking();

                // Wait for the page to load so each DockZone applies its template and BuildDropOverlay
                // builds the driver widgets - the things we are about to assert get severed.
                await LeakProbe_WaitForLoaded(probe.EditorPage, 3000);
                await Task.Delay(200);

                // Collect the driver widgets across the whole zone tree while they are still live in the
                // overlay. Hold strong refs so Dispose can't collect them out from under the assertion -
                // this probe checks the back-ref is nulled, not whether the widget itself collects.
                var widgets = LeakProbe_CollectDescendants<AJut.UX.Controls.DockDropInsertionDriverWidget>(probe.RootZone);
                int built = widgets.Count;
                int boundBefore = widgets.Count(w => w.InsertionZone != null);

                // The operation under test.
                probe.Manager.Dispose();

                int stillBound = widgets.Count(w => w.InsertionZone != null);
                int stillParented = widgets.Count(w => VisualTreeHelper.GetParent(w) != null);

                // Assertion done - drop the host parenting and strong refs.
                if (probe.MainHost != null) { probe.MainHost.Child = null; }
                if (probe.EditorPage is ContentControl page) { page.Content = null; }
                widgets.Clear();
                probe = null;

                string result;
                if (built == 0)
                {
                    result = "INCONCLUSIVE - no drop-overlay widgets realized (template never applied?)";
                }
                else if (stillBound == 0 && stillParented == 0)
                {
                    result = $"PASS - all {built} driver widgets severed (InsertionZone null) and detached after Dispose (boundBefore={boundBefore})";
                }
                else
                {
                    result = $"FAIL - after Dispose: {stillBound}/{built} still hold InsertionZone, {stillParented}/{built} still parented (boundBefore={boundBefore})";
                }

                this.LeakProbe_AppendOutput("Dock overlay severed (mechanism)", result);
            }
            finally
            {
                if (button != null) { button.IsEnabled = true; }
            }
        }

        // Builds the docking graph inside a fresh child Window (RootWindow stays the main window).
        private static DockingLeakProbe LeakProbe_BuildChildWindowDocking (Window mainRoot)
        {
            var childWindow = new Window();
            var frame = new Frame();
            var editorPage = LeakProbe_BuildEditorPageStandin(out var rootZone);

            frame.Content = editorPage;
            childWindow.Content = frame;
            childWindow.Activate();

            var probe = LeakProbe_StockDockingGraph(mainRoot, rootZone, editorPage);
            probe.ChildWindow = childWindow;
            probe.Frame = frame;
            probe.EditorPage = editorPage;
            return probe;
        }

        // Builds the same docking graph but hosts it in the main window's own visual tree.
        private DockingLeakProbe LeakProbe_BuildMainWindowDocking ()
        {
            var frame = new Frame();
            var editorPage = LeakProbe_BuildEditorPageStandin(out var rootZone);

            frame.Content = editorPage;
            this.LeakProbe_MainHostArea.Child = frame;

            var probe = LeakProbe_StockDockingGraph(this, rootZone, editorPage);
            probe.Frame = frame;
            probe.EditorPage = editorPage;
            probe.MainHost = this.LeakProbe_MainHostArea;
            return probe;
        }

        // The editor "page" stand-in: a ContentControl whose content is the root DockZone,
        // mirroring an editor Page whose layout root is an AJut dock zone.
        private static FrameworkElement LeakProbe_BuildEditorPageStandin (out AJut.UX.Controls.DockZone rootZone)
        {
            rootZone = new AJut.UX.Controls.DockZone { Name = "LeakProbeRootZone" };
            return new ContentControl
            {
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Content = rootZone,
            };
        }

        // Stands up a DockingManager against the given root window, registers the zone, docks a
        // couple of panels, and binds one panel back to the editor page (page as binding Source)
        // so a surviving panel pins the page exactly the way the real consumer's editor does.
        private static DockingLeakProbe LeakProbe_StockDockingGraph (Window root, AJut.UX.Controls.DockZone rootZone, FrameworkElement editorPage)
        {
            var manager = new DockingManager(root, "dock-leak-" + Guid.NewGuid().ToString("N"));
            manager.RegisterDisplayFactory<ShowRoomPanel>();
            manager.RegisterMainWindowRootDockZones(rootZone);

            ShowRoomPanel panelA = null;
            ShowRoomPanel panelB = null;

            var zone = manager.FindFirstAvailableDockZone();
            if (zone != null)
            {
                panelA = manager.DockNewPanel<ShowRoomPanel>(zone);
                panelA.Label = "Leak A";
                panelA.DockingAdapter.TitleContent = "Leak A";

                panelB = manager.DockNewPanel<ShowRoomPanel>(zone);
                panelB.Label = "Leak B";
                panelB.DockingAdapter.TitleContent = "Leak B";
            }

            // Bind a docked panel with the editor page as the binding Source. WinUI keeps a
            // binding's Source alive while the target lives, so a surviving panel keeps the page.
            if (panelA != null)
            {
                panelA.SetBinding(FrameworkElement.TagProperty, new Microsoft.UI.Xaml.Data.Binding { Source = editorPage });
            }

            return new DockingLeakProbe
            {
                Manager = manager,
                RootZone = rootZone,
                PanelA = panelA,
                PanelB = panelB,
            };
        }

        // Drives the app-side teardown (manager.Dispose + content null-out + window close),
        // snapshots weak references to every node that must collect, then drops all strong refs.
        private static DockingLeakProbeWeaks LeakProbe_TeardownDockingProbe (DockingLeakProbe probe)
        {
            probe.Manager.Dispose();

            if (probe.Frame != null)
            {
                probe.Frame.Content = null;
            }

            if (probe.EditorPage is ContentControl pageContent)
            {
                pageContent.Content = null;
            }

            if (probe.MainHost != null)
            {
                probe.MainHost.Child = null;
            }

            if (probe.ChildWindow != null)
            {
                probe.ChildWindow.Content = null;
                probe.ChildWindow.Close();
            }

            var weaks = new DockingLeakProbeWeaks
            {
                RootZone = new WeakReference(probe.RootZone),
                PanelA = probe.PanelA != null ? new WeakReference(probe.PanelA) : null,
                EditorPage = new WeakReference(probe.EditorPage),
                ChildWindow = probe.ChildWindow != null ? new WeakReference(probe.ChildWindow) : null,
            };

            // Drop every strong reference the probe holds so the only remaining handles are weak.
            probe.Manager = null;
            probe.RootZone = null;
            probe.PanelA = null;
            probe.PanelB = null;
            probe.EditorPage = null;
            probe.Frame = null;
            probe.MainHost = null;
            probe.ChildWindow = null;

            return weaks;
        }

        // Polls IsLoaded rather than subscribing Loaded (no anonymous handler, and re-entrant safe).
        private static async Task LeakProbe_WaitForLoaded (FrameworkElement element, int timeoutMs)
        {
            int waited = 0;
            while (element != null && !element.IsLoaded && waited < timeoutMs)
            {
                await Task.Delay(50);
                waited += 50;
            }
        }

        // Waits for the element to actually leave the live tree. WinUI releases its native /
        // item-container references during the Unloaded pass (a later dispatcher tick), not at
        // the moment we detach - so measuring before this completes reads a still-pinned element
        // that would collect a beat later (the source of the flaky reads).
        private static async Task LeakProbe_WaitForUnloaded (FrameworkElement element, int timeoutMs)
        {
            int waited = 0;
            while (element != null && element.IsLoaded && waited < timeoutMs)
            {
                await Task.Delay(50);
                waited += 50;
            }
        }

        // Deterministic collect: drain the dispatcher, then run a full GC, repeated. The drain
        // matters because some controls defer work via DispatcherQueue.TryEnqueue that captures
        // data we are measuring - e.g. PropertyGridItemRow.OnDataContextChanged enqueues a
        // (Normal-priority) ApplyEditorContent callback capturing the edit target -> source. Those
        // are self-cleaning once they run, but if still queued at GC time they pin what they
        // captured. Awaiting a Low-priority enqueue returns only after every higher-priority
        // queued callback has run, so the captures are gone before we measure.
        private static async Task LeakProbe_SettleAndCollectAsync ()
        {
            for (int i = 0; i < 6; ++i)
            {
                await LeakProbe_DrainDispatcherAsync();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                await Task.Delay(100);
            }
        }

        // Completes only after all higher-priority dispatcher work has run (Low runs last).
        private static Task LeakProbe_DrainDispatcherAsync ()
        {
            var done = new TaskCompletionSource();
            var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            bool enqueued = queue != null && queue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () => done.SetResult()
            );

            if (!enqueued)
            {
                done.SetResult();
            }

            return done.Task;
        }

        private static int LeakProbe_CountDescendants<T> (DependencyObject root) where T : DependencyObject
        {
            if (root == null)
            {
                return 0;
            }

            int count = root is T ? 1 : 0;
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; ++i)
            {
                count += LeakProbe_CountDescendants<T>(VisualTreeHelper.GetChild(root, i));
            }

            return count;
        }

        private static List<T> LeakProbe_CollectDescendants<T> (DependencyObject root) where T : DependencyObject
        {
            var results = new List<T>();
            LeakProbe_CollectDescendantsInto(root, results);
            return results;
        }

        private static void LeakProbe_CollectDescendantsInto<T> (DependencyObject node, List<T> results) where T : DependencyObject
        {
            if (node == null)
            {
                return;
            }

            if (node is T match)
            {
                results.Add(match);
            }

            int childCount = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < childCount; ++i)
            {
                LeakProbe_CollectDescendantsInto(VisualTreeHelper.GetChild(node, i), results);
            }
        }

        // Counts only - the GC happens in LeakProbe_SettleAndCollectAsync before this is called.
        private static string LeakProbe_ReportSurvivors (List<DockingLeakProbeWeaks> weaks)
        {
            int zones = 0;
            int panels = 0;
            int pages = 0;
            int windows = 0;
            int builtLeaves = 0;
            foreach (var w in weaks)
            {
                if (w.RootZone.IsAlive) { ++zones; }
                if (w.PanelA != null && w.PanelA.IsAlive) { ++panels; }
                if (w.EditorPage.IsAlive) { ++pages; }
                if (w.ChildWindow != null && w.ChildWindow.IsAlive) { ++windows; }
                builtLeaves += w.BuiltLeaves;
            }

            // windows is informational only - WinUI is known to keep a closed Window alive a
            // while; the dock leak is zones/panels/pages surviving past Dispose. builtLeaves
            // proves the dock structure actually realized - builtLeaves=0 means a vacuous run.
            bool leaked = zones > 0 || panels > 0 || pages > 0;
            string detail = $"zones={zones}/{weaks.Count} panels={panels}/{weaks.Count} pages={pages}/{weaks.Count} windows={windows}/{weaks.Count} builtLeaves={builtLeaves}";
            if (builtLeaves == 0)
            {
                return $"INCONCLUSIVE - dock layout never realized (builtLeaves=0): {detail}";
            }

            return leaked
                ? $"FAIL - survivors after GC: {detail}"
                : $"PASS - all collected ({detail})";
        }

        // ===========[ Leak Probe - Output ]=============================================
        // Every probe routes its result here. Newest entry is prepended so the latest run
        // is always visible at the top without scrolling.

        private void LeakProbe_AppendOutput (string title, string body)
        {
            string stamp = DateTime.Now.ToString("HH:mm:ss");
            string entry = $"[{stamp}] {title}\n    {body}";
            this.LeakProbe_Output.Text = this.LeakProbe_Output.Text.Length == 0
                ? entry
                : entry + "\n\n" + this.LeakProbe_Output.Text;
        }

        private void LeakProbe_OnCopyOutputClicked (object sender, RoutedEventArgs e)
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(this.LeakProbe_Output.Text ?? string.Empty);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        }

        private void LeakProbe_OnClearOutputClicked (object sender, RoutedEventArgs e)
        {
            this.LeakProbe_Output.Text = string.Empty;
        }

        // ===========[ Leak Probe - PropertyGrid ]=======================================
        // Builds a PropertyGrid against a fresh source object in the main window's tree,
        // lets it discover + render the source's properties, then disposes it and checks
        // that both the grid and the source object collect. A surviving source means the
        // grid (or its manager / row tree) is holding a reference past Dispose.

        private async void LeakProbe_OnPropertyGridClicked (object sender, RoutedEventArgs e)
        {
            const int kCycles = 4;
            var button = sender as Button;
            if (button != null) { button.IsEnabled = false; }
            try
            {
                var weaks = new List<PropertyGridLeakProbeWeaks>();
                for (int i = 0; i < kCycles; ++i)
                {
                    var probe = this.LeakProbe_BuildPropertyGrid();

                    // Let the grid load + apply its template and build its property-row tree.
                    await LeakProbe_WaitForLoaded(probe.Grid, 3000);
                    await Task.Delay(300);

                    FrameworkElement gridWatch = probe.Grid;

                    weaks.Add(LeakProbe_TeardownPropertyGrid(probe, this.LeakProbe_MainHostArea));
                    probe = null;

                    // Deterministic read: wait for the grid to actually unload, then let deferred
                    // WinUI cleanup run, before collecting.
                    await LeakProbe_WaitForUnloaded(gridWatch, 3000);
                    gridWatch = null;
                    await Task.Delay(150);
                }

                await LeakProbe_SettleAndCollectAsync();
                this.LeakProbe_AppendOutput("Probe PropertyGrid", LeakProbe_ReportPropertyGridSurvivors(weaks));
            }
            finally
            {
                if (button != null) { button.IsEnabled = true; }
            }
        }

        private PropertyGridLeakProbe LeakProbe_BuildPropertyGrid ()
        {
            var grid = new AJut.UX.Controls.PropertyGrid();
            var source = new ShowRoomAlpha();
            grid.SingleItemSource = source;
            this.LeakProbe_MainHostArea.Child = grid;

            return new PropertyGridLeakProbe
            {
                Grid = grid,
                Source = source,
            };
        }

        private static PropertyGridLeakProbeWeaks LeakProbe_TeardownPropertyGrid (PropertyGridLeakProbe probe, Border host)
        {
            probe.Grid.Dispose();
            host.Child = null;

            var weaks = new PropertyGridLeakProbeWeaks
            {
                Grid = new WeakReference(probe.Grid),
                Source = new WeakReference(probe.Source),
            };

            probe.Grid = null;
            probe.Source = null;
            return weaks;
        }

        // Counts only - the GC happens in LeakProbe_SettleAndCollectAsync before this is called.
        private static string LeakProbe_ReportPropertyGridSurvivors (List<PropertyGridLeakProbeWeaks> weaks)
        {
            int grids = 0;
            int sources = 0;
            foreach (var w in weaks)
            {
                if (w.Grid.IsAlive) { ++grids; }
                if (w.Source.IsAlive) { ++sources; }
            }

            bool leaked = grids > 0 || sources > 0;
            string detail = $"grids={grids}/{weaks.Count} sources={sources}/{weaks.Count}";
            return leaked
                ? $"FAIL - survivors after GC: {detail}"
                : $"PASS - all collected ({detail})";
        }

        private sealed class DockingLeakProbe
        {
            public DockingManager Manager { get; set; }
            public AJut.UX.Controls.DockZone RootZone { get; set; }
            public ShowRoomPanel PanelA { get; set; }
            public ShowRoomPanel PanelB { get; set; }
            public FrameworkElement EditorPage { get; set; }
            public Frame Frame { get; set; }
            public Border MainHost { get; set; }
            public Window ChildWindow { get; set; }
        }

        private sealed class DockingLeakProbeWeaks
        {
            public WeakReference RootZone { get; set; }
            public WeakReference PanelA { get; set; }
            public WeakReference EditorPage { get; set; }
            public WeakReference ChildWindow { get; set; }
            public int BuiltLeaves { get; set; }
        }

        private sealed class PropertyGridLeakProbe
        {
            public AJut.UX.Controls.PropertyGrid Grid { get; set; }
            public object Source { get; set; }
        }

        private sealed class PropertyGridLeakProbeWeaks
        {
            public WeakReference Grid { get; set; }
            public WeakReference Source { get; set; }
        }
    }

    // ===========[ ShowRoomTreeNode - FlatTreeListControl smoke-test source ]=========
    public class ShowRoomTreeNode : ObservableTreeNode<ShowRoomTreeNode>
    {
        public ShowRoomTreeNode(string name)
        {
            this.NodeName = name;
        }

        public string NodeName { get; }

        public ShowRoomTreeNode AddItem(string name)
        {
            var child = new ShowRoomTreeNode(name);
            this.CanHaveChildren = true;
            this.InsertChild(this.Children.Count, child);
            return child;
        }

        public override string ToString() => this.NodeName;

        public static ShowRoomTreeNode Build()
        {
            var root = new ShowRoomTreeNode("Root");
            root.CanHaveChildren = true;

            var childA = root.AddItem("Child A");
            childA.AddItem("Grandchild A1");
            childA.AddItem("Grandchild A2");

            var childB = root.AddItem("Child B");
            childB.AddItem("Grandchild B1");
            childB.AddItem("Grandchild B2");

            root.AddItem("Child C");
            return root;
        }

        // Side-by-side Forward/Reversed comparison source. Names track source order (A0, A1, ...)
        // so that the Reversed pane visually lists them highest-index-first at every level.
        public static ShowRoomTreeNode BuildSiblingOrderDemo()
        {
            var root = new ShowRoomTreeNode("Root");
            root.CanHaveChildren = true;

            var a = root.AddItem("A");
            a.AddItem("A0");
            a.AddItem("A1");
            a.AddItem("A2");

            root.AddItem("B");

            var c = root.AddItem("C");
            c.AddItem("C0");
            c.AddItem("C1");

            return root;
        }

        public static ShowRoomTreeNode BuildDragDropDemo()
        {
            var root = new ShowRoomTreeNode("Project");
            root.CanHaveChildren = true;

            var scenes = root.AddItem("Scenes");
            var scene1 = scenes.AddItem("MainScene");
            scene1.AddItem("Camera");
            scene1.AddItem("Light");
            var player = scene1.AddItem("Player");
            player.CanHaveChildren = true; // group-like: can accept children
            scenes.AddItem("MenuScene");

            var assets = root.AddItem("Assets");
            var textures = assets.AddItem("Textures");
            textures.CanHaveChildren = true; // group-like
            assets.AddItem("Models");
            assets.AddItem("Audio");

            var scripts = root.AddItem("Scripts");
            scripts.AddItem("PlayerController");
            scripts.AddItem("GameManager");

            return root;
        }
    }

    // ===========[ ShowRoomTester - PropertyGrid smoke-test source ]==================
    public enum eEditorMode { Text, Number, Color }

    // ===========[ EnumToggleStrip showcase enums ]====================================
    public enum eDisplayMode
    {
        Compact,
        Standard,
        Comfortable,
        Wide,
    }

    [Flags]
    public enum eToppings
    {
        None      = 0,
        Cheese    = 1 << 0,
        Mushrooms = 1 << 1,
        Olives    = 1 << 2,
        Pepperoni = 1 << 3,
        Pineapple = 1 << 4,
    }

    public enum eExclusionDemo
    {
        Visible1,
        [Browsable(false)]
        HiddenViaBrowsable,
        Visible2,
        [ExcludeFromSelection]
        HiddenViaAjutAttr,
        Visible3,
    }

    public class ShowRoomTester : NotifyPropertyChanged
    {

        private double m_value = 3.14;
        
        [PGEditor("Single")]
        [PGOverrideDefault(9001.0)]
        [PGGroup("Transform")]
        public double Value
        {
            get => m_value;
            set => this.SetAndRaiseIfChanged(ref m_value, value);
        }


        [PGEditor("ColorPick")]
        [PGGroup("Appearance")]
        public Color ColorValue { get; set; } = new Color { A = 255, B = 255 };
        [PGTypeAlias(typeof(ColorToStringConverter))]
        [PGGroup("Appearance")]
        public Color ColorAsString { get; set; } = new Color { A = 255, R = 255 };

        [MemberOrder(-1)] // force it to be first
        [PGLabel("Name", IconSource = "Assets/PenguinExample.png", IconMargin = 4)]
        [PGToolTip("The display name shown everywhere this object appears")]
        public string Name { get; set; } = "AJut Is Cool";
        public float? OptionalValue { get; set; }
        public ShowRoomSubObject SubObj { get; set; } = new ShowRoomSubObject();

        [PGElevateChildProperty(nameof(ShowRoomSubObject.SubObjValue))]
        [PGGroup("Transform")]
        public ShowRoomSubObject SubObjWithElevation { get; set; } = new ShowRoomSubObject();

        [PGEditor("SpecialString")]
        public TemplateSubType<string> SpecialStringEditor { get; set; } = new TemplateSubType<string>();

        public TemplateSubType<string> NormalStringEditor { get; set; } = new TemplateSubType<string>();

        // Exercises PGElevateAsParent + deferPGAttributesToParent + PGEditContextBuilder.
        // Before the fix, NumericEditor would show fallback Min/Max; after, it uses 0-100 / Step 0.5.
        [PGEditor("Single")]
        [PGEditContextBuilder("PG-Limits", "{ Min: 0.0, Max: 100.0, Step: 0.5 }")]
        [PGGroup("Transform")]
        public TemplateSubType<float> WrappedFloat { get; set; } = new TemplateSubType<float> { Value = 42.0f };

        // ------ ShowIf / HideIf demo ------
        // Change EditorMode to show/hide the matching editor row
        [PGGroup("Conditional")]
        public eEditorMode EditorMode { get; set; } = eEditorMode.Text;

        [PGShowIf(nameof(IsTextMode))]
        [PGGroup("Conditional")]
        public string TextModeValue { get; set; } = "Hello world";

        [PGShowIf(nameof(IsNumberMode))]
        [PGEditor("Single")]
        [PGGroup("Conditional")]
        public float NumberModeValue { get; set; } = 42.0f;

        [PGShowIf(nameof(IsColorMode))]
        [PGEditor("ColorPick")]
        [PGGroup("Conditional")]
        public Color ColorModeValue { get; set; } = new Color { A = 255, R = 128, G = 200, B = 255 };

        // Exercises PGShowIf + PGElevateAsParent(deferPGAttributesToParent: true)
        // with a method-based condition (matches the reported bug scenario).
        // When EditorMode is Number, this should appear alongside NumberModeValue.
        [PGShowIf(nameof(ShouldShowConditionalWrapped))]
        [PGEditor("Single")]
        [PGGroup("Conditional")]
        public TemplateSubType<float> ConditionalWrapped { get; set; } = new TemplateSubType<float> { Value = 77.0f };

        [PGHidden]
        public bool IsTextMode => this.EditorMode == eEditorMode.Text;
        [PGHidden]
        public bool IsNumberMode => this.EditorMode == eEditorMode.Number;
        [PGHidden]
        public bool IsColorMode => this.EditorMode == eEditorMode.Color;

        private bool ShouldShowConditionalWrapped () => this.EditorMode == eEditorMode.Number;

        // ------ PGCoerce demo ------
        // Value is clamped to 0-100 via custom coercion
        [PGCoerce(nameof(CoerceClampedValue))]
        [PGEditor("Single")]
        [PGGroup("Transform")]
        public float ClampedValue { get; set; } = 50.0f;

        private object CoerceClampedValue (object value)
        {
            if (value is double d)
            {
                return (float)Math.Clamp(d, 0.0, 100.0);
            }

            if (value is float f)
            {
                return Math.Clamp(f, 0.0f, 100.0f);
            }

            return value;
        }

        // ------ PGMemberOrder demo ------
        // Declared out of source order on purpose but tagged with [PGMemberOrder]. They render inside
        // the "PGMemberOrder Demo" group in order-value sequence (10, the button at 15, then 20),
        // proving a button interleaves between properties. The labels call out each expected slot. The
        // group's values are all positive, so it sorts after the unordered rows (the 0 baseline).
        [PGMemberOrder(20)]
        [PGGroup("PGMemberOrder Demo")]
        [PGLabel("Third (order=20)")]
        public string MemberOrderThird { get; set; } = "third";

        [PGMemberOrder(15)]
        [PGGroup("PGMemberOrder Demo")]
        [PGButton("Button (order=15)")]
        public void MemberOrderDemoButton ()
        {
            string temp = this.MemberOrderFirst;
            this.MemberOrderFirst = this.MemberOrderThird;
            this.MemberOrderThird = temp;
            this.RaisePropertyChanged(nameof(MemberOrderFirst));
            this.RaisePropertyChanged(nameof(MemberOrderThird));
        }

        [PGMemberOrder(10)]
        [PGGroup("PGMemberOrder Demo")]
        [PGLabel("First (order=10)")]
        public string MemberOrderFirst { get; set; } = "first";

        // ------ PGButton demo ------
        [PGButton("Reset Conditional Values")]
        [PGGroup("Conditional")]
        public void ResetConditionalValues ()
        {
            this.TextModeValue = "Hello world";
            this.NumberModeValue = 42.0f;
            this.ColorModeValue = new Color { A = 255, R = 128, G = 200, B = 255 };
        }

        // ------ PGList demos ------

        [PGList]
        [PGGroup("Lists")]
        public List<string> Tags { get; set; } = new List<string> { "alpha", "beta", "gamma" };

        [PGList]
        [PGGroup("Lists")]
        public float[] Weights { get; set; } = new float[] { 1.0f, 2.5f, 0.8f };

        [PGList(AddMethodName = nameof(AddCustomItem), RemoveMethodName = nameof(RemoveCustomItem))]
        [PGGroup("Lists")]
        public List<string> CustomListMethods { get; set; } = new List<string> { "custom-a", "custom-b" };

        private void AddCustomItem ()
        {
            this.CustomListMethods.Add($"custom-{this.CustomListMethods.Count}");
        }

        private void RemoveCustomItem (int index)
        {
            if (index >= 0 && index < this.CustomListMethods.Count)
            {
                this.CustomListMethods.RemoveAt(index);
            }
        }

        // ------ PGList with custom editor cascade demo ------

        // ------ External array replacement test ------
        // Models a real-life usage scenario where I noticed things broke.
        // Click "Swap List Externally" to replace the array.
        [PGList(AddMethodName = nameof(AddExternalItem))]
        [PGGroup("Lists")]
        public string[] ExternallyReplacedList
        {
            get => m_externalList;
            set
            {
                m_externalList = value;
                this.RaisePropertyChanged(nameof(ExternallyReplacedList));
            }
        }

        private string[] m_externalList = ["initial-a", "initial-b", "initial-c"];

        private void AddExternalItem ()
        {
            this.ExternallyReplacedList = [.. this.ExternallyReplacedList, $"added-{this.ExternallyReplacedList.Length}"];
        }

        [PGButton("Swap to 2 items")]
        [PGGroup("Lists")]
        public void SwapExternalListToTwo ()
        {
            this.ExternallyReplacedList = ["swapped-x", "swapped-y"];
        }

        [PGButton("Swap to empty")]
        [PGGroup("Lists")]
        public void SwapExternalListToEmpty ()
        {
            this.ExternallyReplacedList = [];
        }

        [PGButton("Swap to 5 items")]
        [PGGroup("Lists")]
        public void SwapExternalListToFive ()
        {
            this.ExternallyReplacedList = ["a", "b", "c", "d", "e"];
        }

        [PGList]
        [PGEditor("WidgetEditor")]
        [PGGroup("Lists")]
        public List<ShowRoomSubObject> ListWithEditor { get; set; } = new List<ShowRoomSubObject>
        {
            new ShowRoomSubObject { SubObjValue = 100 },
            new ShowRoomSubObject { SubObjValue = 200 },
        };

        // ------ PGList with elevation cascade demo ------

        [PGList]
        [PGElevateChildProperty(nameof(ShowRoomSubObject.SubObjValue))]
        [PGGroup("Lists")]
        public List<ShowRoomSubObject> ListWithElevation { get; set; } = new List<ShowRoomSubObject>
        {
            new ShowRoomSubObject { SubObjValue = 42 },
            new ShowRoomSubObject { SubObjValue = 99 },
        };

        // ------ PGList element header demo ------
        // Plain complex-element lists (no elevation, no custom editor), so each element row is expandable
        // with an otherwise-blank value column. ElementDisplayMemberName fills that column with the
        // element's Name. The first list shows the header in every state (Always); the second only while
        // the element row is collapsed (WhenCollapsed).
        [PGList(ElementDisplayMemberName = nameof(RosterEntry.Name))]
        [PGGroup("Lists")]
        public List<RosterEntry> Roster { get; set; } = new List<RosterEntry>
        {
            new RosterEntry { Name = "Tony", Age = 16 },
            new RosterEntry { Name = "Jill", Age = 24 },
            new RosterEntry { Name = "Jessup", Age = 31 },
        };

        [PGList(ElementDisplayMemberName = nameof(RosterEntry.Name), ElementDisplayMemberVisibility = eElementHeaderDisplay.WhenCollapsed)]
        [PGGroup("Lists")]
        public List<RosterEntry> RosterCollapsedHeaders { get; set; } = new List<RosterEntry>
        {
            new RosterEntry { Name = "Avery", Age = 19 },
            new RosterEntry { Name = "Blake", Age = 27 },
        };

        private class ColorToStringConverter : PropertyGridTypeAliasing<Color, string>
        {
            public override Type AliasType => typeof(string);

            public override Color ConvertFromAlias(string aliasValue)
            {
                if (CoerceUtils.TryGetColorFromString(aliasValue, out Color color))
                {
                    return color;
                }

                return default;
            }

            public override string ConvertToAlias(Color sourceValue)
            {
                return CoerceUtils.GetSmallestHexString(sourceValue);
            }
        }
    }

    public class ShowRoomSubObject : NotifyPropertyChanged
    {
        private int m_subObjValue = 9001;
        public int SubObjValue
        {
            get => m_subObjValue;
            set => this.SetAndRaiseIfChanged(ref m_subObjValue, value);
        }
    }

    // Element type for the PGList element-header demo - a plain complex object with a couple of
    // editable properties so the element rows expand and the value column is otherwise empty.
    public class RosterEntry : NotifyPropertyChanged
    {
        private string m_name;
        public string Name
        {
            get => m_name;
            set => this.SetAndRaiseIfChanged(ref m_name, value);
        }

        private int m_age;
        public int Age
        {
            get => m_age;
            set => this.SetAndRaiseIfChanged(ref m_age, value);
        }
    }

    public class TemplateSubType<T> : NotifyPropertyChanged
    {
        private T m_value;

        [PGElevateAsParent(deferPGAttributesToParent: true)]
        public T Value
        {
            get => m_value;
            set => this.SetAndRaiseIfChanged(ref m_value, value);
        }
    }


    // ===========[ ShowRoomAlpha - 5 properties ]=======
    // X=111, Y=222, Z=333 are very distinct from Beta so visual mismatch is immediately obvious.
    // Label (String) and IsActive (Bool) appear in Alpha but not Beta; if they persist after
    // switching to Beta the stale-display bug is confirmed.
    public class ShowRoomAlpha
    {
        public string Label { get; set; } = "Alpha Object";
        public float X { get; set; } = 111f;
        public float Y { get; set; } = 222f;

        [PGEditContextBuilder("PG-Limits", "{ Min: 0.0, Max: 180.0, Step: 1 }")]
        public float Z { get; set; } = 333f;

        [PGLabel("Active?", "Shows if it's active")]
        [PGToolTip("Toggle on or off", ShowName = false)]
        public bool IsActive { get; set; } = true;
    }

    [TypeId("PG-Limits")]
    public class Limits
    {
        public float Min { get; set; } = float.NegativeInfinity;
        public float Max { get; set; } = float.PositiveInfinity;
        public float Step { get; set; } = 1.0f;
        public float BigStep => this.Step * 5.0f;
        public float SmallStep => this.Step / 5.0f;

    }

    // ===========[ ShowRoomBeta - 2 properties ]=====
    // Switching Alpha→Beta: 5 rows → 2 rows, container count mismatch triggers WinUI3 recycling.
    // After switch the grid must show X=777, Y=888 - NOT Alpha's 111/222.
    public class ShowRoomBeta
    {
        public float X { get; set; } = 777f;
        public float Y { get; set; } = 888f;
    }

    // ===========[ MixedOrder - [MemberOrder] props + [PGMemberOrder] button ]=======
    // Reproduces the element-editing case: properties positioned with core [MemberOrder] across a
    // base/derived split, plus a button positioned with [PGMemberOrder]. The numbers read as one
    // sequence (1, 10, 11, 20, 21, 22) and the grid now sorts them on one axis, so the button slots
    // in after Height(21). Every row here is explicitly ordered, so there is no 0-baseline gap.
    public class MixedOrderBase : NotifyPropertyChanged
    {
        [PGEditor("Text")]
        [MemberOrder(1)]
        public string Name { get; set; } = "base name";
    }

    public class MixedOrderButtonSource : MixedOrderBase
    {
        [PGEditor("Text")]
        [MemberOrder(10)]
        public string SourcePath { get; set; } = "cfr://example";

        [PGEditor("AutoEnum")]
        [MemberOrder(11)]
        public eEditorMode Mode { get; set; } = eEditorMode.Text;

        [PGEditor("Single")]
        [MemberOrder(20)]
        public float Width { get; set; } = 5f;

        [PGEditor("Single")]
        [MemberOrder(21)]
        public float Height { get; set; } = 5f;

        // Slots in after Height (21), since all rows share one ordering axis.
        [PGButton("Reset Size From Source")]
        [PGMemberOrder(22)]
        public void ResetSize ()
        {
            this.Width = 5f;
            this.Height = 5f;
        }
    }

    // Manual-PET equivalent of MixedOrderButtonSource: a hand-assembled edit manager. There are no
    // attributes to read here, so it positions every row - properties and the button - by setting
    // MemberSortOrder directly. CreateButton makes the button row; MemberSortOrder slots it after Height.
    public class ManualMixedOrderManager : IPropertyEditManager
    {
        private readonly MixedOrderButtonSource m_data = new MixedOrderButtonSource();

        public IEnumerable<PropertyEditTarget> GenerateEditTargets ()
        {
            yield return MakeRow("Name", "Text", () => m_data.Name, v => m_data.Name = v as string ?? "", 1);
            yield return MakeRow("SourcePath", "Text", () => m_data.SourcePath, v => m_data.SourcePath = v as string ?? "", 10);
            yield return MakeRow("Width", "Single", () => m_data.Width, v => m_data.Width = System.Convert.ToSingle(v), 20);
            yield return MakeRow("Height", "Single", () => m_data.Height, v => m_data.Height = System.Convert.ToSingle(v), 21);

            var button = PropertyEditTarget.CreateButton("Reset Size From Source", () => m_data.ResetSize());
            button.MemberSortOrder = 22;
            yield return button;
        }

        private static PropertyEditTarget MakeRow (string path, string editor, PropertyEditTarget.GetValue get, PropertyEditTarget.SetValue set, int order)
        {
            return new PropertyEditTarget(path, get, set)
            {
                DisplayName = path,
                Editor = editor,
                MemberSortOrder = order,
            };
        }
    }

    // ===========[ ShowRoomPanelState - DockZone save/load state bag ]===============
    [TypeId("ShowRoomPanelState")]
    public class ShowRoomPanelState
    {
        public string Label { get; set; }
        public double Value { get; set; }
    }

    // ===========[ ShowRoomPanel - DockZone smoke-test display element ]=============
    [TypeId("ShowRoomPanel")]
    public class ShowRoomPanel : ContentControl, IDockableDisplayElement
    {
        // ===========[ Fields ]===================================================
        private string m_label = "Panel";
        private double m_value = 0.0;

        private TextBlock m_labelBlock;
        private TextBox m_valueBox;
        private CheckBox m_blockCloseCheckBox;

        // ===========[ Construction ]=============================================
        public ShowRoomPanel()
        {
            this.HorizontalContentAlignment = HorizontalAlignment.Center;
            this.VerticalContentAlignment = VerticalAlignment.Center;
            this.Padding = new Thickness(8);
            this.BuildContent();
        }

        // ===========[ Properties ]===============================================

        public string Label
        {
            get => m_label;
            set
            {
                m_label = value;
                if (m_labelBlock != null)
                {
                    m_labelBlock.Text = value;
                }
            }
        }

        public double Value
        {
            get => m_value;
            set
            {
                m_value = value;
                if (m_valueBox != null)
                {
                    m_valueBox.Text = value.ToString("G", CultureInfo.InvariantCulture);
                }
            }
        }

        public DockingContentAdapterModel DockingAdapter { get; private set; }

        // ===========[ IDockableDisplayElement ]===================================

        public virtual void Setup(DockingContentAdapterModel adapter)
        {
            this.DockingAdapter = adapter;
            adapter.CanClose += (s, e) =>
            {
                if (m_blockCloseCheckBox?.IsChecked == true)
                {
                    e.IsReadyToClose = false;
                }
            };
        }

        public object GenerateState()
            => new ShowRoomPanelState { Label = m_label, Value = m_value };

        public void ApplyState(object state)
        {
            if (state is not ShowRoomPanelState typed)
            {
                return;
            }

            this.Label = typed.Label;
            if (this.DockingAdapter != null)
            {
                this.DockingAdapter.TitleContent = typed.Label;
            }

            this.Value = typed.Value;
        }

        // ===========[ UI Building ]===============================================

        private void BuildContent()
        {
            m_labelBlock = new TextBlock
            {
                Text = m_label,
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4),
            };

            m_valueBox = new TextBox
            {
                Text = m_value.ToString("G", CultureInfo.InvariantCulture),
                PlaceholderText = "Value (double)",
                Width = 140,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4),
            };
            m_valueBox.LostFocus += (s, e) =>
            {
                if (double.TryParse(
                    m_valueBox.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double val))
                {
                    m_value = val;
                }
                else
                {
                    m_valueBox.Text = m_value.ToString("G", CultureInfo.InvariantCulture);
                }
            };

            m_blockCloseCheckBox = new CheckBox
            {
                Content = "Block close",
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            panel.Children.Add(m_labelBlock);
            panel.Children.Add(m_valueBox);
            panel.Children.Add(m_blockCloseCheckBox);
            this.Content = panel;
        }
    }

    [TypeId("ShowRoomPanel-single-inst")]
    public class SingleInstanceShowRoomPanel : ShowRoomPanel
    {
        public override void Setup(DockingContentAdapterModel adapter)
        {
            base.Setup(adapter);
            adapter.HideDontClose = true;
        }
    }

    /// <summary>
    /// A single-instance panel that is hidden from the toolbar and menu UI.
    /// Models an infrastructure panel that is always present in the layout but should
    /// not appear in the "View" menu or the DockPanelAddRemoveToolbar.
    /// </summary>
    [TypeId("ShowRoomPanel-hidden-infra")]
    public class HiddenInfraShowRoomPanel : ShowRoomPanel
    {
        public override void Setup (DockingContentAdapterModel adapter)
        {
            base.Setup(adapter);
            this.Label = "Hidden Infra";
            adapter.TitleContent = "Hidden Infra";
        }
    }

    // ===========[ Wrapped-Mode matrix source for PGShowIf repro ]=====================
    // Reproduces the shape where an enum-like property lives behind a wrapper that
    // raises INPC on its Value (the outer source never fires), several PGShowIf
    // predicates read Mode.Value, and the rows must re-evaluate visibility without
    // rebuilding the grid.

    public enum eMatrixMode { A, B, C }

    public class WrappedModeValue<T> : NotifyPropertyChanged
    {
        private T m_value;

        [PGElevateAsParent(deferPGAttributesToParent: true)]
        public T Value
        {
            get => m_value;
            set => this.SetAndRaiseIfChanged(ref m_value, value);
        }

        public WrappedModeValue () { }
        public WrappedModeValue (T value) { m_value = value; }
    }

    public class WrappedModeMatrixSource
    {
        public WrappedModeValue<eMatrixMode> Mode { get; set; } = new WrappedModeValue<eMatrixMode>(eMatrixMode.A);

        [PGGroup("GroupA")]
        [PGShowIf(nameof(IsModeA))]
        public WrappedModeValue<string> A_GroupedColor { get; set; } = new WrappedModeValue<string>("#FF0000");

        [PGShowIf(nameof(IsModeB))]
        public WrappedModeValue<string> B_UngroupedColor { get; set; } = new WrappedModeValue<string>("#00FF00");

        [PGEditor("Single")]
        [PGEditContextBuilder("PG-Limits", "{ Min: 0.0, Max: 1.0, Step: 0.05 }")]
        [PGShowIf(nameof(IsModeB))]
        public WrappedModeValue<float> B_WithEditCtx { get; set; } = new WrappedModeValue<float>(0.5f);

        [PGShowIf(nameof(IsModeC))]
        public WrappedModeValue<string> C_First { get; set; } = new WrappedModeValue<string>("#0000FF");

        [PGShowIf(nameof(IsModeC))]
        public WrappedModeValue<string> C_Second { get; set; } = new WrappedModeValue<string>("#FFFF00");

        private bool IsModeA () => this.Mode.Value == eMatrixMode.A;
        private bool IsModeB () => this.Mode.Value == eMatrixMode.B;
        private bool IsModeC () => this.Mode.Value == eMatrixMode.C;
    }
}
