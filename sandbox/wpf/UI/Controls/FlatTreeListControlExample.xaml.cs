namespace TheAJutShowRoom.UI.Controls
{
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.Storage;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<FlatTreeListControlExample>;

    public partial class FlatTreeListControlExample : UserControl
    {
        public FlatTreeListControlExample()
        {
            var a = _SetExpanded(new TestTreeItem());
                var b = a.Add();
                var c = _SetExpanded(b.Add());
                    var d = c.Add();
                    var e = _SetExpanded(c.Add());
                        var f = _SetGroup(e.Add());
                        var g = e.Add();
                        var h = _SetExpanded(e.Add());
                        var i = e.Add();
                        var j = e.Add();
                        var k = e.Add();
                    var l = c.Add();
                    var m = _SetExpanded(c.Add());
                        var n = m.Add();
            this.Root = a;
            this.InitializeComponent();

            TestTreeItem _SetExpanded (TestTreeItem item)
            {
                return item;
            }

            TestTreeItem _SetGroup (TestTreeItem item)
            {
                item.IsGroup = true;
                item.Title = $"Group {item.Title}";
                return item;
            }
        }

        public static readonly DependencyProperty NavigatorProperty = DPUtils.Register(_ => _.Navigator);
        public StackNavAdapter Navigator
        {
            get => (StackNavAdapter)this.GetValue(NavigatorProperty);
            set => this.SetValue(NavigatorProperty, value);
        }


        public static readonly DependencyProperty RootProperty = DPUtils.Register(_ => _.Root);
        public TestTreeItem Root
        {
            get => (TestTreeItem)this.GetValue(RootProperty);
            set => this.SetValue(RootProperty, value);
        }

        private void PopupCodeExample_OnClick (object sender, RoutedEventArgs e)
        {
            this.Navigator.ShowPopover(new BasicCodeDisplayPopover(
@"<ajut:FlatTreeListControl Root=""{Binding ElementName=Self, Path=Root}"" TabbingSize=""10"" Margin=""10"">
    <ajut:FlatTreeListControl.Resources>
        <DataTemplate DataType=""{x:Type local:TestTreeItem}"">
            <DockPanel>
                <CheckBox DockPanel.Dock=""Right"" IsChecked=""{Binding OtherThing}"" />
                <TextBlock Text=""{Binding Title}""/>
            </DockPanel>
        </DataTemplate>
    </ajut:FlatTreeListControl.Resources>
</ajut:FlatTreeListControl>")
                );
        }
    }



    [DebuggerDisplay("{Title}")]
    public class TestTreeItem : ObservableTreeNode<TestTreeItem>
    {
        public TestTreeItem ()
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

        public TestTreeItem Add ()
        {
            this.CanHaveChildren = true;
            var newChild = new TestTreeItem();
            newChild.Parent = this;
            this.AddChild(newChild);
            return newChild;
        }
    }
}
