namespace AJut.Application.SourceControl
{
    public class PerforceConnection
    {
        public bool IsConnected { get; internal set; }

        public string WorkspaceName { get; internal set; }

        public string WorkspaceRootPath { get; internal set; }
    }
}
