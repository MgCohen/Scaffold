namespace Scaffold.LiveOps.Editor
{
    /// <summary>Sample: paths for merging package Backend~ trees into repo-root LiveOps/.</summary>
    internal readonly struct LiveOpsBackendInstallContext
    {
        internal LiveOpsBackendInstallContext(string projectRoot, string destLive, string hostBack)
        {
            ProjectRoot = projectRoot;
            DestLive = destLive;
            HostBack = hostBack;
        }

        internal string ProjectRoot { get; }
        internal string DestLive { get; }
        internal string HostBack { get; }
    }
}
