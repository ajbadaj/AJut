namespace AJut.TestApp.WPF
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using AJut;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Docking;
    using AJut.UX.PropertyInteraction;
    using AJut.Storage;
    using AJut.TestApp.WPF.DockTest;
    using AJut.TestApp.WPF.StackNavTest;
    using DPUtils = AJut.UX.DPUtils<MainWindow>;


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public TestTreeItem[] Items { get; }

        public static readonly DependencyProperty SelectedItemsProperty = DPUtils.Register(_ => _.SelectedItems);
        public List<TestTreeItem> SelectedItems
        {
            get => (List<TestTreeItem>)this.GetValue(SelectedItemsProperty);
            set => this.SetValue(SelectedItemsProperty, value);
        }

        TestTreeItem c, g;
        public MainWindow()
        {
            this.ToolWindows = new WindowManager(this);
            this.PropertyGridItems = new SelfAwarePropertyGridSource[]
            {
                new SelfAwarePropertyGridSource () { DogsName = "Wart", DogsAge = 18 },
                new SelfAwarePropertyGridSource () { DogsName = "Bandit", DogsAge = 7 },
                new SelfAwarePropertyGridSource () { DogsName = "Brosephina", DogsAge = 3 },
            };

            _Test<string>();
            void _Test<T> ()
            {
                Console.WriteLine(typeof(T).Name);
            }
            var a = new TestTreeItem();
            var b = new TestTreeItem();
            c = _SetExpanded(new TestTreeItem());

            this.Items = new[] { a, b, c };

                var d = c.Add();
                var e = _SetExpanded(c.Add());
                    var f = _SetGroup(e.Add());
                    g = e.Add();
                    var h = _SetExpanded(e.Add());
                    var i = e.Add();
                    var j = e.Add();
                    var k = e.Add();
                var l = c.Add();
                var m = _SetExpanded(c.Add());
                    var n = m.Add();

            this.InitializeComponent();

            this.DockingManager = new DockingManager(this, "Main");
            this.DockingManager.RegisterRootDockZones(this.LeftDockZone, this.RightDockZone);
            this.LeftDockZone.GenerateAndAdd<DockTestOne>();
            this.LeftDockZone.GenerateAndAdd<DockTestTwo>();

            this.RightDockZone.SetSplitChildZones(eDockOrientation.Vertical, new DockZone(), new DockZone());
            this.RightDockZone.AnteriorZone.GenerateAndAdd<DockTestOne>();
            this.RightDockZone.PosteriorZone.GenerateAndAdd<DockTestTwo>();
            this.RightDockZone.PosteriorZone.GenerateAndAdd<DockTestOne>();
            this.RightDockZone.PosteriorZone.GenerateAndAdd<DockTestTwo>();

            this.Navigator = new StackNavFlowController(this);
            this.Navigator.GenerateAndPushDisplay<FirstDisplay>();

            TestTreeItem _SetExpanded (TestTreeItem item)
            {
                item.CanHaveChildren = true;
                return item;
            }

            TestTreeItem _SetGroup (TestTreeItem item)
            {
                item.IsGroup = true;
                item.Title = $"Group {item.Title}";
                return item;
            }
        }
        
        public static readonly DependencyProperty DockingManagerProperty = DPUtils.Register(_ => _.DockingManager);
        public DockingManager DockingManager
        {
            get => (DockingManager)this.GetValue(DockingManagerProperty);
            set => this.SetValue(DockingManagerProperty, value);
        }

        public static readonly DependencyProperty NavigatorProperty = DPUtils.Register(_ => _.Navigator);
        public StackNavFlowController Navigator
        {
            get => (StackNavFlowController)this.GetValue(NavigatorProperty);
            set => this.SetValue(NavigatorProperty, value);
        }

        public static readonly DependencyProperty ToolWindowsProperty = DPUtils.Register(_ => _.ToolWindows);
        public WindowManager ToolWindows
        {
            get => (WindowManager)this.GetValue(ToolWindowsProperty);
            set => this.SetValue(ToolWindowsProperty, value);
        }

        public static readonly DependencyProperty EditTextBlockTextProperty = DPUtils.Register(_ => _.EditTextBlockText);
        public string EditTextBlockText
        {
            get => (string)this.GetValue(EditTextBlockTextProperty);
            set => this.SetValue(EditTextBlockTextProperty, value);
        }

        public static readonly DependencyProperty FloatValueProperty = DPUtils.Register(_ => _.FloatValue, 2.1f);
        public float FloatValue
        {
            get => (float)this.GetValue(FloatValueProperty);
            set => this.SetValue(FloatValueProperty, value);
        }

        public static readonly DependencyProperty ColorValueProperty = DPUtils.Register(_ => _.ColorValue, Colors.AntiqueWhite);
        public Color ColorValue
        {
            get => (Color)this.GetValue(ColorValueProperty);
            set => this.SetValue(ColorValueProperty, value);
        }

        public static readonly DependencyProperty PropertyGridItemsProperty = DPUtils.Register(_ => _.PropertyGridItems);
        public SelfAwarePropertyGridSource[] PropertyGridItems
        {
            get => (SelfAwarePropertyGridSource[])this.GetValue(PropertyGridItemsProperty);
            set => this.SetValue(PropertyGridItemsProperty, value);
        }

        private void Test_Loaded (object sender, RoutedEventArgs e)
        {
            var test = (TextBlock)sender;
            var result = test.GetFirstParentOf<DockPanel>(eTraversalTree.Both);
            var result2 = test.GetFirstParentOf<FlatTreeListControl>(eTraversalTree.Both);
            Console.WriteLine($"{result} -- {result2}");
        }

        public static readonly RoutedUICommand DoStuffCommand = new RoutedUICommand("Do stuff", nameof(DoStuffCommand), typeof(MainWindow), new InputGestureCollection
        {
            new KeyGesture(Key.T, ModifierKeys.Control | ModifierKeys.Shift)
        });

        private void DrawingDisplayer_OnDrawingCreated (object sender, RoutedEventArgs<PathGeometry> e)
        {
            this.DrawingDisplayer.Children.Add(new Viewbox 
                {
                    Stretch = Stretch.None,
                    Child = new Path
                    {
                        Stroke = Brushes.Black,
                        StrokeThickness = 2.0,
                        Data = e.Value,
                    }
                }
            );
        }

        private void ShowAllToolWindows_OnClick (object sender, RoutedEventArgs e)
        {
            if (this.ToolWindows.Count == 1)
            {
                this.ToolWindows.Track(_Create("Window 1"));
                this.ToolWindows.Track(_Create("Window 2"));
                this.ToolWindows.Track(_Create("Window 3"));
            }
            else
            {
                this.ToolWindows.ShowAllWindows();
            }

            TestWindow _Create(string text)
            {
                var window = new TestWindow { Owner = this, Title = text, Text = text };
                window.Show();
                return window;
            }
        }

        private void HideAllToolWindows_OnClick (object sender, RoutedEventArgs e)
        {
            this.ToolWindows.HideAllWindows();
        }

        private void DoStuff_OnExecuted (object sender, ExecutedRoutedEventArgs e)
        {
            MessageBox.Show("CTRL + Shift + T pressed!");
        }

        private void WindowList_OnDoubleClick (object sender, MouseButtonEventArgs e)
        {
            if (this.ToolWindowDisplayList.SelectedItem is Window w)
            {
                this.ToolWindows.BringToFront(w);
            }
        }


        public static readonly DependencyProperty WatchedValueCounterProperty = DPUtils.Register(_ => _.WatchedValueCounter);
        public int WatchedValueCounter
        {
            get => (int)this.GetValue(WatchedValueCounterProperty);
            set => this.SetValue(WatchedValueCounterProperty, value);
        }

        private DPWatcher m_watcher;
        private void StartWatchingWithDPWatcher_OnClick (object sender, RoutedEventArgs e)
        {
            m_watcher = new DPWatcher(this.TextBoxToStalk);
            m_watcher.Watch(TextBox.TextProperty);
            m_watcher.WatchedValueChanged += _OnWatchedValueChanged;

            void _OnWatchedValueChanged (object _sender, EventArgs _e)
            {
                ++this.WatchedValueCounter;
            }
        }

        private void FlatTree_OnItemAdded (object sender, EventArgs<FlatTreeListControl.Item> e)
        {
            if (((TestTreeItem)e.Value.Source).IsGroup)
            {
                e.Value.IsSelectable = false;
            }
        }
        private void AddNodeToTopSelected_OnClick (object sender, RoutedEventArgs e)
        {
            this.TopTree.SelectedItem?.InsertChild(0, new TestTreeItem());
        }

        private void FlatTreeList_OnTestExternalSelectionClick (object sender, RoutedEventArgs e)
        {
            this.TopTree.SelectedItems.Clear();
            this.TopTree.SelectedItems.Add(this.Items.OfType<IObservableTreeNode>().FirstOrDefault());
            this.TopTree.Focus();
        }

        private void FlatTreeList_OnTestExternalSelectionClearAllClick (object sender, RoutedEventArgs e)
        {
            this.TopTree.SelectedItems.Clear();
            this.TopTree.Focus();
        }

        private void ExtensionGrid_OnClick (object sender, MouseButtonEventArgs args)
        {
            MessageBox.Show("Tada");
        }

        public static Random kRNG = new Random(DateTime.Now.Millisecond);
        private void SetDogAge_OnClick (object sender, RoutedEventArgs e)
        {
            ((SelfAwarePropertyGridSource)((FrameworkElement)e.OriginalSource).DataContext).DogsAge = kRNG.Next(1, 18);
        }

        private async void DoReorder_OnExecuted (object sender, ExecutedRoutedEventArgs e)
        {
            await DragDropElement.DoDragReorder((UIElement)e.OriginalSource, (ActiveDragTracking)e.Parameter);
        }

        private void CreateCustomChromeWindow_OnClick (object sender, RoutedEventArgs e)
        {
            ExampleCustomChromeFullscreenWindow w = new ExampleCustomChromeFullscreenWindow { Owner = this };
            w.Show();
        }

        private void Test1_OnClick (object sender, RoutedEventArgs e)
        {
            MessageBox.Show(((FrameworkElement)sender).DataContext.ToString());
        }

        private void SynchFlatTreeListSelection_OnClick (object sender, RoutedEventArgs e)
        {
            var selection = new[] { c, g };
            this.TopTree.SelectedItems.ResetTo(selection);
            this.BottomTree.SelectedItems.ResetTo(selection);
        }
    }

    //#error TODO, add some kind of selection report so I can see if this is working, then trigger some manual IsSelected\IsExpanded changes
    [DebuggerDisplay("{Title}")]
    public class TestTreeItem : ObservableTreeNode<TestTreeItem>
    {
        public TestTreeItem()
        {
            this.CanHaveChildren = false;
        }

        private static char counter = 'A';
        public string Title { get; set; } = $"Node {counter++}";

        private bool m_otherThing;
        public bool OtherThing
        {
            get => m_otherThing;
            set => this.SetAndRaiseIfChanged(ref m_otherThing, value);
        }
        public bool IsGroup { get; internal set; }

        public TestTreeItem Add()
        {
            this.CanHaveChildren = true;
            var newChild = new TestTreeItem();
            newChild.Parent = this;
            this.AddChild(newChild);
            return newChild;
        }
    }

    public class SelfAwarePropertyGridSource : NotifyPropertyChanged, IPropertyEditManager
    {
        private string m_dogsName;
        [PGEditor("Text")]
        public string DogsName
        {
            get => m_dogsName;
            set => this.SetAndRaiseIfChanged(ref m_dogsName, value);
        }

        private int m_dogsAge;
        [PGEditor("Number")]
        public int DogsAge
        {
            get => m_dogsAge;
            set => this.SetAndRaiseIfChanged(ref m_dogsAge, value);
        }

        public TestTreeItem NullThing { get; set; }

        public IEnumerable<PropertyEditTarget> GenerateEditTargets ()
        {
            foreach (var p in PropertyEditTarget.GenerateForPropertiesOf(this))
            {
                yield return p;
            }

            yield return new PropertyEditTarget("Name Alias", () => this.DogsName, (v) => this.DogsName = (string)v) { Editor = "Text", AdditionalEvalTargets = new[] { nameof(DogsName) } };
        }
    }
}
