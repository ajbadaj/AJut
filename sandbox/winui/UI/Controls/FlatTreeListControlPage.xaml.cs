namespace AJutShowRoomWinUI.UI.Controls
{
    using System;
    using System.Linq;
    using AJut.UX;
    using AJutShowRoomWinUI.UI.Samples;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// FlatTreeListControl demos: the plain tree, drag and drop reordering with SetSelection, and the Forward
    /// and Reversed sibling display orders side by side.
    /// </summary>
    public sealed partial class FlatTreeListControlPage : Page
    {
        public FlatTreeListControlPage ()
        {
            this.InitializeComponent();
        }

        // ===========[ Properties ]======================================================

        public ShowRoomTreeNode TreeRoot { get; } = ShowRoomTreeNode.Build();
        public ShowRoomTreeNode DragDropTreeRoot { get; } = ShowRoomTreeNode.BuildDragDropDemo();

        // Two independent source trees of the same shape - one rendered Forward, one Reversed -
        // so I can eyeball the layers-panel ordering and confirm drag-drop InsertIndex stays source-space.
        public ShowRoomTreeNode SiblingOrderForwardRoot { get; } = ShowRoomTreeNode.BuildSiblingOrderDemo();
        public ShowRoomTreeNode SiblingOrderReversedRoot { get; } = ShowRoomTreeNode.BuildSiblingOrderDemo();

        /// <summary>The markup for the drag and drop tree, shown beside the demo.</summary>
        public string DemoCode { get; } = @"<ajut:FlatTreeListControl x:Name=""DragDropTree""
                          Root=""{x:Bind DragDropTreeRoot}""
                          IncludeRoot=""True""
                          StartItemsExpanded=""True""
                          IsDragDropReorderEnabled=""True""
                          SelectionMode=""Extended""
                          SiblingDisplayOrder=""Forward""
                          SelectionChanged=""DragDropTree_OnSelectionChanged""
                          DragDropReorderRequested=""SiblingOrderTree_OnReorder"">
    <ajut:FlatTreeListControl.ItemTemplate>
        <DataTemplate>
            <TextBlock Text=""{Binding Path=Source.NodeName}"" VerticalAlignment=""Center""/>
        </DataTemplate>
    </ajut:FlatTreeListControl.ItemTemplate>
</ajut:FlatTreeListControl>";

        // ===========[ Sibling order ]===================================================

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
    }
}
