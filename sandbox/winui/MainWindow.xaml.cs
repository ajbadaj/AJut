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

        // ===========[ Dock Zone test state ]============================================
        private DockingManager m_dockingManager;
        private bool m_dockZoneInitialized = false;

        public MainWindow (WindowManager manager, AppThemeManager themeManager)
        {
            this.Application = App.Instance;
            manager.Setup(this);
            themeManager.ApplyTheme(this);
            this.AppWindow.SetIcon("Assets/app.ico");
            this.InitializeComponent();
            this.Root.SetupFor(this);
            this.TestPropertyGrid.PropertyTreeChanged += this.TestPropertyGrid_OnPropertyTreeChanged;
            this.SetPGSource(m_alphaObj);

            // ToggleStrip demo items
            this.ToggleStripSingle.ItemsSource = new[] { "Option A", "Option B", "Option C" };
            this.ToggleStripMulti.ItemsSource = new[] { "Bold", "Italic", "Underline", "Strikethrough" };
            this.ToggleStripFreeform.ItemsSource = new[] { "First", "Second", "Third" };
            this.ToggleStripCustom.ItemsSource = new[] { "Red", "Green", "Blue" };

            // ToggleStrip enum bug repro
            this.ToggleStripEnumBugRepro.ItemsSource = Enum.GetValues<eEditorMode>();

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
        private readonly WrappedModeMatrixSource m_wrappedMatrixObj = new WrappedModeMatrixSource();
        private object m_currentPGTestObj;

        private void SetPGSource (object obj)
        {
            m_currentPGTestObj = obj;
            this.TestPropertyGrid.SingleItemSource = obj;
            this.ResetJsonDisplay();
        }

        private void PGSource_OnAlphaClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_alphaObj);
        private void PGSource_OnBetaClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_betaObj);
        private void PGSource_OnComplexClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_complexObj);
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
            this.LeakProbe_DockingManagerStatus.Text = "running...";
            bool collected = LeakProbe_BuildAndDisposeDockingManager(this);
            this.LeakProbe_DockingManagerStatus.Text = collected
                ? "PASS - DockingManager was collected after Dispose"
                : "FAIL - DockingManager survived Dispose + GC (something is still pinning it)";
        }

        private void LeakProbe_OnWindowManagerClicked (object sender, RoutedEventArgs e)
        {
            this.LeakProbe_WindowManagerStatus.Text = "running...";
            bool collected = LeakProbe_BuildAndDisposeWindowManager(this);
            this.LeakProbe_WindowManagerStatus.Text = collected
                ? "PASS - WindowManager was collected after Dispose"
                : "FAIL - WindowManager survived Dispose + GC (something is still pinning it)";
        }

        private void LeakProbe_OnCycleClicked (object sender, RoutedEventArgs e)
        {
            this.LeakProbe_CycleStatus.Text = "running...";
            int passes = 0;
            for (int i = 0; i < 5; ++i)
            {
                if (LeakProbe_BuildAndDisposeDockingManager(this))
                {
                    ++passes;
                }
            }

            this.LeakProbe_CycleStatus.Text = passes == 5
                ? "PASS - all 5 cycles collected cleanly"
                : $"FAIL - only {passes}/5 cycles collected (later iterations should still pass if the first one does)";
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
    /// Mirrors the Call Familiar pattern where infrastructure panels (outliner,
    /// aspect root) are always present in the layout but should not appear in
    /// the "View" menu or the DockPanelAddRemoveToolbar.
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
