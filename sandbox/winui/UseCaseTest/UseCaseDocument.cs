namespace AJutShowRoomWinUI.UseCaseTest
{
    // ===========[ UseCaseDocument ]=============================================
    // The long-lived "document" model - the tool tree the editor edits. This is the
    // crux of the leak harness: in a real app the document/project model outlives the
    // editor views opened on it. Here it is a process-lifetime singleton, so every editor
    // session binds its flat tree and property grid to the SAME long-lived source objects.
    //
    // That is what turns a framework teardown gap into a visible leak: if a control does not
    // sever its subscriptions to these source objects when it is dropped, the long-lived
    // document keeps the whole editor graph rooted - and accumulates one rooted editor per
    // open/close cycle, exactly the ratchet seen in the consumer heap diff.
    public sealed class UseCaseDocument
    {
        private static UseCaseDocument g_shared;

        public static UseCaseDocument Shared => g_shared ??= CreateDefault();

        public ToolItemNode ToolTreeRoot { get; private set; }

        public ToolItem FirstTool => (this.ToolTreeRoot != null && this.ToolTreeRoot.Children.Count > 0)
            ? this.ToolTreeRoot.Children[0].Item
            : null;

        private static UseCaseDocument CreateDefault ()
        {
            var root = new ToolItemNode("(document)") { CanHaveChildren = true };
            for (int i = 1; i <= 6; ++i)
            {
                root.AddChildItem($"Tool {i}", new ToolItem($"Tool {i}"));
            }

            return new UseCaseDocument { ToolTreeRoot = root };
        }
    }
}
