namespace Scaffold.SceneFlow
{
    public readonly struct SceneFlowLoadOptions
    {
        public static SceneFlowLoadOptions Default => new SceneFlowLoadOptions(true);

        public SceneFlowLoadOptions(bool manageBootstrapShell)
        {
            ManageBootstrapShell = manageBootstrapShell;
        }

        public bool ManageBootstrapShell { get; }
    }
}
