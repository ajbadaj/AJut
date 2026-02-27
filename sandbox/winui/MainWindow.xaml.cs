// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AJutShowRoomWinUI
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
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

    public sealed partial class MainWindow : Window
    {
        private const string kThemeColorsSettingsKey = "_Hidden_theme_colors";
        private readonly ObservableCollection<string> m_themeColorResolver = new ObservableCollection<string>();

        // ===========[ Dock Zone test state ]============================================
        private DockingManager m_dockingManager;
        private int m_panelCounter = 3; // A=1, B=2 created at startup; C onward via button
        private bool m_dockZoneInitialized = false;

        public MainWindow (WindowManager manager, AppThemeManager themeManager)
        {
            this.Application = App.Instance;
            manager.Setup(this);
            themeManager.ApplyTheme(this);
            this.AppWindow.SetIcon("Assets/app.ico");
            this.InitializeComponent();
            this.Root.SetupFor(this);

            // ToggleStrip demo items
            this.ToggleStripSingle.ItemsSource = new[] { "Option A", "Option B", "Option C" };
            this.ToggleStripMulti.ItemsSource = new[] { "Bold", "Italic", "Underline", "Strikethrough" };
            this.ToggleStripFreeform.ItemsSource = new[] { "First", "Second", "Third" };
            this.ToggleStripCustom.ItemsSource = new[] { "Red", "Green", "Blue" };

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
                        value = ColorHelper.GetSmallestHexString(scb.Color);
                        this.ColorPicker.Color = scb.Color;
                    }
                    else if (obj is Color color)
                    {
                        value = ColorHelper.GetSmallestHexString(color);
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
        public ShowRoomTester TestObj { get; } = new ShowRoomTester();

        // ===========[ Dock Zone tab ]====================================================

        private void DockZoneTab_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (m_dockZoneInitialized)
            {
                return;
            }

            m_dockZoneInitialized = true;

            TypeIdRegistrar.RegisterTypeId<ShowRoomPanel>("ShowRoomPanel");
            TypeIdRegistrar.RegisterTypeId<ShowRoomPanelState>("ShowRoomPanelState");

            m_dockingManager = new DockingManager(this, "ShowRoomDock");
            m_dockingManager.FixedTearoffWindowTitle = "Show Room";
            m_dockingManager.RegisterMainWindowRootDockZones(this.TestDockZone);
            m_dockingManager.RegisterDisplayFactory<ShowRoomPanel>();
            

            var zone = m_dockingManager.FindFirstAvailableDockZone();
            if (zone != null)
            {
                var panelA = (ShowRoomPanel)m_dockingManager.BuildNewDisplayElement(typeof(ShowRoomPanel));
                panelA.Label = "Panel A";
                panelA.DockingAdapter.TitleContent = "Panel A";
                zone.AddDockedContent(panelA);

                var panelB = (ShowRoomPanel)m_dockingManager.BuildNewDisplayElement(typeof(ShowRoomPanel));
                panelB.Label = "Panel B";
                panelB.DockingAdapter.TitleContent = "Panel B";
                zone.AddDockedContent(panelB);
            }
        }

        private void DockZone_OnAddPanelClicked(object sender, RoutedEventArgs e)
        {
            var zone = m_dockingManager?.FindFirstAvailableDockZone();
            if (zone == null)
            {
                return;
            }

            string name = m_panelCounter <= 26
                ? $"Panel {(char)('A' + m_panelCounter - 1)}"
                : $"Panel #{m_panelCounter}";
            ++m_panelCounter;

            var panel = (ShowRoomPanel)m_dockingManager.BuildNewDisplayElement(typeof(ShowRoomPanel));
            panel.Label = name;
            panel.DockingAdapter.TitleContent = name;
            zone.AddDockedContent(panel);
        }

        private void DockZone_OnSaveLayoutClicked(object sender, RoutedEventArgs e)
        {
            m_dockingManager?.SaveDockLayoutToPersistentStorage();
        }

        private void DockZone_OnLoadLayoutClicked(object sender, RoutedEventArgs e)
        {
            m_dockingManager?.ReloadDockLayoutFromPersistentStorage();
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
    }

    // ===========[ ShowRoomTester - PropertyGrid smoke-test source ]==================
    public class ShowRoomTester
    {
        [PGEditor("Single")]
        public double Value { get; set; } = 3.14;
        [PGEditor("ColorPick")]
        public Color ColorValue { get; set; } = new Color { A = 255, B = 255 };
        public string Name { get; set; } = "AJut Is Cool";
        public ShowRoomSubObject SubObj { get; set; } = new ShowRoomSubObject();

        [PGElevateChildProperty(nameof(ShowRoomSubObject.SubObjValue))]
        public ShowRoomSubObject SubObjWithElevation { get; set; } = new ShowRoomSubObject();
    }

    public class ShowRoomSubObject
    {
        public int SubObjValue { get; set; } = 9001;
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
    public sealed class ShowRoomPanel : ContentControl, IDockableDisplayElement
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

        public void Setup(DockingContentAdapterModel adapter)
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
}
