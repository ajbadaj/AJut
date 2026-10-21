namespace AJutShowRoomWinUI.UI.Samples
{
    using AJut.Storage;

    /// <summary>
    /// The FlatTreeListControl smoke-test source: a named tree node with builders for each of the tree demos.
    /// </summary>
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
}
